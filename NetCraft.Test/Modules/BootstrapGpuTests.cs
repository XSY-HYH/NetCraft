using NetCraft.Bootstrap;
using NetCraft.Gpu;
using NetCraft.Resources;
using NetCraft.Tags;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;

namespace NetCraft.Test.Modules;

//Bootstrap + Gpu 子库测试
//Bootstrap 引导状态与 Gpu EmptyGpuContext 空实现验证
internal static class BootstrapGpuTests
{
    public const string Module = "bootstrapgpu";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Bootstrap BootStrap sets flag", TestBootstrapSetsFlag);
        yield return ("Bootstrap Reset clears flag", TestBootstrapReset);
        yield return ("Bootstrap ValidateRegistries freezes all", TestValidateRegistries);
        yield return ("Bootstrap LoadBuiltinTags scans ServerData", TestLoadBuiltinTagsScans);
        yield return ("Gpu EmptyGpuContext returns Empty device", TestEmptyGpuContext);
        yield return ("Gpu EmptyGpuCommandBuffer no-op pass", TestEmptyGpuCommandBuffer);
    }

    //ValidateRegistries 反射遍历所有 BuiltInRegistries 注册表 Freeze 后不抛
    private static bool TestValidateRegistries()
    {
        BootstrapClass.Reset();
        BootstrapClass.ValidateRegistries();
        return true;
    }

    //LoadBuiltinTags 扫描空 ResourceManager 不抛
    private static bool TestLoadBuiltinTagsScans()
    {
        var manager = new ResourceManager();
        var tagManager = new TagManager();
        BootstrapClass.LoadBuiltinTags(tagManager, manager);
        return true;
    }

    private static bool TestBootstrapSetsFlag()
    {
        BootstrapClass.Reset();
        if (BootstrapClass.IsBootstrapped) return false;
        BootstrapClass.BootStrap();
        return BootstrapClass.IsBootstrapped;
    }

    private static bool TestBootstrapReset()
    {
        BootstrapClass.BootStrap();
        if (!BootstrapClass.IsBootstrapped) return false;
        BootstrapClass.Reset();
        return !BootstrapClass.IsBootstrapped;
    }

    private static bool TestEmptyGpuContext()
    {
        using var ctx = new EmptyGpuContext();
        if (ctx.Backend != GpuBackend.Empty) return false;
        var device = ctx.CreateDevice(new GpuDeviceOptions());
        var cmd = device.CreateCommandBuffer();
        var pipeline = device.CreateRenderPipeline(new RenderPipelineDescription());
        cmd.BeginRecording();
        cmd.BeginRenderPass(pipeline);
        cmd.Draw(3);
        cmd.EndRenderPass();
        cmd.EndRecording();
        cmd.Submit();
        return true;
    }

    private static bool TestEmptyGpuCommandBuffer()
    {
        using var ctx = new EmptyGpuContext();
        var device = ctx.CreateDevice(new GpuDeviceOptions());
        var cmd = device.CreateCommandBuffer();
        //多次调用都应空实现不抛
        cmd.BeginRecording();
        cmd.Draw(0);
        cmd.Submit();
        return true;
    }
}
