using System.IO;
using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Schemas;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Test.Modules;

//SimpleRegionStorage通用区域存储测试
//覆盖委托IOWorker的read/write/synchronize/chunkScanner/storageInfo路径
//以及DataFixer接通后的upgradeChunkTag真实路径与ReportedException错误包装
internal static class SimpleRegionStorageTests
{
    public const string Module = "simpleregion";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("SimpleRegionStorage read/write round-trip", TestRoundTrip);
        yield return ("SimpleRegionStorage write supplier", TestWriteSupplier);
        yield return ("SimpleRegionStorage synchronize flush", TestSynchronize);
        yield return ("SimpleRegionStorage chunkScanner returns worker", TestChunkScanner);
        yield return ("SimpleRegionStorage storageInfo passthrough", TestStorageInfo);
        yield return ("SimpleRegionStorage dispose idempotent", TestDisposeIdempotent);
        yield return ("SimpleRegionStorage upgradeChunkTag no-op adds DataVersion", TestUpgradeChunkTagNoOpAddsDataVersion);
        yield return ("SimpleRegionStorage upgradeChunkTag same version returns original", TestUpgradeChunkTagSameVersionReturnsOriginal);
        yield return ("SimpleRegionStorage upgradeChunkTag fixer throws wraps as Reported", TestUpgradeChunkTagThrowsWrapsAsReported);
        yield return ("SimpleRegionStorage injectDatafixingContext", TestInjectDatafixingContext);
        yield return ("SimpleRegionStorage isOldChunkAround old chunk", TestIsOldChunkAroundOld);
        yield return ("SimpleRegionStorage write merge same chunk", TestWriteMerge);
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-simpleregion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static RegionStorageInfo NewInfo() => new("test", null!, "chunk");

    //NoOpDataFixer构造最小可用SimpleRegionStorage用CHUNK类型对应原版
    private static SimpleRegionStorage NewStorage(string dir)
        => new(NewInfo(), dir, new NoOpDataFixer(), syncWrites: false, DataFixTypes.Chunk);

    private static CompoundTag MakeChunk(int level, string name)
    {
        var tag = new CompoundTag();
        tag.Put("Level", new IntTag(level));
        tag.Put("Name", new StringTag(name));
        return tag;
    }

    private static bool AssertOptional(Optional<CompoundTag> opt, int level, string name)
        => opt.IsPresent && opt.Get().GetInt("Level")?.Value == level && opt.Get().GetString("Name")?.Value == name;

