using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Test.Modules;

//Block 方块系统测试覆盖 Blocks 常量与 BlockBehaviour/BlockStateDefinition 核心契约
//用独立测试方块验证状态定义逻辑避免污染 BuiltInRegistries.BLOCK 静态状态
internal static class BlockTests
{
    public const string Module = "block";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Blocks constants not null", TestBlocksConstantsNotNull);
        yield return ("Blocks ids correct", TestBlocksIdsCorrect);
        yield return ("BlockBehaviour singleton state built", TestSingletonStateBuilt);
        yield return ("BlockBehaviour multi property states", TestMultiPropertyStates);
        yield return ("BlockBehaviour default state first possible", TestDefaultStateFirstPossible);
    }

    //Blocks 静态常量已构造不依赖 BlockStateRegistry
    private static bool TestBlocksConstantsNotNull()
        => Blocks.AIR is not null
            && Blocks.STONE is not null
            && Blocks.DIRT is not null
            && Blocks.GRASS_BLOCK is not null;

    //各方块 Id 与原版命名空间一致
    private static bool TestBlocksIdsCorrect()
        => Blocks.AIR.Id == Identifier.WithDefaultNamespace("air")
            && Blocks.STONE.Id == Identifier.WithDefaultNamespace("stone")
            && Blocks.DIRT.Id == Identifier.WithDefaultNamespace("dirt")
            && Blocks.GRASS_BLOCK.Id == Identifier.WithDefaultNamespace("grass_block");

    //无属性方块构建单个 singleton 状态
    private static bool TestSingletonStateBuilt()
    {
        BlockStateRegistry.Reset();
        var block = new SingletonTestBlock();
        var def = block.StateDefinition;
        return def.PossibleStates.Count == 1 && def.IsSingletonState;
    }

    //带 BooleanProperty 的方块构建 2 个状态笛卡尔积
    private static bool TestMultiPropertyStates()
    {
        BlockStateRegistry.Reset();
        var block = new MultiPropTestBlock();
        var def = block.StateDefinition;
        return def.PossibleStates.Count == 2 && !def.IsSingletonState;
    }

    //DefaultBlockState 取 PossibleStates 第一个
    private static bool TestDefaultStateFirstPossible()
    {
        BlockStateRegistry.Reset();
        var block = new MultiPropTestBlock();
        var def = block.StateDefinition;
        return block.DefaultBlockState.Equals(def.PossibleStates[0]);
    }

    private sealed class SingletonTestBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("test_singleton");
    }

    private sealed class MultiPropTestBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("test_multi");
        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase>
            {
                ["powered"] = new BooleanProperty("powered")
            };
    }
}
