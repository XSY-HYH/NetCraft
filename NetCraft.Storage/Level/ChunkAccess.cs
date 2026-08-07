using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ChunkAccess 区块访问抽象基类对应原版 net.minecraft.world.level.chunk.ChunkAccess
//持有区块位置/高度访问/区块状态/高度图等基础字段
//子类 LevelChunk/ProtoChunk 按需扩展具体字段
public abstract class ChunkAccess : LevelHeightAccessor
{
    //Heightmap 实例缓存对应原版 heightmaps 字段
    //Heightmaps 抽象属性持有 long[] 序列化数据此缓存持有可变实例避免每次重建丢失更新
    private Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>? _heightmapCache;

    //Pos 区块位置
    public abstract ChunkPos Pos { get; }

    //MinSectionY 最低区段 Y 对应原版 getMinSection
    public abstract int MinSectionY { get; }

    //SectionsCount 区段数量对应原版 getSectionsCount
    public abstract int SectionsCount { get; }

    //MaxSectionY 由 MinSectionY+SectionsCount-1 推导对应原版 getMaxSection
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //ChunkStatus 区块状态
    public abstract ChunkStatus ChunkStatus { get; }

    //Heightmaps 高度图集合对应原版 getHeightmaps
    public abstract IDictionary<HeightmapRegistry.Types, long[]> Heightmaps { get; }

    //GetSection 按区段 Y 获取区段数据越界返回 null 对应原版 getSection
    public abstract LevelChunkSection? GetSection(int sectionY);

    //GetOrCreateHeightmapForType 按类型创建或获取高度图实例对应原版 getOrCreateHeightmap
    //首次调用从 Heightmaps long[] 重建实例并缓存后续调用返回同一实例
    public virtual LevelGen.Heightmap GetOrCreateHeightmapForType(HeightmapRegistry.Types type)
    {
        _heightmapCache ??= new Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>();
        if (_heightmapCache.TryGetValue(type, out var cached))
            return cached;
        var data = Heightmaps.TryGetValue(type, out var d) ? d : Array.Empty<long>();
        var instance = data.Length == 0
            ? new LevelGen.Heightmap(type, MinSectionY * 16, SectionsCount * 16)
            : LevelGen.Heightmap.FromData(type, MinSectionY * 16, SectionsCount * 16, data);
        _heightmapCache[type] = instance;
        return instance;
    }

    //GetHeight 对应原版 getHeight 按类型取列高度
    public int GetHeight(HeightmapRegistry.Types type, int x, int z)
        => GetOrCreateHeightmapForType(type).GetFirstAvailable(x, z);

    //SetBiome 按世界坐标写入生物群系对应原版 setBiome
    //世界坐标转 sectionY 与 quart 局部坐标委托 section.SetBiome
    public virtual void SetBiome(int worldX, int worldY, int worldZ, Holder<Biome> biome)
    {
        var section = GetSection(worldY >> 4);
        if (section is null) return;
        section.SetBiome((worldX >> 2) & 3, (worldY >> 2) & 3, (worldZ >> 2) & 3, biome);
    }

    //GetNoiseBiome 按 quart 世界坐标查询生物群系对应原版 getNoiseBiome
    public virtual Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        var section = GetSection((quartY >> 2) + MinSectionY);
        return section is null
            ? Holder<Biome>.Direct(EmptyBiome)
            : section.GetNoiseBiome(quartX & 3, quartY & 3, quartZ & 3);
    }

    //EmptyBiome 区段越界时返回的默认 biome 占位避免 null
    private static readonly Biome EmptyBiome = new EmptyBiomeImpl();
    private sealed class EmptyBiomeImpl : Biome
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    }
}
