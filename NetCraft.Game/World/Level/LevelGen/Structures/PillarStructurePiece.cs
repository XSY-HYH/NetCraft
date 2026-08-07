using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructurePiece 石柱部件对应原版简化 StructurePiece 子类
//阶段 11.54-B 提供单根石柱的 BoundingBox 与 NBT 序列化
//PostProcess 占位真实方块写入待 WorldGenRegion 子系统就绪
public sealed class PillarStructurePiece : StructurePiece
{
    public int Height { get; }

    public PillarStructurePiece(int baseX, int baseY, int baseZ, int height)
        : base(new BoundingBoxInt(baseX, baseY, baseZ, baseX, baseY + height - 1, baseZ))
    {
        Height = height;
    }

    //PostProcess 占位对应原版 postProcess 真实接入需 WorldGenRegion 子系统就绪
    public override void PostProcess(object worldGenRegion, int chunkX, int chunkZ) { }

    //AddAdditionalSaveData 写入高度字段供反序列化还原
    protected override void AddAdditionalSaveData(CompoundTag tag)
        => tag.PutInt("Height", Height);
}
