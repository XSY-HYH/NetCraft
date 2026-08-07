using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseBasedChunkGenerator 基于噪声的区块生成器对应原版 net.minecraft.world.level.levelgen.NoiseBasedChunkGenerator
//继承 ChunkGenerator 持有 NoiseGeneratorSettings 与 PerlinNoise 基础噪声
//P0 接入 RandomState + NoiseChunk + Aquifer 通过 GetInterpolatedState 驱动方块决策
//内核不硬编码方块 DefaultBlock/DefaultFluid 由 Game 层通过 NoiseGeneratorSettings 注入
public class NoiseBasedChunkGenerator : ChunkGenerator
{
    public NoiseGeneratorSettings Settings { get; }

    //SamplerHeightNoise 用于地形基础高度采样的 NormalNoise 占位
    //真实接入时由 RandomState 派生此处简化为可空字段
    public NormalNoise? HeightNoise { get; private set; }

    public NoiseBasedChunkGenerator(BiomeSource biomeSource, NoiseGeneratorSettings settings)
        : base(WithSamplerIfNeeded(biomeSource, settings))
    {
        Settings = settings;
    }

    //WithSamplerIfNeeded 构造前检查 BiomeSource 是否为未注入 Sampler 的 MultiNoiseBiomeSource
    //是则用 Settings.NoiseRouter 创建 NoiseRouterSampler 自动注入实现真实派生
    //ParameterList 为 null 时不注入因为 GetBiome 仍走 PlainsBiome 占位路径
    private static BiomeSource WithSamplerIfNeeded(BiomeSource biomeSource, NoiseGeneratorSettings settings)
    {
        if (biomeSource is MultiNoiseBiomeSource { Sampler: null, ParameterList: not null } multi)
            return new MultiNoiseBiomeSource(multi.ParameterList!, new Climate.NoiseRouterSampler(settings.NoiseRouter));
        return biomeSource;
    }

    //InitHeightNoise 派生 NormalNoise 实例用于地形高度采样
    //firstOctave/amplitudes 对齐原版默认配置简化版用 -3 与 [1,1,1,1,1,1,1] 占位
    public void InitHeightNoise(RandomSource random)
    {
        Log.Debug($"InitHeightNoise 入口 random={random}");
        HeightNoise = new NormalNoise(random, -3, 1, 1, 1, 1, 1, 1, 1);
        Log.Debug("InitHeightNoise 出口");
    }

    //GetGenDepth 返回 settings 推导的最大生成深度对应原版 getGenDepth
    //用 Settings.NoiseSettings.Height 对齐原版 settings.height
    public override int GetGenDepth() => Settings.NoiseSettings.Height;

