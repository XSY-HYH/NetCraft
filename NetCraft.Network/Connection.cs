using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NetCraft.Logging;
using NetCraft.Network.Protocol;
using NetCraft.Registry;

namespace NetCraft.Network;

//Connection 协议连接对应原版 net.minecraft.network.Connection
//用双向 Stream 替代 netty Channel 持有当前 inbound/outbound 协议状态
//提供 Send/Receive/Tick/Disconnect 完整生命周期对齐原版 API
//加密压缩通过阈值开关控制原版 netty pipeline 这里简化为同步读写
public sealed class Connection : IDisposable
{
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly PacketFlow _receiving;
    private readonly PacketProcessor _processor;
    private readonly ConcurrentQueue<Action<Connection>> _pendingActions = new();

    //读循环后台线程阻塞 ReceiveRaw 流结束即 Disconnect
    //真实 TCP 场景由 ConnectionAcceptor 调 StartReadLoop 启动测试不调保持手动 Receive
    private Thread? _readThread;
    private volatile bool _readLoopRunning;

    //Transport 真实 TCP 传输层 Dispose 时关闭 TcpClient 触发读循环退出
    //测试用 QueueStream 不设置 Dispose 不关任何流
    private IDisposable? _transport;

    //RawPackets 读循环入队的原始 payload 队列解码延迟到主线程 Tick
    //避免读循环用旧协议解码新协议包的竞态如 Handshake 切 Status 时 StatusRequest 被误用 Handshake 解码
    private readonly ConcurrentQueue<byte[]> _rawPackets = new();

    private INonGenericProtocol? _inboundProtocol;
    private INonGenericProtocol? _outboundProtocol;
    private PacketListener? _packetListener;
    private PacketListener? _disconnectListener;
    private DisconnectionDetails? _disconnectionDetails;
    private bool _disconnectionHandled;
    private bool _sendLoginDisconnect = true;
    private int _receivedPackets;
    private int _sentPackets;

    //CompressionThreshold 压缩阈值 -1 禁用包数据长度超过阈值才压缩
    public int CompressionThreshold { get; set; } = -1;

    //EncryptionEnabled 是否启用加密启用后所有读写经 CryptoHelper 处理
    public bool EncryptionEnabled { get; private set; }
    private byte[]? _encryptKey;
    private bool _disposed;

    //RegistryAccess 注册表访问默认空 Login 阶段无注册表上下文
    //进入 Configuration/Play 阶段后由 SetupOutboundProtocol 调用方设置真实 registryAccess
    public RegistryAccess RegistryAccess { get; set; } = RegistryAccess.Empty;

