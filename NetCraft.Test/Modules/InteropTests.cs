using NetCraft.Interop;

namespace NetCraft.Test.Modules;

//Interop 子库测试
//覆盖 NativeLibraryAccessor 工厂方法签名和 InteropRuntime 编码 round-trip
internal static class InteropTests
{
    public const string Module = "interop";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("InteropRuntime Utf8 managed round-trip", TestUtf8ManagedRoundTrip);
        yield return ("InteropRuntime PlatformSuffix non-empty", TestPlatformSuffix);
        yield return ("InteropRuntime PlatformLibraryName contains base", TestPlatformLibraryName);
    }

    private static bool TestUtf8ManagedRoundTrip()
    {
        var s = "你好 NetCraft 喵";
        var bytes = InteropRuntime.StringToUtf8Managed(s);
        var back = InteropRuntime.Utf8ManagedToString(bytes);
        return back == s;
    }

    private static bool TestPlatformSuffix()
    {
        var suffix = InteropRuntime.PlatformSuffix;
        return suffix == "dll" || suffix == "so" || suffix == "dylib";
    }

    private static bool TestPlatformLibraryName()
    {
        var name = InteropRuntime.PlatformLibraryName("vulkan");
        return name.Contains("vulkan");
    }
}
