using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Util;

namespace NetCraft;

//NetCraft 内核主入口（聚合类库的公开 API）。
//对应原版 net.minecraft.server.Main + net.minecraft.Bootstrap 的入口职责。
//注意：本类库不是 exe，调用方需自行创建 Main 函数并调用 Initialize。
public static class NetCraftKernel
{
    private static int _initialized;

    //初始化 NetCraft 内核。包括：
    //  <item>初始化内嵌程序集加载器（从内嵌资源加载子库 dll）</item>
    //  <item>解析启动参数（内核识别的消费，未识别的通过 LaunchOptions.UnhandledArgument 事件广播）</item>
    //  <item>打印启动版本/协议信息（Debug 时）</item>
    //幂等：多次调用只生效一次。
    //args 命令行参数 调用前订阅者应已订阅 LaunchOptions.UnhandledArgument
    public static void Initialize(string[]? args = null)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            Log.Debug("Initialize 出口");
            return;
        }
        Log.Debug($"Initialize 入口 args={(args == null ? "null" : string.Join(",", args))}");

        // 1. 先初始化内嵌资源加载器，确保后续类型解析能找到子库
        EmbeddedAssemblyLoader.Initialize();

        //设置日志源为内核
        Log.SetClassSource(typeof(NetCraftKernel));

        //启动字符画banner
        PrintBanner();

        // 2. 解析启动参数 内核识别的消费 未识别的通过事件广播
        //    订阅者必须在调用 Initialize 前订阅 UnhandledArgument
        if (args is { Length: > 0 })
        {
            LaunchOptions.Parse(args);
        }

        // 3. 调试模式开启时打印启动信息（运行时 --debug flag 触发 Release 构建也可用）
        if (DebugMode.IsEnabled)
            PrintStartupInfo();
        Log.Debug("Initialize 出口");
    }

    //获取内核版本。
    public static string Version => SharedConstants.Version;

    //获取当前协议版本号。
    public static int ProtocolVersion => SharedConstants.ProtocolVersion;

    //PrintStartupInfo 打印内核启动详情对应原版 Main 输出版本/协议/世界数据版本/内嵌子库
    //由 DebugMode.IsEnabled 运行时触发 Release 构建也可通过 --debug 开启
    private static void PrintStartupInfo()
    {
        Log.Debug("PrintStartupInfo 入口");
        Log.Info($"Initializing kernel v{SharedConstants.Version}");
        Log.Info($"Protocol version: {SharedConstants.ProtocolVersion} (min {SharedConstants.ProtocolVersionLowerBound})");
        Log.Info($"World data version: {SharedConstants.WorldDataVersion}");
        Log.Info($"Target TPS: {SharedConstants.TicksPerSecond}");

        var embedded = EmbeddedAssemblyLoader.ListEmbeddedAssemblies();
        if (embedded.Count > 0)
        {
            Log.Info($"Embedded sub-libraries ({embedded.Count}):");
            foreach (var name in embedded)
                Log.Info($"  - {name}");
        }
        Log.Debug("PrintStartupInfo 出口");
    }

    //打印启动字符画banner
    private static void PrintBanner()
    {
        ConsoleAnsiArtist.PrintRainbowText("NetCraft");
    }
}

