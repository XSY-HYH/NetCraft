using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Storage;

//PersistentServerLevel 持久化服务端关卡对应原版 ServerLevel 接入 RegionFileStorage
//继承 SimpleServerLevel 复用 in-memory 字典做缓存实现 LevelHeightAccessor 供 Parse 用
//GetChunk 未命中时委托 ServerChunkCache 异步调度避免主循环同步等待
//SaveChunk 把 ChunkAccess 序列化为 CompoundTag 写入 RegionFileStorage
//阶段 11.47 接入 Tick 与 Entity 集合对齐原版 ServerLevel.tick 调度骨架
//阶段 11.48 接入 ServerChunkCache 替代同步等待 LoadChunkAsync
public sealed class PersistentServerLevel : SimpleServerLevel, LevelHeightAccessor
{
    private readonly SimpleRegionStorage _regionStorage;
    private readonly PalettedContainerFactory _factory;
    private readonly ServerChunkCache _chunkSource;
    //EntityLookup 按 SectionPos 分桶索引替代 List<Entity> 线性扫描
    private readonly EntityLookup _entities = new();
    private long _levelTick;

    public int MinSectionY { get; }
    public int SectionsCount { get; }
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    public SimpleRegionStorage RegionStorage => _regionStorage;
    public PalettedContainerFactory Factory => _factory;

    //ChunkSource 区块源 ServerChunkCache 供外部诊断与玩家位置更新
    public ServerChunkCache ChunkSource => _chunkSource;

    //Entities 关卡内实体列表只读视图供外部诊断
    public IEnumerable<NetCraft.Registry.Entity> Entities => _entities.GetAll();

    //EntityLookup 分区索引查询入口供业务层按 AABB 范围取实体
    public EntityLookup EntityLookup => _entities;

    //LevelTick 累计关卡 tick 数用于诊断与刷盘调度
    public long LevelTick => _levelTick;

    public PersistentServerLevel(
        SimpleRegionStorage regionStorage,
        int minSectionY = -4,
        int sectionsCount = 24,
        PalettedContainerFactory? factory = null,
        Identifier? dimension = null,
        int dataVersion = 0,
        RegistryAccess? registryAccess = null,
        int viewDistance = 8,
        Func<ChunkPos, ChunkAccess?>? generator = null)
        : base(dimension, dataVersion, registryAccess)
    {
        _regionStorage = regionStorage;
        _factory = factory ?? PalettedContainerFactory.Default;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        //loader 委托 LoadChunkAsync 由 ServerChunkCache 异步调度避免循环依赖
        //async lambda 让 Task<LevelChunk?> 隐式转 Task<ChunkAccess?> 因 Task 不支持协变
        //generator 由 Game 层传入走 ChunkStatusProcessor 生成链存档未命中时生成新 chunk
        _chunkSource = new ServerChunkCache(async pos => await LoadChunkAsync(pos), viewDistance, generator);
    }

    //LoadChunkAsync 从 RegionFileStorage 异步加载并反序列化对应原版 chunk load 路径
    //未命中返回 null 反序列化失败抛 ChunkReadException
    //ServerChunkCache 通过 loader 回调调用此方法
    public async Task<LevelChunk?> LoadChunkAsync(ChunkPos pos)
    {
        Log.Debug($"LoadChunkAsync 入口 pos={pos}");
        var optional = await _regionStorage.Read(pos);
        if (!optional.IsPresent)
        {
            Log.Debug($"LoadChunkAsync 出口 result=null 存档未命中");
            return null;
        }
        var tag = optional.Get();
        var data = SerializableChunkData.Parse(this, _factory, tag);
        if (data is null)
        {
            Log.Debug($"LoadChunkAsync 出口 result=null 解析失败");
            return null;
        }
        var result = data.Read(this, SimplePoiManager.Empty, null, pos);
        Log.Debug($"LoadChunkAsync 出口 result={(result is null ? "null" : result.Pos.ToString())}");
        return result;
    }

    //GetChunk 优先走 in-memory 缓存未命中委托 ServerChunkCache 异步调度
    //require=false 不阻塞主循环 require=true 同步等待加载完成
    public override ChunkAccess? GetChunk(ChunkPos pos)
    {
        Log.Debug($"GetChunk 入口 pos={pos}");
        var cached = base.GetChunk(pos);
        if (cached is not null)
        {
            Log.Debug($"GetChunk 出口 result=缓存命中");
            return cached;
        }
        var result = _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, false);
        Log.Debug($"GetChunk 出口 result={(result is null ? "null" : result.Pos.ToString())}");
        return result;
    }

    //GetChunkSync 同步获取区块 require=true 触发加载仅测试或必须同步场景用
    public ChunkAccess? GetChunkSync(ChunkPos pos, bool require = true)
        => _chunkSource.GetChunk(pos.X, pos.Z, ChunkStatus.FULL, require);

    //SaveChunkAsync 把 ChunkAccess 序列化写入 RegionFileStorage
    //使用 PersistentServerLevel.Factory 保证 codec 与注册的 Block/Biome 一致
    public async Task SaveChunkAsync(ChunkAccess chunk)
    {
        Log.Debug($"SaveChunkAsync 入口 chunk={chunk.Pos}");
        var data = SerializableChunkData.CopyOf(this, chunk, _factory);
        var tag = data.Write();
        await _regionStorage.Write(chunk.Pos, tag);
        AddChunk(chunk);
        Log.Debug($"SaveChunkAsync 出口");
    }

    //Synchronize 刷盘对应原版 chunk save 阶段
    public Task SynchronizeAsync(bool flush)
        => _regionStorage.Synchronize(flush);

    //AddEntity 加入实体到关卡对应原版 Level.addFreshEntity
    public void AddEntity(NetCraft.Registry.Entity entity)
        => _entities.Add(entity);

    //RemoveEntity 移除关卡实体返回是否成功
    public bool RemoveEntity(NetCraft.Registry.Entity entity)
        => _entities.Remove(entity);

    //Tick 关卡每帧调度对应原版 ServerLevel.tick
    //1. tick ChunkSource 推进区块调度完成区块移入缓存
    //2. tick 所有实体推进实体行为
    //3. 递增 levelTick 供刷盘调度
    public void Tick()
    {
        //Log.Debug($"Tick 入口 levelTick={_levelTick}");
        _chunkSource.Tick();
        //Log.Debug($"步骤1 ChunkSource.Tick完成");
        foreach (var entity in _entities.GetAll())
            entity.Tick();
        _levelTick++;
        //Log.Debug($"Tick 出口 levelTick={_levelTick}");
    }
}
