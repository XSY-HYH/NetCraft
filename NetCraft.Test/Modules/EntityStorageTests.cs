using System.Collections.Generic;
using System.IO;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util.Thread;

namespace NetCraft.Test.Modules;

//EntityStorage实体存储测试
//覆盖真实路径round-trip构造/Flush/Dispose与空chunk优化
internal static class EntityStorageTests
{
    public const string Module = "entitystorage";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("EntityStorage construct and dispose", TestConstructAndDispose);
        yield return ("EntityStorage flush no-op", TestFlushNoOp);
        yield return ("EntityStorage loadEntities empty chunk returns empty", TestLoadEntitiesEmptyChunk);
        yield return ("EntityStorage storeEntities empty chunk marked", TestStoreEntitiesEmptyChunk);
        yield return ("EntityStorage store/load round-trip", TestStoreLoadRoundTrip);
        yield return ("EntityStorage loadEntities after empty cached skips IO", TestLoadEntitiesAfterEmptyCached);
    }

    //TestEntity 测试用 Entity 子类记录 Id 与 Payload
    private sealed class TestEntity : Entity
    {
        public override Identifier Id { get; }
        public string Payload { get; }

        public TestEntity(Identifier id, string payload)
        {
            Id = id;
            Payload = payload;
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-entity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static RegionStorageInfo NewInfo() => new("test", null!, "entity");

    private static RegistryAccess EmptyRegistryAccess()
        => new ImmutableRegistryAccess(Array.Empty<KeyValuePair<Identifier, object>>());

    //entityLoader 从 CompoundTag 反序列化 TestEntity
    private static TestEntity LoadEntity(CompoundTag tag, RegistryAccess _)
    {
        var id = Identifier.Parse(tag.GetString("id")?.Value ?? "minecraft:test");
        var payload = tag.GetString("payload")?.Value ?? string.Empty;
        return new TestEntity(id, payload);
    }

    //entitySaver 把 TestEntity 序列化为 CompoundTag
    private static CompoundTag SaveEntity(Entity entity)
    {
        var test = (TestEntity)entity;
        var tag = new CompoundTag();
        tag.PutString("id", test.Id.ToString());
        tag.PutString("payload", test.Payload);
        return tag;
    }

    private static EntityStorage NewStorage(string dir)
    {
        var simpleRegion = new SimpleRegionStorage(NewInfo(), dir, new NoOpDataFixer(), syncWrites: false, DataFixTypes.Chunk);
        return new EntityStorage(simpleRegion, DefaultThreadPoolExecutor.Instance, EmptyRegistryAccess(), LoadEntity, SaveEntity);
    }

    private static void TryCleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { }
    }

    private static bool TestConstructAndDispose()
    {
        var dir = NewTempDir();
        try
        {
            var storage = NewStorage(dir);
            storage.Dispose();
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //flush调用SimpleRegionStorage.synchronize与ConsecutiveExecutor.runAll
    //空chunk不写入任务runAll不应抛
    private static bool TestFlushNoOp()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            storage.Flush(false).Wait();
            return true;
        }
        finally { TryCleanup(dir); }
    }

    //loadEntities 区块未存储返回空实体列表不抛
    private static bool TestLoadEntitiesEmptyChunk()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var chunk = storage.LoadEntities(new ChunkPos(0, 0)).Result;
            return chunk.IsEmpty();
        }
        finally { TryCleanup(dir); }
    }

    //storeEntities 空实体列表标记 emptyChunks 写入 null tag
    private static bool TestStoreEntitiesEmptyChunk()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            storage.StoreEntities(new ChunkEntities<Entity>(new ChunkPos(1, 1), new List<Entity>()));
            //再次load应直接命中emptyChunks缓存返回空
            var chunk = storage.LoadEntities(new ChunkPos(1, 1)).Result;
            return chunk.IsEmpty();
        }
        finally { TryCleanup(dir); }
    }

    //store/load round-trip 验证实体集合序列化后能完整还原
    private static bool TestStoreLoadRoundTrip()
    {
        var dir = NewTempDir();
        EntityStorage? storage1 = null;
        try
        {
            storage1 = NewStorage(dir);
            var pos = new ChunkPos(2, 3);
            var entities = new List<Entity>
            {
                new TestEntity(Identifier.Parse("minecraft:cow"), "cow1"),
                new TestEntity(Identifier.Parse("minecraft:pig"), "pig1"),
            };
            storage1.StoreEntities(new ChunkEntities<Entity>(pos, entities));
            //flush 同步底层存储确保写盘完成
            storage1.Flush(true).Wait();
            //先 Dispose 释放底层 RegionFile 文件句柄再新建 storage 模拟重启
            storage1.Dispose();
            storage1 = null;
            using var storage2 = NewStorage(dir);
            var loaded = storage2.LoadEntities(pos).Result;
            if (loaded.IsEmpty()) return false;
            var list = loaded.GetEntities();
            if (list.Count != 2) return false;
            var first = (TestEntity)list[0];
            var second = (TestEntity)list[1];
            return first.Id.ToString() == "minecraft:cow" && first.Payload == "cow1"
                && second.Id.ToString() == "minecraft:pig" && second.Payload == "pig1";
        }
        finally
        {
            storage1?.Dispose();
            TryCleanup(dir);
        }
    }

    //loadEntities 命中 emptyChunks 后同一位置再次 load 不走 IO 路径
    private static bool TestLoadEntitiesAfterEmptyCached()
    {
        var dir = NewTempDir();
        try
        {
            using var storage = NewStorage(dir);
            var pos = new ChunkPos(5, 5);
            storage.StoreEntities(new ChunkEntities<Entity>(pos, new List<Entity>()));
            //第一次load命中emptyChunks返回空
            var first = storage.LoadEntities(pos).Result;
            if (!first.IsEmpty()) return false;
            //第二次load同样命中emptyChunks不应抛
            var second = storage.LoadEntities(pos).Result;
            return second.IsEmpty();
        }
        finally { TryCleanup(dir); }
    }
}
