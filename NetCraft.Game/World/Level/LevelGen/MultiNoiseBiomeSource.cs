using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//MultiNoiseBiomeSource 多噪声生物群系源对应原版 net.minecraft.world.level.biome.MultiNoiseBiomeSource
//阶段 E 接入 Climate.Sampler + MultiNoiseBiomeSourceParameterList 真实派生
//GetBiome 按坐标采样 6 维度参数查找参数空间距离最近的 Biome
public sealed class MultiNoiseBiomeSource : BiomeSource
{
    public MultiNoiseBiomeSourceParameterList? ParameterList { get; }
    public Climate.Sampler? Sampler { get; }

    public MultiNoiseBiomeSource(MultiNoiseBiomeSourceParameterList parameterList, Climate.Sampler sampler)
    {
        ParameterList = parameterList;
        Sampler = sampler;
    }

    //LegacyConstructor 无参数列表与采样器占位返回 PlainsBiome
    //用于 Bootstrap 之前或测试场景
    public MultiNoiseBiomeSource() { }

    //GetBiome 按坐标采样 6 维度参数查找参数空间距离最近的 Biome
    //无 ParameterList/Sampler 时占位返回 PlainsBiome 单例避免 palette 爆炸
    public Biome GetBiome(int x, int y, int z)
    {
        if (ParameterList is null || Sampler is null)
            return PlainsBiome.Instance;

        var target = new Climate.ParameterPoint(
            Sampler.Temperature(x, y, z),
            Sampler.Humidity(x, y, z),
            Sampler.Continentalness(x, y, z),
            Sampler.Erosion(x, y, z),
            Sampler.Depth(x, y, z),
            Sampler.Weirdness(x, y, z),
            0L);
        return ParameterList.FindClosest(target).Value;
    }
}

//PlainsBiome 平原生物群系对应原版 net.minecraft.world.level.biome.Biomes.plains
//阶段 11.45 接入真实 Biome codec 字段 HasPrecipitation/Temperature/Downfall/Category
//阶段 11.54-A 加 Instance 单例避免 BiomeSource 每次返回新实例导致 palette 爆炸
//真实派生 Climate 参数仍待完整 ClimateSettings 子系统就绪
public sealed class PlainsBiome : Biome
{
    public static readonly PlainsBiome Instance = new();
    public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    public override bool HasPrecipitation => true;
    public override float Temperature => 0.8f;
    public override float Downfall => 0.4f;
    public override string Category => "plains";
}
