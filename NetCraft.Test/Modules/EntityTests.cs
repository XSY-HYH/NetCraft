using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;
using PlayerEntity = NetCraft.Game.World.Entity.Player;

namespace NetCraft.Test.Modules;

//Entity 实体系统测试覆盖 EntityTypes 常量与 Entity/Player/Mob 核心契约
//不调用 EntityTypes.Bootstrap 避免污染 BuiltInRegistries.ENTITY_TYPE 静态状态
internal static class EntityTests
{
    public const string Module = "entity";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("EntityTypes constants not null", TestEntityTypesConstantsNotNull);
        yield return ("EntityTypes ids correct", TestEntityTypesIdsCorrect);
        yield return ("Player default fields", TestPlayerDefaultFields);
        yield return ("Mob default fields", TestMobDefaultFields);
        yield return ("Entity SetPos updates pos and rotation", TestEntitySetPos);
    }

    //EntityTypes 静态常量已构造不依赖注册表
    private static bool TestEntityTypesConstantsNotNull()
        => EntityTypes.PIG is not null
            && EntityTypes.COW is not null
            && EntityTypes.CHICKEN is not null
            && EntityTypes.ZOMBIE is not null;

    //各实体类型 Id 与原版命名空间一致
    private static bool TestEntityTypesIdsCorrect()
        => EntityTypes.PIG.Id == Identifier.WithDefaultNamespace("pig")
            && EntityTypes.COW.Id == Identifier.WithDefaultNamespace("cow")
            && EntityTypes.CHICKEN.Id == Identifier.WithDefaultNamespace("chicken")
            && EntityTypes.ZOMBIE.Id == Identifier.WithDefaultNamespace("zombie");

    //Player 默认字段验证
    private static bool TestPlayerDefaultFields()
    {
        var player = new PlayerEntity();
        return player.Id == Identifier.WithDefaultNamespace("player")
            && player.Pos == Vec3.Zero
            && player.Uuid != Guid.Empty
            && player.Health == 20f
            && player.MaxHealth == 20f
            && player.FoodLevel == 20
            && player.XpLevel == 0;
    }

    //Mob 默认字段验证
    private static bool TestMobDefaultFields()
    {
        var mob = new Mob();
        return mob.Id == Identifier.WithDefaultNamespace("mob")
            && mob.Pos == Vec3.Zero
            && mob.Uuid != Guid.Empty
            && mob.NoAi == false
            && mob.TargetUuid is null;
    }

    //SetPos 同时更新位置与角度
    private static bool TestEntitySetPos()
    {
        var player = new PlayerEntity();
        player.SetPos(new Vec3(1, 2, 3), 90f, 45f);
        return player.Pos == new Vec3(1, 2, 3)
            && player.YRot == 90f
            && player.XRot == 45f;
    }
}
