using System.Numerics;
using NetCraft.Game.Client.Render.Entity;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//EntityRenderTests W9.1 实体渲染骨架单元测试
//覆盖 PoseStack 变换栈 EntityVertexBuilder 顶点布局 ModelPart 模型树
//EntityRenderPipelines pipeline 注册 EntityRenderDispatcher 调度器
public class EntityRenderTests
{
    public const string Module = "entityrender";

    public static IEnumerable<(string Name, Func<bool> Test)> All() => Tests();

    public static IEnumerable<(string, Func<bool>)> Tests()
    {
        yield return ("PoseStack Translate 累加位置", TestPoseStackTranslate);
        yield return ("PoseStack PushPose/PopPose 栈操作", TestPoseStackPushPop);
        yield return ("PoseStack Scale 缩放变换", TestPoseStackScale);
        yield return ("EntityVertexBuilder AddVertex 返回递增索引", TestVertexBuilderAddVertex);
        yield return ("EntityVertexBuilder AddQuad 4 顶点 6 索引", TestVertexBuilderAddQuad);
        yield return ("EntityVertexBuilder 顶点 11 float 布局", TestVertexBuilderFloatLayout);
        yield return ("ModelPart Cube.Compile 6 面 24 顶点", TestModelPartCubeCompile);
        yield return ("ModelPart.Render visible=false 跳过", TestModelPartVisibleFalseSkip);
        yield return ("ModelPart.Render 递归子节点", TestModelPartRenderChildren);
        yield return ("EntityRenderPipelines 3 个 pipeline 注册", TestEntityRenderPipelinesRegistered);
        yield return ("EntityRenderPipelines 顶点格式 POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL", TestEntityPipelineVertexFormat);
        yield return ("EntityRenderDispatcher Register/AddEntity/Prepare", TestDispatcherPrepare);
        yield return ("EntityRenderDispatcher 无实体时 HasContent=false", TestDispatcherEmpty);
        yield return ("EntityRenderDispatcher 多实体顶点翻倍", TestDispatcherMultipleEntities);
        yield return ("EntityRenderDispatcher 未注册实体跳过", TestDispatcherUnregisteredSkipped);
        yield return ("EntityRenderDispatcher ClearEntities 清空", TestDispatcherClearEntities);
        yield return ("EntityRenderDispatcher 空内容 Upload 不借 buffer", TestDispatcherEmptyUploadNoBuffer);
        yield return ("EntityRenderDispatcher Dispose 释放 buffer", TestDispatcherDispose);
        yield return ("PoseStack Rotate 四元数旋转", TestPoseStackRotate);
        yield return ("PoseStack MulPose 右乘矩阵", TestPoseStackMulPose);
        yield return ("PoseStack TransformPosition 变换顶点", TestPoseStackTransformPosition);
        yield return ("PoseStack TransformNormal 变换法线", TestPoseStackTransformNormal);
        yield return ("PoseStack SetIdentity 重置栈", TestPoseStackSetIdentity);
        yield return ("PoseStack Copy 快照栈顶", TestPoseStackCopy);
        yield return ("PoseStack IsEmpty 栈状态", TestPoseStackIsEmpty);
        yield return ("PoseStack 多层 Push/Pop 嵌套", TestPoseStackNestedPushPop);
        yield return ("PoseStack Scale 后 TransformPosition 缩放", TestPoseStackScaleTransform);
        yield return ("ModelPart TranslateAndRotate 位置除 16", TestModelPartTranslateAndRotate);
        yield return ("ModelPart SkipDraw 跳过自身渲染子节点", TestModelPartSkipDraw);
        yield return ("ModelPart Scale 缩放变换", TestModelPartScale);
        yield return ("ModelPart HasChild/GetChild 查找子节点", TestModelPartHasGetChild);
        yield return ("ModelPart GetChild 不存在抛异常", TestModelPartGetChildMissing);
        yield return ("ModelPart 多层嵌套子节点渲染", TestModelPartNestedChildren);
        yield return ("EntityVertexBuilder Clear 清空", TestVertexBuilderClear);
        yield return ("EntityVertexBuilder 多 quad 索引连续递增", TestVertexBuilderMultipleQuadIndices);
        yield return ("EntityVertexBuilder AddQuad UV 映射", TestVertexBuilderQuadUV);
    }

