using System.Numerics;
using NetCraft.Game.Client.Level;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Render.World;

//LevelRenderer 世界渲染主入口对标原版 LevelRenderer
//持 StagedVertexBuffer+ClientLevel+ChunkMeshBuilder+Camera 遍历可见 section 构建 mesh 上传 GPU
//Prepare 阶段每帧重建所有 section mesh W8 改异步缓存
//Draw 阶段按 Solid→Cutout→Translucent 顺序 DrawIndexed 深度测试 GEQUAL
//顶点格式 POSITION_COLOR_UV_LIGHT_NORMAL stride 40 字节 CPU 端 bake section offset shader Model=Identity
public sealed class LevelRenderer : IDisposable, IWorldRenderer
{
    //POSITION_COLOR_UV_LIGHT_NORMAL 顶点 stride 40 字节 Position(12)+Color(4)+UV0(8)+Light(4)+Normal(12)
    private const int VertexStride = 40;
    private const int FloatsPerVertex = 10;

    private readonly StagedVertexBuffer _vertexBuffer = new();
    private readonly ClientLevel _level;
    private readonly ChunkMeshBuilder _meshBuilder;
    private readonly Camera _camera;

    //每个 RenderLayer 一个 Draw 累积所有可见 section 的顶点
    private readonly Dictionary<RenderLayer, Draw> _layerDraws = new();

    //性能指标供 GameScreen F3 显示
    public int SectionCount { get; private set; }
    public int VisibleSectionCount { get; private set; }
    public int TotalVertexCount => _vertexBuffer.TotalVertexCount;
    public int DrawCallCount { get; private set; }

    //ViewProj 当前帧 view*proj 矩阵 VulkanGuiApp 读此属性上传 set 0 UBO
    public Matrix4x4 ViewProj => _camera.GetViewProjMatrix();

    public LevelRenderer(ClientLevel level, Camera camera, ChunkMeshBuilder meshBuilder)
    {
        _level = level;
        _camera = camera;
        _meshBuilder = meshBuilder;
    }

    //Prepare 遍历可见 section 调 ChunkMeshBuilder.Build 写 StagedVertexBuffer
    //frustum culling 跳过视体外 section 每帧重建所有 mesh W8 改异步缓存
    public void Prepare()
    {
        _vertexBuffer.EndFrame();
        _layerDraws.Clear();
        SectionCount = 0;
        VisibleSectionCount = 0;

        var viewProj = _camera.GetViewProjMatrix();
        var frustum = new Culling.Frustum(viewProj);

        foreach (var chunk in _level.GetLoadedChunks())
        {
            var sectionsCount = chunk.SectionsCount;
            for (var sy = 0; sy < sectionsCount; sy++)
            {
                var section = chunk.GetSection(sy);
                if (section is null || section.HasOnlyAir()) continue;
                SectionCount++;

                var originX = chunk.Pos.X * 16;
                var originY = sy * 16;
                var originZ = chunk.Pos.Z * 16;

                //frustum culling AABB 是 section 世界坐标范围
                var aabb = new AABB(originX, originY, originZ, originX + 16, originY + 16, originZ + 16);
                if (!frustum.IsVisible(aabb)) continue;
                VisibleSectionCount++;

                var mesh = _meshBuilder.Build(section, originX, originY, originZ);
                AppendMeshToBuffer(mesh);
            }
        }

        //锁定所有 Draw 生成索引
        foreach (var draw in _layerDraws.Values)
            _vertexBuffer.EndDraw(draw);
    }

    //AppendMeshToBuffer 把 ChunkMeshData 各 layer 顶点拷贝到 StagedVertexBuffer 对应 Draw
    //VertexConsumer3D.Vertices 是 List<float> 每 10 float 一顶点逐顶点调 AddVertex3D
    private void AppendMeshToBuffer(ChunkMeshData mesh)
    {
        foreach (var layer in mesh.Layers)
        {
            var consumer = mesh.GetOrBeginLayer(layer);
            if (consumer.VertexCount == 0) continue;

            if (!_layerDraws.TryGetValue(layer, out var draw))
            {
                draw = _vertexBuffer.AppendDraw(
                    DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL,
                    PrimitiveTopology.Quads);
                _layerDraws[layer] = draw;
            }

            var builder = _vertexBuffer.GetVertexBuilder(draw);
            var vertices = consumer.Vertices;
            var vertexCount = vertices.Count / FloatsPerVertex;
            for (var i = 0; i < vertexCount; i++)
            {
                var off = i * FloatsPerVertex;
                //color/light 是 int 数值转换存 float 读出 (int)float 还原再传 AddVertex3D
                builder.AddVertex3D(
                    vertices[off + 0], vertices[off + 1], vertices[off + 2],
                    (int)vertices[off + 3],
                    vertices[off + 4], vertices[off + 5],
                    (int)vertices[off + 6],
                    vertices[off + 7], vertices[off + 8], vertices[off + 9]);
            }
        }
    }

    //Upload 上传顶点/索引到 GPU 跨帧复用 buffer size 不够才重建
    public void Upload(GpuDevice device) => _vertexBuffer.Upload(device);

    //Draw 按 Solid→Cutout→Translucent 顺序渲染 SetPipeline+BindDescriptorSet+DrawIndexed
    //pipelineResolver 由 VulkanGuiApp 传入调 PipelineCache.Precompile
    //descBinder 由 VulkanGuiApp 传入绑定 set 0 ViewProj UBO + set 1 atlas/lightmap sampler
    public void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder)
    {
        DrawCallCount = 0;
        foreach (var layer in new[] { RenderLayer.Solid, RenderLayer.Cutout, RenderLayer.Translucent })
        {
            if (!_layerDraws.TryGetValue(layer, out var draw)) continue;
            if (draw.IndexCount == 0) continue;

            var pipeline = layer switch
            {
                RenderLayer.Solid => WorldRenderPipelines.SOLID_TERRAIN,
                RenderLayer.Cutout => WorldRenderPipelines.CUTOUT_TERRAIN,
                RenderLayer.Translucent => WorldRenderPipelines.TRANSLUCENT_TERRAIN,
                _ => throw new InvalidOperationException($"未知 RenderLayer {layer}")
            };
            var info = _vertexBuffer.GetExecuteInfo(draw);

            pass.SetPipeline(pipelineResolver(pipeline));
            descBinder(pass);
            //BaseVertex 是顶点偏移转字节偏移 SetVertexBuffer 的 offset 参数
            pass.SetVertexBuffer(0, info.VertexBuffer, (ulong)info.BaseVertex * VertexStride);
            pass.SetIndexBuffer(info.IndexBuffer!, GpuIndexType.UInt32);
            pass.DrawIndexed(info.IndexCount);
            DrawCallCount++;
        }
    }

    public void Dispose() => _vertexBuffer.Dispose();
}
