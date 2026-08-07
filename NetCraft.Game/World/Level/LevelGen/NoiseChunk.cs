using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseChunk 区块噪声上下文对应原版 net.minecraft.world.level.levelgen.NoiseChunk
//为区块内每个密度函数提供缓存包装避免重复计算
//持 RandomState 与 Aquifer 实例FillFromNoise 通过 GetInterpolatedState 驱动密度采样与方块决策
//P0 简化版不做跨 cell 三线性插值直接按 blockX/Y/Z 采样 FinalDensity 留好接口供 P1 升级插值算法
public sealed class NoiseChunk
{
    public ChunkAccess Chunk { get; }
    public RandomState RandomState { get; }
    public NoiseGeneratorSettings Settings { get; }
    public NoiseRouter Router { get; }
    public Aquifer Aquifer { get; }

    //DensityCache 每个坐标的密度值缓存键为区块内局部坐标
    private readonly Dictionary<int, double> _densityCache = new();
    private readonly DensityFunction _wrappedFinal;

    //主构造函数接收 RandomState 由 NoiseBasedChunkGenerator.FillFromNoise 调用
    //包装 FinalDensity 加区块内缓存根据 AquifersEnabled 决定 Aquifer 实例类型
    public NoiseChunk(ChunkAccess chunk, RandomState randomState, NoiseGeneratorSettings settings)
    {
        Chunk = chunk;
        RandomState = randomState;
        Settings = settings;
        Router = randomState.Router;
        _wrappedFinal = Wrap(Router.FinalDensity);
        Aquifer = CreateAquifer(settings, this, chunk.Pos, randomState);
    }

    //CreateAquifer 按 settings.AquifersEnabled 决定使用完整含水层还是禁用版对应原版 NoiseChunk 构造中的分支
    //P0 阶段 NoiseBasedAquifer 内部委托 DisabledAquifer 故两个分支行为等价留分支便于 P1 升级
    private static Aquifer CreateAquifer(NoiseGeneratorSettings settings, NoiseChunk noiseChunk, ChunkPos pos, RandomState randomState)
    {
        var globalFluidPicker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);
        if (!settings.AquifersEnabled)
            return Aquifer.CreateDisabled(globalFluidPicker);
        var noiseSettings = settings.NoiseSettings;
        var cellHeight = noiseSettings.GetCellHeight();
        var minBlockY = noiseSettings.MinY;
        var yBlockSize = noiseSettings.Height;
        return Aquifer.Create(noiseChunk, pos, randomState.Router, randomState.AquiferRandom,
            minBlockY, yBlockSize, globalFluidPicker);
    }

    //Wrap 包装 DensityFunction 加入缓存逻辑对应原版 NoiseChunk.wrap
    //包装后 Compute 先查缓存未命中再调用原函数
    public DensityFunction Wrap(DensityFunction function)
        => new WrappedDensityFunction(this, function);

    //GetOrCompute 按区块内坐标查缓存或计算
    public double GetOrCompute(DensityFunction function, int blockX, int blockY, int blockZ)
    {
        var key = PackKey(blockX, blockY, blockZ);
        if (_densityCache.TryGetValue(key, out var cached))
            return cached;
        var ctx = new SinglePointContext(blockX, blockY, blockZ);
        var value = function.Compute(ctx);
        _densityCache[key] = value;
        return value;
    }

    //GetInterpolatedState 按 blockX/Y/Z 采样最终密度并由 Aquifer 决定方块状态对应原版 getInterpolatedState
    //P0 简化版不做 cell 内 trilinear 插值直接按 blockX/Y/Z 采样 FinalDensity
    //密度>0 时 Aquifer.ComputeSubstance 返回 null由上层用 Settings.DefaultBlock 兜底
    public BlockState? GetInterpolatedState(int blockX, int blockY, int blockZ)
    {
        var ctx = new SinglePointContext(blockX, blockY, blockZ);
        var density = _wrappedFinal.Compute(ctx);
        return Aquifer.ComputeSubstance(ctx, density);
    }

    //PreliminarySurfaceLevel 采样指定 x/z 的初步地表高度对应原版 preliminarySurfaceLevel
    //P0 简化版返回 Settings.SeaLevel 占位真实实现需采样 PreliminarySurface 密度函数与 noiseData
    public int PreliminarySurfaceLevel(int blockX, int blockZ) => Settings.SeaLevel;

    //PackKey 把区块内 0..15 坐标打包为单一 int x/z 4 位y 8 位
    private static int PackKey(int x, int y, int z)
        => ((x & 0xF) << 12) | ((z & 0xF) << 8) | (y & 0xFF);

    //WrappedDensityFunction 包装 DensityFunction 加缓存
    private sealed class WrappedDensityFunction : DensityFunction
    {
        private readonly NoiseChunk _owner;
        private readonly DensityFunction _delegate;

        public WrappedDensityFunction(NoiseChunk owner, DensityFunction function)
        {
            _owner = owner;
            _delegate = function;
        }

        public double Compute(FunctionContext context)
            => _owner.GetOrCompute(_delegate, context.BlockX, context.BlockY, context.BlockZ);

        public void FillArray(double[] output, ContextProvider contextProvider)
        {
            for (var i = 0; i < output.Length; i++)
                output[i] = Compute(contextProvider.ForIndex(i));
        }

        public DensityFunction MapChildren(Visitor visitor)
            => new WrappedDensityFunction(_owner, _delegate.MapChildren(visitor));

        public DensityFunction MapAll(Visitor visitor)
            => new WrappedDensityFunction(_owner, _delegate.MapAll(visitor));

        public double MinValue => _delegate.MinValue;
        public double MaxValue => _delegate.MaxValue;
    }
}