    private static bool TestPoseStackTranslate()
    {
        var stack = new PoseStack();
        stack.Translate(1f, 2f, 3f);
        var pose = stack.Pose();
        //平移矩阵的 M41/M42/M43 是平移分量
        return pose.M41 == 1f && pose.M42 == 2f && pose.M43 == 3f;
    }

    private static bool TestPoseStackPushPop()
    {
        var stack = new PoseStack();
        stack.PushPose();
        stack.Translate(5f, 0f, 0f);
        stack.PopPose();
        var pose = stack.Pose();
        //pop 后回到 Identity 平移分量归零
        return pose.M41 == 0f && pose.M42 == 0f && pose.M43 == 0f;
    }

    private static bool TestPoseStackScale()
    {
        var stack = new PoseStack();
        stack.Scale(2f, 3f, 4f);
        var pose = stack.Pose();
        //缩放矩阵的对角线 M11/M22/M33
        return pose.M11 == 2f && pose.M22 == 3f && pose.M33 == 4f;
    }

    private static bool TestVertexBuilderAddVertex()
    {
        var builder = new EntityVertexBuilder();
        var i0 = builder.AddVertex(Vector3.Zero, 0, 0, 0, 0, 0, Vector3.UnitY);
        var i1 = builder.AddVertex(Vector3.UnitX, 0, 0, 0, 0, 0, Vector3.UnitY);
        return i0 == 0 && i1 == 1 && builder.VertexCount == 2;
    }

