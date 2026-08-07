using NetCraft.Game.Network.Protocol.Ping;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Status;

//ServerStatusPacketListenerImpl 服务端 status 监听器实现
//响应 ServerboundStatusRequestPacket 返回 ServerStatus JSON
//ping 请求暂空实现因 StatusProtocols 不注册 ping 包流程跑不到
public sealed class ServerStatusPacketListenerImpl : ServerStatusPacketListener
{
    private readonly Connection _connection;
    private readonly ServerStatus _status;

    public ServerStatusPacketListenerImpl(Connection connection, ServerStatus status)
    {
        _connection = connection;
        _status = status;
    }

    //HandleStatusRequest 回 ClientboundStatusResponsePacket 含 ServerStatus JSON
    public void HandleStatusRequest(ServerboundStatusRequestPacket packet)
    {
        Log.Debug("HandleStatusRequest 入口");
        try
        {
            _connection.Send(new ClientboundStatusResponsePacket(_status));
        }
        catch (Exception e)
        {
            Log.Warning($"发送 status 响应失败 {e.Message}");
        }
        Log.Debug("HandleStatusRequest 出口");
    }

    //HandlePingRequest 暂空实现 ping 不在 StatusProtocols 注册流程跑不到
    public void HandlePingRequest(ServerboundPingRequestPacket packet)
    {
        Log.Debug($"HandlePingRequest 入口 time={packet.Time}");
    }

    public void OnDisconnect(string reason)
    {
        Log.Info($"status 阶段断连 reason={reason}");
    }
}
