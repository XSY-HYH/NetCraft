using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//Climate 多噪声气候参数系统对应原版 net.minecraft.world.level.levelgen.Climate
//持有 6 个维度参数temperature/humidity/continentalness/erosion/depth/weirdness
//MultiNoiseBiomeSource 用 ParameterPoint 与目标点距离查找最近 Biome
public static class Climate
{
    //Parameter 单维度参数范围对应原版 Climate.Parameter
    //持有 min/max 表示参数取值范围用于参数空间距离计算
    public readonly struct Parameter
    {
        public long Min { get; }
        public long Max { get; }

        public Parameter(long min, long max)
        {
            Min = min;
            Max = max;
        }

        //Single 单值参数 min == max
        public static Parameter Single(long value) => new(value, value);

        //Value 中心值用于参数空间距离计算对应原版 parameter.spaceToBlock
        public long Value => (Min + Max) >> 1;

        //Range 参数范围跨度
        public long Range => Max - Min;

        public override string ToString() => Min == Max ? $"[{Min}]" : $"[{Min}..{Max}]";
    }

    //ParameterPoint 多维度参数点对应原版 Climate.ParameterPoint
    //持有 6 个 Climate.Parameter 与 offset 用于 MultiNoiseBiomeSource 距离查找
    public sealed class ParameterPoint
    {
        public Parameter Temperature { get; }
        public Parameter Humidity { get; }
        public Parameter Continentalness { get; }
        public Parameter Erosion { get; }
        public Parameter Depth { get; }
        public Parameter Weirdness { get; }
        public long Offset { get; }

        public ParameterPoint(
            Parameter temperature,
            Parameter humidity,
            Parameter continentalness,
            Parameter erosion,
            Parameter depth,
            Parameter weirdness,
            long offset)
        {
            Temperature = temperature;
            Humidity = humidity;
            Continentalness = continentalness;
            Erosion = erosion;
            Depth = depth;
            Weirdness = weirdness;
            Offset = offset;
        }
    }

    //Sampler 噪声采样器接口对应原版 Climate.Sampler
    //MultiNoiseBiomeSource 用此接口按坐标采样 6 维度参数
    public interface Sampler
    {
        Parameter Temperature(int x, int y, int z);
        Parameter Humidity(int x, int y, int z);
        Parameter Continentalness(int x, int y, int z);
        Parameter Erosion(int x, int y, int z);
        Parameter Depth(int x, int y, int z);
        Parameter Weirdness(int x, int y, int z);
    }

    //ConstantSampler 常量采样器所有维度返回固定参数测试用
    public sealed class ConstantSampler : Sampler
    {
        public Parameter TemperatureValue { get; }
        public Parameter HumidityValue { get; }
        public Parameter ContinentalnessValue { get; }
        public Parameter ErosionValue { get; }
        public Parameter DepthValue { get; }
        public Parameter WeirdnessValue { get; }

        public ConstantSampler(
            Parameter temperature, Parameter humidity, Parameter continentalness,
            Parameter erosion, Parameter depth, Parameter weirdness)
        {
            TemperatureValue = temperature;
            HumidityValue = humidity;
            ContinentalnessValue = continentalness;
            ErosionValue = erosion;
            DepthValue = depth;
            WeirdnessValue = weirdness;
        }

        public Parameter Temperature(int x, int y, int z) => TemperatureValue;
        public Parameter Humidity(int x, int y, int z) => HumidityValue;
        public Parameter Continentalness(int x, int y, int z) => ContinentalnessValue;
        public Parameter Erosion(int x, int y, int z) => ErosionValue;
        public Parameter Depth(int x, int y, int z) => DepthValue;
        public Parameter Weirdness(int x, int y, int z) => WeirdnessValue;
    }

    //NoiseRouterSampler 基于 NoiseRouter 6 个气候维度密度函数的真实采样器
    //对应原版 Climate.Sampler 的 NoiseRouterData 实现
    //把密度值 double 量化为 long 后包装为 Parameter.Single
    //量化精度对齐原版 Climate.quantize 把 double 映射到 long 空间
    public sealed class NoiseRouterSampler : Sampler
    {
        private readonly NoiseRouter _router;

        public NoiseRouterSampler(NoiseRouter router) => _router = router;

        public Parameter Temperature(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Temperature, x, y, z));
        public Parameter Humidity(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Vegetation, x, y, z));
        public Parameter Continentalness(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Continents, x, y, z));
        public Parameter Erosion(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Erosion, x, y, z));
        public Parameter Depth(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Depth, x, y, z));
        public Parameter Weirdness(int x, int y, int z)
            => Parameter.Single(Quantize(_router.Ridges, x, y, z));

        //Quantize 采样密度值后量化为 long 对应原版 Climate.quantize
        //原版用 (long)(value * 1000.0) 量化保留 3 位小数精度
        private static long Quantize(DensityFunction function, int x, int y, int z)
        {
            var ctx = SinglePointContext.At(x, y, z);
            return (long)(function.Compute(ctx) * 1000.0);
        }
    }

    //Distance 计算两个 ParameterPoint 在参数空间的距离对应原版 Climate.pointSpace
    //简化用 6 维度中心值差的平方和 + offset 项
    public static long Distance(ParameterPoint target, ParameterPoint point)
    {
        var dt = target.Temperature.Value - point.Temperature.Value;
        var dh = target.Humidity.Value - point.Humidity.Value;
        var dc = target.Continentalness.Value - point.Continentalness.Value;
        var de = target.Erosion.Value - point.Erosion.Value;
        var dd = target.Depth.Value - point.Depth.Value;
        var dw = target.Weirdness.Value - point.Weirdness.Value;
        return dt * dt + dh * dh + dc * dc + de * de + dd * dd + dw * dw + point.Offset;
    }
}

//MultiNoiseBiomeSourceParameterList 多噪声生物群系参数列表对应原版 MultiNoiseBiomeSourceParameterList
//持有 ParameterPoint -> Holder<Biome> 映射MultiNoiseBiomeSource 用此列表查找最近 Biome
public sealed class MultiNoiseBiomeSourceParameterList
{
    public IReadOnlyList<(Climate.ParameterPoint Point, Holder<Biome> Biome)> Entries { get; }

    public MultiNoiseBiomeSourceParameterList(
        IEnumerable<(Climate.ParameterPoint, Holder<Biome>)> entries)
    {
        Entries = entries.ToList();
    }

    //FindClosest 查找参数空间距离最近的 Biome Holder 对应原版 parameterList.findClosestBiome
    public Holder<Biome> FindClosest(Climate.ParameterPoint target)
    {
        Holder<Biome>? best = null;
        var bestDistance = long.MaxValue;
        foreach (var (point, biome) in Entries)
        {
            var distance = Climate.Distance(target, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = biome;
            }
        }
        return best ?? throw new InvalidOperationException("parameter list is empty");
    }
}
