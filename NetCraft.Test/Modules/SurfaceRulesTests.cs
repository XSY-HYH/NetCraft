using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;
using BiomeSource = NetCraft.Registry.BiomeSource;

namespace NetCraft.Test.Modules;

//SurfaceRules 表面规则系统测试覆盖 ConditionSource 与 RuleSource 真实接入
//验证 BlockStateRule/IfTrue/Sequence/AbovePreliminarySurface/StoneDepth/Not 派发逻辑
internal static class SurfaceRulesTests
{
    public const string Module = "surfacerules";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("BlockStateRule replaces current", TestBlockStateRuleReplaces);
        yield return ("IfTrue applies when condition true", TestIfTrueAppliesWhenTrue);
        yield return ("IfTrue noop when condition false", TestIfTrueNoopWhenFalse);
        yield return ("Sequence later rule wins", TestSequenceLaterWins);
        yield return ("AbovePreliminarySurface true above sea", TestAbovePreliminarySurfaceTrue);
        yield return ("StoneDepth true below threshold", TestStoneDepthTrueBelowThreshold);
        yield return ("Not inverts condition", TestNotInverts);
        yield return ("SurfaceSystem with rule replaces blocks", TestSurfaceSystemWithRule);
        yield return ("SurfaceSystem default still works", TestSurfaceSystemDefaultStillWorks);
    }

    //TestBlockStateRuleReplaces BlockStateRule 无条件替换为指定 BlockState
    private static bool TestBlockStateRuleReplaces()
    {
        GameBootstrap.Bootstrap();
        var dirt = Blocks.DIRT.DefaultBlockState;
        var stone = Blocks.STONE.DefaultBlockState;
        var rule = new SurfaceRules.BlockStateRule(dirt);
        var ctx = NewContext(0, 64, 0, 63);
        var next = rule.Apply(ctx, stone);
        return next == dirt;
    }

    //TestIfTrueAppliesWhenTrue 条件成立时 IfTrue 应用嵌套规则
    private static bool TestIfTrueAppliesWhenTrue()
    {
        GameBootstrap.Bootstrap();
        var dirt = Blocks.DIRT.DefaultBlockState;
        var stone = Blocks.STONE.DefaultBlockState;
        var condition = new AlwaysTrueCondition();
        var rule = new SurfaceRules.IfTrue(condition, new SurfaceRules.BlockStateRule(dirt));
        var ctx = NewContext(0, 64, 0, 63);
        var next = rule.Apply(ctx, stone);
        return next == dirt;
    }

    //TestIfTrueNoopWhenFalse 条件不成立时 IfTrue 返回 null 不替换
    private static bool TestIfTrueNoopWhenFalse()
    {
        GameBootstrap.Bootstrap();
        var dirt = Blocks.DIRT.DefaultBlockState;
        var stone = Blocks.STONE.DefaultBlockState;
        var condition = new AlwaysFalseCondition();
        var rule = new SurfaceRules.IfTrue(condition, new SurfaceRules.BlockStateRule(dirt));
        var ctx = NewContext(0, 64, 0, 63);
        var next = rule.Apply(ctx, stone);
        return next is null;
    }

    //TestSequenceLaterWins Sequence 后面的规则覆盖前面的
    private static bool TestSequenceLaterWins()
    {
        GameBootstrap.Bootstrap();
        var dirt = Blocks.DIRT.DefaultBlockState;
        var grass = Blocks.GRASS_BLOCK.DefaultBlockState;
        var stone = Blocks.STONE.DefaultBlockState;
        var rule = new SurfaceRules.Sequence(new SurfaceRules.RuleSource[]
        {
            new SurfaceRules.BlockStateRule(dirt),
            new SurfaceRules.BlockStateRule(grass)
        });
        var ctx = NewContext(0, 64, 0, 63);
        var next = rule.Apply(ctx, stone);
        return next == grass;
    }

    //TestAbovePreliminarySurfaceTrue AbovePreliminarySurface 在 y 高于海平面时为 true
    private static bool TestAbovePreliminarySurfaceTrue()
    {
        var cond = new SurfaceRules.AbovePreliminarySurface();
        var above = NewContext(0, 80, 0, 63);
        var below = NewContext(0, 50, 0, 63);
        return cond.Test(above) && !cond.Test(below);
    }

    //TestStoneDepthTrueBelowThreshold StoneDepth 在石头深度小于等于阈值时为 true
    private static bool TestStoneDepthTrueBelowThreshold()
    {
        var cond = new SurfaceRules.StoneDepth(2);
        var shallow = NewContext(0, 64, 0, 63, stoneDepthAbove: 1);
        var deep = NewContext(0, 60, 0, 63, stoneDepthAbove: 5);
        return cond.Test(shallow) && !cond.Test(deep);
    }

    //TestNotInverts Not 取反条件
    private static bool TestNotInverts()
    {
        var inner = new SurfaceRules.AbovePreliminarySurface();
        var not = new SurfaceRules.Not(inner);
        var above = NewContext(0, 80, 0, 63);
        return !not.Test(above) && inner.Test(above);
    }

    //TestSurfaceSystemWithRule SurfaceSystem 接入 RuleSource 后按规则替换方块
    private static bool TestSurfaceSystemWithRule()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var pos = new ChunkPos(0, 0);
        var proto = new ProtoChunk(pos, -1, 2, factory.CreateForBlockStates, factory.CreateForBiomes);

        var grass = Blocks.GRASS_BLOCK.DefaultBlockState;
        var stone = Blocks.STONE.DefaultBlockState;
        proto.SetBlockState(0, 0, 15, 0, stone);

        var rule = new SurfaceRules.BlockStateRule(grass);
        var system = new SurfaceSystem(rule, 63);
        system.BuildSurface(proto, RandomSource.Create());

        var section = proto.GetSection(0);
        var state = section?.GetBlockState(0, 15, 0);
        return state == grass;
    }

    //TestSurfaceSystemDefaultStillWorks SurfaceSystem 无 RuleSource 时走默认 GrassBlock/Dirt 替换
    private static bool TestSurfaceSystemDefaultStillWorks()
    {
        GameBootstrap.Bootstrap();
        var factory = NewFactory();
        var pos = new ChunkPos(0, 0);
        var proto = new ProtoChunk(pos, -1, 2, factory.CreateForBlockStates, factory.CreateForBiomes);

        var stone = Blocks.STONE.DefaultBlockState;
        var grass = Blocks.GRASS_BLOCK.DefaultBlockState;
        proto.SetBlockState(0, 0, 15, 0, stone);

        var system = new SurfaceSystem();
        system.BuildSurface(proto, RandomSource.Create());

        var section = proto.GetSection(0);
        var state = section?.GetBlockState(0, 15, 0);
        return state == grass;
    }

    //NewFactory 注册真实方块与 PlainsBiome 后返回可用工厂
    private static DefaultPalettedContainerFactory NewFactory()
    {
        var factory = new DefaultPalettedContainerFactory();
        factory.RegisterBlock(Blocks.AIR);
        factory.RegisterBlock(Blocks.STONE);
        factory.RegisterBlock(Blocks.DIRT);
        factory.RegisterBlock(Blocks.GRASS_BLOCK);
        factory.RegisterBiome(Holder<Biome>.Direct(new PlainsBiome()));
        return factory;
    }

    private static SurfaceRules.Context NewContext(int x, int y, int z, int seaLevel, int stoneDepthAbove = 0)
    {
        GameBootstrap.Bootstrap();
        return new SurfaceRules.Context(x, y, z, new PlainsBiome(), stoneDepthAbove, y, seaLevel);
    }

    private sealed class AlwaysTrueCondition : SurfaceRules.ConditionSource
    {
        public bool Test(SurfaceRules.Context context) => true;
    }

    private sealed class AlwaysFalseCondition : SurfaceRules.ConditionSource
    {
        public bool Test(SurfaceRules.Context context) => false;
    }
}
