using System.Net;
using System.Threading;
using NetCraft.DataFixer;
using NetCraft.Game.DFU;
using NetCraft.Game.Network;
using NetCraft.Game.Network.Protocol.Configuration;
using NetCraft.Game.Network.Protocol.Handshake;
using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Game.Network.Protocol.Status;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;

namespace NetCraft.Game.Server;

//DedicatedServer 专用服务端对应原版 net.minecraft.server.dedicated.DedicatedServer
//继承 MinecraftServer 接入 ServerSettings 与 LevelStorageAccess 持有世界存储引用
//阶段 11.35 接入构造与配置字段阶段 11.46 接入 PersistentServerLevel 与 tick 间隔控制
//阶段 11.47 接入玩家 Connection 列表与 level.Tick 调度对齐原版 tickChildren 调用顺序
//阶段 11.63 接入 ConnectionAcceptor 端口监听与 PlayerList 玩家管理实现 ServerHandshake/Login/Configuration 三阶段 Context 路由
//每 AutoSaveIntervalTicks tick 自动刷盘 Stop 时强制刷盘避免数据丢失
public sealed class DedicatedServer : MinecraftServer, ServerHandshakeContext, ServerLoginContext, ServerConfigurationContext, IDisposable
{
    //AutoSaveIntervalTicks 自动刷盘间隔对应原版 autosave.period 默认 6000 tick 约 5 分钟
    public const int AutoSaveIntervalTicks = 6000;

    private readonly ServerSettings _settings;
    private readonly LevelStorageAccess _levelAccess;
    private readonly NetCraft.DataFixer.DataFixer _dataFixer;
    private readonly PersistentServerLevel _overworld;
    private readonly List<Connection> _connections = new();
    private readonly PlayerList _playerList;
    private readonly ServerStatus _serverStatus;
    private readonly ReloadableServerResources? _rsr;
    private ConnectionAcceptor? _acceptor;
    private bool _disposed;

    //Settings 服务端配置 server.properties 加载结果
    public ServerSettings Settings => _settings;

    //LevelAccess 世界存储访问入口
    public LevelStorageAccess LevelAccess => _levelAccess;

    //Overworld 主世界 PersistentServerLevel 接入 RegionFileStorage
    public PersistentServerLevel Overworld => _overworld;

    //DataFixer 存档升级器由 GameDataFixers.BuildV1_21Fixer 构建传入
    public NetCraft.DataFixer.DataFixer DataFixer => _dataFixer;

    //Connections 已接入的玩家连接列表只读视图供外部诊断
    public IReadOnlyList<Connection> Connections => _connections;

    //PlayerList 在线玩家集合管理
    public PlayerList PlayerList => _playerList;

    //ServerStatus 服务器状态响应 StatusRequest 用
    public ServerStatus ServerStatus => _serverStatus;

    //ServerResources 服务端可重载资源集合持有 ResourceManager 与 Tags
    //为后续 /reload 命令重载 Tags/Recipes/Advancements 等数据驱动内容铺路
    public ReloadableServerResources? ServerResources => _rsr;

    public DedicatedServer(
        Thread serverThread,
        ServerSettings settings,
        LevelStorageAccess levelAccess,
        NetCraft.DataFixer.DataFixer? dataFixer = null,
        RegistryAccess? registryAccess = null,
        PalettedContainerFactory? factory = null,
        int minSectionY = -4,
        int sectionsCount = 24,
        ChunkGenerator? chunkGenerator = null,
        RandomSource? random = null,
        ReloadableServerResources? rsr = null)
        : base(serverThread)
    {
        _settings = settings;
        _levelAccess = levelAccess;
        _dataFixer = dataFixer ?? GameDataFixers.BuildV1_21Fixer();
        _rsr = rsr;
        //为 OVERWORLD 维度创建 SimpleRegionStorage 复用 LevelStorageAccess 缓存
        var regionStorage = levelAccess.CreateRegionStorage(
            LevelKeys.OVERWORLD, _dataFixer, DataFixTypes.Chunk);
        //chunkGenerator 非空时构造 generator 闭包存档未命中走 ChunkStatusProcessor 流水线
        //random 默认用 RandomSource.Create 派生唯一种子保证生成可重现
        Func<ChunkPos, ChunkAccess?>? generator = null;
        if (chunkGenerator is not null)
        {
            var pf = factory ?? PalettedContainerFactory.Default;
            generator = ChunkGenerationHelper.CreateGenerator(
                chunkGenerator, minSectionY, sectionsCount, random ?? RandomSource.Create(), pf);
        }
        _overworld = new PersistentServerLevel(
            regionStorage,
            minSectionY, sectionsCount,
            factory,
            Identifier.WithDefaultNamespace("overworld"),
            SharedConstants.WorldDataVersion,
            registryAccess,
            8,
            generator);
        _playerList = new PlayerList(this, settings.MaxPlayers);
        _serverStatus = BuildServerStatus();
    }

    //BuildServerStatus 构造 StatusRequest 响应数据
    private ServerStatus BuildServerStatus()
    {
        return new ServerStatus
        {
            Description = _settings.Motd,
            Players = new ServerStatus.PlayersData
            {
                Max = _settings.MaxPlayers,
                Online = 0,
            },
            Version = ServerStatus.VersionData.Current(),
        };
    }

