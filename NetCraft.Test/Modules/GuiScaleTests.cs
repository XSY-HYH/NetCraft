using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//GuiScaleTests GUI scale 整数缩放纯逻辑测试不依赖 Vulkan
//覆盖 GuiWindow.UpdateSurfaceSize 的 guiScale 计算 Scaled 尺寸及鼠标坐标转换
//对应阶段 2 GUI scale 自适应布局核心逻辑
internal static class GuiScaleTests
{
    public const string Module = "guiscale";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("GuiScale 320x240 -> 1", TestScale320x240);
        yield return ("GuiScale 640x480 -> 2", TestScale640x480);
        yield return ("GuiScale 800x600 -> 2", TestScale800x600);
        yield return ("GuiScale 1280x720 -> 3", TestScale1280x720);
        yield return ("GuiScale 1920x1080 -> 4", TestScale1920x1080);
        yield return ("GuiScale 100x100 -> 1 floor at min", TestScale100x100);
        yield return ("ScaledWidth/Height = floor(surface/GuiScale)", TestScaledFloor);
        yield return ("Window Width/Height follows Scaled", TestWindowFollowsScaled);
        yield return ("UpdateSurfaceSize recomputes all fields", TestUpdateRecomputes);
        yield return ("Mouse coords divided by GuiScale on down", TestMouseDownCoordsDivided);
        yield return ("Mouse coords divided by GuiScale on move", TestMouseMoveCoordsDivided);
    }

    //320x240 是最小阈值 guiScale=1 scaled 与 surface 相等
    private static bool TestScale320x240()
    {
        var w = new GuiWindow(320, 240);
        return w.GuiScale == 1 && w.ScaledWidth == 320 && w.ScaledHeight == 240;
    }

    //640x480 横纵比 320x240 两倍 guiScale=2
    private static bool TestScale640x480()
    {
        var w = new GuiWindow(640, 480);
        return w.GuiScale == 2 && w.ScaledWidth == 320 && w.ScaledHeight == 240;
    }

    //800x600 max(800/320, 600/240)=max(2,2)=2
    private static bool TestScale800x600()
    {
        var w = new GuiWindow(800, 600);
        return w.GuiScale == 2 && w.ScaledWidth == 400 && w.ScaledHeight == 300;
    }

    //1280x720 max(1280/320, 720/240)=max(4,3)=3
    private static bool TestScale1280x720()
    {
        var w = new GuiWindow(1280, 720);
        return w.GuiScale == 3 && w.ScaledWidth == 426 && w.ScaledHeight == 240;
    }

    //1920x1080 max(1920/320, 1080/240)=max(6,4)=4
    private static bool TestScale1920x1080()
    {
        var w = new GuiWindow(1920, 1080);
        return w.GuiScale == 4 && w.ScaledWidth == 480 && w.ScaledHeight == 270;
    }

    //100x100 不足最小阈值 floor 到 guiScale=1
    private static bool TestScale100x100()
    {
        var w = new GuiWindow(100, 100);
        return w.GuiScale == 1 && w.ScaledWidth == 100 && w.ScaledHeight == 100;
    }

    //ScaledWidth/Height = floor(surface/guiScale) 1280x720 留余数验证 floor 不四舍五入
    private static bool TestScaledFloor()
    {
        var w = new GuiWindow(1280, 720);
        //1280/3=426.67 floor=426 720/3=240 整除
        return w.ScaledWidth == 426 && w.ScaledHeight == 240;
    }

    //Window 自身 Width/Height 跟随 ScaledWidth/Height 供布局坐标基准
    private static bool TestWindowFollowsScaled()
    {
        var w = new GuiWindow(1920, 1080);
        return w.Width == w.ScaledWidth && w.Height == w.ScaledHeight;
    }

    //UpdateSurfaceSize 二次调用重新计算所有字段旧值被覆盖
    private static bool TestUpdateRecomputes()
    {
        var w = new GuiWindow(800, 600);
        w.UpdateSurfaceSize(1920, 1080);
        return w.GuiScale == 4 && w.ScaledWidth == 480 && w.ScaledHeight == 270
            && w.SurfaceWidth == 1920 && w.SurfaceHeight == 1080
            && w.Width == 480 && w.Height == 270;
    }

    //ProcessMouseDown 把 actual 像素除以 GuiScale 转 scaled 再派发到命中控件
    //guiScale=2 时 actual (400,300) 控件应收到 scaled (200,150)
    private static bool TestMouseDownCoordsDivided()
    {
        var w = new GuiWindow(800, 600);
        var probe = new ProbeControl { X = 195, Y = 145, Width = 20, Height = 20 };
        w.Add(probe);
        w.ProcessMouseDown(GuiMouseButton.Left, 400, 300);
        return probe.LastDownX == 200 && probe.LastDownY == 150;
    }

    //ProcessMouseMove 同样把 actual 像素除以 GuiScale 转 scaled
    private static bool TestMouseMoveCoordsDivided()
    {
        var w = new GuiWindow(800, 600);
        var probe = new ProbeControl { X = 195, Y = 145, Width = 20, Height = 20 };
        w.Add(probe);
        w.ProcessMouseMove(400, 300);
        return probe.LastMoveX == 200 && probe.LastMoveY == 150;
    }

    //ProbeControl 记录收到的鼠标坐标用于断言 scaled 转换
    //用 MouseDown/MouseMove 事件而非重写 OnMouseDown 跨程序集 protected internal 不允许重写
    private sealed class ProbeControl : GuiControl
    {
        public int LastDownX = -1, LastDownY = -1;
        public int LastMoveX = -1, LastMoveY = -1;

        public ProbeControl()
        {
            MouseDown += (_, e) => { LastDownX = e.X; LastDownY = e.Y; };
            MouseMove += (_, e) => { LastMoveX = e.X; LastMoveY = e.Y; };
        }

        public override void Render(IGuiRenderContext context) { }
    }
}
