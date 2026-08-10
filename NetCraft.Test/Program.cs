using NetCraft.Test.Modules;

//NetCraft 统一测试入口
//  dotnet run --project NetCraft.Test            运行全部测试（跳过 GUI/TCP 模块）
//  dotnet run --project NetCraft.Test -- nbt     仅运行 NBT 模块
//  dotnet run --project NetCraft.Test -- registry 仅运行 Registry 模块
//  dotnet run --project NetCraft.Test -- ByteTag  运行名称含 ByteTag 的测试
//  dotnet run --project NetCraft.Test -- nbt registry  运行多个模块
//  dotnet run --project NetCraft.Test -- list    列出所有测试条目
//  dotnet run --project NetCraft.Test -- gpugui  运行 GPU GUI 测试（需 Vulkan 驱动+窗口环境）
//  dotnet run --project NetCraft.Test -- networktcp  运行真实 TCP 握手测试（占用本地端口）
//  dotnet run --project NetCraft.Test -- all     运行全部测试（含 GUI/TCP）
//参数无需 -- 前缀，直接给模块名或测试名片段（大小写不敏感）
//GUI/TCP 测试默认跳过，需显式指定模块名或 all 参数触发

var modules = new (string Name, Func<IEnumerable<(string, Func<bool>)>> Loader)[]
{
    (NbtTests.Module, NbtTests.All),
    (RegistryTests.Module, RegistryTests.All),
    (StorageTests.Module, StorageTests.All),
    (CodecTests.Module, CodecTests.All),
    (PalettedTests.Module, PalettedTests.All),
    (IOWorkerTests.Module, IOWorkerTests.All),
    (SimpleRegionStorageTests.Module, SimpleRegionStorageTests.All),
    (DataFixerTests.Module, DataFixerTests.All),
    (V1_21FixEndToEndTests.Module, V1_21FixEndToEndTests.All),
    (EntityStorageTests.Module, EntityStorageTests.All),
    (SavedDataStorageTests.Module, SavedDataStorageTests.All),
    (HeightmapTests.Module, HeightmapTests.All),
    (TagValueIOTests.Module, TagValueIOTests.All),
    (RegistryOpsTests.Module, RegistryOpsTests.All),
    (ChunkAccessTests.Module, ChunkAccessTests.All),
    (InteropTests.Module, InteropTests.All),
    (TagsTests.Module, TagsTests.All),
    (ResourcesTests.Module, ResourcesTests.All),
    (NetworkTests.Module, NetworkTests.All),
    (NetworkHandshakeTests.Module, NetworkHandshakeTests.All),
    (NetworkTcpTests.Module, NetworkTcpTests.All),
    (CommandsTests.Module, CommandsTests.All),
    (ComponentTests.Module, ComponentTests.All),
    (DataComponentsTests.Module, DataComponentsTests.All),
    (ItemStackPacketTests.Module, ItemStackPacketTests.All),
    (MenuTypePacketTests.Module, MenuTypePacketTests.All),
    (ConfigTests.Module, ConfigTests.All),
    (BootstrapTagsTests.Module, BootstrapTagsTests.All),
    (LevelStorageTests.Module, LevelStorageTests.All),
    (BlockTests.Module, BlockTests.All),
    (EntityTests.Module, EntityTests.All),
    (NoiseTests.Module, NoiseTests.All),
    (DensityFunctionTests.Module, DensityFunctionTests.All),
    (WorldGenTests.Module, WorldGenTests.All),
    (SurfaceRulesTests.Module, SurfaceRulesTests.All),
    (LevelChunkSerializerTests.Module, LevelChunkSerializerTests.All),
    (ServerTests.Module, ServerTests.All),
    (BootstrapGpuTests.Module, BootstrapGpuTests.All),
    (PrimitivesTests.Module, PrimitivesTests.All),
    (OptimizationsTests.Module, OptimizationsTests.All),
    (RandomTests.Module, RandomTests.All),
    (CollectionTests.Module, CollectionTests.All),
    (ProfilingTests.Module, ProfilingTests.All),
    (SnbtTests.Module, SnbtTests.All),
    (GpuGuiTests.Module, GpuGuiTests.All),
    (GuiLogicTests.Module, GuiLogicTests.All),
    (GuiLayoutTests.Module, GuiLayoutTests.All),
    (GuiScaleTests.Module, GuiScaleTests.All),
    (GuiRenderStateTests.Module, GuiRenderStateTests.All),
    (RenderPipelinesTests.Module, RenderPipelinesTests.All),
    (ShaderManagerTests.Module, ShaderManagerTests.All),
    (GuiRenderContextTests.Module, GuiRenderContextTests.All),
    (FontProviderTests.Module, FontProviderTests.All),
    (FormattedTextTests.Module, FormattedTextTests.All),
    (SpriteScalingTests.Module, SpriteScalingTests.All),
    (StagedVertexBufferTests.Module, StagedVertexBufferTests.All),
    (GpuBufferPoolTests.Module, GpuBufferPoolTests.All),
    (GuiRendererTests.Module, GuiRendererTests.All),
    (PictureInPictureTests.Module, PictureInPictureTests.All),
    (DynamicAtlasAllocatorTests.Module, DynamicAtlasAllocatorTests.All),
    (BlockTextureAtlasTests.Module, BlockTextureAtlasTests.All),
    (BlockModelTests.Module, BlockModelTests.All),
    (ItemPipeline3DTests.Module, ItemPipeline3DTests.All),
    (ChunkMeshTests.Module, ChunkMeshTests.All),
    (CameraFrustumTests.Module, CameraFrustumTests.All),
    (LevelRendererTests.Module, LevelRendererTests.All),
    //ReloadableServerResourcesTests 调 BootstrapClass.BootStrap 冻结所有注册表
    //必须放在所有需要写注册表的测试之后否则后续测试无法注册抛 Registry is already frozen
    (ReloadableServerResourcesTests.Module, ReloadableServerResourcesTests.All),
    (PackMetadataSectionTests.Module, PackMetadataSectionTests.All),
    (AssetsExtractorTests.Module, AssetsExtractorTests.All),
};

