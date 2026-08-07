using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Game.Network;
using NetCraft.Game.Network.Protocol.Handshake;
using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Game.Network.Protocol.Ping;
using NetCraft.Game.Network.Protocol.Status;

namespace NetCraft.Test.Modules;

//NetworkHandshakeTests 端到端握手流程测试
//用双向 MemoryStream 模拟网络管道客户端服务端各持一个 Connection
//验证协议状态机+StreamCodec 编解码+PacketProcessor 调度
internal static class NetworkHandshakeTests
{
    public const string Module = "handshake";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Handshake to Status end-to-end", TestHandshakeStatusFlow);
        yield return ("Handshake to Login end-to-end", TestHandshakeLoginFlow);
        yield return ("Connection protocol switch", TestConnectionProtocolSwitch);
    }

    //TestHandshakeStatusFlow 完整 Status 流程
    //客户端发 Intention(STATUS)→服务端切协议→客户端发 StatusRequest→服务端回 StatusResponse
    private static bool TestHandshakeStatusFlow()
    {
        var pipe = new DuplexPipe();
        var client = new Connection(pipe.ServerToClient, pipe.ClientToServer, PacketFlow.Clientbound);
        var server = new Connection(pipe.ClientToServer, pipe.ServerToClient, PacketFlow.Serverbound);

        var serverHandshakeListener = new TestServerHandshakeListener(server);
        server.SetListenerForServerboundHandshake(serverHandshakeListener);

        var clientStatusListener = new TestClientStatusListener();
        client.InitiateServerboundStatusConnection("localhost", 25565, clientStatusListener);

        //服务端读 Intention 并处理触发协议切换
        if (!ReceiveAndTick(server)) return false;
        if (serverHandshakeListener.ReceivedIntention is not { Intention: ClientIntent.Status }) return false;
        if (server.Listener is not TestServerStatusListener) return false;

        //客户端发 StatusRequest 服务端回 StatusResponse
        client.Send(ServerboundStatusRequestPacket.Instance);
        if (!ReceiveAndTick(server)) return false;
        if (!ReceiveAndTick(client)) return false;

        if (clientStatusListener.ReceivedStatusResponse is null) return false;
        return clientStatusListener.ReceivedStatusResponse.Status.Description == "NetCraft Test";
    }

    //TestHandshakeLoginFlow 简化 Login 流程不加密不压缩
    //客户端发 Intention(LOGIN)→服务端切协议→客户端发 Hello→服务端回 LoginFinished
    private static bool TestHandshakeLoginFlow()
    {
        var pipe = new DuplexPipe();
        var client = new Connection(pipe.ServerToClient, pipe.ClientToServer, PacketFlow.Clientbound);
        var server = new Connection(pipe.ClientToServer, pipe.ServerToClient, PacketFlow.Serverbound);

        var serverHandshakeListener = new TestServerHandshakeListener(server);
        server.SetListenerForServerboundHandshake(serverHandshakeListener);

        var clientLoginListener = new TestClientLoginListener();
        client.InitiateServerboundLoginConnection("localhost", 25565, clientLoginListener);

        //服务端读 Intention 切到 Login 协议
        if (!ReceiveAndTick(server)) return false;
        if (serverHandshakeListener.ReceivedIntention is not { Intention: ClientIntent.Login }) return false;
        if (server.Listener is not TestServerLoginListener) return false;

        //客户端发 Hello
        var profileId = Guid.NewGuid();
        client.Send(new ServerboundHelloPacket("TestPlayer", profileId));

        //服务端读 Hello 并回 LoginFinished
        if (!ReceiveAndTick(server)) return false;
        if (!ReceiveAndTick(client)) return false;

        if (clientLoginListener.ReceivedLoginFinished is null) return false;
        return clientLoginListener.ReceivedLoginFinished.GameProfile.Id == profileId;
    }

    //TestConnectionProtocolSwitch 验证协议状态机切换 API
    //服务端初始无 inbound 调用 SetListenerForServerboundHandshake 后 inbound=Handshake
    //SetupInboundProtocol 切换后 PacketListener.Protocol 反映新协议
    private static bool TestConnectionProtocolSwitch()
    {
        var pipe = new DuplexPipe();
        var server = new Connection(pipe.ClientToServer, pipe.ServerToClient, PacketFlow.Serverbound);

        //初始无 inbound protocol IsConnecting 为 true
        if (!server.IsConnecting) return false;

        var handshakeListener = new TestServerHandshakeListener(server);
        server.SetListenerForServerboundHandshake(handshakeListener);
        if (server.Listener != handshakeListener) return false;
        if (server.Listener!.Protocol != ConnectionProtocol.Handshake) return false;

        //切换到 Status 协议
        var statusListener = new TestServerStatusListener(server);
        server.SetupInboundProtocol(StatusProtocols.Serverbound, statusListener);
        server.SetupOutboundProtocol(StatusProtocols.Clientbound);
        if (server.Listener != statusListener) return false;
        if (server.Listener!.Protocol != ConnectionProtocol.Status) return false;

        //切换到 Login 协议
        var loginListener = new TestServerLoginListener(server);
        server.SetupInboundProtocol(LoginProtocols.Serverbound, loginListener);
        server.SetupOutboundProtocol(LoginProtocols.Clientbound);
        if (server.Listener != loginListener) return false;
        if (server.Listener!.Protocol != ConnectionProtocol.Login) return false;

        return true;
    }

    //ReceiveAndTick 读一个包并触发 Tick 处理
    private static bool ReceiveAndTick(Connection conn)
    {
        var ok = conn.Receive();
        if (ok) conn.Tick();
        return ok;
    }

    //CreateTestStatus 构造测试用 ServerStatus
    private static ServerStatus CreateTestStatus() => new()
    {
        Description = "NetCraft Test",
        Version = new ServerStatus.VersionData { Name = "NetCraft 1.21", Protocol = 767 },
        Players = new ServerStatus.PlayersData { Max = 20, Online = 0 },
    };

    //DuplexPipe 双向内存管道
    //用独立 Position 的 QueueStream 避免共享 MemoryStream 读写位置冲突
    //ClientToServer 客户端写服务端读
    //ServerToClient 服务端写客户端读
    private sealed class DuplexPipe
    {
        public QueueStream ClientToServer { get; } = new();
        public QueueStream ServerToClient { get; } = new();
    }

    //QueueStream 简单字节块队列流
    //写入追加到队列读取从头消费读写位置独立
    //Connection.ReadFill 在无数据时返回 false 不阻塞
    private sealed class QueueStream : Stream
    {
        private readonly Queue<byte[]> _chunks = new();
        private byte[]? _current;
        private int _currentOffset;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                if (_current == null || _currentOffset >= _current.Length)
                {
                    if (_chunks.Count == 0) break;
                    _current = _chunks.Dequeue();
                    _currentOffset = 0;
                }
                int n = Math.Min(count - read, _current.Length - _currentOffset);
                Array.Copy(_current, _currentOffset, buffer, offset + read, n);
                _currentOffset += n;
                read += n;
            }
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0) return;
            var copy = new byte[count];
            Array.Copy(buffer, offset, copy, 0, count);
            _chunks.Enqueue(copy);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    //TestServerHandshakeListener 服务端握手监听器 stub
    //HandleIntention 根据 intent 切换到 Status 或 Login 协议
    private sealed class TestServerHandshakeListener : ServerHandshakePacketListener
    {
        private readonly Connection _server;
        public ClientIntentionPacket? ReceivedIntention;

        public TestServerHandshakeListener(Connection server) => _server = server;

        public void HandleIntention(ClientIntentionPacket packet)
        {
            ReceivedIntention = packet;
            if (packet.Intention == ClientIntent.Status)
            {
                var statusListener = new TestServerStatusListener(_server);
                _server.SetupInboundProtocol(StatusProtocols.Serverbound, statusListener);
                _server.SetupOutboundProtocol(StatusProtocols.Clientbound);
            }
            else if (packet.Intention == ClientIntent.Login)
            {
                var loginListener = new TestServerLoginListener(_server);
                _server.SetupInboundProtocol(LoginProtocols.Serverbound, loginListener);
                _server.SetupOutboundProtocol(LoginProtocols.Clientbound);
            }
        }

        public void OnDisconnect(string reason) { }
    }

    //TestServerStatusListener 服务端 status 监听器 stub
    //HandleStatusRequest 返回测试 ServerStatus
    private sealed class TestServerStatusListener : ServerStatusPacketListener
    {
        private readonly Connection _server;
        public ServerboundStatusRequestPacket? ReceivedRequest;

        public TestServerStatusListener(Connection server) => _server = server;

        public void HandleStatusRequest(ServerboundStatusRequestPacket packet)
        {
            ReceivedRequest = packet;
            _server.Send(new ClientboundStatusResponsePacket(CreateTestStatus()));
        }

        public void HandlePingRequest(ServerboundPingRequestPacket packet) { }

        public void OnDisconnect(string reason) { }
    }

    //TestClientStatusListener 客户端 status 监听器 stub
    private sealed class TestClientStatusListener : ClientStatusPacketListener
    {
        public ClientboundStatusResponsePacket? ReceivedStatusResponse;
        public ClientboundPongResponsePacket? ReceivedPong;

        public void HandleStatusResponse(ClientboundStatusResponsePacket packet)
            => ReceivedStatusResponse = packet;

        public void HandlePongResponse(ClientboundPongResponsePacket packet)
            => ReceivedPong = packet;

        public void OnDisconnect(string reason) { }
    }

    //TestServerLoginListener 服务端 login 监听器 stub
    //HandleHello 回 LoginFinished 简化版跳过加密
    private sealed class TestServerLoginListener : ServerLoginPacketListener
    {
        private readonly Connection _server;
        public ServerboundHelloPacket? ReceivedHello;
        public ServerboundLoginAcknowledgedPacket? ReceivedAcknowledged;

        public TestServerLoginListener(Connection server) => _server = server;

        public void HandleHello(ServerboundHelloPacket packet)
        {
            ReceivedHello = packet;
            var profile = new GameProfile(packet.ProfileId, packet.Name);
            var sessionId = Guid.NewGuid();
            _server.Send(new ClientboundLoginFinishedPacket(profile, sessionId));
        }

        public void HandleKey(ServerboundKeyPacket packet) { }

        public void HandleCustomQueryPacket(ServerboundCustomQueryAnswerPacket packet) { }

        public void HandleLoginAcknowledgement(ServerboundLoginAcknowledgedPacket packet)
            => ReceivedAcknowledged = packet;

        public void OnDisconnect(string reason) { }
    }

    //TestClientLoginListener 客户端 login 监听器 stub
    private sealed class TestClientLoginListener : ClientLoginPacketListener
    {
        public ClientboundLoginFinishedPacket? ReceivedLoginFinished;
        public ClientboundHelloPacket? ReceivedHello;
        public ClientboundLoginDisconnectPacket? ReceivedDisconnect;
        public ClientboundLoginCompressionPacket? ReceivedCompression;
        public ClientboundCustomQueryPacket? ReceivedCustomQuery;

        public void HandleHello(ClientboundHelloPacket packet) => ReceivedHello = packet;

        public void HandleLoginFinished(ClientboundLoginFinishedPacket packet)
            => ReceivedLoginFinished = packet;

        public void HandleDisconnect(ClientboundLoginDisconnectPacket packet)
            => ReceivedDisconnect = packet;

        public void HandleCompression(ClientboundLoginCompressionPacket packet)
            => ReceivedCompression = packet;

        public void HandleCustomQuery(ClientboundCustomQueryPacket packet)
            => ReceivedCustomQuery = packet;

        public void OnDisconnect(string reason) { }
    }
}
