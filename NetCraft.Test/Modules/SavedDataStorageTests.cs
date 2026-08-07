using System.IO;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Test.Modules;

//SavedDataStorage saveddata存储测试
//覆盖ReadTagFromDisk真实升级路径与SavedData抽象接通路径
internal static class SavedDataStorageTests
{
    public const string Module = "saveddata";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("SavedDataStorage readTagFromDisk gzip", TestReadTagFromDiskGzip);
        yield return ("SavedDataStorage readTagFromDisk uncompressed", TestReadTagFromDiskUncompressed);
        yield return ("SavedDataStorage computeIfAbsent creates new when absent", TestComputeIfAbsentCreatesNew);
        yield return ("SavedDataStorage computeIfAbsent loads from disk", TestComputeIfAbsentLoadsFromDisk);
        yield return ("SavedDataStorage computeIfAbsent cached returns same", TestComputeIfAbsentCached);
        yield return ("SavedDataStorage get returns null when absent", TestGetReturnsNullWhenAbsent);
        yield return ("SavedDataStorage set stores in cache", TestSetStoresInCache);
        yield return ("SavedDataStorage scheduleSave persists dirty", TestScheduleSavePersistsDirty);
        yield return ("SavedDataStorage scheduleSave clears dirty", TestScheduleSaveClearsDirty);
        yield return ("SavedDataStorage saveAndJoin no-op", TestSaveAndJoinNoOp);
        yield return ("SavedDataStorage dispose closes", TestDisposeCloses);
    }

    //TestSavedData 测试用 SavedData 子类记录 name 字段
    private sealed class TestSavedData : SavedData
    {
        public override string Id => "test";
        public string Value { get; }

        public TestSavedData(string value) { Value = value; }

        public override CompoundTag Save(CompoundTag tag)
        {
            tag.PutString("value", Value);
            return tag;
        }
    }

    //TestSavedDataType 测试用 SavedDataType 工厂占位
    private sealed class TestSavedDataType : SavedDataType<TestSavedData>
    {
        public string Id => "test";
        public TestSavedData Create(CompoundTag tag, RegistryAccess registryAccess)
            => new(tag.GetString("value")?.Value ?? string.Empty);
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-saveddata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SavedDataStorage NewStorage(string dir)
        => new(dir, new NoOpDataFixer());

    private static void TryCleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { }
    }

    //构造测试CompoundTag含字段DataVersion缺失时默认1343
    private static CompoundTag MakeTestData()
    {
        var tag = new CompoundTag();
        tag.PutString("name", "test");
        tag.PutInt("value", 42);
        return tag;
    }

    //gzip文件读取走ReadCompressed路径NoOpDataFixer不修改返回原tag引用
    private static bool TestReadTagFromDiskGzip()
    {
        var dir = NewTempDir();
        try
        {
            var dataFile = Path.Combine(dir, "test.dat");
            var originalTag = MakeTestData();
            NbtIo.WriteCompressed(originalTag, dataFile);
            using var storage = NewStorage(dir);
            var loaded = storage.ReadTagFromDisk(dataFile, DataFixTypes.Chunk, 4189);
            //NoOpDataFixer不修改返回原tag内容字段保留
            return loaded.GetString("name")?.Value == "test"
                && loaded.GetInt("value")?.Value == 42;
        }
        finally { TryCleanup(dir); }
    }

    //非压缩文件读取走Read路径
    private static bool TestReadTagFromDiskUncompressed()
    {
        var dir = NewTempDir();
        try
        {
            var dataFile = Path.Combine(dir, "test.dat");
            var originalTag = MakeTestData();
            NbtIo.Write(originalTag, dataFile);
            using var storage = NewStorage(dir);
            var loaded = storage.ReadTagFromDisk(dataFile, DataFixTypes.Chunk, 4189);
            return loaded.GetString("name")?.Value == "test"
                && loaded.GetInt("value")?.Value == 42;
        }
        finally { TryCleanup(dir); }
    }

    //computeIfAbsent 文件不存在时用空 CompoundTag 调 SavedDataType.Create 创建新实例并缓存
    private static bool TestComputeIfAbsentCreatesNew()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var type = new TestSavedDataType();
            var data = storage.ComputeIfAbsent(type);
            return data is not null && data.Value == string.Empty;
        }
        finally { TryCleanup(dir); }
    }

    //computeIfAbsent 文件存在时从磁盘读取并反序列化为已存值
    private static bool TestComputeIfAbsentLoadsFromDisk()
    {
        var dir = NewTempDir();
        try
        {
            //预先写盘一个 test.dat 含 value=hello
            var dataFile = Path.Combine(dir, "test.dat");
            var tag = new CompoundTag();
            tag.PutString("value", "hello");
            NbtIo.WriteCompressed(tag, dataFile);

            using var storage = NewStorage(dir);
            var data = storage.ComputeIfAbsent(new TestSavedDataType());
            return data is not null && data.Value == "hello";
        }
        finally { TryCleanup(dir); }
    }

    //computeIfAbsent 缓存命中返回同一实例不再读盘
    private static bool TestComputeIfAbsentCached()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var type = new TestSavedDataType();
            var first = storage.ComputeIfAbsent(type);
            var second = storage.ComputeIfAbsent(type);
            return ReferenceEquals(first, second);
        }
        finally { TryCleanup(dir); }
    }

    //get 未注册时返回 null
    private static bool TestGetReturnsNullWhenAbsent()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var result = storage.Get(new TestSavedDataType());
            return result == null;
        }
        finally { TryCleanup(dir); }
    }

    //set 缓存 SavedData 后 get 返回该实例且标记 dirty
    private static bool TestSetStoresInCache()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var type = new TestSavedDataType();
            var data = new TestSavedData("hello");
            storage.Set(type, data);
            var got = storage.Get(type);
            return ReferenceEquals(got, data) && data.IsDirty;
        }
        finally { TryCleanup(dir); }
    }

    //scheduleSave 把 dirty 数据写入磁盘
    private static bool TestScheduleSavePersistsDirty()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var type = new TestSavedDataType();
            //构造一个 dirty 实例替换缓存并写盘
            var dirty = new TestSavedData("persisted");
            storage.Set(type, dirty);
            storage.ScheduleSave().Wait();
            //新建 storage 模拟重启读取
            var storage2 = NewStorage(dir);
            var loaded = storage2.ComputeIfAbsent(type);
            storage2.Dispose();
            return loaded is not null && loaded.Value == "persisted";
        }
        finally { TryCleanup(dir); }
    }

    //scheduleSave 写盘后 ClearDirty 标记清除再次 scheduleSave 不再写盘
    private static bool TestScheduleSaveClearsDirty()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var type = new TestSavedDataType();
            var dirty = new TestSavedData("clear-test");
            storage.Set(type, dirty);
            if (!dirty.IsDirty) return false;
            storage.ScheduleSave().Wait();
            //scheduleSave 完成后 dirty 标记应清除
            return !dirty.IsDirty;
        }
        finally { TryCleanup(dir); }
    }

    //saveAndJoin 捕获 NotSupportedException 不抛对齐无 dirty 数据空操作
    private static bool TestSaveAndJoinNoOp()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            storage.SaveAndJoin();
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //dispose 调用 SaveAndJoin 后 closed 标记再次 Dispose 抛 InvalidOperationException
    private static bool TestDisposeCloses()
    {
        var dir = NewTempDir();
        try
        {
            var storage = NewStorage(dir);
            storage.Dispose();
            try
            {
                storage.Dispose();
                return false;
            }
            catch (InvalidOperationException) { return true; }
        }
        finally { TryCleanup(dir); }
    }
}
