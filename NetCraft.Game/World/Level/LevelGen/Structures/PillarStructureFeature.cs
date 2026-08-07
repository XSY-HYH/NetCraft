using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructureFeature 单根石柱示例结构对应原版简化 Structure 子类
//阶段 11.54-B 提供可复用的真实 StructureFeature 子类验证 STRUCTURE_START 链路
//FindGenerationPoint 在 chunk 中央生成一根高度 16 的石柱 piece
public sealed class PillarStructureFeature : StructureFeature
{
    public override Identifier Id => Identifier.WithDefaultNamespace("pillar");
    public const int PillarHeight = 16;
    public const int BaseY = 64;

    //FindGenerationPoint 在 chunk 中央 (8, BaseY, 8) 生成石柱 piece
    //返回包含一个 PillarStructurePiece 的 StructureStart
    public override StructureStart? FindGenerationPoint(GenerationContext context)
    {
        var chunkPos = context.ChunkPos;
        var baseX = chunkPos.X * 16 + 8;
        var baseZ = chunkPos.Z * 16 + 8;
        var piece = new PillarStructurePiece(baseX, BaseY, baseZ, PillarHeight);
        return new StructureStart(this, new[] { piece });
    }
}
