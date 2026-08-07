using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Game.Bootstrap;

//GameBootstrap Game 层引导入口对应原版 net.minecraft.server.Bootstrap 的 Game 层扩展
//在 BootstrapClass.BootStrap 之前调用触发 Game 层内置注册表填充方块/物品/实体等
//确保 NoiseBasedChunkGenerator.FillFromNoise 等业务流程可访问已注册的 BlockState
public static class GameBootstrap
{
    private static bool _bootstrapped;

    static GameBootstrap() => Log.SetClassSource(typeof(GameBootstrap));

    //Bootstrap Game 层引导入口触发方块/物品等内置注册表填充
    //重复调用幂等直接返回避免重复注册
    public static void Bootstrap()
    {
        if (_bootstrapped)
        {
            Log.Debug("Bootstrap 出口");
            return;
        }
        Log.Debug("Bootstrap 入口");
        Log.Info("Game 层引导开始");
        Blocks.Bootstrap();
        RegisterBiomes();
        DensityFunctionBootstrap.RegisterAll();
        Noises.Bootstrap();
        _bootstrapped = true;
        Log.Info("Game 层引导完成方块已注册");
        Log.Debug("Bootstrap 出口");
    }

    //RegisterBiomes 注册内置生物群系到 BuiltInRegistries.BIOME
    //当前注册 plains 占位真实接入需 Biome 配置 codec 与多噪声参数子系统
    private static void RegisterBiomes()
    {
        var plainsKey = ResourceKey<Biome>.Create(Registries.BIOME, Identifier.WithDefaultNamespace("plains"));
        BuiltInRegistries.BIOME.Register(plainsKey, new PlainsBiome(), RegistrationInfo.BuiltIn);
    }

    public static bool IsBootstrapped => _bootstrapped;

    public static void Reset()
    {
        _bootstrapped = false;
        DensityFunctionBootstrap.Reset();
    }
}
