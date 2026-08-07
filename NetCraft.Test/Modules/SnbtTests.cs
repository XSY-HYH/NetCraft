using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Test.Modules;

//SNBT 解析器测试覆盖TagParser基础解析功能
//整数浮点字符串列表映射数组及CompoundTag codec
internal static class SnbtTests
{
    public const string Module = "snbt";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("TagParser parse int", TestParseInt);
        yield return ("TagParser parse long", TestParseLong);
        yield return ("TagParser parse byte", TestParseByte);
        yield return ("TagParser parse short", TestParseShort);
        yield return ("TagParser parse float", TestParseFloat);
        yield return ("TagParser parse double", TestParseDouble);
        yield return ("TagParser parse bool true", TestParseBoolTrue);
        yield return ("TagParser parse bool false", TestParseBoolFalse);
        yield return ("TagParser parse string quoted", TestParseStringQuoted);
        yield return ("TagParser parse string unquoted", TestParseStringUnquoted);
        yield return ("TagParser parse string escapes", TestParseStringEscapes);
        yield return ("TagParser parse empty list", TestParseEmptyList);
        yield return ("TagParser parse list of int", TestParseListOfInt);
        yield return ("TagParser parse empty map", TestParseEmptyMap);
        yield return ("TagParser parse map", TestParseMap);
        yield return ("TagParser parse nested map", TestParseNestedMap);
        yield return ("TagParser parse byte array", TestParseByteArray);
        yield return ("TagParser parse int array", TestParseIntArray);
        yield return ("TagParser parse long array", TestParseLongArray);
        yield return ("TagParser parse hex int", TestParseHexInt);
        yield return ("TagParser parse binary int", TestParseBinaryInt);
        yield return ("TagParser parse trailing data throws", TestParseTrailingDataThrows);
        yield return ("TagParser parseCompoundFully", TestParseCompoundFully);
        yield return ("TagParser parseCompoundFully throws on list", TestParseCompoundFullyThrowsOnList);
        yield return ("TagParser parseAsArgument no trailing check", TestParseAsArgumentNoTrailingCheck);
        yield return ("FlattenedCodec parse valid", TestFlattenedCodecParseValid);
        yield return ("FlattenedCodec parse invalid", TestFlattenedCodecParseInvalid);
        yield return ("FlattenedCodec encode round-trip", TestFlattenedCodecEncodeRoundTrip);
        yield return ("LenientCodec parse string", TestLenientCodecParseString);
        yield return ("LenientCodec parse tag", TestLenientCodecParseTag);
        yield return ("CompoundTag.Codec parse CompoundTag", TestCompoundTagCodecParseCompoundTag);
        yield return ("CompoundTag.Codec parse non CompoundTag errors", TestCompoundTagCodecParseNonCompoundTagErrors);
    }

    private static bool TestParseInt()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("42");
        return result is IntTag intTag && intTag.Value == 42;
    }

    private static bool TestParseLong()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("100L");
        return result is LongTag longTag && longTag.Value == 100L;
    }

    private static bool TestParseByte()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("7b");
        return result is ByteTag byteTag && byteTag.Value == 7;
    }

    private static bool TestParseShort()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("300s");
        return result is ShortTag shortTag && shortTag.Value == 300;
    }

    private static bool TestParseFloat()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("3.14f");
        return result is FloatTag floatTag && Math.Abs(floatTag.Value - 3.14f) < 0.001f;
    }

    private static bool TestParseDouble()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("3.14d");
        return result is DoubleTag doubleTag && Math.Abs(doubleTag.Value - 3.14) < 0.001;
    }

    private static bool TestParseBoolTrue()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("true");
        return result is ByteTag byteTag && byteTag.Value == 1;
    }

    private static bool TestParseBoolFalse()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("false");
        return result is ByteTag byteTag && byteTag.Value == 0;
    }

    private static bool TestParseStringQuoted()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("\"hello world\"");
        return result is StringTag stringTag && stringTag.Value == "hello world";
    }

    private static bool TestParseStringUnquoted()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("hello");
        return result is StringTag stringTag && stringTag.Value == "hello";
    }

    private static bool TestParseStringEscapes()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("\"a\\tb\\nc\"");
        return result is StringTag stringTag && stringTag.Value == "a\tb\nc";
    }

    private static bool TestParseEmptyList()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("[]");
        return result is ListTag listTag && listTag.Count == 0;
    }

    private static bool TestParseListOfInt()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("[1,2,3]");
        if (result is not ListTag listTag || listTag.Count != 3) return false;
        return listTag[0] is IntTag a && a.Value == 1
            && listTag[1] is IntTag b && b.Value == 2
            && listTag[2] is IntTag c && c.Value == 3;
    }

    private static bool TestParseEmptyMap()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("{}");
        return result is CompoundTag compoundTag && compoundTag.IsEmpty;
    }

    private static bool TestParseMap()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("{name:\"alice\",age:30}");
        if (result is not CompoundTag compoundTag) return false;
        if (compoundTag.Get<StringTag>("name") is not { } nameTag || nameTag.Value != "alice") return false;
        if (compoundTag.Get<IntTag>("age") is not { } ageTag || ageTag.Value != 30) return false;
        return true;
    }

    private static bool TestParseNestedMap()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("{outer:{inner:42}}");
        if (result is not CompoundTag outer) return false;
        if (outer.Get<CompoundTag>("outer") is not { } innerTag) return false;
        return innerTag.Get<IntTag>("inner") is { } intTag && intTag.Value == 42;
    }

    private static bool TestParseByteArray()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("[B;1,2,3]");
        if (result is not ByteArrayTag byteArray) return false;
        var bytes = byteArray.Value;
        return bytes.Length == 3 && bytes[0] == 1 && bytes[1] == 2 && bytes[2] == 3;
    }

    private static bool TestParseIntArray()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("[I;1,2,3]");
        if (result is not IntArrayTag intArray) return false;
        var ints = intArray.Value;
        return ints.Length == 3 && ints[0] == 1 && ints[1] == 2 && ints[2] == 3;
    }

    private static bool TestParseLongArray()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("[L;1L,2L,3L]");
        if (result is not LongArrayTag longArray) return false;
        var longs = longArray.Value;
        return longs.Length == 3 && longs[0] == 1L && longs[1] == 2L && longs[2] == 3L;
    }

    private static bool TestParseHexInt()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("0xff");
        return result is IntTag intTag && intTag.Value == 255;
    }

    private static bool TestParseBinaryInt()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var result = parser.ParseFully("0b1010");
        return result is IntTag intTag && intTag.Value == 10;
    }

    private static bool TestParseTrailingDataThrows()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        try
        {
            parser.ParseFully("42 extra");
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestParseCompoundFully()
    {
        var result = TagParser<Tag>.ParseCompoundFully("{name:\"alice\"}");
        return result.Get<StringTag>("name") is { } nameTag && nameTag.Value == "alice";
    }

    private static bool TestParseCompoundFullyThrowsOnList()
    {
        try
        {
            TagParser<Tag>.ParseCompoundFully("[1,2,3]");
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestParseAsArgumentNoTrailingCheck()
    {
        var parser = TagParser<Tag>.Create(NbtOps.Instance);
        var reader = new CommandStringReader("42 trailing");
        var result = parser.ParseAsArgument(reader);
        return result is IntTag intTag && intTag.Value == 42;
    }

    private static bool TestFlattenedCodecParseValid()
    {
        var result = TagParser<Tag>.FlattenedCodec.Parse(NbtOps.Instance, new StringTag("{key:1}"));
        return result.Result().IsPresent
            && result.Result().Get() is CompoundTag compoundTag
            && compoundTag.Get<IntTag>("key") is { } intTag
            && intTag.Value == 1;
    }

    private static bool TestFlattenedCodecParseInvalid()
    {
        var result = TagParser<Tag>.FlattenedCodec.Parse(NbtOps.Instance, new StringTag("not valid snbt {"));
        return !result.Result().IsPresent;
    }

    private static bool TestFlattenedCodecEncodeRoundTrip()
    {
        var compound = new CompoundTag();
        compound.Put("key", IntTag.ValueOf(1));
        var encoded = TagParser<Tag>.FlattenedCodec.EncodeStart(NbtOps.Instance, compound);
        if (!encoded.Result().IsPresent) return false;
        var encodedTag = encoded.Result().Get();
        var parsed = TagParser<Tag>.FlattenedCodec.Parse(NbtOps.Instance, encodedTag);
        return parsed.Result().IsPresent
            && parsed.Result().Get() is CompoundTag roundTripped
            && roundTripped.Get<IntTag>("key") is { } intTag
            && intTag.Value == 1;
    }

    private static bool TestLenientCodecParseString()
    {
        var result = TagParser<Tag>.LenientCodec.Parse(NbtOps.Instance, new StringTag("{key:1}"));
        return result.Result().IsPresent
            && result.Result().Get() is CompoundTag compoundTag
            && compoundTag.Get<IntTag>("key") is { } intTag
            && intTag.Value == 1;
    }

    private static bool TestLenientCodecParseTag()
    {
        //LENIENT_CODEC也接受CompoundTag直接传入对应原版Codec.withAlternative第二分支
        var compound = new CompoundTag();
        compound.Put("key", IntTag.ValueOf(1));
        var result = TagParser<Tag>.LenientCodec.Parse(NbtOps.Instance, compound);
        return result.Result().IsPresent
            && result.Result().Get() is CompoundTag parsed
            && parsed.Get<IntTag>("key") is { } intTag
            && intTag.Value == 1;
    }

    private static bool TestCompoundTagCodecParseCompoundTag()
    {
        var compound = new CompoundTag();
        compound.Put("key", IntTag.ValueOf(42));
        var result = CompoundTag.Codec.Parse(NbtOps.Instance, compound);
        return result.Result().IsPresent
            && result.Result().Get() is CompoundTag parsed
            && parsed.Get<IntTag>("key") is { } intTag
            && intTag.Value == 42;
    }

    private static bool TestCompoundTagCodecParseNonCompoundTagErrors()
    {
        var intTag = IntTag.ValueOf(42);
        var result = CompoundTag.Codec.Parse(NbtOps.Instance, intTag);
        return !result.Result().IsPresent;
    }
}
