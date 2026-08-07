using System.Collections.Generic;
using System.Threading.Tasks;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Thread;

namespace NetCraft.Storage;

//实体存储对应原版 net.minecraft.world.level.chunk.storage.EntityStorage
//实现 EntityPersistentStorage 按 chunk 读写实体集合依赖 SimpleRegionStorage
//Entity 序列化策略用 delegate 注入避开具体 Entity 子类依赖
public sealed class EntityStorage : EntityPersistentStorage<Entity>
{
    private const string EntitiesTag = "Entities";

    private readonly SimpleRegionStorage _simpleRegionStorage;
    //emptyChunks 缓存已知的空 chunk 避免重复 IO 读取
    private readonly HashSet<long> _emptyChunks = new();
    //entityDeserializerQueue 实体反序列化队列保证单线程串行
    private readonly ConsecutiveExecutor _entityDeserializerQueue;
    //registryAccess 注册表访问入口用于 TagValueInput 查表
    private readonly RegistryAccess _registryAccess;
    //entityLoader 从 CompoundTag 反序列化 Entity 对应原版 EntityType.loadRecursive
    private readonly Func<CompoundTag, RegistryAccess, Entity> _entityLoader;
    //entitySaver 把 Entity 序列化为 CompoundTag 对应原版 Entity.save
    private readonly Func<Entity, CompoundTag> _entitySaver;

    //构造方法接收 SimpleRegionStorage 与主线程 Executor
    //registryAccess 用于 TagValueInput 查表 entityLoader/entitySaver 注入具体序列化策略
    public EntityStorage(
        SimpleRegionStorage simpleRegionStorage,
        IExecutor mainThreadExecutor,
        RegistryAccess registryAccess,
        Func<CompoundTag, RegistryAccess, Entity> entityLoader,
        Func<Entity, CompoundTag> entitySaver)
    {
        _simpleRegionStorage = simpleRegionStorage;
        _entityDeserializerQueue = new ConsecutiveExecutor(mainThreadExecutor, "entity-deserializer");
        _registryAccess = registryAccess;
        _entityLoader = entityLoader;
        _entitySaver = entitySaver;
    }

    //LoadEntities 按 chunk 位置加载实体集合对应原版 loadEntities
    //走 SimpleRegionStorage.Read + TagValueInput + entityLoader 注入策略
    public async Task<ChunkEntities<Entity>> LoadEntities(ChunkPos pos)
    {
        var packed = pos.Pack();
        if (_emptyChunks.Contains(packed))
            return new ChunkEntities<Entity>(pos, new List<Entity>());

        var tagOptional = await _simpleRegionStorage.Read(pos);
        if (!tagOptional.IsPresent)
        {
            _emptyChunks.Add(packed);
            return new ChunkEntities<Entity>(pos, new List<Entity>());
        }

        var chunkTag = tagOptional.Get();
        //用 TagValueInput 包装注册表入口对应原版 TagValueInput.create
        //实际 entity 解析用注入的 entityLoader 不走 Codec 路径对应原版 EntityType.loadRecursive
        var input = TagValueInput.Create(_registryAccess, chunkTag);
        var entitiesList = input.ChildrenList(EntitiesTag);
        var entities = new List<Entity>();
        if (entitiesList is not null)
        {
            //直接读原始 ListTag 用注入的 entityLoader 解析每个 CompoundTag
            var rawList = chunkTag.GetList(EntitiesTag);
            if (rawList is not null)
                foreach (var t in rawList)
                    if (t is CompoundTag entityTag)
                        entities.Add(_entityLoader(entityTag, _registryAccess));
        }
        return new ChunkEntities<Entity>(pos, entities);
    }

    //StoreEntities 按 chunk 存储实体集合空 chunk 标记 emptyChunks 对应原版 storeEntities
    //走 entitySaver 注入策略 + SimpleRegionStorage.Write
    public void StoreEntities(ChunkEntities<Entity> chunk)
    {
        var pos = chunk.Pos;
        var packed = pos.Pack();
        if (chunk.IsEmpty())
        {
            _emptyChunks.Add(packed);
            _simpleRegionStorage.Write(pos, () => null!);
            return;
        }

        var entitiesList = new ListTag();
        foreach (var entity in chunk.GetEntities())
        {
            var entityTag = _entitySaver(entity);
            entitiesList.Add(entityTag);
        }
        var chunkTag = new CompoundTag();
        chunkTag.Put(EntitiesTag, entitiesList);
        _simpleRegionStorage.Write(pos, chunkTag);
        _emptyChunks.Remove(packed);
    }

    //Flush 同步底层存储并执行 entityDeserializerQueue.runAll
    public async Task Flush(bool flushStorage)
    {
        await _simpleRegionStorage.Synchronize(flushStorage);
        _entityDeserializerQueue.RunAll();
    }

    public void Dispose()
    {
        _simpleRegionStorage.Dispose();
        _entityDeserializerQueue.Close();
    }
}
