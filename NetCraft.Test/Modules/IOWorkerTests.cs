using System.IO;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Test.Modules;

//IOWorker异步IO调度测试
//覆盖store/loadAsync round-trip/合并写入/pending命中/synchronize/scanChunk/close/blending stub
internal static class IOWorkerTests
{
    public const string Module = "ioworker";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("IOWorker store/loadAsync round-trip", TestRoundTrip);
        yield return ("IOWorker store merge same chunk", TestStoreMerge);
        yield return ("IOWorker loadAsync hits pending", TestLoadHitsPending);
        yield return ("IOWorker synchronize flush", TestSynchronizeFlush);
        yield return ("IOWorker synchronize no flush", TestSynchronizeNoFlush);
        yield return ("IOWorker scanChunk from pending", TestScanChunkFromPending);
        yield return ("IOWorker scanChunk from disk", TestScanChunkFromDisk);
        yield return ("IOWorker close idempotent", TestCloseIdempotent);
        yield return ("IOWorker isOldChunkAround old chunk", TestIsOldChunkAroundOld);
        yield return ("IOWorker isOldChunkAround new chunk", TestIsOldChunkAroundNew);
        yield return ("IOWorker isOldChunkAround with blending_data", TestIsOldChunkAroundBlending);
        yield return ("IOWorker store null clears chunk", TestStoreNullClears);
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-ioworker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static RegionStorageInfo NewInfo() => new("test", null!, "chunk");

    private static CompoundTag MakeChunk(int level, string name)
    {
        var tag = new CompoundTag();
        tag.Put("Level", new IntTag(level));
        tag.Put("Name", new StringTag(name));
        return tag;
    }

    //构造带DataVersion字段的chunk用于blending扫描测试
    private static CompoundTag MakeChunkWithVersion(int dataVersion, string name)
    {
        var tag = new CompoundTag();
        tag.PutInt("DataVersion", dataVersion);
        tag.Put("Name", new StringTag(name));
        return tag;
    }

    private static bool AssertTag(CompoundTag? tag, int level, string name)
        => tag != null
            && tag.GetInt("Level")?.Value == level
            && tag.GetString("Name")?.Value == name;

