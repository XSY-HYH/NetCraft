using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ProtoChunk 原型区块对应原版 net.minecraft.world.level.chunk.ProtoChunk
//ChunkAccess 抽象基类的最简具体实现用于噪声阶段填方块
//子类 LevelChunk 待 networking 子系统接入升级此处仅做生成阶段最小实现
public class ProtoChunk : ChunkAccess
{
    public override ChunkPos Pos { get; }
    public override int MinSectionY { get; }
    public override int SectionsCount { get; }
    public override ChunkStatus ChunkStatus { get; } = ChunkStatus.EMPTY;

    private readonly LevelChunkSection[] _sections;
    private readonly Dictionary<HeightmapRegistry.Types, long[]> _heightmaps;

    public override IDictionary<HeightmapRegistry.Types, long[]> Heightmaps => _heightmaps;

    public ProtoChunk(ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory,
        Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        Pos = pos;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        _sections = new LevelChunkSection[sectionsCount];
        for (var i = 0; i < sectionsCount; i++)
            _sections[i] = new LevelChunkSection(statesFactory, biomesFactory);
        _heightmaps = new Dictionary<HeightmapRegistry.Types, long[]>();
    }

    public override LevelChunkSection? GetSection(int sectionY)
    {
        var idx = sectionY - MinSectionY;
        return idx >= 0 && idx < _sections.Length ? _sections[idx] : null;
    }

    //GetOrCreateSection 按区段 Y 获取或确认区段存在
    public LevelChunkSection GetOrCreateSection(int sectionY)
        => GetSection(sectionY) ?? throw new ArgumentOutOfRangeException(nameof(sectionY));

    //SetBlockState 写入方块到指定世界坐标对应原版 setBlockState
    //x/y/z 为区块内 0..15 局部坐标sectionY 为区段 Y
    public BlockState SetBlockState(int sectionY, int sectionX, int sectionYLocal, int sectionZ, BlockState state)
    {
        var section = GetOrCreateSection(sectionY);
        return section.SetBlockState(sectionX, sectionYLocal, sectionZ, state);
    }

    //GetSectionsInternal 暴露区段数组供 LevelChunk 子类访问
    protected LevelChunkSection[] GetSectionsInternal() => _sections;
}

//LevelChunk 完整区块对应原版 net.minecraft.world.level.chunk.LevelChunk
//继承 ProtoChunk 扩展 Level 引用与 ChunkStatus 升级能力
//networking 序列化通过 partial LevelChunk.Serializer 在 LevelChunk.Serializer.cs 实现
public sealed partial class LevelChunk : ProtoChunk
{
    public object? Level { get; }
    public new ChunkStatus ChunkStatus { get; set; } = ChunkStatus.FULL;

    public LevelChunk(ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory,
        Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        object? level = null)
        : base(pos, minSectionY, sectionsCount, statesFactory, biomesFactory)
    {
        Level = level;
    }

    //GetBlockState 按世界坐标获取方块状态对应原版 getBlockState
    public BlockState GetBlockState(int worldX, int worldY, int worldZ)
    {
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        return GetSection(sectionY)?.GetBlockState(localX, localY, localZ) ?? default;
    }

    //SetSection 替换指定区段对应原版 LevelChunk.setSection
    //供 LevelChunkSerializer.Read 反序列化时写入新构造的区段
    public void SetSection(int sectionY, LevelChunkSection section)
    {
        var idx = sectionY - MinSectionY;
        if (idx < 0 || idx >= SectionsCount) return;
        GetSectionsInternal()[idx] = section;
    }
}
