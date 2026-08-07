using System.Numerics;

namespace NetCraft.Gpu;

//VertexConsumer3D 3D 顶点消费者对标原版 VertexConsumer
//putBakedQuad 用 PoseStack 变换顶点位置和法线后写入 buffer
//顶点格式 position(3f)+color(1int)+uv(2f)+light(1int)+normal(3f) 简化原版去掉 overlay
//PoC 用 List<float> 暂存顶点供测试验证不直接上传 GPU 后续接 StagedVertexBuffer
public sealed class VertexConsumer3D
{
    //Vertices 写入的顶点数据每个顶点 10 float position3+color1+uv2+light1+normal3
    public readonly List<float> Vertices = new();

    //PutBakedQuad 用 PoseStack 变换 BakedQuad 的 4 顶点后写入 buffer
    //法线是面级别来自 Direction 经法线矩阵变换 color/light 来自 QuadInstance
    public void PutBakedQuad(PoseStack poseStack, in BakedQuad quad, QuadInstance instance)
    {
        var pose = poseStack.Pose();
        var normalMatrix = poseStack.Normal();
        var normalVec = quad.Direction.UnitVector();
        var normal = Vector3.Normalize(Vector3.TransformNormal(normalVec, normalMatrix));
        var lightEmission = quad.LightEmission;
        for (var i = 0; i < BakedQuad.VertexCount; i++)
        {
            var pos = Vector3.Transform(quad.Position(i), pose);
            var uv = quad.Uv(i);
            var color = instance.GetColor(i);
            var light = instance.GetLightCoordsWithEmission(i, lightEmission);
            //position3
            Vertices.Add(pos.X); Vertices.Add(pos.Y); Vertices.Add(pos.Z);
            //color1
            Vertices.Add(color);
            //uv2
            Vertices.Add(uv.X); Vertices.Add(uv.Y);
            //light1
            Vertices.Add(light);
            //normal3
            Vertices.Add(normal.X); Vertices.Add(normal.Y); Vertices.Add(normal.Z);
        }
    }

    public int VertexCount => Vertices.Count / 10;
    public void Clear() => Vertices.Clear();
}
