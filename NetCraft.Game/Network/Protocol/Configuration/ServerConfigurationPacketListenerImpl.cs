using NetCraft.Game.Network.Protocol.Cookie;
using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Configuration;

//ServerConfigurationPacketListenerImpl 服务端 configuration 监听器实现
//核心处理 HandleConfigurationFinished 切换到 Play 阶段触发 PlayerList.PlaceNewPlayer
//其余 common/cookie 子协议包暂空实现因 ConfigurationProtocols 未注册这些包不会解码到
public sealed class ServerConfigurationPacketListenerImpl : ServerConfigurationPacketListener
{
    private readonly Connection _connection;
    private readonly ServerConfigurationContext _context;
    private readonly GameProfile _profile;

    public ServerConfigurationPacketListenerImpl(Connection connection, GameProfile profile, ServerConfigurationContext context)
    {
        _connection = connection;
        _profile = profile;
        _context = context;
    }

    //HandleConfigurationFinished 客户端完成配置切换到 Play 阶段挂 ServerGamePacketListenerImpl
    public void HandleConfigurationFinished(ServerboundFinishConfigurationPacket packet)
    {
        Log.Debug($"HandleConfigurationFinished 入口 profile={_profile.Name}");
        _context.TransitionToGame(_connection, _profile);
        Log.Debug("HandleConfigurationFinished 出口");
    }

    //HandleSelectKnownPacks 客户端选择已知 pack 简化版空实现
    public void HandleSelectKnownPacks(ServerboundSelectKnownPacks packet)
    {
        Log.Debug($"HandleSelectKnownPacks 入口");
    }

    //HandleAcceptCodeOfConduct 行为准则接受空实现
    public void HandleAcceptCodeOfConduct(ServerboundAcceptCodeOfConductPacket packet)
    {
        Log.Debug("HandleAcceptCodeOfConduct 入口");
    }

    //以下为继承自 ServerCommonPacketListener 的方法
    //ConfigurationProtocols 未注册这些包不会解码到暂空实现
    public void HandleClientInformation(ServerboundClientInformationPacket packet) { }
    public void HandleCustomPayload(ServerboundCustomPayloadPacket packet) { }
    public void HandleKeepAlive(ServerboundKeepAlivePacket packet) { }
    public void HandlePong(ServerboundPongPacket packet) { }
    public void HandleResourcePack(ServerboundResourcePackPacket packet) { }
    public void HandleCustomClickAction(ServerboundCustomClickActionPacket packet) { }

    //继承自 ServerCookiePacketListener
    public void HandleCookieResponse(ServerboundCookieResponsePacket packet) { }

    public void OnDisconnect(string reason)
    {
        Log.Info($"configuration 阶段断连 reason={reason} profile={_profile.Name}");
    }
}

//ServerConfigurationContext configuration 阶段上下文由 DedicatedServer 实现封装切到 Game 的逻辑
public interface ServerConfigurationContext
{
    //TransitionToGame 切换连接到 Play 阶段挂 ServerGamePacketListenerImpl 并触发 PlayerList.PlaceNewPlayer
    void TransitionToGame(Connection connection, GameProfile profile);
}
