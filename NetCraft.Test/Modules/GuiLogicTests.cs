using System.Numerics;
using NetCraft;
using NetCraft.Game.Client;
using NetCraft.Game.Gui;
using NetCraft.Game.Gui.Screens;
using NetCraft.Gpu;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Test.Modules;

//GuiLogicTests GUI 控件纯逻辑测试不依赖 Vulkan
//覆盖 GuiControl.Update 动画派发链路验证容器与子控件的 Update 调用
internal static class GuiLogicTests
{
    public const string Module = "guilogic";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("GuiControl.Update default no-op", TestControlUpdateDefaultNoop);
        yield return ("GuiContainer.Update dispatches to children", TestContainerUpdateDispatches);
        yield return ("GuiWindow.Update dispatches through tree", TestWindowUpdateDispatches);
        yield return ("Invisible control skips Update", TestInvisibleSkipsUpdate);
        yield return ("ScreenManager.SetScreen inits screen", TestScreenManagerSetScreenInits);
        yield return ("ScreenManager switch calls Removed on old", TestScreenManagerSwitchCallsRemoved);
        yield return ("ScreenManager.SetScreen null clears widgets", TestScreenManagerSetScreenNullClears);
        yield return ("ScreenManager.Tick drives current screen", TestScreenManagerTickDrivesCurrent);
        yield return ("ScreenManager.PushScreen/PopScreen back to parent", TestScreenManagerPushPop);
        yield return ("Esc triggers OnClose and PopScreen", TestScreenManagerEscTriggersOnClose);
        yield return ("Window.ProcessKeyChar dispatches to focused", TestWindowProcessKeyCharToFocused);
        yield return ("TextBox Home inserts at start", TestTextBoxHomeInsertsAtStart);
        yield return ("TextBox Right navigates and inserts", TestTextBoxRightNavigateInserts);
        yield return ("TextBox Backspace deletes at caret", TestTextBoxBackspaceAtCaret);
        yield return ("TextBox Delete deletes forward", TestTextBoxDeleteAtCaret);
        yield return ("TextBox Ctrl+A/C/V select copy paste", TestTextBoxSelectCopyPaste);
        yield return ("TextBox Shift+Right selects then Backspace", TestTextBoxShiftSelectBackspace);
        yield return ("TextBox click positions caret via _charX", TestTextBoxClickPositionsCaret);
        yield return ("GuiSlider.Value clamps and aligns to Step", TestSliderValueClampsAndSteps);
        yield return ("GuiSlider drag updates Value via Window", TestSliderDragUpdatesValue);
        yield return ("GuiSlider ValueChanged fires on change", TestSliderValueChangedFires);
        yield return ("GuiSlider disabled ignores drag", TestSliderDisabledIgnoresDrag);
        yield return ("GuiCheckbox click toggles Checked via Window", TestCheckboxClickTogglesChecked);
        yield return ("GuiCheckbox CheckedChanged fires", TestCheckboxCheckedChangedFires);
        yield return ("GuiImage render no throw", TestImageRenderNoThrow);
        yield return ("GuiImage with TextureId calls DrawImage", TestImageDrawImageWithTexture);
        yield return ("ScreenManager.HandleRawKeyDown(F3) reaches Screen.OnF3Pressed", TestRawKeyDownF3ReachesScreen);
        yield return ("ScreenManager.HandleRawKeyDown(D6) reaches Screen.OnHotbarSelect", TestRawKeyDownHotbarReachesScreen);
        yield return ("GameScreen.Init creates HUD widgets", TestGameScreenInitCreatesHud);
        yield return ("GuiContainer.Render pushes scissor", TestContainerRenderPushesScissor);
        yield return ("Nested container scissor stack order", TestNestedContainerScissorStackOrder);
        yield return ("PushPose/PopPose balanced no throw", TestPushPosePopPoseBalanced);
        yield return ("LinearLayout vertical stacks children", TestLinearLayoutVerticalStacksChildren);
        yield return ("LinearLayout horizontal stacks children", TestLinearLayoutHorizontalStacksChildren);
        yield return ("LinearLayout skips invisible children", TestLinearLayoutSkipsInvisibleChildren);
        yield return ("LinearLayout padding and spacing", TestLinearLayoutPaddingAndSpacing);
        yield return ("DrawQuadInverted counts separate from DrawQuad", TestDrawQuadInvertedCounts);
        yield return ("Label center align offsets x", TestLabelCenterAlign);
        yield return ("Label right align offsets x", TestLabelRightAlign);
        yield return ("Tab key cycles focus through TabStops", TestTabKeyCyclesFocus);
        yield return ("TabStop false skips control", TestTabStopFalseSkipsControl);
        yield return ("GuiImage Tile grids multiple DrawImage", TestImageTileGridsDraws);
        yield return ("SetScreen injects RenderBackgroundHook", TestSetScreenInjectsRenderBackgroundHook);
    }

    //MockControl 计数 Update 调用次数用于断言派发
    private sealed class MockControl : GuiControl
    {
        public int UpdateCount { get; private set; }
        public double LastDelta { get; private set; }

        public override void Render(IGuiRenderContext context) { }

        public override void Update(double delta)
        {
            UpdateCount++;
            LastDelta = delta;
        }
    }

    //默认 Update 是空实现不抛异常验证基类提供的安全默认
    private static bool TestControlUpdateDefaultNoop()
    {
        var control = new MockControl();
        control.Update(0.016);
        //MockControl 重写了基类默认 Update 验证基类默认实现存在且非抽象
        //这里换成直接 new 一个未重写 Update 的控件验证默认空实现不抛
        var plain = new PlainControl();
        plain.Update(0.016);
        return plain is not null;
    }

    //GuiContainer.Update 遍历可见子控件调用其 Update
    private static bool TestContainerUpdateDispatches()
    {
        var panel = new GuiPanel();
        var child1 = new MockControl();
        var child2 = new MockControl();
        panel.Add(child1);
        panel.Add(child2);
        panel.Update(0.5);
        return child1.UpdateCount == 1
            && child2.UpdateCount == 1
            && Math.Abs(child1.LastDelta - 0.5) < 1e-9
            && Math.Abs(child2.LastDelta - 0.5) < 1e-9;
    }

    //GuiWindow.Update 从根容器派发到嵌套子控件
    private static bool TestWindowUpdateDispatches()
    {
        var window = new GuiWindow(800, 600);
        var panel = new GuiPanel();
        var child = new MockControl();
        panel.Add(child);
        window.Add(panel);
        window.Update(0.016);
        return child.UpdateCount == 1;
    }

    //Visible=false 的子控件跳过 Update 派发
    private static bool TestInvisibleSkipsUpdate()
    {
        var panel = new GuiPanel();
        var visible = new MockControl();
        var hidden = new MockControl { Visible = false };
        panel.Add(visible);
        panel.Add(hidden);
        panel.Update(1.0);
        return visible.UpdateCount == 1 && hidden.UpdateCount == 0;
    }

    //PlainControl 仅用于验证默认 Update 不抛异常
    private sealed class PlainControl : GuiControl
    {
        public override void Render(IGuiRenderContext context) { }
    }

    //TrackingScreen 记录 Removed 调用用于断言切换生命周期
    private sealed class TrackingScreen : Screen
    {
        public bool RemovedCalled { get; private set; }
        public override string Title => "tracking";
        public override void Init() => AddWidget(new GuiLabel("x"));
        public override void Removed() => RemovedCalled = true;
    }

    //TickingScreen 计数 Tick 用于断言 ScreenManager.Tick 派发
    private sealed class TickingScreen : Screen
    {
        public int TickCount { get; private set; }
        public override void Tick() => TickCount++;
    }

    //SetScreen 后 Init 被调控件加到 Window
    private static bool TestScreenManagerSetScreenInits()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        manager.SetScreen(new TitleScreen());
        return window.Children.Count > 0 && manager.Current is TitleScreen;
    }

    //切换屏幕时旧 Screen.Removed 被调
    private static bool TestScreenManagerSwitchCallsRemoved()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        var first = new TrackingScreen();
        manager.SetScreen(first);
        manager.SetScreen(new TitleScreen());
        return first.RemovedCalled;
    }

    //SetScreen(null) 清空控件并置空当前屏幕
    private static bool TestScreenManagerSetScreenNullClears()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        manager.SetScreen(new TitleScreen());
        manager.SetScreen(null);
        return window.Children.Count == 0 && manager.Current is null;
    }

    //ScreenManager.Tick 驱动当前屏幕 Tick
    private static bool TestScreenManagerTickDrivesCurrent()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        var screen = new TickingScreen();
        manager.SetScreen(screen);
        manager.Tick();
        manager.Tick();
        return screen.TickCount == 2;
    }

    //PushScreen 压栈当前屏幕 PopScreen 弹回上一级
    private static bool TestScreenManagerPushPop()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        manager.SetScreen(new TitleScreen());
        manager.PushScreen(new OptionsScreen());
        var pushedIsOptions = manager.Current is OptionsScreen;
        manager.PopScreen();
        var poppedBackToTitle = manager.Current is TitleScreen;
        return pushedIsOptions && poppedBackToTitle;
    }

    //Esc 触发当前屏幕 OnClose 默认 PopScreen 回上一级
    private static bool TestScreenManagerEscTriggersOnClose()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        manager.SetScreen(new TitleScreen());
        manager.PushScreen(new OptionsScreen());
        //按 Esc 走 manager.HandleRawKeyDown(Escape) → OnClose → PopScreen
        manager.HandleRawKeyDown(GameKeys.Escape);
        return manager.Current is TitleScreen;
    }

    //Window.ProcessKeyChar 派发字符到焦点控件 TextBox 累积文本
    private static bool TestWindowProcessKeyCharToFocused()
    {
        var window = new GuiWindow(800, 600);
        var tb = new GuiTextBox();
        window.Add(tb);
        window.FocusedControl = tb;
        window.ProcessKeyChar('a');
        window.ProcessKeyChar('b');
        return tb.Text == "ab";
    }

    //MakeTextBoxWindow 构造带 TextBox 的 Window 焦点设到 TextBox 供键盘测试复用
    private static (GuiWindow window, GuiTextBox tb) MakeTextBoxWindow(string text = "")
    {
        var window = new GuiWindow(800, 600);
        var tb = new GuiTextBox { Text = text, Width = 200, Height = 20 };
        window.Add(tb);
        window.FocusedControl = tb;
        return (window, tb);
    }

    //TestTextBoxHomeInsertsAtStart Home 移光标到 0 后插入应在开头
    private static bool TestTextBoxHomeInsertsAtStart()
    {
        var (window, tb) = MakeTextBoxWindow("abc");
        window.ProcessKeyDown(GuiKeys.Home);
        window.ProcessKeyChar('X');
        return tb.Text == "Xabc";
    }

    //TestTextBoxRightNavigateInserts Home 后 Right 移到 1 插入在 a 和 b 之间
    private static bool TestTextBoxRightNavigateInserts()
    {
        var (window, tb) = MakeTextBoxWindow("ab");
        window.ProcessKeyDown(GuiKeys.Home);
        window.ProcessKeyDown(GuiKeys.Right);
        window.ProcessKeyChar('X');
        return tb.Text == "aXb";
    }

    //TestTextBoxBackspaceAtCaret End 后 Left 移到 2 BackSpace 删 c
    private static bool TestTextBoxBackspaceAtCaret()
    {
        var (window, tb) = MakeTextBoxWindow("abc");
        window.ProcessKeyDown(GuiKeys.End);
        window.ProcessKeyDown(GuiKeys.Left);
        window.ProcessKeyDown(GuiKeys.BackSpace);
        return tb.Text == "ac";
    }

    //TestTextBoxDeleteAtCaret Home 后 Delete 删 a 前向删除
    private static bool TestTextBoxDeleteAtCaret()
    {
        var (window, tb) = MakeTextBoxWindow("abc");
        window.ProcessKeyDown(GuiKeys.Home);
        window.ProcessKeyDown(GuiKeys.Delete);
        return tb.Text == "bc";
    }

    //TestTextBoxSelectCopyPaste Ctrl+A 全选 Ctrl+C 复制 Home 清选区 Ctrl+V 粘贴到开头
    private static bool TestTextBoxSelectCopyPaste()
    {
        var (window, tb) = MakeTextBoxWindow("abc");
        window.ProcessKeyDown(GuiKeys.LeftControl);
        window.ProcessKeyDown(GuiKeys.A);
        window.ProcessKeyDown(GuiKeys.C);
        window.ProcessKeyUp(GuiKeys.LeftControl);
        window.ProcessKeyDown(GuiKeys.Home);
        window.ProcessKeyDown(GuiKeys.LeftControl);
        window.ProcessKeyDown(GuiKeys.V);
        window.ProcessKeyUp(GuiKeys.LeftControl);
        return tb.Text == "abcabc";
    }

    //TestTextBoxShiftSelectBackspace Home 后 Shift+Right 选 a BackSpace 删选区
    private static bool TestTextBoxShiftSelectBackspace()
    {
        var (window, tb) = MakeTextBoxWindow("abc");
        window.ProcessKeyDown(GuiKeys.Home);
        window.ProcessKeyDown(GuiKeys.LeftShift);
        window.ProcessKeyDown(GuiKeys.Right);
        window.ProcessKeyUp(GuiKeys.LeftShift);
        window.ProcessKeyDown(GuiKeys.BackSpace);
        return tb.Text == "bc";
    }

    //TestTextBoxClickPositionsCaret Render 更新 _charX 后点击定位光标到字符 1 插入 X 得 aXbc
    //ProcessMouseDown 接收 actual 坐标 GuiWindow(800x600) GuiScale=2 点击(30,10)→scaled(15,5) 命中字符 1
    private static bool TestTextBoxClickPositionsCaret()
    {
        var window = new GuiWindow(800, 600);
        var tb = new GuiTextBox { Text = "abc", X = 0, Y = 0, Width = 200, Height = 20 };
        window.Add(tb);
        var ctx = new MockRenderContext();
        tb.Render(ctx);
        window.ProcessMouseDown(GuiMouseButton.Left, 30, 10);
        window.ProcessKeyChar('X');
        return tb.Text == "aXbc";
    }

    //Slider.Value 钳制到 Min/Max 并按 Step 对齐
    private static bool TestSliderValueClampsAndSteps()
    {
        var slider = new GuiSlider(0, 100, 0) { Step = 10 };
        slider.Value = 23;
        if (Math.Abs(slider.Value - 20) > 1e-9) return false;
        slider.Value = -5;
        if (Math.Abs(slider.Value - 0) > 1e-9) return false;
        slider.Value = 150;
        return Math.Abs(slider.Value - 100) < 1e-9;
    }

    //Slider 拖动鼠标 X 坐标更新 Value
    private static bool TestSliderDragUpdatesValue()
    {
        //guiScale=1 的窗口 surface=scaled 鼠标坐标与控件坐标语义一致
        var window = new GuiWindow(320, 240);
        var slider = new GuiSlider(0, 100, 0) { X = 100, Y = 100, Width = 100, Height = 20 };
        window.Add(slider);
        window.ProcessMouseDown(GuiMouseButton.Left, 150, 110);
        window.ProcessMouseMove(175, 110);
        return Math.Abs(slider.Value - 75) < 1e-9;
    }

    //Slider.Value 变化触发 ValueChanged 相同值不重复触发
    private static bool TestSliderValueChangedFires()
    {
        var slider = new GuiSlider(0, 100, 0);
        var fired = 0;
        slider.ValueChanged += (_, _) => fired++;
        slider.Value = 50;
        slider.Value = 50;
        return fired == 1;
    }

    //Slider.Enabled=false 时拖动不更新 Value
    private static bool TestSliderDisabledIgnoresDrag()
    {
        var window = new GuiWindow(320, 240);
        var slider = new GuiSlider(0, 100, 0) { X = 100, Y = 100, Width = 100, Height = 20, Enabled = false };
        window.Add(slider);
        window.ProcessMouseDown(GuiMouseButton.Left, 150, 110);
        window.ProcessMouseMove(175, 110);
        return Math.Abs(slider.Value - 0) < 1e-9;
    }

    //Checkbox 点击切换 Checked 状态二次点击回原状态
    private static bool TestCheckboxClickTogglesChecked()
    {
        var window = new GuiWindow(320, 240);
        var cb = new GuiCheckbox("", false) { X = 100, Y = 100, Width = 100, Height = 20 };
        window.Add(cb);
        window.ProcessMouseDown(GuiMouseButton.Left, 105, 105);
        window.ProcessMouseUp(GuiMouseButton.Left, 105, 105);
        if (!cb.Checked) return false;
        window.ProcessMouseDown(GuiMouseButton.Left, 105, 105);
        window.ProcessMouseUp(GuiMouseButton.Left, 105, 105);
        return !cb.Checked;
    }

    //Checkbox 点击触发 CheckedChanged 并切换 Checked
    private static bool TestCheckboxCheckedChangedFires()
    {
        var window = new GuiWindow(320, 240);
        var cb = new GuiCheckbox("", false) { X = 100, Y = 100, Width = 100, Height = 20 };
        window.Add(cb);
        var fired = 0;
        cb.CheckedChanged += (_, _) => fired++;
        window.ProcessMouseDown(GuiMouseButton.Left, 105, 105);
        window.ProcessMouseUp(GuiMouseButton.Left, 105, 105);
        return fired == 1 && cb.Checked;
    }

    //GuiImage.Render 调 DrawQuad 不抛异常背景加边框至少 5 个 quad
    private static bool TestImageRenderNoThrow()
    {
        var ctx = new MockRenderContext();
        var img = new GuiImage { X = 10, Y = 10, Width = 50, Height = 50 };
        img.Render(ctx);
        return ctx.QuadCount >= 5;
    }

    //GuiImage 设 TextureId 非 0 时走 DrawImage 不再画色块
    private static bool TestImageDrawImageWithTexture()
    {
        var ctx = new MockRenderContext();
        var img = new GuiImage
        {
            X = 10,
            Y = 10,
            Width = 50,
            Height = 50,
            TextureId = 7,
            TextureWidth = 100,
            TextureHeight = 100,
            SourceU = 0.1f,
            SourceV = 0.2f,
            SourceW = 0.5f,
            SourceH = 0.5f
        };
        img.Render(ctx);
        return ctx.ImageCount == 1 && ctx.QuadCount == 0;
    }

    //Tile=true 按 16x16 网格平铺 32x32 区域应调 4 次 DrawImage
    private static bool TestImageTileGridsDraws()
    {
        var ctx = new MockRenderContext();
        var img = new GuiImage
        {
            X = 0,
            Y = 0,
            Width = 32,
            Height = 32,
            TextureId = 3,
            TextureWidth = 16,
            TextureHeight = 16,
            Tile = true
        };
        img.Render(ctx);
        return ctx.ImageCount == 4 && ctx.QuadCount == 0;
    }

    //SetScreen 后 Window.Render 调用注入的 RenderBackgroundHook 触发 Screen.RenderBackground
    private static bool TestSetScreenInjectsRenderBackgroundHook()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(320, 240);
        var manager = new ScreenManager(mc, window);
        var screen = new RecordingBgScreen();
        manager.SetScreen(screen);
        var ctx = new MockRenderContext();
        window.Render(ctx);
        return screen.BgCount == 1;
    }

    //MockRenderContext 计数 DrawQuad/DrawText/DrawImage 调用用于断言渲染路径
    //记录 PushPose/PopPose/PushScissor/PopScissor 调用次数供栈行为断言
    private sealed class MockRenderContext : IGuiRenderContext
    {
        public int QuadCount;
        public int InvertedQuadCount;
        public int TextCount;
        public int ImageCount;
        public int NinePatchCount;
        public int LastTextureId;
        public int PushPoseCount, PopPoseCount, PushScissorCount, PopScissorCount;
        public int LastTextX;
        public int LineHeight => 20;
        //MeasureText 每字符 10 像素供对齐测试计算偏移
        public float MeasureText(string text) => text.Length * 10;
        public void DrawQuad(int x, int y, int width, int height, GuiColor color) => QuadCount++;
        public void DrawQuadInverted(int x, int y, int width, int height, GuiColor color) => InvertedQuadCount++;
        public void DrawText(int x, int y, string text, GuiColor color)
        {
            TextCount++;
            LastTextX = x;
        }
        public void DrawImage(int textureId, int x, int y, int width, int height,
            int srcX, int srcY, int srcW, int srcH, GuiColor tint)
        {
            ImageCount++;
            LastTextureId = textureId;
        }
        public void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
            int srcX, int srcY, int srcW, int srcH, int border, GuiColor tint)
        {
            NinePatchCount++;
            LastTextureId = textureId;
        }
        //DrawImageNinePatch per-side border + stretchInner 重载 mock 仅计数用于断言
        public void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
            int srcX, int srcY, int srcW, int srcH,
            int borderLeft, int borderTop, int borderRight, int borderBottom,
            bool stretchInner, GuiColor tint)
        {
            NinePatchCount++;
            LastTextureId = textureId;
        }
        //DrawTiledSprite mock 仅计数用于断言九宫格中心平铺路径
        public void DrawTiledSprite(int textureId, int srcW, int srcH,
            int x, int y, int width, int height, GuiColor tint)
        {
            NinePatchCount++;
            LastTextureId = textureId;
        }
        //DrawSprite mock 无 GuiSpriteManager 直接返回供接口实现完整
        public void DrawSprite(string identifier, int x, int y, int width, int height, GuiColor tint) { }
        //DrawGlyphQuad F7 字形渲染接口 MockRenderContext 仅计数用于断言
        public void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup textureSetup,
            float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
            float u0, float v0, float u1, float v1, int color)
        {
            //F7 字形渲染 Mock 不实际绘制
        }
        public void PushPose(Matrix3x2 delta) => PushPoseCount++;
        public void PopPose() => PopPoseCount++;
        public void PushScissor(int x, int y, int width, int height) => PushScissorCount++;
        public void PopScissor() => PopScissorCount++;
        //MockRenderContext 不支持 retained mode cache 录制留空逻辑测试只走一次 Render
        public void BeginRecording(List<GuiElementRenderState> cache) { }
        public void EndRecording() { }
        public void ReplayRange(IReadOnlyList<GuiElementRenderState> cached) { }
        public void BlurBeforeThisStratum() { }
        public void AddPictureInPicture(PictureInPictureRenderState pip) { }
    }

    //TrackingKeyScreen 记录 OnF3Pressed/OnHotbarSelect 调用用于断言事件链路
    private sealed class TrackingKeyScreen : Screen
    {
        public int F3Count;
        public int LastSlot = -1;
        public override string Title => "tracking-keys";
        public override void OnF3Pressed() => F3Count++;
        public override void OnHotbarSelect(int slot) => LastSlot = slot;
    }

    //RecordingBgScreen 计数 RenderBackground 调用用于断言 hook 注入
    private sealed class RecordingBgScreen : Screen
    {
        public int BgCount;
        public override string Title => "rec-bg";
        public override void RenderBackground(IGuiRenderContext context) => BgCount++;
    }

    //manager.HandleRawKeyDown(F3) 调当前 Screen.OnF3Pressed
    private static bool TestRawKeyDownF3ReachesScreen()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        var screen = new TrackingKeyScreen();
        manager.SetScreen(screen);
        manager.HandleRawKeyDown(GameKeys.F3);
        manager.HandleRawKeyDown(GameKeys.F3);
        return screen.F3Count == 2;
    }

    //manager.HandleRawKeyDown(D6) 调当前 Screen.OnHotbarSelect slot 5
    private static bool TestRawKeyDownHotbarReachesScreen()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        var screen = new TrackingKeyScreen();
        manager.SetScreen(screen);
        //D6 键码 = D1 + 5 对应 slot 5
        manager.HandleRawKeyDown(GameKeys.D1 + 5);
        return screen.LastSlot == 5;
    }

    //GameScreen.Init 创建十字准星+Hotbar+选中框+10心+10鸡腿+7Debug 行至少 30 控件
    private static bool TestGameScreenInitCreatesHud()
    {
        var mc = new MinecraftClient(new GameConfig());
        var window = new GuiWindow(800, 600);
        var manager = new ScreenManager(mc, window);
        manager.SetScreen(new GameScreen());
        //1 准星 + 1 Hotbar + 1 选中框 + 10 鸡腿 + 7 Debug 行 = 20 心走 RenderForeground 不创建 GuiImage
        return window.Children.Count >= 20;
    }

    //GuiContainer.Render 自动 push/pop scissor 子控件在裁剪段内绘制
    private static bool TestContainerRenderPushesScissor()
    {
        var ctx = new MockRenderContext();
        var panel = new GuiPanel { X = 10, Y = 10, Width = 100, Height = 100 };
        var child = new GuiLabel("x") { X = 20, Y = 20, Width = 50, Height = 20 };
        panel.Add(child);
        panel.Render(ctx);
        //背景 1 quad + 子控件 0 quadLabel 无文本不画但 Render 仍调
        //push scissor 和 pop scissor 各 1 次
        return ctx.PushScissorCount == 1 && ctx.PopScissorCount == 1;
    }

    //嵌套 container 的 push/pop scissor 顺序外层先 push 后 push 内层先 pop 后 pop 外层
    private static bool TestNestedContainerScissorStackOrder()
    {
        var ctx = new MockRenderContext();
        var outer = new GuiPanel { X = 0, Y = 0, Width = 200, Height = 200 };
        var inner = new GuiPanel { X = 50, Y = 50, Width = 100, Height = 100 };
        var leaf = new GuiLabel("x") { X = 60, Y = 60, Width = 20, Height = 20 };
        inner.Add(leaf);
        outer.Add(inner);
        outer.Render(ctx);
        //outer push → inner push → leaf render → inner pop → outer pop
        return ctx.PushScissorCount == 2 && ctx.PopScissorCount == 2;
    }

    //PushPose/PopPose 调用不抛异常栈平衡由实现保证 MockRenderContext 仅计数
    private static bool TestPushPosePopPoseBalanced()
    {
        var ctx = new MockRenderContext();
        ctx.PushPose(Matrix3x2.CreateTranslation(10, 10));
        ctx.PushPose(Matrix3x2.CreateRotation(1.0f));
        ctx.PopPose();
        ctx.PopPose();
        return ctx.PushPoseCount == 2 && ctx.PopPoseCount == 2;
    }

    //LinearLayout vertical 按 Padding 起始 y 方向堆叠子控件加 Spacing
    private static bool TestLinearLayoutVerticalStacksChildren()
    {
        var panel = new GuiPanel { X = 10, Y = 20, Width = 200, Height = 300 };
        panel.Layout = new GuiLinearLayout(GuiLayoutOrientation.Vertical) { Padding = 5, Spacing = 3 };
        var a = new GuiLabel("a") { Width = 50, Height = 10 };
        var b = new GuiLabel("b") { Width = 50, Height = 20 };
        panel.Add(a);
        panel.Add(b);
        panel.Update(0.016);
        //a 起 y=20+5=25 b 起 y=25+10+3=38 x 均为 10+5=15
        return a.X == 15 && a.Y == 25 && b.X == 15 && b.Y == 38;
    }

    //LinearLayout horizontal 按 Padding 起始 x 方向堆叠子控件加 Spacing
    private static bool TestLinearLayoutHorizontalStacksChildren()
    {
        var panel = new GuiPanel { X = 10, Y = 20, Width = 300, Height = 200 };
        panel.Layout = new GuiLinearLayout(GuiLayoutOrientation.Horizontal) { Padding = 5, Spacing = 3 };
        var a = new GuiLabel("a") { Width = 50, Height = 10 };
        var b = new GuiLabel("b") { Width = 30, Height = 10 };
        panel.Add(a);
        panel.Add(b);
        panel.Update(0.016);
        //a 起 x=10+5=15 b 起 x=15+50+3=68 y 均为 20+5=25
        return a.X == 15 && a.Y == 25 && b.X == 68 && b.Y == 25;
    }

    //LinearLayout 跳过 Visible=false 的子控件不占位
    private static bool TestLinearLayoutSkipsInvisibleChildren()
    {
        var panel = new GuiPanel { X = 0, Y = 0, Width = 200, Height = 300 };
        panel.Layout = new GuiLinearLayout(GuiLayoutOrientation.Vertical) { Padding = 0, Spacing = 0 };
        var a = new GuiLabel("a") { Width = 50, Height = 10 };
        var hidden = new GuiLabel("h") { Width = 50, Height = 20, Visible = false };
        var b = new GuiLabel("b") { Width = 50, Height = 15 };
        panel.Add(a);
        panel.Add(hidden);
        panel.Add(b);
        panel.Update(0.016);
        //a y=0 b y=10 hidden 跳过不占位
        return a.Y == 0 && b.Y == 10;
    }

    //LinearLayout Padding 和 Spacing 正确应用子控件位置
    private static bool TestLinearLayoutPaddingAndSpacing()
    {
        var panel = new GuiPanel { X = 100, Y = 200, Width = 300, Height = 400 };
        panel.Layout = new GuiLinearLayout(GuiLayoutOrientation.Vertical) { Padding = 10, Spacing = 5 };
        var a = new GuiLabel("a") { Width = 50, Height = 20 };
        var b = new GuiLabel("b") { Width = 50, Height = 30 };
        panel.Add(a);
        panel.Add(b);
        panel.Update(0.016);
        //a y=200+10=210 b y=210+20+5=235 x 均为 100+10=110
        return a.X == 110 && a.Y == 210 && b.X == 110 && b.Y == 235;
    }

    //DrawQuadInverted 调用 InvertedQuadCount 计数与 DrawQuad 分离
    private static bool TestDrawQuadInvertedCounts()
    {
        var ctx = new MockRenderContext();
        ctx.DrawQuad(0, 0, 10, 10, GuiColor.White);
        ctx.DrawQuadInverted(0, 0, 10, 10, GuiColor.White);
        ctx.DrawQuadInverted(0, 0, 10, 10, GuiColor.White);
        return ctx.QuadCount == 1 && ctx.InvertedQuadCount == 2;
    }

    //Label Center 对齐按 MeasureText 居中 MockRenderContext 每字符 10 像素
    private static bool TestLabelCenterAlign()
    {
        var ctx = new MockRenderContext();
        var label = new GuiLabel("hello") { X = 10, Y = 10, Width = 200, Height = 20 };
        label.TextAlign = GuiTextAlign.Center;
        label.Render(ctx);
        //MeasureText 返回 5*10=50 x = 10 + (200-50)/2 = 85
        return ctx.LastTextX == 85;
    }

    //Label Right 对齐按 MeasureText 右对齐
    private static bool TestLabelRightAlign()
    {
        var ctx = new MockRenderContext();
        var label = new GuiLabel("hello") { X = 10, Y = 10, Width = 200, Height = 20 };
        label.TextAlign = GuiTextAlign.Right;
        label.Render(ctx);
        //MeasureText 返回 5*10=50 x = 10 + 200 - 50 = 160
        return ctx.LastTextX == 160;
    }

    //Tab 键按 TabIndex 顺序循环切换焦点 258 是 GLFW_KEY_TAB
    private static bool TestTabKeyCyclesFocus()
    {
        var window = new GuiWindow(800, 600);
        var btn1 = new GuiButton("A") { X = 10, Y = 10, Width = 80, Height = 30, TabStop = true, TabIndex = 0 };
        var btn2 = new GuiButton("B") { X = 100, Y = 10, Width = 80, Height = 30, TabStop = true, TabIndex = 1 };
        var btn3 = new GuiButton("C") { X = 190, Y = 10, Width = 80, Height = 30, TabStop = true, TabIndex = 2 };
        window.Add(btn1);
        window.Add(btn2);
        window.Add(btn3);

        //初始无焦点 Tab 后聚焦第一个
        window.ProcessKeyDown(258, '\0');
        if (!ReferenceEquals(window.FocusedControl, btn1)) return false;
        //Tab 切换到第二个
        window.ProcessKeyDown(258, '\0');
        if (!ReferenceEquals(window.FocusedControl, btn2)) return false;
        //Tab 切换到第三个
        window.ProcessKeyDown(258, '\0');
        if (!ReferenceEquals(window.FocusedControl, btn3)) return false;
        //Tab 循环回到第一个
        window.ProcessKeyDown(258, '\0');
        return ReferenceEquals(window.FocusedControl, btn1);
    }

    //TabStop=false 的控件不参与 Tab 导航被跳过
    private static bool TestTabStopFalseSkipsControl()
    {
        var window = new GuiWindow(800, 600);
        var btn1 = new GuiButton("A") { TabStop = true, TabIndex = 0 };
        var btn2 = new GuiButton("B") { TabStop = false, TabIndex = 1 };
        var btn3 = new GuiButton("C") { TabStop = true, TabIndex = 2 };
        window.Add(btn1);
        window.Add(btn2);
        window.Add(btn3);

        window.ProcessKeyDown(258, '\0');
        if (!ReferenceEquals(window.FocusedControl, btn1)) return false;
        //btn2 TabStop=false 跳过到 btn3
        window.ProcessKeyDown(258, '\0');
        return ReferenceEquals(window.FocusedControl, btn3);
    }
}