    private static void TryCleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { }
    }

    private static bool TestRoundTrip()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var pos = new ChunkPos(2, -3);
            storage.Write(pos, MakeChunk(42, "hello")).Wait();
            storage.Synchronize(true).Wait();
            var loaded = storage.Read(pos).Result;
            return AssertOptional(loaded, 42, "hello");
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestWriteSupplier()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var pos = new ChunkPos(1, 1);
            var counter = 0;
            storage.Write(pos, () => { counter++; return MakeChunk(7, "supplier"); }).Wait();
            storage.Synchronize(true).Wait();
            return counter == 1 && AssertOptional(storage.Read(pos).Result, 7, "supplier");
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestSynchronize()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var positions = new[] { new ChunkPos(0, 0), new ChunkPos(1, 0), new ChunkPos(0, 1) };
            foreach (var (p, i) in positions.Select((p, i) => (p, i)))
                storage.Write(p, MakeChunk(i, $"chunk{i}"));
            storage.Synchronize(true).Wait();
            foreach (var (p, i) in positions.Select((p, i) => (p, i)))
                if (!AssertOptional(storage.Read(p).Result, i, $"chunk{i}")) return false;
            return true;
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestChunkScanner()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var scanner = storage.ChunkScanner();
            return scanner is not null;
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestStorageInfo()
    {
        var dir = NewTempDir();
        try
        {
            var expected = NewInfo();
            using var storage = new SimpleRegionStorage(expected, dir, new NoOpDataFixer(), syncWrites: false, DataFixTypes.Chunk);
            var info = storage.StorageInfo();
            return ReferenceEquals(expected, info) || info.Equals(expected);
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestDisposeIdempotent()
    {
        var dir = NewTempDir();
        try
        {
            var storage = NewStorage(dir);
            storage.Dispose();
            storage.Dispose();
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //NoOpDataFixer路径接通版本相同时原版直接返回tag不修改
    //版本不同时NoOp不修改内容但路径会走完RemoveDatafixingContext+AddDataVersion
    //验证升级后tag有DataVersion字段且原字段保留
    private static bool TestUpgradeChunkTagNoOpAddsDataVersion()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var chunkTag = MakeChunk(5, "test");
            var upgraded = storage.UpgradeChunkTag(chunkTag, 0);
            return upgraded.Contains(SharedConstants.DataVersionTag)
                && upgraded.GetIntValue(SharedConstants.DataVersionTag) == SharedConstants.WorldDataVersion
                && upgraded.GetInt("Level")?.Value == 5
                && upgraded.GetString("Name")?.Value == "test";
        }
        finally { TryCleanup(dir); }
    }

    //version>=targetVersion直接返回原tag引用
    private static bool TestUpgradeChunkTagSameVersionReturnsOriginal()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var chunkTag = new CompoundTag();
            NbtUtils.AddDataVersion(chunkTag, SharedConstants.WorldDataVersion);
            var upgraded = storage.UpgradeChunkTag(chunkTag, 0);
            return ReferenceEquals(chunkTag, upgraded);
        }
        finally { TryCleanup(dir); }
    }

    //DataFixer抛异常时包装为ReportedException标题Updated chunk
    private sealed class ThrowingDataFixer : NetCraft.DataFixer.DataFixer
    {
        public Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion)
            => throw new InvalidOperationException("fixer boom");
        public Schema GetSchema(int key) => throw new NotSupportedException();
    }

    private static bool TestUpgradeChunkTagThrowsWrapsAsReported()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = new SimpleRegionStorage(NewInfo(), dir, new ThrowingDataFixer(), syncWrites: false, DataFixTypes.Chunk);
            try
            {
                storage.UpgradeChunkTag(new CompoundTag(), 0);
                return false;
            }
            catch (ReportedException ex)
            {
                return ex.Report.Title == "Updated chunk";
            }
        }
        finally { TryCleanup(dir); }
    }

    //注入DataFixer上下文tag到chunkTag对应原版injectDatafixingContext
    private static bool TestInjectDatafixingContext()
    {
        var chunkTag = new CompoundTag();
        var contextTag = new CompoundTag();
        contextTag.PutString("dimension", "minecraft:overworld");
        SimpleRegionStorage.InjectDatafixingContext(chunkTag, contextTag);
        if (!chunkTag.Contains(SimpleRegionStorage.DatafixerContextTag)) return false;
        var before = chunkTag.GetCompound(SimpleRegionStorage.DatafixerContextTag);
        if (before?.GetStringValue("dimension") != "minecraft:overworld") return false;
        //传null不修改
        var chunkTag2 = new CompoundTag();
        SimpleRegionStorage.InjectDatafixingContext(chunkTag2, null);
        return !chunkTag2.Contains(SimpleRegionStorage.DatafixerContextTag);
    }

    //isOldChunkAround委托IOWorker扫描旧区块
    private static bool TestIsOldChunkAroundOld()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var pos = new ChunkPos(0, 0);
            var chunk = new CompoundTag();
            chunk.PutInt("DataVersion", 4881);
            storage.Write(pos, chunk).Wait();
            storage.Synchronize(true).Wait();
            return storage.IsOldChunkAround(pos, 0);
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestWriteMerge()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var pos = new ChunkPos(5, 5);
            storage.Write(pos, MakeChunk(1, "a")).Wait();
            storage.Write(pos, MakeChunk(2, "b")).Wait();
            storage.Write(pos, MakeChunk(3, "c")).Wait();
            storage.Synchronize(true).Wait();
            return AssertOptional(storage.Read(pos).Result, 3, "c");
        }
        finally { TryCleanup(dir); }
    }
}
