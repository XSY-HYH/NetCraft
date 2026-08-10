namespace NetCraft.Primitives;

//AABB 轴对齐包围盒对标原版 net.minecraft.world.phys.AABB
//Min/Max 两点定义盒体 Corners 返回 8 角点供 Frustum 剔除测试
//W5 首版仅含 Frustum 剔除所需接口 grow/intersect/move 等留后续
public readonly struct AABB
{
    public readonly Vec3 Min;
    public readonly Vec3 Max;

    public AABB(Vec3 min, Vec3 max)
    {
        Min = min;
        Max = max;
    }

    public AABB(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        : this(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ)) { }

    //Corners 返回 8 角点供 Frustum 8 角点测试或调试可视化
    public Vec3[] Corners() => new[]
    {
        Min,
        new Vec3(Max.X, Min.Y, Min.Z),
        new Vec3(Min.X, Max.Y, Min.Z),
        new Vec3(Max.X, Max.Y, Min.Z),
        new Vec3(Min.X, Min.Y, Max.Z),
        new Vec3(Max.X, Min.Y, Max.Z),
        new Vec3(Min.X, Max.Y, Max.Z),
        Max
    };
}
