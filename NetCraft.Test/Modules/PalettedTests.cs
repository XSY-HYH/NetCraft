using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Storage.Paletted;

namespace NetCraft.Test.Modules;

//PalettedContainer子系统测试
//覆盖SingleValue/Linear/HashMap三种palette切换与pack/unpack round-trip
internal static class PalettedTests
{
    public const string Module = "paletted";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("SingleValue Get/Set", TestSingleValue);
        yield return ("GetAndSet返回旧值", TestGetAndSet);
        yield return ("SwitchToLinear", TestSwitchToLinear);
        yield return ("SwitchToHashMap", TestSwitchToHashMap);
        yield return ("GetAll遍历", TestGetAll);
        yield return ("Count统计", TestCount);
        yield return ("Copy复制", TestCopy);
        yield return ("Recreate重建", TestRecreate);
        yield return ("Pack/Unpack round-trip single", () => TestPackUnpack(1));
        yield return ("Pack/Unpack round-trip multi", () => TestPackUnpack(8));
        yield return ("MaybeHas查询", TestMaybeHas);
        yield return ("BitsPerEntry", TestBitsPerEntry);
        yield return ("GetAndSetUnchecked跳过Acquire", TestGetAndSetUnchecked);
        yield return ("ForEachInPalette遍历调色板", TestForEachInPalette);
        yield return ("Count单值容器短路", TestCountSingleValue);
        yield return ("MaybeHas空容器", TestMaybeHasEmpty);
        yield return ("Pack/Unpack大palette", () => TestPackUnpack(48));
        yield return ("Unpack位宽不匹配返回Error", TestUnpackBitsMismatch);
        yield return ("Unpack缺storage返回Error", TestUnpackMissingStorage);
    }

    private static IdMapper<string> NewRegistry()
    {
        var map = new IdMapper<string>();
        for (var i = 0; i < 256; i++) map.Add($"block{i}");
        return map;
    }

    private static bool TestSingleValue()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block1", strategy);
        if (container.Get(0, 0, 0) != "block1") return false;
        container.Set(0, 0, 0, "block2");
        return container.Get(0, 0, 0) == "block2";
    }

    private static bool TestGetAndSet()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block1", strategy);
        var old = container.GetAndSet(0, 0, 0, "block5");
        if (old != "block1") return false;
        return container.Get(0, 0, 0) == "block5";
    }

    private static bool TestSwitchToLinear()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        container.Set(1, 0, 0, "block2");
        container.Set(2, 0, 0, "block3");
        return container.Get(0, 0, 0) == "block1"
            && container.Get(1, 0, 0) == "block2"
            && container.Get(2, 0, 0) == "block3";
    }

    private static bool TestSwitchToHashMap()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        for (var i = 0; i < 40; i++)
            container.Set(i % 16, (i / 16) % 16, 0, $"block{i + 1}");
        var ok = true;
        for (var i = 0; i < 40; i++)
            if (container.Get(i % 16, (i / 16) % 16, 0) != $"block{i + 1}") ok = false;
        return ok;
    }

    private static bool TestGetAll()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        container.Set(1, 0, 0, "block2");
        var seen = new HashSet<string>();
        container.GetAll(v => seen.Add(v));
        return seen.Contains("block1") && seen.Contains("block2");
    }

    private static bool TestCount()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        container.Set(1, 0, 0, "block1");
        container.Set(2, 0, 0, "block2");
        var counts = new Dictionary<string, int>();
        container.Count((v, c) => counts[v] = c);
        return counts["block1"] == 2 && counts["block2"] == 1;
    }

    private static bool TestCopy()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        var copy = container.Copy();
        container.Set(0, 0, 0, "block2");
        return copy.Get(0, 0, 0) == "block1";
    }

    private static bool TestRecreate()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block3", strategy);
        container.Set(0, 0, 0, "block5");
        var recreated = container.Recreate();
        return recreated.Get(0, 0, 0) == "block3";
    }

    private static bool TestPackUnpack(int distinctValues)
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        for (var i = 0; i < distinctValues; i++)
            container.Set(i % 16, (i / 16) % 16, 0, $"block{i + 1}");
        var packed = container.Pack(strategy);
        var result = PalettedContainer<string>.Unpack(strategy, packed);
        if (!result.Result().IsPresent) return false;
        var restored = result.GetOrThrow();
        for (var i = 0; i < distinctValues; i++)
            if (restored.Get(i % 16, (i / 16) % 16, 0) != $"block{i + 1}") return false;
        return true;
    }

    private static bool TestMaybeHas()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        return container.MaybeHas(v => v == "block1")
            && !container.MaybeHas(v => v == "block999");
    }

    private static bool TestBitsPerEntry()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        if (container.BitsPerEntry != 0) return false;
        container.Set(0, 0, 0, "block1");
        return container.BitsPerEntry >= 1;
    }

    //GetAndSetUnchecked 不走 acquire/release 路径仍返回正确旧值
    private static bool TestGetAndSetUnchecked()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block1", strategy);
        var old = container.GetAndSetUnchecked(0, 0, 0, "block9");
        return old == "block1" && container.Get(0, 0, 0) == "block9";
    }

    //ForEachInPalette 遍历当前 palette 中所有值
    private static bool TestForEachInPalette()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        container.Set(0, 0, 0, "block1");
        container.Set(1, 0, 0, "block2");
        var seen = new HashSet<string>();
        container.ForEachInPalette(v => seen.Add(v));
        return seen.Contains("block0") && seen.Contains("block1") && seen.Contains("block2");
    }

    //Count 单值容器短路逻辑直接用 storage.Size
    private static bool TestCountSingleValue()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block1", strategy);
        var counts = new Dictionary<string, int>();
        container.Count((v, c) => counts[v] = c);
        return counts["block1"] == strategy.EntryCount;
    }

    //MaybeHas 空容器（仅默认值）查询走 palette.MaybeHas
    private static bool TestMaybeHasEmpty()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block1", strategy);
        return container.MaybeHas(v => v == "block1")
            && !container.MaybeHas(v => v == "block2");
    }

    //Unpack BitsPerEntry 不匹配返回 Error 不抛异常
    private static bool TestUnpackBitsMismatch()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        for (var i = 0; i < 4; i++)
            container.Set(i, 0, 0, $"block{i + 1}");
        var packed = container.Pack(strategy);
        //改 BitsPerEntry 让校验失败
        var mismatched = new PackedData<string>(packed.PaletteEntries, packed.Storage, packed.BitsPerEntry + 1);
        var result = PalettedContainer<string>.Unpack(strategy, mismatched);
        return !result.Result().IsPresent;
    }

    //Unpack 非 ZeroBitStorage 缺 storage 数据返回 Error
    private static bool TestUnpackMissingStorage()
    {
        var registry = NewRegistry();
        var strategy = Strategy<string>.CreateForBlockStates(registry);
        var container = new PalettedContainer<string>("block0", strategy);
        for (var i = 0; i < 4; i++)
            container.Set(i, 0, 0, $"block{i + 1}");
        var packed = container.Pack(strategy);
        //构造 paletteEntries 多于 1 但 storage 缺失
        var missing = new PackedData<string>(packed.PaletteEntries, Optional<long[]>.Empty());
        var result = PalettedContainer<string>.Unpack(strategy, missing);
        return !result.Result().IsPresent;
    }
}
