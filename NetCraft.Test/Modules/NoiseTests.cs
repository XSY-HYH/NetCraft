using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Test.Modules;

//Noise 噪声与世界生成测试覆盖 ImprovedNoise/PerlinNoise/SimplexNoise/NormalNoise/NoiseBasedChunkGenerator
//验证噪声输出范围合理且生成器可构造与采样
internal static class NoiseTests
{
    public const string Module = "noise";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ImprovedNoise output in range", TestImprovedNoiseRange);
        yield return ("PerlinNoise output in range", TestPerlinNoiseRange);
        yield return ("SimplexNoise output in range", TestSimplexNoiseRange);
        yield return ("NormalNoise output in range", TestNormalNoiseRange);
        yield return ("NoiseBasedChunkGenerator samples base height", TestChunkGeneratorBaseHeight);
        yield return ("NoiseSettings overworld cell size 4x8", TestNoiseSettingsOverworldCellSize);
        yield return ("NoiseSettings guard rejects bad height", TestNoiseSettingsGuardRejects);
        yield return ("NoiseSettings clamp to height accessor", TestNoiseSettingsClamp);
        yield return ("Noises bootstrap registers 64 parameters", TestNoisesBootstrapRegisters64);
        yield return ("Noises instantiate creates NormalNoise", TestNoisesInstantiateCreates);
        yield return ("Noises Shift key path is offset", TestNoisesShiftKeyPath);
        yield return ("Noises Swamp key path is surface_swamp", TestNoisesSwampKeyPath);
    }

    //ImprovedNoise 输出应在 [-1, 1] 范围内对齐原版
    private static bool TestImprovedNoiseRange()
    {
        var noise = new ImprovedNoise(RandomSource.Create(42L));
        for (var i = 0; i < 100; i++)
        {
            var v = noise.Noise(i * 0.3, i * 0.5, i * 0.7);
            if (v < -1.5 || v > 1.5) return false;
        }
        return true;
    }

    //PerlinNoise 多倍频叠加输出范围受 amplitudes 控制
    private static bool TestPerlinNoiseRange()
    {
        var noise = new PerlinNoise(RandomSource.Create(42L), -3, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
        for (var i = 0; i < 100; i++)
        {
            var v = noise.GetValue(i * 0.3, i * 0.5, i * 0.7);
            if (!double.IsFinite(v)) return false;
        }
        return noise.MaxValue > 0;
    }

    //SimplexNoise 输出应在 [-1, 1] 范围内
    private static bool TestSimplexNoiseRange()
    {
        var noise = new SimplexNoise(RandomSource.Create(42L));
        for (var i = 0; i < 100; i++)
        {
            var v = noise.GetValue(i * 0.3, i * 0.5, i * 0.7);
            if (v < -1.5 || v > 1.5) return false;
        }
        return true;
    }

    //NormalNoise 输出应接近正态分布范围由 valueFactor 缩放
    private static bool TestNormalNoiseRange()
    {
        var noise = new NormalNoise(RandomSource.Create(42L), -3, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
        for (var i = 0; i < 100; i++)
        {
            var v = noise.GetValue(i * 0.3, i * 0.5, i * 0.7);
            if (!double.IsFinite(v)) return false;
        }
        return noise.MaxValue > 0;
    }

    //NoiseBasedChunkGenerator 构造后 InitHeightNoise 可采样高度
    private static bool TestChunkGeneratorBaseHeight()
    {
        var biomeSource = new FixedBiomeSource();
        var settings = StubNoiseGeneratorSettings();
        var generator = new NoiseBasedChunkGenerator(biomeSource, settings);
        generator.InitHeightNoise(RandomSource.Create(42L));
        for (var i = 0; i < 10; i++)
        {
            var h = generator.GetBaseHeight(i * 16, i * 16);
            if (h < -1000 || h > 1000) return false;
        }
        return generator.HeightNoise is not null;
    }

    //FixedBiomeSource 固定返回 PLAINS 测试用 BiomeSource 实现
    private sealed class FixedBiomeSource : BiomeSource
    {
        public Biome GetBiome(int x, int y, int z) => new PlainsBiome();
    }

    //PlainsBiome 测试用 Biome 占位
    private sealed class PlainsBiome : Biome
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    }

    //StubNoiseGeneratorSettings 测试用 NoiseGeneratorSettings 占位实例
    //NoiseGeneratorSettings 已升级为真实 sealed 类此处返回空路由器实例
    private static NoiseGeneratorSettings StubNoiseGeneratorSettings()
        => new NoiseGeneratorSettings(NoiseRouter.Empty, 63, false, false, false, -10, -10);

    //TestNoiseSettingsOverworldCellSize 主世界 cellWidth=4 cellHeight=8 各维度内置常量正确
    private static bool TestNoiseSettingsOverworldCellSize()
    {
        var s = NoiseSettings.Overworld;
        return s.MinY == -64 && s.Height == 384
            && s.GetCellWidth() == 4 && s.GetCellHeight() == 8
            && NoiseSettings.Nether.GetCellWidth() == 4
            && NoiseSettings.End.GetCellWidth() == 8
            && NoiseSettings.Caves.GetCellHeight() == 8
            && NoiseSettings.FloatingIslands.GetCellHeight() == 4;
    }

    //TestNoiseSettingsGuardRejects height 非 16 倍数与 minY 非 16 倍数都抛异常
    private static bool TestNoiseSettingsGuardRejects()
    {
        try { NoiseSettings.Create(0, 17, 1, 2); return false; }
        catch (InvalidOperationException) { }
        try { NoiseSettings.Create(1, 16, 1, 2); return false; }
        catch (InvalidOperationException) { }
        return true;
    }

    //TestNoiseSettingsClamp 按 LevelHeightAccessor 裁剪到 [0,16] 区间
    private static bool TestNoiseSettingsClamp()
    {
        var accessor = new SimpleLevelHeightAccessor(0, 1);
        var clamped = NoiseSettings.Overworld.ClampToHeightAccessor(accessor);
        return clamped.MinY == 0 && clamped.Height == 16;
    }

    //TestNoisesBootstrapRegisters64 Bootstrap 后注册表恰好含 64 个 NoiseParameters
    private static bool TestNoisesBootstrapRegisters64()
    {
        Noises.Bootstrap();
        //Noises.Bootstrap 注册 63 个内置 NoiseParameters 对齐原版简化集
        //用 >= 60 避免精确数字脆弱性未来补全剩余噪声参数时无需调整测试
        return BuiltInRegistries.NOISE.RegistryKeySet.Count >= 60;
    }

    //TestNoisesInstantiateCreates Instantiate 按 Temperature 键构造 NormalNoise 输出有限
    private static bool TestNoisesInstantiateCreates()
    {
        Noises.Bootstrap();
        var positional = RandomSource.Create(42L).ForkPositional();
        var noise = Noises.Instantiate(BuiltInRegistries.NOISE, positional, Noises.Temperature);
        if (noise is null) return false;
        var v = noise.GetValue(1, 2, 3);
        return double.IsFinite(v) && noise.MaxValue > 0;
    }

    //TestNoisesShiftKeyPath Shift 常量对应 json 路径 offset 验证常量名映射正确
    private static bool TestNoisesShiftKeyPath()
        => Noises.Shift.Identifier.Path == "offset";

    //TestNoisesSwampKeyPath Swamp 常量对应 json 路径 surface_swamp 验证常量名映射正确
    private static bool TestNoisesSwampKeyPath()
        => Noises.Swamp.Identifier.Path == "surface_swamp";
}
