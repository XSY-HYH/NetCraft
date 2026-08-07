using NetCraft.DataFixer.Util;
using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//DataComponents 预定义组件类型对应原版 net.minecraft.core.component.DataComponents
//简化只实现简单值类型组件用 object 装箱对齐 Java Integer/Boolean/Unit 装箱语义
//复杂业务类型组件（FoodProperties/Tool/Weapon/Enchantments/ItemLore 等）待业务子系统就绪后补全
//Bootstrap 由 ClientMain/ServerMain 在 NetCraftKernel.Initialize 之后调用
public static class DataComponents
{
    //MAX_STACK_SIZE 物品最大堆叠数
    public static readonly DataComponentType<object> MAX_STACK_SIZE = Register(
        "max_stack_size", new VarIntObjectCodec());

    //MAX_DAMAGE 物品最大耐久
    public static readonly DataComponentType<object> MAX_DAMAGE = Register(
        "max_damage", new VarIntObjectCodec());

    //DAMAGE 物品当前损坏值
    public static readonly DataComponentType<object> DAMAGE = Register(
        "damage", new VarIntObjectCodec());

    //REPAIR_COST 修复费用
    public static readonly DataComponentType<object> REPAIR_COST = Register(
        "repair_cost", new VarIntObjectCodec());

    //UNBREAKABLE 不可破坏标记
    public static readonly DataComponentType<object> UNBREAKABLE = Register(
        "unbreakable", new UnitObjectCodec());

    //CREATIVE_SLOT_LOCK 创造模式槽锁
    public static readonly DataComponentType<object> CREATIVE_SLOT_LOCK = Register(
        "creative_slot_lock", new UnitObjectCodec());

    //INTANGIBLE_PROJECTILE 无形弹射物
    public static readonly DataComponentType<object> INTANGIBLE_PROJECTILE = Register(
        "intangible_projectile", new UnitObjectCodec());

    //ENCHANTMENT_GLINT_OVERRIDE 附魔光泽覆盖
    public static readonly DataComponentType<object> ENCHANTMENT_GLINT_OVERRIDE = Register(
        "enchantment_glint_override", new BooleanObjectCodec());

    //Register 注册单个组件类型到 BuiltInRegistries.DATA_COMPONENT_TYPE
    private static DataComponentType<object> Register(string name, StreamCodec<RegistryFriendlyByteBuf, object> streamCodec)
    {
        var type = new SimpleDataComponentType<object>(null, streamCodec, false);
        Registry<object>.Register(BuiltInRegistries.DATA_COMPONENT_TYPE, name, type);
        return type;
    }

    //Bootstrap 注册所有预定义组件类型由 ClientMain/ServerMain 调用
    //C# 静态字段在类首次访问时初始化此方法强制触发确保注册时机
    public static void Bootstrap()
    {
        _ = MAX_STACK_SIZE;
    }
}

//VarIntObjectCodec int 装箱为 object 的 StreamCodec 用 VarInt 编解码
internal sealed class VarIntObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadVarInt();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteVarInt((int)value);
}

//BooleanObjectCodec bool 装箱为 object 的 StreamCodec 读 1 字节
internal sealed class BooleanObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadBoolean();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteBoolean((bool)value);
}

//UnitObjectCodec Unit 装箱为 object 的 StreamCodec 无 payload 编解码返回 Unit.Instance
internal sealed class UnitObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => Unit.Instance;

    public void Encode(RegistryFriendlyByteBuf buf, object value) { }
}
