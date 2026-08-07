using System.Diagnostics;
using System.Threading;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//MinecraftServer 服务端主循环对应原版 net.minecraft.server.MinecraftServer
//持有运行状态与 Shutdown 信号提供 Tick 与 Run 主循环骨架
//阶段 11.35 接入循环空壳阶段 11.46 加 tick 间隔控制避免 CPU 100% 对齐原版 20 TPS
//子类 DedicatedServer 接入 PersistentServerLevel 真实存档调度
public abstract class MinecraftServer
{
    //TargetTps 目标每秒 tick 数对齐原版 20 TPS
    public const int TargetTps = 20;
    //TargetTickMillis 单 tick 目标时长 50ms
    public const int TargetTickMillis = 1000 / TargetTps;

    private readonly Thread _serverThread;
    private volatile bool _running;
    private long _tickCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //SleepBudgetMillis 单 tick sleep 预算小于 1 时不 sleep 避免 CPU 100%
    private int _sleepBudgetMillis = TargetTickMillis;

    protected MinecraftServer(Thread serverThread)
    {
        _serverThread = serverThread;
    }

    //Running 是否在主循环中
    public bool Running => _running;

    //TickCount 累计 Tick 数用于诊断
    public long TickCount => _tickCount;

    //ServerThread 主循环所在线程用于诊断
    public Thread ServerThread => _serverThread;

    //SleepBudgetMillis 单 tick sleep 预算测试场景设 0 加速跑测生产用默认 50ms
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run 主循环入口阻塞调用线程直到 Stop 被调用
    //对齐原版 run() 循环每 tick 后按 watch 计算剩余时间 sleep 保持 20 TPS
    public void Run()
    {
        if (_running) return;
        _running = true;
        Log.Info("MinecraftServer 主循环启动");
        var watch = Stopwatch.StartNew();
        try
        {
            while (_running)
            {
                var tickStart = watch.Elapsed;
                Tick();
                _tickCount++;
                if (_sleepBudgetMillis > 0)
                {
                    var elapsed = (int)(watch.Elapsed - tickStart).TotalMilliseconds;
                    var remaining = _sleepBudgetMillis - elapsed;
                    if (remaining > 0) Thread.Sleep(remaining);
                }
            }
        }
        finally
        {
            _running = false;
            Log.Info($"MinecraftServer 主循环已退出累计 tick {_tickCount}");
        }
    }

    //Tick 单帧逻辑基类空壳子类按需重写
    //原版 Tick 含世界推进/玩家调度/网络处理此处仅做骨架
    protected virtual void Tick()
    {
    }

    //Stop 触发主循环退出由外部或 ShutdownHook 调用
    //子类重写时必须调 base.Stop 保证 _running 与 Cts 状态
    public virtual void Stop()
    {
        Log.Info("MinecraftServer 收到停止信号");
        _running = false;
        _shutdownCts.Cancel();
    }

    //WaitForShutdown 阻塞调用线程直到服务端关闭外部 EXE 用此保持进程
    public void WaitForShutdown()
    {
        try
        {
            _shutdownCts.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //正常退出
        }
    }
}
