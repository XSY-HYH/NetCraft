using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Game.Network;
using NetCraft.Game.Network.Protocol.Handshake;
using NetCraft.Game.Network.Protocol.Ping;
using NetCraft.Game.Network.Protocol.Status;

namespace NetCraft.Test.Modules;

//NetworkTcpTests 真实 TCP 端到端握手测试
//默认在 SelectTests 中跳过需通过参数显式指定 networktcp 模块才运行
//与内存管道的 NetworkHandshakeTests 不同这里走真实 TcpListener/TcpClient
//验证 StreamCodec 字节级兼容+协议状态机+读循环跨线程入队主线程 Tick 解码
internal static class NetworkTcpTests
{
    public const string Module = "networktcp";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //服务端视角启动监听等客户端连接握手完成后退出
        yield return ("TCP server accepts client and completes handshake", TestTcpServerHandshake);
        //客户端视角连接服务端收到 StatusResponse 后退出
        yield return ("TCP client connects and receives status response", TestTcpClientHandshake);
    }

    //TestTcpServerHandshake 服务端视角端到端
    //主线程跑服务端监听端口 Accept 客户端连接后持续 Tick 处理握手
    //后台辅助线程跑客户端连接发 Intention+StatusRequest 收到 StatusResponse 后退出
    //握手完成后服务端自动退出验证协议状态机+读循环+Tick 调度
    private static bool TestTcpServerHandshake()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverStatus = CreateTcpStatus();
        Exception? clientError = null;
        var clientDone = new ManualResetEventSlim(false);

        //客户端辅助线程连接服务端发握手包触发服务端状态流转
        var clientThread = new Thread(() =>
        {
            try
            {
                using var tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress.Loopback, port);
                var stream = tcpClient.GetStream();
                var clientConn = new Connection(stream, stream, PacketFlow.Clientbound);
                var clientListener = new TcpClientStatusListener();
                clientConn.InitiateServerboundStatusConnection("localhost", port, clientListener);
                clientConn.StartReadLoop();
                clientConn.Send(ServerboundStatusRequestPacket.Instance);

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                while (DateTime.UtcNow < deadline && clientListener.ReceivedStatusResponse == null)
                {
                    clientConn.Tick();
                    Thread.Sleep(2);
                }
                clientConn.Disconnect("done");
                clientConn.Dispose();
            }
            catch (Exception ex) { clientError = ex; }
            finally { clientDone.Set(); }
        }) { IsBackground = true };
        clientThread.Start();

        TcpClient? serverClient = null;
        Connection? serverConn = null;
        try
        {
            //Accept 阻塞等待客户端连接
            serverClient = listener.AcceptTcpClient();
            var stream = serverClient.GetStream();
            serverConn = new Connection(stream, stream, PacketFlow.Serverbound);
            var handshakeListener = new TcpServerHandshakeListener(serverConn, serverStatus);
            serverConn.SetListenerForServerboundHandshake(handshakeListener);
            serverConn.StartReadLoop();

            //持续 Tick 处理握手直到收到 StatusRequest 或超时
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline && !handshakeListener.StatusRequestReceived)
            {
                serverConn.Tick();
                Thread.Sleep(2);
            }

            bool success = handshakeListener.ReceivedIntention is { Intention: ClientIntent.Status }
                && handshakeListener.StatusRequestReceived;

            //等客户端辅助线程退出避免资源残留
            clientDone.Wait(3000);
            clientThread.Join(2000);

            if (clientError != null)
            {
                Console.Error.WriteLine($"客户端辅助线程异常: {clientError.Message}");
                return false;
            }
            return success;
        }
        finally
        {
            serverConn?.Disconnect("test done");
            serverConn?.Dispose();
            serverClient?.Dispose();
            listener.Stop();
        }
    }

    //TestTcpClientHandshake 客户端视角端到端
    //后台辅助线程跑服务端监听 Accept 客户端连接后持续 Tick 回 StatusResponse
    //主线程跑客户端连接发 Intention+StatusRequest 收到 StatusResponse 后退出
    //验证客户端能解码真实 TCP 字节流并触发监听器回调
    private static bool TestTcpClientHandshake()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverStatus = CreateTcpStatus();
        Exception? serverError = null;
        var serverReady = new ManualResetEventSlim(false);

        //服务端辅助线程监听端口 Accept 后握手回 StatusResponse
        var serverThread = new Thread(() =>
        {
            try
            {
                serverReady.Set();
                var tcpClient = listener.AcceptTcpClient();
                var stream = tcpClient.GetStream();
                var serverConn = new Connection(stream, stream, PacketFlow.Serverbound);
                var handshakeListener = new TcpServerHandshakeListener(serverConn, serverStatus);
                serverConn.SetListenerForServerboundHandshake(handshakeListener);
                serverConn.StartReadLoop();

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                while (DateTime.UtcNow < deadline && !handshakeListener.StatusRequestReceived)
                {
                    serverConn.Tick();
                    Thread.Sleep(2);
                }
                serverConn.Disconnect("done");
                serverConn.Dispose();
                tcpClient.Dispose();
            }
            catch (Exception ex) { serverError = ex; }
        }) { IsBackground = true };
        serverThread.Start();
        serverReady.Wait();

        TcpClient? client = null;
        Connection? clientConn = null;
        try
        {
            client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            var stream = client.GetStream();
            clientConn = new Connection(stream, stream, PacketFlow.Clientbound);
            var clientListener = new TcpClientStatusListener();
            clientConn.InitiateServerboundStatusConnection("localhost", port, clientListener);
            clientConn.StartReadLoop();
            clientConn.Send(ServerboundStatusRequestPacket.Instance);

            //持续 Tick 直到收到 StatusResponse 或超时
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline && clientListener.ReceivedStatusResponse == null)
            {
                clientConn.Tick();
                Thread.Sleep(2);
            }

            bool success = clientListener.ReceivedStatusResponse != null
                && clientListener.ReceivedStatusResponse!.Status.Description == serverStatus.Description;

            serverThread.Join(3000);
            if (serverError != null)
            {
                Console.Error.WriteLine($"服务端辅助线程异常: {serverError.Message}");
                return false;
            }
            return success;
        }
        finally
        {
            clientConn?.Disconnect("test done");
            clientConn?.Dispose();
            client?.Dispose();
            listener.Stop();
        }
    }

    //CreateTcpStatus 构造 TCP 测试用 ServerStatus
    private static ServerStatus CreateTcpStatus() => new()
    {
        Description = "NetCraft TCP",
        Version = new ServerStatus.VersionData { Name = "NetCraft 1.21", Protocol = 767 },
        Players = new ServerStatus.PlayersData { Max = 20, Online = 0 },
    };

    //TcpServerHandshakeListener 服务端握手监听器收到 ClientIntention 切换到 Status 协议
    private sealed class TcpServerHandshakeListener : ServerHandshakePacketListener
    {
        private readonly Connection _server;
        private readonly ServerStatus _status;
        public ClientIntentionPacket? ReceivedIntention;
        public bool StatusRequestReceived;

        public TcpServerHandshakeListener(Connection server, ServerStatus status)
        {
            _server = server;
            _status = status;
        }

        public void HandleIntention(ClientIntentionPacket packet)
        {
            ReceivedIntention = packet;
            if (packet.Intention == ClientIntent.Status)
            {
                var statusListener = new TcpServerStatusListener(_server, _status, () => StatusRequestReceived = true);
                _server.SetupInboundProtocol(StatusProtocols.Serverbound, statusListener);
                _server.SetupOutboundProtocol(StatusProtocols.Clientbound);
            }
        }

        public void OnDisconnect(string reason) { }
    }

    //TcpServerStatusListener 服务端 status 监听器收到 StatusRequest 回 StatusResponse
    private sealed class TcpServerStatusListener : ServerStatusPacketListener
    {
        private readonly Connection _server;
        private readonly ServerStatus _status;
        private readonly Action _onStatusRequest;

        public TcpServerStatusListener(Connection server, ServerStatus status, Action onStatusRequest)
        {
            _server = server;
            _status = status;
            _onStatusRequest = onStatusRequest;
        }

        public void HandleStatusRequest(ServerboundStatusRequestPacket packet)
        {
            _onStatusRequest();
            _server.Send(new ClientboundStatusResponsePacket(_status));
        }

        public void HandlePingRequest(ServerboundPingRequestPacket packet) { }
        public void OnDisconnect(string reason) { }
    }

    //TcpClientStatusListener 客户端 status 监听器记录收到的 StatusResponse
    private sealed class TcpClientStatusListener : ClientStatusPacketListener
    {
        public ClientboundStatusResponsePacket? ReceivedStatusResponse;
        public ClientboundPongResponsePacket? ReceivedPong;

        public void HandleStatusResponse(ClientboundStatusResponsePacket packet)
            => ReceivedStatusResponse = packet;
        public void HandlePongResponse(ClientboundPongResponsePacket packet)
            => ReceivedPong = packet;
        public void OnDisconnect(string reason) { }
    }
}
