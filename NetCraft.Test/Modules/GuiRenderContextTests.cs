using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;
using NetCraft.Gpu.Sprite;

namespace NetCraft.Test.Modules;

//GuiRenderContextTests 阶段 5b submission 层单元测试
//覆盖 DrawQuad/DrawQuadInverted/DrawImage → RenderState 转换 PushPose/PopPose 坐标变换 PushScissor/PopScissor 裁剪栈
//guiScale 坐标转换验证纯 CPU 逻辑不依赖 Vulkan
internal static class GuiRenderContextTests
{
    public const string Module = "guirenderctx";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("DrawQuad submits ColoredRectangleRenderState to GUI pipeline", TestDrawQuadSubmitsColoredState);
        yield return ("DrawQuadInverted uses GUI_INVERT pipeline", TestDrawQuadInvertedPipeline);
        yield return ("DrawImage submits BlitRenderState to GUI_TEXTURED pipeline", TestDrawImageSubmitsBlitState);
        yield return ("DrawImage UV calculated from src pixels and texture size", TestDrawImageUV);
        yield return ("DrawImageNinePatch submits 9 BlitRenderState for full grid", TestDrawImageNinePatchSubmits9BlitStates);
        yield return ("DrawImageNinePatch falls back to single DrawImage on small target", TestDrawImageNinePatchFallbackOnSmallTarget);
        yield return ("DrawImageNinePatch uses full texture size when srcW<=0", TestDrawImageNinePatchUsesFullTextureSize);
        yield return ("DrawImageNinePatch per-side border stretchInner=true 提交 9 BlitRenderState", TestDrawImageNinePatchPerSideBorderStretchInner);
        yield return ("DrawImageNinePatch stretchInner=false 中心提交 TiledBlitRenderState", TestDrawImageNinePatchStretchInnerFalseTiles);
        yield return ("DrawImageNinePatch border 钳制到 width/2 避免负区域", TestDrawImageNinePatchBorderClamps);
        yield return ("DrawImageNinePatch 源尺寸不足 border 退化为 DrawImage", TestDrawImageNinePatchSourceTooSmallFallsBack);
        yield return ("DrawTiledSprite 提交 TiledBlitRenderState 到 GUI_TEXTURED pipeline", TestDrawTiledSpriteSubmitsTiledBlitState);
        yield return ("DrawSprite Stretch 路径提交单 BlitRenderState", TestDrawSpriteStretch);
        yield return ("DrawSprite NineSlice 路径提交 9 块 RenderState", TestDrawSpriteNineSlice);
        yield return ("PushPose scales translation by guiScale", TestPushPoseScalesTranslation);
        yield return ("PopPose restores parent pose", TestPopPoseRestores);
        yield return ("PushScissor intersects with parent actual", TestPushScissorIntersectsParent);
        yield return ("PopScissor restores parent scissor", TestPopScissorRestores);
        yield return ("guiScale=2 doubles coordinates and sizes", TestGuiScale2DoublesCoordinates);
        yield return ("ToIntColor converts GuiColor to ARGB", TestToIntColorConversion);
        yield return ("retained mode cache replay repeats same RenderState count", TestRetainedModeCacheReplayRepeatsRenderState);
        yield return ("retained mode cache skips Render on clean frame", TestRetainedModeCacheSkipsRenderOnCleanFrame);
        yield return ("retained mode cache re-records after MarkDirty", TestRetainedModeCacheReRecordsAfterMarkDirty);
        yield return ("retained mode child cache isolates dirty subtree", TestRetainedModeChildCacheIsolatesDirtySubtree);
        yield return ("retained mode parent change invalidates child cache", TestRetainedModeParentChangeInvalidatesChildCache);
        yield return ("retained mode nested container dirty propagation", TestRetainedModeNestedContainerDirtyPropagation);
    }

    //TestRetainedModeCacheReplayRepeatsRenderState 第一帧录制后第二帧 !dirty 走 replay
    //验证 replay 产生相同数量 RenderState 证明 cache 机制工作
    private static bool TestRetainedModeCacheReplayRepeatsRenderState()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        window.Add(new CountingControl());
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        int firstCount = CollectElements(state).Count;
        if (firstCount == 0) return false;
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        int secondCount = CollectElements(state).Count;
        return secondCount == firstCount;
    }

    //TestRetainedModeCacheSkipsRenderOnCleanFrame 第一帧调控件 Render 第二帧 !dirty 走 replay
    //验证控件 Render 不被重复调用证明 submission 阶段跳过未变化子树
    private static bool TestRetainedModeCacheSkipsRenderOnCleanFrame()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        var ctrl = new CountingControl();
        window.Add(ctrl);
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        if (ctrl.RenderCount != 1) return false;
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        return ctrl.RenderCount == 1;
    }

    //TestRetainedModeCacheReRecordsAfterMarkDirty 控件属性变化 MarkDirty 后整窗口重 Render
    //验证 dirty 标记正确传播 cache 失效重新录制
    private static bool TestRetainedModeCacheReRecordsAfterMarkDirty()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        var ctrl = new CountingControl();
        window.Add(ctrl);
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        if (ctrl.RenderCount != 1) return false;
        ctrl.X = 100; // setter MarkDirty 向上传播到 window
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        return ctrl.RenderCount == 2;
    }

    //CountingControl 计数 Render 调用供 retained mode cache 测试断言
    private sealed class CountingControl : GuiControl
    {
        public int RenderCount;
        public override void Render(IGuiRenderContext context)
        {
            RenderCount++;
            context.DrawQuad(X, Y, 10, 10, GuiColor.White);
        }
    }

    //TestRetainedModeChildCacheIsolatesDirtySubtree window dirty 时只有 dirty 子控件重 Render
    //child1.MarkDirty 向上传播到 window 整窗口 dirty 但 child2 !dirty 走 ReplayRange 不重 Render
    private static bool TestRetainedModeChildCacheIsolatesDirtySubtree()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        var child1 = new CountingControl();
        var child2 = new CountingControl();
        window.Add(child1);
        window.Add(child2);

        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        if (child1.RenderCount != 1 || child2.RenderCount != 1) return false;

        //child1 属性变化 MarkDirty 向上传播 window dirty child2 保持 !dirty
        child1.X = 100;
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        //child1 dirty 重 Render child2 !dirty ReplayRange 跳过 Render
        return child1.RenderCount == 2 && child2.RenderCount == 1;
    }

    //TestRetainedModeParentChangeInvalidatesChildCache 父容器属性变化时子控件 cache 失效重 Render
    //parent.X 变化 MarkDirty 向上传播到 window + 向下传播到 child child cache 失效
    private static bool TestRetainedModeParentChangeInvalidatesChildCache()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        var parent = new GuiPanel();
        var child = new CountingControl();
        parent.Add(child);
        window.Add(parent);

        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        if (child.RenderCount != 1) return false;

        //parent 属性变化 MarkDirty 向下传播 child cache 失效
        parent.X = 50;
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        return child.RenderCount == 2;
    }

    //TestRetainedModeNestedContainerDirtyPropagation 多层嵌套容器 dirty 向下传播到 leaf
    //container1.X 变化 MarkDirty 向下传播 container2 和 leaf child 都 dirty 重 Render
    private static bool TestRetainedModeNestedContainerDirtyPropagation()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        var window = new GuiWindow(800, 600);
        var container1 = new GuiPanel();
        var container2 = new GuiPanel();
        var child = new CountingControl();
        container2.Add(child);
        container1.Add(container2);
        window.Add(container1);

        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        if (child.RenderCount != 1) return false;

        //container1 属性变化 MarkDirty 向下传播到 container2 和 child
        container1.X = 50;
        state.Reset(); ctx.BeginFrame();
        window.Render(ctx);
        return child.RenderCount == 2;
    }

    //TestDrawQuadSubmitsColoredState 验证 DrawQuad 提交 ColoredRectangleRenderState 到 GUI pipeline
    private static bool TestDrawQuadSubmitsColoredState()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        ctx.DrawQuad(10, 20, 100, 50, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        return crs.Pipeline == RenderPipelines.GUI
            && crs.TextureSetup == TextureSetup.NoTexture
            && crs.X0 == 10 && crs.Y0 == 20 && crs.X1 == 110 && crs.Y1 == 70
            && crs.Col1 == crs.Col2
            && crs.Col1 == unchecked((int)0xFFFFFFFF);
    }

    //TestDrawQuadInvertedPipeline 验证 DrawQuadInverted 用 GUI_INVERT pipeline
    private static bool TestDrawQuadInvertedPipeline()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        ctx.DrawQuadInverted(0, 0, 10, 10, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        return crs.Pipeline == RenderPipelines.GUI_INVERT;
    }

    //TestDrawImageSubmitsBlitState 验证 DrawImage 提交 BlitRenderState 到 GUI_TEXTURED pipeline
    private static bool TestDrawImageSubmitsBlitState()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(256, 256);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImage(1, 0, 0, 100, 100, 0, 0, 64, 64, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not BlitRenderState blit) return false;
        return blit.Pipeline == RenderPipelines.GUI_TEXTURED
            && blit.TextureSetup == texture
            && blit.X0 == 0 && blit.Y0 == 0 && blit.X1 == 100 && blit.Y1 == 100;
    }

    //TestDrawImageUV 验证 UV 由 src 像素与 GpuImage 尺寸计算
    private static bool TestDrawImageUV()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(256, 256);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImage(1, 0, 0, 100, 100, 0, 0, 64, 64, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not BlitRenderState blit) return false;
        //u0=0/256=0 u1=64/256=0.25 v0=0 v1=0.25
        return Math.Abs(blit.U0 - 0f) < 0.001f
            && Math.Abs(blit.U1 - 0.25f) < 0.001f
            && Math.Abs(blit.V0 - 0f) < 0.001f
            && Math.Abs(blit.V1 - 0.25f) < 0.001f;
    }

    //TestDrawImageNinePatchSubmits9BlitStates 验证九宫格产生 9 个 BlitRenderState
    //模拟原版 button.png 200x20 border=3 目标 240x25 不退化应得 9 元素全 GUI_TEXTURED pipeline 同 texture
    private static bool TestDrawImageNinePatchSubmits9BlitStates()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 240, 25, 0, 0, 200, 20, 3, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 9) return false;
        foreach (var e in elements)
        {
            if (e is not BlitRenderState b) return false;
            if (b.Pipeline != RenderPipelines.GUI_TEXTURED) return false;
            if (b.TextureSetup != texture) return false;
        }
        return true;
    }

    //TestDrawImageNinePatchFallbackOnSmallTarget 验证目标尺寸不足 2*border 退化为直接 DrawImage
    //目标 5x5 < 2*3=6 应只产生 1 个 BlitRenderState
    private static bool TestDrawImageNinePatchFallbackOnSmallTarget()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 5, 5, 0, 0, 200, 20, 3, GuiColor.White);

        var elements = CollectElements(state);
        return elements.Count == 1 && elements[0] is BlitRenderState;
    }

    //TestDrawImageNinePatchUsesFullTextureSize 验证 srcW<=0/srcH<=0 用纹理全尺寸
    //模拟 GuiButton 调用时传 0 让 context 自动查纹理尺寸应得 9 个 BlitRenderState
    private static bool TestDrawImageNinePatchUsesFullTextureSize()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 240, 25, 0, 0, 0, 0, 3, GuiColor.White);

        var elements = CollectElements(state);
        return elements.Count == 9;
    }

    //TestDrawImageNinePatchPerSideBorderStretchInner per-side border stretchInner=true
    //模拟 slider_handle.png 8x20 border={2,2,2,3} 目标 16x25 应得 9 个 BlitRenderState
    //验证四边独立 border 切片正确中心拉伸走 DrawImage 路径
    private static bool TestDrawImageNinePatchPerSideBorderStretchInner()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(8, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 16, 25, 0, 0, 8, 20,
            2, 2, 2, 3, true, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 9) return false;
        foreach (var e in elements)
        {
            if (e is not BlitRenderState b) return false;
            if (b.Pipeline != RenderPipelines.GUI_TEXTURED) return false;
            if (b.TextureSetup != texture) return false;
        }
        return true;
    }

    //TestDrawImageNinePatchStretchInnerFalseTiles stretchInner=false 中心走 TiledBlitRenderState
    //200x20 border=3 目标 240x25 stretchInner=false 应得 8 BlitRenderState + 1 TiledBlitRenderState
    private static bool TestDrawImageNinePatchStretchInnerFalseTiles()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 240, 25, 0, 0, 200, 20,
            3, 3, 3, 3, false, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 9) return false;
        //4 角 + 4 边 = 8 个 BlitRenderState 中心 1 个 TiledBlitRenderState
        int blitCount = elements.Count(e => e is BlitRenderState);
        int tiledCount = elements.Count(e => e is TiledBlitRenderState);
        return blitCount == 8 && tiledCount == 1;
    }

    //TestDrawImageNinePatchBorderClamps border 钳制到 width/2 避免负区域
    //目标 5x5 border=3 钳制到 5/2=2 应得 9 块而非退化（对标原版 Math.min）
    private static bool TestDrawImageNinePatchBorderClamps()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        //目标 5x5 border=3 钳制到 2 应得 9 块
        ctx.DrawImageNinePatch(1, 0, 0, 5, 5, 0, 0, 200, 20,
            3, 3, 3, 3, true, GuiColor.White);

        var elements = CollectElements(state);
        return elements.Count == 9;
    }

    //TestDrawImageNinePatchSourceTooSmallFallsBack 源尺寸不足 border 之和退化为 DrawImage
    //源 5x5 border=3 钳制后 srcW(5) < bl+br=6 退化应得 1 个 BlitRenderState
    private static bool TestDrawImageNinePatchSourceTooSmallFallsBack()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(5, 5);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawImageNinePatch(1, 0, 0, 100, 100, 0, 0, 5, 5,
            3, 3, 3, 3, true, GuiColor.White);

        var elements = CollectElements(state);
        return elements.Count == 1 && elements[0] is BlitRenderState;
    }

    //TestDrawTiledSpriteSubmitsTiledBlitState DrawTiledSprite 提交 TiledBlitRenderState
    //验证 tile 平铺路径走对 pipeline 和 RenderState 类型
    private static bool TestDrawTiledSpriteSubmitsTiledBlitState()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(16, 16);
        var texture = TextureSetup.SingleTexture(img, null!);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null);
        ctx.BeginFrame();
        ctx.DrawTiledSprite(1, 16, 16, 0, 0, 100, 100, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not TiledBlitRenderState t) return false;
        return t.Pipeline == RenderPipelines.GUI_TEXTURED
            && t.TextureSetup == texture
            && t.TileWidth == 16 && t.TileHeight == 16;
    }

    //TestDrawSpriteStretch DrawSprite Stretch scaling 路径提交单 BlitRenderState
    //mock GuiSpriteManager 返回 StretchScaling sprite 验证分派到 DrawImage
    private static bool TestDrawSpriteStretch()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(16, 16);
        var texture = TextureSetup.SingleTexture(img, null!);
        var sprite = new GuiSprite(1, texture, 16, 16, new StretchScaling());
        var mgr = new StubSpriteManager(sprite);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null, mgr);
        ctx.BeginFrame();
        ctx.DrawSprite("minecraft:test/stretch", 0, 0, 100, 100, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        return elements[0] is BlitRenderState b
            && b.Pipeline == RenderPipelines.GUI_TEXTURED
            && b.TextureSetup == texture;
    }

    //TestDrawSpriteNineSlice DrawSprite NineSlice scaling 路径提交 9 块 RenderState
    //mock GuiSpriteManager 返回 NineSliceScaling sprite border=3 stretchInner=true
    //验证分派到 DrawImageNinePatch per-side border API 产生 9 个 BlitRenderState
    private static bool TestDrawSpriteNineSlice()
    {
        var state = new GuiRenderState();
        var img = new TestGpuImage(200, 20);
        var texture = TextureSetup.SingleTexture(img, null!);
        var sprite = new GuiSprite(1, texture, 200, 20,
            new NineSliceScaling(200, 20, NineSliceBorder.Uniform(3), true));
        var mgr = new StubSpriteManager(sprite);
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, id => id == 1 ? texture : null, mgr);
        ctx.BeginFrame();
        ctx.DrawSprite("minecraft:test/nine_slice", 0, 0, 240, 25, GuiColor.White);

        var elements = CollectElements(state);
        return elements.Count == 9;
    }

    //StubSpriteManager 测试用派生类覆盖 LoadSprite 返回固定 GuiSprite 避免 GpuDevice 依赖
    private sealed class StubSpriteManager : GuiSpriteManager
    {
        private readonly GuiSprite? _sprite;
        public StubSpriteManager(GuiSprite? sprite) : base(string.Empty, null!) => _sprite = sprite;
        protected override GuiSprite? LoadSprite(string identifier) => _sprite;
    }

    //TestPushPoseScalesTranslation 验证 PushPose 的 translation *guiScale
    private static bool TestPushPoseScalesTranslation()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 2, null, null, null, _ => null);
        ctx.BeginFrame();
        //delta translation (10, 20) guiScale=2 → actual (20, 40)
        ctx.PushPose(Matrix3x2.CreateTranslation(10, 20));
        ctx.DrawQuad(0, 0, 1, 1, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        var pose = crs.Pose;
        //pose 是 actual translation (20, 40)
        return Math.Abs(pose.M31 - 20f) < 0.001f && Math.Abs(pose.M32 - 40f) < 0.001f;
    }

    //TestPopPoseRestores 验证 PopPose 恢复父 pose
    private static bool TestPopPoseRestores()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        ctx.PushPose(Matrix3x2.CreateTranslation(10, 20));
        ctx.PopPose();
        ctx.DrawQuad(0, 0, 1, 1, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        var pose = crs.Pose;
        //pop 后 pose 恢复 Identity
        return Math.Abs(pose.M31 - 0f) < 0.001f && Math.Abs(pose.M32 - 0f) < 0.001f;
    }

    //TestPushScissorIntersectsParent 验证 PushScissor 与父 scissor 求交
    private static bool TestPushScissorIntersectsParent()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        //父 scissor (0,0,800,600) PushScissor(10,10,100,100) → 交集 (10,10,100,100)
        ctx.PushScissor(10, 10, 100, 100);
        ctx.DrawQuad(0, 0, 1, 1, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        var scissor = elements[0].ScissorArea;
        return scissor.X == 10 && scissor.Y == 10 && scissor.Width == 100 && scissor.Height == 100;
    }

    //TestPopScissorRestores 验证 PopScissor 恢复父 scissor
    private static bool TestPopScissorRestores()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        ctx.PushScissor(10, 10, 100, 100);
        ctx.PopScissor();
        ctx.DrawQuad(0, 0, 1, 1, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        var scissor = elements[0].ScissorArea;
        //pop 后恢复全屏
        return scissor.X == 0 && scissor.Y == 0 && scissor.Width == 800 && scissor.Height == 600;
    }

    //TestGuiScale2DoublesCoordinates 验证 guiScale=2 时坐标和尺寸 *2
    private static bool TestGuiScale2DoublesCoordinates()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 2, null, null, null, _ => null);
        ctx.BeginFrame();
        //scaled (10,20,100,50) guiScale=2 → actual (20,40,200,100)
        ctx.DrawQuad(10, 20, 100, 50, GuiColor.White);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        return crs.X0 == 20 && crs.Y0 == 40 && crs.X1 == 220 && crs.Y1 == 140;
    }

    //TestToIntColorConversion 验证 GuiColor 浮点 0-1 转 ARGB int
    private static bool TestToIntColorConversion()
    {
        var state = new GuiRenderState();
        var ctx = new GuiRenderContext(state, 800, 600, 1, null, null, null, _ => null);
        ctx.BeginFrame();
        //红色 (1,0,0,1) → ARGB 0xFFFF0000
        ctx.DrawQuad(0, 0, 1, 1, GuiColor.Red);

        var elements = CollectElements(state);
        if (elements.Count != 1) return false;
        if (elements[0] is not ColoredRectangleRenderState crs) return false;
        return crs.Col1 == unchecked((int)0xFFFF0000);
    }

    //CollectElements 遍历 GuiRenderState 收集所有元素供断言
    private static List<GuiElementRenderState> CollectElements(GuiRenderState state)
    {
        var list = new List<GuiElementRenderState>();
        state.ForEachElement(e => list.Add(e), TraverseRange.All);
        return list;
    }

    //TestGpuImage 测试用 GpuImage 桩不依赖 Vulkan 提供 Width/Height 供 UV 计算
    private sealed class TestGpuImage : GpuImage
    {
        public TestGpuImage(int width, int height)
            : base(new GpuImageDescription
            {
                Width = width,
                Height = height,
                Format = GpuImageFormat.R8G8B8A8Unorm,
                Usage = GpuImageUsage.SampledImage
            })
        {
        }

        public override void Upload(ReadOnlySpan<byte> pixels) { }
    }
}
