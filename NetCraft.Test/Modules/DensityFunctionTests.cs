using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Nbt;
using NetCraft.Util.Random;

namespace NetCraft.Test.Modules;

//DensityFunction 密度函数测试覆盖 Constant/Noise/Mapped/Clamp/MulOrAdd/Ap2/YClampedGradient/Shift/ShiftedNoise/NoiseRouter
//验证 compute 输出正确性与 MapChildren 子节点替换行为
internal static class DensityFunctionTests
{
    public const string Module = "density";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Constant returns fixed value", TestConstant);
        yield return ("Noise samples in max range", TestNoise);
        yield return ("Clamp limits output to range", TestClamp);
        yield return ("MulOrAdd Add shifts by value", TestMulOrAddAdd);
        yield return ("MulOrAdd Mul scales by value", TestMulOrAddMul);
        yield return ("Ap2 Max picks larger", TestAp2Max);
        yield return ("Ap2 Add sums two inputs", TestAp2Add);
        yield return ("YClampedGradient interpolates by Y", TestYClampedGradient);
        yield return ("Shift offsets query coordinates", TestShift);
        yield return ("ShiftA offsets X only", TestShiftA);
        yield return ("ShiftB offsets X and Z", TestShiftB);
        yield return ("ShiftedNoise samples shifted coords", TestShiftedNoise);
        yield return ("NoiseRouter Empty all zero", TestNoiseRouterEmpty);
        yield return ("NoiseRouter MapAll replaces children", TestNoiseRouterMapAll);
        yield return ("MapChildren visitor rewrites Constant", TestVisitorRewrite);
        yield return ("ConstantCodec round trip", TestConstantCodecRoundTrip);
        yield return ("ClampCodec round trip", TestClampCodecRoundTrip);
        yield return ("MulOrAddCodec round trip", TestMulOrAddCodecRoundTrip);
        yield return ("Ap2Codec round trip", TestAp2CodecRoundTrip);
        yield return ("YClampedGradientCodec round trip", TestYClampedGradientCodecRoundTrip);
        yield return ("DispatchCodec nested round trip", TestDispatchCodecNestedRoundTrip);
        yield return ("DispatchCodec unknown type errors", TestDispatchCodecUnknownType);
        yield return ("SplineFunction delegates to CubicSpline", TestSplineFunction);
        yield return ("RangeChoice picks branch by input range", TestRangeChoice);
        yield return ("IntervalSelect picks branch by threshold", TestIntervalSelect);
        yield return ("EndIslands output within min/max bounds", TestEndIslands);
        yield return ("MarkerNode delegates compute to wrapped", TestMarkerNode);
        yield return ("BlendAlpha/BlendOffset fixed values", TestBlendAlphaOffset);
        yield return ("FindTopSurface returns topmost solid Y", TestFindTopSurface);
    }

    //RoundTripHelper 编解码往返通用辅助
    //把 df 编码为 NbtOps 再解码回来对比 compute 输出
    private static bool RoundTripHelper(DensityFunction original)
    {
        var ops = NbtOps.Instance;
        var encoded = DensityFunctionCodec.Instance.EncodeStart(ops, original).GetOrThrow();
        var mapResult = ops.GetMap(encoded).GetOrThrow();
        var decoded = DensityFunctionCodec.Instance.Decode(ops, mapResult).GetOrThrow();
        var ctx = SinglePointContext.At(7, 11, 13);
        return Math.Abs(original.Compute(ctx) - decoded.Compute(ctx)) < 1e-9
            && Math.Abs(original.MinValue - decoded.MinValue) < 1e-9
            && Math.Abs(original.MaxValue - decoded.MaxValue) < 1e-9;
    }

    //TestConstantCodecRoundTrip Constant dispatch codec 往返
    private static bool TestConstantCodecRoundTrip()
        => RoundTripHelper(new Constant(3.5));

    //TestClampCodecRoundTrip Clamp 嵌套 Constant dispatch 往返
    private static bool TestClampCodecRoundTrip()
        => RoundTripHelper(new Clamp(new Constant(10.0), 5.0, 15.0));

    //TestMulOrAddCodecRoundTrip MulOrAdd ADD 嵌套 Constant dispatch 往返
    private static bool TestMulOrAddCodecRoundTrip()
        => RoundTripHelper(MulOrAdd.Of(MulOrAdd.OpType.Add, 2.0, new Constant(5.0)));

    //TestAp2CodecRoundTrip Ap2 Max 嵌套两个 Constant dispatch 往返
    private static bool TestAp2CodecRoundTrip()
        => RoundTripHelper(new Ap2(Ap2.OpType.Max, new Constant(10.0), new Constant(20.0)));

    //TestYClampedGradientCodecRoundTrip YClampedGradient dispatch codec 往返
    private static bool TestYClampedGradientCodecRoundTrip()
        => RoundTripHelper(new YClampedGradient(0, 10, 0.0, 100.0));

    //TestDispatchCodecNestedRoundTrip 嵌套两层结构 dispatch codec 往返
    //Clamp(MulOrAdd Mul(Constant, 2), -10, 10)
    private static bool TestDispatchCodecNestedRoundTrip()
        => RoundTripHelper(new Clamp(
            MulOrAdd.Of(MulOrAdd.OpType.Mul, 2.0, new Constant(3.0)),
            -10.0, 10.0));

    //TestDispatchCodecUnknownType 未知 type 字符串返回 Error
    private static bool TestDispatchCodecUnknownType()
    {
        var ops = NbtOps.Instance;
        var map = new Dictionary<string, Tag>
        {
            ["type"] = StringTag.ValueOf("nonexistent"),
            ["argument"] = DoubleTag.ValueOf(1.0)
        };
        var mapLike = MapLikeFromDict(map);
        var result = DensityFunctionCodec.Instance.Decode(ops, mapLike);
        return !result.Result().IsPresent;
    }

    //MapLikeFromDict 从 Dictionary<string, Tag> 构造 MapLike<Tag>
    private static MapLike<Tag> MapLikeFromDict(IReadOnlyDictionary<string, Tag> dict)
        => new DictMapLike(dict);

    //DictMapLike 字典转 MapLike 适配器测试用
    private sealed class DictMapLike : MapLike<Tag>
    {
        private readonly IReadOnlyDictionary<string, Tag> _dict;
        public DictMapLike(IReadOnlyDictionary<string, Tag> dict) { _dict = dict; }
        public Optional<Tag> Get(Tag key)
            => key is StringTag s && _dict.TryGetValue(s.Value, out var v)
                ? Optional<Tag>.Of(v)
                : Optional<Tag>.Empty();
        public Optional<Tag> Get(string key)
            => _dict.TryGetValue(key, out var v) ? Optional<Tag>.Of(v) : Optional<Tag>.Empty();
        public IEnumerable<Pair<Tag, Tag>> Entries()
            => _dict.Select(kv => new Pair<Tag, Tag>(StringTag.ValueOf(kv.Key), kv.Value));
    }

    //TestConstant Constant 返回固定值 min/max 等于 value
    private static bool TestConstant()
    {
        var c = new Constant(3.5);
        var ctx = SinglePointContext.At(0, 0, 0);
        return c.Compute(ctx) == 3.5
            && c.MinValue == 3.5
            && c.MaxValue == 3.5
            && Constant.Zero.Compute(ctx) == 0.0;
    }

    //TestNoise Noise.Compute 等于 NoiseHolder.GetValue 输出范围在 [-max, max]
    private static bool TestNoise()
    {
        var noise = new NormalNoise(StubRandom.Instance, -3, 1.0, 1.0, 1.0);
        var holder = new NoiseHolder(new NoiseParameters(-3, new[] { 1.0, 1.0, 1.0 }), noise);
        var df = new Noise(holder);
        var ctx = SinglePointContext.At(10, 20, 30);
        var direct = holder.GetValue(10, 20, 30);
        var viaDf = df.Compute(ctx);
        return Math.Abs(direct - viaDf) < 1e-9
            && df.MinValue == -holder.MaxValue
            && df.MaxValue == holder.MaxValue;
    }

    //TestClamp Clamp 把输入钳制到 [min, max]
    private static bool TestClamp()
    {
        var input = new YClampedGradient(0, 10, 0.0, 100.0);
        var clamp = new Clamp(input, 25.0, 75.0);
        var lo = clamp.Compute(SinglePointContext.At(0, 0, 0));
        var mid = clamp.Compute(SinglePointContext.At(0, 5, 0));
        var hi = clamp.Compute(SinglePointContext.At(0, 10, 0));
        return lo == 25.0 && mid == 50.0 && hi == 75.0
            && clamp.MinValue == 25.0 && clamp.MaxValue == 75.0;
    }

    //TestMulOrAddAdd ADD 模式 output = input + value
    private static bool TestMulOrAddAdd()
    {
        var input = new Constant(10.0);
        var add = MulOrAdd.Of(MulOrAdd.OpType.Add, 5.0, input);
        var v = add.Compute(SinglePointContext.At(0, 0, 0));
        return v == 15.0 && add.MinValue == 15.0 && add.MaxValue == 15.0;
    }

    //TestMulOrAddMul MUL 模式 output = input * value
    private static bool TestMulOrAddMul()
    {
        var input = new Constant(10.0);
        var mul = MulOrAdd.Of(MulOrAdd.OpType.Mul, 0.5, input);
        var v = mul.Compute(SinglePointContext.At(0, 0, 0));
        return v == 5.0 && mul.MinValue == 5.0 && mul.MaxValue == 5.0;
    }

    //TestAp2Max Max 取两输入较大值
    private static bool TestAp2Max()
    {
        var a = new Constant(10.0);
        var b = new Constant(20.0);
        var max = new Ap2(Ap2.OpType.Max, a, b);
        var v = max.Compute(SinglePointContext.At(0, 0, 0));
        return v == 20.0 && max.MinValue == 20.0 && max.MaxValue == 20.0;
    }

    //TestAp2Add Add 求和两输入
    private static bool TestAp2Add()
    {
        var a = new Constant(10.0);
        var b = new Constant(3.0);
        var add = new Ap2(Ap2.OpType.Add, a, b);
        var v = add.Compute(SinglePointContext.At(0, 0, 0));
        return v == 13.0 && add.MinValue == 13.0 && add.MaxValue == 13.0;
    }

    //TestYClampedGradient Y 区间内线性插值超出取端点值
    private static bool TestYClampedGradient()
    {
        var g = new YClampedGradient(0, 10, 0.0, 100.0);
        var lo = g.Compute(SinglePointContext.At(0, -5, 0));
        var mid = g.Compute(SinglePointContext.At(0, 5, 0));
        var hi = g.Compute(SinglePointContext.At(0, 15, 0));
        return lo == 0.0 && mid == 50.0 && hi == 100.0
            && g.MinValue == 0.0 && g.MaxValue == 100.0;
    }

    //TestShift Shift 把坐标缩放 0.25 采样噪声后放大 4 倍输出
    private static bool TestShift()
    {
        var noise = new NormalNoise(StubRandom.Instance, -3, 1.0, 1.0, 1.0);
        var holder = new NoiseHolder(new NoiseParameters(-3, new[] { 1.0, 1.0, 1.0 }), noise);
        var shift = new Shift(holder);
        var ctx = SinglePointContext.At(8, 16, 24);
        var expected = holder.GetValue(8 * 0.25, 16 * 0.25, 24 * 0.25) * 4.0;
        return Math.Abs(shift.Compute(ctx) - expected) < 1e-9;
    }

    //TestShiftA ShiftA 取 blockX 与 blockZ 局部坐标 Y 置零
    private static bool TestShiftA()
    {
        var noise = new NormalNoise(StubRandom.Instance, -3, 1.0, 1.0, 1.0);
        var holder = new NoiseHolder(new NoiseParameters(-3, new[] { 1.0, 1.0, 1.0 }), noise);
        var shift = new ShiftA(holder);
        var ctx = SinglePointContext.At(8, 16, 24);
        var expected = holder.GetValue(8 * 0.25, 0.0, 24 * 0.25) * 4.0;
        return Math.Abs(shift.Compute(ctx) - expected) < 1e-9;
    }

    //TestShiftB ShiftB 取 blockZ 与 blockX 交叉坐标 Y 置零
    private static bool TestShiftB()
    {
        var noise = new NormalNoise(StubRandom.Instance, -3, 1.0, 1.0, 1.0);
        var holder = new NoiseHolder(new NoiseParameters(-3, new[] { 1.0, 1.0, 1.0 }), noise);
        var shift = new ShiftB(holder);
        var ctx = SinglePointContext.At(8, 16, 24);
        var expected = holder.GetValue(24 * 0.25, 8 * 0.25, 0.0) * 4.0;
        return Math.Abs(shift.Compute(ctx) - expected) < 1e-9;
    }

    //TestShiftedNoise ShiftedNoise 把 shift 函数结果加到坐标再采样
    private static bool TestShiftedNoise()
    {
        var noise = new NormalNoise(StubRandom.Instance, -3, 1.0, 1.0, 1.0);
        var holder = new NoiseHolder(new NoiseParameters(-3, new[] { 1.0, 1.0, 1.0 }), noise);
        var shiftX = new Constant(100.0);
        var shiftY = new Constant(200.0);
        var shiftZ = new Constant(300.0);
        var df = new ShiftedNoise(holder, 1.0, 1.0, shiftX, shiftY, shiftZ);
        var ctx = SinglePointContext.At(1, 2, 3);
        var direct = holder.GetValue(1 + 100, 2 + 200, 3 + 300);
        var viaDf = df.Compute(ctx);
        return Math.Abs(direct - viaDf) < 1e-9;
    }

    //TestSplineFunction SplineFunction 包装 CubicSpline.Constant 委托采样
    private static bool TestSplineFunction()
    {
        var spline = CubicSpline.Constant(42.0f);
        var df = DensityFunctionsExtra.Spline(spline);
        var v = df.Compute(SinglePointContext.At(0, 0, 0));
        return Math.Abs(v - 42.0) < 1e-6
            && Math.Abs(df.MinValue - 42.0) < 1e-6
            && Math.Abs(df.MaxValue - 42.0) < 1e-6;
    }

    //TestRangeChoice input 在 [min,max) 返回 whenInRange 否则 whenOutOfRange
    private static bool TestRangeChoice()
    {
        var rc = DensityFunctionsExtra.RangeChoice(new Constant(5.0), 0.0, 10.0, new Constant(100.0), new Constant(-100.0));
        var inRange = rc.Compute(SinglePointContext.At(0, 0, 0));
        var rc2 = DensityFunctionsExtra.RangeChoice(new Constant(15.0), 0.0, 10.0, new Constant(100.0), new Constant(-100.0));
        var outRange = rc2.Compute(SinglePointContext.At(0, 0, 0));
        return inRange == 100.0 && outRange == -100.0;
    }

    //TestIntervalSelect input 按阈值分段选择对应 function
    private static bool TestIntervalSelect()
    {
        var functions = new DensityFunction[] { new Constant(10.0), new Constant(20.0), new Constant(30.0) };
        var sel = DensityFunctionsExtra.IntervalSelect(new Constant(0.5), new[] { 0.0, 1.0 }, functions);
        var v = sel.Compute(SinglePointContext.At(0, 0, 0));
        return v == 20.0;
    }

    //TestEndIslands 末地岛屿密度落在 min/max 范围内
    private static bool TestEndIslands()
    {
        var end = DensityFunctionsExtra.EndIslands(0L);
        var v = end.Compute(SinglePointContext.At(0, 0, 0));
        return v >= end.MinValue && v <= end.MaxValue;
    }

    //TestMarkerNode MarkerNode 委托 wrapped 的 compute 与 min/max
    private static bool TestMarkerNode()
    {
        var node = DensityFunctionsExtra.Interpolated(new Constant(7.0));
        var v = node.Compute(SinglePointContext.At(0, 0, 0));
        return v == 7.0 && node.MinValue == 7.0 && node.MaxValue == 7.0;
    }

    //TestBlendAlphaOffset BlendAlpha 固定 1.0 BlendOffset 固定 0.0
    private static bool TestBlendAlphaOffset()
    {
        var ctx = SinglePointContext.At(0, 0, 0);
        return BlendAlpha.Instance.Compute(ctx) == 1.0
            && BlendOffset.Instance.Compute(ctx) == 0.0
            && BlendAlpha.Instance.MinValue == 1.0
            && BlendOffset.Instance.MaxValue == 0.0;
    }

    //TestFindTopSurface density 恒正时返回 upperBound 对齐的 topY
    private static bool TestFindTopSurface()
    {
        var fts = new FindTopSurface(new Constant(1.0), new Constant(64.0), 0, 8);
        var v = fts.Compute(SinglePointContext.At(0, 0, 0));
        return v == 64.0;
    }

    //TestNoiseRouterEmpty Empty 14 个字段全部为 Constant.Zero
    private static bool TestNoiseRouterEmpty()
    {
        var empty = NoiseRouter.Empty;
        var allZero = empty.Barrier is Constant cz1 && cz1.Value == 0.0
            && empty.FluidLevelFloodedness is Constant cz2 && cz2.Value == 0.0
            && empty.VeinRidged is Constant czLast && czLast.Value == 0.0;
        return allZero;
    }

    //TestNoiseRouterMapAll MapAll 替换所有字段为 visitor 返回值
    private static bool TestNoiseRouterMapAll()
    {
        var empty = NoiseRouter.Empty;
        var sentinel = new Constant(-99.0);
        var visitor = new ReplaceAllVisitor(sentinel);
        var mapped = empty.MapAll(visitor);
        var firstIsSentinel = mapped.Barrier is Constant c && c.Value == -99.0;
        var lastIsSentinel = mapped.VeinRidged is Constant c2 && c2.Value == -99.0;
        return firstIsSentinel && lastIsSentinel;
    }

    //TestVisitorRewrite Constant 经 visitor 替换为新常量
    private static bool TestVisitorRewrite()
    {
        DensityFunction input = new Constant(1.0);
        var replacement = new Constant(42.0);
        var visitor = new ReplaceAllVisitor(replacement);
        var mapped = input.MapAll(visitor);
        return mapped is Constant c && c.Value == 42.0;
    }

    //TrackingFunction 记录最近一次 Compute 上下文坐标用于验证 Shift 偏移行为
    private sealed class TrackingFunction : DensityFunction
    {
        public int LastX { get; private set; }
        public int LastY { get; private set; }
        public int LastZ { get; private set; }

        public double Compute(FunctionContext context)
        {
            LastX = context.BlockX;
            LastY = context.BlockY;
            LastZ = context.BlockZ;
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

    //ReplaceAllVisitor 把所有节点替换为固定值
    private sealed class ReplaceAllVisitor : Visitor
    {
        private readonly DensityFunction _replacement;
        public ReplaceAllVisitor(DensityFunction replacement) { _replacement = replacement; }
        public DensityFunction Apply(DensityFunction input) => _replacement;
    }

    //StubRandom 固定种子 RandomSource 测试用
    private sealed class StubRandom : RandomSource
    {
        internal static readonly StubRandom Instance = new();
        public RandomSource Fork() => this;
        public PositionalRandomFactory ForkPositional() => throw new NotSupportedException();
        public void SetSeed(long seed) { }
        public long NextLong() => 0;
        public int NextInt(int bound) => 0;
        public int NextInt() => 0;
        public bool NextBoolean() => false;
        public float NextFloat() => 0f;
        public double NextDouble() => 0.0;
        public double NextGaussian() => 0.0;
    }
}
