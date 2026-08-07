using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerationHelper 区块生成辅助类
//把 ChunkGenerator + ChunkStatusProcessor 包装成 ServerChunkCache 需要的 generator 回调
//存档未命中时按 ChunkStatus 状态机流水线从 EMPTY 推进到 FULL 生成新 chunk
public static class ChunkGenerationHelper
{
    //CreateGenerator 创建 generator 闭包返回 Func<ChunkPos, ChunkAccess?>
    //minSectionY/sectionsCount 用于构造 SimpleLevelHeightAccessor 决定生成 chunk 的区段范围
    public static Func<ChunkPos, ChunkAccess?> CreateGenerator(
        ChunkGenerator generator,
        int minSectionY,
        int sectionsCount,
        RandomSource random,
        PalettedContainerFactory factory)
    {
        var level = new SimpleLevelHeightAccessor(minSectionY, sectionsCount);
        var processor = new ChunkStatusProcessor(generator, random);
        return pos => GenerateChunk(processor, level, factory, pos);
    }

    //GenerateChunk 单 chunk 生成流程对应原版 chunk generator 流水线
    //1. 构造 ProtoChunk 状态 EMPTY
    //2. 走 ChunkStatusProcessor.ProcessToStatus 推进到 FULL
    //3. 返回 proto 供 ServerChunkCache 缓存
    private static ChunkAccess GenerateChunk(
        ChunkStatusProcessor processor,
        LevelHeightAccessor level,
        PalettedContainerFactory factory,
        ChunkPos pos)
    {
        var proto = new ProtoChunk(
            pos,
            level.MinSectionY,
            level.SectionsCount,
            factory.CreateForBlockStates,
            factory.CreateForBiomes);
        processor.ProcessToStatus(proto, ChunkStatus.FULL);
        return proto;
    }
}
