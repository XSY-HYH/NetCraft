using NetCraft.Registry;

namespace NetCraft.Resources;

//FolderPackResources 文件夹型资源包对应原版 net.minecraft.server.packs.FilePackResources
//从文件系统目录读取资源 assets/对应ClientResources data/对应ServerData
public sealed class FolderPackResources : PackResources
{
    private readonly string _rootPath;

    public FolderPackResources(string packId, string rootPath) : base(packId)
    {
        _rootPath = rootPath;
    }

    //TypeToDir 资源类型到根目录名映射对齐原版 assets/data 约定
    internal static string TypeToDir(PackType type) => type switch
    {
        PackType.ClientResources => "assets",
        PackType.ServerData => "data",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public override Stream? GetRootResource(string path)
    {
        var fullPath = Path.Combine(_rootPath, path);
        return File.Exists(fullPath) ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public override Stream? GetResource(PackType type, Identifier location)
    {
        var relativePath = Path.Combine(TypeToDir(type), location.Namespace, location.Path);
        var fullPath = Path.Combine(_rootPath, relativePath);
        return File.Exists(fullPath) ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output)
    {
        var typeDir = Path.Combine(_rootPath, TypeToDir(type), namespaceName, pathPrefix);
        if (!Directory.Exists(typeDir)) return;
        foreach (var file in Directory.EnumerateFiles(typeDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(typeDir, file).Replace(Path.DirectorySeparatorChar, '/');
            output.Add(Identifier.FromNamespaceAndPath(namespaceName, pathPrefix + "/" + relative));
        }
    }

    public override ISet<string> GetNamespaces(PackType type)
    {
        var result = new HashSet<string>();
        var typeDir = Path.Combine(_rootPath, TypeToDir(type));
        if (!Directory.Exists(typeDir)) return result;
        foreach (var dir in Directory.EnumerateDirectories(typeDir))
        {
            result.Add(Path.GetFileName(dir));
        }
        return result;
    }
}
