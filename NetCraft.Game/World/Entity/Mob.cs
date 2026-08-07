using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//Mob 生物实体对应原版 net.minecraft.world.entity.Mob
//继承 Entity 持有 AI 标志位与目标引用
//AI/Pathfinding/Goal 子系统待后续接入此处仅基础字段
public class Mob : NetCraft.Registry.Entity
{
    //Id Mob 实体类型注册名占位具体 Mob 子类按需重写
    public override Identifier Id => Identifier.WithDefaultNamespace("mob");

    //NoAi 是否禁用 AI 默认 false 对齐原版 NoAI NBT 标签
    public bool NoAi { get; set; }

    //TargetUuid 当前攻击目标实体 Uuid 占位待 AI 子系统就绪
    public Guid? TargetUuid { get; set; }

    //PersistenceRequired 是否强制保留不卸载默认 false
    public bool PersistenceRequired { get; set; }
}
