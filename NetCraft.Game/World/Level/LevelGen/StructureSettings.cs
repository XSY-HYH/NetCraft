using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//StructureSettings 结构生成设置对应原版 net.minecraft.world.level.levelgen.structure.placement.StructureSettings
//持有 StructureFeature 到 StructurePlacement 列表的映射供 ChunkGenerator 查询每个 chunk 要生成哪些结构
public sealed class StructureSettings
{
    private readonly Dictionary<StructureFeature, List<StructurePlacement>> _placements = new();
    private readonly long _seed;

    public long Seed => _seed;

    public StructureSettings(long seed)
    {
        _seed = seed;
    }

    //AddPlacement 注册结构的放置配置对应原版 structures 配置项
    //同一结构可注册多个 Placement 实际生成时按任意命中
    public void AddPlacement(StructureFeature feature, StructurePlacement placement)
    {
        if (!_placements.TryGetValue(feature, out var list))
        {
            list = new List<StructurePlacement>();
            _placements[feature] = list;
        }
        list.Add(placement);
    }

    //GetPlacementsForFeature 查询结构的所有放置配置
    public IReadOnlyList<StructurePlacement> GetPlacementsForFeature(StructureFeature feature)
        => _placements.TryGetValue(feature, out var list) ? list : Array.Empty<StructurePlacement>();

    //GetFeaturesForChunk 查询目标 chunk 命中的所有结构对应原版 StructureSettings.evaluate
    //遍历所有 (feature, placements) 对任意 placement.IsPlacementChunk 命中则加入结果
    public List<StructureFeature> GetFeaturesForChunk(ChunkPos pos)
    {
        var result = new List<StructureFeature>();
        foreach (var (feature, placements) in _placements)
        {
            foreach (var placement in placements)
            {
                if (placement.IsPlacementChunk(_seed, pos.X, pos.Z))
                {
                    result.Add(feature);
                    break;
                }
            }
        }
        return result;
    }

    //Empty 空配置用于不生成结构的场景
    public static StructureSettings Empty { get; } = new(0L);
}
