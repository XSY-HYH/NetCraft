using System.Diagnostics;
using System.Threading;
using NetCraft.Game.Gui;
using NetCraft.Game.Gui.Screens;
using NetCraft.Game.World.Entity;
using NetCraft.Gpu.Vulkan;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Client;

//MinecraftClient 客户端主循环对应原版 net.minecraft.client.Minecraft
//持有 GameConfig 与运行状态提供 Run 与 Stop 主循环骨架
//阶段 11.35 接入循环空壳 11.46 加帧率控制 11.49 接入双模式 + Tick 四件套
//双模式有 gpuApp 走窗口驱动 Tick 挂 FrameUpdate 无 gpuApp 走 while+sleep 供测试
public sealed class MinecraftClient : IDisposable
{
    //TargetFps 目标每秒帧数对齐 60 FPS
    public const int TargetFps = 60;
    //TargetFrameMillis 单帧目标时长约 16.67ms
    public const int TargetFrameMillis = 1000 / TargetFps;

    private volatile bool _running;
    private long _frameCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //SleepBudgetMillis 单帧 sleep 预算测试场景设 0 加速跑测生产用默认 16ms
    private int _sleepBudgetMillis = TargetFrameMillis;
    //GpuApp 可空 null 时走 Headless 模式供服务器集成和无 GPU 测试
    private readonly VulkanGuiApp? _gpuApp;
    //Connection 可空 null 时跳过网络包处理供单机或测试场景
    private readonly Connection? _connection;
    //ScreenManager 可空 null 时跳过屏幕逻辑供 Headless 测试
    private readonly ScreenManager? _screens;
    //ServerResources 可空 null 时跳过 Tags 等数据驱动查询供 Headless 测试
    private readonly ReloadableServerResources? _rsr;

    //GameConfig 客户端配置 options.txt 加载结果
    public GameConfig Config { get; }

    //Player 本地玩家实体 HUD 和业务读取 Health/Food/Pos 等状态
    public Player Player { get; } = new();

    //GpuApp 持有的 Vulkan GUI 应用窗口驱动模式非空
    public VulkanGuiApp? GpuApp => _gpuApp;

    //Connection 持有的网络连接非空时 Tick 调其 Tick 处理入站包
    public Connection? Connection => _connection;

    //Screens 屏幕管理器非空时驱动屏幕生命周期
    public ScreenManager? Screens => _screens;

    //ServerResources 服务端可重载资源集合非空时供客户端 Tags 等数据驱动查询
    public ReloadableServerResources? ServerResources => _rsr;

    //SetScreen 切换屏幕委托给 ScreenManager null 表示关闭当前屏幕
    public void SetScreen(Screen? screen) => _screens?.SetScreen(screen);

    //PushScreen 压栈当前屏幕进子菜单 Esc 可回上一级
    public void PushScreen(Screen screen) => _screens?.PushScreen(screen);

    //PopScreen 弹栈回上一级栈空关闭当前屏幕
    public void PopScreen() => _screens?.PopScreen();

    public MinecraftClient(GameConfig config, VulkanGuiApp? gpuApp = null, Connection? connection = null, ReloadableServerResources? rsr = null)
    {
        Config = config;
        _gpuApp = gpuApp;
        _connection = connection;
        _rsr = rsr;
        //有 GpuApp 时创建 ScreenManager 接管窗口控件树
        _screens = gpuApp is not null ? new ScreenManager(this, gpuApp.Window) : null;
        //订阅 swapchain 重建事件窗口 resize 时让 ScreenManager 重布局当前 Screen
        if (gpuApp is not null && _screens is not null)
            gpuApp.SwapchainRecreated += () => _screens.Resized();
    }

    //Running 是否在主循环中
    public bool Running => _running;

    //FrameCount 累计帧数用于诊断
    public long FrameCount => _frameCount;

    //SleepBudgetMillis 单帧 sleep 预算测试场景设 0 加速跑测生产用默认 16ms
    //仅 Headless 模式生效窗口驱动模式由 vsync 控制帧率
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run 主循环入口阻塞调用线程直到 Stop 被调用
    //有 gpuApp 时 Render 走窗口循环 Tick 由 gpuApp 内部 Tick 线程驱动 FrameTick 派发
    //无 gpuApp 时走 while+sleep 保持 60 FPS 供 Headless 测试
    public void Run()
    {
        if (_running) return;
        _running = true;
        if (_gpuApp is not null)
        {
            Log.Info($"MinecraftClient 窗口驱动模式启动渲染距离 {Config.RenderDistance} FOV {Config.Fov}");
            _screens?.SetScreen(new TitleScreen());
            _gpuApp.FrameTick += OnFrameTick;
            try
            {
                _gpuApp.Run();
            }
            finally
            {
                _gpuApp.FrameTick -= OnFrameTick;
                _running = false;
                Log.Info($"MinecraftClient 窗口驱动循环已退出累计帧 {_frameCount}");
            }
            return;
        }
        Log.Info($"MinecraftClient Headless 模式启动渲染距离 {Config.RenderDistance} FOV {Config.Fov}");
        var watch = Stopwatch.StartNew();
        try
        {
            while (_running)
            {
                var frameStart = watch.Elapsed;
                Tick(TargetFrameMillis / 1000.0);
                if (_sleepBudgetMillis > 0)
                {
                    var elapsed = (int)(watch.Elapsed - frameStart).TotalMilliseconds;
                    var remaining = _sleepBudgetMillis - elapsed;
                    if (remaining > 0) Thread.Sleep(remaining);
                }
            }
        }
        finally
        {
            _running = false;
            Log.Info($"MinecraftClient Headless 循环已退出累计帧 {_frameCount}");
        }
    }

    //OnFrameTick VulkanGuiApp.Tick 线程 20tps 触发本回调做业务四件套
    //阶段7 Tick/Render 解耦后业务全在 Tick 线程 SubmitFrame 由 VulkanGuiApp 自动调
    private void OnFrameTick(double delta)
    {
        Tick(delta);
    }

    //Tick 单帧逻辑四件套
    //Input.Poll 有 GPU 时从队列派发输入到 GuiWindow
    //Layout 处理 resize 后的脏标记独占控件树无竞争
    //Gui.Update 调 Window.Update 推进控件动画状态
    //Network.ProcessPackets 处理入站包
    //Gpu.Render 由 VulkanGuiApp 在 FrameTick 后自动调 SubmitFrame 发布快照
    private void Tick(double delta)
    {
        _gpuApp?.PollInput();
        _screens?.ProcessLayoutIfDirty();
        _gpuApp?.Window.Update(delta);
        _screens?.Tick();
        _connection?.Tick();
        _frameCount++;
    }

    //Stop 触发主循环退出由窗口关闭或 ShutdownHook 调用
    //窗口驱动模式下调 gpuApp.RequestClose 让 _window.Run 退出
    public void Stop()
    {
        Log.Info("MinecraftClient 收到停止信号");
        _running = false;
        _shutdownCts.Cancel();
        _gpuApp?.RequestClose();
    }

    public void Dispose()
    {
        if (_gpuApp is not null && _screens is not null)
            _gpuApp.RawKeyDown -= _screens.HandleRawKeyDown;
        _shutdownCts.Dispose();
        _gpuApp?.Dispose();
    }
}
