using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//Blocks 内置方块常量对应原版 net.minecraft.world.level.block.Blocks
//注册到 BuiltInRegistries.BLOCK 注册表
//简化版只含几个示例方块 AIR/STONE/DIRT/GRASS_BLOCK 验证 BlockStateRegistry 框架
//原版有 1000+ 方块 NC 按需扩展
public static class Blocks
{
    //AirBlock 空气方块不可见不可碰撞
    public sealed class AirBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("air");
    }

    //StoneBlock 石头方块基础建筑材料
    public sealed class StoneBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("stone");
    }

    //DirtBlock 泥土方块
    public sealed class DirtBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("dirt");
    }

    //GrassBlock 草方块
    public sealed class GrassBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("grass_block");
    }

    public sealed class WaterBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("water");
    }

    public sealed class LavaBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("lava");
    }

    public static readonly AirBlock AIR = new();
    public static readonly StoneBlock STONE = new();
    public static readonly DirtBlock DIRT = new();
    public static readonly GrassBlock GRASS_BLOCK = new();
    public static readonly WaterBlock WATER = new();
    public static readonly LavaBlock LAVA = new();

    //Bootstrap 注册所有内置方块到 BuiltInRegistries.BLOCK
    //由 Game 层 Bootstrap 在 BuiltInRegistries.BootStrap 之前调用
    public static void Bootstrap()
    {
        Register(AIR);
        Register(STONE);
        Register(DIRT);
        Register(GRASS_BLOCK);
        Register(WATER);
        Register(LAVA);
    }

    //Register 注册方块到 BLOCK 注册表触发默认状态构建
    private static void Register(BlockBehaviour block)
    {
        //访问 DefaultBlockState 触发 BlockStateDefinition 构建
        _ = block.DefaultBlockState;
        Registry<NetCraft.Registry.Block>.Register(BuiltInRegistries.BLOCK, block.Id, block);
    }
}
