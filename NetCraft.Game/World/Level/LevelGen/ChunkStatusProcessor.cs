using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkStatusProcessor 区块状态机处理器对应原版 ChunkStatus 状态机流水线
//阶段 E 简化实现按 ChunkStatus 名称调用 ChunkGenerator 对应方法
//阶段 11.54-B 接入 STRUCTURE_START/STRUCTURE_REFERENCES 真实结构生成
public sealed class ChunkStatusProcessor
{
    private readonly ChunkGenerator _generator;
    private readonly RandomSource _random;
    private readonly StructureSettings? _settings;
    private readonly long _seed;
    //StructureFeatures 持有本 processor 生成的所有 starts/references 供后续 FEATURES 阶段查询避让
    public StructureFeatureManager StructureFeatures { get; } = new();

    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random)
        : this(generator, random, null, 0L) { }

    //带 StructureSettings 的构造函数供结构生成场景使用
    //seed 决定 StructurePlacement 的网格哈希与 piece 随机
    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random, StructureSettings? settings, long seed)
    {
        _generator = generator;
        _random = random;
        _settings = settings;
        _seed = seed;
    }

    //ProcessChunk 按 ChunkStatus 调用对应生成阶段
    //返回 true 表示该状态已处理false 表示无对应处理
    public bool ProcessChunk(ChunkAccess chunk, ChunkStatus status)
    {
        Log.Debug($"ProcessChunk 入口 chunk={chunk.Pos} status={status}");
        var structures = StructureManager.Default;
        if (status == ChunkStatus.STRUCTURE_START)
        {
            //STRUCTURE_START 调 StructureFeatureManager.CreateStarts 走 FindGenerationPoint
            //_settings 为 null 时跳过保持向后兼容
            Log.Debug($"步骤1 处理 STRUCTURE_START");
            if (_settings is not null)
                StructureFeatures.CreateStarts(chunk, _settings, _seed);
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.STRUCTURE_REFERENCES)
        {
            //STRUCTURE_REFERENCES 扫 3x3 邻居收集 BoundingBox 覆盖当前 chunk 的结构引用
            //供后续 FEATURES 阶段避让装饰
            Log.Debug($"步骤2 处理 STRUCTURE_REFERENCES");
            StructureFeatures.CollectReferences(chunk.Pos, radius: 1);
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.BIOMES)
        {
            //按 quart 遍历每个 section 写入 BiomeSource 查询的 biome 对应原版点采样
            Log.Debug($"步骤3 处理 BIOMES");
            var biomeSource = _generator.BiomeSource;
            var posX = chunk.Pos.X;
            var posZ = chunk.Pos.Z;
            for (var sectionIdx = 0; sectionIdx < chunk.SectionsCount; sectionIdx++)
            {
                var sectionY = chunk.MinSectionY + sectionIdx;
                for (var qY = 0; qY < 4; qY++)
                {
                    for (var qX = 0; qX < 4; qX++)
                    {
                        for (var qZ = 0; qZ < 4; qZ++)
                        {
                            var wx = posX * 16 + qX * 4;
                            var wy = sectionY * 16 + qY * 4;
                            var wz = posZ * 16 + qZ * 4;
                            var biome = biomeSource.GetBiome(wx, wy, wz);
                            chunk.SetBiome(wx, wy, wz, Holder<Biome>.Direct(biome));
                        }
                    }
                }
            }
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.NOISE)
        {
            //噪声阶段调用 FillFromNoise 填方块
            Log.Debug($"步骤4 处理 NOISE 调 FillFromNoise");
            _generator.FillFromNoise(new object(), structures, chunk, _random);
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.SURFACE)
        {
            //表面阶段调用 BuildSurface 应用表面规则
            Log.Debug($"步骤5 处理 SURFACE 调 BuildSurface");
            _generator.BuildSurface(new object(), structures, chunk, _random);
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.CARVERS || status == ChunkStatus.LIQUID_CARVERS)
        {
            //洞穴雕刻占位真实接入需 WorldGenCarver 子系统就绪
            Log.Debug($"步骤6 处理 CARVERS 占位");
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.FEATURES)
        {
            //特征装饰占位真实接入需 FeatureDecorator 子系统就绪
            Log.Debug($"步骤7 处理 FEATURES 调 ApplyBiomeDecoration");
            _generator.ApplyBiomeDecoration(new object(), structures, chunk);
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.LIGHT || status == ChunkStatus.SPAWN || status == ChunkStatus.HEIGHTMAPS)
        {
            //光照/生物生成/高度图占位真实接入需对应子系统就绪
            Log.Debug($"步骤8 处理 LIGHT/SPAWN/HEIGHTMAPS 占位");
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        if (status == ChunkStatus.EMPTY || status == ChunkStatus.FULL)
        {
            //EMPTY 与 FULL 无生成任务
            Log.Debug($"步骤9 处理 EMPTY/FULL 空任务");
            Log.Debug($"ProcessChunk 出口 result=true");
            return true;
        }
        Log.Debug($"ProcessChunk 出口 result=false");
        return false;
    }

    //ProcessToStatus 把区块从当前状态推进到目标状态
    //按 ChunkStatus 注册顺序依次调用 ProcessChunk 直到达到 target
    public void ProcessToStatus(ChunkAccess chunk, ChunkStatus target)
    {
        Log.Debug($"ProcessToStatus 入口 chunk={chunk.Pos} target={target}");
        var current = chunk.ChunkStatus;
        while (current != target && current is not null)
        {
            var next = NextStatus(current);
            if (next is null) break;
            Log.Debug($"步骤1 推进状态 current={current} next={next}");
            ProcessChunk(chunk, next);
            current = next;
        }
        Log.Debug($"ProcessToStatus 出口");
    }

    //NextStatus 查询下一个状态按 ChunkStatus 静态注册顺序
    private static ChunkStatus? NextStatus(ChunkStatus status)
    {
        var order = new[]
        {
            ChunkStatus.EMPTY,
            ChunkStatus.STRUCTURE_START,
            ChunkStatus.STRUCTURE_REFERENCES,
            ChunkStatus.BIOMES,
            ChunkStatus.NOISE,
            ChunkStatus.SURFACE,
            ChunkStatus.CARVERS,
            ChunkStatus.LIQUID_CARVERS,
            ChunkStatus.FEATURES,
            ChunkStatus.LIGHT,
            ChunkStatus.SPAWN,
            ChunkStatus.HEIGHTMAPS,
            ChunkStatus.FULL
        };
        for (var i = 0; i < order.Length - 1; i++)
        {
            if (order[i] == status)
                return order[i + 1];
        }
        return null;
    }
}