    //StartNetwork 启动 TCP 监听线程接受新连接
    public void StartNetwork()
    {
        _acceptor = new ConnectionAcceptor(IPAddress.Any, _settings.ServerPort, OnNewConnection);
        _acceptor.Start();
        Log.Info($"网络监听已启动端口 {_settings.ServerPort}");
    }

    //OnNewConnection 新连接回调装初始握手监听器
    private void OnNewConnection(Connection conn)
    {
        var handshake = new ServerHandshakePacketListenerImpl(conn, this);
        conn.SetListenerForServerboundHandshake(handshake);
        lock (_connections) _connections.Add(conn);
        Log.Debug($"新连接已加入当前连接数 {_connections.Count}");
    }

    //AddPlayer 把 Connection 加入调度列表对应原版 PlayerList.addPlayer
    //低层 API 测试用直接添加 Connection 不创建 ServerPlayer 不同于 PlayerList.PlaceNewPlayer
    public void AddPlayer(Connection connection)
    {
        lock (_connections) _connections.Add(connection);
    }

    //RemovePlayer 移除 Connection 返回是否成功
    public bool RemovePlayer(Connection connection)
    {
        lock (_connections) return _connections.Remove(connection);
    }

    //--- ServerHandshakeContext 实现 ---

    //TransitionToStatus 切换连接到 Status 阶段挂 ServerStatusPacketListenerImpl
    public void TransitionToStatus(Connection connection)
    {
        Log.Debug("TransitionToStatus");
        connection.SetListenerForServerboundStatus(new ServerStatusPacketListenerImpl(connection, _serverStatus));
    }

    //TransitionToLogin 切换连接到 Login 阶段挂 ServerLoginPacketListenerImpl
    public void TransitionToLogin(Connection connection)
    {
        Log.Debug("TransitionToLogin");
        connection.SetListenerForServerboundLogin(new ServerLoginPacketListenerImpl(connection, this));
    }

    //--- ServerLoginContext 实现 ---

    //TransitionToConfiguration 切换连接到 Configuration 阶段挂 ServerConfigurationPacketListenerImpl
    public void TransitionToConfiguration(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToConfiguration profile={profile.Name}");
        connection.SetListenerForServerboundConfiguration(
            new ServerConfigurationPacketListenerImpl(connection, profile, this));
    }

    //--- ServerConfigurationContext 实现 ---

    //TransitionToGame 切换连接到 Play 阶段挂 ServerGamePacketListenerImpl 并触发 PlayerList.PlaceNewPlayer
    public void TransitionToGame(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToGame profile={profile.Name}");
        var gameListener = new ServerGamePacketListenerImpl(connection, profile);
        connection.SetListenerForServerboundGame(gameListener);
        _playerList.PlaceNewPlayer(connection, profile);
    }

    //Tick 专用服务端帧逻辑对齐原版 MinecraftServer.tickChildren 调用顺序
    //1. tick 所有玩家 Connection 处理入站包队列与断连检测
    //2. 清理已断开连接调 HandleDisconnection 并从 PlayerList 移除
    //3. tick 主世界 ServerLevel 推进 ChunkSource 异步调度与实体调度
    //4. 周期刷盘
    protected override void Tick()
    {
        List<Connection> snapshot;
        lock (_connections) snapshot = _connections.ToList();
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].Tick();
        //清理已断开的连接读循环检测到流结束会调 Disconnect 标记 _disposed 此处统一回收
        lock (_connections)
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                var conn = _connections[i];
                if (!conn.IsConnected)
                {
                    conn.HandleDisconnection();
                    _connections.RemoveAt(i);
                    //从 PlayerList 移除关联的 ServerPlayer 避免泄漏
                    foreach (var player in _playerList.Players)
                    {
                        if (ReferenceEquals(player.Connection, conn))
                        {
                            _playerList.RemovePlayer(player);
                            break;
                        }
                    }
                    Log.Debug($"连接已断开移除 当前连接数 {_connections.Count}");
                }
            }
        }
        _overworld.Tick();
        foreach (var player in _playerList.Players)
            player.Tick();
        if (TickCount > 0 && TickCount % AutoSaveIntervalTicks == 0)
        {
            try
            {
                _overworld.SynchronizeAsync(flush: false).GetAwaiter().GetResult();
                Log.Info($"自动刷盘已触发 tick {TickCount}");
            }
            catch (Exception e)
            {
                Log.Error($"自动刷盘失败 tick {TickCount} {e.Message}");
            }
        }
    }

    //Stop 触发主循环退出并强制刷盘避免数据丢失
    public override void Stop()
    {
        if (Running)
        {
            try
            {
                _overworld.SynchronizeAsync(flush: true).GetAwaiter().GetResult();
                Log.Info("DedicatedServer 关闭刷盘完成");
            }
            catch (Exception e)
            {
                Log.Error($"DedicatedServer 关闭刷盘失败 {e.Message}");
            }
        }
        _acceptor?.Stop();
        foreach (var player in _playerList.Players)
            player.Disconnect("server stopping");
        base.Stop();
    }

    //RunStatus 阻塞直到 Shutdown 信号给外部 EXE 调用
    public void RunStatus()
    {
        Log.Info($"DedicatedServer 端口 {_settings.ServerPort} 世界 {_settings.LevelName} 等待关闭信号");
        WaitForShutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _acceptor?.Dispose();
        _levelAccess.Dispose();
        _disposed = true;
    }
}
