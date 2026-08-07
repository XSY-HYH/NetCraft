using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Test.Modules;

//RegistryOps 注册表感知 DynamicOps 测试
//覆盖 GetRegistry/DecodeHolder/EncodeId 与委托方法
internal static class RegistryOpsTests
{
    public const string Module = "registryops";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("RegistryOps GetRegistry 命中返回实例", TestGetRegistryHit);
        yield return ("RegistryOps GetRegistry 未命中返回 null", TestGetRegistryMiss);
        yield return ("RegistryOps DecodeHolder 解析 Reference Holder", TestDecodeHolder);
        yield return ("RegistryOps DecodeHolder 未知注册表返回 Error", TestDecodeHolderUnknownRegistry);
        yield return ("RegistryOps DecodeHolder 未知键返回 Error", TestDecodeHolderUnknownKey);
        yield return ("RegistryOps EncodeId Reference Holder 编码为字符串", TestEncodeIdReference);
        yield return ("RegistryOps EncodeId Direct Holder 返回 Error", TestEncodeIdDirectHolder);
        yield return ("RegistryOps 委托 EmptyList", TestDelegateEmptyList);
        yield return ("RegistryOps 委托 CreateString", TestDelegateCreateString);
        yield return ("RegistryOps 委托 GetNumberValue", TestDelegateGetNumberValue);
    }

    //TestElement 测试用注册表元素
    private sealed class TestElement
    {
        public string Name { get; }
        public TestElement(string name) { Name = name; }
    }

    private static (MappedRegistry<TestElement>, RegistryAccess, RegistryOps<Tag>) NewSetup()
    {
        var registryKey = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("test_element"));
        var registry = new MappedRegistry<TestElement>(registryKey, Lifecycle.Stable);
        var element = new TestElement("foo");
        var elementId = Identifier.WithDefaultNamespace("foo");
        var key = ResourceKey<TestElement>.Create(registryKey, elementId);
        registry.Register(key, element, RegistrationInfo.BuiltIn);
        registry.Freeze();

        var entries = new List<KeyValuePair<Identifier, object>>
        {
            new(registryKey.Identifier, registry),
        };
        var ra = new ImmutableRegistryAccess(entries);
        var ops = new RegistryOps<Tag>(NbtOps.Instance, ra);
        return (registry, ra, ops);
    }

    private static bool TestGetRegistryHit()
    {
        var (registry, _, ops) = NewSetup();
        var key = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("test_element"));
        var got = ops.GetRegistry(key);
        return ReferenceEquals(got, registry);
    }

    private static bool TestGetRegistryMiss()
    {
        var (_, _, ops) = NewSetup();
        var unknownKey = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("unknown"));
        return ops.GetRegistry(unknownKey) is null;
    }

    private static bool TestDecodeHolder()
    {
        var (registry, _, ops) = NewSetup();
        var key = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("test_element"));
        var holderResult = ops.DecodeHolder(key, NbtOps.Instance.CreateString("minecraft:foo"));
        if (!holderResult.Result().IsPresent) return false;
        var holder = holderResult.GetOrThrow();
        if (holder.HolderKind != Holder<TestElement>.Kind.Reference) return false;
        return holder.Value.Name == "foo";
    }

    private static bool TestDecodeHolderUnknownRegistry()
    {
        var (_, _, ops) = NewSetup();
        var unknownKey = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("unknown"));
        var result = ops.DecodeHolder(unknownKey, NbtOps.Instance.CreateString("minecraft:foo"));
        return !result.Result().IsPresent;
    }

    private static bool TestDecodeHolderUnknownKey()
    {
        var (_, _, ops) = NewSetup();
        var key = ResourceKeys.CreateRegistryKey<TestElement>(Identifier.WithDefaultNamespace("test_element"));
        var result = ops.DecodeHolder(key, NbtOps.Instance.CreateString("minecraft:bar"));
        return !result.Result().IsPresent;
    }

    private static bool TestEncodeIdReference()
    {
        var (registry, _, ops) = NewSetup();
        var elementId = Identifier.WithDefaultNamespace("foo");
        var holder = registry.Get(elementId);
        if (holder is null) return false;
        var result = ops.EncodeId(holder);
        if (!result.Result().IsPresent) return false;
        var encoded = result.GetOrThrow();
        var strResult = NbtOps.Instance.GetStringValue(encoded);
        return strResult.Result().IsPresent && strResult.GetOrThrow() == "minecraft:foo";
    }

    private static bool TestEncodeIdDirectHolder()
    {
        var (_, _, ops) = NewSetup();
        var direct = Holder<TestElement>.Direct(new TestElement("unbound"));
        var result = ops.EncodeId(direct);
        return !result.Result().IsPresent;
    }

    private static bool TestDelegateEmptyList()
    {
        var (_, _, ops) = NewSetup();
        var empty = ops.EmptyList();
        var streamResult = NbtOps.Instance.GetStream(empty);
        //委托到底层 NbtOps 的 EmptyList 应能被 GetStream 识别为空流
        return streamResult.Result().IsPresent;
    }

    private static bool TestDelegateCreateString()
    {
        var (_, _, ops) = NewSetup();
        var s = ops.CreateString("hello");
        var strResult = NbtOps.Instance.GetStringValue(s);
        return strResult.Result().IsPresent && strResult.GetOrThrow() == "hello";
    }

    private static bool TestDelegateGetNumberValue()
    {
        var (_, _, ops) = NewSetup();
        var n = ops.CreateInt(42);
        var numResult = NbtOps.Instance.GetNumberValue(n);
        return numResult.Result().IsPresent && numResult.GetOrThrow() == 42;
    }
}
