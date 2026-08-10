using System.Collections.Concurrent;
using System.Threading;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Level;

//ClientLevel 客户端世界对标原版 net.minecraft.client.multiplayer.ClientLevel
//持区块存储 + 光照存储提供 BlockState/BlockLight/SkyLight 查询
//光照独立存储不挂 LevelChunkSection 对齐原版 LevelLightEngine 设计
//W8 加 ReaderWriterLockSlim 保护 section 内部 PalettedContainer SetBlockState 加写锁 dispatcher Build 持读锁
//_chunks/_lights 用 ConcurrentDictionary 字典级线程安全 LoadChunk/UnloadChunk 不加额外锁避免阻塞编译
//SectionDirty 事件写锁外触发防回调内读 ClientLevel 死锁 dispatcher 订阅调 MarkDirty 扩散
public sealed class ClientLevel
{
    //区块存储按 ChunkPos 索引 ConcurrentDictionary 字典级线程安全
    private readonly ConcurrentDictionary<ChunkPos, ChunkAccess> _chunks = new();
    //光照存储按 SectionPos.AsLong 索引 ConcurrentDictionary 字典级线程安全
    private readonly ConcurrentDictionary<long, (DataLayer BlockLight, DataLayer SkyLight)> _lights = new();
    //_syncRoot 保护 section 内部 PalettedContainer SetBlockState 加写锁 dispatcher Build 持读锁
    private readonly ReaderWriterLockSlim _syncRoot = new();

    //SectionDirty 方块变更事件写锁外触发 dispatcher 订阅调 MarkDirty 扩散自身+6邻居
    public event Action<SectionPos>? SectionDirty;

    //EnterReadLock/ExitReadLock 供 SectionRenderDispatcher 持读锁调 Build 防 SetBlockState 数据竞争
    //持读锁期间允许并发读 SetBlockState 持写锁阻塞 RecursionPolicy.NoRecursion 不可递归调用内部加锁的读方法
    public void EnterReadLock() => _syncRoot.EnterReadLock();
    public void ExitReadLock() => _syncRoot.ExitReadLock();

    //LoadChunk 装入区块覆盖同位置旧区块 ConcurrentDictionary 索引器线程安全不加写锁
    public void LoadChunk(ChunkAccess chunk) => _chunks[chunk.Pos] = chunk;

    //LoadLight 装入区段光照覆盖同位置旧光照 ConcurrentDictionary 索引器线程安全
    public void LoadLight(SectionPos pos, DataLayer blockLight, DataLayer skyLight)
        => _lights[pos.AsLong()] = (blockLight, skyLight);

    //UnloadChunk 移除区块及关联光照遍历该 chunk 所有 sectionY
    //ConcurrentDictionary.Keys 快照遍历安全移除线程安全
    public void UnloadChunk(ChunkPos pos)
    {
        _chunks.TryRemove(pos, out _);
        var keysToRemove = new List<long>();
        foreach (var key in _lights.Keys)
        {
            var sx = SectionPos.GetX(key);
            var sz = SectionPos.GetZ(key);
            if (sx == pos.X && sz == pos.Z) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _lights.TryRemove(key, out _);
    }

    //GetBlockState 查世界坐标方块状态越界或未装载返回 default（air）
    //调用方负责持读锁防 SetBlockState 数据竞争 section 内部非线程安全
    public BlockState GetBlockState(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return default;
        var section = chunk.GetSection(pos.Y >> 4);
        if (section is null) return default;
        return section.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetBlockLight 查世界坐标方块光照越界返回 0
    public int GetBlockLight(BlockPos pos) => GetLight(pos, true);

    //GetSkyLight 查世界坐标天空光照越界返回 15
    public int GetSkyLight(BlockPos pos) => GetLight(pos, false);

    //GetLight 内部光照查询 blockLight=true 取 BlockLight 否则 SkyLight
    //区段越界 block=0 sky=15 对齐原版默认行为
    private int GetLight(BlockPos pos, bool blockLight)
    {
        var sectionPos = SectionPos.Of(pos);
        if (!_lights.TryGetValue(sectionPos.AsLong(), out var light))
            return blockLight ? 0 : 15;
        var layer = blockLight ? light.BlockLight : light.SkyLight;
        return layer.Get(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetSection 按区段坐标取 LevelChunkSection 未装载返回 null
    //调用方负责持读锁防 SetBlockState 数据竞争
    public LevelChunkSection? GetSection(int sectionX, int sectionY, int sectionZ)
    {
        var chunkPos = new ChunkPos(sectionX, sectionZ);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return null;
        return chunk.GetSection(sectionY);
    }

    //HasChunk 区块是否已装载 ConcurrentDictionary 线程安全
    public bool HasChunk(ChunkPos pos) => _chunks.ContainsKey(pos);

    //GetLoadedChunks 返回所有已装载区块快照供 LevelRenderer/ViewArea 遍历
    //ConcurrentDictionary.Values 返回快照遍历安全调用方不需持锁
    public IEnumerable<ChunkAccess> GetLoadedChunks() => _chunks.Values;

    //SetBlockState 改方块加写锁保护 section 内部 PalettedContainer 写锁外触发 SectionDirty
    //chunk 或 section 未装载返回 false 不触发事件
    public bool SetBlockState(BlockPos pos, BlockState state)
    {
        _syncRoot.EnterWriteLock();
        try
        {
            var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
            if (!_chunks.TryGetValue(chunkPos, out var chunk)) return false;
            var section = chunk.GetSection(pos.Y >> 4);
            if (section is null) return false;
            section.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        }
        finally { _syncRoot.ExitWriteLock(); }
        //写锁外触发事件防回调内读 ClientLevel 死锁 dispatcher.MarkDirty 会扩散自身+6邻居
        SectionDirty?.Invoke(SectionPos.Of(pos));
        return true;
    }
}
