using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Level;

//ClientLevel 客户端世界对标原版 net.minecraft.client.multiplayer.ClientLevel
//持区块存储 + 光照存储提供 BlockState/BlockLight/SkyLight 查询
//光照独立存储不挂 LevelChunkSection 对齐原版 LevelLightEngine 设计
//W4 首版最小骨架只含光照+区段访问 W7 补全网络装入
public sealed class ClientLevel
{
    //区块存储按 ChunkPos 索引
    private readonly Dictionary<ChunkPos, ChunkAccess> _chunks = new();
    //光照存储按 SectionPos.AsLong 索引 BlockLight/SkyLight 独立 DataLayer
    private readonly Dictionary<long, (DataLayer BlockLight, DataLayer SkyLight)> _lights = new();

    //LoadChunk 装入区块覆盖同位置旧区块
    public void LoadChunk(ChunkAccess chunk) => _chunks[chunk.Pos] = chunk;

    //LoadLight 装入区段光照覆盖同位置旧光照
    public void LoadLight(SectionPos pos, DataLayer blockLight, DataLayer skyLight)
        => _lights[pos.AsLong()] = (blockLight, skyLight);

    //UnloadChunk 移除区块及关联光照遍历该 chunk 所有 sectionY
    public void UnloadChunk(ChunkPos pos)
    {
        _chunks.Remove(pos);
        var keysToRemove = new List<long>();
        foreach (var key in _lights.Keys)
        {
            var sx = SectionPos.GetX(key);
            var sz = SectionPos.GetZ(key);
            if (sx == pos.X && sz == pos.Z) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _lights.Remove(key);
    }

    //GetBlockState 查世界坐标方块状态越界或未装载返回 default（air）
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
    public LevelChunkSection? GetSection(int sectionX, int sectionY, int sectionZ)
    {
        var chunkPos = new ChunkPos(sectionX, sectionZ);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return null;
        return chunk.GetSection(sectionY);
    }

    //HasChunk 区块是否已装载
    public bool HasChunk(ChunkPos pos) => _chunks.ContainsKey(pos);

    //GetLoadedChunks 返回所有已装载区块供 LevelRenderer 遍历渲染
    public IEnumerable<ChunkAccess> GetLoadedChunks() => _chunks.Values;
}
