using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Network;
using NetCraft.Storage;
using NetCraft.Primitives;

namespace NetCraft.Game.Server;

//ServerPlayer 服务端玩家对象对应原版 ServerPlayer
//最小实现持有 Connection/GameProfile/坐标供 PlayerList 管理
//不继承 NetCraft.Registry.Entity 避免与已有实体体系混淆本轮只做网络层玩家
public sealed class ServerPlayer
{
    //EntityId 服务端分配的实体 id 用于 ClientboundLoginPacket 等同步
    private static int _nextEntityId;

    public int EntityId { get; }
    public GameProfile Profile { get; }
    public Connection Connection { get; }
    public ServerLevel Level { get; }

    //Position 玩家坐标默认出生点 0,0,0 后续接入出生点逻辑
    public Vec3 Position { get; set; } = new(0, 64, 0);
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    //NextEntityId 线程安全分配实体 id 自增
    private static int NextEntityId() => Interlocked.Increment(ref _nextEntityId);

    public ServerPlayer(GameProfile profile, Connection connection, ServerLevel level)
    {
        EntityId = NextEntityId();
        Profile = profile;
        Connection = connection;
        Level = level;
    }

    //Tick 每帧调度对应原版 ServerPlayer.tick
    //本轮空实现未来接入坐标同步生命恢复等
    public void Tick()
    {
    }

    //Disconnect 断开玩家连接对齐原版 ServerPlayer.disconnect
    public void Disconnect(string reason)
    {
        Connection.Disconnect(reason);
    }
}
