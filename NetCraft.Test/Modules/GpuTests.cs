using System.Runtime.InteropServices;
using NetCraft.Gpu.Vulkan;
using Silk.NET.Vulkan;

namespace NetCraft.Test.Modules;

//Gpu Vulkan PoC 单元测试
//验证 Silk.NET Vulkan 绑定工具链可加载
//无 Vulkan 驱动环境直接 FAIL 符合用户要求
internal static class GpuTests
{
    public const string Module = "gpu";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Vulkan API loader available", TestVulkanApiLoader);
        yield return ("Vulkan instance creation succeeds", TestVulkanInstanceCreation);
    }

    //testVulkanApiLoader 验证 Silk.NET Vk.GetApi 能加载 libvulkan
    //无 Vulkan 驱动或 loader 缺失抛异常 FAIL
    private static bool TestVulkanApiLoader()
    {
        try
        {
            var vk = Vk.GetApi();
            return vk != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Vulkan loader failed: " + ex.Message);
            return false;
        }
    }

    //testVulkanInstanceCreation 验证能创建 VkInstance
    //创建失败说明 Vulkan 驱动不可用或扩展缺失 FAIL
    private static unsafe bool TestVulkanInstanceCreation()
    {
        try
        {
            var vk = Vk.GetApi();
            var appNamePtr = Marshal.StringToHGlobalAnsi("NetCraft.Gpu.PoC.Test");
            var engineNamePtr = Marshal.StringToHGlobalAnsi("NetCraft");
            try
            {
                var appInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = (byte*)appNamePtr,
                    ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                    PEngineName = (byte*)engineNamePtr,
                    EngineVersion = Vk.MakeVersion(1, 0, 0),
                    ApiVersion = Vk.MakeVersion(1, 2, 0),
                };
                var createInfo = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo,
                    EnabledExtensionCount = 0,
                    EnabledLayerCount = 0,
                };
                Instance instance;
                var result = vk.CreateInstance(&createInfo, null, &instance);
                if (result == Result.Success)
                {
                    vk.DestroyInstance(instance, null);
                }
                return result == Result.Success;
            }
            finally
            {
                Marshal.FreeHGlobal(appNamePtr);
                Marshal.FreeHGlobal(engineNamePtr);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Vulkan instance creation failed: " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            return false;
        }
    }
}
