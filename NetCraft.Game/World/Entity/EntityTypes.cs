using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//EntityTypes 内置实体类型常量对应原版 net.minecraft.world.entity.EntityType
//注册到 BuiltInRegistries.ENTITY_TYPE 注册表
//简化版只含几个示例实体 PIG/COW/CHICKEN/ZOMBIE 验证注册框架
//原版有 100+ 实体类型 NC 按需扩展
//注意 BuiltInRegistries.ENTITY_TYPE 是 EntityType<object> 弱类型注册表
//用 object 类型参数承载不同具体 Entity 子类对齐原版类型擦除方案
public static class EntityTypes
{
    //PigType 猪类型
    public sealed class PigType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("pig");
    }

    //CowType 牛类型
    public sealed class CowType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("cow");
    }

    //ChickenType 鸡类型
    public sealed class ChickenType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("chicken");
    }

    //ZombieType 僵尸类型
    public sealed class ZombieType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("zombie");
    }

    public static readonly PigType PIG = new();
    public static readonly CowType COW = new();
    public static readonly ChickenType CHICKEN = new();
    public static readonly ZombieType ZOMBIE = new();

    //Bootstrap 注册所有内置实体类型到 BuiltInRegistries.ENTITY_TYPE
    //由 Game 层 Bootstrap 在 BuiltInRegistries.BootStrap 后调用
    public static void Bootstrap()
    {
        Register(PIG);
        Register(COW);
        Register(CHICKEN);
        Register(ZOMBIE);
    }

    //Register 注册实体类型到 ENTITY_TYPE 注册表
    private static void Register(EntityType<object> type)
        => Registry<EntityType<object>>.Register(BuiltInRegistries.ENTITY_TYPE, type.Id, type);
}
