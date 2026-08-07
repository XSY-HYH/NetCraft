using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//RenderPipelinesTests 声明式 pipeline 体系单元测试对标原版 RenderPipeline/Snippet/PipelineBuilder 行为
//覆盖 pipeline 构造 Snippet 组合 sortKey 分配 RenderPipelines 静态注册 PipelineCache 缓存命中
internal static class RenderPipelinesTests
{
    public const string Module = "renderpipelines";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("PipelineBuilder.Build constructs pipeline with all fields", TestBuildConstructsPipeline);
        yield return ("PipelineBuilder.Build throws when location missing", TestBuildThrowsMissingLocation);
        yield return ("PipelineBuilder.Build throws when vertex shader missing", TestBuildThrowsMissingVertexShader);
        yield return ("PipelineBuilder.Build throws when primitive topology missing", TestBuildThrowsMissingTopology);
        yield return ("RenderPipeline.SortKey is monotonically increasing", TestSortKeyMonotonic);
        yield return ("RenderPipeline.WantsDepthTexture true when depth state set", TestWantsDepthTexture);
        yield return ("Snippet combination: later overrides earlier non-null fields", TestSnippetCombination);
        yield return ("PipelineBuilder.From combines snippets", TestFromCombinesSnippets);
        yield return ("RenderPipelines.GUI registered with correct location", TestGuiRegistered);
        yield return ("RenderPipelines.GetStaticPipelines contains GUI variants", TestGetStaticPipelines);
        yield return ("RenderPipelines.GetByLocation returns registered pipeline", TestGetByLocation);
        yield return ("RenderPipelines all 15 static pipelines registered with location", TestAllStaticPipelinesRegistered);
        yield return ("BlendFunction.LIGHTNING preset fields", TestBlendFunctionLightning);
        yield return ("BlendFunction.TRANSLUCENT preset fields", TestBlendFunctionTranslucent);
        yield return ("BlendFunction.INVERT preset fields", TestBlendFunctionInvert);
        yield return ("ColorTargetState.DEFAULT fields", TestColorTargetStateDefault);
        yield return ("DepthStencilState.DEFAULT fields", TestDepthStencilStateDefault);
        yield return ("ShaderDefines.Builder accumulates values and flags", TestShaderDefinesBuilder);
        yield return ("BindGroupLayout.Builder accumulates samplers and uniforms", TestBindGroupLayoutBuilder);
        yield return ("DefaultVertexFormat.POSITION_COLOR has Position+Color elements", TestPositionColorFormat);
        yield return ("DefaultVertexFormat.POSITION_TEX_COLOR has 3 elements", TestPositionTexColorFormat);
        yield return ("PipelineCache.Precompile hits cache on second call", TestPipelineCacheHit);
        yield return ("PipelineCache.Precompile returns same instance for same declaration", TestPipelineCacheSameInstance);
    }

    private static RenderPipeline BuildSimplePipeline(string location) =>
        PipelineBuilder.From()
            .WithLocation(location)
            .WithVertexShader("core/test")
            .WithFragmentShader("core/test")
            .WithPrimitiveTopology(PrimitiveTopology.Quads)
            .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR)
            .Build();

    private static bool TestBuildConstructsPipeline()
    {
        var p = BuildSimplePipeline("pipeline/test_simple");
        return p.Location == "pipeline/test_simple"
            && p.VertexShader == "core/test"
            && p.FragmentShader == "core/test"
            && p.PrimitiveTopology == PrimitiveTopology.Quads
            && p.PolygonMode == PolygonMode.Fill
            && p.Cull
            && p.ColorTargetStates.Count == 1
            && p.ColorTargetStates[0].Equals(ColorTargetState.DEFAULT)
            && p.VertexFormatPerBuffer[0] == DefaultVertexFormat.POSITION_COLOR;
    }

    private static bool TestBuildThrowsMissingLocation()
    {
        try
        {
            PipelineBuilder.From()
                .WithVertexShader("v")
                .WithFragmentShader("f")
                .WithPrimitiveTopology(PrimitiveTopology.Quads)
                .Build();
            return false;
        }
        catch (InvalidOperationException) { return true; }
    }

    private static bool TestBuildThrowsMissingVertexShader()
    {
        try
        {
            PipelineBuilder.From()
                .WithLocation("p/x")
                .WithFragmentShader("f")
                .WithPrimitiveTopology(PrimitiveTopology.Quads)
                .Build();
            return false;
        }
        catch (InvalidOperationException) { return true; }
    }

    private static bool TestBuildThrowsMissingTopology()
    {
        try
        {
            PipelineBuilder.From()
                .WithLocation("p/x")
                .WithVertexShader("v")
                .WithFragmentShader("f")
                .Build();
            return false;
        }
        catch (InvalidOperationException) { return true; }
    }

    private static bool TestSortKeyMonotonic()
    {
        var p1 = BuildSimplePipeline("p/1");
        var p2 = BuildSimplePipeline("p/2");
        var p3 = BuildSimplePipeline("p/3");
        return p2.SortKey > p1.SortKey && p3.SortKey > p2.SortKey;
    }

    private static bool TestWantsDepthTexture()
    {
        var withDepth = PipelineBuilder.From()
            .WithLocation("p/depth")
            .WithVertexShader("v").WithFragmentShader("f")
            .WithPrimitiveTopology(PrimitiveTopology.Quads)
            .WithDepthStencilState(DepthStencilState.DEFAULT)
            .Build();
        var noDepth = BuildSimplePipeline("p/nodepth");
        return withDepth.WantsDepthTexture() && !noDepth.WantsDepthTexture();
    }

    private static bool TestSnippetCombination()
    {
        var baseSnippet = PipelineBuilder.From()
            .WithVertexShader("core/base")
            .WithPrimitiveTopology(PrimitiveTopology.Quads)
            .BuildSnippet();
        var overrideSnippet = PipelineBuilder.From()
            .WithVertexShader("core/override")
            .BuildSnippet();
        var combined = PipelineBuilder.From(baseSnippet, overrideSnippet).BuildSnippet();
        return combined.VertexShader == "core/override"
            && combined.PrimitiveTopology == PrimitiveTopology.Quads;
    }

    private static bool TestFromCombinesSnippets()
    {
        var globals = PipelineBuilder.From().WithBindGroupLayout(BindGroupLayouts.GLOBALS).BuildSnippet();
        var matrices = PipelineBuilder.From(globals).WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION).BuildSnippet();
        var combined = PipelineBuilder.From(matrices).BuildSnippet();
        return combined.BindGroupLayouts != null && combined.BindGroupLayouts.Count == 2;
    }

    private static bool TestGuiRegistered()
    {
        return RenderPipelines.GUI.Location == "pipeline/gui"
            && RenderPipelines.GUI_INVERT.Location == "pipeline/gui_invert"
            && RenderPipelines.GUI_TEXTURED.Location == "pipeline/gui_textured"
            && RenderPipelines.GUI_TEXT.Location == "pipeline/gui_text";
    }

    private static bool TestGetStaticPipelines()
    {
        var all = RenderPipelines.GetStaticPipelines();
        return all.Contains(RenderPipelines.GUI)
            && all.Contains(RenderPipelines.GUI_INVERT)
            && all.Contains(RenderPipelines.GUI_TEXTURED)
            && all.Contains(RenderPipelines.GUI_TEXT)
            && all.Contains(RenderPipelines.DEBUG_QUADS)
            && all.Contains(RenderPipelines.BLIT);
    }

    private static bool TestGetByLocation()
    {
        return RenderPipelines.GetByLocation("pipeline/gui") == RenderPipelines.GUI
            && RenderPipelines.GetByLocation("pipeline/nonexistent") == null;
    }

    //TestAllStaticPipelinesRegistered 验证 RenderPipelines 声明的 15 个 pipeline 全部注册到 location 表
    //覆盖 GUI/GUI_INVERT/GUI_TEXT_HIGHLIGHT/GUI_TEXTURED/GUI_TEXTURED_PREMULTIPLIED_ALPHA/GUI_TEXT/GUI_TEXT_GRAYSCALE
    //F7 新增 GUI_TEXT_SEE_THROUGH/GUI_TEXT_POLYGON_OFFSET/GUI_TEXT_GRAYSCALE_SEE_THROUGH/GUI_TEXT_GRAYSCALE_POLYGON_OFFSET
    //阶段8 新增 BLUR 后处理 pipeline DEBUG_QUADS/BLIT P10 新增 ITEM_3D 3D 物品渲染 pipeline
    private static bool TestAllStaticPipelinesRegistered()
    {
        var all = new (RenderPipeline Pipeline, string Location)[]
        {
            (RenderPipelines.GUI, "pipeline/gui"),
            (RenderPipelines.GUI_INVERT, "pipeline/gui_invert"),
            (RenderPipelines.GUI_TEXT_HIGHLIGHT, "pipeline/gui_text_highlight"),
            (RenderPipelines.GUI_TEXTURED, "pipeline/gui_textured"),
            (RenderPipelines.GUI_TEXTURED_PREMULTIPLIED_ALPHA, "pipeline/gui_textured_premultiplied_alpha"),
            (RenderPipelines.GUI_TEXT, "pipeline/gui_text"),
            (RenderPipelines.GUI_TEXT_GRAYSCALE, "pipeline/gui_text_grayscale"),
            (RenderPipelines.GUI_TEXT_SEE_THROUGH, "pipeline/gui_text_see_through"),
            (RenderPipelines.GUI_TEXT_POLYGON_OFFSET, "pipeline/gui_text_polygon_offset"),
            (RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH, "pipeline/gui_text_grayscale_see_through"),
            (RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET, "pipeline/gui_text_grayscale_polygon_offset"),
            (RenderPipelines.DEBUG_QUADS, "pipeline/debug_quads"),
            (RenderPipelines.BLIT, "pipeline/blit"),
            (RenderPipelines.BLUR, "pipeline/blur"),
            (RenderPipelines.ITEM_3D, "pipeline/item_3d"),
        };
        var staticSet = RenderPipelines.GetStaticPipelines();
        foreach (var (pipeline, location) in all)
        {
            if (pipeline.Location != location) return false;
            if (pipeline.VertexShader is null || pipeline.FragmentShader is null) return false;
            if (pipeline.BindGroupLayouts.Count == 0) return false;
            if (!staticSet.Contains(pipeline)) return false;
            if (RenderPipelines.GetByLocation(location) != pipeline) return false;
        }
        return staticSet.Count == all.Length;
    }

    private static bool TestBlendFunctionLightning()
    {
        var f = BlendFunction.LIGHTNING;
        return f.Color.SourceFactor == BlendFactor.SrcAlpha
            && f.Color.DestFactor == BlendFactor.One
            && f.Color.Op == BlendOp.Add
            && f.Alpha == f.Color;
    }

    private static bool TestBlendFunctionTranslucent()
    {
        var f = BlendFunction.TRANSLUCENT;
        return f.Color.SourceFactor == BlendFactor.SrcAlpha
            && f.Color.DestFactor == BlendFactor.OneMinusSrcAlpha
            && f.Alpha.SourceFactor == BlendFactor.One
            && f.Alpha.DestFactor == BlendFactor.OneMinusSrcAlpha;
    }

    private static bool TestBlendFunctionInvert()
    {
        var f = BlendFunction.INVERT;
        return f.Color.SourceFactor == BlendFactor.OneMinusDstColor
            && f.Color.DestFactor == BlendFactor.OneMinusSrcColor
            && f.Alpha.SourceFactor == BlendFactor.One
            && f.Alpha.DestFactor == BlendFactor.Zero;
    }

    private static bool TestColorTargetStateDefault()
    {
        var d = ColorTargetState.DEFAULT;
        return d.BlendFunction == null
            && d.Format == GpuFormat.R8G8B8A8Unorm
            && d.WriteMask == ColorTargetState.WriteAll;
    }

    private static bool TestDepthStencilStateDefault()
    {
        var d = DepthStencilState.DEFAULT;
        return d.DepthTest == CompareOp.GreaterOrEqual
            && d.WriteDepth
            && d.DepthBiasScaleFactor == 0f
            && d.DepthBiasConstant == 0f;
    }

    private static bool TestShaderDefinesBuilder()
    {
        var defines = ShaderDefines.NewBuilder()
            .Define("FLAG_A")
            .Define("COUNT", 4)
            .Define("THRESHOLD", 0.5f)
            .Build();
        return defines.Flags.Contains("FLAG_A")
            && defines.Values["COUNT"] == "4"
            && defines.Values.ContainsKey("THRESHOLD");
    }

    private static bool TestBindGroupLayoutBuilder()
    {
        var layout = BindGroupLayout.Create()
            .AddSampler("Sampler0")
            .AddUniform("Matrices", UniformType.Mat4)
            .Build();
        return layout.Samplers.Count == 1 && layout.Samplers[0] == "Sampler0"
            && layout.Uniforms.Count == 1 && layout.Uniforms[0].Name == "Matrices"
            && layout.Uniforms[0].Type == UniformType.Mat4;
    }

    private static bool TestPositionColorFormat()
    {
        var f = DefaultVertexFormat.POSITION_COLOR;
        return f.Elements.Count == 2
            && f.Elements[0].Name == "Position"
            && f.Elements[0].Format == VertexElementFormat.Vec3
            && f.Elements[1].Name == "Color"
            && f.Elements[1].Format == VertexElementFormat.UByte4Norm
            && f.Stride == 16;
    }

    private static bool TestPositionTexColorFormat()
    {
        var f = DefaultVertexFormat.POSITION_TEX_COLOR;
        return f.Elements.Count == 3 && f.Stride == 24;
    }

    //TestPipelineCacheHit 第一次 Precompile 编译第二次直接命中缓存零编译开销
    private static bool TestPipelineCacheHit()
    {
        var device = new MockDevice(new EmptyGpuContext());
        var cache = new PipelineCache(device);
        var declaration = RenderPipelines.GUI;
        var first = cache.Precompile(declaration);
        var second = cache.Precompile(declaration);
        return ReferenceEquals(first, second) && device.CompileCount == 1;
    }

    private static bool TestPipelineCacheSameInstance()
    {
        var device = new MockDevice(new EmptyGpuContext());
        var cache = new PipelineCache(device);
        var a = cache.Precompile(RenderPipelines.GUI_TEXTURED);
        var b = cache.Precompile(RenderPipelines.GUI_TEXTURED);
        return ReferenceEquals(a, b);
    }

    //MockDevice 测试用 GpuDevice 桩不依赖 Vulkan
    //PrecompilePipeline 计数编译次数用于断言缓存命中
    private sealed class MockDevice : GpuDevice
    {
        public int CompileCount;
        public override DeviceLimits Limits { get; } = new(4096);
        public MockDevice(GpuContext context) : base(context) { }

        public override CompiledRenderPipeline PrecompilePipeline(RenderPipeline declaration)
        {
            CompileCount++;
            return new MockCompiledPipeline(RenderPipelineDescription.FromDeclaration(declaration, ShaderManager));
        }

        public override GpuCommandBuffer CreateCommandBuffer() => throw new NotSupportedException();
        public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
            => new MockCompiledPipeline(description);
        public override GpuBuffer CreateBuffer(int size, GpuBufferUsage usage) => throw new NotSupportedException();
        public override GpuImage CreateImage(GpuImageDescription desc) => throw new NotSupportedException();
        public override GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main") => throw new NotSupportedException();
        public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description) => throw new NotSupportedException();
        public override GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout layout) => throw new NotSupportedException();
        public override GpuSampler CreateSampler(GpuSamplerDescription description) => throw new NotSupportedException();
    }

    private sealed class MockCompiledPipeline : CompiledRenderPipeline
    {
        public MockCompiledPipeline(RenderPipelineDescription description) : base(description) { }
    }
}
