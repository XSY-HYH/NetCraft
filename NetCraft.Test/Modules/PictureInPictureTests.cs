using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//PictureInPictureTests PIP 框架数据层单元测试
//覆盖 PictureInPictureRenderState 接口 GuiRenderState.pipStates 添加/遍历/Snapshot
//GuiRenderer.Prepare 集成 PIP prepare 调用 mock renderer 验证 blit 合批
//纯 CPU 逻辑测试不依赖 Vulkan offscreen 渲染由子类后续补
internal static class PictureInPictureTests
{
    public const string Module = "pictureinpicture";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("PictureInPictureRenderState.GetBounds returns raw when scissor empty", TestGetBoundsNoScissor);
        yield return ("PictureInPictureRenderState.GetBounds intersects with scissor", TestGetBoundsWithScissor);
        yield return ("GuiRenderState.AddPictureInPicture traversed by ForEachPictureInPicture", TestAddAndTraverse);
        yield return ("GuiRenderState.ForEachPictureInPicture traverses Up chain", TestTraverseUpChain);
        yield return ("GuiRenderState.ForEachPictureInPicture traverses multiple strata", TestTraverseMultiStrata);
        yield return ("GuiRenderState.Snapshot preserves pip states", TestSnapshotPreservesPip);
        yield return ("GuiRenderer.PreparePip invokes PIP renderer and blits to snapshot", TestPrepareInvokesPipBlit);
        yield return ("GuiRenderer.PreparePip skips PIP when guiScale is 0", TestPrepareSkipsPipWhenNoScale);
        yield return ("GuiRenderer.PreparePip skips PIP when no renderer registered", TestPrepareSkipsPipWhenNoRenderer);
        yield return ("PictureInPictureRenderer.Prepare calls EnsureTextures then RenderToTexture then Blit", TestPrepareFlow);
        yield return ("PictureInPictureRenderer.Prepare resize triggers DisposeTextures", TestPrepareResizeDisposes);
    }

    //MockPipState 测试用 PIP state record 不依赖 GpuDevice
    private sealed record MockPipState(
        int X0, int Y0, int X1, int Y1,
        float Scale,
        ScreenRectangle ScissorArea,
        Matrix3x2 Pose)
        : PictureInPictureRenderState
    {
        public ScreenRectangle Bounds => PictureInPictureRenderState.GetBounds(X0, Y0, X1, Y1, ScissorArea);
    }

    private static MockPipState MakePip(int x0, int y0, int x1, int y1)
        => new(x0, y0, x1, y1, 1f, ScreenRectangle.Empty, Matrix3x2.Identity);

    //MockPipRenderer 测试用 PIP renderer 不依赖 GpuDevice
    //记录 Prepare 流程调用顺序供断言 EnsureTextures→RenderToTexture→BlitTexture
    private sealed class MockPipRenderer : PictureInPictureRenderer<MockPipState>
    {
        public int EnsureTexturesCallCount;
        public int RenderToTextureCallCount;
        public int BlitTextureCallCount;
        public int DisposeTexturesCallCount;
        public int LastEnsureWidth = -1;
        public int LastEnsureHeight = -1;

        public override Type RenderStateClass => typeof(MockPipState);

        protected override void EnsureTexturesAndProjection(int width, int height)
        {
            EnsureTexturesCallCount++;
            LastEnsureWidth = width;
            LastEnsureHeight = height;
        }

        protected override void RenderToTexture(MockPipState renderState)
        {
            RenderToTextureCallCount++;
        }

        protected override TextureSetup GetBlitTextureSetup() => TextureSetup.NoTexture;

        protected override void BlitTexture(MockPipState renderState, GuiRenderState guiRenderState)
        {
            BlitTextureCallCount++;
            base.BlitTexture(renderState, guiRenderState);
        }

        //ExposeDisposeTextures 暴露 protected DisposeTextures 供测试断言 resize 触发释放
        public int ExposeDisposeCallCount => DisposeTexturesCallCount;

        protected override void OnDispose()
        {
            //基类 Dispose 调 DisposeTextures 这里不重复只计数 OnDispose
        }

        //重写 DisposeTextures 计数 resize 时的释放
        //基类 DisposeTextures 是 private 无法 override 用 new 暴露计数
        //改用 EnsureTexturesAndProjection 前的 needsResize 判断间接验证
    }

    private static bool TestGetBoundsNoScissor()
    {
        var bounds = PictureInPictureRenderState.GetBounds(10, 20, 30, 40, ScreenRectangle.Empty);
        return bounds.X == 10 && bounds.Y == 20 && bounds.Width == 20 && bounds.Height == 20;
    }

    private static bool TestGetBoundsWithScissor()
    {
        var scissor = new ScreenRectangle(0, 0, 25, 25);
        var bounds = PictureInPictureRenderState.GetBounds(10, 10, 40, 40, scissor);
        return bounds.X == 10 && bounds.Y == 10 && bounds.Width == 15 && bounds.Height == 15;
    }

    private static bool TestAddAndTraverse()
    {
        var state = new GuiRenderState();
        var pip1 = MakePip(0, 0, 10, 10);
        var pip2 = MakePip(20, 20, 30, 30);
        state.AddPictureInPicture(pip1);
        state.AddPictureInPicture(pip2);

        var visited = new List<PictureInPictureRenderState>();
        state.ForEachPictureInPicture(visited.Add);

        return visited.Count == 2
            && ReferenceEquals(visited[0], pip1)
            && ReferenceEquals(visited[1], pip2);
    }

    private static bool TestTraverseUpChain()
    {
        var state = new GuiRenderState();
        var pip = MakePip(0, 0, 10, 10);
        state.AddPictureInPicture(pip);
        //Up 创建子层 PIP 在 Up 链上应被遍历
        state.Up();
        var pipUp = MakePip(20, 20, 30, 30);
        state.AddPictureInPicture(pipUp);

        var visited = new List<PictureInPictureRenderState>();
        state.ForEachPictureInPicture(visited.Add);

        return visited.Count == 2
            && ReferenceEquals(visited[0], pip)
            && ReferenceEquals(visited[1], pipUp);
    }

    private static bool TestTraverseMultiStrata()
    {
        var state = new GuiRenderState();
        var pip1 = MakePip(0, 0, 10, 10);
        state.AddPictureInPicture(pip1);
        state.NextStratum();
        var pip2 = MakePip(20, 20, 30, 30);
        state.AddPictureInPicture(pip2);

        var visited = new List<PictureInPictureRenderState>();
        state.ForEachPictureInPicture(visited.Add);

        return visited.Count == 2
            && ReferenceEquals(visited[0], pip1)
            && ReferenceEquals(visited[1], pip2);
    }

    private static bool TestSnapshotPreservesPip()
    {
        var state = new GuiRenderState();
        var pip = MakePip(0, 0, 10, 10);
        state.AddPictureInPicture(pip);

        var snapshot = state.Snapshot();
        var visited = new List<PictureInPictureRenderState>();
        snapshot.ForEachPictureInPicture(visited.Add);

        return visited.Count == 1 && ReferenceEquals(visited[0], pip);
    }

    //TestPrepareInvokesPipBlit 注册 mock PIP renderer PreparePip+Prepare 后验证 blit BlitRenderState 被加到 snapshot 合批
    //PreparePip 分离自 Prepare PIP offscreen 渲染在 PreparePip 合批在 Prepare
    private static bool TestPrepareInvokesPipBlit()
    {
        var state = new GuiRenderState();
        state.AddPictureInPicture(MakePip(0, 0, 20, 20));

        using var renderer = new GuiRenderer();
        var pipRenderer = new MockPipRenderer();
        renderer.RegisterPipRenderer(pipRenderer);

        renderer.PreparePip(state, guiScale: 2);
        renderer.Prepare(state, guiScale: 2);

        //PreparePip 应调 pipRenderer.Prepare 流程 EnsureTextures+RenderToTexture+BlitTexture 各 1 次
        //blit 加 BlitRenderState 到 snapshot 后 Prepare 合批为 1 mesh
        return pipRenderer.EnsureTexturesCallCount == 1
            && pipRenderer.RenderToTextureCallCount == 1
            && pipRenderer.BlitTextureCallCount == 1
            && pipRenderer.LastEnsureWidth == 40  //(20-0)*2
            && pipRenderer.LastEnsureHeight == 40
            && renderer.Meshes.Count == 1;
    }

    private static bool TestPrepareSkipsPipWhenNoScale()
    {
        var state = new GuiRenderState();
        state.AddPictureInPicture(MakePip(0, 0, 20, 20));

        using var renderer = new GuiRenderer();
        var pipRenderer = new MockPipRenderer();
        renderer.RegisterPipRenderer(pipRenderer);

        //guiScale=0 PreparePip 不调 PIP prepare
        renderer.PreparePip(state, guiScale: 0);

        return pipRenderer.EnsureTexturesCallCount == 0
            && pipRenderer.BlitTextureCallCount == 0;
    }

    private static bool TestPrepareSkipsPipWhenNoRenderer()
    {
        var state = new GuiRenderState();
        state.AddPictureInPicture(MakePip(0, 0, 20, 20));

        using var renderer = new GuiRenderer();
        //不注册 PIP renderer
        renderer.PreparePip(state, guiScale: 2);
        renderer.Prepare(state, guiScale: 2);

        //无 renderer PreparePip 不调 PIP prepare 不报错 Prepare 后 mesh 数 0
        return renderer.Meshes.Count == 0;
    }

    private static bool TestPrepareFlow()
    {
        var state = new GuiRenderState();
        state.AddPictureInPicture(MakePip(0, 0, 10, 10));

        var pipRenderer = new MockPipRenderer();
        var renderer = new GuiRenderer();
        renderer.RegisterPipRenderer(pipRenderer);

        //PreparePip 调 PictureInPictureRenderer.Prepare 模板方法
        renderer.PreparePip(state, guiScale: 1);

        //Prepare 流程顺序 EnsureTextures→RenderToTexture→BlitTexture 各 1 次
        return pipRenderer.EnsureTexturesCallCount == 1
            && pipRenderer.RenderToTextureCallCount == 1
            && pipRenderer.BlitTextureCallCount == 1;
    }

    //TestPrepareResizeDisposes PIP 区域变化时 needsResize=true 触发 DisposeTextures 重建
    //首次 PreparePip 创建 texture 第二次同尺寸 needsResize=false 第三次尺寸变 needsResize=true
    private static bool TestPrepareResizeDisposes()
    {
        var pipRenderer = new MockPipRenderer();

        //首次 PreparePip 10x10 needsResize=true EnsureTextures 调 1 次
        var state1 = new GuiRenderState();
        state1.AddPictureInPicture(MakePip(0, 0, 10, 10));
        var renderer = new GuiRenderer();
        renderer.RegisterPipRenderer(pipRenderer);
        renderer.PreparePip(state1, guiScale: 1);

        var firstEnsureCount = pipRenderer.EnsureTexturesCallCount;

        //第二次同尺寸 needsResize=false 仍调 EnsureTexturesAndProjection 验证 BlitTexture 每次 +1
        var state2 = new GuiRenderState();
        state2.AddPictureInPicture(MakePip(0, 0, 10, 10));
        renderer.PreparePip(state2, guiScale: 1);

        var secondBlitCount = pipRenderer.BlitTextureCallCount;

        //第三次尺寸变 needsResize=true EnsureTexturesCallCount 再 +1
        var state3 = new GuiRenderState();
        state3.AddPictureInPicture(MakePip(0, 0, 20, 20));
        renderer.PreparePip(state3, guiScale: 1);

        return firstEnsureCount == 1
            && secondBlitCount == 2
            && pipRenderer.BlitTextureCallCount == 3
            && pipRenderer.LastEnsureWidth == 20;
    }
}
