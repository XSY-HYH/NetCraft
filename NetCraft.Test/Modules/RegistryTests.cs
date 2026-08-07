using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Test.Modules;

//注册表 + State 子系统测试
//覆盖 Identifier/ResourceKey/Registry/MappedRegistry/DefaultedMappedRegistry/RegistryAccess
//BuiltInRegistries/TagKey 以及 State 框架（Property/StateHolder/StateDefinition/BlockState）
//从 NetCraft.Registry.Tests 迁移合并
internal static class RegistryTests
{
    public const string Module = "registry";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("Identifier.Parse minecraft:air", TestIdentifierParse);
        yield return ("Identifier equality is value-based", TestIdentifierEquality);
        yield return ("Identifier.WithDefaultNamespace", TestIdentifierWithDefaultNamespace);
        yield return ("ResourceKey intern dedup", TestResourceKeyIntern);
        yield return ("ResourceKey.RegistryKey round-trip", TestResourceKeyRegistryKey);
        yield return ("ResourceKey.Cast cross-type", TestResourceKeyCast);
        yield return ("MappedRegistry Register + GetByLocation", TestMappedRegistryRegister);
        yield return ("MappedRegistry GetId/ById consistency", TestMappedRegistryIdConsistency);
        yield return ("MappedRegistry Freeze binds Holder", TestMappedRegistryFreeze);
        yield return ("MappedRegistry duplicate key throws", TestMappedRegistryDuplicateKey);
        yield return ("MappedRegistry Freeze twice idempotent", TestMappedRegistryFreezeTwice);
        yield return ("DefaultedMappedRegistry fallback to default", TestDefaultedFallback);
        yield return ("DefaultedMappedRegistry ById fallback", TestDefaultedByIdFallback);
        yield return ("DefaultedMappedRegistry GetOptional no fallback", TestDefaultedGetOptionalNoFallback);
        yield return ("RegistryAccess.ImmutableRegistryAccess Lookup", TestImmutableRegistryAccessLookup);
        yield return ("RegistryAccess.LookupOrThrow throws on missing", TestRegistryAccessLookupOrThrow);
        yield return ("BuiltInRegistries BLOCK not null", TestBuiltInRegistriesBlock);
        yield return ("BuiltInRegistries ITEM is DefaultedRegistry", TestBuiltInRegistriesItemDefaulted);
        yield return ("TagKey intern dedup", TestTagKeyIntern);
        yield return ("TagKey.IsFor registry check", TestTagKeyIsFor);
        yield return ("Registry BindTags binds Named HolderSet", TestRegistryBindTags);
        yield return ("Registry Get(TagKey) returns null on missing", TestRegistryGetTagMissing);
        yield return ("Registry holds tag after BindTags", TestRegistryHoldsTag);
        yield return ("Reference Tags() reflects bound tags", TestReferenceTagsReflectsBind);
        yield return ("HolderLookup.ListElements enumerates", TestHolderLookupListElements);
        yield return ("HolderLookup.Get by ResourceKey", TestHolderLookupGetByKey);
        yield return ("HolderLookup.ListTags returns bound", TestHolderLookupListTags);
        yield return ("HolderLookupProvider.ListRegistryKeys", TestHolderLookupProviderListRegistryKeys);
        yield return ("DataComponentMap as DataComponentLookup", TestDataComponentMapAsLookup);
        yield return ("Registry.GetRandom returns in-range Holder", TestRegistryGetRandom);
        yield return ("Registry.GetRandom empty returns null", TestRegistryGetRandomEmpty);
        yield return ("CreateRegistrationLookup finds own registry", TestCreateRegistrationLookupOwn);
        yield return ("CreateRegistrationLookup other registry null", TestCreateRegistrationLookupOther);

