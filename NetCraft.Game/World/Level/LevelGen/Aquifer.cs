using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//Aquifer 含水层接口对应原版 net.minecraft.world.level.levelgen.Aquifer
//根据 NoiseRouter 的 Barrier/FluidLevelFloodedness/FluidLevelSpread/Lava 与 globalFluidPicker 决定每格方块状态
//两种实现 NoiseBasedAquifer 完整含水层 DisabledAquifer 简化版密度>0 返回 null 否则返回全局流体
public interface Aquifer
{
    //FluidPicker 流体选择器按坐标返回 FluidStatus 对应原版 Aquifer.FluidPicker
    public interface FluidPicker
    {
        FluidStatus ComputeFluid(int blockX, int blockY, int blockZ);
    }

    //ComputeSubstance 按上下文与密度值返回方块状态密度>0 通常返回 null 由上层用 DefaultBlock 兜底
    BlockState? ComputeSubstance(FunctionContext context, double density);

    //ShouldScheduleFluidUpdate 是否需要调度流体更新对应原版 shouldScheduleFluidUpdate
    bool ShouldScheduleFluidUpdate();

    //Create 完整含水层工厂对应原版 create
    //P0 阶段 NoiseBasedAquifer 内部委托 DisabledAquifer 留好构造签名便于 P1 补全含水层网格算法
    static Aquifer Create(NoiseChunk noiseChunk, ChunkPos pos, NoiseRouter router,
        PositionalRandomFactory positionalRandomFactory, int minBlockY, int yBlockSize,
        FluidPicker globalFluidPicker)
        => new NoiseBasedAquifer(noiseChunk, pos, router, positionalRandomFactory, minBlockY, yBlockSize, globalFluidPicker);

    //CreateDisabled 禁用含水层工厂对应原版 createDisabled
    //密度>0 返回 null 否则返回 globalFluidPicker 在该坐标的 FluidStatus.At(blockY)
    static Aquifer CreateDisabled(FluidPicker fluidRule) => new DisabledAquifer(fluidRule);
}

//FluidStatus 流体状态记录流体液面高度与方块状态对应原版 Aquifer.FluidStatus
//At(blockY) 当 blockY 小于 fluidLevel 返回 fluidType 否则返回 AIR
public sealed class FluidStatus
{
    public int FluidLevel { get; }
    public BlockState FluidType { get; }

    public FluidStatus(int fluidLevel, BlockState fluidType)
    {
        FluidLevel = fluidLevel;
        FluidType = fluidType;
    }

    //At 按坐标 y 决定返回 fluidType 还是 AIR 对应原版 at
    public BlockState At(int blockY)
        => blockY < FluidLevel ? FluidType : Blocks.AIR.DefaultBlockState;

    public override bool Equals(object? obj)
        => obj is FluidStatus s && s.FluidLevel == FluidLevel && s.FluidType == FluidType;

    public override int GetHashCode() => HashCode.Combine(FluidLevel, FluidType);

    public static bool operator ==(FluidStatus a, FluidStatus b)
        => a.FluidLevel == b.FluidLevel && a.FluidType == b.FluidType;

    public static bool operator !=(FluidStatus a, FluidStatus b) => !(a == b);
}

//DisabledAquifer 禁用含水层对应原版 Aquifer.createDisabled 匿名实现
//密度>0 返回 null 由 NoiseBasedChunkGenerator 用 Settings.DefaultBlock 兜底
//密度<=0 返回 globalFluidPicker 的 FluidStatus.At(blockY)
internal sealed class DisabledAquifer : Aquifer
{
    private readonly Aquifer.FluidPicker _fluidRule;

    public DisabledAquifer(Aquifer.FluidPicker fluidRule)
    {
        _fluidRule = fluidRule;
    }

    public BlockState? ComputeSubstance(FunctionContext context, double density)
    {
        if (density > 0.0) return null;
        return _fluidRule.ComputeFluid(context.BlockX, context.BlockY, context.BlockZ).At(context.BlockY);
    }

    public bool ShouldScheduleFluidUpdate() => false;
}

//NoiseBasedAquifer 噪声含水层对应原版 Aquifer.NoiseBasedAquifer
//P0 阶段简化实现委托 DisabledAquifer 留好字段与构造签名便于 P1 阶段补全含水层网格算法
//完整算法需 barrierNoise/fluidLevelFloodednessNoise/fluidLevelSpreadNoise/lavaNoise 与 8 邻居 AquiferCache 网格扫描
//依赖 BlockPos/SectionPos/Mth/DynamicGraphMinFixedPoint/OverworldBiomeBuilder 等子系统就绪后补全
public sealed class NoiseBasedAquifer : Aquifer
{
    private readonly DisabledAquifer _fallback;
    private readonly NoiseChunk _noiseChunk;
    private readonly ChunkPos _pos;
    private readonly NoiseRouter _router;
    private readonly PositionalRandomFactory _positionalRandomFactory;
    private readonly int _minBlockY;
    private readonly int _yBlockSize;
    private readonly Aquifer.FluidPicker _globalFluidPicker;

    public NoiseBasedAquifer(NoiseChunk noiseChunk, ChunkPos pos, NoiseRouter router,
        PositionalRandomFactory positionalRandomFactory, int minBlockY, int yBlockSize,
        Aquifer.FluidPicker globalFluidPicker)
    {
        _noiseChunk = noiseChunk;
        _pos = pos;
        _router = router;
        _positionalRandomFactory = positionalRandomFactory;
        _minBlockY = minBlockY;
        _yBlockSize = yBlockSize;
        _globalFluidPicker = globalFluidPicker;
        _fallback = new DisabledAquifer(globalFluidPicker);
    }

    public BlockState? ComputeSubstance(FunctionContext context, double density)
        => _fallback.ComputeSubstance(context, density);

    public bool ShouldScheduleFluidUpdate() => _fallback.ShouldScheduleFluidUpdate();
}