var allTests = modules
    .SelectMany(m => m.Loader().Select(t => (Module: m.Name, Name: t.Item1, Test: t.Item2)))
    .ToList();

//list 模式：打印所有测试条目后退出
if (args.Length == 1 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"共 {allTests.Count} 个测试条目（{modules.Length} 个模块）：");
    foreach (var m in modules)
    {
        var count = allTests.Count(t => t.Module == m.Name);
        var exclusiveTag = IsExclusiveModule(m.Name) ? " (跳过)" : "";
        Console.WriteLine($"  [{m.Name}] {count} 个{exclusiveTag}");
        foreach (var t in allTests.Where(t => t.Module == m.Name))
            Console.WriteLine($"    {t.Name}");
    }
    return 0;
}

//筛选即将运行的测试
var selected = SelectTests(allTests, args);

if (selected.Count == 0)
{
    Console.WriteLine("没有匹配的测试条目");
    Console.WriteLine($"可用模块：{string.Join(", ", modules.Select(m => m.Name))}");
    Console.WriteLine("用 list 参数查看全部测试条目");
    Console.WriteLine("GUI/TCP 测试需显式指定模块名触发：dotnet run -- gpugui / networktcp");
    return 1;
}

Console.WriteLine($"运行 {selected.Count}/{allTests.Count} 个测试");
Console.WriteLine();

var pass = 0;
var fail = 0;
var error = 0;
foreach (var (module, name, test) in selected)
{
    try
    {
        var ok = test();
        if (ok)
        {
            pass++;
            Console.WriteLine($"[{module}] PASS  {name}");
        }
        else
        {
            fail++;
            Console.WriteLine($"[{module}] FAIL  {name}");
        }
    }
    catch (Exception ex)
    {
        error++;
        Console.WriteLine($"[{module}] ERROR {name}: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

Console.WriteLine();
Console.WriteLine($"结果：{pass} 通过，{fail} 失败，{error} 错误（共 {selected.Count}）");
return fail == 0 && error == 0 ? 0 : 1;

//按命令行参数筛选测试
//无参数=全部但跳过 GUI/TCP 模块
//参数匹配模块名（大小写不敏感）→ 该模块全部
//否则按测试名包含匹配（大小写不敏感）
//多个参数为OR关系
//all 参数包含 GUI/TCP 测试
//GUI/TCP 测试按测试名匹配时跳过必须显式按模块名触发
static List<(string Module, string Name, Func<bool> Test)> SelectTests(
    List<(string Module, string Name, Func<bool> Test)> all, string[] args)
{
    if (args.Length == 0)
        return all.Where(t => !IsExclusiveModule(t.Module)).ToList();

    var result = new List<(string, string, Func<bool>)>();
    var seen = new HashSet<int>();
    foreach (var arg in args)
    {
        if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
            return all;

        var isModule = all.Any(t => t.Module.Equals(arg, StringComparison.OrdinalIgnoreCase));
        foreach (var t in all)
        {
            var match = isModule
                ? t.Module.Equals(arg, StringComparison.OrdinalIgnoreCase)
                : t.Name.Contains(arg, StringComparison.OrdinalIgnoreCase);
            if (!isModule && IsExclusiveModule(t.Module))
                continue;
            if (match && seen.Add(all.IndexOf(t)))
                result.Add(t);
        }
    }
    return result;
}

//IsExclusiveModule 判定是否为需显式触发的测试模块
//GUI 测试需窗口+Vulkan 驱动默认不跑
//TCP 测试占用本地端口需独立环境默认不跑
static bool IsExclusiveModule(string module)
    => module.Equals(GpuGuiTests.Module, StringComparison.OrdinalIgnoreCase)
    || module.Equals(NetworkTcpTests.Module, StringComparison.OrdinalIgnoreCase);
