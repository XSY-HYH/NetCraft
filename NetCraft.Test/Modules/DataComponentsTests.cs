using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Test.Modules;

//DataComponents 预定义组件类型注册测试
//验证 Bootstrap 后 BuiltInRegistries.DATA_COMPONENT_TYPE 含 8 个简单值类型组件
//StreamCodec 端到端编解码留待 Play 协议框架升级 RegistryFriendlyByteBuf 后验证
internal static class DataComponentsTests
{
    public const string Module = "datacomponents";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("DataComponents bootstrap registers 8 types", TestBootstrapRegistersTypes);
        yield return ("DataComponents MAX_STACK_SIZE registered", TestMaxStackSizeRegistered);
        yield return ("DataComponents UNBREAKABLE registered", TestUnbreakableRegistered);
        yield return ("DataComponents ENCHANTMENT_GLINT_OVERRIDE registered", TestEnchantmentGlintOverrideRegistered);
        yield return ("FilterMask initial empty", TestFilterMaskEmpty);
        yield return ("FilterMask Add excludes type", TestFilterMaskAddExcludes);
        yield return ("FilterMask Remove reverts exclusion", TestFilterMaskRemoveReverts);
        yield return ("FilterMask Filter strips excluded components", TestFilterMaskFilterStrips);
    }

    //TestBootstrapRegistersTypes 验证 Bootstrap 后注册表含 8 个组件类型
    private static bool TestBootstrapRegistersTypes()
    {
        DataComponents.Bootstrap();
        return BuiltInRegistries.DATA_COMPONENT_TYPE.Size == 8;
    }

    //TestMaxStackSizeRegistered 验证 MAX_STACK_SIZE 按 name 注册成功
    //用 ContainsKey 验证不访问未绑定 Value Freeze 前 holder.Value 抛异常
    private static bool TestMaxStackSizeRegistered()
    {
        DataComponents.Bootstrap();
        return BuiltInRegistries.DATA_COMPONENT_TYPE.ContainsKey(Identifier.Parse("max_stack_size"));
    }

    //TestUnbreakableRegistered 验证 UNBREAKABLE 按 name 注册成功
    private static bool TestUnbreakableRegistered()
    {
        DataComponents.Bootstrap();
        return BuiltInRegistries.DATA_COMPONENT_TYPE.ContainsKey(Identifier.Parse("unbreakable"));
    }

    //TestEnchantmentGlintOverrideRegistered 验证 ENCHANTMENT_GLINT_OVERRIDE 按 name 注册成功
    private static bool TestEnchantmentGlintOverrideRegistered()
    {
        DataComponents.Bootstrap();
        return BuiltInRegistries.DATA_COMPONENT_TYPE.ContainsKey(Identifier.Parse("enchantment_glint_override"));
    }

    //TestFilterMaskEmpty 新建 FilterMask 无任何规则 IsEmpty 返回 true
    private static bool TestFilterMaskEmpty()
    {
        var mask = new FilterMask();
        return mask.IsEmpty;
    }

    //TestFilterMaskAddExcludes Add 后 type 被过滤 IsEmpty 变 false
    private static bool TestFilterMaskAddExcludes()
    {
        DataComponents.Bootstrap();
        var mask = new FilterMask();
        mask.Add(DataComponents.MAX_STACK_SIZE);
        return !mask.IsEmpty
            && mask.IsFiltered(DataComponents.MAX_STACK_SIZE)
            && !mask.IsFiltered(DataComponents.MAX_DAMAGE);
    }

    //TestFilterMaskRemoveReverts Remove 把 type 从排除集合移到包含集合 IsFiltered 变 false
    private static bool TestFilterMaskRemoveReverts()
    {
        DataComponents.Bootstrap();
        var mask = new FilterMask();
        mask.Add(DataComponents.MAX_STACK_SIZE);
        if (!mask.IsFiltered(DataComponents.MAX_STACK_SIZE)) return false;
        mask.Remove(DataComponents.MAX_STACK_SIZE);
        return !mask.IsFiltered(DataComponents.MAX_STACK_SIZE)
            && mask.IsExplicitlyIncluded(DataComponents.MAX_STACK_SIZE);
    }

    //TestFilterMaskFilterStrips Filter 后被排除的组件 Get 返回 null KeySet 不含该 type
    private static bool TestFilterMaskFilterStrips()
    {
        DataComponents.Bootstrap();
        //构造含 MAX_STACK_SIZE 与 MAX_DAMAGE 两个组件的 map
        var map = new DataComponentMapBuilder()
            .Set(DataComponents.MAX_STACK_SIZE, (object)64)
            .Set(DataComponents.MAX_DAMAGE, (object)100)
            .Build();
        var mask = new FilterMask();
        mask.Add(DataComponents.MAX_STACK_SIZE);
        var filtered = mask.Filter(map);
        return filtered.Get(DataComponents.MAX_STACK_SIZE) is null
            && filtered.Get(DataComponents.MAX_DAMAGE) is not null
            && filtered.KeySet.Count() == 1;
    }
}
