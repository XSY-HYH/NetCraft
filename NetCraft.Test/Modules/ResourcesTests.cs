using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Test.Modules;

//Resources 子库测试
//覆盖 ResourceManager 添加/移除 Pack + FolderPackResources 文件读取
internal static class ResourcesTests
{
    public const string Module = "resources";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ResourceManager add/remove pack", TestAddRemovePack);
        yield return ("ResourceManager priority ordering", TestPriorityOrdering);
        yield return ("FolderPackResources read file", TestFolderPackReadFile);
        yield return ("VanillaPackResources read asset", TestVanillaPackReadAsset);
    }

    //VanillaPackResources 从 assets/<ns>/<path> 读取客户端资源
    private static bool TestVanillaPackReadAsset()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-vanilla-" + Guid.NewGuid().ToString("N"));
        try
        {
            var assetDir = Path.Combine(dir, "assets", "minecraft", "textures");
            Directory.CreateDirectory(assetDir);
            File.WriteAllText(Path.Combine(assetDir, "stone.png"), "fake-png");
            var pack = new VanillaPackResources(dir);
            var id = Identifier.FromNamespaceAndPath("minecraft", "textures/stone.png");
            using var stream = pack.GetResource(PackType.ClientResources, id);
            if (stream == null) return false;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd() == "fake-png";
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static bool TestAddRemovePack()
    {
        var manager = new ResourceManager();
        var id = Identifier.FromNamespaceAndPath("minecraft", "test");
        var pack = CreateStubPack(id, priority: 0);
        manager.AddPack(pack);
        if (manager.Packs.Count != 1) return false;
        if (!manager.RemovePack(id)) return false;
        return manager.Packs.Count == 0;
    }

    private static bool TestPriorityOrdering()
    {
        var manager = new ResourceManager();
        manager.AddPack(CreateStubPack(Identifier.FromNamespaceAndPath("minecraft", "low"), priority: 10));
        manager.AddPack(CreateStubPack(Identifier.FromNamespaceAndPath("minecraft", "high"), priority: 1));
        if (manager.Packs.Count != 2) return false;
        //priority 小的在前
        return manager.Packs[0].Priority == 1 && manager.Packs[1].Priority == 10;
    }

    private static bool TestFolderPackReadFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-res-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var content = "hello resources";
            File.WriteAllText(Path.Combine(dir, "pack.txt"), content);
            var pack = new FolderPackResources("test", dir);
            using var stream = pack.GetRootResource("pack.txt");
            if (stream == null) return false;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd() == content;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static Pack CreateStubPack(Identifier id, int priority)
    {
        var stub = new StubPackResources(id.ToString());
        return new Pack(id, id.ToString(), "stub", priority, false, stub);
    }
}

//StubPackResources 占位实现所有方法返回空用于测试
internal sealed class StubPackResources : PackResources
{
    public StubPackResources(string packId) : base(packId) { }
    public override Stream? GetRootResource(string path) => null;
    public override Stream? GetResource(PackType type, Identifier location) => null;
    public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output) { }
    public override ISet<string> GetNamespaces(PackType type) => new HashSet<string>();
}
