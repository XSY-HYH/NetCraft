using NetCraft.Registry.State;

namespace NetCraft.Registry;

//Block 抽象基类对应原版 net.minecraft.world.level.block.Block
//原版继承 BlockBehaviour 此处简化为抽象类持有 Id 与默认 BlockState
//子类按需重写 Id 与 DefaultBlockState 提供具体方块定义
public abstract class Block
{
    //Id 方块的注册名子类必须实现
    public abstract Identifier Id { get; }

    //DefaultBlockState 方块的默认状态子类必须实现
    public abstract BlockState DefaultBlockState { get; }
}
