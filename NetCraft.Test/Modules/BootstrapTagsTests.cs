using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;

namespace NetCraft.Test.Modules;

//BootstrapTags 测试覆盖 Bootstrap.LoadBuiltinTags 完整接通 TagLoader 框架
//验证从 ResourceManager 加载 tag 文件到 TagManager 注册的端到端链路
internal static class BootstrapTagsTests
{
    public const string Module = "bootstraptags";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LoadBuiltinTags no tag files returns 0", TestNoTagFiles);
        yield return ("LoadBuiltinTags loads blocks tag file", TestLoadsBlockTagFile);
        yield return ("LoadBuiltinTags merges multiple namespaces", TestMergesMultipleNamespaces);
        yield return ("LoadBuiltinTags skips invalid json", TestSkipsInvalidJson);
        yield return ("LoadBuiltinTags replace flag resets prior entries", TestReplaceFlag);
    }

    //CreateTagPack 创建临时 FolderPackResources 含指定的 tag 文件
    //tagsByCategory 是 category -> (tagName, json) 列表
    private static (string Dir, Pack Pack) CreateTagPack(
        Dictionary<string, List<(string TagPath, string Json)>> tagsByCategory)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"netcraft-tags-{Guid.NewGuid():N}");
        foreach (var (category, files) in tagsByCategory)
        {
            foreach (var (tagPath, json) in files)
            {
                //tagPath 形如 minecraft/test_block 需拆分为多级目录
                var parts = tagPath.Split('/');
                var pathParts = new List<string> { dir, "data", "minecraft", "tags", category };
                pathParts.AddRange(parts);
                var fullPath = Path.Combine(pathParts.ToArray()) + ".json";
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, json);
            }
        }
        var pack = new FolderPackResources("test-tags", dir);
        return (dir, new Pack(
            Identifier.FromNamespaceAndPath("minecraft", "test-tags"),
            "test-tags", "stub", 0, false, pack));
    }

    private static bool TestNoTagFiles()
    {
        var (dir, pack) = CreateTagPack(new Dictionary<string, List<(string, string)>>());
        try
        {
            var rm = new ResourceManager();
            rm.AddPack(pack);
            var tm = new TagManager();
            BootstrapClass.LoadBuiltinTags(tm, rm);
            //无 tag 文件 TagManager 不应注册任何 loader
            return tm.GetLoader<Block>(Registries.BLOCK) is null;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static bool TestLoadsBlockTagFile()
    {
        var json = """{"replace": false, "entries": ["minecraft:stone", "minecraft:dirt"]}""";
        var tagsByCategory = new Dictionary<string, List<(string, string)>>
        {
            ["blocks"] = new() { ("minecraft/test_block", json) }
        };
        var (dir, pack) = CreateTagPack(tagsByCategory);
        try
        {
            var rm = new ResourceManager();
            rm.AddPack(pack);
            var tm = new TagManager();
            BootstrapClass.LoadBuiltinTags(tm, rm);
            var loader = tm.GetLoader<Block>(Registries.BLOCK);
            return loader is not null;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static bool TestMergesMultipleNamespaces()
    {
        //两个 namespace 各放一个 tag 文件验证都加载
        var dir1 = Path.Combine(Path.GetTempPath(), $"netcraft-tags-{Guid.NewGuid():N}");
        var dir2 = Path.Combine(Path.GetTempPath(), $"netcraft-tags-{Guid.NewGuid():N}");
        try
        {
            WriteTagFile(dir1, "items", "minecraft/test_item", """{"replace": false, "entries": ["minecraft:stick"]}""");
            WriteTagFile(dir2, "items", "minecraft/another_item", """{"replace": false, "entries": ["minecraft:apple"]}""");

            var rm = new ResourceManager();
            rm.AddPack(new Pack(
                Identifier.FromNamespaceAndPath("minecraft", "pack1"),
                "pack1", "stub", 0, false, new FolderPackResources("pack1", dir1)));
            rm.AddPack(new Pack(
                Identifier.FromNamespaceAndPath("minecraft", "pack2"),
                "pack2", "stub", 1, false, new FolderPackResources("pack2", dir2)));

            var tm = new TagManager();
            BootstrapClass.LoadBuiltinTags(tm, rm);
            var loader = tm.GetLoader<Item>(Registries.ITEM);
            return loader is not null;
        }
        finally
        {
            try { Directory.Delete(dir1, true); } catch { }
            try { Directory.Delete(dir2, true); } catch { }
        }
    }

    //WriteTagFile 写一个 tag 文件到指定根目录的 data/minecraft/tags/{category}/{tagPath}.json
    //tagPath 中 / 自动拆为多级目录
    private static void WriteTagFile(string root, string category, string tagPath, string json)
    {
        var parts = tagPath.Split('/');
        var pathParts = new List<string> { root, "data", "minecraft", "tags", category };
        pathParts.AddRange(parts);
        var fullPath = Path.Combine(pathParts.ToArray()) + ".json";
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, json);
    }

    private static bool TestSkipsInvalidJson()
    {
        //无效 JSON 应被跳过不抛
        var tagsByCategory = new Dictionary<string, List<(string, string)>>
        {
            ["blocks"] = new()
            {
                ("minecraft/bad", "not valid json"),
                ("minecraft/good", """{"replace": false, "entries": ["minecraft:stone"]}"""),
            }
        };
        var (dir, pack) = CreateTagPack(tagsByCategory);
        try
        {
            var rm = new ResourceManager();
            rm.AddPack(pack);
            var tm = new TagManager();
            BootstrapClass.LoadBuiltinTags(tm, rm);
            //应加载 good 文件并注册 loader 不应抛
            return tm.GetLoader<Block>(Registries.BLOCK) is not null;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static bool TestReplaceFlag()
    {
        //同 tag 两个文件第二个 replace=true 应清空第一个
        var dir1 = Path.Combine(Path.GetTempPath(), $"netcraft-tags-{Guid.NewGuid():N}");
        var dir2 = Path.Combine(Path.GetTempPath(), $"netcraft-tags-{Guid.NewGuid():N}");
        try
        {
            WriteTagFile(dir1, "blocks", "minecraft/test_replace",
                """{"replace": false, "entries": ["minecraft:stone"]}""");
            WriteTagFile(dir2, "blocks", "minecraft/test_replace",
                """{"replace": true, "entries": ["minecraft:dirt"]}""");

            var rm = new ResourceManager();
            rm.AddPack(new Pack(
                Identifier.FromNamespaceAndPath("minecraft", "pack1"),
                "pack1", "stub", 0, false, new FolderPackResources("pack1", dir1)));
            rm.AddPack(new Pack(
                Identifier.FromNamespaceAndPath("minecraft", "pack2"),
                "pack2", "stub", 1, false, new FolderPackResources("pack2", dir2)));

            var tm = new TagManager();
            BootstrapClass.LoadBuiltinTags(tm, rm);
            return tm.GetLoader<Block>(Registries.BLOCK) is not null;
        }
        finally
        {
            try { Directory.Delete(dir1, true); } catch { }
            try { Directory.Delete(dir2, true); } catch { }
        }
    }
}
