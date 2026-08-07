using NetCraft.DataFixer;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Test.Modules;

//LevelStorage 测试覆盖世界存储入口和维度访问
internal static class LevelStorageTests
{
    public const string Module = "levelstorage";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LevelStorage CreateAccess creates directory", TestCreateAccess);
        yield return ("LevelStorage ListWorlds returns all", TestListWorlds);
        yield return ("LevelStorage WorldExists", TestWorldExists);
        yield return ("LevelStorage DeleteWorld removes", TestDeleteWorld);
        yield return ("LevelStorageAccess overworld path is worldDir", TestOverworldPath);
        yield return ("LevelStorageAccess nether path uses dim_ prefix", TestNetherPath);
        yield return ("LevelStorageAccess end path", TestEndPath);
        yield return ("LevelStorageAccess CreateRegionStorage returns instance", TestCreateRegionStorage);
        yield return ("LevelStorageAccess caches RegionStorage per dimension", TestCachesRegionStorage);
        yield return ("LevelStorageAccess GetExistingRegionStorage null for uncreated", TestGetExistingNull);
        yield return ("LevelStorageAccess LevelDataPath correct", TestLevelDataPath);
        yield return ("LevelKeys overworld identifier", TestLevelKeysOverworld);
        yield return ("LevelKeys nether identifier", TestLevelKeysNether);
        yield return ("LevelKeys end identifier", TestLevelKeysEnd);
    }

    private static string NewBaseDir()
        => Path.Combine(Path.GetTempPath(), $"netcraft-ls-{Guid.NewGuid():N}");

    private static bool TestCreateAccess()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("myworld", acquireLock: false);
            return Directory.Exists(Path.Combine(baseDir, "myworld"))
                && access.WorldName == "myworld"
                && access.WorldDir == Path.Combine(baseDir, "myworld");
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestListWorlds()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using (ls.CreateAccess("world1", acquireLock: false))
            using (ls.CreateAccess("world2", acquireLock: false))
            using (ls.CreateAccess("world3", acquireLock: false))
            {
                var worlds = ls.ListWorlds().OrderBy(w => w).ToList();
                return worlds.Count == 3
                    && worlds[0] == "world1"
                    && worlds[1] == "world2"
                    && worlds[2] == "world3";
            }
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestWorldExists()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using (ls.CreateAccess("alpha", acquireLock: false))
            {
                return ls.WorldExists("alpha") && !ls.WorldExists("beta");
            }
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestDeleteWorld()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using (ls.CreateAccess("todelete", acquireLock: false))
            {
                if (!ls.WorldExists("todelete")) return false;
            }
            ls.DeleteWorld("todelete");
            return !ls.WorldExists("todelete");
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestOverworldPath()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("overworld_test", acquireLock: false);
            var path = access.GetDimensionPath(LevelKeys.OVERWORLD);
            return path == access.WorldDir;
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestNetherPath()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("nether_test", acquireLock: false);
            var path = access.GetDimensionPath(LevelKeys.NETHER);
            return path == Path.Combine(access.WorldDir, "dim_the_nether");
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestEndPath()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("end_test", acquireLock: false);
            var path = access.GetDimensionPath(LevelKeys.END);
            return path == Path.Combine(access.WorldDir, "dim_the_end");
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestCreateRegionStorage()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("rs_test", acquireLock: false);
            var fixer = new DataFixerBuilder(0).Build().Fixer();
            var storage = access.CreateRegionStorage(LevelKeys.OVERWORLD, fixer, DataFixTypes.Chunk);
            return storage is not null
                && Directory.Exists(access.GetRegionPath(LevelKeys.OVERWORLD));
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestCachesRegionStorage()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("cache_test", acquireLock: false);
            var fixer = new DataFixerBuilder(0).Build().Fixer();
            var s1 = access.CreateRegionStorage(LevelKeys.OVERWORLD, fixer, DataFixTypes.Chunk);
            var s2 = access.CreateRegionStorage(LevelKeys.OVERWORLD, fixer, DataFixTypes.Chunk);
            return ReferenceEquals(s1, s2);
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestGetExistingNull()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("existing_test", acquireLock: false);
            return access.GetExistingRegionStorage(LevelKeys.OVERWORLD) is null;
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestLevelDataPath()
    {
        var baseDir = NewBaseDir();
        try
        {
            var ls = new LevelStorage(baseDir);
            using var access = ls.CreateAccess("leveldata_test", acquireLock: false);
            return access.LevelDataPath == Path.Combine(access.WorldDir, "level.dat")
                && access.LevelDataOldPath == Path.Combine(access.WorldDir, "level.dat_old");
        }
        finally { try { Directory.Delete(baseDir, true); } catch { } }
    }

    private static bool TestLevelKeysOverworld()
        => LevelKeys.OVERWORLD.Identifier.ToString() == "minecraft:overworld";

    private static bool TestLevelKeysNether()
        => LevelKeys.NETHER.Identifier.ToString() == "minecraft:the_nether";

    private static bool TestLevelKeysEnd()
        => LevelKeys.END.Identifier.ToString() == "minecraft:the_end";
}
