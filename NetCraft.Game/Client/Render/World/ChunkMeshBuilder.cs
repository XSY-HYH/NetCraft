using NetCraft.Gpu;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Render.World;

//ChunkMeshBuilder 区块 mesh 生成器对标原版 SectionCompiler
//遍历 LevelChunkSection 16³ 方块对非空方块查 BakedModel 按 cullface+邻居 BlockRenderShape 剔除面
//cullface quad 邻居是 FullBlock 则剔除该方向面 no-cull quad 总是渲染
//顶点位置由 BlockModelBaker 产出 0-16 范围经 PoseStack scale(1/16) 归一化到 0-1 再 translate(x,y,z) 平移到方块位置
//首版不跨 section 边界邻居查询超出 0-15 视为 air 不剔除（多渲染边界面功能正确）
//光照占位 QuadInstance Color=-1 全白 LightCoords=0 等 W4 光照引擎接入
//流体渲染不含 FluidRenderer 是独立子系统后续补
public sealed class ChunkMeshBuilder
{
    private readonly Func<BlockState, BakedModel?> _modelMapper;

    public ChunkMeshBuilder(BlockStateModelMapper modelMapper)
        : this(modelMapper.GetModel) { }

    //Func 构造供测试注入 stub 映射避免依赖 ResourceManager
    public ChunkMeshBuilder(Func<BlockState, BakedModel?> modelMapper)
    {
        _modelMapper = modelMapper;
    }

    //Build 遍历 section 16³ 方块生成 mesh 数据
    public ChunkMeshData Build(LevelChunkSection section)
    {
        var mesh = new ChunkMeshData();
        var pose = new PoseStack();
        for (var x = 0; x < 16; x++)
        for (var y = 0; y < 16; y++)
        for (var z = 0; z < 16; z++)
        {
            var state = section.GetBlockState(x, y, z);
            if (BlockRenderShapeProvider.GetShape(state) == BlockRenderShape.Empty)
                continue;
            var model = _modelMapper(state);
            if (model is null)
                continue;
            pose.PushPose();
            pose.Translate(x, y, z);
            pose.Scale(1f / 16f, 1f / 16f, 1f / 16f);
            AddBlockQuads(pose, section, x, y, z, model, mesh);
            pose.PopPose();
        }
        return mesh;
    }

    //AddBlockQuads 把方块的 BakedModel quad 按 layer + cullface 写入 mesh
    private static void AddBlockQuads(PoseStack pose, LevelChunkSection section,
        int x, int y, int z, BakedModel model, ChunkMeshData mesh)
    {
        var instance = new QuadInstance { Color = -1, LightCoords = 0 };
        foreach (var layer in model.Layers)
        {
            //cullface quad：按方向查邻居 FullBlock 则剔除
            AddCullfaceQuads(pose, section, x, y, z, model, layer, mesh, instance);
            //no-cull quad：总是渲染
            AddNoCullQuads(pose, model, layer, mesh, instance);
        }
    }

    //AddCullfaceQuads 遍历 6 方向 cullface quad 邻居是 FullBlock 则跳过该方向
    private static void AddCullfaceQuads(PoseStack pose, LevelChunkSection section,
        int x, int y, int z, BakedModel model, RenderLayer layer, ChunkMeshData mesh, QuadInstance instance)
    {
        foreach (var dir in AllDirections)
        {
            var quads = model.GetCullfaceQuads(layer, dir);
            if (quads.Count == 0) continue;
            if (ShouldCullFace(section, x, y, z, dir)) continue;
            var consumer = mesh.GetOrBeginLayer(layer);
            for (var i = 0; i < quads.Count; i++)
                consumer.PutBakedQuad(pose, quads[i], instance);
        }
    }

    //AddNoCullQuads 写入无 cullface 的 quad 总是渲染
    private static void AddNoCullQuads(PoseStack pose, BakedModel model,
        RenderLayer layer, ChunkMeshData mesh, QuadInstance instance)
    {
        var quads = model.GetNoCullQuads(layer);
        if (quads.Count == 0) return;
        var consumer = mesh.GetOrBeginLayer(layer);
        for (var i = 0; i < quads.Count; i++)
            consumer.PutBakedQuad(pose, quads[i], instance);
    }

    //ShouldCullFace 判断当前方块某方向面是否被邻居遮挡应剔除
    //邻居坐标超出 section 边界（0-15）视为 air 不剔除（首版不跨 section）
    //邻居 BlockRenderShape==FullBlock 则剔除 Custom/Empty 不剔除
    private static bool ShouldCullFace(LevelChunkSection section, int x, int y, int z, Direction dir)
    {
        var offset = dir.UnitVector();
        var nx = x + (int)offset.X;
        var ny = y + (int)offset.Y;
        var nz = z + (int)offset.Z;
        if ((uint)nx >= 16 || (uint)ny >= 16 || (uint)nz >= 16)
            return false;
        var neighbor = section.GetBlockState(nx, ny, nz);
        return BlockRenderShapeProvider.GetShape(neighbor) == BlockRenderShape.FullBlock;
    }

    private static readonly Direction[] AllDirections =
    {
        Direction.Down, Direction.Up, Direction.North,
        Direction.South, Direction.West, Direction.East
    };
}