    private static bool TestVertexBuilderAddQuad()
    {
        var builder = new EntityVertexBuilder();
        builder.AddQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY,
            -1, 0, 0, 1, 0, 1, 0, Vector3.UnitZ);
        //1 quad = 4 顶点 6 索引
        return builder.VertexCount == 4 && builder.Indices.Count == 6;
    }

    private static bool TestVertexBuilderFloatLayout()
    {
        var builder = new EntityVertexBuilder();
        var color = unchecked((int)0xFF0000FF);
        builder.AddVertex(new Vector3(1, 2, 3), color, 0.5f, 0.25f, 0, 0, new Vector3(0, 1, 0));
        var v = builder.Vertices;
        //11 float/顶点 position(3)+color(1)+uv(2)+overlay(1)+light(1)+normal(3)
        if (v.Count != 11) return false;
        return v[0] == 1f && v[1] == 2f && v[2] == 3f
            && v[3] == BitConverter.Int32BitsToSingle(color)
            && v[4] == 0.5f && v[5] == 0.25f
            && v[6] == 0f && v[7] == 0f
            && v[8] == 0f && v[9] == 1f && v[10] == 0f;
    }

    //CreateUnitCubePolygons 创建 0-16 范围单位 cube 的 6 面 polygon
    //每面 4 顶点 CCW 从外侧看 UV (0,0)-(1,0)-(1,1)-(0,1)
    private static ModelPart.Polygon[] CreateUnitCubePolygons()
    {
        var n = new[]
        {
            Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY,
            Vector3.UnitZ, -Vector3.UnitZ
        };
        //6 面 顶点 0-16 范围 CCW 从外侧看
        var faces = new[]
        {
            //East +X
            (new Vector3(16,16,0), new Vector3(16,0,0), new Vector3(16,0,16), new Vector3(16,16,16)),
            //West -X
            (new Vector3(0,16,16), new Vector3(0,0,16), new Vector3(0,0,0), new Vector3(0,16,0)),
            //Up +Y
            (new Vector3(0,16,0), new Vector3(0,16,16), new Vector3(16,16,16), new Vector3(16,16,0)),
            //Down -Y
            (new Vector3(0,0,16), new Vector3(0,0,0), new Vector3(16,0,0), new Vector3(16,0,16)),
            //South +Z
            (new Vector3(16,16,16), new Vector3(16,0,16), new Vector3(0,0,16), new Vector3(0,16,16)),
            //North -Z
            (new Vector3(0,16,0), new Vector3(0,0,0), new Vector3(16,0,0), new Vector3(16,16,0)),
        };
        var polygons = new ModelPart.Polygon[6];
        for (int i = 0; i < 6; i++)
        {
            var (p0, p1, p2, p3) = faces[i];
            var normal = n[i];
            polygons[i] = new ModelPart.Polygon(new[]
            {
                new ModelPart.Vertex(p0.X, p0.Y, p0.Z, 0, 0, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p1.X, p1.Y, p1.Z, 1, 0, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p2.X, p2.Y, p2.Z, 1, 1, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p3.X, p3.Y, p3.Z, 0, 1, normal.X, normal.Y, normal.Z),
            });
        }
        return polygons;
    }

    private static bool TestModelPartCubeCompile()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var builder = new EntityVertexBuilder();
        cube.Compile(Matrix4x4.Identity, Matrix4x4.Identity, builder,
            LightTexture.FullBrightCoords, 0, -1);
        //6 面 每 4 顶点 = 24 顶点 每 6 索引 = 36 索引
        return builder.VertexCount == 24 && builder.Indices.Count == 36;
    }

    private static bool TestModelPartVisibleFalseSkip()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var root = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>())
        {
            Visible = false
        };
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        root.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        return builder.VertexCount == 0;
    }

    private static bool TestModelPartRenderChildren()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var child = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>());
        var root = new ModelPart(new List<ModelPart.Cube>(), new Dictionary<string, ModelPart>
        {
            { "body", child }
        });
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        root.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        //子节点 cube 24 顶点
        return builder.VertexCount == 24;
    }

    private static bool TestEntityRenderPipelinesRegistered()
    {
        return EntityRenderPipelines.ENTITY_SOLID is not null
            && EntityRenderPipelines.ENTITY_CUTOUT is not null
            && EntityRenderPipelines.ENTITY_TRANSLUCENT is not null;
    }

    private static bool TestEntityPipelineVertexFormat()
    {
        //POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL stride 44 字节
        //Position(12)+Color(4)+UV0(8)+Overlay(4)+Light(4)+Normal(12)
        return DefaultVertexFormat.POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL.Stride == 44;
    }

    //TestCubeEntityModel 测试用立方体实体模型
    private sealed class TestCubeEntityModel : EntityModel
    {
        public TestCubeEntityModel() : base(CreateRoot()) { }
        private static ModelPart CreateRoot()
        {
            var cube = new ModelPart.Cube(CreateUnitCubePolygons());
            return new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>());
        }
    }

    //TestCubeEntityRenderer 测试用立方体实体渲染器
    private sealed class TestCubeEntityRenderer : EntityRenderer
    {
        private readonly EntityModel _model = new TestCubeEntityModel();
        public override EntityModel Model => _model;
    }

    private static bool TestDispatcherPrepare()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Register("test", new TestCubeEntityRenderer());
        dispatcher.AddEntity("test", new EntityRenderState
        {
            Position = new Vector3(10, 20, 30),
            YRot = 0f
        });
        dispatcher.Prepare(Vector3.Zero);
        //1 实体 cube 24 顶点 36 索引
        return dispatcher.VisibleEntityCount == 1
            && dispatcher.EntityVertexCount == 24
            && dispatcher.EntityIndexCount == 36
            && dispatcher.HasContent;
    }

    private static bool TestDispatcherEmpty()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Prepare(Vector3.Zero);
        return !dispatcher.HasContent
            && dispatcher.EntityVertexCount == 0
            && dispatcher.VisibleEntityCount == 0;
    }

    private static bool TestDispatcherMultipleEntities()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Register("test", new TestCubeEntityRenderer());
        dispatcher.AddEntity("test", new EntityRenderState { Position = new Vector3(0, 0, 0) });
        dispatcher.AddEntity("test", new EntityRenderState { Position = new Vector3(10, 0, 0) });
        dispatcher.Prepare(Vector3.Zero);
        //2 实体每 cube 24 顶点 36 索引
        return dispatcher.VisibleEntityCount == 2
            && dispatcher.EntityVertexCount == 48
            && dispatcher.EntityIndexCount == 72;
    }

    private static bool TestDispatcherUnregisteredSkipped()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Register("test", new TestCubeEntityRenderer());
        dispatcher.AddEntity("unknown", new EntityRenderState { Position = Vector3.Zero });
        dispatcher.AddEntity("test", new EntityRenderState { Position = Vector3.Zero });
        dispatcher.Prepare(Vector3.Zero);
        //未注册的 unknown 跳过只渲染 test
        return dispatcher.VisibleEntityCount == 1;
    }

    private static bool TestDispatcherClearEntities()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Register("test", new TestCubeEntityRenderer());
        dispatcher.AddEntity("test", new EntityRenderState { Position = Vector3.Zero });
        dispatcher.ClearEntities();
        dispatcher.Prepare(Vector3.Zero);
        return dispatcher.VisibleEntityCount == 0 && !dispatcher.HasContent;
    }

    private static bool TestDispatcherEmptyUploadNoBuffer()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Prepare(Vector3.Zero);
        dispatcher.Upload(null!);
        //空内容 Upload 不借 buffer pool InUseCount=0
        return pool.InUseCount == 0;
    }

    private static bool TestDispatcherDispose()
    {
        var pool = new GpuBufferPool((size, usage) => null!);
        var dispatcher = new EntityRenderDispatcher(pool);
        dispatcher.Dispose();
        //Dispose 不抛异常即可多次 Dispose 安全
        dispatcher.Dispose();
        return true;
    }

    //MockBuffer 测试用 GpuBuffer 桩记录 Upload 调用
    private sealed class MockBuffer : GpuBuffer
    {
        public int UploadCalls;
        public MockBuffer(int size, GpuBufferUsage usage) : base(size, usage) { }
        public override void Upload<T>(ReadOnlySpan<T> data) => UploadCalls++;
        public override void Download<T>(Span<T> data) { }
    }

    private static bool TestPoseStackRotate()
    {
        var stack = new PoseStack();
        //绕 Z 轴 90 度旋转后 X 轴方向变 Y 轴
        stack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f));
        var pose = stack.Pose();
        //System.Numerics 旋转矩阵 M11=cos=0 M22=cos=0 M12=sin=1 M21=-sin=-1
        return MathF.Abs(pose.M11) < 1e-6f
            && MathF.Abs(pose.M22) < 1e-6f
            && MathF.Abs(pose.M12 - 1f) < 1e-6f
            && MathF.Abs(pose.M21 - (-1f)) < 1e-6f;
    }

    private static bool TestPoseStackMulPose()
    {
        var stack = new PoseStack();
        stack.MulPose(Matrix4x4.CreateTranslation(5f, 0f, 0f));
        var pose = stack.Pose();
        return pose.M41 == 5f && pose.M42 == 0f && pose.M43 == 0f;
    }

    private static bool TestPoseStackTransformPosition()
    {
        var stack = new PoseStack();
        stack.Translate(10f, 20f, 30f);
        var p = stack.TransformPosition(1f, 2f, 3f);
        return p.X == 11f && p.Y == 22f && p.Z == 33f;
    }

    private static bool TestPoseStackTransformNormal()
    {
        var stack = new PoseStack();
        //均匀缩放法线方向不变
        stack.Scale(2f, 2f, 2f);
        var n = stack.TransformNormal(0f, 1f, 0f);
        return MathF.Abs(n.X) < 1e-6f
            && MathF.Abs(n.Y - 1f) < 1e-6f
            && MathF.Abs(n.Z) < 1e-6f;
    }

    private static bool TestPoseStackSetIdentity()
    {
        var stack = new PoseStack();
        stack.Translate(5f, 5f, 5f);
        stack.PushPose();
        stack.SetIdentity();
        var pose = stack.Pose();
        //SetIdentity 清栈回到 Identity
        return pose.M41 == 0f && pose.M42 == 0f && pose.M43 == 0f && stack.IsEmpty;
    }

    private static bool TestPoseStackCopy()
    {
        var stack = new PoseStack();
        stack.Translate(7f, 8f, 9f);
        var snapshot = stack.Copy();
        stack.PopPose();
        //Copy 快照栈顶 pop 后 snapshot 仍保留原值
        return snapshot.M41 == 7f && snapshot.M42 == 8f && snapshot.M43 == 9f;
    }

    private static bool TestPoseStackIsEmpty()
    {
        var stack = new PoseStack();
        if (!stack.IsEmpty) return false;
        stack.PushPose();
        if (stack.IsEmpty) return false;
        stack.PopPose();
        return stack.IsEmpty;
    }

    private static bool TestPoseStackNestedPushPop()
    {
        var stack = new PoseStack();
        stack.PushPose();
        stack.Translate(1f, 0f, 0f);
        stack.PushPose();
        stack.Translate(2f, 0f, 0f);
        //嵌套 2 层平移累加 M41=3
        if (MathF.Abs(stack.Pose().M41 - 3f) > 1e-6f) return false;
        stack.PopPose();
        //pop 1 层回到 M41=1
        if (MathF.Abs(stack.Pose().M41 - 1f) > 1e-6f) return false;
        stack.PopPose();
        //pop 到根 M41=0
        return MathF.Abs(stack.Pose().M41) < 1e-6f;
    }

    private static bool TestPoseStackScaleTransform()
    {
        var stack = new PoseStack();
        stack.Scale(2f, 4f, 8f);
        var p = stack.TransformPosition(1f, 1f, 1f);
        //缩放后顶点位置按各轴缩放因子
        return p.X == 2f && p.Y == 4f && p.Z == 8f;
    }

    private static bool TestModelPartTranslateAndRotate()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var part = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>())
        {
            //X=16 对应方块单位平移 1.0
            X = 16f
        };
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        part.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        var v = builder.Vertices;
        //第 0 顶点 X 坐标原 16 经 +1.0 平移变 17
        return MathF.Abs(v[0] - 17f) < 1e-6f;
    }

    private static bool TestModelPartSkipDraw()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var child = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>());
        var root = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>
        {
            { "body", child }
        })
        {
            SkipDraw = true
        };
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        root.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        //root SkipDraw 跳过自身 cube 只渲染 child 24 顶点
        return builder.VertexCount == 24;
    }

    private static bool TestModelPartScale()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var part = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>())
        {
            XScale = 2f,
            YScale = 2f,
            ZScale = 2f
        };
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        part.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        var v = builder.Vertices;
        //第 0 顶点 X 原坐标 16 经 2 倍缩放变 32
        return MathF.Abs(v[0] - 32f) < 1e-6f;
    }

    private static bool TestModelPartHasGetChild()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var child = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>());
        var root = new ModelPart(new List<ModelPart.Cube>(), new Dictionary<string, ModelPart>
        {
            { "body", child }
        });
        return root.HasChild("body")
            && !root.HasChild("missing")
            && ReferenceEquals(root.GetChild("body"), child);
    }

    private static bool TestModelPartGetChildMissing()
    {
        var root = new ModelPart(new List<ModelPart.Cube>(), new Dictionary<string, ModelPart>());
        try
        {
            root.GetChild("missing");
            return false;
        }
        catch (KeyNotFoundException) { return true; }
    }

    private static bool TestModelPartNestedChildren()
    {
        var polygons = CreateUnitCubePolygons();
        var cube = new ModelPart.Cube(polygons);
        var grandchild = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>());
        var child = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>
        {
            { "head", grandchild }
        });
        var root = new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>
        {
            { "body", child }
        });
        var builder = new EntityVertexBuilder();
        var stack = new PoseStack();
        root.Render(stack, builder, LightTexture.FullBrightCoords, 0, -1);
        //3 层每层 1 cube 24 顶点共 72
        return builder.VertexCount == 72;
    }

    private static bool TestVertexBuilderClear()
    {
        var builder = new EntityVertexBuilder();
        builder.AddQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY,
            -1, 0, 0, 1, 0, 1, 0, Vector3.UnitZ);
        builder.Clear();
        return builder.VertexCount == 0 && builder.Indices.Count == 0;
    }

    private static bool TestVertexBuilderMultipleQuadIndices()
    {
        var builder = new EntityVertexBuilder();
        builder.AddQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY,
            -1, 0, 0, 1, 0, 1, 0, Vector3.UnitZ);
        builder.AddQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY,
            -1, 0, 0, 1, 0, 1, 0, Vector3.UnitZ);
        var idx = builder.Indices;
        //2 quad 顶点 0-7 索引连续第 2 quad 起 4
        return builder.VertexCount == 8
            && idx.Count == 12
            && idx[6] == 4 && idx[7] == 5 && idx[8] == 6
            && idx[9] == 4 && idx[10] == 6 && idx[11] == 7;
    }

    private static bool TestVertexBuilderQuadUV()
    {
        var builder = new EntityVertexBuilder();
        builder.AddQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX + Vector3.UnitY, Vector3.UnitY,
            -1, 0.25f, 0.5f, 0.75f, 0.9f, 0, 0, Vector3.UnitZ);
        var v = builder.Vertices;
        //11 float/顶点 UV 在 index 4-5
        //p0 UV (0.25, 0.5) p2 UV (0.75, 0.9)
        return v[4] == 0.25f && v[5] == 0.5f
            && v[4 + 11 * 2] == 0.75f && v[5 + 11 * 2] == 0.9f;
    }
}
