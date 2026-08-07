using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//PerlinNoise 多倍频柏林噪声对应原版 net.minecraft.world.level.levelgen.synth.PerlinNoise
//多个 ImprovedNoise 不同频率叠加形成分形布朗运动 fBm
//amplitudes 控制每倍频权重firstOctave 控制起始倍频
public class PerlinNoise
{
    private const double RoundOff = 3.3554432E7;

    private readonly ImprovedNoise[] _noiseLevels;
    private readonly int _firstOctave;
    private readonly double[] _amplitudes;
    private readonly double _lowestFreqValueFactor;
    private readonly double _lowestFreqInputFactor;
    private readonly double _maxValue;

    //CreateLegacyForBlendedNoise BlendedNoise 专用 legacy 工厂对应原版 createLegacyForBlendedNoise
    //octaves 为 -15..0 或 -7..0 等 IntStream 推导 firstOctave 与 amplitudes(全 1.0)
    public static PerlinNoise CreateLegacyForBlendedNoise(RandomSource random, IEnumerable<int> octaves)
    {
        var (firstOctave, amplitudes) = MakeAmplitudes(new SortedSet<int>(octaves));
        return new PerlinNoise(random, firstOctave, amplitudes, false);
    }

    //CreateLegacyForLegacyNetherBiome 旧版下界生物群系工厂对应原版 createLegacyForLegacyNetherBiome
    //直接使用传入 firstOctave/amplitudes 走 legacy 初始化路径
    public static PerlinNoise CreateLegacyForLegacyNetherBiome(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        => new(random, firstOctave, amplitudes, false);

    //Create 新版工厂对应原版 create(random, firstOctave, amplitudes)
    //走 forkPositional.FromHashOf 派生确定性随机源保证跨实例可复现
    public static PerlinNoise Create(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        => new(random, firstOctave, amplitudes, true);

    //MakeAmplitudes 把 octave 集合推导为 (firstOctave, amplitudes) 对应对齐原版 makeAmplitudes
    //octave 集合通常为负数区间推导出的 amplitudes 数组在对应位置为 1.0 其余为 0
    private static (int FirstOctave, double[] Amplitudes) MakeAmplitudes(ISet<int> octaveSet)
    {
        if (octaveSet.Count == 0)
            throw new ArgumentException("Need some octaves!");
        var sorted = octaveSet.OrderBy(x => x).ToList();
        var lowFreqOctaves = -sorted[0];
        var highFreqOctaves = sorted[^1];
        var octaves = lowFreqOctaves + highFreqOctaves + 1;
        if (octaves < 1)
            throw new ArgumentException("Total number of octaves needs to be >= 1");
        var amplitudes = new double[octaves];
        foreach (var octave in sorted)
            amplitudes[octave + lowFreqOctaves] = 1.0;
        return (-lowFreqOctaves, amplitudes);
    }

    public PerlinNoise(RandomSource random, int firstOctave, params double[] amplitudes)
        : this(random, firstOctave, (IReadOnlyList<double>)amplitudes)
    {
    }

    public PerlinNoise(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        : this(random, firstOctave, amplitudes, false)
    {
    }

    //forceLegacy 强制走 legacy Fork 派生路径对应原版 useNewInitialization=false
    //legacy 路径对齐原版 createLegacyForBlendedNoise/createLegacyForLegacyNetherBiome
    //新版路径用 forkPositional.FromHashOf 派生确定性随机源
    public PerlinNoise(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes, bool forceLegacy)
    {
        _firstOctave = firstOctave;
        _amplitudes = amplitudes.ToArray();
        _noiseLevels = new ImprovedNoise[amplitudes.Count];
        var zeroOctaveIndex = -firstOctave;

        if (forceLegacy)
        {
            //legacy 路径用 random.Fork 派生 zeroOctaveIndex 及以下倍频对齐原版 legacy 初始化
            //zeroOctaveIndex = -firstOctave 即 firstOctave==0 对应的索引位置
            var zeroOctave = new ImprovedNoise(random.Fork());
            if (zeroOctaveIndex >= 0 && zeroOctaveIndex < amplitudes.Count && amplitudes[zeroOctaveIndex] != 0.0)
                _noiseLevels[zeroOctaveIndex] = zeroOctave;

            //从 zeroOctaveIndex-1 向下补全负倍频amplitudes 为 0 时跳过一次 random 推进
            for (var i = zeroOctaveIndex - 1; i >= 0; i--)
            {
                if (i < amplitudes.Count)
                {
                    if (amplitudes[i] != 0.0)
                        _noiseLevels[i] = new ImprovedNoise(random.Fork());
                    else
                        random.ConsumeCount(262);
                }
                else
                {
                    random.ConsumeCount(262);
                }
            }
        }
        else
        {
            //新版路径用 forkPositional.FromHashOf 派生确定性随机源对应原版 fromHashOf("octave_"+(firstOctave+i))
            var positional = random.ForkPositional();
            for (var i = 0; i < amplitudes.Count; i++)
            {
                if (amplitudes[i] != 0.0)
                    _noiseLevels[i] = new ImprovedNoise(positional.FromHashOf("octave_" + (firstOctave + i)));
            }
        }

        _lowestFreqInputFactor = Math.Pow(2.0, -zeroOctaveIndex);
        _lowestFreqValueFactor = Math.Pow(2.0, amplitudes.Count - 1) / (Math.Pow(2.0, amplitudes.Count) - 1.0);
        _maxValue = EdgeValue(2.0);
    }

    //GetValue 3 参数采样对应原版 getValue(x,y,z) 委托 5 参数重载 yScale=0 yFudge=0
    public double GetValue(double x, double y, double z)
        => GetValue(x, y, z, 0.0, 0.0);

    //GetValue 5 参数采样对应原版 getValue(x,y,z,yScale,yFudge)
    //BlendedNoise 调用时传入 yScale/yFudge 实现低倍频 y 方向阶梯偏移
    public double GetValue(double x, double y, double z, double yScale, double yFudge)
    {
        var value = 0.0;
        var factor = _lowestFreqInputFactor;
        var valueFactor = _lowestFreqValueFactor;
        for (var i = 0; i < _noiseLevels.Length; i++)
        {
            var noise = _noiseLevels[i];
            if (noise is not null)
            {
                var noiseVal = noise.Noise(Wrap(x * factor), Wrap(y * factor), Wrap(z * factor), yScale * factor, yFudge * factor);
                value += _amplitudes[i] * noiseVal * valueFactor;
            }
            factor *= 2.0;
            valueFactor /= 2.0;
        }
        return value;
    }

    public double MaxValue => _maxValue;

    //MaxBrokenValue 给定 yScale 下的最大值对应原版 maxBrokenValue
    //BlendedNoise 用 yMultiplier 作为 yScale 计算实际最大值
    public double MaxBrokenValue(double yScale)
        => EdgeValue(yScale + 2.0);

    //GetOctaveNoise 取第 i 倍频(高到低索引)对应原版 getOctaveNoise
    public ImprovedNoise GetOctaveNoise(int i)
        => _noiseLevels[_noiseLevels.Length - 1 - i];

    //EdgeValue 按累加 valueFactor 计算给定 noiseValue 的边界值对应原版 edgeValue
    //原版遍历所有非空 noiseLevels 用 amplitudes[i]*noiseValue*valueFactor 累加
    private double EdgeValue(double noiseValue)
    {
        var value = 0.0;
        var valueFactor = _lowestFreqValueFactor;
        for (var i = 0; i < _noiseLevels.Length; i++)
        {
            if (_noiseLevels[i] is not null)
                value += _amplitudes[i] * noiseValue * valueFactor;
            valueFactor /= 2.0;
        }
        return value;
    }

    //wrap 防止大坐标精度丢失对齐原版 wrap
    public static double Wrap(double x)
        => x - Math.Floor(x / RoundOff + 0.5) * RoundOff;
}
