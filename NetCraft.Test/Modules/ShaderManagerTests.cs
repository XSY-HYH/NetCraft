using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//ShaderManagerTests 阶段 8 shader 机制单元测试
//覆盖嵌入 GLSL 加载 SPIR-V 编译 defines 注入 缓存命中 所有 RenderPipelines 静态 pipeline shader 可加载
internal static class ShaderManagerTests
{
    public const string Module = "shadermanager";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LoadVertexShader returns non-empty SPIR-V for core/gui", TestLoadVertexShaderCoreGui);
        yield return ("LoadFragmentShader returns non-empty SPIR-V for core/gui", TestLoadFragmentShaderCoreGui);
        yield return ("LoadVertexShader cached second call returns same reference", TestCacheHitSameReference);
        yield return ("LoadFragmentShader with IS_GRAYSCALE define compiles core/text", TestLoadWithDefines);
        yield return ("LoadFragmentShader different defines returns different SPIR-V", TestDifferentDefinesDifferentSpirv);
        yield return ("All RenderPipelines static pipelines shaders load successfully", TestAllStaticPipelinesShadersLoad);
        yield return ("LoadVertexShader throws for missing shader resource", TestThrowsForMissingShader);
    }

    //TestLoadVertexShaderCoreGui 加载 core/gui.vert.glsl 编译为 SPIR-V 非空
    private static bool TestLoadVertexShaderCoreGui()
    {
        var mgr = new ShaderManager();
        var spirv = mgr.LoadVertexShader("core/gui");
        return spirv != null && spirv.Length > 0;
    }

    //TestLoadFragmentShaderCoreGui 加载 core/gui.frag.glsl 编译为 SPIR-V 非空
    private static bool TestLoadFragmentShaderCoreGui()
    {
        var mgr = new ShaderManager();
        var spirv = mgr.LoadFragmentShader("core/gui");
        return spirv != null && spirv.Length > 0;
    }

    //TestCacheHitSameReference 同 name+defines 二次调用返回相同引用零编译
    private static bool TestCacheHitSameReference()
    {
        var mgr = new ShaderManager();
        var first = mgr.LoadVertexShader("core/gui");
        var second = mgr.LoadVertexShader("core/gui");
        return ReferenceEquals(first, second);
    }

    //TestLoadWithDefines 带 IS_GRAYSCALE define 编译 core/text fragment shader
    private static bool TestLoadWithDefines()
    {
        var mgr = new ShaderManager();
        var defines = ShaderDefines.NewBuilder().Define("IS_GRAYSCALE").Build();
        var spirv = mgr.LoadFragmentShader("core/text", defines);
        return spirv != null && spirv.Length > 0;
    }

    //TestDifferentDefinesDifferentSpirv 不同 defines 编译产出不同 SPIR-V 字节码
    private static bool TestDifferentDefinesDifferentSpirv()
    {
        var mgr = new ShaderManager();
        var noDefine = mgr.LoadFragmentShader("core/text");
        var withDefine = mgr.LoadFragmentShader("core/text",
            ShaderDefines.NewBuilder().Define("IS_GRAYSCALE").Build());
        return !ReferenceEquals(noDefine, withDefine)
            && !noDefine.SequenceEqual(withDefine);
    }

    //TestAllStaticPipelinesShadersLoad 遍历 RenderPipelines 所有静态 pipeline 加载 vertex+fragment shader
    //验证 GUI/GUI_INVERT/GUI_TEXTURED/GUI_TEXT/DEBUG_QUADS/BLIT 等引用的 shader 资源都存在且可编译
    private static bool TestAllStaticPipelinesShadersLoad()
    {
        var mgr = new ShaderManager();
        foreach (var pipeline in RenderPipelines.GetStaticPipelines())
        {
            var vs = mgr.LoadVertexShader(pipeline.VertexShader, pipeline.ShaderDefines);
            var fs = mgr.LoadFragmentShader(pipeline.FragmentShader, pipeline.ShaderDefines);
            if (vs == null || vs.Length == 0) return false;
            if (fs == null || fs.Length == 0) return false;
        }
        return true;
    }

    //TestThrowsForMissingShader 不存在的 shader name 抛 FileNotFoundException
    private static bool TestThrowsForMissingShader()
    {
        var mgr = new ShaderManager();
        try
        {
            mgr.LoadVertexShader("nonexistent/shader");
            return false;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
    }
}
