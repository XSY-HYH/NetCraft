using System.Threading;
using NetCraft;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.Client;
using NetCraft.Game.World.Items;
using NetCraft.Gpu.Vulkan;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;

namespace NetCraft.Game;

//ClientMain 客户端主入口
//对应原版 net.minecraft.client.main.Main
//串联 内核初始化 + 启动参数解析 + 素材提取 + 客户端业务调度
//本类库非 EXE 调用方需自行创建 Main 函数调用 ClientMain.Run(args)
public static class ClientMain
{
    private static int _started;

    //Run 客户端启动主函数
    //args 命令行参数 内核识别的消费未识别的通过事件传给 GameOptions
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ClientMain 已启动忽略重复调用");
            Log.Debug("Run 出口");
            return;
        }
        Log.Debug($"Run 入口 args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ClientMain));
        Log.Info("NetCraft 客户端启动中");

        //1. 创建 GameOptions 订阅内核未识别参数事件
        //   必须在 NetCraftKernel.Initialize 之前订阅才能收到事件
        var options = new GameOptions();
        options.Subscribe();

        //2. 初始化内核（触发 LaunchOptions.Parse 内核识别的消费未识别的发出事件）
        NetCraftKernel.Initialize(args);

        //3. 注册 Game 层预定义组件类型触发内置注册表 bootstrap 回调并验证
        //   必须在 BootstrapClass.BootStrap 之前注册因 BootStrap 会 Freeze 所有注册表
        //   GameBootstrap 注册 Blocks/Noises/DensityFunction 必须在 Freeze 之前完成
        //   否则世界生成相关 NoiseGeneratorSettings 取 Noises.* 会抛 Missing key
        DataComponents.Bootstrap();
        GameBootstrap.Bootstrap();
        BootstrapClass.BootStrap();

        //4. 解析剩余挂起的 --opt 参数若无后续 value 降级为 flag
        options.FlushPending();

        //4.5 设置 --output-dir 覆盖基准未传则用 AppContext.BaseDirectory
        //    下游统一从 AppPaths 取避免相对路径被解释为运行时工作目录
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //5. 提取 jar 和音频素材到固定目录
        //   核心业务后续直接从 assets 和 assets/sounds 加载不依赖本步骤
        //   同时把 pack.mcmeta 复制到根目录与 assets/data 同级
        AssetsExtractor.Extract(options);

        //5.5 构造 ResourceManager 加载 vanilla pack 并触发 Tags 等数据驱动重载
        //    客户端同样需要 Tags 用于物品/方块标签查询（如工具等级判断）
        //    必须在 BootstrapClass.BootStrap 之后因 BindAll 要求 Registry 已 Freeze
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
        Log.Info($"客户端资源加载完成监听器 {rsr.Listeners.Count} 个资源包 {resourceManager.Packs.Count} 个");

        //6. 加载客户端配置 options.txt
        //   demo 模式 fullscreen 渲染距离 FOV gamma 等字段从 options.txt 读取
        //   文件不存在返回默认配置不抛
        var gameConfig = GameConfig.Load(AppPaths.OptionsPath);
        if (options.HasFlag("demo")) gameConfig.Demo = true;
        if (options.HasFlag("fullscreen")) gameConfig.Fullscreen = true;
        Log.Info($"客户端配置渲染距离 {gameConfig.RenderDistance} FOV {gameConfig.Fov} 语言 {gameConfig.Language}");

        //7. 创建 MinecraftClient 实例并启动主循环
        //   阶段 11.49 传入 VulkanGuiApp 走窗口驱动模式
        //   MinecraftClient 持有 gpuApp 所有权 Dispose 时释放
        //   rsr 传入供后续客户端 Tags 查询或 /reload 重载
        //   minecraft.Run 阻塞当前线程直到 minecraft.Stop 被调用
        using var minecraft = new MinecraftClient(gameConfig, new VulkanGuiApp(gameConfig.EnableVsync, 800, 600), rsr: rsr);

        //注册 Ctrl+C 触发优雅关闭
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            minecraft.Stop();
        };

        minecraft.Run();

        //8. 清理退出
        options.Unsubscribe();
        Log.Info("NetCraft 客户端已退出");
        Log.Debug("Run 出口");
    }
}
