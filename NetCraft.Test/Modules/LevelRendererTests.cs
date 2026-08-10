using System.Numerics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render;
using NetCraft.Game.Client.Render.World;
using NetCraft.Gpu;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using Direction = NetCraft.Gpu.Direction;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Test.Modules;

//LevelRendererTests 世界渲染主入口单元测试
//覆盖 Prepare 遍历 section + frustum culling + 按 layer 分组到 StagedVertexBuffer
//用 TestChunkAccess 装入 ClientLevel + mock BakedModel 避免依赖 GPU
internal static class LevelRendererTests
{
    public const string Module = "levelrenderer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LevelRenderer empty level produces zero vertices", TestEmptyLevel);
        yield return ("LevelRenderer single section produces solid vertices", TestSingleSection);
        yield return ("LevelRenderer frustum culls sections behind camera", TestFrustumCullingBehind);
        yield return ("LevelRenderer air section skipped", TestAirSectionSkipped);
        yield return ("LevelRenderer multiple layers produce multiple draws", TestMultipleLayers);
        yield return ("LevelRenderer sectionCount and visibleSectionCount tracked", TestSectionCountTracking);
    }

    //空 ClientLevel 无 chunk Prepare 后 0 顶点 0 可见 section
    private static bool TestEmptyLevel()
    {
        var (renderer, _, _) = NewLevelRendererWithCamera();
        renderer.Prepare();
        return renderer.TotalVertexCount == 0
            && renderer.VisibleSectionCount == 0
            && renderer.DrawCallCount == 0;
    }

    //单 section 有 stone 方块 Prepare 后 Solid layer 有 24 顶点（6 面 * 4）
    private static bool TestSingleSection()
    {
        var (renderer, level, _) = NewLevelRendererWithCamera();
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 0, section);
        renderer.Prepare();
        //单方块 6 面 * 4 = 24 顶点
        return renderer.TotalVertexCount == 24
            && renderer.VisibleSectionCount == 1;
    }

    //section 在 Camera 后方被 frustum 剔除 VisibleSectionCount=0
    private static bool TestFrustumCullingBehind()
    {
        var (renderer, level, _) = NewLevelRendererWithCamera();
        //Camera 在 (8,8,24) 朝 -Z 前方 z 减小 后方 z 增大 section 放在 chunk(0,2) 即 z=32~48 在 Camera 后方
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 2, section);
        renderer.Prepare();
        return renderer.VisibleSectionCount == 0
            && renderer.TotalVertexCount == 0;
    }

    //全 air section 被 HasOnlyAir 跳过不计入 SectionCount
    private static bool TestAirSectionSkipped()
    {
        var (renderer, level, _) = NewLevelRendererWithCamera();
        var (section, _) = NewSectionWithAir();
        LoadSection(level, 0, 0, section);
        renderer.Prepare();
        return renderer.SectionCount == 0
            && renderer.VisibleSectionCount == 0;
    }

    //Solid + Cutout 双 layer 产出 2 个 Draw（DrawCallCount 在 Draw 后才更新 Prepare 阶段验证 TotalVertexCount）
    private static bool TestMultipleLayers()
    {
        var (renderer, level, _) = NewLevelRendererWithMultiLayerBuilder();
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 0, section);
        renderer.Prepare();
        //Solid 6 面 * 4 = 24 + Cutout 1 quad * 4 = 4 = 28
        return renderer.TotalVertexCount == 28;
    }

    //SectionCount 统计非空 section 数 VisibleSectionCount 统计视体内 section 数
    private static bool TestSectionCountTracking()
    {
        var (renderer, level, _) = NewLevelRendererWithCamera();
        //视体内 section
        var (section1, stone) = NewSectionWithAir();
        section1.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 0, section1);
        //视体外 section（在 Camera 后方 chunk(0,5) 即 z=80~96）
        var (section2, _) = NewSectionWithAir();
        section2.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 5, section2);
        renderer.Prepare();
        return renderer.SectionCount == 2
            && renderer.VisibleSectionCount == 1;
    }

    //NewLevelRendererWithCamera 创建 Camera 在 (8,8,24) 朝 -Z 的 LevelRenderer
    //Camera 看向原点 section(0,0,0) 在视体内 section(0,0,-2) 在视体外
    private static (LevelRenderer renderer, ClientLevel level, Camera camera) NewLevelRendererWithCamera()
    {
        var level = new ClientLevel();
        var camera = new Camera();
        camera.SetPosition(new Vector3(8, 8, 24));
        camera.UpdatePerspective(MathF.PI / 4f, 800, 600, 0.05f, 1000f);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var renderer = new LevelRenderer(level, camera, builder);
        return (renderer, level, camera);
    }

    //NewLevelRendererWithMultiLayerBuilder 用多 layer BakedModel 的 builder
    private static (LevelRenderer renderer, ClientLevel level, Camera camera) NewLevelRendererWithMultiLayerBuilder()
    {
        var level = new ClientLevel();
        var camera = new Camera();
        camera.SetPosition(new Vector3(8, 8, 24));
        camera.UpdatePerspective(MathF.PI / 4f, 800, 600, 0.05f, 1000f);
        var builder = new ChunkMeshBuilder(_ => NewMultiLayerBakedModel());
        var renderer = new LevelRenderer(level, camera, builder);
        return (renderer, level, camera);
    }

    //LoadSection 把 section 装入 ClientLevel 的指定 chunkZ 位置 sectionY=0
    private static void LoadSection(ClientLevel level, int chunkX, int chunkZ, LevelChunkSection section)
    {
        var chunk = new TestChunkAccess(new ChunkPos(chunkX, chunkZ), 0, 1, section);
        level.LoadChunk(chunk);
    }

    //NewSectionWithAir 创建全 air section 返回 section 与 stone BlockState
    private static (LevelChunkSection section, BlockState stone) NewSectionWithAir()
    {
        var factory = new DefaultPalettedContainerFactory();
        var airBlock = new MockBlock(Identifier.WithDefaultNamespace("air"));
        var stoneBlock = new MockBlock(Identifier.WithDefaultNamespace("stone"));
        factory.RegisterBlock(airBlock);
        factory.RegisterBlock(stoneBlock);
        var section = new LevelChunkSection(factory.CreateForBlockStates(), factory.CreateForBiomes());
        return (section, stoneBlock.DefaultBlockState);
    }

    //NewCubeBakedModel 6 面 cube BakedModel 全 Solid layer
    private static BakedModel NewCubeBakedModel()
    {
        var model = new BakedModel();
        var uv0 = new Vector2(0, 0);
        var uv1 = new Vector2(1, 0);
        var uv2 = new Vector2(1, 1);
        var uv3 = new Vector2(0, 1);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 0, 16), new(0, 0, 0), new(16, 0, 0), new(16, 0, 16),
            uv0, uv1, uv2, uv3, Direction.Down), Direction.Down);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 0), new(0, 16, 16), new(16, 16, 16), new(16, 16, 0),
            uv0, uv1, uv2, uv3, Direction.Up), Direction.Up);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 0), new(0, 0, 0), new(16, 0, 0), new(16, 16, 0),
            uv0, uv1, uv2, uv3, Direction.North), Direction.North);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(16, 16, 16), new(16, 0, 16), new(0, 0, 16), new(0, 16, 16),
            uv0, uv1, uv2, uv3, Direction.South), Direction.South);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(0, 16, 16), new(0, 0, 16), new(0, 0, 0), new(0, 16, 0),
            uv0, uv1, uv2, uv3, Direction.West), Direction.West);
        model.AddQuad(RenderLayer.Solid, new BakedQuad(
            new(16, 16, 0), new(16, 0, 0), new(16, 0, 16), new(16, 16, 16),
            uv0, uv1, uv2, uv3, Direction.East), Direction.East);
        return model;
    }

    //NewMultiLayerBakedModel cube 6 面 Solid + 1 no-cull quad Cutout
    private static BakedModel NewMultiLayerBakedModel()
    {
        var model = NewCubeBakedModel();
        model.AddQuad(RenderLayer.Cutout, new BakedQuad(
            new(0, 0, 0), new(16, 0, 0), new(16, 16, 0), new(0, 16, 0),
            new(0, 0), new(1, 0), new(1, 1), new(0, 1), Direction.North), null);
        return model;
    }

    //TestChunkAccess 测试用 ChunkAccess 具体子类单 section
    private sealed class TestChunkAccess : ChunkAccess
    {
        private readonly ChunkPos _pos;
        private readonly Dictionary<HeightmapRegistry.Types, long[]> _heightmaps;
        private readonly LevelChunkSection? _section;

        public override ChunkPos Pos => _pos;
        public override int MinSectionY { get; }
        public override int SectionsCount { get; }
        public override ChunkStatus ChunkStatus { get; }
        public override IDictionary<HeightmapRegistry.Types, long[]> Heightmaps => _heightmaps;
        public int MinBuildHeight => MinSectionY * 16;
        public int MaxBuildHeight => (MaxSectionY + 1) * 16;

        public TestChunkAccess(ChunkPos pos, int minSectionY, int sectionsCount, LevelChunkSection? section = null)
        {
            _pos = pos;
            MinSectionY = minSectionY;
            SectionsCount = sectionsCount;
            _heightmaps = new Dictionary<HeightmapRegistry.Types, long[]>();
            _section = section;
            ChunkStatus = ChunkStatus.EMPTY;
        }

        public override LevelChunkSection? GetSection(int sectionY)
            => sectionY == MinSectionY ? _section : null;
    }

    //MockBlock 测试用 Block 子类
    private sealed class MockBlock : Block
    {
        public override Identifier Id { get; }
        public override BlockState DefaultBlockState { get; }

        public MockBlock(Identifier id)
        {
            Id = id;
            var state = BlockStateRegistry.Register(this, Array.Empty<PropertyBase>(), Array.Empty<object?>());
            BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
            DefaultBlockState = state;
        }
    }
}
