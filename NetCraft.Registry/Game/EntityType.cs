namespace NetCraft.Registry;

//EntityType 抽象基类对应原版 net.minecraft.world.entity.EntityType
//T 是具体 Entity 子类原版持有 EntityFactory/Codec/MobCategory
//此处简化为抽象类持有 Id
public abstract class EntityType<T> where T : class
{
    //Id 实体类型的注册名子类必须实现
    public abstract Identifier Id { get; }
}
