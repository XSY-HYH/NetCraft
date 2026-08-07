using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.Block;

//BlockBehaviour 方块行为基类对应原版 net.minecraft.world.level.block.state.BlockBehaviour
//继承 Block 抽象基类提供 Properties/StateDefinition 等基础行为契约
//子类按需重写具体行为方法本类只承载状态定义与默认状态
//当前命名空间段名 Block 与 Registry.Block 类型同名用完全限定名避免歧义
public abstract class BlockBehaviour : NetCraft.Registry.Block
{
    private BlockState? _defaultState;
    private BlockStateDefinition? _stateDefinition;

    //Properties 方块属性集合默认空子类可重写添加 Property<T>
    public virtual IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>();

    //StateDefinition 方块状态定义延迟构建首次访问时构建所有可能状态
    public BlockStateDefinition StateDefinition
        => _stateDefinition ??= BuildStateDefinition();

    //DefaultBlockState 方块的默认状态取 StateDefinition 第一个状态
    public override BlockState DefaultBlockState
        => _defaultState ??= StateDefinition.PossibleStates[0];

    //BuildStateDefinition 用 Properties 构建状态定义
    private BlockStateDefinition BuildStateDefinition()
        => new(this, Properties);
}
