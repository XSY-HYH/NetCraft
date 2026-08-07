using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft;

//内嵌程序集加载器（.NET 独家技术）。
//通过 AssemblyLoadContext.Resolving 事件，在子库未被运行时找到时，
//从主库内嵌资源（NetCraft.Embedded.*.dll）加载字节流。
//对应 [C#内核重写计划.md] 第四节"通过内嵌资源加载子库"。
public static class EmbeddedAssemblyLoader
{
    //主库程序集（包含内嵌资源）。
    private static readonly Assembly MainAssembly = typeof(EmbeddedAssemblyLoader).Assembly;

    //内嵌资源的命名前缀，与 csproj 中 LogicalName 一致。
    private const string ResourcePrefix = "NetCraft.Embedded.";

    //是否已初始化。
    private static int _initialized;

    //注册内嵌资源解析回调。应在程序启动最早期调用一次。
    //幂等：多次调用只生效一次。
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        var context = AssemblyLoadContext.Default;
        context.Resolving += OnResolvingAssembly;
    }

    //解析失败时回调：尝试从内嵌资源加载子库。
    private static Assembly? OnResolvingAssembly(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
        {
            return null;
        }

        var resourceName = ResourcePrefix + name.Name + ".dll";
        using var stream = MainAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        return context.LoadFromStream(stream);
    }

    //列出所有可加载的内嵌子库（仅用于诊断/调试）。
    public static IReadOnlyList<string> ListEmbeddedAssemblies()
    {
        var result = new List<string>();
        foreach (var name in MainAssembly.GetManifestResourceNames())
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".dll", StringComparison.Ordinal))
            {
                var assemblyName = name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - ".dll".Length);
                result.Add(assemblyName);
            }
        }
        return result;
    }
}

