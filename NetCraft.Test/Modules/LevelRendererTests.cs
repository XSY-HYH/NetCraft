using System.Numerics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render;
using NetCraft.Game.Client.Render.Culling;
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

//LevelRendererTests 世界渲染场景单元测试
//W8 改造：LevelRenderer.Prepare 不再同步构建 mesh 核心断言下移到 ChunkMeshBuilder/SectionMesh/ViewArea
//覆盖空级别/单 section/frustum 剔除/air 跳过/多 layer/section 计数 6 个场景
//与 SectionDispatcherTests 互补聚焦 LevelRenderer 原有测试场景
internal static class LevelRendererTests
{
    public const string Module = "levelrenderer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LevelRenderer empty level produces zero visible sections", TestEmptyLevel);
        yield return ("LevelRenderer single section produces solid vertices", TestSingleSection);
        yield return ("LevelRenderer frustum culls sections behind camera", TestFrustumCullingBehind);
        yield return ("LevelRenderer air section skipped", TestAirSectionSkipped);
        yield return ("LevelRenderer multiple layers produce multiple draws", TestMultipleLayers);
        yield return ("LevelRenderer sectionCount and visibleSectionCount tracked", TestSectionCountTracking);
    }

    //空 ClientLevel 无 chunk ViewArea.Update 后 0 可见 section
    private static bool TestEmptyLevel()
    {
        var level = new ClientLevel();
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        viewArea.Update(frustum, level);
        return viewArea.VisibleCount == 0;
    }

    //单 section 有 stone 方块 ChunkMeshBuilder.Build 后 Solid layer 有 24 顶点（6 面 * 4）
    private static bool TestSingleSection()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var data = builder.Build(section);
        var mesh = SectionMesh.FromChunkMeshData(data);
        //单方块 6 面 * 4 = 24 顶点
        return mesh.GetVertexCount(RenderLayer.Solid) == 24
            && mesh.TotalVertexCount == 24;
    }

    //section 在 Camera 后方被 frustum 剔除 ViewArea.VisibleCount=0
    private static bool TestFrustumCullingBehind()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var level = new ClientLevel();
        //Camera 在 (8,8,24) 朝 -Z 前方 z 减小 后方 z 增大 section 放在 chunk(0,2) 即 z=32~48 在 Camera 后方
        LoadSection(level, 0, 2, section);
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        viewArea.Update(frustum, level);
        return viewArea.VisibleCount == 0;
    }

    //全 air section 被 HasOnlyAir 跳过不计入 VisibleCount
    private static bool TestAirSectionSkipped()
    {
        var (section, _) = NewSectionWithAir();
        var level = new ClientLevel();
        LoadSection(level, 0, 0, section);
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        viewArea.Update(frustum, level);
        return viewArea.VisibleCount == 0;
    }

    //Solid + Cutout 双 layer SectionMesh 分离 Solid 24 + Cutout 4 = 28 顶点
    private static bool TestMultipleLayers()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var builder = new ChunkMeshBuilder(_ => NewMultiLayerBakedModel());
        var data = builder.Build(section);
        var mesh = SectionMesh.FromChunkMeshData(data);
        //Solid 6 面 * 4 = 24 + Cutout 1 quad * 4 = 4 = 28
        return mesh.GetVertexCount(RenderLayer.Solid) == 24
            && mesh.GetVertexCount(RenderLayer.Cutout) == 4
            && mesh.TotalVertexCount == 28;
    }

    //视体内 section 可见 视体外 section 不可见 ViewArea.VisibleCount=1
    private static bool TestSectionCountTracking()
    {
        var (section1, stone) = NewSectionWithAir();
        section1.SetBlockState(0, 0, 0, stone);
        var level = new ClientLevel();
        LoadSection(level, 0, 0, section1);
        //视体外 section（在 Camera 后方 chunk(0,5) 即 z=80~96）
        var (section2, _) = NewSectionWithAir();
        section2.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 5, section2);
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        viewArea.Update(frustum, level);
        return viewArea.VisibleCount == 1;
    }

    //NewCameraLookingAtOrigin Camera 在 (8,8,24) 朝 -Z 看向原点 section(0,0,0) 在视体内
    private static Camera NewCameraLookingAtOrigin()
    {
        var camera = new Camera();
        camera.SetPosition(new Vector3(8, 8, 24));
        camera.UpdatePerspective(MathF.PI / 4f, 800, 600, 0.05f, 1000f);
        return camera;
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