    //Connection 构造对齐原版 Connection(PacketFlow) 但显式传 Stream
    public Connection(Stream readStream, Stream writeStream, PacketFlow receiving, Thread? runningThread = null)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _receiving = receiving;
        _processor = new PacketProcessor(runningThread);
    }

    //SetTransport 设置真实 TCP 传输层 Dispose 时关闭触发读循环退出
    //ConnectionAcceptor 传入 TcpClient 测试用 QueueStream 不调用
    internal void SetTransport(IDisposable transport) => _transport = transport;

    //StartReadLoop 启动后台读线程循环 Receive 直到流结束或 Dispose
    //真实 TCP 场景由 ConnectionAcceptor 在装好监听器后调用
    //测试用 QueueStream 不调用由测试手动 Receive 保持兼容
    //读线程阻塞在 Receive 的 Read 上 Dispose 关闭 Transport 会让 Read 抛异常退出
    public void StartReadLoop()
    {
        if (_readThread != null) return;
        _readLoopRunning = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ConnectionReadLoop" };
        _readThread.Start();
    }

    //ReadLoop 后台读循环阻塞 ReceiveRaw 仅读原始 payload 入队不解码
    //解码延迟到主线程 Tick 的 DecodeRawPackets 避免协议切换竞态
    //如 Handshake 切 Status 时 StatusRequest 被读循环用旧 Handshake 协议误解码
    private void ReadLoop()
    {
        while (_readLoopRunning && !_disposed)
        {
            try
            {
                var payload = ReceiveRaw();
                if (payload == null)
                {
                    Disconnect(new DisconnectionDetails("对端关闭连接"));
                    break;
                }
                _rawPackets.Enqueue(payload);
            }
            catch (IOException) { Disconnect(new DisconnectionDetails("连接 IO 异常")); break; }
            catch (SocketException) { Disconnect(new DisconnectionDetails("连接 Socket 异常")); break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    //Receiving 接收方向对齐原版 getReceiving
    public PacketFlow Receiving => _receiving;

    //Sending 发送方向对齐原版 getSending
    public PacketFlow Sending => _receiving.GetOpposite();

    //ReceivingDirection 接收方向转为 FlowDirection 用于和 PacketListener.Flow 比较
    public FlowDirection ReceivingDirection
        => _receiving == PacketFlow.Serverbound ? FlowDirection.Serverbound : FlowDirection.Clientbound;

    //IsConnected 是否已连接对齐原版 isConnected
    public bool IsConnected => !_disposed;

    //IsConnecting 是否正在连接对齐原版 isConnecting
    public bool IsConnecting => !_disposed && _inboundProtocol == null;

    //PacketListener 当前包监听器对齐原版 getPacketListener
    public PacketListener? Listener => _packetListener;

    //DisconnectionDetails 断连详情
    public DisconnectionDetails? DisconnectionDetails => _disconnectionDetails;

    //SetupInboundProtocol 配置入站协议和监听器对齐原版 setupInboundProtocol
    public void SetupInboundProtocol<THandler>(ProtocolInfo<THandler> protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ValidateListener(protocol, listener);
        _packetListener = listener;
        _disconnectListener = null;
        _inboundProtocol = protocol;
    }

    //SetupOutboundProtocol 配置出站协议对齐原版 setupOutboundProtocol
    public void SetupOutboundProtocol(INonGenericProtocol protocol)
    {
        _outboundProtocol = protocol;
        _sendLoginDisconnect = protocol.Id == ConnectionProtocol.Login;
    }

    //SetInitialInboundProtocolInternal 设置初始入站协议和监听器
    //供 Game 层扩展方法 SetListenerForServerboundHandshake 使用
    //不暴露 public 是因为业务包依赖由 Game 层负责
    internal void SetInitialInboundProtocolInternal(PacketListener listener, INonGenericProtocol inboundProtocol)
    {
        if (_packetListener != null)
            throw new InvalidOperationException("监听器已设置");
        _packetListener = listener;
        _inboundProtocol = inboundProtocol;
    }

    //DisconnectListenerInternal 设置断连监听器供 Game 扩展方法使用
    internal void SetDisconnectListenerInternal(PacketListener? listener) => _disconnectListener = listener;

    //Send 发包对齐原版 send(Packet)
    //EncodePacket 内部已写 packetId+payload 这里不重复写 packetId
    //用 RegistryFriendlyByteBuf 让 ItemStack 等业务 codec 访问注册表
    public void Send<THandler>(Packet<THandler> packet) where THandler : class
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Connection));
        if (_outboundProtocol == null)
            throw new InvalidOperationException("未配置出站协议");
        var buf = new RegistryFriendlyByteBuf(RegistryAccess);
        _outboundProtocol.EncodePacket(buf, packet);
        byte[] payload = buf.ToArray();
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.CompressIfNeeded(payload, CompressionThreshold);
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Encrypt(payload, _encryptKey);
        var lengthBuf = new FriendlyByteBuf();
        lengthBuf.WriteVarInt(payload.Length);
        _writeStream.Write(lengthBuf.ToArray());
        _writeStream.Write(payload);
        _writeStream.Flush();
        _sentPackets++;
    }

    //SendAll 批量发包对齐原版 sendAll
    public void SendAll<THandler>(IEnumerable<Packet<THandler>> packets) where THandler : class
    {
        foreach (var p in packets) Send(p);
    }

    //Receive 阻塞读取一个包并立即解码调度到 PacketProcessor
    //对齐原版 channelRead0 解码后调用 genericsFtw 调 packet.handle(listener)
    //测试场景手动调用真实 TCP 场景由 ReadLoop 入队 Tick 解码
    //返回 false 表示流已结束无包可读
    public bool Receive()
    {
        if (_disposed || _inboundProtocol == null || _packetListener == null) return false;
        byte[]? payload;
        try
        {
            payload = ReceiveRaw();
        }
        catch (IOException)
        {
            return false;
        }
        if (payload == null) return false;
        DecodePayload(payload);
        return true;
    }

    //ReceiveRaw 读取一个原始包返回 payload 不解码
    //读取 VarInt 长度前缀 + 指定长度 payload 解密解压后返回
    //返回 null 表示流已结束无包可读
    private byte[]? ReceiveRaw()
    {
        //读 VarInt 长度前缀 1-5 字节读到 MSB=0 为止
        int length = 0;
        int shift = 0;
        int b;
        do
        {
            b = _readStream.ReadByte();
            if (b < 0) return null;
            length |= (b & 0x7F) << shift;
            shift += 7;
            if (shift > 35) throw new IOException($"VarInt 长度前缀超长");
        } while ((b & 0x80) != 0);

        if (length <= 0 || length > 0x200000)
            throw new IOException($"非法包长度 {length}");
        byte[] payload = new byte[length];
        if (!ReadFill(payload, length)) return null;
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Decrypt(payload, _encryptKey);
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.Decompress(payload);
        return payload;
    }

    //DecodePayload 解码单个 payload 调度到 PacketProcessor
    //解码失败丢弃该包不抛异常避免读循环退出
    private void DecodePayload(byte[] payload)
    {
        if (_inboundProtocol == null || _packetListener == null) return;
        var dataBuf = new RegistryFriendlyByteBuf(RegistryAccess, payload);
        object? packet;
        try
        {
            packet = _inboundProtocol.DecodePacket(dataBuf);
        }
        catch (Exception ex)
        {
            Log.Warning("Connection", $"解码包失败丢弃该包: {ex.Message}");
            return;
        }
        if (packet == null)
        {
            Log.Warning("Connection", "解码包返回 null丢弃该包");
            return;
        }
        _processor.ScheduleIfPossible(_packetListener, packet);
        _receivedPackets++;
    }

    //DecodeRawPackets 解码 _rawPackets 队列中的所有原始包
    //解码一个立即 ProcessQueuedPackets 确保协议切换在解码下一个包前生效
    //避免 Handshake 包切换 Status 协议后 StatusRequest 仍用旧协议解码的竞态
    private void DecodeRawPackets()
    {
        while (_rawPackets.TryDequeue(out var payload))
        {
            DecodePayload(payload);
            //立即处理让协议切换生效再解码下一个包
            _processor.ProcessQueuedPackets();
        }
    }

    //Tick 每帧调用对齐原版 tick
    //先 DecodeRawPackets 解码读循环入队的原始包并处理触发协议切换
    //再 FlushQueue 执行待处理动作最后兜底处理排队包和断连
    public void Tick()
    {
        DecodeRawPackets();
        FlushQueue();
        _processor.ProcessQueuedPackets();
        if (!IsConnected && !_disconnectionHandled)
            HandleDisconnection();
    }

    //RunOnceConnected 连接建立后执行动作对齐原版 runOnceConnected
    public void RunOnceConnected(Action<Connection> action)
    {
        if (IsConnected)
        {
            FlushQueue();
            action(this);
        }
        else
        {
            _pendingActions.Enqueue(action);
        }
    }

    //FlushQueue 执行所有待处理动作对齐原版 flushQueue
    private void FlushQueue()
    {
        while (_pendingActions.TryDequeue(out var action))
            action(this);
    }

    //Disconnect 断开连接对齐原版 disconnect
    public void Disconnect(string reason)
        => Disconnect(new DisconnectionDetails(reason));

    //Disconnect 断开连接对齐原版 disconnect(DisconnectionDetails)
    public void Disconnect(DisconnectionDetails details)
    {
        _disconnectionDetails = details;
        _disposed = true;
    }

    //HandleDisconnection 处理断连事件对齐原版 handleDisconnection
    public void HandleDisconnection()
    {
        if (_disconnectionHandled) return;
        _disconnectionHandled = true;
        var listener = _packetListener ?? _disconnectListener;
        if (listener == null) return;
        var details = _disconnectionDetails ?? new DisconnectionDetails("连接已断开");
        listener.OnDisconnect(details.Reason);
    }

    //EnableEncryption 启用 AES 加密对齐原版 setEncryptionKey
    public void EnableEncryption(byte[] key)
    {
        _encryptKey = key;
        EncryptionEnabled = true;
    }

    //SetupCompression 启用压缩对齐原版 setupCompression
    public void SetupCompression(int threshold)
    {
        CompressionThreshold = threshold;
    }

    //ValidateListener 校验监听器方向和协议与 inbound 协议匹配
    private void ValidateListener<THandler>(INonGenericProtocol protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (listener.Flow != ReceivingDirection)
            throw new InvalidOperationException($"监听器方向 {listener.Flow} 与连接接收方向 {ReceivingDirection} 不匹配");
        if (protocol.Id != listener.Protocol)
            throw new InvalidOperationException($"监听器协议 {listener.Protocol} 与 inbound 协议 {protocol.Id} 不匹配");
    }

    //ReadFill 从底层流读满指定字节数到 buffer 返回 false 若流提前结束
    private bool ReadFill(byte[] buffer, int length)
    {
        int read = 0;
        while (read < length)
        {
            int n = _readStream.Read(buffer, read, length - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readLoopRunning = false;
        //关闭 Transport 让读循环的阻塞 Read 抛 IOException 退出线程
        //测试无 Transport 不影响 QueueStream 由测试自行管理
        _transport?.Dispose();
        _processor.Dispose();
        _packetListener = null;
        _inboundProtocol = null;
        _outboundProtocol = null;
    }
}
