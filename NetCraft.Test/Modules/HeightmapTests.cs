using NetCraft.Registry;
using HeightmapRegistry = NetCraft.Registry.Heightmap;
using LevelHeightmap = NetCraft.Storage.LevelGen.Heightmap;

namespace NetCraft.Test.Modules;

//Heightmap 子系统测试
//覆盖初始化/Update/GetFirstAvailable/SetHeight/FromData round-trip
internal static class HeightmapTests
{
    public const string Module = "heightmap";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Heightmap 初始空列返回 -1", TestInitialEmpty);
        yield return ("Heightmap Update 写入返回 true", TestUpdateWrites);
        yield return ("Heightmap Update 不降低高度", TestUpdateKeepsMax);
        yield return ("Heightmap SetHeight 强制覆盖", TestSetHeight);
        yield return ("Heightmap SetHeight 低于 minY 写 0", TestSetHeightBelowMinY);
        yield return ("Heightmap FromData round-trip", TestFromDataRoundTrip);
        yield return ("Heightmap GetFirstAvailable 各列独立", TestPerColumnIndependent);
        yield return ("Heightmap Update 边界列", TestUpdateBoundaryColumn);
        yield return ("Heightmap MinValue 常量 -1", TestMinValueConstant);
        yield return ("Heightmap GetData 长度匹配 storage", TestGetDataLength);
    }

    //新 Heightmap 所有列未设置返回 MinValue(-1)
    private static bool TestInitialEmpty()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        for (var x = 0; x < 16; x++)
            for (var z = 0; z < 16; z++)
                if (map.GetFirstAvailable(x, z) != LevelHeightmap.MinValue) return false;
        return true;
    }

    //Update 写入比初始高的高度返回 true 并生效
    //Update 签名为 (x, y, z) 对齐原版 update(int x, int y, int z)
    private static bool TestUpdateWrites()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        var ok = map.Update(0, 100, 0);
        if (!ok) return false;
        return map.GetFirstAvailable(0, 0) == 100;
    }

    //Update 写入比已存低的值返回 false 且不修改原高度
    private static bool TestUpdateKeepsMax()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.OceanFloor, 0, 256);
        map.Update(5, 50, 5);
        var ok = map.Update(5, 30, 5);
        if (ok) return false;
        return map.GetFirstAvailable(5, 5) == 50;
    }

    //SetHeight 直接覆盖任意高度值
    private static bool TestSetHeight()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.MotionBlocking, 0, 256);
        map.SetHeight(2, 3, 80);
        if (map.GetFirstAvailable(2, 3) != 80) return false;
        map.SetHeight(2, 3, 40);
        return map.GetFirstAvailable(2, 3) == 40;
    }

    //SetHeight 传入小于 minY 的值写 0 对应空列
    private static bool TestSetHeightBelowMinY()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        map.SetHeight(7, 7, -100);
        return map.GetFirstAvailable(7, 7) == LevelHeightmap.MinValue;
    }

    //FromData 从 long[] 重建高度图与原实例一致
    private static bool TestFromDataRoundTrip()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        map.Update(0, 100, 0);
        map.Update(15, 200, 15);
        var data = map.GetData();
        var restored = LevelHeightmap.FromData(HeightmapRegistry.Types.WorldSurface, -64, 384, data);
        return restored.GetFirstAvailable(0, 0) == 100
            && restored.GetFirstAvailable(15, 15) == 200;
    }

    //每列独立存储互不影响
    private static bool TestPerColumnIndependent()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, 0, 256);
        map.Update(0, 50, 0);
        map.Update(15, 100, 15);
        for (var x = 0; x < 16; x++)
            for (var z = 0; z < 16; z++)
            {
                var expected = (x == 0 && z == 0) ? 50 : (x == 15 && z == 15) ? 100 : LevelHeightmap.MinValue;
                if (map.GetFirstAvailable(x, z) != expected) return false;
            }
        return true;
    }

    //Update 边界列 x=15 z=15 正确写入
    private static bool TestUpdateBoundaryColumn()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.OceanFloor, 0, 256);
        map.Update(15, 64, 15);
        return map.GetFirstAvailable(15, 15) == 64;
    }

    //MinValue 常量为 -1 对应原版 -1 空列
    private static bool TestMinValueConstant() => LevelHeightmap.MinValue == -1;

    //GetData 返回 long[] 长度与 storage 内部一致
    //bits=ceil(log2(385))=9 valuesPerLong=64/9=7 requiredLength=ceil(256/7)=37
    private static bool TestGetDataLength()
    {
        var map = new LevelHeightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        var data = map.GetData();
        return data.Length == 37;
    }
}
