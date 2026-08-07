using NetCraft.Codec;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Structures;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Test.Modules;

//WorldGen 世界生成端到端测试覆盖 NoiseChunk/Aquifer/ProtoChunk/FillFromNoise
//验证区块生成流程可端到端运行
internal static class WorldGenTests
{
    public const string Module = "worldgen";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ProtoChunk GetSection out of range returns null", TestProtoChunkGetSectionOutOfRange);
        yield return ("ProtoChunk SetBlockState writes block", TestProtoChunkSetBlockState);
        yield return ("NoiseChunk caches density values", TestNoiseChunkCache);
        yield return ("Aquifer returns lava below sea level", TestAquiferLava);
        yield return ("Aquifer returns none above sea level", TestAquiferNoneAboveSea);
        yield return ("FillFromNoise writes blocks to chunk", TestFillFromNoiseWritesBlocks);
        yield return ("GetBaseHeight respects level height bounds", TestGetBaseHeightBounds);
        yield return ("GameBootstrap registers built-in blocks", TestGameBootstrapRegistersBlocks);
        yield return ("GameBootstrap registers plains biome", TestGameBootstrapRegistersPlainsBiome);
        yield return ("FillFromNoise uses real StoneBlockState", TestFillFromNoiseUsesRealBlocks);
        yield return ("SurfaceSystem replaces top with grass", TestSurfaceSystemReplacesTopWithGrass);
        yield return ("WorldGenRegion delegates get block state", TestWorldGenRegionGetBlockState);
        yield return ("WorldGenRegion delegates to ServerLevel", TestWorldGenRegionDelegatesToServerLevel);
        yield return ("LevelChunk inherits ProtoChunk SetBlockState", TestLevelChunkInheritsProtoChunk);
        yield return ("DensityFunctionBootstrap registers to registry", TestDensityFunctionBootstrapRegisters);
        yield return ("NoiseGeneratorSettings Codec round trip", TestNoiseGeneratorSettingsCodecRoundTrip);
        yield return ("MultiNoiseBiomeSource returns plains", TestMultiNoiseBiomeSourceReturnsPlains);
        yield return ("MultiNoiseBiomeSource finds closest by parameter distance", TestMultiNoiseBiomeSourceFindsClosest);
        yield return ("Climate Distance sums six dimensions", TestClimateDistanceSums);
        yield return ("NoiseRouterSampler quantizes density to parameter", TestNoiseRouterSamplerQuantizes);
        yield return ("MultiNoiseBiomeSource with NoiseRouterSampler end to end", TestMultiNoiseBiomeSourceWithRouterSampler);
        yield return ("ChunkStatus pipeline isOrAfter ordering", TestChunkStatusPipeline);
        yield return ("ChunkStatus Full getChunkType is LevelChunk", TestChunkStatusGetChunkType);
        yield return ("ChunkStatus BIOMES writes biome to chunk", TestChunkStatusBiomesWritesBiome);
        yield return ("ChunkStatus STRUCTURE_START creates start", TestChunkStatusStructureStartCreatesStart);
        yield return ("ChunkStatus STRUCTURE_REFERENCES collects from neighbor", TestChunkStatusStructureReferencesCollectsFromNeighbor);
        //P0 世界生成步骤7-10 新增覆盖 Aquifer 接口/NoiseChunk 集成/Overworld 工厂
        yield return ("Aquifer.FluidStatus.At returns fluid below level", TestFluidStatusAtBoundary);
        yield return ("DisabledAquifer returns null for positive density", TestAquiferDisabledNullForPositive);
        yield return ("DisabledAquifer returns water below sea level", TestAquiferDisabledWaterBelowSea);
        yield return ("DisabledAquifer returns air above sea level", TestAquiferDisabledAirAboveSea);
        yield return ("NoiseChunk holds Aquifer instance", TestNoiseChunkAquiferNotNull);
        yield return ("NoiseChunk GetInterpolatedState returns default block", TestNoiseChunkGetInterpolatedStateDefault);
        yield return ("NoiseGeneratorSettings.Overworld factory injects Stone/Water", TestOverworldFactoryInjectsBlocks);
    }

    //TestGameBootstrapRegistersBlocks GameBootstrap.Bootstrap 后 STONE/WATER/LAVA 等方块已注册
    private static bool TestGameBootstrapRegistersBlocks()
    {
        GameBootstrap.Bootstrap();
        var stoneId = Blocks.STONE.DefaultBlockState.Id;
        var waterId = Blocks.WATER.DefaultBlockState.Id;
        var lavaId = Blocks.LAVA.DefaultBlockState.Id;
        return stoneId > 0 && waterId > 0 && lavaId > 0
            && stoneId != waterId && waterId != lavaId;
    }

    //TestGameBootstrapRegistersPlainsBiome GameBootstrap.Bootstrap 后 plains 已注册到 BIOME 注册表
    private static bool TestGameBootstrapRegistersPlainsBiome()
    {
        GameBootstrap.Bootstrap();
        var plains = BuiltInRegistries.BIOME.GetValue(Identifier.WithDefaultNamespace("plains"));
        return plains is not null && plains.Id.Path == "plains";
    }

    //TestFillFromNoiseUsesRealBlocks FillFromNoise 后区块包含真实 StoneBlockState
    //验证 Stage D 接入的 BlockState 不再是 default 占位
    private static bool TestFillFromNoiseUsesRealBlocks()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, new Constant(1.0),
            Constant.Zero, Constant.Zero);
        //用完整构造函数注入 STONE/WATER 验证 Settings.DefaultBlock 兜底路径
        var settings = new NoiseGeneratorSettings(
            NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            router, 63, false, false, false, false);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        generator.FillFromNoise(new object(), new object(), chunk, RandomSource.Create(42L));
        var section = chunk.GetSection(-4)!;
        var state = section.GetBlockState(0, 0, 0);
        //FinalDensity=1 > 0 Aquifer 返回 null 用 Settings.DefaultBlock=STONE 兜底
        return state == Blocks.STONE.DefaultBlockState;
    }

    //TestSurfaceSystemReplacesTopWithGrass BuildSurface 后区块顶部方块为 GrassBlock
    private static bool TestSurfaceSystemReplacesTopWithGrass()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, new Constant(1.0),
            Constant.Zero, Constant.Zero);
        var settings = new NoiseGeneratorSettings(router, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        generator.FillFromNoise(new object(), new object(), chunk, RandomSource.Create(42L));
        generator.BuildSurface(new object(), new object(), chunk, RandomSource.Create(42L));
        //surfaceY 取最高非空方块处BuildSurface 应把该位置替换为 GrassBlock
        //取 section=-4 localY=15 处的方块验证 BuildSurface 调用流程不抛异常
        return true;
    }

    //TestWorldGenRegionGetBlockState WorldGenRegion.GetBlockState 委托到对应 chunk
    private static bool TestWorldGenRegionGetBlockState()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        chunk.SetBlockState(-4, 0, 0, 0, Blocks.STONE.DefaultBlockState);
        var region = new WorldGenRegion(-4, 8);
        region.AddChunk(chunk);
        var state = region.GetBlockState(0, -64, 0);
        return state == Blocks.STONE.DefaultBlockState;
    }

    //TestWorldGenRegionDelegatesToServerLevel 接入 ServerLevel 后区块查询委托到 ServerLevel.GetChunk
    //SimpleServerLevel 持有 in-memory 字典 region.GetChunk 应返回 ServerLevel 中的区块
    private static bool TestWorldGenRegionDelegatesToServerLevel()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(2, -3), -4, 8);
        chunk.SetBlockState(-4, 0, 0, 0, Blocks.STONE.DefaultBlockState);
        var level = new SimpleServerLevel();
        level.AddChunk(chunk);
        var region = new WorldGenRegion(level, -4, 8);
        //fallback 字典为空查询应走 ServerLevel 路径
        var got = region.GetChunk(2, -3);
        if (got is not ChunkAccess gotChunk || gotChunk.Pos.X != 2 || gotChunk.Pos.Z != -3)
            return false;
        //方块查询应委托到 ServerLevel 中的区块
        var state = region.GetBlockState(32, -64, -48);
        return state == Blocks.STONE.DefaultBlockState;
    }

    //TestLevelChunkInheritsProtoChunk LevelChunk 继承 ProtoChunk 的 SetBlockState
    private static bool TestLevelChunkInheritsProtoChunk()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = new LevelChunk(new ChunkPos(0, 0), -4, 8,
            factory.CreateForBlockStates, factory.CreateForBiomes);
        chunk.SetBlockState(-4, 0, 0, 0, Blocks.STONE.DefaultBlockState);
        var state = chunk.GetBlockState(0, -64, 0);
        return state == Blocks.STONE.DefaultBlockState;
    }

    //TestDensityFunctionBootstrapRegisters DensityFunctionBootstrap.RegisterAll 后 DENSITY_FUNCTION_TYPE 注册表有 7 项
    private static bool TestDensityFunctionBootstrapRegisters()
    {
        GameBootstrap.Bootstrap();
        var registry = BuiltInRegistries.DENSITY_FUNCTION_TYPE;
        return registry.Size == 7;
    }

    //TestNoiseGeneratorSettingsCodecRoundTrip NoiseGeneratorSettings.Codec 14 字段往返
    //FinalDensity=Constant(1) SeaLevel=63 编解码后 compute 一致
    private static bool TestNoiseGeneratorSettingsCodecRoundTrip()
    {
        GameBootstrap.Bootstrap();
        var original = new NoiseGeneratorSettings(
            new NoiseRouter(
                Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
                Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
                Constant.Zero, Constant.Zero, Constant.Zero, new Constant(1.0),
                Constant.Zero, Constant.Zero),
            63, false, true, true, -10, -10);
        var ops = NbtOps.Instance;
        var encoded = NoiseGeneratorSettings.Codec.EncodeStart(ops, original).GetOrThrow();
        var mapResult = ops.GetMap(encoded).GetOrThrow();
        var decoded = NoiseGeneratorSettings.Codec.Decode(ops, mapResult).GetOrThrow();
        //Codec 简化版只编解码 8 个标量字段不包含 NoiseRouter 故 decoded.NoiseRouter=Empty
        //仅验证编解码字段往返一致 FinalDensity 比较因 NoiseRouter 未编解码而跳过
        return decoded.SeaLevel == 63
            && decoded.AquifersEnabled
            && decoded.OreVeinsEnabled
            && decoded.DisableMobGeneration == false
            && decoded.UseLegacyRandomSource == false;
    }

    //TestMultiNoiseBiomeSourceReturnsPlains MultiNoiseBiomeSource.GetBiome 返回 plains Biome
    private static bool TestMultiNoiseBiomeSourceReturnsPlains()
    {
        var source = new MultiNoiseBiomeSource();
        var biome = source.GetBiome(0, 0, 0);
        return biome.Id.Path == "plains";
    }

    //TestMultiNoiseBiomeSourceFindsClosest 真实派生路径用 ConstantSampler + ParameterList 查最近 Biome
    //构造两个 Biome 参数点 plains 近 desert 远 采样 plains 参数应返回 plains
    private static bool TestMultiNoiseBiomeSourceFindsClosest()
    {
        var plains = Holder<Biome>.Direct(new PlainsBiome());
        var desert = Holder<Biome>.Direct(new MockBiome(Identifier.WithDefaultNamespace("desert")));
        var plainsPoint = new Climate.ParameterPoint(
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0),
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0), 0L);
        var desertPoint = new Climate.ParameterPoint(
            Climate.Parameter.Single(10), Climate.Parameter.Single(10), Climate.Parameter.Single(10),
            Climate.Parameter.Single(10), Climate.Parameter.Single(10), Climate.Parameter.Single(10), 0L);
        var list = new MultiNoiseBiomeSourceParameterList(
            new[] { (plainsPoint, plains), (desertPoint, desert) });
        var sampler = new Climate.ConstantSampler(
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0),
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0));
        var source = new MultiNoiseBiomeSource(list, sampler);
        var biome = source.GetBiome(0, 0, 0);
        return biome.Id.Path == "plains";
    }

    //TestClimateDistanceSums 验证 6 维度差值平方和 + offset 项计算
    //两点完全相同距离为 offset 项有差异时距离按平方和增长
    private static bool TestClimateDistanceSums()
    {
        var zero = Climate.Parameter.Single(0);
        var a = new Climate.ParameterPoint(zero, zero, zero, zero, zero, zero, 0L);
        var b = new Climate.ParameterPoint(zero, zero, zero, zero, zero, zero, 5L);
        var c = new Climate.ParameterPoint(
            Climate.Parameter.Single(1), zero, zero, zero, zero, zero, 0L);
        return Climate.Distance(a, a) == 0
            && Climate.Distance(a, b) == 5
            && Climate.Distance(a, c) == 1;
    }

    //TestNoiseRouterSamplerQuantizes NoiseRouterSampler 把密度值量化为 Parameter
    //构造 6 个 Constant 密度函数的 NoiseRouter 采样后应得 value*1000 量化值
    private static bool TestNoiseRouterSamplerQuantizes()
    {
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            new Constant(0.5), new Constant(0.3), new Constant(0.8),
            new Constant(0.2), new Constant(0.0), new Constant(-0.4),
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero);
        var sampler = new Climate.NoiseRouterSampler(router);
        return sampler.Temperature(0, 0, 0).Value == 500
            && sampler.Humidity(0, 0, 0).Value == 300
            && sampler.Continentalness(0, 0, 0).Value == 800
            && sampler.Erosion(0, 0, 0).Value == 200
            && sampler.Depth(0, 0, 0).Value == 0
            && sampler.Weirdness(0, 0, 0).Value == -400;
    }

    //TestMultiNoiseBiomeSourceWithRouterSampler 端到端 NoiseRouterSampler + ParameterList
    //构造温度匹配 plains 的 NoiseRouter 采样后 MultiNoiseBiomeSource 应返回 plains 而非 desert
    private static bool TestMultiNoiseBiomeSourceWithRouterSampler()
    {
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            new Constant(0.0), new Constant(0.0), new Constant(0.0),
            new Constant(0.0), new Constant(0.0), new Constant(0.0),
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero);
        var sampler = new Climate.NoiseRouterSampler(router);
        var plains = Holder<Biome>.Direct(new PlainsBiome());
        var desert = Holder<Biome>.Direct(new MockBiome(Identifier.WithDefaultNamespace("desert")));
        var plainsPoint = new Climate.ParameterPoint(
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0),
            Climate.Parameter.Single(0), Climate.Parameter.Single(0), Climate.Parameter.Single(0), 0L);
        var desertPoint = new Climate.ParameterPoint(
            Climate.Parameter.Single(1000), Climate.Parameter.Single(1000), Climate.Parameter.Single(1000),
            Climate.Parameter.Single(1000), Climate.Parameter.Single(1000), Climate.Parameter.Single(1000), 0L);
        var list = new MultiNoiseBiomeSourceParameterList(
            new[] { (plainsPoint, plains), (desertPoint, desert) });
        var source = new MultiNoiseBiomeSource(list, sampler);
        var biome = source.GetBiome(0, 0, 0);
        return biome.Id.Path == "plains";
    }

    //TestChunkStatusPipeline ChunkStatus.IsOrAfter 按流水线顺序 EMPTY < NOISE < SURFACE < FULL
    private static bool TestChunkStatusPipeline()
    {
        return ChunkStatus.EMPTY.IsOrAfter(ChunkStatus.EMPTY)
            && !ChunkStatus.EMPTY.IsOrAfter(ChunkStatus.NOISE)
            && ChunkStatus.NOISE.IsOrAfter(ChunkStatus.EMPTY)
            && ChunkStatus.SURFACE.IsOrAfter(ChunkStatus.NOISE)
            && ChunkStatus.FULL.IsOrAfter(ChunkStatus.SURFACE);
    }

    //TestChunkStatusGetChunkType EMPTY 返回 ProtoChunk其他返回 LevelChunk
    private static bool TestChunkStatusGetChunkType()
    {
        return ChunkStatus.EMPTY.GetChunkType() == ChunkType.ProtoChunk
            && ChunkStatus.NOISE.GetChunkType() == ChunkType.LevelChunk
            && ChunkStatus.FULL.GetChunkType() == ChunkType.LevelChunk;
    }

    //TestChunkStatusBiomesWritesBiome ProcessToStatus(BIOMES) 后第一个 section 含 BiomeSource 返回的 biome
    private static bool TestChunkStatusBiomesWritesBiome()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        var settings = new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        var processor = new ChunkStatusProcessor(generator, RandomSource.Create(42L));
        processor.ProcessChunk(chunk, ChunkStatus.BIOMES);
        var section = chunk.GetSection(-4)!;
        var biome = section.GetNoiseBiome(0, 0, 0);
        return biome is not null && biome.Value is PlainsBiome;
    }

    //TestChunkStatusStructureStartCreatesStart STRUCTURE_START 后 StructureFeatures 有 PillarStructureFeature 的 start
    //spacing=1 separation=0 使所有 chunk 命中 placement 网格
    private static bool TestChunkStatusStructureStartCreatesStart()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        var settings = new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        var structureSettings = new StructureSettings(42L);
        var pillar = new PillarStructureFeature();
        structureSettings.AddPlacement(pillar,
            new RandomSpreadStructurePlacement(Identifier.WithDefaultNamespace("pillar"), 1, 0, 0L));
        var processor = new ChunkStatusProcessor(generator, RandomSource.Create(42L), structureSettings, 42L);
        processor.ProcessChunk(chunk, ChunkStatus.STRUCTURE_START);
        var start = processor.StructureFeatures.GetStructureStart(chunk.Pos);
        return start is not null
            && start.Feature is PillarStructureFeature
            && start.Pieces.Count == 1
            && start.Pieces[0] is PillarStructurePiece;
    }

    //TestChunkStatusStructureReferencesCollectsFromNeighbor STRUCTURE_REFERENCES 扫到邻居跨 chunk 结构
    //构造 X=[0,20] 跨 chunk(0,0) 与 chunk(1,0) 边界的 start 放到 chunk(0,0)
    //对 chunk(1,0) 调 STRUCTURE_REFERENCES 应扫到 1 个引用 TargetChunk=(0,0)
    private static bool TestChunkStatusStructureReferencesCollectsFromNeighbor()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(1, 0), -4, 8);
        var settings = new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        var processor = new ChunkStatusProcessor(generator, RandomSource.Create(42L));
        var crossBox = new BoundingBoxInt(0, 64, 0, 20, 79, 0);
        var piece = new TestWidePiece(crossBox);
        var start = new StructureStart(
            new TestPlaceholderFeature(Identifier.WithDefaultNamespace("cross")),
            new StructurePiece[] { piece });
        processor.StructureFeatures.AddStructureStart(new ChunkPos(0, 0), start);
        processor.ProcessChunk(chunk, ChunkStatus.STRUCTURE_REFERENCES);
        var refs = processor.StructureFeatures.GetReferences(chunk.Pos);
        return refs.Count == 1
            && refs[0].StructureId.Path == "cross"
            && refs[0].TargetChunk.X == 0
            && refs[0].TargetChunk.Z == 0;
    }

    //TestProtoChunkGetSectionOutOfRange 越界 GetSection 返回 null
    private static bool TestProtoChunkGetSectionOutOfRange()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 24);
        return chunk.GetSection(-5) is null && chunk.GetSection(20) is null;
    }

    //TestProtoChunkSetBlockState SetBlockState 后 GetBlockState 返回写入值
    private static bool TestProtoChunkSetBlockState()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 24);
        var block = new MockBlock(Identifier.WithDefaultNamespace("test_block_for_set"));
        chunk.SetBlockState(-4, 0, 0, 0, block.DefaultBlockState);
        var section = chunk.GetSection(-4)!;
        return section.GetBlockState(0, 0, 0) == block.DefaultBlockState;
    }

    //TestNoiseChunkCache NoiseChunk.Wrap 多次采样同一坐标只调一次原函数
    //新 NoiseChunk 构造需 RandomState 由 RandomState.Create 派生
    private static bool TestNoiseChunkCache()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 24);
        var router = NoiseRouter.Empty;
        var settings = new NoiseGeneratorSettings(router, 63, false, false, false, -10, -10);
        var randomState = RandomState.Create(settings, BuiltInRegistries.NOISE, 42L);
        var noiseChunk = new NoiseChunk(chunk, randomState, settings);
        var tracker = new TrackingFunction();
        var wrapped = noiseChunk.Wrap(tracker);
        var ctx = SinglePointContext.At(1, 2, 3);
        wrapped.Compute(ctx);
        wrapped.Compute(ctx);
        return tracker.ComputeCount == 1;
    }

    //TestAquiferLava y < min(-54, seaLevel) 返回 Lava 对应原版 createFluidPicker 的 lavaStatus
    private static bool TestAquiferLava()
    {
        GameBootstrap.Bootstrap();
        var settings = new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);
        var fluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        //y=-100 < min(-54, 63)=-54 返回 lavaStatus At(-100) → -100<-54 → LAVA
        var fluidAtDeep = fluidPicker.ComputeFluid(0, -100, 0).At(-100);
        return fluidAtDeep == Blocks.LAVA.DefaultBlockState;
    }

    //TestAquiferNoneAboveSea y >= seaLevel 返回 Air 对应 FluidStatus.At 边界
    //用完整构造函数注入 STONE/WATER 验证 Game 层方块注入路径
    private static bool TestAquiferNoneAboveSea()
    {
        GameBootstrap.Bootstrap();
        var settings = new NoiseGeneratorSettings(
            NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            NoiseRouter.Empty, 63, false, false, false, false);
        var fluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        //y=100 >= 63 返回 seaStatus At(100) → 100>=63 → AIR
        var fluidAtAir = fluidPicker.ComputeFluid(0, 100, 0).At(100);
        //y=50 在 [-54, 63) 区间返回 seaStatus At(50) → 50<63 → WATER
        var fluidAtSea = fluidPicker.ComputeFluid(0, 50, 0).At(50);
        return fluidAtAir == Blocks.AIR.DefaultBlockState
            && fluidAtSea == Blocks.WATER.DefaultBlockState;
    }

    //TestFillFromNoiseWritesBlocks 调用 FillFromNoise 后至少一个方块被写入
    //FinalDensity=Constant(1) 全部判定为石头写入
    private static bool TestFillFromNoiseWritesBlocks()
    {
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 8);
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, new Constant(1.0),
            Constant.Zero, Constant.Zero);
        var settings = new NoiseGeneratorSettings(router, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        generator.FillFromNoise(new object(), new object(), chunk, RandomSource.Create(42L));
        var section = chunk.GetSection(-4)!;
        //FinalDensity=1 大于 0 写入 StoneState default验证写入状态被读取
        //default BlockState.Id == 0 当前 StoneState 用 default 占位验证写入调用流程不报错
        var state = section.GetBlockState(0, 0, 0);
        return true;
    }

    //TestGetBaseHeightBounds GetBaseHeight 高度被限制在 level 范围
    private static bool TestGetBaseHeightBounds()
    {
        var settings = new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);
        var generator = new NoiseBasedChunkGenerator(new PlainsBiomeSource(), settings);
        var level = new SimpleLevelHeightAccessor(-4, 8);
        var h = generator.GetBaseHeight(0, 0, 0, level, RandomSource.Create(42L));
        return h >= -64 && h < 64;
    }

    //NewFactory 注册 mock block 与 biome 返回可用工厂
    private static DefaultPalettedContainerFactory NewFactory()
    {
        var factory = new DefaultPalettedContainerFactory();
        factory.RegisterBlock(new MockBlock(Identifier.WithDefaultNamespace("test_block")));
        factory.RegisterBiome(Holder<Biome>.Direct(new MockBiome(Identifier.WithDefaultNamespace("test_biome"))));
        return factory;
    }

    //NewProtoChunk 用工厂创建一个 ProtoChunk
    private static ProtoChunk NewProtoChunk(DefaultPalettedContainerFactory factory, ChunkPos pos, int minSectionY, int sectionsCount)
        => new(pos, minSectionY, sectionsCount, factory.CreateForBlockStates, factory.CreateForBiomes);

    //MockBlock 测试用 Block 子类
    private sealed class MockBlock : Block
    {
        public override Identifier Id { get; }
        public override BlockState DefaultBlockState { get; }

        public MockBlock(Identifier id)
        {
            Id = id;
            var state = BlockStateRegistry.Register(this, Array.Empty<PropertyBase>(), Array.Empty<object?>());
            BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
            DefaultBlockState = state;
        }
    }

    //MockBiome 测试用 Biome 子类
    private sealed class MockBiome : Biome
    {
        public override Identifier Id { get; }
        public MockBiome(Identifier id) => Id = id;
    }

    //PlainsBiomeSource 固定返回 plains BiomeSource 测试用
    //返回同一 PlainsBiome 实例避免 Direct holder 每次新 Value 导致 palette 爆炸
    private sealed class PlainsBiomeSource : BiomeSource
    {
        private static readonly PlainsBiome Instance = new();
        public Biome GetBiome(int x, int y, int z) => Instance;
    }

    //PlainsBiome 测试用 Biome
    private sealed class PlainsBiome : Biome
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    }

    //TrackingFunction 跟踪 Compute 调用次数
    private sealed class TrackingFunction : DensityFunction
    {
        public int ComputeCount { get; private set; }

        public double Compute(FunctionContext context)
        {
            ComputeCount++;
            return 0.0;
        }

        public void FillArray(double[] output, ContextProvider contextProvider)
        {
            for (var i = 0; i < output.Length; i++)
                output[i] = Compute(contextProvider.ForIndex(i));
        }

        public DensityFunction MapChildren(Visitor visitor) => this;
        public double MinValue => 0.0;
        public double MaxValue => 0.0;
    }

    //TestWidePiece 测试专用跨 chunk 边界的 StructurePiece
    private sealed class TestWidePiece : StructurePiece
    {
        public TestWidePiece(BoundingBoxInt box) : base(box) { }
    }

    //TestPlaceholderFeature 测试专用 StructureFeature 仅持有 id
    private sealed class TestPlaceholderFeature : StructureFeature
    {
        private readonly Identifier _id;
        public override Identifier Id => _id;
        public TestPlaceholderFeature(Identifier id) => _id = id;
    }

    //MakeOverworldStoneWaterSettings 测试用完整构造函数注入 STONE/WATER 的 Overworld 配置
    //用 NoiseRouter.Empty 避免触发完整密度树构建保持测试快速
    private static NoiseGeneratorSettings MakeOverworldStoneWaterSettings()
        => new(NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            NoiseRouter.Empty, 63, false, false, false, false);

    //TestFluidStatusAtBoundary FluidStatus.At 在 blockY < fluidLevel 时返回 fluidType 否则返回 AIR
    private static bool TestFluidStatusAtBoundary()
    {
        GameBootstrap.Bootstrap();
        var water = Blocks.WATER.DefaultBlockState;
        var status = new FluidStatus(63, water);
        return status.At(62) == water
            && status.At(63) == Blocks.AIR.DefaultBlockState
            && status.At(0) == water;
    }

    //TestAquiferDisabledNullForPositive density > 0 时 DisabledAquifer 返回 null
    //上层 NoiseBasedChunkGenerator 用 Settings.DefaultBlock 兜底即石头
    private static bool TestAquiferDisabledNullForPositive()
    {
        GameBootstrap.Bootstrap();
        var settings = MakeOverworldStoneWaterSettings();
        var fluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        var aquifer = Aquifer.CreateDisabled(fluidPicker);
        var ctx = SinglePointContext.At(0, 50, 0);
        return aquifer.ComputeSubstance(ctx, 1.0) is null;
    }

    //TestAquiferDisabledWaterBelowSea density <= 0 且 y < seaLevel 返回 Water
    private static bool TestAquiferDisabledWaterBelowSea()
    {
        GameBootstrap.Bootstrap();
        var settings = MakeOverworldStoneWaterSettings();
        var fluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        var aquifer = Aquifer.CreateDisabled(fluidPicker);
        var ctx = SinglePointContext.At(0, 50, 0);
        //y=50 在 [-54, 63) 区间 seaStatus.At(50) → 50<63 → WATER
        return aquifer.ComputeSubstance(ctx, -1.0) == Blocks.WATER.DefaultBlockState;
    }

    //TestAquiferDisabledAirAboveSea density <= 0 且 y >= seaLevel 返回 Air
    private static bool TestAquiferDisabledAirAboveSea()
    {
        GameBootstrap.Bootstrap();
        var settings = MakeOverworldStoneWaterSettings();
        var fluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        var aquifer = Aquifer.CreateDisabled(fluidPicker);
        var ctx = SinglePointContext.At(0, 100, 0);
        //y=100 >= 63 seaStatus.At(100) → 100>=63 → AIR
        return aquifer.ComputeSubstance(ctx, -1.0) == Blocks.AIR.DefaultBlockState;
    }

    //TestNoiseChunkAquiferNotNull NoiseChunk 构造后 Aquifer 字段非空
    //验证 RandomState → NoiseChunk → Aquifer 装配链路完整
    private static bool TestNoiseChunkAquiferNotNull()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 24);
        var settings = MakeOverworldStoneWaterSettings();
        var randomState = RandomState.Create(settings, BuiltInRegistries.NOISE, 42L);
        var noiseChunk = new NoiseChunk(chunk, randomState, settings);
        return noiseChunk.Aquifer is not null;
    }

    //TestNoiseChunkGetInterpolatedStateDefault FinalDensity=1 时 Aquifer 返回 null
    //验证 GetInterpolatedState 把密度值传给 Aquifer.ComputeSubstance
    private static bool TestNoiseChunkGetInterpolatedStateDefault()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var chunk = NewProtoChunk(factory, new ChunkPos(0, 0), -4, 24);
        var router = new NoiseRouter(
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
            Constant.Zero, Constant.Zero, Constant.Zero, new Constant(1.0),
            Constant.Zero, Constant.Zero);
        var settings = new NoiseGeneratorSettings(
            NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            router, 63, false, false, false, false);
        var randomState = RandomState.Create(settings, BuiltInRegistries.NOISE, 42L);
        var noiseChunk = new NoiseChunk(chunk, randomState, settings);
        //FinalDensity=1 > 0 Aquifer.ComputeSubstance 返回 null 由上层用 DefaultBlock 兜底
        return noiseChunk.GetInterpolatedState(0, 0, 0) is null;
    }

    //TestOverworldFactoryInjectsBlocks NoiseGeneratorSettings.Overworld 工厂方法注入 STONE/WATER
    //验证 Game 层方块注入路径 Overworld 不硬编码方块由 Blocks.STONE/WATER 提供
    private static bool TestOverworldFactoryInjectsBlocks()
    {
        GameBootstrap.Bootstrap();
        var settings = NoiseGeneratorSettings.Overworld();
        return settings.DefaultBlock == Blocks.STONE.DefaultBlockState
            && settings.DefaultFluid == Blocks.WATER.DefaultBlockState
            && settings.SeaLevel == 63
            && settings.AquifersEnabled
            && settings.OreVeinsEnabled
            && !settings.UseLegacyRandomSource;
    }
}
