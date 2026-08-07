using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Fixes;
using NetCraft.Game.DFU;
using NetCraft.Nbt;

namespace NetCraft.Test.Modules;

//V1_21段DFU端到端验证测试模块
//覆盖GameDataFixers.BuildV1_21Fixer注册流程与V1_21段Schema/Fix链真实生效
//验证13个Schema与18个Fix类按版本顺序注册可构造可用DataFixer
//通过MemoryExpiryDataFix真实场景验证存档升级流程跑通
internal static class V1_21FixEndToEndTests
{
    public const string Module = "v121fix";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("V1_21 BuildFixer construction", TestBuildFixerConstruction);
        yield return ("V1_21 GetSchema by version", TestGetSchemaByVersion);
        yield return ("V1_21 GetTypeRaw Entity/BlockEntity/Player", TestGetTypeRawReferences);
        yield return ("V1_21 Update no-op when version equal", TestUpdateNoOpWhenVersionEqual);
        yield return ("V1_21 Update Entity no-op when newVersion lower", TestUpdateNoOpWhenNewVersionLower);
        yield return ("V1_21 MemoryExpiryDataFix wraps memory value", TestMemoryExpiryDataFix);
    }

    //buildFixer缓存避免每个测试重复构造
    private static NetCraft.DataFixer.DataFixer? _cached;
    private static NetCraft.DataFixer.DataFixer GetFixer()
    {
        _cached ??= GameDataFixers.BuildV1_21Fixer();
        return _cached;
    }

    //验证BuildV1_21Fixer能成功构造返回非空DataFixer
    //构造过程中13个Schema按版本递增注册parent链自动链接
    //18个Fix类按版本递增注册每个用对应输出Schema
    private static bool TestBuildFixerConstruction()
    {
        try
        {
            var fixer = GetFixer();
            return fixer != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BuildV1_21Fixer failed: " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            return false;
        }
    }

    //验证fixer.GetSchema能访问v99基础与v4312末尾
    //GetSchema内部按DataFixUtils.MakeKey转换key
    private static bool TestGetSchemaByVersion()
    {
        try
        {
            var fixer = GetFixer();
            var v99 = fixer.GetSchema(DataFixUtils.MakeKey(99));
            var v4312 = fixer.GetSchema(DataFixUtils.MakeKey(4312));
            return v99 != null && v4312 != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("GetSchema failed: " + ex.Message);
            return false;
        }
    }

    //验证v4312 schema能访问V1_21段全部引用类型
    //V1Foundation注册的ENTITY/BLOCK_ENTITY/ITEM_STACK/DATA_COMPONENTS/PLAYER类型都应可解析
    private static bool TestGetTypeRawReferences()
    {
        try
        {
            var fixer = GetFixer();
            var v4312 = fixer.GetSchema(DataFixUtils.MakeKey(4312));
            var entity = v4312.GetTypeRaw(References.Entity);
            var blockEntity = v4312.GetTypeRaw(References.BlockEntity);
            var player = v4312.GetTypeRaw(References.Player);
            var itemStack = v4312.GetTypeRaw(References.ItemStack);
            var dataComponents = v4312.GetTypeRaw(References.DataComponents);
            return entity != null && blockEntity != null && player != null
                && itemStack != null && dataComponents != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("GetTypeRaw failed: " + ex.Message);
            return false;
        }
    }

    //验证version等于newVersion时Update返回原输入
    //对应DataFixerUpper.Update中version<newVersion判断的false分支
    private static bool TestUpdateNoOpWhenVersionEqual()
    {
        var fixer = GetFixer();
        var entity = BuildVillagerEntity();
        var input = new Dynamic<Tag>(NbtOps.Instance, entity);
        var updated = fixer.Update(References.Entity, input, 99, 99);
        return ReferenceEquals(updated.Value, entity);
    }

    //验证newVersion小于version时Update返回原输入
    //对应DataFixerUpper.Update中version<newVersion判断的false分支
    private static bool TestUpdateNoOpWhenNewVersionLower()
    {
        var fixer = GetFixer();
        var entity = BuildVillagerEntity();
        var input = new Dynamic<Tag>(NbtOps.Instance, entity);
        var updated = fixer.Update(References.Entity, input, 4312, 99);
        return ReferenceEquals(updated.Value, entity);
    }

    //端到端验证MemoryExpiryDataFix真实生效
    //构造villager实体Brain.memories.walk=1从v99升级到v2505触发MemoryExpiryDataFix
    //Fix将memories每个value包装为{value:原值}
    //升级后memories.walk应为CompoundTag{value:1}
    private static bool TestMemoryExpiryDataFix()
    {
        var fixer = GetFixer();
        var entity = BuildVillagerEntity();
        var input = new Dynamic<Tag>(NbtOps.Instance, entity);
        try
        {
            var updated = fixer.Update(References.Entity, input, 99, 2505);
            var resultTag = updated.Value as CompoundTag;
            if (resultTag == null)
            {
                Console.Error.WriteLine("Update result is not CompoundTag");
                return false;
            }
            //v99升级到v2505仅触发MemoryExpiryDataFix
            //校验Brain.memories.walk被包装为{value:1}
            var brain = resultTag.GetCompound("Brain");
            if (brain == null)
            {
                Console.Error.WriteLine("Brain field missing");
                return false;
            }
            var memories = brain.GetCompound("memories");
            if (memories == null)
            {
                Console.Error.WriteLine("memories field missing");
                return false;
            }
            var walk = memories.GetCompound("walk");
            if (walk == null)
            {
                Console.Error.WriteLine("walk not wrapped as CompoundTag");
                Console.Error.WriteLine("memories keys: " + string.Join(", ", memories.Keys));
                return false;
            }
            int value = walk.GetIntValue("value");
            return value == 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Update failed: " + ex.GetBaseException().Message);
            foreach (var frame in new System.Diagnostics.StackTrace(ex, true).GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>())
            {
                Console.Error.WriteLine("  at " + frame.GetMethod()?.DeclaringType?.FullName + "." + frame.GetMethod()?.Name + " (" + frame.GetFileName() + ":" + frame.GetFileLineNumber() + ")");
            }
            return false;
        }
    }

    //构造测试用villager实体{id, Brain: {memories: {walk: 1}}}
    //对应原版1.20.2前的存档格式MemoryExpiryDataFix期望的输入
    private static CompoundTag BuildVillagerEntity()
    {
        var memories = new CompoundTag();
        memories.PutInt("walk", 1);
        var brain = new CompoundTag();
        brain.Put("memories", memories);
        var entity = new CompoundTag();
        entity.PutString("id", "minecraft:villager");
        entity.Put("Brain", brain);
        return entity;
    }
}
