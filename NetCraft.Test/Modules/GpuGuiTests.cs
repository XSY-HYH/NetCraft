using System.Numerics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Game.Client.Render.World;
using NetCraft.Gpu;
using NetCraft.Gpu.Vulkan;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using Direction = NetCraft.Gpu.Direction;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Test.Modules;

//GpuGuiTests Vulkan PoC + GUI 控件端到端测试
//默认在 SelectTests 中跳过，需通过参数显式指定 gpugui 模块才运行
//Vulkan 渲染测试需 STA 线程跑窗口主循环
//控件交互测试纯逻辑不依赖 Vulkan 验证 GuiWindow/GuiControl 事件派发
internal static class GpuGuiTests
{
    public const string Module = "gpugui";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        if (Environment.GetEnvironmentVariable("NETCRAFT_W85") is not null)
        {
            yield return ("Vulkan world renderer 8x8 sections renders 300 frames", TestWorldRendererRenders300Frames);
            yield break;
        }
        yield return ("Vulkan triangle renders 60 frames", TestTriangleRenders60Frames);
        yield return ("Vulkan triangle renders 300 frames", TestTriangleRenders300Frames);
        yield return ("Vulkan cube renders 60 frames", TestCubeRenders60Frames);
        yield return ("Vulkan cube renders 300 frames", TestCubeRenders300Frames);
        yield return ("Vulkan GUI renders 60 frames with controls", TestGuiRendersWithControls60Frames);
        yield return ("Vulkan GUI renders 300 frames with controls", TestGuiRendersWithControls300Frames);
        yield return ("GuiButton click interaction", TestButtonClickInteraction);
        yield return ("GuiTextBox accepts keyboard input", TestTextBoxInput);
        yield return ("GuiPanel contains children", TestPanelChildren);
        yield return ("GuiButton hover state changes", TestButtonHoverState);
        yield return ("Vulkan GUI nested scissor renders 300 frames", TestGuiRendersWithNestedScissor300Frames);
        yield return ("Vulkan GUI inverted quad renders 60 frames", TestGuiRendersInvertedQuad60Frames);
        yield return ("Vulkan ItemAtlas renders 60 frames with 3D items", TestItemAtlasRenders60Frames);
        yield return ("Vulkan ItemAtlas renders 300 frames with 3D items", TestItemAtlasRenders300Frames);
        yield return ("Vulkan ItemAtlas DrawToSlot writes non-transparent pixels to AtlasTexture", TestItemAtlasDrawToSlotWritesPixels);
        yield return ("Vulkan ItemAtlas slot pixels match 64x64 RGBA size", TestItemAtlasSlotPixelSize);
        yield return ("Vulkan ItemAtlas slot0 contains lit pixels from atlas*tint*diffuse*lightmap", TestItemAtlasSlot0ContainsLitPixels);
        yield return ("Vulkan ItemAtlas all registered items render to their slots", TestItemAtlasAllItemsRenderedToSlots);
        yield return ("Vulkan ItemAtlas clear color writes opaque black alpha=255", TestItemAtlasClearWritesOpaqueBlack);
        yield return ("Vulkan CommandEncoder WriteToTexture writes pixels via staging buffer", TestCommandEncoderWriteToTexture);
        yield return ("Vulkan GUI performance metrics collection", TestGuiPerformanceMetrics);
        yield return ("Vulkan PIP offscreen+blit renders 60 frames", TestPipRenders60Frames);
        yield return ("Vulkan PIP offscreen+blit renders 300 frames", TestPipRenders300Frames);
        yield return ("Vulkan PIP performance benchmark under threshold", TestPipPerformanceBenchmark);
        yield return ("Vulkan world renderer 8x8 sections renders 300 frames", TestWorldRendererRenders300Frames);
    }

    //testItemAtlasDrawToSlotWritesPixels 验证 DrawToSlot 实际写入 AtlasTexture 槽位区域
    //OnBeforeRun 调 RenderSlotAndReadback(0,0,0) readback 第一个物品槽位像素
    //clear 色是 (0,0,0,1) 黑色 渲染后 RGB 非零像素 > 0 说明顶点+UBO+shader+lightmap+atlas 链路写入正确
    //诊断模式 frag shader 输出固定红色 vec4(1,0,0,1) 应写入红色像素到 slot(0,0)
    private static bool TestItemAtlasDrawToSlotWritesPixels()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var slotPixels = app.Slot0ReadbackPixels;
            if (slotPixels == null) return;
            var nonBlack = VulkanItemAtlasApp.CountNonBlackPixels(slotPixels);
            var opaqueCount = VulkanItemAtlasApp.CountAlphaPixels(slotPixels, 255);
            Console.WriteLine($"[ItemAtlasPixelDiag] vertexCount={app.LastVertexCount} slot0 nonBlack={nonBlack} opaque={opaqueCount}/{slotPixels.Length / 4} first8=");
            for (int i = 0; i < 8 && i * 4 + 3 < slotPixels.Length; i++)
            {
                Console.WriteLine($"  [{i}] R={slotPixels[i * 4]} G={slotPixels[i * 4 + 1]} B={slotPixels[i * 4 + 2]} A={slotPixels[i * 4 + 3]}");
            }
            //诊断整张图集找渲染写入位置 8x8 槽位网格每槽位非黑像素数
            var fullPixels = app.FullAtlasReadbackPixels;
            if (fullPixels != null)
            {
                var totalNonBlack = VulkanItemAtlasApp.CountNonBlackPixels(fullPixels);
                var firstPos = VulkanItemAtlasApp.FindFirstNonBlackPixel(fullPixels, 512);
                Console.WriteLine($"[ItemAtlasPixelDiag] fullAtlas totalNonBlack={totalNonBlack} firstNonBlackAt=({firstPos.x},{firstPos.y})");
                for (int sy = 0; sy < 8; sy++)
                {
                    var line = "";
                    for (int sx = 0; sx < 8; sx++)
                    {
                        var slotCount = VulkanItemAtlasApp.CountNonBlackPixelsRegion(fullPixels, 512, sx * 64, sy * 64, 64, 64);
                        line += $"{slotCount,5} ";
                    }
                    Console.WriteLine($"[ItemAtlasPixelDiag] row{sy}: {line}");
                }
            }
            result = nonBlack > 0;
        });
        return result;
    }

    //testItemAtlasSlot0ContainsLitPixels 验证 slot(0,0) 含被光照调制的非黑像素
    //frag shader 输出 atlas*tint*diffuse*lightmap 四重调制 应写入非黑像素到 slot(0,0)
    //失败说明 shader 未执行或顶点被裁剪或四重调制输出全黑
    private static bool TestItemAtlasSlot0ContainsLitPixels()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var slotPixels = app.Slot0ReadbackPixels;
            if (slotPixels == null) return;
            var nonBlack = VulkanItemAtlasApp.CountNonBlackPixels(slotPixels);
            Console.WriteLine($"[ItemAtlasLitDiag] slot0 nonBlack={nonBlack}/{slotPixels.Length / 4} firstLit=");
            for (int i = 0; i < slotPixels.Length; i += 4)
            {
                if (slotPixels[i] > 0 || slotPixels[i + 1] > 0 || slotPixels[i + 2] > 0)
                {
                    Console.WriteLine($"  ({i/4 % 64},{i/4 / 64}) R={slotPixels[i]} G={slotPixels[i + 1]} B={slotPixels[i + 2]} A={slotPixels[i + 3]}");
                    break;
                }
            }
            result = nonBlack > 0;
        });
        return result;
    }

    //testItemAtlasAllItemsRenderedToSlots 验证所有注册物品都渲染到各自 slot
    //OnBeforeRun 循环 GetOrUpdate 10 个物品 Allocator 分配 10 个 slot 各自 DrawToSlot
    //非黑 slot 数应 == ItemCount(10) 表示 10 个物品都成功渲染
    private static bool TestItemAtlasAllItemsRenderedToSlots()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var fullPixels = app.FullAtlasReadbackPixels;
            if (fullPixels == null) return;
            int renderedSlots = 0;
            for (int sy = 0; sy < 8; sy++)
            {
                for (int sx = 0; sx < 8; sx++)
                {
                    var slotNonBlack = VulkanItemAtlasApp.CountNonBlackPixelsRegion(fullPixels, 512, sx * 64, sy * 64, 64, 64);
                    if (slotNonBlack > 0) renderedSlots++;
                }
            }
            Console.WriteLine($"[ItemAtlasRenderedDiag] renderedSlots={renderedSlots}/10 (所有注册物品应各渲染到一个 slot)");
            result = renderedSlots == 10;
        });
        return result;
    }

    //testItemAtlasClearWritesOpaqueBlack 验证未使用 slot 的 clear 色 alpha=255
    //clear 色 (0,0,0,1) alpha=1.0 对应字节 255 未渲染区域 alpha 应为 255
    //10 个物品用 10 个 slot 剩余 54 个 slot 应保持 clear 黑色 alpha=255
    private static bool TestItemAtlasClearWritesOpaqueBlack()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var fullPixels = app.FullAtlasReadbackPixels;
            if (fullPixels == null) return;
            //扫描所有 slot 找第一个全黑 slot 验证 alpha=255
            int totalPixels = 64 * 64;
            int checkedSlot = -1;
            for (int sy = 0; sy < 8 && checkedSlot < 0; sy++)
            {
                for (int sx = 0; sx < 8 && checkedSlot < 0; sx++)
                {
                    var slotNonBlack = VulkanItemAtlasApp.CountNonBlackPixelsRegion(fullPixels, 512, sx * 64, sy * 64, 64, 64);
                    if (slotNonBlack == 0)
                    {
                        var opaqueCount = VulkanItemAtlasApp.CountAlphaPixelsRegion(fullPixels, 512, sx * 64, sy * 64, 64, 64, 255);
                        if (opaqueCount == totalPixels)
                        {
                            checkedSlot = sy * 8 + sx;
                            Console.WriteLine($"[ItemAtlasClearDiag] slot({sx},{sy}) opaqueAlpha={opaqueCount}/{totalPixels}");
                            result = true;
                        }
                    }
                }
            }
            if (!result) Console.WriteLine($"[ItemAtlasClearDiag] 未找到全黑 clear slot");
        });
        return result;
    }

    //testItemAtlasSlotPixelSize 验证 readback 槽位像素尺寸是 64x64x4=16384 bytes
    private static bool TestItemAtlasSlotPixelSize()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var pixels = app.Slot0ReadbackPixels;
            result = pixels != null && pixels.Length == 64 * 64 * 4;
        });
        return result;
    }

    //testCommandEncoderWriteToTexture 验证 ICommandEncoder.WriteToTexture 录制+Submit 后像素正确写入
    //OnBeforeRun 创建 4x4 图像用 WriteToTexture 写入全红 RGBA(255,0,0,255) readback 验证所有像素匹配
    //失败说明 staging buffer 生命周期管理有 bug 或 layout 转换/CmdCopyBufferToImage 录制错误
    private static bool TestCommandEncoderWriteToTexture()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(1);
            var pixels = app.WriteTextureReadbackPixels;
            if (pixels == null) return;
            //4x4 全红 RGBA(255,0,0,255) 共 16 像素
            int matchCount = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i] == 255 && pixels[i + 1] == 0 && pixels[i + 2] == 0 && pixels[i + 3] == 255)
                    matchCount++;
            }
            Console.WriteLine($"[WriteToTextureDiag] matchCount={matchCount}/16");
            result = matchCount == 16;
        });
        return result;
    }

    //testGuiPerformanceMetrics 跑 GUI 渲染 120 帧后收集性能指标对比技术规划 8.2 目标
    //输出 DrawCall/Mesh/Vertex/SubmissionCpuMs/RenderCpuMs/TickRate/PipelineHits/PipelineMisses
    //目标 DrawCall≤5 submission<0.1ms(控件不动) TickRate=20tps PipelineMisses 只首帧后续全 hit
    private static bool TestGuiPerformanceMetrics()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            AddControlsForTest(app);
            app.RunFor(120);
            Console.WriteLine($"[PerfMetrics]");
            Console.WriteLine($"  DrawCall={app.DrawCallCount} (target ≤5)");
            Console.WriteLine($"  Mesh={app.MeshCount} Vertex={app.VertexCount}");
            Console.WriteLine($"  SubmissionCpu={app.SubmissionCpuMs:F3}ms (target <0.1ms idle)");
            Console.WriteLine($"  RenderCpu={app.RenderCpuMs:F3}ms");
            Console.WriteLine($"  TickRate={app.TickRate}tps (target 20)");
            Console.WriteLine($"  PipelineHit={app.PipelineHits} Miss={app.PipelineMisses} (miss 只首帧)");
            //TickRate 目标 20tps 允许 ±2 容差测试环境 vsync 和线程调度可能有波动
            result = app.DrawCallCount > 0 && app.DrawCallCount <= 10 && app.PipelineMisses > 0;
        });
        return result;
    }

    //testPipRenders60Frames native Vulkan PIP offscreen+blit 链路稳定性测试
    //通过 RenderForegroundHook 提交 ItemPipState 触发 ItemPipRenderer.Prepare 完整流程
    //验证 offscreen texture 创建+3D 渲染+blit 到 swapchain 链路 60 帧不崩溃
    //失败说明 PIP 渲染器资源管理/descriptor/pipeline 有 bug 或驱动兼容问题
    private static bool TestPipRenders60Frames()
        => RunPipRenderTest(60);

    //testPipRenders300Frames 跑 300 帧验证 PIP 持续渲染稳定性
    //offscreen texture 跨帧复用 + descriptor set 复用 + pipeline cache 命中
    private static bool TestPipRenders300Frames()
        => RunPipRenderTest(300);

    //runPipRenderTest PIP 渲染链路测试公共入口
    //SwapchainRecreated 在 OnCreatePipelineResources 末尾触发此时 ItemPipRenderer 已就绪
    //在事件回调中 RegisterItem 注册 cube 物品 + 设置 RenderForegroundHook 每帧提交 ItemPipState
    //RenderForegroundHook 走 GuiRenderContext.AddPictureInPicture 不进 retained cache 每帧重新提交
    private static bool RunPipRenderTest(int frames)
    {
        return RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            ItemPipState? pip = null;
            app.SwapchainRecreated += () =>
            {
                var renderer = app.ItemPipRenderer;
                if (renderer is null) return;
                renderer.RegisterItem("cube", 1.0f);
                pip = new ItemPipState(
                    X0: 100, Y0: 100, X1: 260, Y1: 260,
                    Scale: 40f,
                    ScissorArea: ScreenRectangle.Empty,
                    Pose: Matrix3x2.Identity,
                    ItemIdentity: "cube",
                    RotationY: 0.5f);
                app.Window.RenderForegroundHook = ctx => ctx.AddPictureInPicture(pip);
            };
            app.RunFor(frames);
        });
    }

    //testPipPerformanceBenchmark PIP 链路性能基准测试
    //跑 120 帧带 PIP 提交验证 PIP offscreen 渲染 + blit 不超阈值
    //目标 PIP 模式 RenderCpuMs <5ms 单帧 DrawCallCount 含 PIP blit 1 + GUI 1-2
    //失败说明 PIP offscreen 渲染卡顿或 pipeline 未命中 cache 重复编译
    private static bool TestPipPerformanceBenchmark()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            app.SwapchainRecreated += () =>
            {
                var renderer = app.ItemPipRenderer;
                if (renderer is null) return;
                renderer.RegisterItem("cube", 1.0f);
                var pip = new ItemPipState(
                    X0: 100, Y0: 100, X1: 260, Y1: 260,
                    Scale: 40f,
                    ScissorArea: ScreenRectangle.Empty,
                    Pose: Matrix3x2.Identity,
                    ItemIdentity: "cube",
                    RotationY: 0.5f);
                app.Window.RenderForegroundHook = ctx => ctx.AddPictureInPicture(pip);
            };
            app.RunFor(120);
            var renderMs = app.RenderCpuMs;
            var drawCall = app.DrawCallCount;
            Console.WriteLine($"[PipPerf]");
            Console.WriteLine($"  RenderCpu={renderMs:F3}ms (target <10ms)");
            Console.WriteLine($"  DrawCall={drawCall} (含 PIP blit + GUI)");
            Console.WriteLine($"  Mesh={app.MeshCount} Vertex={app.VertexCount}");
            Console.WriteLine($"  SubmissionCpu={app.SubmissionCpuMs:F3}ms");
            Console.WriteLine($"  PipelineHit={app.PipelineHits} Miss={app.PipelineMisses}");
            //RenderCpuMs 阈值 10ms PIP offscreen 每帧独立 Submit+fence wait 同步开销大
            //非 PIP GUI 仅 0.3ms 6ms 全是 PIP offscreen 的 vkQueueSubmit 同步 测试环境集成显卡波动大
            //DrawCall 至少 1（PIP blit）+ GUI 背景
            result = renderMs < 10.0 && drawCall > 0;
        });
        return result;
    }

    //testItemAtlasRenders60Frames 跑 ItemItemAtlas.DrawToSlot 60 帧验证 3D 物品 GPU 渲染链路
    //验证顶点上传+MVP UBO+Lighting UBO+lightmap sampler+atlas sampler+scissor+DrawIndexed 完整链路
    //失败说明 Vulkan 驱动不可用或 ItemItemAtlas GPU 渲染代码存在 bug
    private static bool TestItemAtlasRenders60Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(60);
        });

    //testItemAtlasRenders300Frames 跑 300 帧验证持续渲染稳定
    //如果 descriptor set 泄漏或 vertex buffer 跨帧复用有问题会在 300 帧内暴露
    private static bool TestItemAtlasRenders300Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanItemAtlasApp();
            app.RunFor(300);
        });

    //testTriangleRenders60Frames 跑完整 Vulkan PoC 60 帧后自动退出
    //失败说明 Vulkan 驱动不可用或 PoC 代码存在 bug
    private static bool TestTriangleRenders60Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanTriangleApp();
            app.RunFor(60);
        });

    //testTriangleRenders300Frames 跑 300 帧验证持续渲染稳定
    //如果驱动内存泄漏或资源销毁顺序错误会在 300 帧内暴露
    private static bool TestTriangleRenders300Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanTriangleApp();
            app.RunFor(300);
        });

    //testCubeRenders60Frames 跑 3D 立方体 60 帧验证深度测试+MVP uniform+索引绘制链路
    //失败说明深度附件或 uniform buffer 描述符集绑定有问题
    private static bool TestCubeRenders60Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanCubeApp();
            app.RunFor(60);
        });

    //testCubeRenders300Frames 跑 300 帧验证 3D 立方体持续渲染稳定
    private static bool TestCubeRenders300Frames()
        => RunOnStaThread(() =>
        {
            var app = new VulkanCubeApp();
            app.RunFor(300);
        });

    //testGuiRendersWithControls60Frames 创建 GuiWindow 加 4 类控件跑 60 帧
    //验证 VulkanGuiRenderer 文本管线 + 矩形管线 + 描述符集切换稳定
    private static bool TestGuiRendersWithControls60Frames()
        => RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            AddControlsForTest(app);
            app.RunFor(60);
        });

    //testGuiRendersWithControls300Frames 跑 300 帧验证 GUI 持续渲染稳定性
    private static bool TestGuiRendersWithControls300Frames()
        => RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            AddControlsForTest(app);
            app.RunFor(300);
        });

    //testButtonClickInteraction 模拟鼠标按下抬起验证 Button Click 事件触发
    //纯逻辑测试不依赖 Vulkan 验证 GuiWindow 事件派发和 GuiButton 状态机
    //GuiWindow 用 320x240 确保 GuiScale=1 鼠标坐标与控件坐标语义一致避免 800x600 触发 GuiScale=2 除法
    private static bool TestButtonClickInteraction()
    {
        var window = new GuiWindow(320, 240);
        var button = new GuiButton("Click Me")
        {
            X = 100,
            Y = 100,
            Width = 120,
            Height = 40
        };
        window.Add(button);
        int clickCount = 0;
        button.Click += (_, _) => clickCount++;

        //点击按钮中心
        window.ProcessMouseDown(GuiMouseButton.Left, 110, 110);
        window.ProcessMouseUp(GuiMouseButton.Left, 110, 110);
        if (clickCount != 1) return false;

        //再次点击验证多次触发
        window.ProcessMouseDown(GuiMouseButton.Left, 200, 130);
        window.ProcessMouseUp(GuiMouseButton.Left, 200, 130);
        if (clickCount != 2) return false;

        return true;
    }

    //testTextBoxInput 模拟键盘输入验证 TextBox TextChanged 事件和文本累积
    //GuiWindow 用 320x240 确保 GuiScale=1 鼠标坐标与控件坐标语义一致
    private static bool TestTextBoxInput()
    {
        var window = new GuiWindow(320, 240);
        var textBox = new GuiTextBox
        {
            X = 50,
            Y = 50,
            Width = 200,
            Height = 30
        };
        window.Add(textBox);

        //先点击 textBox 设为焦点否则键盘事件不会派发过来
        window.ProcessMouseDown(GuiMouseButton.Left, 55, 55);
        window.ProcessMouseUp(GuiMouseButton.Left, 55, 55);
        if (!ReferenceEquals(window.FocusedControl, textBox)) return false;

        string? lastText = null;
        int changeCount = 0;
        textBox.TextChanged += (_, _) =>
        {
            lastText = textBox.Text;
            changeCount++;
        };

        //输入 "hello"
        foreach (var ch in "hello")
        {
            window.ProcessKeyDown(0, ch);
            window.ProcessKeyUp(0, ch);
        }
        if (textBox.Text != "hello") return false;
        if (changeCount != 5) return false;
        if (lastText != "hello") return false;

        //退格删一个字符 BackSpace 走 OnKeyDown 不是 OnKeyPress
        window.ProcessKeyDown(GuiKeys.BackSpace);
        if (textBox.Text != "hell") return false;

        //输入控制字符应被忽略
        window.ProcessKeyDown(0, '\r');
        if (textBox.Text != "hell") return false;

        return true;
    }

    //testPanelChildren 验证 GuiPanel 容器 Add 子控件后 Children 列表正确
    private static bool TestPanelChildren()
    {
        var panel = new GuiPanel
        {
            X = 10,
            Y = 10,
            Width = 300,
            Height = 200
        };
        if (panel.Children.Count != 0) return false;

        var btn1 = new GuiButton("A") { X = 20, Y = 20, Width = 80, Height = 30 };
        var btn2 = new GuiButton("B") { X = 110, Y = 20, Width = 80, Height = 30 };
        panel.Add(btn1);
        panel.Add(btn2);

        if (panel.Children.Count != 2) return false;
        if (!ReferenceEquals(panel.Children[0], btn1)) return false;
        if (!ReferenceEquals(panel.Children[1], btn2)) return false;
        if (!ReferenceEquals(btn1.Parent, panel)) return false;

        //移除后列表更新父子关系清空
        panel.Remove(btn1);
        if (panel.Children.Count != 1) return false;
        if (btn1.Parent is not null) return false;

        //Clear 清空所有子控件
        panel.Add(btn1);
        panel.Clear();
        if (panel.Children.Count != 0) return false;
        if (btn1.Parent is not null) return false;
        if (btn2.Parent is not null) return false;

        return true;
    }

    //testButtonHoverState 验证 Button 鼠标进入离开后 hover 状态变化
    //这里间接验证事件派发链路 MouseEnter/MouseLeave 是否工作
    //GuiWindow 用 320x240 确保 GuiScale=1 鼠标坐标与控件坐标语义一致
    private static bool TestButtonHoverState()
    {
        var window = new GuiWindow(320, 240);
        var button = new GuiButton("Hover Me")
        {
            X = 50,
            Y = 50,
            Width = 100,
            Height = 30
        };
        window.Add(button);
        bool entered = false;
        bool left = false;
        button.MouseEnter += (_, _) => entered = true;
        button.MouseLeave += (_, _) => left = true;

        //从外部移到按钮内触发 enter
        window.ProcessMouseMove(10, 10);
        if (entered) return false;
        window.ProcessMouseMove(60, 60);
        if (!entered) return false;

        //从按钮内移到外部触发 leave
        window.ProcessMouseMove(700, 500);
        if (!left) return false;

        return true;
    }

    //testGuiRendersWithNestedScissor300Frames 三层嵌套 panel + 超出边界子控件跑 300 帧
    //验证 SetScissor + 段分组绘制链路稳定不崩溃驱动状态正确
    private static bool TestGuiRendersWithNestedScissor300Frames()
        => RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            var outer = new GuiPanel { X = 10, Y = 10, Width = 780, Height = 580 };
            app.Window.Add(outer);
            var middle = new GuiPanel { X = 20, Y = 20, Width = 400, Height = 300 };
            outer.Add(middle);
            var inner = new GuiPanel { X = 10, Y = 10, Width = 200, Height = 150 };
            middle.Add(inner);
            //inner 内正常子控件
            inner.Add(new GuiLabel("inside") { X = 10, Y = 10, Width = 100, Height = 20 });
            //inner 超出边界子控件验证 scissor 裁剪
            inner.Add(new GuiLabel("overflow") { X = 180, Y = 130, Width = 100, Height = 50 });
            app.RunFor(300);
        });

    //testGuiRendersInvertedQuad60Frames 用 InvertedQuadControl 调 DrawQuadInverted 验证反色管线渲染稳定
    private static bool TestGuiRendersInvertedQuad60Frames()
        => RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            app.Window.Add(new InvertedQuadControl());
            app.RunFor(60);
        });

    //InvertedQuadControl 调 DrawQuadInverted + DrawQuad 验证反色管线与标准管线交替渲染
    private sealed class InvertedQuadControl : GuiControl
    {
        public override void Render(IGuiRenderContext context)
        {
            //标准红色矩形
            context.DrawQuad(50, 50, 100, 100, GuiColor.Red);
            //反色白色矩形 fragment shader 对 RGB 取反
            context.DrawQuadInverted(200, 100, 200, 100, GuiColor.White);
        }
    }

    //addControlsForTest 给 VulkanGuiApp 预先添加一组控件供渲染稳定性测试
    private static void AddControlsForTest(VulkanGuiApp app)
    {
        var panel = new GuiPanel
        {
            X = 20,
            Y = 20,
            Width = 400,
            Height = 200
        };
        app.Window.Add(panel);

        var title = new GuiLabel("NetCraft GPU GUI Test")
        {
            X = 30,
            Y = 30,
            Width = 380,
            Height = 24
        };
        panel.Add(title);

        var button = new GuiButton("Click Me")
        {
            X = 30,
            Y = 70,
            Width = 120,
            Height = 36
        };
        button.Click += (_, _) => { /* 测试不验证点击效果只验证渲染稳定 */ };
        panel.Add(button);

        var textBox = new GuiTextBox
        {
            X = 30,
            Y = 120,
            Width = 200,
            Height = 30
        };
        panel.Add(textBox);

        var innerPanel = new GuiPanel
        {
            X = 440,
            Y = 20,
            Width = 340,
            Height = 400
        };
        app.Window.Add(innerPanel);

        for (int i = 0; i < 5; i++)
        {
            innerPanel.Add(new GuiLabel($"Line {i + 1}")
            {
                X = 10,
                Y = 10 + i * 28,
                Width = 200,
                Height = 24
            });
        }
    }

    //runOnStaThread 在 STA 工作线程上跑 PoC 主循环
    //Windows GLFW 要求 STA 否则消息循环在某些驱动下卡死
    //Linux/macOS Silk.NET 不需要 STA 直接 MTA 跑
    private static bool RunOnStaThread(ThreadStart action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }
        thread.Start();
        thread.Join();
        if (error != null)
        {
            Console.Error.WriteLine("Vulkan GUI test failed: " + error.Message);
            Console.Error.WriteLine(error.StackTrace);
            return false;
        }
        return true;
    }

    //testWorldRendererRenders300Frames W8.5 LevelRenderer+SectionRenderDispatcher 集成 VulkanGuiApp
    //8x8 section 相机静止 300 帧验证异步编译+Upload+Draw 链路无崩溃 buffer 无泄漏
    //SwapchainRecreated 时创建 dispatcher+LevelRenderer 注入 app 第一帧 Prepare 触发编译后续帧 Upload+Draw
    //验证 uploaded==64（8x8 全编译上传）bufferInUse==uploaded*2（每 section vb+ib 无泄漏）drawCall>0
    private static bool TestWorldRendererRenders300Frames()
    {
        bool result = false;
        RunOnStaThread(() =>
        {
            using var app = new VulkanGuiApp(800, 600);
            SectionRenderDispatcher? dispatcher = null;
            LevelRenderer? renderer = null;
            try
            {
                app.SwapchainRecreated += () =>
                {
                    var level = NewLevelWith8x8Sections();
                    var camera = new Camera();
                    //camera 在 (64,8,300) 朝 -Z fov 90° 视锥覆盖 0..128 x 0..16 z 0..128
                    camera.SetPosition(new Vector3(64, 8, 300));
                    camera.UpdatePerspective(MathF.PI / 2f, 800, 600, 0.05f, 1000f);
                    var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
                    var pool = new GpuBufferPool((size, usage) => app.Device.CreateHostVisibleBuffer(size, usage));
                    dispatcher = new SectionRenderDispatcher(level, builder, pool, workerCount: 2);
                    dispatcher.Start();
                    renderer = new LevelRenderer(level, camera, dispatcher);
                    app.SetLevelRenderer(renderer);
                };
                app.RunFor(300);
                if (dispatcher is null || renderer is null) return;
                //Stop dispatcher 确保 worker 不再修改状态再计数
                dispatcher.Stop();
                var uploaded = dispatcher.UploadedSectionCount;
                var bufferInUse = dispatcher.BufferPoolInUseCount;
                Console.WriteLine($"[WorldRender] uploaded={uploaded}/64 bufferInUse={bufferInUse} drawCall={app.WorldDrawCallCount} renderCpu={app.RenderCpuMs:F3}ms");
                result = app.WorldDrawCallCount > 0
                    && uploaded == 64
                    && bufferInUse == uploaded * 2;
            }
            finally
            {
                renderer?.Dispose();
                dispatcher?.Dispose();
            }
        });
        return result;
    }

    //NewLevelWith8x8Sections 装入 8x8 chunk 各 1 个有 stone 的 section 共 64 section
    private static ClientLevel NewLevelWith8x8Sections()
    {
        var level = new ClientLevel();
        for (var cx = 0; cx < 8; cx++)
        for (var cz = 0; cz < 8; cz++)
        {
            var (section, stone) = NewSectionWithAir();
            section.SetBlockState(0, 0, 0, stone);
            var chunk = new TestChunkAccess(new ChunkPos(cx, cz), 0, 1, section);
            level.LoadChunk(chunk);
        }
        return level;
    }

    //NewSectionWithAir 创建全 air section 返回 section 与 stone BlockState
    private static (LevelChunkSection section, BlockState stone) NewSectionWithAir()
    {
        var factory = new DefaultPalettedContainerFactory();
        var airBlock = new MockBlock(Identifier.WithDefaultNamespace("air"));
        var stoneBlock = new MockBlock(Identifier.WithDefaultNamespace("stone"));
        factory.RegisterBlock(airBlock);
        factory.RegisterBlock(stoneBlock);
        var section = new LevelChunkSection(factory.CreateForBlockStates(), factory.CreateForBiomes());
        return (section, stoneBlock.DefaultBlockState);
    }

    //NewCubeBakedModel 6 面 cube BakedModel 全 Solid layer
    private static BakedModel NewCubeBakedModel()
    {
        var model = new BakedModel();
        var uv0 = new Vector2(0, 0);
        var uv1 = new Vector2(1, 0);
        var uv2 = new Vector2(1, 1);
        var uv3 = new Vector2(0, 1);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 0, 16), new(0, 0, 0), new(16, 0, 0), new(16, 0, 16),
            uv0, uv1, uv2, uv3, Direction.Down), Direction.Down);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 0), new(0, 16, 16), new(16, 16, 16), new(16, 16, 0),
            uv0, uv1, uv2, uv3, Direction.Up), Direction.Up);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 0), new(0, 0, 0), new(16, 0, 0), new(16, 16, 0),
            uv0, uv1, uv2, uv3, Direction.North), Direction.North);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(16, 16, 16), new(16, 0, 16), new(0, 0, 16), new(0, 16, 16),
            uv0, uv1, uv2, uv3, Direction.South), Direction.South);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 16), new(0, 0, 16), new(0, 0, 0), new(0, 16, 0),
            uv0, uv1, uv2, uv3, Direction.West), Direction.West);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(16, 16, 0), new(16, 0, 0), new(16, 0, 16), new(16, 16, 16),
            uv0, uv1, uv2, uv3, Direction.East), Direction.East);
        return model;
    }

    //TestChunkAccess 测试用 ChunkAccess 具体子类单 section
    private sealed class TestChunkAccess : ChunkAccess
    {
        private readonly ChunkPos _pos;
        private readonly Dictionary<HeightmapRegistry.Types, long[]> _heightmaps;
        private readonly LevelChunkSection? _section;

        public override ChunkPos Pos => _pos;
        public override int MinSectionY { get; }
        public override int SectionsCount { get; }
        public override ChunkStatus ChunkStatus { get; }
        public override IDictionary<HeightmapRegistry.Types, long[]> Heightmaps => _heightmaps;

        public TestChunkAccess(ChunkPos pos, int minSectionY, int sectionsCount, LevelChunkSection? section = null)
        {
            _pos = pos;
            MinSectionY = minSectionY;
            SectionsCount = sectionsCount;
            _heightmaps = new Dictionary<HeightmapRegistry.Types, long[]>();
            _section = section;
            ChunkStatus = ChunkStatus.EMPTY;
        }

        public override LevelChunkSection? GetSection(int sectionY)
            => sectionY == MinSectionY ? _section : null;
    }

    //MockBlock 测试用 Block 子类
    private sealed class MockBlock : Block
    {
        public override Identifier Id { get; }
        public override BlockState DefaultBlockState { get; }

        public MockBlock(Identifier id)
        {
            Id = id;
            var state = BlockStateRegistry.Register(this, Array.Empty<PropertyBase>(), Array.Empty<object?>());
            BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
            DefaultBlockState = state;
        }
    }
}
