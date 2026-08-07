using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Server;

//PlayerList 在线玩家集合管理对应原版 PlayerList
//管理 ServerPlayer 生命周期提供 PlaceNewPlayer/RemovePlayer/Broadcast API
//满员拒绝新玩家加入对应原版 max-players 限制
public sealed class PlayerList
{
    private readonly DedicatedServer _server;
    private readonly List<ServerPlayer> _players = new();
    private readonly object _lock = new();

    public IReadOnlyList<ServerPlayer> Players
    {
        get { lock (_lock) return _players.ToList(); }
    }

    public int PlayerCount
    {
        get { lock (_lock) return _players.Count; }
    }

    public int MaxPlayers { get; }

    public PlayerList(DedicatedServer server, int maxPlayers)
    {
        _server = server;
        MaxPlayers = maxPlayers;
    }

    //PlaceNewPlayer 创建 ServerPlayer 加入玩家列表对应原版 placeNewPlayer
    //满员返回 null 调用方应发送 disconnect 包并断连
    public ServerPlayer? PlaceNewPlayer(Connection connection, GameProfile profile)
    {
        ServerPlayer? player;
        lock (_lock)
        {
            if (_players.Count >= MaxPlayers)
            {
                Log.Warning($"玩家加入被拒满员 {MaxPlayers} name={profile.Name}");
                return null;
            }
            player = new ServerPlayer(profile, connection, _server.Overworld);
            _players.Add(player);
        }
        Log.Info($"玩家加入 {profile.Name} entityId={player.EntityId} 当前在线 {PlayerCount}/{MaxPlayers}");
        return player;
    }

    //RemovePlayer 移除玩家返回是否成功
    public bool RemovePlayer(ServerPlayer player)
    {
        bool removed;
        lock (_lock) removed = _players.Remove(player);
        if (removed)
            Log.Info($"玩家移除 {player.Profile.Name} 当前在线 {PlayerCount}/{MaxPlayers}");
        return removed;
    }

    //BroadcastAll 向所有在线玩家发包
    public void BroadcastAll<THandler>(Packet<THandler> packet) where THandler : class
    {
        List<ServerPlayer> snapshot;
        lock (_lock) snapshot = _players.ToList();
        foreach (var p in snapshot)
        {
            try { p.Connection.Send(packet); }
            catch (Exception e) { Log.Warning($"广播失败 player={p.Profile.Name} {e.Message}"); }
        }
    }

    //BroadcastAllExcept 向除指定玩家外的所有在线玩家发包
    public void BroadcastAllExcept<THandler>(ServerPlayer exclude, Packet<THandler> packet) where THandler : class
    {
        List<ServerPlayer> snapshot;
        lock (_lock) snapshot = _players.ToList();
        foreach (var p in snapshot)
        {
            if (ReferenceEquals(p, exclude)) continue;
            try { p.Connection.Send(packet); }
            catch (Exception e) { Log.Warning($"广播失败 player={p.Profile.Name} {e.Message}"); }
        }
    }
}
