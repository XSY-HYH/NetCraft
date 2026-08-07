using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen;

//StructurePiece 结构部件抽象基类对应原版 net.minecraft.world.level.levelgen.structure.StructurePiece
//阶段 11.43-11.45 接入 BoundingBox + NBT 序列化辅助
//子类持有 BoundingBox 并实现具体部件生成逻辑
public abstract class StructurePiece
{
    public BoundingBoxInt BoundingBox { get; protected set; }

    protected StructurePiece(BoundingBoxInt boundingBox)
    {
        BoundingBox = boundingBox;
    }

    //Move 整体平移边界框对应原版 StructurePiece.move
    public void Move(int dx, int dy, int dz)
        => BoundingBox = new BoundingBoxInt(
            BoundingBox.MinX + dx, BoundingBox.MinY + dy, BoundingBox.MinZ + dz,
            BoundingBox.MaxX + dx, BoundingBox.MaxY + dy, BoundingBox.MaxZ + dz);

    //PostProcess 后处理占位对应原版 postProcess
    //真实接入需 WorldGenRegion 子系统就绪子类覆盖提供真实方块写入
    public virtual void PostProcess(object worldGenRegion, int chunkX, int chunkZ) { }

    //AddAdditionalSaveData 写入额外数据到 CompoundTag 对应原版 addAdditionalSaveData
    //默认空实现子类按需覆盖持久化自定义字段
    protected virtual void AddAdditionalSaveData(CompoundTag tag) { }

    //WriteSaveData 序列化部件到 CompoundTag 对应原版 StructurePiece.save
    //子类应先调 base 再补充自定义字段
    public CompoundTag WriteSaveData()
    {
        var tag = new CompoundTag();
        tag.PutInt("MinX", BoundingBox.MinX);
        tag.PutInt("MinY", BoundingBox.MinY);
        tag.PutInt("MinZ", BoundingBox.MinZ);
        tag.PutInt("MaxX", BoundingBox.MaxX);
        tag.PutInt("MaxY", BoundingBox.MaxY);
        tag.PutInt("MaxZ", BoundingBox.MaxZ);
        AddAdditionalSaveData(tag);
        return tag;
    }

    //ReadBoundingBox 从 CompoundTag 还原边界框对应原版 StructurePiece 构造反序列化
    protected static BoundingBoxInt ReadBoundingBox(CompoundTag tag)
        => new(
            tag.GetIntOr("MinX", 0), tag.GetIntOr("MinY", 0), tag.GetIntOr("MinZ", 0),
            tag.GetIntOr("MaxX", 0), tag.GetIntOr("MaxY", 0), tag.GetIntOr("MaxZ", 0));
}

//BoundingBoxInt 整数边界框对应原版 net.minecraft.world.level.levelgen.structure.BoundingBox
//简化为 int 六元组持有 minX/maxX/minY/maxY/minZ/maxZ
public sealed record BoundingBoxInt(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    //FromChunkPos 从 ChunkPos 构造 16x16x384 边界框对应原版 chunk 区域
    public static BoundingBoxInt FromChunkPos(ChunkPos pos, int minY = -64, int maxY = 320)
        => new(pos.X << 4, minY, pos.Z << 4, (pos.X << 4) + 15, maxY, (pos.Z << 4) + 15);

    public int LengthX => MaxX - MinX + 1;
    public int LengthY => MaxY - MinY + 1;
    public int LengthZ => MaxZ - MinZ + 1;

    //Intersects 检测两边界框是否相交对应原版 BoundingBox.intersects
    public bool Intersects(BoundingBoxInt other)
        => MaxX >= other.MinX && MinX <= other.MaxX
        && MaxY >= other.MinY && MinY <= other.MaxY
        && MaxZ >= other.MinZ && MinZ <= other.MaxZ;

    //Encapsulate 合并两个边界框对应原版 BoundingBox.encapsulate
    public BoundingBoxInt Encapsulate(BoundingBoxInt other)
        => new(
            Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY), Math.Max(MaxZ, other.MaxZ));
}
