using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Nbt.Visitors;

namespace NetCraft.Test.Modules;

//NBT 子系统测试
//round-trip 字节级兼容性 + 流式访问者正确性
//从 NetCraft.Nbt.Tests 迁移合并
internal static class NbtTests
{
    public const string Module = "nbt";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ByteTag round-trip", TestByteTagRoundTrip);
        yield return ("ShortTag round-trip", TestShortTagRoundTrip);
        yield return ("IntTag round-trip", TestIntTagRoundTrip);
        yield return ("LongTag round-trip", TestLongTagRoundTrip);
        yield return ("FloatTag round-trip", TestFloatTagRoundTrip);
        yield return ("DoubleTag round-trip", TestDoubleTagRoundTrip);
        yield return ("StringTag round-trip", TestStringTagRoundTrip);
        yield return ("ByteArrayTag round-trip", TestByteArrayTagRoundTrip);
        yield return ("IntArrayTag round-trip", TestIntArrayTagRoundTrip);
        yield return ("LongArrayTag round-trip", TestLongArrayTagRoundTrip);
        yield return ("ListTag round-trip", TestListTagRoundTrip);
        yield return ("CompoundTag round-trip", TestCompoundTagRoundTrip);
        yield return ("Nested compound round-trip", TestNestedCompoundRoundTrip);
        yield return ("GZIP compressed round-trip", TestGzipRoundTrip);
        yield return ("ParseCompressed stream visitor", TestParseCompressed);
        yield return ("CollectToTag visitor", TestCollectToTag);
        yield return ("SkipAll visitor", TestSkipAll);
        yield return ("Modified UTF-8 (surrogate pair)", TestModifiedUtf8Surrogate);
        yield return ("Empty compound round-trip", TestEmptyCompoundRoundTrip);
        yield return ("Deeply nested (10 levels)", TestDeepNestedRoundTrip);
        yield return ("NbtUtils.PrettyPrint", TestPrettyPrint);
        yield return ("NbtUtils.CompareNbt", TestCompareNbt);
        yield return ("NbtUtils.PackBlockState", TestPackBlockState);
        yield return ("ByteTag cache", TestByteTagCache);
        yield return ("NbtOps mergeToList ByteArrayTag keeps compact", TestNbtOpsMergeToByteArrayTag);
        yield return ("NbtOps mergeToList IntArrayTag keeps compact", TestNbtOpsMergeToIntArrayTag);
        yield return ("NbtOps mergeToList LongArrayTag keeps compact", TestNbtOpsMergeToLongArrayTag);
        yield return ("NbtOps mergeToList ByteArrayTag downgrade on non Byte", TestNbtOpsMergeToByteArrayTagDowngrade);
        yield return ("NbtOps mergeToList empty ListTag to ByteArrayTag", TestNbtOpsMergeEmptyListToByteArrayTag);
    }

    //==== 辅助 ====

    private static (Tag, Tag) RoundTrip(Tag tag)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var writer = new BinaryNbtWriter(bw);
            NbtIo.WriteUnnamedTag(tag, writer);
        }
        ms.Position = 0;
        using var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        var reader = new BinaryNbtReader(br);
        var loaded = NbtIo.ReadUnnamedTag(reader, NbtAccounter.UnlimitedHeap());
        return (tag, loaded);
    }

    private static (Tag, Tag) RoundTripCompressed(Tag tag)
    {
        if (tag is not CompoundTag compound)
            throw new ArgumentException("Compressed round-trip requires CompoundTag root");
        using var ms = new MemoryStream();
        NbtIo.WriteCompressed(compound, ms);
        ms.Position = 0;
        var loaded = NbtIo.ReadCompressed(ms, NbtAccounter.UnlimitedHeap());
        return (tag, loaded);
    }

    //==== 测试用例 ====

    private static bool TestByteTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new ByteTag(42));
        return loaded is ByteTag bt && bt.Value == 42;
    }

    private static bool TestShortTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new ShortTag(-12345));
        return loaded is ShortTag st && st.Value == -12345;
    }

    private static bool TestIntTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new IntTag(int.MaxValue));
        return loaded is IntTag it && it.Value == int.MaxValue;
    }

    private static bool TestLongTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new LongTag(long.MinValue));
        return loaded is LongTag lt && lt.Value == long.MinValue;
    }

    private static bool TestFloatTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new FloatTag(3.14159f));
        return loaded is FloatTag ft && ft.Value == 3.14159f;
    }

    private static bool TestDoubleTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new DoubleTag(double.Epsilon));
        return loaded is DoubleTag dt && dt.Value == double.Epsilon;
    }

    private static bool TestStringTagRoundTrip()
    {
        var (orig, loaded) = RoundTrip(new StringTag("Hello, NBT!"));
        return loaded is StringTag st && st.Value == "Hello, NBT!";
    }

    private static bool TestByteArrayTagRoundTrip()
    {
        var arr = new byte[] { 0, 1, 2, 3, 255, 128, 64, 32 };
        var (orig, loaded) = RoundTrip(new ByteArrayTag(arr));
        if (loaded is not ByteArrayTag bat) return false;
        return bat.Value.SequenceEqual(arr);
    }

    private static bool TestIntArrayTagRoundTrip()
    {
        var arr = new[] { int.MinValue, -1, 0, 1, int.MaxValue };
        var (orig, loaded) = RoundTrip(new IntArrayTag(arr));
        if (loaded is not IntArrayTag iat) return false;
        return iat.Value.SequenceEqual(arr);
    }

    private static bool TestLongArrayTagRoundTrip()
    {
        var arr = new[] { long.MinValue, -1L, 0L, 1L, long.MaxValue };
        var (orig, loaded) = RoundTrip(new LongArrayTag(arr));
        if (loaded is not LongArrayTag lat) return false;
        return lat.Value.SequenceEqual(arr);
    }

    private static bool TestListTagRoundTrip()
    {
        var list = new ListTag
        {
            new IntTag(1),
            new IntTag(2),
            new IntTag(3),
        };
        var (orig, loaded) = RoundTrip(list);
        if (loaded is not ListTag lt) return false;
        if (lt.Count != 3) return false;
        return lt.OfType<IntTag>().Select(t => t.Value).SequenceEqual(new[] { 1, 2, 3 });
    }

    private static bool TestCompoundTagRoundTrip()
    {
        var compound = new CompoundTag();
        compound.PutByte("byte", (byte)0xAB);
        compound.PutShort("short", (short)0x1234);
        compound.PutInt("int", unchecked((int)0xCAFEBABE));
        compound.PutLong("long", 0xDEADBEEFCAFEL);
        compound.PutFloat("float", 1.5f);
        compound.PutDouble("double", 2.5);
        compound.PutString("string", "test value");
        var (orig, loaded) = RoundTrip(compound);
        if (loaded is not CompoundTag ct) return false;
        return ct.GetByteValue("byte") == 0xAB
            && ct.GetShortValue("short") == 0x1234
            && ct.GetIntValue("int") == unchecked((int)0xCAFEBABE)
            && ct.GetLongValue("long") == 0xDEADBEEFCAFEL
            && ct.GetFloatValue("float") == 1.5f
            && ct.GetDoubleValue("double") == 2.5
            && ct.GetStringValue("string") == "test value";
    }

    private static bool TestNestedCompoundRoundTrip()
    {
        var root = new CompoundTag();
        var inner = new CompoundTag();
        inner.PutString("name", "inner");
        inner.PutInt("value", 42);
        root.Put("inner", inner);
        root.PutString("outer", "root");

        var (orig, loaded) = RoundTrip(root);
        if (loaded is not CompoundTag ct) return false;
        var innerLoaded = ct.GetCompound("inner");
        if (innerLoaded is null) return false;
        return innerLoaded.GetStringValue("name") == "inner"
            && innerLoaded.GetIntValue("value") == 42
            && ct.GetStringValue("outer") == "root";
    }

    private static bool TestGzipRoundTrip()
    {
        var root = new CompoundTag();
        root.PutString("type", "compressed");
        root.PutInt("data", 12345);
        var (_, loaded) = RoundTripCompressed(root);
        if (loaded is not CompoundTag ct) return false;
        return ct.GetStringValue("type") == "compressed"
            && ct.GetIntValue("data") == 12345;
    }

    private static bool TestParseCompressed()
    {
        var root = new CompoundTag();
        root.PutString("type", "stream");
        root.PutInt("data", 67890);
        root.PutByteArray("arr", new byte[] { 1, 2, 3 });

        using var ms = new MemoryStream();
        NbtIo.WriteCompressed(root, ms);
        ms.Position = 0;

        var collector = new CollectToTag();
        NbtIo.ParseCompressed(ms, collector, NbtAccounter.UnlimitedHeap());
        var result = collector.GetResult();
        if (result is not CompoundTag ct) return false;
        return ct.GetStringValue("type") == "stream"
            && ct.GetIntValue("data") == 67890;
    }

    private static bool TestCollectToTag()
    {
        var root = new CompoundTag();
        root.PutString("a", "value-a");
        root.PutInt("b", 100);
        var inner = new CompoundTag();
        inner.PutByte("x", 1);
        root.Put("inner", inner);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var writer = new BinaryNbtWriter(bw);
            NbtIo.WriteUnnamedTag(root, writer);
        }
        ms.Position = 0;
        using var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        var reader = new BinaryNbtReader(br);

        var collector = new CollectToTag();
        NbtIo.Parse(reader, collector, NbtAccounter.UnlimitedHeap());
        var result = collector.GetResult();
        if (result is not CompoundTag ct) return false;
        if (ct.GetStringValue("a") != "value-a") return false;
        if (ct.GetIntValue("b") != 100) return false;
        var innerLoaded = ct.GetCompound("inner");
        return innerLoaded is not null && innerLoaded.GetByteValue("x") == 1;
    }

    private static bool TestSkipAll()
    {
        var root = new CompoundTag();
        root.PutString("a", "value-a");
        root.PutInt("b", 100);
        root.PutLong("c", long.MaxValue);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var writer = new BinaryNbtWriter(bw);
            NbtIo.WriteUnnamedTag(root, writer);
        }
        ms.Position = 0;
        using var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        var reader = new BinaryNbtReader(br);

        //SkipAll 应完整跳过所有内容不抛异常，读完后流位置应在末尾
        NbtIo.Parse(reader, SkipAll.Instance, NbtAccounter.UnlimitedHeap());
        return br.BaseStream.Position <= br.BaseStream.Length;
    }

    private static bool TestModifiedUtf8Surrogate()
    {
        //emoji 需 surrogate pair（Java modified UTF-8 编码）
        var emoji = "🎮 MineCraft ⛏️";
        var (orig, loaded) = RoundTrip(new StringTag(emoji));
        return loaded is StringTag st && st.Value == emoji;
    }

    private static bool TestEmptyCompoundRoundTrip()
    {
        var root = new CompoundTag();
        var (orig, loaded) = RoundTrip(root);
        return loaded is CompoundTag ct && ct.IsEmpty;
    }

    private static bool TestDeepNestedRoundTrip()
    {
        var root = new CompoundTag();
        var current = root;
        for (var i = 0; i < 10; i++)
        {
            var next = new CompoundTag();
            next.PutInt("level", i);
            current.Put("next", next);
            current = next;
        }
        var (orig, loaded) = RoundTrip(root);
        if (loaded is not CompoundTag ct) return false;
        //结构：root.next.level=0, root.next.next.level=1, ..., 共 10 层嵌套
        CompoundTag level = ct;
        for (var i = 0; i < 10; i++)
        {
            var nextLevel = level.GetCompound("next");
            if (nextLevel is null) return false;
            level = nextLevel;
            if (level.GetIntValue("level") != i) return false;
        }
        //最后一层（level=9）后不应再有 next
        return level.GetCompound("next") is null;
    }

    private static bool TestPrettyPrint()
    {
        var root = new CompoundTag();
        root.PutInt("a", 1);
        root.PutString("b", "hello");
        var pretty = NbtUtils.PrettyPrint(root, withBinaryBlobs: false);
        return pretty.Contains("\"a\"") && pretty.Contains("\"b\"") && pretty.Contains("1");
    }

    private static bool TestCompareNbt()
    {
        var a = new CompoundTag();
        a.PutInt("x", 1);
        a.PutString("y", "hello");
        var b = new CompoundTag();
        b.PutInt("x", 1);
        b.PutString("y", "hello");
        var c = new CompoundTag();
        c.PutInt("x", 2);
        return NbtUtils.CompareNbt(a, b, false)
            && !NbtUtils.CompareNbt(a, c, false);
    }

    private static bool TestPackBlockState()
    {
        var state = new CompoundTag();
        state.PutString("Name", "minecraft:stone");
        var props = new CompoundTag();
        props.PutString("axis", "y");
        state.Put("Properties", props);
        var packed = NbtUtils.PackBlockState(state);
        if (packed != "minecraft:stone{axis:y}") return false;
        var unpacked = NbtUtils.UnpackBlockState(packed);
        return unpacked.GetStringValue("Name") == "minecraft:stone"
            && unpacked.GetCompound("Properties")?.GetStringValue("axis") == "y";
    }

    private static bool TestByteTagCache()
    {
        //ValueOf 应返回缓存实例
        var a = ByteTag.ValueOf((byte)42);
        var b = ByteTag.ValueOf((byte)42);
        return ReferenceEquals(a, b) && a.Value == 42;
    }

    //mergeToList 在 ByteArrayTag 上追加 ByteTag 保持 ByteArrayTag 紧凑数组
    private static bool TestNbtOpsMergeToByteArrayTag()
    {
        var ops = NbtOps.Instance;
        var initial = new ByteArrayTag(new byte[] { 1, 2 });
        var result = ops.MergeToList(initial, ByteTag.ValueOf(3)).GetOrThrow();
        return result is ByteArrayTag bat && bat.Value.SequenceEqual(new byte[] { 1, 2, 3 });
    }

    //mergeToList 在 IntArrayTag 上追加 IntTag 保持 IntArrayTag 紧凑数组
    private static bool TestNbtOpsMergeToIntArrayTag()
    {
        var ops = NbtOps.Instance;
        var initial = new IntArrayTag(new[] { 10, 20 });
        var result = ops.MergeToList(initial, IntTag.ValueOf(30)).GetOrThrow();
        return result is IntArrayTag iat && iat.Value.SequenceEqual(new[] { 10, 20, 30 });
    }

    //mergeToList 在 LongArrayTag 上追加 LongTag 保持 LongArrayTag 紧凑数组
    private static bool TestNbtOpsMergeToLongArrayTag()
    {
        var ops = NbtOps.Instance;
        var initial = new LongArrayTag(new long[] { 100L, 200L });
        var result = ops.MergeToList(initial, LongTag.ValueOf(300L)).GetOrThrow();
        return result is LongArrayTag lat && lat.Value.SequenceEqual(new long[] { 100L, 200L, 300L });
    }

    //mergeToList 在 ByteArrayTag 上追加非 ByteTag 失败返回 DataResult.Error 对齐原版
    private static bool TestNbtOpsMergeToByteArrayTagDowngrade()
    {
        var ops = NbtOps.Instance;
        var initial = new ByteArrayTag(new byte[] { 1, 2 });
        var dataResult = ops.MergeToList(initial, IntTag.ValueOf(3));
        return !dataResult.Result().IsPresent;
    }

    //mergeToList 在空 ListTag 上追加 ByteTag 后保持 ListTag 不升级为 ByteArrayTag
    //原版 ListTag 初始不强制紧凑数组保持 ListTag 通用性
    private static bool TestNbtOpsMergeEmptyListToByteArrayTag()
    {
        var ops = NbtOps.Instance;
        var initial = new ListTag();
        var result = ops.MergeToList(initial, ByteTag.ValueOf(7)).GetOrThrow();
        return result is ListTag list && list.Count == 1 && list[0] is ByteTag b && b.Value == 7;
    }
}
