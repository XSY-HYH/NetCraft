using System.Numerics;
using System.Threading;
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

//SectionDispatcherTests W8 SectionRenderDispatcher 数据结构骨架单元测试
//覆盖 SectionMesh 字节布局/RenderRegionCache 跨 section 邻居查询/RenderSection 状态机
//用 TestChunkAccess 装入 ClientLevel + mock BakedModel 避免依赖 GPU
internal static class SectionDispatcherTests
{
    public const string Module = "sectiondispatcher";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("SectionMesh single cube produces 24 vertices 40 byte stride", TestSingleCubeVertexLayout);
        yield return ("SectionMesh single cube produces 36 quad indices", TestSingleCubeIndices);
        yield return ("SectionMesh multi layer separates Solid and Cutout", TestMultiLayerSeparation);
        yield return ("SectionMesh empty layer omitted", TestEmptyLayerOmitted);
        yield return ("RenderRegionCache cross section neighbor lookup", TestCrossSectionNeighbor);
        yield return ("RenderRegionCache cull face across section boundary", TestCullFaceAcrossBoundary);
        yield return ("RenderRegionCache unloaded section returns air", TestUnloadedReturnsAir);
        yield return ("RenderSection initial state is Empty", TestInitialStateEmpty);
        yield return ("RenderSection TryMarkDirty Empty to Queued", TestMarkDirtyEmptyToQueued);
        yield return ("RenderSection TryBeginCompile Queued to Compiling", TestBeginCompile);
        yield return ("RenderSection PublishMesh to Compiled", TestPublishMesh);
        yield return ("RenderSection Uploaded to Dirty keeps old buffer", TestUploadedToDirtyKeepsBuffer);
        yield return ("RenderSection TryMarkDirty dedup when Queued", TestMarkDirtyDedup);
        yield return ("Dispatcher single section compiles to upload queue", TestDispatcherSingleSectionCompiles);
        yield return ("Dispatcher multiple workers compile all sections no loss", TestDispatcherMultipleWorkersNoLoss);
        yield return ("Dispatcher UploadTerrainBuffers uploads mesh to buffer pool", TestDispatcherUploadTerrainBuffers);
        yield return ("ViewArea single section visible triggers onNewSection", TestViewAreaSingleSectionVisible);
        yield return ("ViewArea section out of frustum no callback", TestViewAreaSectionOutOfFrustum);
        yield return ("Dispatcher SetCameraPosition triggers compile", TestDispatcherSetCameraPositionTriggersCompile);
    }

    //单 cube 6 面 * 4 顶点 = 24 顶点 stride 40 字节总 960 字节
    private static bool TestSingleCubeVertexLayout()
    {
        var mesh = BuildSingleCubeMesh();
        return mesh.GetVertexCount(RenderLayer.Solid) == 24
            && mesh.GetVertices(RenderLayer.Solid).Length == 24 * 40
            && mesh.TotalVertexCount == 24;
    }

    //单 cube 6 quad * 6 索引 = 36 索引
    private static bool TestSingleCubeIndices()
    {
        var mesh = BuildSingleCubeMesh();
        return mesh.GetIndices(RenderLayer.Solid).Length == 36
            && mesh.TotalIndexCount == 36;
    }

    //Solid 6 面 + Cutout 1 quad 两个 layer 分离
    private static bool TestMultiLayerSeparation()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var builder = new ChunkMeshBuilder(_ => NewMultiLayerBakedModel());
        var data = builder.Build(section);
        var mesh = SectionMesh.FromChunkMeshData(data);
        return mesh.HasLayer(RenderLayer.Solid)
            && mesh.HasLayer(RenderLayer.Cutout)
            && mesh.GetVertexCount(RenderLayer.Solid) == 24
            && mesh.GetVertexCount(RenderLayer.Cutout) == 4;
    }

    //空 section 产出的 mesh 无任何 layer
    private static bool TestEmptyLayerOmitted()
    {
        var (section, _) = NewSectionWithAir();
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var data = builder.Build(section);
        var mesh = SectionMesh.FromChunkMeshData(data);
        return !mesh.HasLayer(RenderLayer.Solid)
            && mesh.TotalVertexCount == 0
            && mesh.TotalIndexCount == 0;
    }

    //2 个相邻 section(0,0,0)和(1,0,0) RenderRegionCache 查跨 section 邻居方块
    private static bool TestCrossSectionNeighbor()
    {
        var (level, stone) = NewLevelWithAdjacentSections();
        var cache = RenderRegionCache.Snapshot(level, new SectionPos(0, 0, 0));
        //section(0,0,0) 的 (15,0,0) 邻居是 section(1,0,0) 的 (0,0,0) 即 stone
        var neighbor = cache.GetBlockState(16, 0, 0);
        return BlockStateRegistry.Owner(neighbor.Id).Id.Path == "stone";
    }

    //相邻 section 都有 stone 跨 section 边界面应被剔除
    private static bool TestCullFaceAcrossBoundary()
    {
        var (level, _) = NewLevelWithAdjacentSections();
        var cache = RenderRegionCache.Snapshot(level, new SectionPos(0, 0, 0));
        //(15,0,0) 的 East 邻居是 (16,0,0) 即 section(1,0,0) 的 stone FullBlock 应剔除
        return cache.ShouldCullFace(15, 0, 0, Direction.East);
    }

    //未加载的邻居 section ShouldCullFace 不剔除保守渲染等邻居加载后重编译
    private static bool TestUnloadedReturnsAir()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var level = new ClientLevel();
        LoadSection(level, 0, 0, section);
        //只装 section(0,0,0) 邻居 section(1,0,0) 未加载 ShouldCullFace 应返回 false
        var cache = RenderRegionCache.Snapshot(level, new SectionPos(0, 0, 0));
        return !cache.ShouldCullFace(15, 0, 0, Direction.East);
    }

    //RenderSection 构造后初始状态 Empty
    private static bool TestInitialStateEmpty()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        return rs.State == RenderSectionState.Empty
            && rs.Mesh is null
            && rs.VertexBuffer is null;
    }

    //Empty 状态 TryMarkDirty 返回 true 转 Queued
    private static bool TestMarkDirtyEmptyToQueued()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        var enqueued = rs.TryMarkDirty();
        return enqueued && rs.State == RenderSectionState.Queued;
    }

    //Queued 状态 TryBeginCompile 返回 true 转 Compiling
    private static bool TestBeginCompile()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        rs.TryMarkDirty();
        var begun = rs.TryBeginCompile();
        return begun && rs.State == RenderSectionState.Compiling;
    }

    //Compiling 状态 PublishMesh 转 Compiled Mesh 非 null
    private static bool TestPublishMesh()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        rs.TryMarkDirty();
        rs.TryBeginCompile();
        var mesh = BuildSingleCubeMesh();
        rs.PublishMesh(mesh);
        return rs.State == RenderSectionState.Compiled
            && ReferenceEquals(rs.Mesh, mesh);
    }

    //Uploaded 状态 TryMarkDirty 转 Dirty 旧 VertexBuffer 仍保留
    private static bool TestUploadedToDirtyKeepsBuffer()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        var (pool, created) = MakePool();
        var vb = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        var ib = pool.GetBuffer(2048, GpuBufferUsage.IndexBuffer);
        rs.SetUploadedBuffers(vb, ib);
        if (rs.State != RenderSectionState.Uploaded) return false;
        var enqueued = rs.TryMarkDirty();
        return enqueued
            && rs.State == RenderSectionState.Dirty
            && ReferenceEquals(rs.VertexBuffer, vb)
            && ReferenceEquals(rs.IndexBuffer, ib);
    }

    //Queued 状态再 TryMarkDirty 返回 false 不重复入队
    private static bool TestMarkDirtyDedup()
    {
        var rs = new RenderSection(new SectionPos(0, 0, 0));
        rs.TryMarkDirty();
        var second = rs.TryMarkDirty();
        return !second && rs.State == RenderSectionState.Queued;
    }

    //Dispatcher 单 section MarkDirty 后台编译完成入 upload 队列
    private static bool TestDispatcherSingleSectionCompiles()
    {
        var (level, _) = NewLevelWithSections(1);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var (pool, _) = MakePool();
        using var dispatcher = new SectionRenderDispatcher(level, builder, pool, workerCount: 1);
        dispatcher.Start();
        try
        {
            dispatcher.MarkDirty(new SectionPos(0, 0, 0));
            var compiled = SpinWait.SpinUntil(() => dispatcher.PendingUploadCount >= 1, 5000);
            return compiled && dispatcher.PendingUploadCount >= 1;
        }
        finally { dispatcher.Stop(); }
    }

    //Dispatcher 10 section 2 worker 全部编译无丢失
    private static bool TestDispatcherMultipleWorkersNoLoss()
    {
        const int N = 10;
        var (level, _) = NewLevelWithSections(N);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var (pool, _) = MakePool();
        using var dispatcher = new SectionRenderDispatcher(level, builder, pool, workerCount: 2);
        dispatcher.Start();
        try
        {
            for (var i = 0; i < N; i++)
                dispatcher.MarkDirty(new SectionPos(i, 0, 0));
            var compiled = SpinWait.SpinUntil(() => dispatcher.PendingUploadCount >= N, 10000);
            return compiled && dispatcher.PendingUploadCount >= N;
        }
        finally { dispatcher.Stop(); }
    }

    //Dispatcher UploadTerrainBuffers 编译后上传 mesh 借 vb+ib 设置 Slices
    private static bool TestDispatcherUploadTerrainBuffers()
    {
        var (level, _) = NewLevelWithSections(1);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var (pool, created) = MakePool();
        using var dispatcher = new SectionRenderDispatcher(level, builder, pool, workerCount: 1);
        dispatcher.Start();
        try
        {
            var pos = new SectionPos(0, 0, 0);
            dispatcher.MarkDirty(pos);
            SpinWait.SpinUntil(() => dispatcher.PendingUploadCount >= 1, 5000);
            dispatcher.Lock();
            try { dispatcher.UploadTerrainBuffers(); }
            finally { dispatcher.Unlock(); }
            var slice = dispatcher.GetSectionSlice(pos, RenderLayer.Solid);
            return slice is not null
                && slice.Value.IndexCount == 36
                && created.Count >= 2;
        }
        finally { dispatcher.Stop(); }
    }

    //ViewArea 单 section 在视锥内 onNewSection 回调触发 1 次 VisibleCount=1
    private static bool TestViewAreaSingleSectionVisible()
    {
        var (level, _) = NewLevelWithSections(1);
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        var newCount = 0;
        viewArea.Update(frustum, level, _ => newCount++);
        return newCount == 1 && viewArea.VisibleCount == 1;
    }

    //ViewArea section 在 Camera 后方视锥外 onNewSection 不触发 VisibleCount=0
    private static bool TestViewAreaSectionOutOfFrustum()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var level = new ClientLevel();
        //section 放在 chunk(0,2) 即 z=32~48 在 Camera(8,8,24) 朝 -Z 的后方
        LoadSection(level, 0, 2, section);
        var camera = NewCameraLookingAtOrigin();
        var frustum = new Frustum(camera.GetViewProjMatrix());
        var viewArea = new ViewArea();
        var newCount = 0;
        viewArea.Update(frustum, level, _ => newCount++);
        return newCount == 0 && viewArea.VisibleCount == 0;
    }

    //Dispatcher.SetCameraPosition 触发 ViewArea diff 新可见 section 入编译队列完成入 upload 队列
    private static bool TestDispatcherSetCameraPositionTriggersCompile()
    {
        var (level, _) = NewLevelWithSections(1);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var (pool, _) = MakePool();
        using var dispatcher = new SectionRenderDispatcher(level, builder, pool, workerCount: 1);
        dispatcher.Start();
        try
        {
            var camera = NewCameraLookingAtOrigin();
            var frustum = new Frustum(camera.GetViewProjMatrix());
            dispatcher.SetCameraPosition(camera, frustum);
            var compiled = SpinWait.SpinUntil(() => dispatcher.PendingUploadCount >= 1, 5000);
            return compiled && dispatcher.PendingUploadCount >= 1;
        }
        finally { dispatcher.Stop(); }
    }

    //NewCameraLookingAtOrigin Camera 在 (8,8,24) 朝 -Z 看向原点 section(0,0,0) 在视锥内
    private static Camera NewCameraLookingAtOrigin()
    {
        var camera = new Camera();
        camera.SetPosition(new Vector3(8, 8, 24));
        camera.UpdatePerspective(MathF.PI / 4f, 800, 600, 0.05f, 1000f);
        return camera;
    }

    //NewLevelWithSections 装入 count 个相邻 chunk(i,0) 各一个有 stone 的 section
    private static (ClientLevel level, BlockState stone) NewLevelWithSections(int count)
    {
        var level = new ClientLevel();
        BlockState stone = default;
        for (var i = 0; i < count; i++)
        {
            var (section, s) = NewSectionWithAir();
            section.SetBlockState(0, 0, 0, s);
            stone = s;
            LoadSection(level, i, 0, section);
        }
        return (level, stone);
    }

    //BuildSingleCubeMesh 构造单 cube SectionMesh 供布局测试
    private static SectionMesh BuildSingleCubeMesh()
    {
        var (section, stone) = NewSectionWithAir();
        section.SetBlockState(0, 0, 0, stone);
        var builder = new ChunkMeshBuilder(_ => NewCubeBakedModel());
        var data = builder.Build(section);
        return SectionMesh.FromChunkMeshData(data);
    }

    //NewLevelWithAdjacentSections 装入相邻 section(0,0,0)和(1,0,0)都有 stone
    private static (ClientLevel level, BlockState stone) NewLevelWithAdjacentSections()
    {
        var level = new ClientLevel();
        var (section0, stone) = NewSectionWithAir();
        section0.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 0, 0, section0);
        var (section1, _) = NewSectionWithAir();
        section1.SetBlockState(0, 0, 0, stone);
        LoadSection(level, 1, 0, section1);
        return (level, stone);
    }

    //MakePool 创建带 mock factory 的 GpuBufferPool 记录创建的 buffer
    private static (GpuBufferPool pool, List<MockBuffer> created) MakePool()
    {
        var created = new List<MockBuffer>();
        var pool = new GpuBufferPool((size, usage) =>
        {
            var b = new MockBuffer(size, usage);
            created.Add(b);
            return b;
        });
        return (pool, created);
    }

    private static void LoadSection(ClientLevel level, int chunkX, int chunkZ, LevelChunkSection section)
    {
        var chunk = new TestChunkAccess(new ChunkPos(chunkX, chunkZ), 0, 1, section);
        level.LoadChunk(chunk);
    }

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

    private static BakedModel NewMultiLayerBakedModel()
    {
        var model = NewCubeBakedModel();
        model.AddQuad(RenderLayer.Cutout, new BakedQuad(
            new(0, 0, 0), new(16, 0, 0), new(16, 16, 0), new(0, 16, 0),
            new(0, 0), new(1, 0), new(1, 1), new(0, 1), Direction.North), null);
        return model;
    }

    //MockBuffer 测试用 GpuBuffer 桩记录 Dispose 调用
    private sealed class MockBuffer : GpuBuffer
    {
        public bool Disposed;
        public MockBuffer(int size, GpuBufferUsage usage) : base(size, usage) { }
        public override void Upload<T>(ReadOnlySpan<T> data) { }
        public override void Download<T>(Span<T> data) { }
        public override void Dispose() => Disposed = true;
    }

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
