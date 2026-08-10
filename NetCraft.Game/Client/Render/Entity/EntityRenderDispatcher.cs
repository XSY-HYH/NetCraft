using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.Entity;

//EntityRenderDispatcher 实体渲染调度器对标原版 EntityRenderDispatcher
//管理 EntityRenderer 注册表按 EntityType 分派
//持 EntityVertexBuilder 收集所有可见实体顶点 Upload 到 GPU buffer 后 DrawIndexed
//PoC 骨架版所有实体共用一个 vertex/index buffer 同一 pipeline 后续按 RenderLayer 分批
public sealed class EntityRenderDispatcher : IDisposable
{
    private readonly Dictionary<string, EntityRenderer> _renderers = new();
    private readonly List<(EntityRenderState State, EntityRenderer Renderer)> _visible = new();
    private readonly EntityVertexBuilder _builder = new();
    private readonly GpuBufferPool _bufferPool;
    private GpuBuffer? _vertexBuffer;
    private GpuBuffer? _indexBuffer;
    private int _vertexCount;
    private int _indexCount;
    private bool _disposed;

    //EntityVertexCount 最近一帧实体顶点数供调试
    public int EntityVertexCount => _vertexCount;
    //EntityIndexCount 最近一帧实体索引数
    public int EntityIndexCount => _indexCount;
    //VisibleEntityCount 最近一帧可见实体数
    public int VisibleEntityCount => _visible.Count;
    //HasContent 是否有可渲染的实体顶点
    public bool HasContent => _indexCount > 0;

    public EntityRenderDispatcher(GpuBufferPool bufferPool) => _bufferPool = bufferPool;

    //Register 注册实体渲染器按 entityTypeName 索引
    public void Register(string entityTypeName, EntityRenderer renderer)
        => _renderers[entityTypeName] = renderer;

    //ClearEntities 清空可见实体列表每帧 Prepare 前调
    public void ClearEntities() => _visible.Clear();

    //AddEntity 添加可见实体由 LevelRenderer 遍历实体列表时调
    //entityTypeName 匹配 Register 的 key 未注册的实体跳过
    public void AddEntity(string entityTypeName, EntityRenderState state)
    {
        if (_renderers.TryGetValue(entityTypeName, out var renderer))
            _visible.Add((state, renderer));
    }

    //Prepare 生成所有可见实体顶点到 builder
    //调用方负责相机变换 poseStack 从 Identity 开始每实体 push 世界变换
    //cameraPosition 用于实体相对位置计算
    public void Prepare(Vector3 cameraPosition)
    {
        _builder.Clear();
        _vertexCount = 0;
        _indexCount = 0;
        foreach (var (state, renderer) in _visible)
        {
            var poseStack = new PoseStack();
            //实体世界位置相对相机使 shader ViewProj 只含相机旋转+投影
            //与 terrain 一致 terrain 的 section offset bake 进顶点 position
            poseStack.Translate(
                state.Position.X - cameraPosition.X,
                state.Position.Y - cameraPosition.Y,
                state.Position.Z - cameraPosition.Z);
            //实体 Y 轴旋转朝向
            if (state.YRot != 0f)
                poseStack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, state.YRot));
            renderer.Render(poseStack, _builder, state);
        }
        _vertexCount = _builder.VertexCount;
        _indexCount = _builder.Indices.Count;
    }

    //Upload 把 builder 顶点索引上传到 GPU buffer 从 pool 借 buffer
    //无内容时跳过不借 buffer 避免空 DrawCall
    public void Upload(GpuDevice device)
    {
        //归还上一帧的 buffer
        if (_vertexBuffer is not null) { _bufferPool.ReturnBuffer(_vertexBuffer); _vertexBuffer = null; }
        if (_indexBuffer is not null) { _bufferPool.ReturnBuffer(_indexBuffer); _indexBuffer = null; }
        if (_indexCount == 0) return;
        //上传顶点 11 float/顶点 = 44 字节
        var vertexSpan = CollectionsMarshal.AsSpan(_builder.Vertices);
        var vertexBytes = MemoryMarshal.AsBytes(vertexSpan).ToArray();
        _vertexBuffer = _bufferPool.GetBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
        _vertexBuffer.Upload<byte>(vertexBytes);
        //上传索引 int/索引 = 4 字节
        var indexSpan = CollectionsMarshal.AsSpan(_builder.Indices);
        var indexBytes = MemoryMarshal.AsBytes(indexSpan).ToArray();
        _indexBuffer = _bufferPool.GetBuffer(indexBytes.Length, GpuBufferUsage.IndexBuffer);
        _indexBuffer.Upload<byte>(indexBytes);
    }

    //Draw 录制实体渲染命令绑定 vertex/index buffer 调 DrawIndexed
    //pipelineResolver 解析 RenderPipeline→CompiledRenderPipeline descBinder 绑定 descriptor set
    //调用方需先 SetPipeline 和 BindDescriptorSet 再调此方法
    public void Draw(IRenderPass pass)
    {
        if (_vertexBuffer is null || _indexBuffer is null || _indexCount == 0) return;
        pass.SetVertexBuffer(0, _vertexBuffer);
        pass.SetIndexBuffer(_indexBuffer, GpuIndexType.UInt32);
        pass.DrawIndexed(_indexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer = null;
        _indexBuffer = null;
        _disposed = true;
    }
}
