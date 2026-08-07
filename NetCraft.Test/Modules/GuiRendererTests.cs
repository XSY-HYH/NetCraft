using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//GuiRendererTests 阶段 4.4a GuiRenderer.Prepare 合批逻辑单元测试
//覆盖同 pipeline+texture+scissor 合并为 1 mesh 不同则分组的合批规则
//纯 CPU 逻辑测试不依赖 Vulkan Draw/Upload 留集成测试
internal static class GuiRendererTests
{
    public const string Module = "guirenderer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Prepare same pipeline+texture+scissor merges into 1 mesh", TestSameGroupMerges);
        yield return ("Prepare different pipeline separates meshes", TestDifferentPipelineSeparates);
        yield return ("Prepare different texture separates meshes", TestDifferentTextureSeparates);
        yield return ("Prepare different scissor separates meshes", TestDifferentScissorSeparates);
        yield return ("Prepare mesh vertex count accumulates", TestVertexCountAccumulates);
        yield return ("Prepare twice resets previous meshes", TestPrepareTwiceResets);
        yield return ("Prepare mixed groups correct mesh count", TestMixedGroups);
    }

    //MakePipeline 构造带 VertexFormat 的测试 pipeline 供 BlitRenderState 用
    private static RenderPipeline MakePipeline(string location) => PipelineBuilder.From()
        .WithLocation(location)
        .WithVertexShader("core/test")
        .WithFragmentShader("core/test")
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_TEX_COLOR)
        .Build();

    private static ScreenRectangle FullScreen() => new(0, 0, 800, 600);
    private const int White = unchecked((int)0xFFFFFFFF);

    //MakeBlit 构造指定位置的 BlitRenderState
    private static BlitRenderState MakeBlit(RenderPipeline pipeline, int x, int y, ScreenRectangle scissor)
        => new(pipeline, TextureSetup.NoTexture, Matrix3x2.Identity,
            x, y, x + 10, y + 10, 0f, 1f, 0f, 1f, White, scissor);

    //TestSameGroupMerges 验证 3 个同 pipeline+texture+scissor 元素合并为 1 mesh 顶点数 12
    private static bool TestSameGroupMerges()
    {
        var pipeline = MakePipeline("test/same");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, scissor));
        state.AddGuiElement(MakeBlit(pipeline, 100, 0, scissor));
        state.AddGuiElement(MakeBlit(pipeline, 200, 0, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);

        return renderer.Meshes.Count == 1
            && renderer.Meshes[0].Draw.VertexCount == 12;
    }

    //TestDifferentPipelineSeparates 验证不同 pipeline 分成 2 mesh
    private static bool TestDifferentPipelineSeparates()
    {
        var p1 = MakePipeline("test/p1");
        var p2 = MakePipeline("test/p2");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(p1, 0, 0, scissor));
        state.AddGuiElement(MakeBlit(p2, 100, 0, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);

        return renderer.Meshes.Count == 2
            && renderer.Meshes[0].Pipeline == p1
            && renderer.Meshes[1].Pipeline == p2;
    }

    //TestDifferentTextureSeparates 验证不同 texture 分成 2 mesh
    private static bool TestDifferentTextureSeparates()
    {
        var pipeline = MakePipeline("test/tex");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        //NoTexture 和 mock texture 不同分组 null texture 会与 NoTexture Equals 误合批用 TestGpuImage 区分
        var t1 = TextureSetup.NoTexture;
        var t2 = TextureSetup.SingleTexture(new TestGpuImage(16, 16), null!);
        state.AddGuiElement(new BlitRenderState(pipeline, t1, Matrix3x2.Identity,
            0, 0, 10, 10, 0f, 1f, 0f, 1f, White, scissor));
        state.AddGuiElement(new BlitRenderState(pipeline, t2, Matrix3x2.Identity,
            100, 0, 110, 10, 0f, 1f, 0f, 1f, White, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);

        return renderer.Meshes.Count == 2;
    }

    //TestDifferentScissorSeparates 验证不同 scissor 分成 2 mesh
    private static bool TestDifferentScissorSeparates()
    {
        var pipeline = MakePipeline("test/scissor");
        var s1 = new ScreenRectangle(0, 0, 400, 300);
        var s2 = new ScreenRectangle(400, 0, 400, 300);
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, s1));
        state.AddGuiElement(MakeBlit(pipeline, 400, 0, s2));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);

        return renderer.Meshes.Count == 2
            && renderer.Meshes[0].ScissorArea == s1
            && renderer.Meshes[1].ScissorArea == s2;
    }

    //TestVertexCountAccumulates 验证 mesh 顶点数随元素累加
    private static bool TestVertexCountAccumulates()
    {
        var pipeline = MakePipeline("test/accum");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);
        if (renderer.Meshes.Count != 1 || renderer.Meshes[0].Draw.VertexCount != 4) return false;

        //第二次 Prepare 添加 2 个元素
        state.Reset();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, scissor));
        state.AddGuiElement(MakeBlit(pipeline, 100, 0, scissor));
        renderer.Prepare(state);

        return renderer.Meshes.Count == 1 && renderer.Meshes[0].Draw.VertexCount == 8;
    }

    //TestPrepareTwiceResets 验证重复 Prepare 重置上一帧 mesh
    private static bool TestPrepareTwiceResets()
    {
        var pipeline = MakePipeline("test/reset");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, scissor));
        state.AddGuiElement(MakeBlit(pipeline, 100, 0, scissor));
        state.AddGuiElement(MakeBlit(pipeline, 200, 0, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);
        if (renderer.Meshes.Count != 1) return false;

        //第二次 Prepare 只 1 个元素 mesh 应只有 1 个顶点数 4
        state.Reset();
        state.AddGuiElement(MakeBlit(pipeline, 0, 0, scissor));
        renderer.Prepare(state);

        return renderer.Meshes.Count == 1 && renderer.Meshes[0].Draw.VertexCount == 4;
    }

    //TestMixedGroups 验证混合分组合批正确
    //3 个 A 组(同)+2 个 B 组(同)+1 个 C 组(单)应得 3 mesh 顶点数 12+8+4
    private static bool TestMixedGroups()
    {
        var pA = MakePipeline("test/a");
        var pB = MakePipeline("test/b");
        var pC = MakePipeline("test/c");
        var scissor = FullScreen();
        var state = new GuiRenderState();
        //A 组 3 个
        state.AddGuiElement(MakeBlit(pA, 0, 0, scissor));
        state.AddGuiElement(MakeBlit(pA, 100, 0, scissor));
        state.AddGuiElement(MakeBlit(pA, 200, 0, scissor));
        //B 组 2 个不同 pipeline
        state.AddGuiElement(MakeBlit(pB, 0, 100, scissor));
        state.AddGuiElement(MakeBlit(pB, 100, 100, scissor));
        //C 组 1 个不同 pipeline
        state.AddGuiElement(MakeBlit(pC, 0, 200, scissor));

        using var renderer = new GuiRenderer();
        renderer.Prepare(state);

        if (renderer.Meshes.Count != 3) return false;
        //按添加顺序 mesh[0]=A(12) mesh[1]=B(8) mesh[2]=C(4)
        return renderer.Meshes[0].Pipeline == pA && renderer.Meshes[0].Draw.VertexCount == 12
            && renderer.Meshes[1].Pipeline == pB && renderer.Meshes[1].Draw.VertexCount == 8
            && renderer.Meshes[2].Pipeline == pC && renderer.Meshes[2].Draw.VertexCount == 4;
    }

    //TestGpuImage 测试用 GpuImage 桩不依赖 Vulkan 供 TextureSetup.SingleTexture 绑定
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