        yield return ("BooleanProperty values & name mapping", TestBooleanProperty);
        yield return ("IntegerProperty values & name mapping", TestIntegerProperty);
        yield return ("EnumProperty values & name mapping", TestEnumProperty);
        yield return ("Property ValueCodec round trip", TestPropertyValueCodec);
        yield return ("StateDefinition enumerates all combinations", TestStateDefinitionCombinations);
        yield return ("StateHolder GetValue returns correct value", TestStateHolderGetValue);
        yield return ("StateHolder SetValue returns neighbor O(1)", TestStateHolderSetValue);
        yield return ("StateHolder Cycle wraps around", TestStateHolderCycle);
        yield return ("StateHolder HasProperty", TestStateHolderHasProperty);
        yield return ("StateHolder TrySetValue missing returns self", TestStateHolderTrySetValueMissing);
        yield return ("StateDefinition singleton state (no props)", TestStateDefinitionSingleton);
        yield return ("StateDefinition invalid name throws", TestStateDefinitionInvalidName);
    }

    //==== Identifier ====

    private static bool TestIdentifierParse()
    {
        var id = Identifier.Parse("minecraft:air");
        return id.Namespace == "minecraft" && id.Path == "air";
    }

    private static bool TestIdentifierEquality()
    {
        var a = Identifier.Parse("minecraft:stone");
        var b = Identifier.Parse("minecraft:stone");
        var c = Identifier.Parse("minecraft:dirt");
        return a == b && a != c && a.GetHashCode() == b.GetHashCode();
    }

    private static bool TestIdentifierWithDefaultNamespace()
    {
        var id = Identifier.WithDefaultNamespace("diamond");
        return id.Namespace == "minecraft" && id.Path == "diamond";
    }

    //==== ResourceKey ====

    private static bool TestResourceKeyIntern()
    {
        var regKey = ResourceKeys.CreateRegistryKey<object>(Identifier.WithDefaultNamespace("test_reg"));
        var k1 = ResourceKey<object>.Create(regKey, Identifier.Parse("minecraft:foo"));
        var k2 = ResourceKey<object>.Create(regKey, Identifier.Parse("minecraft:foo"));
        return ReferenceEquals(k1, k2);
    }

    private static bool TestResourceKeyRegistryKey()
    {
        var regKey = ResourceKeys.CreateRegistryKey<object>(Identifier.WithDefaultNamespace("test_reg2"));
        var elemKey = ResourceKey<object>.Create(regKey, Identifier.Parse("minecraft:bar"));
        var back = elemKey.RegistryKey();
        return back == regKey;
    }

    private static bool TestResourceKeyCast()
    {
        var regKey = ResourceKeys.CreateRegistryKey<object>(Identifier.WithDefaultNamespace("cast_reg"));
        var elemKey = ResourceKey<object>.Create(regKey, Identifier.Parse("minecraft:baz"));
        var casted = elemKey.Cast(regKey);
        return casted is not null && casted.Identifier == elemKey.Identifier;
    }

    //==== MappedRegistry ====

    private static bool TestMappedRegistryRegister()
    {
        var reg = NewTestRegistry();
        var key = ResourceKey<string>.Create(reg.Key, Identifier.Parse("minecraft:foo"));
        reg.Register(key, "FooValue", RegistrationInfo.BuiltIn);
        reg.Freeze();
        var holder = reg.Get(Identifier.Parse("minecraft:foo"));
        return holder is not null && holder.Value == "FooValue";
    }

    private static bool TestMappedRegistryIdConsistency()
    {
        var reg = NewTestRegistry();
        var key = ResourceKey<string>.Create(reg.Key, Identifier.Parse("minecraft:hello"));
        reg.Register(key, "HelloValue", RegistrationInfo.BuiltIn);
        reg.Freeze();
        var id = reg.GetId("HelloValue");
        return id >= 0 && reg.ById(id) == "HelloValue";
    }

    private static bool TestMappedRegistryFreeze()
    {
        var reg = NewTestRegistry();
        var key = ResourceKey<string>.Create(reg.Key, Identifier.Parse("minecraft:frozen"));
        reg.Register(key, "FrozenValue", RegistrationInfo.BuiltIn);
        reg.Freeze();
        return reg.Get(Identifier.Parse("minecraft:frozen"))?.Value == "FrozenValue";
    }

    private static bool TestMappedRegistryDuplicateKey()
    {
        var reg = NewTestRegistry();
        var key = ResourceKey<string>.Create(reg.Key, Identifier.Parse("minecraft:dup"));
        reg.Register(key, "First", RegistrationInfo.BuiltIn);
        try
        {
            reg.Register(key, "Second", RegistrationInfo.BuiltIn);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool TestMappedRegistryFreezeTwice()
    {
        var reg = NewTestRegistry();
        reg.Freeze();
        reg.Freeze();
        return true;
    }

    //==== DefaultedMappedRegistry ====

    private static bool TestDefaultedFallback()
    {
        var key = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("defaulted_reg"));
        var reg = new DefaultedMappedRegistry<string>("minecraft:default", key, Lifecycle.Stable);
        var defaultKey = ResourceKey<string>.Create(key, Identifier.Parse("minecraft:default"));
        reg.Register(defaultKey, "DefaultValue", RegistrationInfo.BuiltIn);
        reg.Freeze();
        return reg.GetValue(Identifier.Parse("minecraft:nonexistent")) == "DefaultValue";
    }

    private static bool TestDefaultedByIdFallback()
    {
        var key = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("defaulted_reg2"));
        var reg = new DefaultedMappedRegistry<string>("minecraft:default", key, Lifecycle.Stable);
        var defaultKey = ResourceKey<string>.Create(key, Identifier.Parse("minecraft:default"));
        reg.Register(defaultKey, "DefaultValue", RegistrationInfo.BuiltIn);
        reg.Freeze();
        return reg.ById(9999) == "DefaultValue";
    }

    private static bool TestDefaultedGetOptionalNoFallback()
    {
        var key = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("defaulted_reg3"));
        var reg = new DefaultedMappedRegistry<string>("minecraft:default", key, Lifecycle.Stable);
        var defaultKey = ResourceKey<string>.Create(key, Identifier.Parse("minecraft:default"));
        reg.Register(defaultKey, "DefaultValue", RegistrationInfo.BuiltIn);
        var opt = ((Registry<string>)reg).GetOptional(Identifier.Parse("minecraft:nonexistent"));
        return opt is null;
    }

    //==== RegistryAccess ====

    private static bool TestImmutableRegistryAccessLookup()
    {
        var regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("lookup_test_reg"));
        var reg = NewTestRegistry(regKey);
        var access = new ImmutableRegistryAccess(new[] {
            new RegistryEntry(regKey.Identifier, reg)
        });
        var found = access.Lookup<string>(regKey);
        return ReferenceEquals(found, reg);
    }

    private static bool TestRegistryAccessLookupOrThrow()
    {
        var access = new ImmutableRegistryAccess(Array.Empty<RegistryEntry>());
        try
        {
            var missingKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("missing_reg"));
            ((RegistryAccess)access).LookupOrThrow<string>(missingKey);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    //==== BuiltInRegistries ====

    private static bool TestBuiltInRegistriesBlock()
    {
        return BuiltInRegistries.BLOCK is not null;
    }

    private static bool TestBuiltInRegistriesItemDefaulted()
    {
        return BuiltInRegistries.ITEM is DefaultedRegistry<Item>;
    }

    //==== TagKey ====

    private static bool TestTagKeyIntern()
    {
        var regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("tag_test_reg"));
        var t1 = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:foo"));
        var t2 = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:foo"));
        return ReferenceEquals(t1, t2);
    }

    private static bool TestTagKeyIsFor()
    {
        var regKey1 = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("tag_test_reg_a"));
        var regKey2 = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("tag_test_reg_b"));
        var t = TagKey<string>.Create(regKey1, Identifier.Parse("minecraft:foo"));
        return t.IsFor(regKey1) && !t.IsFor(regKey2);
    }

    //构建已冻结注册表带 3 个 string 值供 tag 测试用
    private static MappedRegistry<string> NewFrozenTagTestRegistry(
        out ResourceKey<Registry<string>> regKey,
        out Reference<string> fooRef,
        out Reference<string> barRef,
        out Reference<string> bazRef)
    {
        regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("tag_test_registry"));
        var reg = new MappedRegistry<string>(regKey, Lifecycle.Stable);
        var fooKey = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:foo"));
        var barKey = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:bar"));
        var bazKey = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:baz"));
        fooRef = reg.Register(fooKey, "Foo", RegistrationInfo.BuiltIn);
        barRef = reg.Register(barKey, "Bar", RegistrationInfo.BuiltIn);
        bazRef = reg.Register(bazKey, "Baz", RegistrationInfo.BuiltIn);
        reg.Freeze();
        return reg;
    }

    private static bool TestRegistryBindTags()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var fooRef, out var barRef, out var bazRef);
        var tagKey = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:group_ab"));
        var pendingTags = new Dictionary<TagKey<string>, IReadOnlyList<Holder<string>>>
        {
            [tagKey] = new List<Holder<string>> { fooRef, barRef },
        };
        reg.BindTags(pendingTags);

        var named = reg.Get(tagKey);
        if (named is null) return false;
        if (named.Size != 2) return false;
        if (!named.IsBound) return false;
        if (named.Key != tagKey) return false;

        var holders = named.Select(h => h.Value).ToList();
        if (holders.Count != 2) return false;
        if (!holders.Contains("Foo") || !holders.Contains("Bar")) return false;
        if (holders.Contains("Baz")) return false;
        return true;
    }

    private static bool TestRegistryGetTagMissing()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out _, out _, out _);
        var missingTag = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:nonexistent"));
        return reg.Get(missingTag) is null;
    }

    private static bool TestRegistryHoldsTag()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var fooRef, out _, out _);
        var tagKey = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:solo"));
        reg.BindTags(new Dictionary<TagKey<string>, IReadOnlyList<Holder<string>>>
        {
            [tagKey] = new List<Holder<string>> { fooRef },
        });

        if (!reg.Holds(tagKey)) return false;
        var missingTag = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:missing"));
        if (reg.Holds(missingTag)) return false;

        var allTags = reg.GetTags().ToList();
        return allTags.Count == 1 && allTags[0].Key == tagKey;
    }

    private static bool TestReferenceTagsReflectsBind()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var fooRef, out var barRef, out _);
        var tagA = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:tag_a"));
        var tagB = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:tag_b"));
        reg.BindTags(new Dictionary<TagKey<string>, IReadOnlyList<Holder<string>>>
        {
            [tagA] = new List<Holder<string>> { fooRef },
            [tagB] = new List<Holder<string>> { fooRef, barRef },
        });

        var fooTags = fooRef.Tags().ToList();
        if (!fooTags.Contains(tagA)) return false;
        if (!fooTags.Contains(tagB)) return false;

        var barTags = barRef.Tags().ToList();
        if (barTags.Contains(tagA)) return false;
        if (!barTags.Contains(tagB)) return false;

        return fooRef.Is(tagA) && fooRef.Is(tagB) && !barRef.Is(tagA);
    }

    //==== HolderLookup 子系统 ====

    private static bool TestHolderLookupListElements()
    {
        var reg = NewFrozenTagTestRegistry(out _, out var fooRef, out var barRef, out var bazRef);
        var elements = reg.ListElements().ToList();
        if (elements.Count != 3) return false;
        if (!elements.Contains(fooRef)) return false;
        if (!elements.Contains(barRef)) return false;
        if (!elements.Contains(bazRef)) return false;
        return true;
    }

    private static bool TestHolderLookupGetByKey()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var fooRef, out _, out _);
        var fooKey = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:foo"));
        var holder = reg.Get(fooKey);
        if (holder is null) return false;
        if (!ReferenceEquals(holder, fooRef)) return false;
        if (holder.Value != "Foo") return false;
        var missingKey = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:missing"));
        return reg.Get(missingKey) is null;
    }

    private static bool TestHolderLookupListTags()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var fooRef, out var barRef, out _);
        var tagA = TagKey<string>.Create(regKey, Identifier.Parse("minecraft:tag_a"));
        reg.BindTags(new Dictionary<TagKey<string>, IReadOnlyList<Holder<string>>>
        {
            [tagA] = new List<Holder<string>> { fooRef, barRef },
        });
        var tags = reg.ListTags().ToList();
        if (tags.Count != 1) return false;
        if (tags[0].Key != tagA) return false;
        if (tags[0].Value.Size != 2) return false;
        return true;
    }

    private static bool TestHolderLookupProviderListRegistryKeys()
    {
        var regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("lookup_provider_test"));
        var reg = new MappedRegistry<string>(regKey, Lifecycle.Stable);
        reg.Register(ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:a")), "A", RegistrationInfo.BuiltIn);
        reg.Freeze();
        var access = new ImmutableRegistryAccess(new[] { new RegistryEntry(regKey.Identifier, reg) });
        var keys = access.ListRegistryKeys().ToList();
        if (keys.Count != 1) return false;
        if (keys[0] != regKey.Identifier) return false;
        var lookedUp = access.Lookup<string>(regKey);
        return lookedUp is not null && lookedUp == reg;
    }

    private static bool TestDataComponentMapAsLookup()
    {
        DataComponentLookup lookup = DataComponentMap.Empty;
        if (!lookup.IsEmpty) return false;
        if (lookup.Size != 0) return false;
        return ReferenceEquals(lookup, DataComponentMap.Empty);
    }

