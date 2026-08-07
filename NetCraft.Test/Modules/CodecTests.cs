using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Test.Modules;

//Codec 子系统测试
//验证 DataResult/Optional/NbtOps/CompoundTag.Store/Read 基础流程
internal static class CodecTests
{
    public const string Module = "codec";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("DataResult Success GetOrThrow", TestDataResultSuccess);
        yield return ("DataResult Error throws", TestDataResultError);
        yield return ("DataResult Map chains", TestDataResultMap);
        yield return ("Optional Of/Map/OrElse", TestOptionalMap);
        yield return ("NbtOps CreateInt/CreateString", TestNbtOpsCreate);
        yield return ("NbtOps GetMap on CompoundTag", TestNbtOpsGetMap);
        yield return ("NbtOps MergeToMap single", TestNbtOpsMergeToMap);
        yield return ("NbtOps ConvertTo self-reflect", TestNbtOpsConvertTo);
        yield return ("CompoundTag Store/Read int", TestCompoundStoreReadInt);
        yield return ("CompoundTag Store/Read string", TestCompoundStoreReadString);
        yield return ("CompoundTag Store/Read bool", TestCompoundStoreReadBool);
        yield return ("CompoundTag StoreNullable skips null", TestCompoundStoreNullableNull);
        yield return ("RecordCodecBuilder Of2 round-trip", TestRecordCodecOf2RoundTrip);
        yield return ("RecordCodecBuilder Of3 round-trip", TestRecordCodecOf3RoundTrip);
        yield return ("FieldMapCodec missing key returns error", TestFieldMapCodecMissingKey);
        yield return ("OptionalFieldMapCodec uses default", TestOptionalFieldMapCodecDefault);
        yield return ("OptionalFieldMapCodecOptional returns Empty", TestOptionalFieldMapCodecOptionalEmpty);
        yield return ("Nested RecordCodecBuilder round-trip", TestNestedRecordRoundTrip);
        yield return ("RecordCodecBuilder Of5 round-trip", TestRecordCodecOf5RoundTrip);
        yield return ("IdentifierCodec round-trip", TestIdentifierCodec);
        yield return ("PropertiesCodec round-trip", TestPropertiesCodecRoundTrip);
        yield return ("PropertiesCodec no properties", TestPropertiesCodecNoProps);
    }

    private static bool TestDataResultSuccess()
    {
        var r = DataResult<int>.Success(42);
        return r.GetOrThrow() == 42 && r.Result().IsPresent;
    }

    private static bool TestDataResultError()
    {
        var r = DataResult<int>.Error(() => "bad");
        try { r.GetOrThrow(); return false; }
        catch (InvalidOperationException) { }
        return !r.Result().IsPresent;
    }

    private static bool TestDataResultMap()
    {
        var r = DataResult<int>.Success(10).Map(x => x * 2);
        return r.GetOrThrow() == 20;
    }

    private static bool TestOptionalMap()
    {
        var o = Optional<int>.Of(1).Map(x => x + 1);
        return o.IsPresent && o.Get() == 2 && Optional<int>.Empty().OrElse(99) == 99;
    }

    private static bool TestNbtOpsCreate()
    {
        var i = NbtOps.Instance.CreateInt(42);
        var s = NbtOps.Instance.CreateString("hi");
        return i is IntTag it && it.Value == 42
            && s is StringTag st && st.Value == "hi";
    }

    private static bool TestNbtOpsGetMap()
    {
        var compound = new CompoundTag();
        compound.PutInt("a", 1);
        var mapResult = NbtOps.Instance.GetMap(compound);
        if (!mapResult.Result().IsPresent) return false;
        var map = mapResult.GetOrThrow();
        return map.Get("a").IsPresent && map.Get("a").Get() is IntTag v && v.Value == 1;
    }

    private static bool TestNbtOpsMergeToMap()
    {
        var empty = NbtOps.Instance.EmptyMap();
        var merged = NbtOps.Instance.MergeToMap(empty, NbtOps.Instance.CreateString("k"), NbtOps.Instance.CreateInt(7));
        if (!merged.Result().IsPresent) return false;
        var result = merged.GetOrThrow();
        return result is CompoundTag c && c.GetIntValue("k") == 7;
    }

    private static bool TestNbtOpsConvertTo()
    {
        var intTag = NbtOps.Instance.CreateInt(42);
        var converted = NbtOps.Instance.ConvertTo(NbtOps.Instance, intTag);
        return converted is IntTag it && it.Value == 42;
    }

    private static bool TestCompoundStoreReadInt()
    {
        var compound = new CompoundTag();
        compound.Store("x", Codecs.Int, 42);
        var read = compound.Read<int>("x", Codecs.Int);
        return read.IsPresent && read.Get() == 42;
    }

    private static bool TestCompoundStoreReadString()
    {
        var compound = new CompoundTag();
        compound.Store("name", Codecs.String, "hello");
        var read = compound.Read<string>("name", Codecs.String);
        return read.IsPresent && read.Get() == "hello";
    }

    private static bool TestCompoundStoreReadBool()
    {
        var compound = new CompoundTag();
        compound.Store("flag", Codecs.Bool, true);
        var read = compound.Read<bool>("flag", Codecs.Bool);
        return read.IsPresent && read.Get();
    }

    private static bool TestCompoundStoreNullableNull()
    {
        var compound = new CompoundTag();
        compound.StoreNullable<string>("opt", Codecs.String, null);
        return !compound.Contains("opt");
    }

    private static bool TestRecordCodecOf2RoundTrip()
    {
        var point = new TestPoint(3, 4);
        var compound = new CompoundTag();
        compound.Store("point", TestPoint.Codec, point);
        var read = compound.Read<TestPoint>("point", TestPoint.Codec);
        return read.IsPresent && read.Get().X == 3 && read.Get().Y == 4;
    }

    private static bool TestRecordCodecOf3RoundTrip()
    {
        var box = new TestBox(1, 2, 3);
        var compound = new CompoundTag();
        compound.Store("box", TestBox.Codec, box);
        var read = compound.Read<TestBox>("box", TestBox.Codec);
        return read.IsPresent && read.Get() == box;
    }

    //Of5 round-trip 验证 5 字段 codec 完整编解码路径
    private static bool TestRecordCodecOf5RoundTrip()
    {
        var player = new TestPlayer("alice", 100, 200, 50.5, true);
        var compound = new CompoundTag();
        compound.Store("player", TestPlayer.Codec, player);
        var read = compound.Read<TestPlayer>("player", TestPlayer.Codec);
        return read.IsPresent && read.Get() == player;
    }

    private static bool TestFieldMapCodecMissingKey()
    {
        var compound = new CompoundTag();
        var codec = (Codec<int>)Codecs.Int.FieldOf("x");
        var result = codec.Parse(NbtOps.Instance, compound);
        return !result.Result().IsPresent;
    }

    private static bool TestOptionalFieldMapCodecDefault()
    {
        var compound = new CompoundTag();
        var codec = (Codec<int>)Codecs.Int.OptionalFieldOf("x", 42);
        var result = codec.Parse(NbtOps.Instance, compound);
        return result.Result().IsPresent && result.GetOrThrow() == 42;
    }

    private static bool TestOptionalFieldMapCodecOptionalEmpty()
    {
        var compound = new CompoundTag();
        var codec = (Codec<Optional<int>>)Codecs.Int.OptionalFieldOf("x");
        var result = codec.Parse(NbtOps.Instance, compound);
        return result.Result().IsPresent && !result.GetOrThrow().IsPresent;
    }

    private static bool TestNestedRecordRoundTrip()
    {
        var rect = new TestRect(new TestPoint(1, 2), new TestPoint(3, 4));
        var compound = new CompoundTag();
        compound.Store("rect", TestRect.Codec, rect);
        var read = compound.Read<TestRect>("rect", TestRect.Codec);
        return read.IsPresent
            && read.Get().Origin.X == 1 && read.Get().Origin.Y == 2
            && read.Get().Size.X == 3 && read.Get().Size.Y == 4;
    }

    //2字段record测试类型
    internal sealed record TestPoint(int X, int Y)
    {
        public static readonly Codec<TestPoint> Codec = RecordCodecBuilder.Of2(
            Codecs.Int.FieldOf("x").ForGetter((TestPoint p) => p.X),
            Codecs.Int.FieldOf("y").ForGetter((TestPoint p) => p.Y),
            (x, y) => new TestPoint(x, y));
    }

    //3字段record测试类型
    internal sealed record TestBox(int W, int H, int D)
    {
        public static readonly Codec<TestBox> Codec = RecordCodecBuilder.Of3(
            Codecs.Int.FieldOf("w").ForGetter((TestBox b) => b.W),
            Codecs.Int.FieldOf("h").ForGetter((TestBox b) => b.H),
            Codecs.Int.FieldOf("d").ForGetter((TestBox b) => b.D),
            (w, h, d) => new TestBox(w, h, d));
    }

    //嵌套record测试类型验证内层record codec作为字段
    internal sealed record TestRect(TestPoint Origin, TestPoint Size)
    {
        public static readonly Codec<TestRect> Codec = RecordCodecBuilder.Of2(
            TestPoint.Codec.FieldOf("origin").ForGetter((TestRect r) => r.Origin),
            TestPoint.Codec.FieldOf("size").ForGetter((TestRect r) => r.Size),
            (o, s) => new TestRect(o, s));
    }

    //5字段record测试类型验证Of5编解码
    internal sealed record TestPlayer(string Name, int Hp, int Mp, double Pos, bool Active)
    {
        public static readonly Codec<TestPlayer> Codec = RecordCodecBuilder.Of5(
            Codecs.String.FieldOf("name").ForGetter((TestPlayer p) => p.Name),
            Codecs.Int.FieldOf("hp").ForGetter((TestPlayer p) => p.Hp),
            Codecs.Int.FieldOf("mp").ForGetter((TestPlayer p) => p.Mp),
            Codecs.Double.FieldOf("pos").ForGetter((TestPlayer p) => p.Pos),
            Codecs.Bool.FieldOf("active").ForGetter((TestPlayer p) => p.Active),
            (name, hp, mp, pos, active) => new TestPlayer(name, hp, mp, pos, active));
    }

    private static bool TestIdentifierCodec()
    {
        var id = Identifier.WithDefaultNamespace("stone");
        var encoded = IdentifierCodec.Instance.EncodeStart(NbtOps.Instance, id);
        if (!encoded.Result().IsPresent) return false;
        var parsed = IdentifierCodec.Instance.Parse(NbtOps.Instance, encoded.GetOrThrow());
        return parsed.Result().IsPresent && parsed.GetOrThrow() == id;
    }

    private static bool TestPropertiesCodecRoundTrip()
    {
        var id = Identifier.WithDefaultNamespace("test_block");
        var block = TestBlock.GetOrCreate(id);
        var stateCodec = StateDefinitionCodecs.PropertiesCodec<TestBlock, TestBlockState>(
            TestBlock.Codec, b => b.Definition);

        var powered = block.Powered;
        var target = block.Definition.PossibleStates.First(s => s.GetValue(powered) && s.GetValue(block.Age) == 1);

        var encoded = stateCodec.EncodeStart(NbtOps.Instance, target);
        if (!encoded.Result().IsPresent) return false;
        var parsed = stateCodec.Parse(NbtOps.Instance, encoded.GetOrThrow());
        if (!parsed.Result().IsPresent) return false;
        var restored = parsed.GetOrThrow();
        return restored.Owner.Id == target.Owner.Id
            && restored.GetValue(powered) == target.GetValue(powered)
            && restored.GetValue(block.Age) == target.GetValue(block.Age);
    }

    private static bool TestPropertiesCodecNoProps()
    {
        var id = Identifier.WithDefaultNamespace("empty_block");
        var block = TestBlock.GetOrCreateNoProps(id);
        var stateCodec = StateDefinitionCodecs.PropertiesCodec<TestBlock, TestBlockState>(
            TestBlock.Codec, b => b.Definition);

        var state = block.Definition.Any();
        var encoded = stateCodec.EncodeStart(NbtOps.Instance, state);
        if (!encoded.Result().IsPresent) return false;
        var parsed = stateCodec.Parse(NbtOps.Instance, encoded.GetOrThrow());
        return parsed.Result().IsPresent && parsed.GetOrThrow().Owner.Id == state.Owner.Id;
    }

    //测试用 Block stub持有Identifier和StateDefinition
    private sealed class TestBlock : Block
    {
        private static readonly Dictionary<Identifier, TestBlock> Cache = new();

        public override Identifier Id { get; }
        public override BlockState DefaultBlockState => default;
        public StateDefinition<TestBlock, TestBlockState> Definition { get; }
        public BooleanProperty Powered { get; }
        public IntegerProperty Age { get; }

        private TestBlock(Identifier id, bool withProps)
        {
            Id = id;
            Powered = new BooleanProperty("powered");
            Age = new IntegerProperty("age", 0, 1);
            var builder = new StateDefinition<TestBlock, TestBlockState>.Builder(this);
            if (withProps) builder.Add(Powered, Age);
            Definition = builder.Create(_ => null!, new TestBlockStateFactory());
        }

        public static TestBlock GetOrCreate(Identifier id)
        {
            if (Cache.TryGetValue(id, out var existing)) return existing;
            var block = new TestBlock(id, true);
            Cache[id] = block;
            return block;
        }

        public static TestBlock GetOrCreateNoProps(Identifier id)
        {
            if (Cache.TryGetValue(id, out var existing)) return existing;
            var block = new TestBlock(id, false);
            Cache[id] = block;
            return block;
        }

        //TestBlock codec用IdentifierCodec映射保持Cache一致
        public static readonly Codec<TestBlock> Codec = IdentifierCodec.Instance.ComapFlatMap(
            id =>
            {
                var block = GetOrCreate(id);
                return DataResult<TestBlock>.Success(block);
            },
            b => b.Id);
    }

    private sealed class TestBlockState : StateHolder<TestBlock, TestBlockState>
    {
        public TestBlockState(TestBlock owner, PropertyBase[] propertyKeys, object?[] propertyValues)
            : base(owner, propertyKeys, propertyValues) { }
    }

    private sealed class TestBlockStateFactory : StateFactory<TestBlock, TestBlockState>
    {
        public TestBlockState Create(TestBlock owner, PropertyBase[] propertyKeys, object?[] propertyValues)
            => new(owner, propertyKeys, propertyValues);
    }
}
