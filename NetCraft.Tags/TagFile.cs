using System.Text.Json;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagFile 标签文件格式对应原版 net.minecraft.tags.TagFile
//含 replace 标志和 entries 列表
//JSON 格式对齐原版 {"replace": bool, "entries": ["id", "#tag", "!id"]}
public sealed record TagFile(bool Replace, List<TagEntry> Entries)
{
    //FromJson 从 JSON 字符串解析 TagFile
    public static TagFile FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var replace = root.TryGetProperty("replace", out var r) && r.GetBoolean();
        var entries = new List<TagEntry>();
        if (root.TryGetProperty("entries", out var e))
        {
            foreach (var entry in e.EnumerateArray())
            {
                entries.Add(TagEntry.FromString(entry.GetString()!));
            }
        }
        return new TagFile(replace, entries);
    }

    //ToJson 序列化为 JSON 字符串
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("replace", Replace);
            writer.WriteStartArray("entries");
            foreach (var entry in Entries)
            {
                writer.WriteStringValue(entry.AsString());
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