    private static bool AssertOptional(Optional<CompoundTag> opt, int level, string name)
        => opt.IsPresent && AssertTag(opt.Get(), level, name);

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
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(3, -5);
            worker.Store(pos, MakeChunk(42, "hello")).Wait();
            worker.Synchronize(true).Wait();
            var loaded = worker.LoadAsync(pos).Result;
            return AssertOptional(loaded, 42, "hello");
        }
        finally { TryCleanup(dir); }
    }

    //同一chunk多次store合并为最后一次值
    //LoadAsync返回最后写入的值不读盘
    private static bool TestStoreMerge()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(1, 1);
            worker.Store(pos, MakeChunk(1, "a")).Wait();
            worker.Store(pos, MakeChunk(2, "b")).Wait();
            worker.Store(pos, MakeChunk(3, "c")).Wait();
            var loaded = worker.LoadAsync(pos).Result;
            if (!AssertOptional(loaded, 3, "c")) return false;
            worker.Synchronize(true).Wait();
            var fromDisk = worker.LoadAsync(pos).Result;
            return AssertOptional(fromDisk, 3, "c");
        }
        finally { TryCleanup(dir); }
    }

    //store后立即loadAsync应命中pending不读盘
    //用sync=true保证串行执行便于断言
    private static bool TestLoadHitsPending()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: true);
            var pos = new ChunkPos(2, 2);
            var storeTask = worker.Store(pos, MakeChunk(99, "pending"));
            var loadTask = worker.LoadAsync(pos);
            Task.WaitAll(storeTask, loadTask);
            return AssertOptional(loadTask.Result, 99, "pending");
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestSynchronizeFlush()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var positions = new[]
            {
                new ChunkPos(0, 0),
                new ChunkPos(1, 0),
                new ChunkPos(0, 1),
                new ChunkPos(32, 32)
            };
            foreach (var (p, i) in positions.Select((p, i) => (p, i)))
                worker.Store(p, MakeChunk(i, $"chunk{i}"));
            worker.Synchronize(true).Wait();
            foreach (var (p, i) in positions.Select((p, i) => (p, i)))
            {
                if (!AssertOptional(worker.LoadAsync(p).Result, i, $"chunk{i}"))
                    return false;
            }
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //Synchronize(false)只等pending完成不flush盘
    //验证不抛异常且后续LoadAsync能拿到数据
    private static bool TestSynchronizeNoFlush()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(5, 5);
            worker.Store(pos, MakeChunk(7, "noflush")).Wait();
            worker.Synchronize(false).Wait();
            return AssertOptional(worker.LoadAsync(pos).Result, 7, "noflush");
        }
        finally { TryCleanup(dir); }
    }

    private sealed class CountingVisitor : StreamTagVisitorBase
    {
        public int VisitCount;
        public override StreamTagVisitor.ValueResult VisitRootEntry(TagType type)
        {
            VisitCount++;
            return StreamTagVisitor.ValueResult.Continue;
        }
        public override StreamTagVisitor.EntryResult VisitEntry(TagType type, string name)
        {
            VisitCount++;
            return StreamTagVisitor.EntryResult.Enter;
        }
        public override StreamTagVisitor.ValueResult VisitInt(int value)
        {
            VisitCount++;
            return StreamTagVisitor.ValueResult.Continue;
        }
        public override StreamTagVisitor.ValueResult VisitString(string value)
        {
            VisitCount++;
            return StreamTagVisitor.ValueResult.Continue;
        }
    }

    private static bool TestScanChunkFromPending()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: true);
            var pos = new ChunkPos(0, 0);
            worker.Store(pos, MakeChunk(10, "scan")).Wait();
            var visitor = new CountingVisitor();
            worker.ScanChunk(pos, visitor).Wait();
            return visitor.VisitCount > 0;
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestScanChunkFromDisk()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(3, 3);
            worker.Store(pos, MakeChunk(20, "disk")).Wait();
            worker.Synchronize(true).Wait();
            var visitor = new CountingVisitor();
            worker.ScanChunk(pos, visitor).Wait();
            return visitor.VisitCount > 0;
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestCloseIdempotent()
    {
        var dir = NewTempDir();
        try
        {
            var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(1, 1);
            worker.Store(pos, MakeChunk(1, "close")).Wait();
            worker.Dispose();
            worker.Dispose();
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //旧区块判定DataVersion缺失或低于4882返回true
    private static bool TestIsOldChunkAroundOld()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(0, 0);
            worker.Store(pos, MakeChunkWithVersion(4881, "old")).Wait();
            worker.Synchronize(true).Wait();
            return worker.IsOldChunkAround(pos, 0);
        }
        finally { TryCleanup(dir); }
    }

    //新区块DataVersion高于4882且无blending_data返回false
    private static bool TestIsOldChunkAroundNew()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(0, 0);
            worker.Store(pos, MakeChunkWithVersion(4999, "new")).Wait();
            worker.Synchronize(true).Wait();
            return !worker.IsOldChunkAround(pos, 0);
        }
        finally { TryCleanup(dir); }
    }

    //新区块但带blending_data字段仍判定为旧区块
    private static bool TestIsOldChunkAroundBlending()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(0, 0);
            var chunk = MakeChunkWithVersion(4999, "blended");
            chunk.Put("blending_data", new CompoundTag());
            worker.Store(pos, chunk).Wait();
            worker.Synchronize(true).Wait();
            return worker.IsOldChunkAround(pos, 0);
        }
        finally { TryCleanup(dir); }
    }

    //store null清除chunk数据
    //原版PendingStore.data=null表示删除Clear chunk
    private static bool TestStoreNullClears()
    {
        var dir = NewTempDir();
        try
        {
            using var worker = new IOWorker(NewInfo(), dir, sync: false);
            var pos = new ChunkPos(7, 7);
            worker.Store(pos, MakeChunk(1, "first")).Wait();
            worker.Synchronize(true).Wait();
            worker.Store(pos, () => null!).Wait();
            worker.Synchronize(true).Wait();
            var loaded = worker.LoadAsync(pos).Result;
            return !loaded.IsPresent;
        }
        finally { TryCleanup(dir); }
    }
}
