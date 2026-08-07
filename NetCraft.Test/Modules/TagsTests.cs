using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Tags;

namespace NetCraft.Test.Modules;

//Tags 子库测试
//覆盖 TagEntry 前缀编码 round-trip 和 TagFile JSON 解析
internal static class TagsTests
{
    public const string Module = "tags";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("TagEntry element round-trip", TestElementRoundTrip);
        yield return ("TagEntry tag round-trip", TestTagRoundTrip);
        yield return ("TagEntry required prefix round-trip", TestRequiredRoundTrip);
        yield return ("TagFile from/to JSON round-trip", TestTagFileRoundTrip);
        yield return ("TagLoader BuildSingle with element", TestLoaderBuildSingle);
    }

    private static bool TestElementRoundTrip()
    {
        var entry = TagEntry.Element(Identifier.FromNamespaceAndPath("minecraft", "stone"), false);
        return entry.AsString() == "minecraft:stone";
    }

    private static bool TestTagRoundTrip()
    {
        var entry = TagEntry.Tag(Identifier.FromNamespaceAndPath("minecraft", "logs"), false);
        return entry.AsString() == "#minecraft:logs";
    }

    private static bool TestRequiredRoundTrip()
    {
        var entry = TagEntry.Element(Identifier.FromNamespaceAndPath("minecraft", "diamond"), true);
        return entry.AsString() == "!minecraft:diamond";
    }

    private static bool TestTagFileRoundTrip()
    {
        var json = """{"replace": false, "entries": ["minecraft:stone", "#minecraft:logs", "!minecraft:diamond"]}""";
        var file = TagFile.FromJson(json);
        if (file.Replace) return false;
        if (file.Entries.Count != 3) return false;
        if (file.Entries[0].AsString() != "minecraft:stone") return false;
        if (file.Entries[1].AsString() != "#minecraft:logs") return false;
        if (file.Entries[2].AsString() != "!minecraft:diamond") return false;
        //ToJson 重新解析验证 round-trip
        var json2 = file.ToJson();
        var file2 = TagFile.FromJson(json2);
        return file2.Entries.Count == 3 && !file2.Replace;
    }

    private static bool TestLoaderBuildSingle()
    {
        var loader = new TagLoader<string>("blocks", id => Optional<string>.Of(id.ToString()));
        var file = new TagFile(false, new List<TagEntry>
        {
            TagEntry.Element(Identifier.FromNamespaceAndPath("minecraft", "stone"), false),
            TagEntry.Element(Identifier.FromNamespaceAndPath("minecraft", "dirt"), false),
        });
        var result = loader.BuildSingle(file);
        return result.Count == 2 && result.Contains("minecraft:stone") && result.Contains("minecraft:dirt");
    }
}
