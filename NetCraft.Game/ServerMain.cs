using System.Threading;
using NetCraft;
using NetCraft.DataFixer;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.DFU;
using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Storage;
using NetCraft.Util.Random;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;

namespace NetCraft.Game;

//ServerMain 服务端主入口
//对应原版 net.minecraft.server.Main
//串联 内核初始化 + 启动参数解析 + 服务端业务调度
//本类库非 EXE 调用方需自行创建 Main 函数调用 ServerMain.Run(args)
public static class ServerMain
{
    private static int _started;

    //Run 服务端启动主函数
    //args 命令行参数 内核识别的消费未识别的通过事件传给 GameOptions
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ServerMain 已启动忽略重复调用");
            Log.Debug("Run 出口");
            return;
        }
        Log.Debug($"Run 入口 args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ServerMain));
        Log.Info("NetCraft 服务端启动中");

        //1. 创建 GameOptions 订阅内核未识别参数事件
        var options = new GameOptions();
        options.Subscribe();

        //2. 初始化内核触发 LaunchOptions.Parse
        NetCraftKernel.Initialize(args);

        //3. 注册 Game 层预定义组件类型触发内置注册表 bootstrap 回调并验证
        //   必须在 BootstrapClass.BootStrap 之前注册因 BootStrap 会 Freeze 所有注册表
        //   GameBootstrap 注册 Blocks/Noises/DensityFunction 必须在 Freeze 之前完成
        //   否则 NoiseGeneratorSettings.Overworld 取 Noises.AquiferBarrier 等会抛 Missing key
        DataComponents.Bootstrap();
        GameBootstrap.Bootstrap();
        BootstrapClass.BootStrap();

        //4. 解析剩余挂起参数
        options.FlushPending();

        //4.5 设置 --output-dir 覆盖基准未传则用 AppContext.BaseDirectory
        //    下游统一从 AppPaths 取避免相对路径被解释为运行时工作目录
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //4.6 提取 jar 资源到 assets/ 与 data/ 同时把 pack.mcmeta 复制到根目录
        //   与 ClientMain 步骤 5 对齐让服务端也能加载原版资源与数据驱动内容
        AssetsExtractor.Extract(options);

        //4.7 构造 ResourceManager 加载 vanilla pack 并触发 Tags 等数据驱动重载
        //   必须在 BootstrapClass.BootStrap 之后因 BindAll 要求 Registry 已 Freeze
        //   vanilla pack 以 BaseDirectory 为根读 assets/ 与 data/ 子树与 pack.mcmeta
        //   LoadResources 注册 TagsReloadListener 调 LoadBuiltinTags+BindAll
        var registryAccess = BuiltInRegistries.CreateRegistryAccess();
        var resourceManager = new ResourceManager();
        resourceManager.AddPack(new Pack(
            id: Identifier.WithDefaultNamespace("vanilla"),
            title: "Minecraft",
            description: "The default data for Minecraft",
            priority: 0,
            isBuiltin: true,
            resources: new FolderPackResources("vanilla", AppPaths.BaseDirectory)));
        var rsr = ReloadableServerResources.LoadResources(resourceManager, registryAccess);
        Log.Info($"服务端资源加载完成监听器 {rsr.Listeners.Count} 个资源包 {resourceManager.Packs.Count} 个");

        //5. 加载 server.properties 不存在则生成默认
        //   端口 max-players 难度 正版验证 PVP 视野距离等
        var settings = ServerSettings.LoadOrGenerate(AppPaths.ServerPropertiesPath);
        Log.Info($"服务端配置端口 {settings.ServerPort} 世界 {settings.LevelName} 模式 {settings.Gamemode} 难度 {settings.Difficulty}");

        //6. 初始化世界存储
        //   参考原生 LevelStorageSource.createWorldStorage 加载 anvil 区域文件
        //   NetCraft.Storage 已实现 LevelStorage 包装 RegionFileStorage 提供世界存储入口
        var levelStorage = new LevelStorage(AppPaths.WorldsDir);
        var levelAccess = levelStorage.CreateAccess(settings.LevelName);
        Log.Info($"世界存储入口已创建 {levelAccess.WorldDir}");

        //7. 构建 DataFixer 与 DedicatedServer 实例并启动主循环
        //   GameDataFixers.BuildV1_21Fixer 注册 13 个 Schema 与 18 个 Fix 覆盖 1.20.2 到 1.21.4 升级链
        //   DedicatedServer 内部创建 OVERWORLD 维度 SimpleRegionStorage 与 PersistentServerLevel 接入存档
        //   接入 NoiseBasedChunkGenerator + MultiNoiseBiomeSource 让世界生成子系统真正被使用
        //   rsr 持有 ResourceManager 与 Tags 供后续 /reload 命令重载
        //   server.Run 阻塞当前线程直到 server.Stop 被调用 Stop 时强制刷盘
        var dataFixer = GameDataFixers.BuildV1_21Fixer();
        var seed = ParseLevelSeed(settings.LevelSeed);
        var random = RandomSource.Create(seed);
        var biomeSource = new MultiNoiseBiomeSource();
        var chunkGenerator = new NoiseBasedChunkGenerator(biomeSource, NoiseGeneratorSettings.Overworld());
        using var server = new DedicatedServer(
            Thread.CurrentThread,
            settings,
            levelAccess,
            dataFixer,
            chunkGenerator: chunkGenerator,
            random: random,
            rsr: rsr);

        //注册 Ctrl+C 触发优雅关闭
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        //启动 TCP 端口监听线程接受新连接
        server.StartNetwork();
        server.Run();

        //8. 清理退出
        options.Unsubscribe();
        Log.Info("NetCraft 服务端已退出");
        Log.Debug("Run 出口");
    }

    //WaitForServer 阻塞调用线程直到服务端关闭
    //外部 EXE 用此方法保持进程不退出
    public static void WaitForServer(CancellationToken cancellationToken = default)
    {
        Log.Debug($"WaitForServer 入口 cancellationToken={cancellationToken}");
        try
        {
            cancellationToken.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //正常退出
        }
        Log.Debug("WaitForServer 出口");
    }

    //ParseLevelSeed 解析 server.properties 中的 level-seed 字段
    //空或非数字返回随机 long 数字返回 long.Parse 结果对齐原版 seed 语义
    private static long ParseLevelSeed(string? seedStr)
    {
        if (string.IsNullOrWhiteSpace(seedStr)) return RandomSupport.GenerateUniqueSeed();
        if (long.TryParse(seedStr, out var seed)) return seed;
        Log.Warning($"level-seed 非数字 {seedStr} 改用随机种子");
        return RandomSupport.GenerateUniqueSeed();
    }
}
