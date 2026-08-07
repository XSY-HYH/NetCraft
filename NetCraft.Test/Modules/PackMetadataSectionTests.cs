using System.Text.Json;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Test.Modules;

//PackMetadataSection 测试覆盖 pack.mcmeta 解析各种情况
//验证标准格式缺 description 缺 pack 节点 description 为对象等场景
internal static class PackMetadataSectionTests
{
    public const string Module = "packmetadata";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("FromJson standard format", TestStandardFormat);
        yield return ("FromJson missing description returns empty", TestMissingDescription);
        yield return ("FromJson missing pack node throws", TestMissingPackNode);
        yield return ("FromJson description as object returns raw json", TestDescriptionAsObject);
        yield return ("FromJson missing pack_format returns 0", TestMissingPackFormat);
        yield return ("Reader returns null when pack.mcmeta missing", TestReaderReturnsNull);
        yield return ("Reader reads from StubPackResources", TestReaderReadsFromStub);
    }

    //TestStandardFormat 标准 {"pack":{"pack_format":N,"description":"..."}} 解析
    private static bool TestStandardFormat()
    {
        var json = """{"pack":{"pack_format":15,"description":"The default data"}}""";
        var meta = PackMetadataSection.FromJson(json);
        return meta.PackFormat == 15 && meta.Description == "The default data";
    }

    //TestMissingDescription 缺 description 字段返回空字符串不抛
    private static bool TestMissingDescription()
    {
        var json = """{"pack":{"pack_format":15}}""";
        var meta = PackMetadataSection.FromJson(json);
        return meta.PackFormat == 15 && meta.Description == string.Empty;
    }

    //TestMissingPackNode 缺 pack 节点抛 JsonException
    private static bool TestMissingPackNode()
    {
        var json = """{"other":"value"}""";
        try
        {
            PackMetadataSection.FromJson(json);
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    //TestDescriptionAsObject description 为聊天组件对象时返回 GetRawText 对齐原版
    private static bool TestDescriptionAsObject()
    {
        var json = """{"pack":{"pack_format":15,"description":{"text":"Hello","color":"white"}}}""";
        var meta = PackMetadataSection.FromJson(json);
        return meta.PackFormat == 15 && meta.Description.Contains("\"text\":\"Hello\"");
    }

    //TestMissingPackFormat 缺 pack_format 字段返回 0
    private static bool TestMissingPackFormat()
    {
        var json = """{"pack":{"description":"no format"}}""";
        var meta = PackMetadataSection.FromJson(json);
        return meta.PackFormat == 0 && meta.Description == "no format";
    }

    //TestReaderReturnsNull pack 不含 pack.mcmeta 时 Reader 返回 null
    private static bool TestReaderReturnsNull()
    {
        var pack = new StubPackResources("empty", rootResource: null);
        return PackMetadataSectionReader.Read(pack) is null;
    }

    //TestReaderReadsFromStub pack 含 pack.mcmeta 时 Reader 解析返回正确元数据
    private static bool TestReaderReadsFromStub()
    {
        var json = """{"pack":{"pack_format":18,"description":"stub pack"}}""";
        var pack = new StubPackResources("stub", rootResource: json);
        var meta = PackMetadataSectionReader.Read(pack);
        return meta is not null && meta.PackFormat == 18 && meta.Description == "stub pack";
    }

    //StubPackResources 测试用桩固定 rootResource 内容其他方法返回空
    private sealed class StubPackResources : PackResources
    {
        private readonly string? _rootResource;
        public StubPackResources(string packId, string? rootResource) : base(packId)
        {
            _rootResource = rootResource;
        }
        public override Stream? GetRootResource(string path)
            => _rootResource is null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_rootResource));
        public override Stream? GetResource(PackType type, Identifier location) => null;
        public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output) { }
        public override ISet<string> GetNamespaces(PackType type) => new HashSet<string>();
    }
}
