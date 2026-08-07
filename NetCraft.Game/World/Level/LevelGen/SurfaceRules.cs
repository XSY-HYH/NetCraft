using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceRules 表面规则系统对应原版 net.minecraft.world.level.levelgen.SurfaceRules
//提供条件源 ConditionSource 与规则源 RuleSource 两类可序列化对象
//MATERIAL_CONDITION/MATERIAL_RULE 用 MapCodec<object> 弱类型对齐跨层方案
public static class SurfaceRules
{
    //Context 规则求值上下文持有坐标与生物群系查询能力
    //每个 (x, y, z) 评估前由 SurfaceSystem 构建一个 Context 实例
    public sealed class Context
    {
        public int BlockX { get; }
        public int BlockY { get; }
        public int BlockZ { get; }
        public Biome Biome { get; }
        public int StoneDepthAbove { get; }
        public int SurfaceHeight { get; }
        public int SeaLevel { get; }

        public Context(int blockX, int blockY, int blockZ, Biome biome,
            int stoneDepthAbove, int surfaceHeight, int seaLevel)
        {
            BlockX = blockX;
            BlockY = blockY;
            BlockZ = blockZ;
            Biome = biome;
            StoneDepthAbove = stoneDepthAbove;
            SurfaceHeight = surfaceHeight;
            SeaLevel = seaLevel;
        }
    }

    //ConditionSource 条件源注册到 MATERIAL_CONDITION 注册表
    //实现按 Context 决定是否进入某条规则分支
    public interface ConditionSource
    {
        bool Test(Context context);
    }

    //RuleSource 规则源注册到 MATERIAL_RULE 注册表
    //返回替换后的 BlockState返回 null 表示不替换
    public interface RuleSource
    {
        BlockState? Apply(Context context, BlockState current);
    }

    //BlockStateRule 持有固定 BlockState 直接替换对应原版 SurfaceRules.state
    public sealed class BlockStateRule : RuleSource
    {
        public BlockState State { get; }

        public BlockStateRule(BlockState state)
        {
            State = state;
        }

        public BlockState? Apply(Context context, BlockState current) => State;
    }

    //IfTrue 条件成立时应用嵌套规则对应原版 SurfaceRules.ifTrue
    public sealed class IfTrue : RuleSource
    {
        public ConditionSource Condition { get; }
        public RuleSource Then { get; }

        public IfTrue(ConditionSource condition, RuleSource then)
        {
            Condition = condition;
            Then = then;
        }

        public BlockState? Apply(Context context, BlockState current)
            => Condition.Test(context) ? Then.Apply(context, current) : null;
    }

    //Sequence 顺序规则列表后面的覆盖前面的对应原版 SurfaceRules.sequence
    public sealed class Sequence : RuleSource
    {
        public IReadOnlyList<RuleSource> Rules { get; }

        public Sequence(IReadOnlyList<RuleSource> rules)
        {
            Rules = rules;
        }

        public BlockState? Apply(Context context, BlockState current)
        {
            var state = current;
            var changed = false;
            foreach (var rule in Rules)
            {
                var next = rule.Apply(context, state);
                if (next is not null)
                {
                    state = next.Value;
                    changed = true;
                }
            }
            return changed ? state : null;
        }
    }

    //AbovePreliminarySurface 测试 y 高于海平面对应原版 SurfaceRules.abovePreliminarySurface
    public sealed class AbovePreliminarySurface : ConditionSource
    {
        public bool Test(Context context) => context.BlockY > context.SeaLevel;
    }

    //StoneDepth 测试当前 y 距地表石头深度小于等于阈值对应原版 SurfaceRules.stoneDepth
    public sealed class StoneDepth : ConditionSource
    {
        public int Offset { get; }

        public StoneDepth(int offset)
        {
            Offset = offset;
        }

        public bool Test(Context context) => context.StoneDepthAbove <= Offset;
    }

    //VerticalGradient 测试 y 是否在指定范围对应原版 SurfaceRules.verticalGradient
    public sealed class VerticalGradient : ConditionSource
    {
        public int TrueAtAndBelow { get; }
        public int FalseAtAndAbove { get; }

        public VerticalGradient(int trueAtAndBelow, int falseAtAndAbove)
        {
            TrueAtAndBelow = trueAtAndBelow;
            FalseAtAndAbove = falseAtAndAbove;
        }

        public bool Test(Context context)
        {
            var y = context.BlockY;
            if (y <= TrueAtAndBelow) return true;
            if (y >= FalseAtAndAbove) return false;
            return false;
        }
    }

    //Not 取反条件对应原版 SurfaceRules.not
    public sealed class Not : ConditionSource
    {
        public ConditionSource Inner { get; }

        public Not(ConditionSource inner)
        {
            Inner = inner;
        }

        public bool Test(Context context) => !Inner.Test(context);
    }

    //Biome 测试当前生物群系是否匹配对应原版 SurfaceRules.biome
    public sealed class BiomeCondition : ConditionSource
    {
        public IReadOnlyList<Biome> Biomes { get; }

        public BiomeCondition(IReadOnlyList<Biome> biomes)
        {
            Biomes = biomes;
        }

        public bool Test(Context context)
        {
            foreach (var b in Biomes)
                if (b.Id == context.Biome.Id) return true;
            return false;
        }
    }
}