//==== Registry.GetRandom ====

    //StubRandom 固定返回指定值用于验证 GetRandom 走 byId 索引
    private sealed class StubRandom : RandomSource
    {
        private readonly int _value;
        public StubRandom(int value) => _value = value;
        public RandomSource Fork() => this;
        public PositionalRandomFactory ForkPositional() => throw new NotSupportedException();
        public void SetSeed(long seed) { }
        public int NextInt() => _value;
        public int NextInt(int bound) => _value % bound;
        public long NextLong() => _value;
        public bool NextBoolean() => (_value & 1) != 0;
        public float NextFloat() => _value;
        public double NextDouble() => _value;
        public double NextGaussian() => _value;
    }

    private static bool TestRegistryGetRandom()
    {
        var reg = NewFrozenTagTestRegistry(out var _, out var _, out var _, out var _);
        //3个元素索引0..2 StubRandom返回5对应2验证返回baz Holder
        var random = new StubRandom(5);
        var holder = reg.GetRandom(random);
        if (holder is null) return false;
        return holder.Value == "Baz";
    }

    private static bool TestRegistryGetRandomEmpty()
    {
        var regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("empty_random_reg"));
        var reg = new MappedRegistry<string>(regKey, Lifecycle.Stable);
        reg.Freeze();
        return reg.GetRandom(new StubRandom(0)) is null;
    }

