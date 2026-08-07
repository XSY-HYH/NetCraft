using NetCraft.Primitives;

namespace NetCraft.Test.Modules;

//Primitives 值类型测试
//覆盖 Direction/Vec3i/BlockPos/SectionPos/GlobalPos/QuartPos/ChunkPos 关键路径
internal static class PrimitivesTests
{
    public const string Module = "primitives";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Direction ById round trip", TestDirectionById);
        yield return ("Direction opposite", TestDirectionOpposite);
        yield return ("Direction axis and step", TestDirectionAxisStep);
        yield return ("Vec3i offset and relative", TestVec3iOffset);
        yield return ("Vec3i cross and distSqr", TestVec3iCrossDist);
        yield return ("Vec3i compareTo ordering", TestVec3iCompareTo);
        yield return ("BlockPos asLong round trip", TestBlockPosAsLong);
        yield return ("BlockPos offset by direction", TestBlockPosOffsetDirection);
        yield return ("SectionPos blockToSection round trip", TestSectionPosBlockToSection);
        yield return ("SectionPos of BlockPos", TestSectionPosOfBlockPos);
        yield return ("SectionPos asLong round trip", TestSectionPosAsLong);
        yield return ("QuartPos round trip", TestQuartPosRoundTrip);
        yield return ("GlobalPos equality", TestGlobalPosEquality);
        yield return ("ChunkPos pack round trip", TestChunkPosPack);
    }

    private static bool TestDirectionById()
    {
        return Direction.ById(0) == Direction.Down
            && Direction.ById(1) == Direction.Up
            && Direction.ById(2) == Direction.North
            && Direction.ById(3) == Direction.South
            && Direction.ById(4) == Direction.West
            && Direction.ById(5) == Direction.East;
    }

    private static bool TestDirectionOpposite()
    {
        return Direction.Down.Opposite == Direction.Up
            && Direction.North.Opposite == Direction.South
            && Direction.West.Opposite == Direction.East;
    }

    private static bool TestDirectionAxisStep()
    {
        return Direction.Up.GetAxis() == Direction.Axis.Y
            && Direction.Up.StepY == 1
            && Direction.Down.StepY == -1
            && Direction.North.StepZ == -1
            && Direction.South.StepZ == 1
            && Direction.West.StepX == -1
            && Direction.East.StepX == 1
            && Direction.East.IsHorizontal
            && !Direction.Up.IsHorizontal;
    }

    private static bool TestVec3iOffset()
    {
        var v = new Vec3i(1, 2, 3);
        return v.Offset(1, 0, 0) == new Vec3i(2, 2, 3)
            && v.Above() == new Vec3i(1, 3, 3)
            && v.North(2) == new Vec3i(1, 2, 1)
            && v.Relative(Direction.East, 3) == new Vec3i(4, 2, 3)
            && v.Relative(Direction.Axis.Y, 5) == new Vec3i(1, 7, 3);
    }

    private static bool TestVec3iCrossDist()
    {
        var a = new Vec3i(1, 0, 0);
        var b = new Vec3i(0, 1, 0);
        var cross = a.Cross(b);
        if (cross != new Vec3i(0, 0, 1)) return false;
        var p = new Vec3i(1, 0, 0);
        return p.DistSqr(new Vec3i(4, 0, 0)) == 9.0
            && p.CloserThan(new Vec3i(4, 0, 0), 4.0)
            && !p.CloserThan(new Vec3i(4, 0, 0), 2.0);
    }

    private static bool TestVec3iCompareTo()
    {
        var a = new Vec3i(1, 1, 1);
        var b = new Vec3i(2, 1, 1);
        var c = new Vec3i(1, 2, 1);
        return a.CompareTo(b) < 0
            && b.CompareTo(a) > 0
            && a.CompareTo(a) == 0
            && a.CompareTo(c) < 0;
    }

    private static bool TestBlockPosAsLong()
    {
        var pos = new BlockPos(10, 20, 30);
        var packed = pos.AsLong();
        var restored = BlockPos.FromLong(packed);
        return restored == pos
            && BlockPos.GetX(packed) == 10
            && BlockPos.GetY(packed) == 20
            && BlockPos.GetZ(packed) == 30;
    }

    private static bool TestBlockPosOffsetDirection()
    {
        var pos = new BlockPos(0, 64, 0);
        return pos.Offset(Direction.Up) == new BlockPos(0, 65, 0)
            && pos.Offset(Direction.East) == new BlockPos(1, 64, 0)
            && pos.Offset(Direction.North) == new BlockPos(0, 64, -1);
    }

    private static bool TestSectionPosBlockToSection()
    {
        return SectionPos.BlockToSectionCoord(0) == 0
            && SectionPos.BlockToSectionCoord(15) == 0
            && SectionPos.BlockToSectionCoord(16) == 1
            && SectionPos.BlockToSectionCoord(31) == 1
            && SectionPos.BlockToSectionCoord(-1) == -1
            && SectionPos.SectionToBlockCoord(2) == 32;
    }

    private static bool TestSectionPosOfBlockPos()
    {
        var bp = new BlockPos(33, 70, 25);
        var sp = SectionPos.Of(bp);
        return sp.X == 2 && sp.Y == 4 && sp.Z == 1
            && sp.AsBlockPos() == new BlockPos(32, 64, 16);
    }

    private static bool TestSectionPosAsLong()
    {
        var sp = new SectionPos(2, 4, 1);
        var packed = sp.AsLong();
        var restored = SectionPos.Of(packed);
        return restored == sp
            && SectionPos.GetX(packed) == 2
            && SectionPos.GetY(packed) == 4
            && SectionPos.GetZ(packed) == 1;
    }

    private static bool TestQuartPosRoundTrip()
    {
        return QuartPos.FromBlock(0) == 0
            && QuartPos.FromBlock(4) == 1
            && QuartPos.FromBlock(7) == 1
            && QuartPos.FromBlock(8) == 2
            && QuartPos.ToBlock(3) == 12;
    }

    private static bool TestGlobalPosEquality()
    {
        var a = GlobalPos.Of("overworld", new BlockPos(1, 2, 3));
        var b = GlobalPos.Of("overworld", new BlockPos(1, 2, 3));
        var c = GlobalPos.Of("nether", new BlockPos(1, 2, 3));
        return a == b && a != c;
    }

    private static bool TestChunkPosPack()
    {
        var cp = new ChunkPos(10, -5);
        var packed = cp.Pack();
        var restored = ChunkPos.Unpack(packed);
        return restored == cp
            && ChunkPos.GetX(packed) == 10
            && ChunkPos.GetZ(packed) == -5
            && cp.GetRegionX() == 0
            && cp.GetRegionZ() == -1;
    }
}
