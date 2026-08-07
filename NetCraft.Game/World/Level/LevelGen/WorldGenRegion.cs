using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//WorldGenRegion 世界生成区域访问接口对应原版 net.minecraft.server.level.WorldGenRegion
//持有 ServerLevel 引用 GetChunk 委托到 ServerLevel 实现真实接入
//无 ServerLevel 时回退到 in-memory 字典兼容旧测试场景
public sealed class WorldGenRegion
{
    private readonly ServerLevel? _level;
    private readonly Dictionary<long, ChunkAccess> _fallbackChunks = new();
    public int MinSectionY { get; }
    public int SectionsCount { get; }

    //WorldGenRegion 接入 ServerLevel 真实路径区块查询委托到 ServerLevel.GetChunk
    public WorldGenRegion(ServerLevel level, int minSectionY, int sectionsCount)
    {
        _level = level;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //WorldGenRegion 旧构造无 ServerLevel 时走 in-memory 字典占位
    //兼容阶段 D 测试场景真实场景应传 ServerLevel
    public WorldGenRegion(int minSectionY, int sectionsCount)
    {
        _level = null;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //Level 持有的 ServerLevel 引用未接入时返回 null
    public ServerLevel? Level => _level;

    //AddChunk 加入区块到 in-memory 字典仅 fallback 路径有效
    public void AddChunk(ChunkAccess chunk)
        => _fallbackChunks[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = chunk;

    //GetChunk 优先委托到 ServerLevel.GetChunk无 ServerLevel 时走 fallback 字典
    public ChunkAccess? GetChunk(int chunkX, int chunkZ)
    {
        if (_level is not null)
        {
            if (_level is SimpleServerLevel simple)
                return simple.GetChunk(chunkX, chunkZ);
            return _level.GetChunk(new ChunkPos(chunkX, chunkZ));
        }
        return _fallbackChunks.TryGetValue(ChunkPos.Pack(chunkX, chunkZ), out var chunk) ? chunk : null;
    }

    //GetBlockState 按世界坐标获取方块状态委托到对应区块的 section
    public BlockState GetBlockState(int worldX, int worldY, int worldZ)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;
        var chunk = GetChunk(chunkX, chunkZ);
        if (chunk is null) return default;
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        var section = chunk.GetSection(sectionY);
        return section?.GetBlockState(localX, localY, localZ) ?? default;
    }

    //SetBlockState 按世界坐标设置方块状态返回旧状态
    //委托到对应区块的 section若区块不存在或越界返回 default
    public BlockState SetBlockState(int worldX, int worldY, int worldZ, BlockState state)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;
        if (GetChunk(chunkX, chunkZ) is not ProtoChunk proto) return default;
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        return proto.SetBlockState(sectionY, localX, localY, localZ, state);
    }
}
