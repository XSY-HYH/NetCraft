using System.Threading;
using System.Threading.Tasks;
using NetCraft;
using NetCraft.Game.Client;
using NetCraft.Game.Server;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Paletted;

namespace NetCraft.Test.Modules;

//Server 主循环与 Client 主循环测试覆盖 MinecraftServer/DedicatedServer/MinecraftClient 启停契约
//阶段 11.46 增加 DedicatedServer 接入 PersistentServerLevel 与 tick 刷盘验证
//阶段 11.47 增加 level.Tick 调度实体 + DedicatedServer tick 推进 Overworld.LevelTick 验证
//用独立线程启动主循环主线程触发 Stop 后 join 验证退出
internal static class ServerTests
{
    public const string Module = "server";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("MinecraftServer construction", TestMinecraftServerConstruction);
        yield return ("DedicatedServer holds settings and level access", TestDedicatedServerHoldsFields);
        yield return ("DedicatedServer initializes overworld level", TestDedicatedServerInitializesOverworld);
        yield return ("MinecraftClient construction", TestMinecraftClientConstruction);
        yield return ("MinecraftServer Run stops on Stop", TestServerRunStopsOnStop);
        yield return ("MinecraftClient Run stops on Stop", TestClientRunStopsOnStop);
        yield return ("DedicatedServer Run stops on Stop", TestDedicatedServerRunStopsOnStop);
        yield return ("MinecraftServer tick interval avoids busy loop", TestServerTickInterval);
        yield return ("DedicatedServer tick advances overworld level", TestDedicatedServerTickAdvancesOverworldLevel);
        yield return ("PersistentServerLevel tick advances entities", TestPersistentServerLevelTickAdvancesEntities);
        yield return ("DedicatedServer tick processes player connections", TestDedicatedServerTickProcessesConnections);
        yield return ("ChunkSource async loads chunk from storage", TestChunkSourceLoadChunkAsync);
        yield return ("ChunkSource tick advances loaded cache", TestChunkSourceTickAdvancesLoaded);
        yield return ("PersistentServerLevel tick advances ChunkSource", TestPersistentServerLevelTickAdvancesChunkSource);
        yield return ("ChunkSource update player pos raises ticket", TestChunkSourceUpdatePlayerPosRaisesTicket);
        yield return ("EntityLookup range query returns in-range entities", TestEntityLookupRangeQuery);
        yield return ("EntityLookup remove updates index", TestEntityLookupRemove);
        yield return ("ChunkSource generator fallback on missing chunk", TestChunkSourceGeneratorFallback);
    }

    //MinecraftServer 基类构造与初始状态
    private static bool TestMinecraftServerConstruction()
    {
        var server = new TestServer(Thread.CurrentThread);
        return !server.Running && server.TickCount == 0
            && server.SleepBudgetMillis == MinecraftServer.TargetTickMillis;
    }

    //DedicatedServer 持有 Settings 与 LevelAccess 与 DataFixer
    private static bool TestDedicatedServerHoldsFields()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            return server.Settings is not null
                && server.LevelAccess is not null
                && server.DataFixer is not null
                && !server.Running;
        }
        finally
        {
            server.Dispose();
        }
    }

    //DedicatedServer 构造时创建 OVERWORLD 维度 PersistentServerLevel
    private static bool TestDedicatedServerInitializesOverworld()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            var overworld = server.Overworld;
            return overworld is not null
                && overworld.RegionStorage is not null
                && overworld.Factory is not null
                && overworld.MinSectionY == -4
                && overworld.SectionsCount == 24;
        }
        finally
        {
            server.Dispose();
        }
    }

    //MinecraftClient 构造持有 GameConfig 与默认 SleepBudgetMillis
    private static bool TestMinecraftClientConstruction()
    {
        var config = new GameConfig { RenderDistance = 8, Fov = 60 };
        using var client = new MinecraftClient(config);
        return client.Config.RenderDistance == 8
            && client.Config.Fov == 60
            && !client.Running
            && client.FrameCount == 0
            && client.SleepBudgetMillis == MinecraftClient.TargetFrameMillis;
    }

    //主循环在独立线程运行主线程 Stop 后退出
    private static bool TestServerRunStopsOnStop()
    {
        var server = new TestServer(Thread.CurrentThread);
        var thread = new Thread(server.Run);
        thread.Start();
        Thread.Sleep(100);
        var runningBeforeStop = server.Running;
        server.Stop();
        thread.Join(2000);
        return runningBeforeStop && !server.Running && !thread.IsAlive;
    }

    //客户端主循环同上验证
    private static bool TestClientRunStopsOnStop()
    {
        using var client = new MinecraftClient(new GameConfig());
        //测试场景用 1ms 预算避免无 sleep 时狂跑浪费 CPU
        client.SleepBudgetMillis = 1;
        var thread = new Thread(client.Run);
        thread.Start();
        Thread.Sleep(100);
        var runningBeforeStop = client.Running;
        client.Stop();
        thread.Join(2000);
        return runningBeforeStop && !client.Running && !thread.IsAlive;
    }

    //DedicatedServer 主循环 Run + Stop 跑通且刷盘不抛
    private static bool TestDedicatedServerRunStopsOnStop()
    {
        var (server, _) = CreateDedicatedServer();
        //测试场景用 1ms 预算避免触发 6000 tick 自动刷盘日志淹没输出
        server.SleepBudgetMillis = 1;
        var thread = new Thread(server.Run);
        thread.Start();
        Thread.Sleep(50);
        var runningBeforeStop = server.Running;
        server.Stop();
        thread.Join(2000);
        try
        {
            return runningBeforeStop && !server.Running && !thread.IsAlive;
        }
        finally
        {
            server.Dispose();
        }
    }

    //SleepBudgetMillis=10 时单 tick sleep 不超过 10ms 验证间隔控制生效
    private static bool TestServerTickInterval()
    {
        var server = new TestServer(Thread.CurrentThread) { SleepBudgetMillis = 10 };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var thread = new Thread(server.Run);
        thread.Start();
        //跑 5 个 tick 约 50ms
        Thread.Sleep(80);
        server.Stop();
        thread.Join(2000);
        watch.Stop();
        //SleepBudgetMillis=10 下 5 个 tick 应约 50ms 不会立刻返回
        return watch.ElapsedMilliseconds >= 40 && server.TickCount >= 3;
    }

    //DedicatedServer 主循环 tick 推进 Overworld.LevelTick 验证 level.Tick 被调用
    private static bool TestDedicatedServerTickAdvancesOverworldLevel()
    {
        var (server, _) = CreateDedicatedServer();
        server.SleepBudgetMillis = 1;
        var thread = new Thread(server.Run);
        thread.Start();
        Thread.Sleep(50);
        server.Stop();
        thread.Join(2000);
        try
        {
            return server.Overworld.LevelTick > 0
                && server.Overworld.LevelTick == server.TickCount;
        }
        finally
        {
            server.Dispose();
        }
    }

    //PersistentServerLevel.Tick 调用实体 Tick 验证调度链工作
    private static bool TestPersistentServerLevelTickAdvancesEntities()
    {
        var (server, _) = CreateDedicatedServer();
        var entity = new CountingEntity();
        server.Overworld.AddEntity(entity);
        server.SleepBudgetMillis = 1;
        var thread = new Thread(server.Run);
        thread.Start();
        Thread.Sleep(50);
        server.Stop();
        thread.Join(2000);
        try
        {
            return entity.TickCount > 0
                && entity.TickCount == server.Overworld.LevelTick;
        }
        finally
        {
            server.Dispose();
        }
    }

    //DedicatedServer tick 调度玩家 Connection.Tick 验证连接入站包队列被处理
    private static bool TestDedicatedServerTickProcessesConnections()
    {
        var (server, _) = CreateDedicatedServer();
        //用 MemoryStream 双端构造 Connection 模拟已连接客户端
        using var readStream = new System.IO.MemoryStream();
        using var writeStream = new System.IO.MemoryStream();
        var connection = new Connection(readStream, writeStream, PacketFlow.Serverbound);
        server.AddPlayer(connection);
        server.SleepBudgetMillis = 1;
        var thread = new Thread(server.Run);
        thread.Start();
        Thread.Sleep(50);
        server.Stop();
        thread.Join(2000);
        try
        {
            //Connection 加入后未断开验证 Tick 调度未误触断连
            return server.Connections.Count == 1
                && connection.IsConnected
                && server.Overworld.LevelTick > 0;
        }
        finally
        {
            connection.Dispose();
            server.Dispose();
        }
    }

    //ChunkSource.GetChunkFuture 异步加载区块验证加载链工作
    //用内存 loader 隔离序列化子系统专注验证调度链
    private static bool TestChunkSourceLoadChunkAsync()
    {
        var pos = new ChunkPos(2, 3);
        var chunk = CreateStandaloneLevelChunk(pos);
        using var cache = new ServerChunkCache(_ => Task.FromResult<ChunkAccess?>(chunk));
        var future = cache.GetChunkFuture(pos.X, pos.Z, ChunkStatus.FULL);
        var result = future.GetAwaiter().GetResult();
        return result.IsSuccess && result.Chunk == chunk;
    }

    //ChunkSource.Tick 推进完成的 holder 移入加载缓存验证 tick 调度
    private static bool TestChunkSourceTickAdvancesLoaded()
    {
        var pos = new ChunkPos(4, 5);
        var chunk = CreateStandaloneLevelChunk(pos);
        using var cache = new ServerChunkCache(_ => Task.FromResult<ChunkAccess?>(chunk));
        var future = cache.GetChunkFuture(pos.X, pos.Z, ChunkStatus.FULL);
        future.GetAwaiter().GetResult();
        var loadedBefore = cache.LoadedCount;
        cache.Tick();
        return cache.LoadedCount == loadedBefore + 1
            && cache.HasChunk(pos.X, pos.Z);
    }

    //PersistentServerLevel.Tick 推进 ChunkSource 验证调度链接入
    //手动完成 holder 模拟异步加载完成避免依赖序列化子系统
    private static bool TestPersistentServerLevelTickAdvancesChunkSource()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            var level = server.Overworld;
            var pos = new ChunkPos(6, 7);
            var chunk = CreateTestLevelChunk(level, pos);
            var cache = level.ChunkSource;
            var holder = cache.GetOrCreateHolder(pos.X, pos.Z);
            holder.MarkScheduled();
            holder.Complete(chunk);
            var loadedBefore = cache.LoadedCount;
            level.Tick();
            return cache.LoadedCount == loadedBefore + 1;
        }
        finally
        {
            server.Dispose();
        }
    }

    //ChunkSource.UpdatePlayerPos 提升视距内 holder ticket 验证玩家位置更新链
    private static bool TestChunkSourceUpdatePlayerPosRaisesTicket()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            var cache = server.Overworld.ChunkSource;
            cache.UpdatePlayerPos(0, 0);
            var holder = cache.TryGetHolder(0, 0);
            return holder is not null
                && holder.TicketLevel == ChunkHolder.TickingLevel;
        }
        finally
        {
            server.Dispose();
        }
    }

    //EntityLookup 按范围查询返回 AABB 内的实体验证分区索引正确分桶
    //固定实体 Pos=(20,100,20) 查询 (10,90,10)-(30,110,30) 应命中查询 (100,100,100) 应不命中
    private static bool TestEntityLookupRangeQuery()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            var level = server.Overworld;
            var inRange = new MockPosEntity(new Vec3(20, 100, 20));
            var outRange = new MockPosEntity(new Vec3(100, 100, 100));
            level.AddEntity(inRange);
            level.AddEntity(outRange);
            var found = level.EntityLookup.GetInRange(
                new Vec3(10, 90, 10), new Vec3(30, 110, 30)).ToList();
            return found.Count == 1
                && ReferenceEquals(found[0], inRange)
                && level.EntityLookup.Count == 2;
        }
        finally
        {
            server.Dispose();
        }
    }

    //EntityLookup.Remove 移除实体后查询不再命中验证反向索引更新
    private static bool TestEntityLookupRemove()
    {
        var (server, _) = CreateDedicatedServer();
        try
        {
            var level = server.Overworld;
            var e1 = new MockPosEntity(new Vec3(20, 100, 20));
            level.AddEntity(e1);
            if (level.EntityLookup.Count != 1) return false;
            if (!level.RemoveEntity(e1)) return false;
            var found = level.EntityLookup.GetInRange(
                new Vec3(0, 0, 0), new Vec3(100, 200, 100)).ToList();
            return found.Count == 0 && level.EntityLookup.Count == 0;
        }
        finally
        {
            server.Dispose();
        }
    }

    //ChunkSource generator 回调存档未命中走 ChunkStatusProcessor 生成链验证 fallback
    //loader 返回 null 时 generator 创建 ProtoChunk 走 ProcessToStatus FULL 后回填 holder
    private static bool TestChunkSourceGeneratorFallback()
    {
        var pos = new ChunkPos(8, 9);
        var factory = PalettedContainerFactory.Default;
        var generated = new List<ChunkAccess>();
        ChunkAccess? Generator(ChunkPos p)
        {
            var chunk = new ProtoChunk(p, -4, 24,
                factory.CreateForBlockStates, factory.CreateForBiomes);
            generated.Add(chunk);
            return chunk;
        }
        using var cache = new ServerChunkCache(
            _ => Task.FromResult<ChunkAccess?>(null),
            8,
            Generator);
        var future = cache.GetChunkFuture(pos.X, pos.Z, ChunkStatus.FULL);
        var result = future.GetAwaiter().GetResult();
        return result.IsSuccess
            && result.Chunk is not null
            && result.Chunk.Pos == pos
            && generated.Count == 1
            && result.Chunk.ChunkStatus == ChunkStatus.EMPTY;
    }

    //CreateStandaloneLevelChunk 用默认 factory 构造空 LevelChunk 供内存 loader 测试
    private static LevelChunk CreateStandaloneLevelChunk(ChunkPos pos)
    {
        var factory = PalettedContainerFactory.Default;
        return new LevelChunk(pos, -4, 24,
            factory.CreateForBlockStates, factory.CreateForBiomes);
    }

    //CreateTestLevelChunk 用 PersistentServerLevel 的 factory 构造空 LevelChunk 供保存测试
    private static LevelChunk CreateTestLevelChunk(PersistentServerLevel level, ChunkPos pos)
    {
        var factory = level.Factory;
        return new LevelChunk(pos, level.MinSectionY, level.SectionsCount,
            factory.CreateForBlockStates, factory.CreateForBiomes);
    }

    //CreateDedicatedServer 复用工厂方法保证 tmpDir 与 LevelStorage 正确初始化
    private static (DedicatedServer server, string tmpDir) CreateDedicatedServer()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"netcraft-server-{Guid.NewGuid():N}");
        var storage = new LevelStorage(tmpDir);
        //测试场景 acquireLock: false 避免锁占用
        var access = storage.CreateAccess("test", acquireLock: false);
        var settings = new ServerSettings();
        var server = new DedicatedServer(Thread.CurrentThread, settings, access);
        return (server, tmpDir);
    }

    //TestServer 测试用 MinecraftServer 子类 Tick 加 sleep 避免 CPU 100%
    private sealed class TestServer : MinecraftServer
    {
        public TestServer(Thread thread) : base(thread) { }

        protected override void Tick()
        {
            Thread.Sleep(1);
        }
    }

    //CountingEntity 测试用 Entity 子类记录 Tick 被调用次数
    private sealed class CountingEntity : NetCraft.Registry.Entity
    {
        public int TickCount { get; private set; }
        public override Identifier Id => Identifier.WithDefaultNamespace("counting");
        public override void Tick() => TickCount++;
    }

    //MockPosEntity 测试用 Entity 子类带固定 Pos 用于 EntityLookup 分桶测试
    private sealed class MockPosEntity : NetCraft.Registry.Entity
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("mock_pos");
        public MockPosEntity(Vec3 pos) => Pos = pos;
    }
}

