using System.IO.Compression;
using System.Text;
using NetCraft.Game;

namespace NetCraft.Test.Modules;

//AssetsExtractor 测试覆盖 jar 提取 assets/ data/ pack.mcmeta 三类条目
//通过临时构造 zip 文件模拟 jar 验证提取后目录结构与跳过 class/META-INF 等无关条目
internal static class AssetsExtractorTests
{
    public const string Module = "assetsextractor";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("TryExtractJar extracts assets and data and pack.mcmeta", TestExtractsAllThree);
        yield return ("TryExtractJar skips class and META-INF entries", TestSkipsClassAndMetaInf);
        yield return ("TryExtractJar dataDir null skips data entries", TestDataDirNullSkipsData);
        yield return ("TryExtractJar rootDir null falls back to assetsDir for pack.mcmeta", TestRootDirNullFallsBack);
        yield return ("TryExtractJar missing jar returns false", TestMissingJarReturnsFalse);
    }

    //TestExtractsAllThree 验证 assets/ data/ pack.mcmeta 都被正确提取到目标目录
    private static bool TestExtractsAllThree()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "netcraft-extract-" + Guid.NewGuid().ToString("N"));
        var jarPath = Path.Combine(tmp, "test.jar");
        var assetsDir = Path.Combine(tmp, "assets");
        var dataDir = Path.Combine(tmp, "data");
        var rootDir = tmp;
        try
        {
            Directory.CreateDirectory(tmp);
            CreateTestJar(jarPath);
            var ok = AssetsExtractor.TryExtractJar(jarPath, assetsDir, dataDir, rootDir);
            if (!ok) return false;
            //assets/minecraft/textures/stone.png 存在
            var assetFile = Path.Combine(assetsDir, "minecraft", "textures", "stone.png");
            if (!File.Exists(assetFile)) return false;
            //data/minecraft/tags/blocks/test.json 存在
            var dataFile = Path.Combine(dataDir, "minecraft", "tags", "blocks", "test.json");
            if (!File.Exists(dataFile)) return false;
            //pack.mcmeta 在 rootDir 与 assets/data 同级
            var mcmeta = Path.Combine(rootDir, "pack.mcmeta");
            if (!File.Exists(mcmeta)) return false;
            return true;
        }
        finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
    }

    //TestSkipsClassAndMetaInf 验证 .class 与 META-INF 条目不被提取
    private static bool TestSkipsClassAndMetaInf()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "netcraft-extract-" + Guid.NewGuid().ToString("N"));
        var jarPath = Path.Combine(tmp, "test.jar");
        var assetsDir = Path.Combine(tmp, "assets");
        var dataDir = Path.Combine(tmp, "data");
        var rootDir = tmp;
        try
        {
            Directory.CreateDirectory(tmp);
            using (var archive = ZipFile.Open(jarPath, ZipArchiveMode.Create))
            {
                AddEntry(archive, "com/example/Example.class", "class bytes");
                AddEntry(archive, "META-INF/MANIFEST.MF", "manifest");
                AddEntry(archive, "assets/minecraft/textures/stone.png", "png bytes");
            }
            var ok = AssetsExtractor.TryExtractJar(jarPath, assetsDir, dataDir, rootDir);
            if (!ok) return false;
            //assets 资源存在
            if (!File.Exists(Path.Combine(assetsDir, "minecraft", "textures", "stone.png"))) return false;
            //class 与 META-INF 没被提取到 assets 或 data
            if (File.Exists(Path.Combine(assetsDir, "com", "example", "Example.class"))) return false;
            if (File.Exists(Path.Combine(dataDir, "META-INF", "MANIFEST.MF"))) return false;
            return true;
        }
        finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
    }

    //TestDataDirNullSkipsData 验证 dataDir=null 时 data/ 条目被跳过
    private static bool TestDataDirNullSkipsData()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "netcraft-extract-" + Guid.NewGuid().ToString("N"));
        var jarPath = Path.Combine(tmp, "test.jar");
        var assetsDir = Path.Combine(tmp, "assets");
        var rootDir = tmp;
        try
        {
            Directory.CreateDirectory(tmp);
            CreateTestJar(jarPath);
            var ok = AssetsExtractor.TryExtractJar(jarPath, assetsDir, null, rootDir);
            if (!ok) return false;
            //assets 资源存在
            if (!File.Exists(Path.Combine(assetsDir, "minecraft", "textures", "stone.png"))) return false;
            //data 目录不存在因 dataDir=null 跳过
            if (Directory.Exists(Path.Combine(tmp, "data"))) return false;
            return true;
        }
        finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
    }

    //TestRootDirNullFallsBack 验证 rootDir=null 时 pack.mcmeta 落到 assetsDir 兼容旧行为
    private static bool TestRootDirNullFallsBack()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "netcraft-extract-" + Guid.NewGuid().ToString("N"));
        var jarPath = Path.Combine(tmp, "test.jar");
        var assetsDir = Path.Combine(tmp, "assets");
        var dataDir = Path.Combine(tmp, "data");
        try
        {
            Directory.CreateDirectory(tmp);
            CreateTestJar(jarPath);
            var ok = AssetsExtractor.TryExtractJar(jarPath, assetsDir, dataDir, null);
            if (!ok) return false;
            //pack.mcmeta 在 assetsDir 下兼容旧签名行为
            var mcmeta = Path.Combine(assetsDir, "pack.mcmeta");
            return File.Exists(mcmeta);
        }
        finally { try { Directory.Delete(tmp, recursive: true); } catch { } }
    }

    //TestMissingJarReturnsFalse jar 不存在返回 false 不抛
    private static bool TestMissingJarReturnsFalse()
    {
        return AssetsExtractor.TryExtractJar("nonexistent.jar", "dummy", "dummy", "dummy") == false;
    }

    //CreateTestJar 构造包含 assets data pack.mcmeta 三类条目的测试 jar
    private static void CreateTestJar(string jarPath)
    {
        using var archive = ZipFile.Open(jarPath, ZipArchiveMode.Create);
        AddEntry(archive, "assets/minecraft/textures/stone.png", "png bytes");
        AddEntry(archive, "data/minecraft/tags/blocks/test.json", "{\"replace\":false,\"entries\":[]}");
        AddEntry(archive, "pack.mcmeta", "{\"pack\":{\"pack_format\":15,\"description\":\"test\"}}");
    }

    //AddEntry 向 zip 添加一个文本条目
    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
