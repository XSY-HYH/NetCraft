using System.Numerics;

namespace NetCraft.Gpu;

//IVertexConsumer 顶点消费者接口对标原版 VertexConsumer
//RenderState.BuildVertices 通过此接口写入顶点数据
//阶段 4 StagedVertexBuffer 实现此接口暂存顶点
public interface IVertexConsumer
{
    //AddVertexWith2DPose 添加带 2D pose 变换的顶点位置+UV+颜色
    //pose 在此处 bake 进顶点位置同 Draw 内多元素可不同 pose 不影响合批
    void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color);
}
