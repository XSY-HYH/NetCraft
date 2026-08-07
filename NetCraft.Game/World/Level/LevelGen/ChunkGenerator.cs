using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerator 区块生成器抽象基类对应原版 net.minecraft.world.level.chunk.ChunkGenerator
//持有 BiomeSource 子类按需实现噪声/表面/高度/列采样等方法
//BuildSurface/FillFromNoise 等方法签名对齐原版占位参数用 object 待 WorldGenRegion/StructureManager 就绪升级
public abstract class ChunkGenerator
{
    public BiomeSource BiomeSource { get; }

    protected ChunkGenerator(BiomeSource biomeSource)
    {
        BiomeSource = biomeSource;
    }

    //GetGenDepth 最大生成深度对应原版 getGenDepth
    //表示区块从最低到最高的总高度待子类按 settings 推导
    public abstract int GetGenDepth();

    //GetBaseHeight 采样指定坐标的基础高度对应原版 getBaseHeight
    //type 参数为 HeightmapTypes 占位用 int待 Heightmap 子系统就绪升级
    public abstract int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random);

    //GetBaseColumn 采样指定坐标的基础列方块状态对应原版 getBaseColumn
    //返回 BlockState[] 简化占位用 object[] 待 BlockState 在 Game 层就绪升级
    public abstract object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random);

    //FillFromNoise 从噪声填方块到区块对应原版 fillFromNoise
    //blender/structures 占位用 object 待子系统就绪升级
    public abstract void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random);

    //BuildSurface 应用表面规则到区块对应原版 buildSurface
    //region/structures 占位用 object 待子系统就绪升级
    public abstract void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random);

    //ApplyBiomeDecoration 应用生物群系装饰对应原版 applyBiomeDecoration
    //占位签名待 decoration 子系统就绪升级
    public abstract void ApplyBiomeDecoration(object region, object structures, ChunkAccess chunk);
}
