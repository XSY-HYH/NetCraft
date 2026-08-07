using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen;

//StructureFeature 结构特征抽象基类对应原版 net.minecraft.world.level.levelgen.structure.Structure
//阶段 11.43-11.45 接入 StructureSettings + StructurePlacement 子系统提供结构生成入口
//子类实现 FindGenerationPoint 决定在何处生成结构并返回 StructureStart
public abstract class StructureFeature
{
    public abstract Identifier Id { get; }

    //Settings 关联的生成设置对应原版 Structure.settings
    //null 表示未关联具体设置不参与实际生成
    public StructureSettings? Settings { get; set; }

    //FindGenerationPoint 查找结构生成点对应原版 findGenerationPoint
    //返回 StructureStart 包含生成的 StructurePiece 列表返回 null 表示不生成
    //默认实现返回 null 子类覆盖提供真实生成逻辑
    public virtual StructureStart? FindGenerationPoint(GenerationContext context)
        => null;

    //GeneratePieces 生成结构部件列表对应原版 Structure.generatePieces
    //默认返回空列表子类覆盖提供真实 piece 构造
    //FindGenerationPoint 默认实现包装此方法生成 StructureStart
    protected virtual IReadOnlyList<StructurePiece> GeneratePieces(GenerationContext context)
        => Array.Empty<StructurePiece>();

    //TryGenerate 包装 FindGenerationPoint 并把结果存入 manager
    //返回 true 表示成功生成 false 表示该 chunk 不生成此结构
    public bool TryGenerate(StructureFeatureManager manager, GenerationContext context)
    {
        var start = FindGenerationPoint(context);
        if (start is null) return false;
        manager.AddStructureStart(context.ChunkPos, start);
        return true;
    }

    //GenerationContext 结构生成上下文对应原版 Structure.GenerationContext
    //持有 chunkPos 与 worldSeed 供子类生成时使用
    public sealed class GenerationContext(ChunkPos chunkPos, long seed)
    {
        public ChunkPos ChunkPos { get; } = chunkPos;
        public long Seed { get; } = seed;

        //CreateBoundingBox 从 ChunkPos 构造默认 16x16x384 边界框供子类 piece 使用
        public BoundingBoxInt CreateChunkBoundingBox(int minY = -64, int maxY = 320)
            => BoundingBoxInt.FromChunkPos(ChunkPos, minY, maxY);
    }
}
