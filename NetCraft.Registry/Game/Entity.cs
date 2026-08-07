using NetCraft.Primitives;

namespace NetCraft.Registry;

//Entity 抽象基类对应原版 net.minecraft.world.entity.Entity
//持有 Pos/Uuid/Velocity/YRot/XRot 核心字段Level 用 object 占位待 Level 子系统就绪后替换
//原版持有 CompoundTag 持久化字段此处简化子类按需扩展
public abstract class Entity
{
    //Id 实体的注册名子类必须实现
    public abstract Identifier Id { get; }

    //Level 实体所在世界引用占位待 Level 子系统就绪后替换为强类型
    public object? Level { get; set; }

    //Pos 实体在世界中的位置默认原点
    public Vec3 Pos { get; set; } = Vec3.Zero;

    //Velocity 实体速度向量默认零
    public Vec3 Velocity { get; set; } = Vec3.Zero;

    //Uuid 实体唯一标识默认随机生成
    public Guid Uuid { get; set; } = Guid.NewGuid();

    //YRot/Yaw 偏航角默认 0
    public float YRot { get; set; }

    //XRot/Pitch 俯仰角默认 0
    public float XRot { get; set; }

    //OnGround 是否接触地面默认 false
    public bool OnGround { get; set; }

    //SetPos 同时设置 Pos 与角度对齐原版 moveTo/moveTo
    public void SetPos(Vec3 pos, float yRot, float xRot)
    {
        Pos = pos;
        YRot = yRot;
        XRot = xRot;
    }

    //Tick 每帧调用占位对齐原版 Entity.tick
    //默认空实现子类按需重写提供 AI/移动/碰撞 等具体行为
    public virtual void Tick()
    {
    }
}