    //GetBaseHeight 采样指定坐标的基础高度对应原版 getBaseHeight
    //type 参数为 HeightmapTypes 占位用 int简化版调 HeightNoise 采样
    public override int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseHeight 入口 x={x} z={z} type={type} level={level} random={random}");
        if (HeightNoise is null) InitHeightNoise(random);
        var value = HeightNoise!.GetValue(x, 0, z);
        var raw = (int)Math.Round(value * 32 + 64);
        var result = Math.Clamp(raw, level.MinBuildHeight, level.MaxBuildHeight - 1);
        Log.Debug($"GetBaseHeight 出口 result={result}");
        return result;
    }

    //GetBaseColumn 采样指定坐标的基础列方块状态对应原版 getBaseColumn
    //简化返回 object[] 长度为 SectionsCount*16 内容全 null 待 BlockState 接入
    public override object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseColumn 入口 x={x} z={z} level={level} random={random}");
        var column = new object?[level.SectionsCount * 16];
        var surfaceY = GetBaseHeight(x, z, 0, level, random);
        for (var y = 0; y < column.Length; y++)
        {
            var absoluteY = level.MinBuildHeight + y;
            column[y] = absoluteY < surfaceY - 4 ? "stone" : null;
        }
        var result = column!;
        Log.Debug($"GetBaseColumn 出口 result={result.Length}");
        return result;
    }

    //CreateFluidPicker 构造全局流体选择器对应原版 NoiseBasedChunkGenerator.createFluidPicker
    //lavaStatus 深岩浆 y<-54 返回 lava seaStatus 海平面流体 y<seaLevel 返回水
    //emptyStatus 占位 AIR 防御性兜底原版用 DimensionType.MIN_Y*2 此处用 int.MinValue/2 更极端
    //FluidPicker 是 Game 层注入方块的边界点 Blocks.LAVA/AIR 由 Game 层提供
    public static Aquifer.FluidPicker CreateFluidPicker(NoiseGeneratorSettings settings)
    {
        var lavaStatus = new FluidStatus(-54, Blocks.LAVA.DefaultBlockState);
        var seaLevel = settings.SeaLevel;
        var seaStatus = new FluidStatus(seaLevel, settings.DefaultFluid);
        var emptyStatus = new FluidStatus(int.MinValue / 2, Blocks.AIR.DefaultBlockState);
        return new GlobalFluidPicker(lavaStatus, seaStatus, emptyStatus, seaLevel);
    }

    //FillFromNoise 从噪声填方块到区块对应原版 fillFromNoise
    //P0 接入 RandomState + NoiseChunk + Aquifer 通过 GetInterpolatedState 驱动方块决策
    //密度>0 时 Aquifer 返回 null 用 Settings.DefaultBlock 兜底即石头
    //密度<=0 时 Aquifer 返回流体状态海平面以下水更深处岩浆否则空气
    public override void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random)
    {
        Log.Debug($"FillFromNoise 入口 chunk={chunk.Pos} random={random}");
        if (chunk is not ProtoChunk proto)
        {
            Log.Debug("FillFromNoise 出口 chunk非ProtoChunk");
            return;
        }

        //首次调用触发 Game 层 Bootstrap 注册方块与噪声参数后续调用幂等返回
        GameBootstrap.Bootstrap();

        //P0 简化版每次按 random 派生 seed 创建 RandomState
        //真实接入应由上层缓存 RandomState 避免重复 mapAll 密度树
        var seed = random.NextLong();
        var randomState = RandomState.Create(Settings, BuiltInRegistries.NOISE, seed);
        var noiseChunk = new NoiseChunk(chunk, randomState, Settings);

        var defaultBlock = Settings.DefaultBlock;

        Log.Debug($"步骤1 开始遍历区块填方块 sections={proto.SectionsCount}");
        for (var sectionIdx = 0; sectionIdx < proto.SectionsCount; sectionIdx++)
        {
            var sectionY = proto.MinSectionY + sectionIdx;
            for (var localY = 0; localY < 16; localY++)
            {
                var worldY = sectionY * 16 + localY;
                for (var localX = 0; localX < 16; localX++)
                {
                    for (var localZ = 0; localZ < 16; localZ++)
                    {
                        var worldX = proto.Pos.X * 16 + localX;
                        var worldZ = proto.Pos.Z * 16 + localZ;
                        var state = noiseChunk.GetInterpolatedState(worldX, worldY, worldZ);
                        proto.SetBlockState(sectionY, localX, localY, localZ, state ?? defaultBlock);
                    }
                }
            }
        }
        Log.Debug("FillFromNoise 出口");
    }

    //BuildSurface 应用表面规则到区块对应原版 buildSurface
    //阶段 D 接入 SurfaceSystem 扫描区块顶部方块替换为 GrassBlock/Dirt
    public override void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random)
    {
        Log.Debug($"BuildSurface 入口 chunk={chunk.Pos} random={random}");
        GameBootstrap.Bootstrap();
        var surfaceSystem = new SurfaceSystem();
        surfaceSystem.BuildSurface(chunk, random);
        Log.Debug("BuildSurface 出口");
    }

    //ApplyBiomeDecoration 应用生物群系装饰对应原版 applyBiomeDecoration
    //阶段 C 占位实现真实接入需 decoration 子系统就绪
    public override void ApplyBiomeDecoration(object region, object structures, ChunkAccess chunk)
    {
        //占位实现真实接入待 decoration 子系统就绪
    }

    //GetBaseHeight 旧版简化签名兼容测试与简单调用
    //内部委托抽象 GetBaseHeight(int,int,int,LevelHeightAccessor,RandomSource) 用 type=0 与占位 accessor
    public int GetBaseHeight(int x, int z)
    {
        Log.Debug($"GetBaseHeight 入口 x={x} z={z}");
        if (HeightNoise is null)
        {
            Log.Debug("GetBaseHeight 出口 result=0 HeightNoise为空");
            return 0;
        }
        var value = HeightNoise.GetValue(x, 0, z);
        var result = (int)Math.Round(value * 32 + 64);
        Log.Debug($"GetBaseHeight 出口 result={result}");
        return result;
    }

    //GetBiome 委托 BiomeSource 查询生物群系
    public Biome GetBiome(int x, int y, int z)
        => BiomeSource.GetBiome(x, y, z);

    //GlobalFluidPicker 全局流体选择器内部实现对应原版 createFluidPicker 的 lambda
    //y < min(-54, seaLevel) 返回 lavaStatus 否则返回 seaStatus
    private sealed class GlobalFluidPicker : Aquifer.FluidPicker
    {
        private readonly FluidStatus _lavaStatus;
        private readonly FluidStatus _seaStatus;
        private readonly FluidStatus _emptyStatus;
        private readonly int _seaLevel;

        public GlobalFluidPicker(FluidStatus lava, FluidStatus sea, FluidStatus empty, int seaLevel)
        {
            _lavaStatus = lava;
            _seaStatus = sea;
            _emptyStatus = empty;
            _seaLevel = seaLevel;
        }

        public FluidStatus ComputeFluid(int blockX, int blockY, int blockZ)
        {
            if (blockY < Math.Min(-54, _seaLevel))
                return _lavaStatus;
            return _seaStatus;
        }
    }
}
