using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Test.Modules;

//TagValueInput/Output NBT 序列化抽象测试
//覆盖标量/子节点/列表路径 round-trip 与 Codec 集成
internal static class TagValueIOTests
{
    public const string Module = "tagvalueio";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("TagValueOutput PutInt/TagValueInput GetInt", TestPutGetInt);
        yield return ("TagValueOutput PutString/TagValueInput GetString", TestPutGetString);
        yield return ("TagValueOutput PutBoolean/TagValueInput GetBooleanOr", TestPutBoolean);
        yield return ("TagValueOutput PutLong/TagValueInput GetLong", TestPutGetLong);
        yield return ("TagValueOutput Child 嵌套结构 round-trip", TestChildNested);
        yield return ("TagValueOutput ChildrenList 子节点列表 round-trip", TestChildrenList);
        yield return ("TagValueOutput List Codec 类型化列表 round-trip", TestTypedList);
        yield return ("TagValueOutput Store Codec 序列化 round-trip", TestStoreCodec);
        yield return ("TagValueOutput StoreNullable null 跳过", TestStoreNullableSkipsNull);
        yield return ("TagValueOutput Discard 删除字段", TestDiscard);
        yield return ("TagValueInput Child 缺失返回 null", TestChildMissingReturnsNull);
        yield return ("TagValueInput ChildOrEmpty 返回空实例", TestChildOrEmpty);
        yield return ("TagValueInput ChildrenListOrEmpty 缺失返回空列表", TestChildrenListOrEmpty);
        yield return ("TagValueInput ListOrEmpty 缺失返回空列表", TestListOrEmpty);
        yield return ("TagValueInput IsEmpty 空容器判定", TestIsEmpty);
        yield return ("TagValueInput Lookup 返回注册表入口", TestLookup);
    }

    private static RegistryAccess EmptyRegistryAccess()
        => new ImmutableRegistryAccess(Array.Empty<KeyValuePair<Identifier, object>>());

    //PutInt 写入 GetInt 读取
    private static bool TestPutGetInt()
    {
        var output = new TagValueOutput();
        output.PutInt("value", 42);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.GetInt("value") == 42;
    }

    //PutString 写入 GetString 读取
    private static bool TestPutGetString()
    {
        var output = new TagValueOutput();
        output.PutString("name", "hello");
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.GetString("name") == "hello";
    }

    //PutBoolean 写入 GetBooleanOr 读取
    private static bool TestPutBoolean()
    {
        var output = new TagValueOutput();
        output.PutBoolean("flag", true);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.GetBooleanOr("flag", false) && !input.GetBooleanOr("missing", false);
    }

    //PutLong 写入 GetLong 读取
    private static bool TestPutGetLong()
    {
        var output = new TagValueOutput();
        output.PutLong("big", 1234567890L);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.GetLong("big") == 1234567890L;
    }

    //Child 嵌套子节点 round-trip
    private static bool TestChildNested()
    {
        var output = new TagValueOutput();
        var child = output.Child("inner");
        child.PutInt("x", 10);
        child.PutString("label", "nested");
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var innerInput = input.Child("inner");
        if (innerInput is null) return false;
        return innerInput.GetInt("x") == 10 && innerInput.GetString("label") == "nested";
    }

    //ChildrenList 子节点列表 round-trip
    private static bool TestChildrenList()
    {
        var output = new TagValueOutput();
        var list = output.ChildrenList("items");
        var a = list.AddChild();
        a.PutInt("v", 1);
        var b = list.AddChild();
        b.PutInt("v", 2);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var children = input.ChildrenList("items");
        if (children is null || children.Count != 2) return false;
        return children[0].GetInt("v") == 1 && children[1].GetInt("v") == 2;
    }

    //List Codec 类型化列表 round-trip
    private static bool TestTypedList()
    {
        var output = new TagValueOutput();
        var intList = output.List("nums", Codecs.Int);
        intList.Add(1);
        intList.Add(2);
        intList.Add(3);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var list = input.List("nums", Codecs.Int);
        if (list is null || list.Count != 3) return false;
        return list[0] == 1 && list[1] == 2 && list[2] == 3;
    }

    //Store 按 Codec 序列化 round-trip
    private static bool TestStoreCodec()
    {
        var output = new TagValueOutput();
        output.Store("count", Codecs.Int, 99);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.Read("count", Codecs.Int) == 99;
    }

    //StoreNullable 传入 null 跳过不写入字段
    private static bool TestStoreNullableSkipsNull()
    {
        var output = new TagValueOutput();
        output.StoreNullable("opt", Codecs.String, null);
        var tag = output.BuildResult();
        return tag.GetString("opt") is null;
    }

    //Discard 删除指定字段
    private static bool TestDiscard()
    {
        var output = new TagValueOutput();
        output.PutInt("a", 1);
        output.PutInt("b", 2);
        output.Discard("a");
        var tag = output.BuildResult();
        return tag.GetInt("a") is null && tag.GetInt("b")?.Value == 2;
    }

    //Child 字段缺失返回 null
    private static bool TestChildMissingReturnsNull()
    {
        var output = new TagValueOutput();
        output.PutInt("only", 1);
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        return input.Child("missing") is null;
    }

    //ChildOrEmpty 字段缺失返回空 ValueInput 不抛
    private static bool TestChildOrEmpty()
    {
        var output = new TagValueOutput();
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var empty = input.ChildOrEmpty("missing");
        return empty.IsEmpty();
    }

    //ChildrenListOrEmpty 缺失返回空列表
    private static bool TestChildrenListOrEmpty()
    {
        var output = new TagValueOutput();
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var list = input.ChildrenListOrEmpty("missing");
        return list.Count == 0;
    }

    //ListOrEmpty 缺失返回空列表
    private static bool TestListOrEmpty()
    {
        var output = new TagValueOutput();
        var tag = output.BuildResult();
        var input = TagValueInput.Create(EmptyRegistryAccess(), tag);
        var list = input.ListOrEmpty("missing", Codecs.Int);
        return list.Count == 0;
    }

    //IsEmpty 空容器与有内容容器判定
    private static bool TestIsEmpty()
    {
        var emptyOutput = new TagValueOutput();
        var emptyTag = emptyOutput.BuildResult();
        var emptyInput = TagValueInput.Create(EmptyRegistryAccess(), emptyTag);
        if (!emptyInput.IsEmpty()) return false;

        var nonEmptyOutput = new TagValueOutput();
        nonEmptyOutput.PutInt("v", 1);
        var nonEmptyTag = nonEmptyOutput.BuildResult();
        var nonEmptyInput = TagValueInput.Create(EmptyRegistryAccess(), nonEmptyTag);
        return !nonEmptyInput.IsEmpty();
    }

    //Lookup 返回注册表入口用于 Codec 解析时查表
    private static bool TestLookup()
    {
        var ra = EmptyRegistryAccess();
        var output = new TagValueOutput();
        var tag = output.BuildResult();
        var input = TagValueInput.Create(ra, tag);
        return ReferenceEquals(input.Lookup(), ra);
    }
}