//==== WritableRegistry.CreateRegistrationLookup ====

    private static bool TestCreateRegistrationLookupOwn()
    {
        var reg = NewFrozenTagTestRegistry(out var regKey, out var _, out var _, out var _);
        var provider = ((WritableRegistry<string>)reg).CreateRegistrationLookup();
        var found = provider.Lookup<string>(regKey);
        return ReferenceEquals(found, reg);
    }

    private static bool TestCreateRegistrationLookupOther()
    {
        var reg = NewFrozenTagTestRegistry(out var _, out var _, out var _, out var _);
        var provider = ((WritableRegistry<string>)reg).CreateRegistrationLookup();
        var otherRegKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("other_reg"));
        var notFound = provider.Lookup<string>(otherRegKey);
        if (notFound is not null) return false;
        var keys = provider.ListRegistryKeys().ToList();
        return keys.Count == 1 && keys[0] == reg.Key.Identifier;
    }

//==== State 子系统 ====

    private static bool TestBooleanProperty()
    {
        var p = new BooleanProperty("powered");
        if (p.PossibleValues.Count != 2) return false;
        if (p.GetName(true) != "true" || p.GetName(false) != "false") return false;
        if (!p.TryGetValue("true", out var t) || t != true) return false;
        if (!p.TryGetValue("false", out var f) || f != false) return false;
        if (p.TryGetValue("xxx", out _)) return false;
        if (p.GetInternalIndex(true) != 1 || p.GetInternalIndex(false) != 0) return false;
        return p.Name == "powered";
    }

    private static bool TestIntegerProperty()
    {
        var p = new IntegerProperty("age", 0, 2);
        if (p.PossibleValues.Count != 3) return false;
        if (p.PossibleValues[0] != 0 || p.PossibleValues[2] != 2) return false;
        if (p.GetName(1) != "1") return false;
        if (!p.TryGetValue("2", out var v2) || v2 != 2) return false;
        if (p.TryGetValue("5", out _)) return false;
        if (p.GetInternalIndex(0) != 0 || p.GetInternalIndex(2) != 2) return false;
        if (p.GetInternalIndex(5) != -1) return false;
        return true;
    }

    //TestEnumProperty EnumProperty 用测试枚举验证枚举值与名字映射
    private static bool TestEnumProperty()
    {
        var p = new EnumProperty<TestAxis>("axis");
        return p.PossibleValues.Count == 3
            && p.PossibleValues.Contains(TestAxis.X)
            && p.GetName(TestAxis.Y) == "Y"
            && p.TryGetValue("Z", out var z) && z == TestAxis.Z
            && !p.TryGetValue("W", out _)
            && p.GetInternalIndex(TestAxis.X) >= 0;
    }

    private enum TestAxis { X, Y, Z }

    //TestPropertyValueCodec Property.ValueCodec 用 BooleanProperty 验证字符串往返
    private static bool TestPropertyValueCodec()
    {
        var prop = new BooleanProperty("powered");
        var codec = prop.ValueCodec();
        var ops = NbtOps.Instance;
        var encoded = codec.EncodeStart(ops, true);
        if (!encoded.Result().IsPresent) return false;
        var parsed = codec.Parse(ops, encoded.GetOrThrow());
        return parsed.Result().IsPresent && parsed.GetOrThrow();
    }

    private static bool TestStateDefinitionCombinations()
    {
        var def = NewTestStateDefinition(out _);
        return def.PossibleStates.Count == 4;
    }

    private static bool TestStateHolderGetValue()
    {
        var def = NewTestStateDefinition(out var powered);
        var state = def.PossibleStates.First(s => !s.GetValue(powered));
        if (state.GetValue(powered) != false) return false;
        var on = state.SetValue(powered, true);
        return on.GetValue(powered) == true;
    }

    private static bool TestStateHolderSetValue()
    {
        var def = NewTestStateDefinition(out var powered);
        var state = def.Any();
        var next = state.SetValue(powered, true);
        //struct BlockState 用值比较验证邻居查表返回正确状态
        var expected = def.PossibleStates.First(s => s.GetValue(powered) == true);
        return next == expected;
    }

    private static bool TestStateHolderCycle()
    {
        var def = NewTestStateDefinition(out var powered);
        var state = def.Any();
        var cycled = state.Cycle(powered);
        //powered 当前 false，cycle 后应为 true
        return cycled.GetValue(powered) == true;
    }

    private static bool TestStateHolderHasProperty()
    {
        var def = NewTestStateDefinition(out var powered);
        var unknown = new BooleanProperty("unknown");
        var state = def.Any();
        return state.HasProperty(powered) && !state.HasProperty(unknown);
    }

    private static bool TestStateHolderTrySetValueMissing()
    {
        var def = NewTestStateDefinition(out _);
        var unknown = new BooleanProperty("unknown");
        var state = def.Any();
        var result = state.TrySetValue(unknown, true);
        //struct BlockState 找不到属性时返回当前 state 值比较相等
        return result == state;
    }

    private static bool TestStateDefinitionSingleton()
    {
        var block = new TestBlock();
        var def = new BlockStateDefinition.Builder(block).Create();
        return def.PossibleStates.Count == 1 && def.IsSingletonState;
    }

    private static bool TestStateDefinitionInvalidName()
    {
        var block = new TestBlock();
        try
        {
            new BlockStateDefinition.Builder(block)
                .Add(new BooleanProperty("Powered"))
                .Create();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    //==== 辅助 ====

    private static MappedRegistry<string> NewTestRegistry(ResourceKey<Registry<string>>? key = null)
    {
        key ??= ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("test_string_reg"));
        return new MappedRegistry<string>(key, Lifecycle.Stable);
    }

    //构建测试用 BlockStateDefinition：powered(bool) + age(int 0..1) = 4 个状态
    //out powered 返回构建时用的属性实例，因为 ValueIndex 用引用比较
    private static BlockStateDefinition NewTestStateDefinition(out BooleanProperty powered)
    {
        BlockStateRegistry.Reset();
        powered = new BooleanProperty("powered");
        var age = new IntegerProperty("age", 0, 1);
        var block = new TestBlock();
        return new BlockStateDefinition.Builder(block)
            .Add(powered, age)
            .Create();
    }

    //测试用 Block 实现
    private sealed class TestBlock : Block
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("test_block");
        public override BlockState DefaultBlockState => default;
    }
}
