using NetCraft.Config;
using NetCraft;
using NetCraft.Game;
using NetCraft.Logging;

namespace NetCraft.Loader;

//NetCraft.Loader 启动器入口对应原版 launcher
//控制台 EXE 负责模式分发调用 ServerMain.Run 或 ClientMain.Run
//默认 --client 模式其余参数透传给 Run 由现有 LaunchOptions+GameOptions 机制解析
public static class Program
{
    //Main 启动器入口
    //声明 --server/--client 为内核 flag 让 LaunchOptions 自动吞掉不污染下游业务参数
    //扫描 args 识别模式后透传整个 args 调对应 Run
    public static int Main(string[] args)
    {
        Log.Debug($"Main 入口 args={string.Join(",", args)}");
        LaunchOptions.DeclareKernelFlag("server");
        LaunchOptions.DeclareKernelFlag("client");
        LaunchOptions.DeclareKernelFlag("debug");

        //检测 --debug flag 开启全局调试模式触发 NetCraftKernel.PrintStartupInfo 等调试行为
        if (Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;

        var mode = DetectMode(args);
        var result = mode switch
        {
            LaunchMode.Server => RunServer(args),
            _ => RunClient(args)
        };
        Log.Debug($"Main 出口 result={result}");
        return result;
    }

    //DetectMode 扫描 args 取第一个出现的 --server/--client 决定模式
    //未传模式 flag 默认 Client 对齐原版客户端优先
    private static LaunchMode DetectMode(string[] args)
    {
        //Log.Debug($"DetectMode 入口 args={string.Join(",", args)}");
        foreach (var arg in args)
        {
            if (arg == "--server")
            {
                //Log.Debug($"DetectMode 出口 result={LaunchMode.Server}");
                return LaunchMode.Server;
            }
            if (arg == "--client")
            {
                //Log.Debug($"DetectMode 出口 result={LaunchMode.Client}");
                return LaunchMode.Client;
            }
        }
        //Log.Debug($"DetectMode 出口 result={LaunchMode.Client}");
        return LaunchMode.Client;
    }

    //RunServer 透传 args 调 ServerMain.Run
    //ServerMain 内部订阅 GameOptions 后触发 NetCraftKernel.Initialize 解析剩余参数
    private static int RunServer(string[] args)
    {
        //Log.Debug($"RunServer 入口 args={string.Join(",", args)}");
        ServerMain.Run(args);
        //Log.Debug($"RunServer 出口 result=0");
        return 0;
    }

    //RunClient 透传 args 调 ClientMain.Run
    //ClientMain 内部订阅 GameOptions 后触发 NetCraftKernel.Initialize 解析剩余参数
    private static int RunClient(string[] args)
    {
        Log.Debug($"调用ClientMain.Run...");
        ClientMain.Run(args);
        Log.Debug($"RunClient 出口 ");
        return 0;
    }
}

//LaunchMode 启动模式枚举
//Client 客户端含渲染与输入Server 纯服务端
internal enum LaunchMode
{
    Client,
    Server
}
