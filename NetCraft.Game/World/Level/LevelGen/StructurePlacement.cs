using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen;

//StructurePlacement 结构放置抽象基类对应原版 net.minecraft.world.level.levelgen.structure.placement.StructurePlacement
//决定结构在哪些 chunk 生成按 spacing/separation/salt 计算区块坐标
//子类实现 IsPlacementChunk 决定具体放置逻辑
public abstract class StructurePlacement
{
    public Identifier Id { get; }
    public int Spacing { get; }
    public int Separation { get; }
    public long Salt { get; }

    protected StructurePlacement(Identifier id, int spacing, int separation, long salt)
    {
        Id = id;
        Spacing = spacing;
        Separation = separation;
        Salt = salt;
    }

    //GetPotentialChunkPos 计算潜在放置区块坐标对应原版 getPotentialChunkPos
    //按 spacing 网格划分每个网格一个潜在 chunk 取网格左下角加随机偏移
    public ChunkPos GetPotentialChunkPos(long seed, int chunkX, int chunkZ)
    {
        var gridX = FloorDiv(chunkX, Spacing);
        var gridZ = FloorDiv(chunkZ, Spacing);
        var hash = GridHash(seed, gridX, gridZ, Salt);
        var offsetX = FloorMod(hash, Spacing - Separation);
        var offsetZ = FloorMod(hash >> 28, Spacing - Separation);
        return new ChunkPos(gridX * Spacing + (int)offsetX, gridZ * Spacing + (int)offsetZ);
    }

    //IsPlacementChunk 判断该 chunk 是否为放置点对应原版 isPlacementChunk
    //默认实现比较 GetPotentialChunkPos 是否等于目标 chunk 子类可覆盖提供额外过滤
    public virtual bool IsPlacementChunk(long seed, int chunkX, int chunkZ)
    {
        var potential = GetPotentialChunkPos(seed, chunkX, chunkZ);
        return potential.X == chunkX && potential.Z == chunkZ;
    }

    //FloorDiv Java 风格向下取整除法对应 Math.floorDiv
    private static int FloorDiv(int a, int b)
    {
        var r = a / b;
        return (a ^ b) < 0 && r * b != a ? r - 1 : r;
    }

    //FloorMod Java 风格向下取整模运算对应 Math.floorMod
    private static long FloorMod(long a, int b)
    {
        var mod = a % b;
        return mod < 0 ? mod + b : mod;
    }

    //GridHash 网格哈希对应原版 ConcentricRingsStructurePlacement.hash
    //用 xoroshiro 风格混合 gridX/gridZ/salt 生成确定性哈希
    private static long GridHash(long seed, long gridX, long gridZ, long salt)
    {
        var hash = seed ^ (gridX * 341873128712L + gridZ * 132897987541L + salt);
        hash ^= (long)((ulong)hash >> 29);
        hash = (long)((ulong)hash * 0x94D049BB133111EBUL);
        hash ^= (long)((ulong)hash >> 32);
        return hash;
    }
}

//RandomSpreadStructurePlacement 随机散布放置对应原版 RandomSpreadStructurePlacement
//最简单的放置类型按 spacing 网格每格随机选一个 chunk
public sealed class RandomSpreadStructurePlacement : StructurePlacement
{
    public RandomSpreadStructurePlacement(Identifier id, int spacing, int separation, long salt)
        : base(id, spacing, separation, salt) { }
}
