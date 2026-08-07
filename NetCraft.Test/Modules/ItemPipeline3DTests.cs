using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//ItemPipeline3DTests 3D 物品渲染管线 PoC 测试
//覆盖 PoseStack/Projection/BakedQuad/CubeModel/ItemStackRenderState/ItemFeatureRenderer
//验证 DrawToSlot→item.submit→Execute→putBakedQuad 顶点生成链路
//用 MockGpuDevice 不依赖 Vulkan
internal static class ItemPipeline3DTests
{
    public const string Module = "itempipeline3d";

    public static IEnumerable<(string, Func<bool>)> All()
    {
        yield return ("PoseStack.PushPose isolates transforms", TestPoseStackPushPoseIsolates);
        yield return ("PoseStack.Scale flips Y with negative", TestPoseStackScaleFlipsY);
        yield return ("PoseStack.TransformNormal normalizes", TestPoseStackTransformNormal);
        yield return ("Projection.SetupOrtho invertY flips Y axis", TestProjectionOrthoInvertY);
        yield return ("Projection.SetupPerspective produces perspective matrix", TestProjectionPerspective);
        yield return ("CubeModel.Create returns 6 quads", TestCubeModelReturns6Quads);
        yield return ("BakedQuad.Position/Uv indexer returns correct values", TestBakedQuadIndexer);
        yield return ("ItemStackRenderState.Submit snapshots pose", TestItemSubmitSnapshotsPose);
        yield return ("ItemFeatureRenderer.Execute writes 24 vertices for cube", TestExecuteWrites24Vertices);
        yield return ("ItemItemAtlas.DrawToSlot generates vertices", TestItemItemAtlasDrawToSlot);
        yield return ("ItemItemAtlas.GetOrUpdate returns SlotView for registered item", TestItemItemAtlasGetOrUpdate);
        yield return ("ItemPipRenderer.RenderToTexture generates vertices", TestItemPipRendererRenderToTexture);
        //P10 3D 物品 GPU 渲染链路
        yield return ("Lighting precomputes Items3D light directions", TestLightingItems3D);
        yield return ("Lighting.SetupFor switches current entry", TestLightingSetupFor);
        yield return ("Lighting.LightUniform is 32 bytes for std140", TestLightUniformSize);
        yield return ("DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL stride 40", TestItemVertexFormatStride);
        yield return ("BindGroupLayouts.ITEM_MATRICES and ITEM_LIGHTING exist", TestItemBindGroupLayouts);
        yield return ("RenderPipelines.ITEM_3D registered with location", TestItem3DPipelineRegistered);
        yield return ("ShaderManager compiles item_3d vertex shader", TestItem3DShaderCompiles);
        yield return ("ItemItemAtlas CPU path skips GPU rendering on mock device", TestItemItemAtlasCpuPathOnlyOnMock);
        //P11 lightmap 真光照
        yield return ("LightTexture.PackLightCoords packs block/sky light", TestLightTexturePackCoords);
        yield return ("LightTexture.FullBrightCoords equals ItemFeatureRenderer.FullBright", TestLightTextureFullBrightEquals);
        yield return ("LightTexture.ComputePixel full bright returns white", TestLightTextureComputePixelFullBright);
        yield return ("LightTexture.GeneratePixels 16x16 RGBA", TestLightTextureGeneratePixels);
        yield return ("BindGroupLayouts.ITEM_LIGHTMAP contains 1 sampler", TestItemLightmapLayout);
        yield return ("RenderPipelines.ITEM_3D has 3 bindgroup layouts with lightmap", TestItem3DPipelineHasLightmap);
        yield return ("ShaderManager compiles item_3d vert+frag with lightmap sampling", TestItem3DShaderCompilesWithLightmap);
        //P12 frag 采样 AtlasTexture + tint 颜色
        yield return ("ItemTextureAtlas.GenerateTestTexture 16x16 RGBA checkerboard", TestItemTextureAtlasGenerateTestTexture);
        yield return ("ItemTints.PackColor packs RGBA to ARGB int", TestItemTintsPackColor);
        yield return ("ItemTints.GetTint returns white for any tintIndex", TestItemTintsGetTint);
        yield return ("BindGroupLayouts.ITEM_ATLAS contains 1 sampler", TestItemAtlasLayout);
        yield return ("RenderPipelines.ITEM_3D has 4 bindgroup layouts with atlas", TestItem3DPipelineHasAtlas);
        yield return ("ShaderManager compiles item_3d vert+frag with atlas+tint", TestItem3DShaderCompilesWithAtlas);
        yield return ("ItemFeatureRenderer.Execute writes tint color from quad.TintIndex", TestItemFeatureRendererWritesTintColor);
    }

    private static bool TestPoseStackPushPoseIsolates()
    {
        var ps = new PoseStack();
        ps.PushPose();
        ps.Translate(10, 20, 30);
        //栈顶有 translate 栈底无
        var top = ps.Pose();
        ps.PopPose();
        var bottom = ps.Pose();
        //top.Translation 应 (10,20,30) bottom 应 (0,0,0)
        return top.Translation == new Vector3(10, 20, 30)
            && bottom.Translation == Vector3.Zero;
    }

    private static bool TestPoseStackScaleFlipsY()
    {
        var ps = new PoseStack();
        ps.Scale(1, -1, 1);
        var m = ps.Pose();
        //M22 应为 -1（Y 缩放 -1）
        return m.M22 == -1f;
    }

    private static bool TestPoseStackTransformNormal()
    {
        var ps = new PoseStack();
        ps.Scale(2, 2, 2);
        var n = ps.TransformNormal(1, 0, 0);
        //均匀缩放法线归一化后仍 (1,0,0)
        return MathF.Abs(n.X - 1f) < 0.001f && MathF.Abs(n.Y) < 0.001f && MathF.Abs(n.Z) < 0.001f;
    }

    private static bool TestProjectionOrthoInvertY()
    {
        var proj = new Projection();
        proj.SetupOrtho(-1000, 1000, 512, 512, true);
        var m = proj.GetMatrix();
        //正交投影 M22 应为负（Y 翻转）invertY=true 时 bottom=512 top=0
        return m.M22 < 0f;
    }

    private static bool TestProjectionPerspective()
    {
        var proj = new Projection();
        proj.SetupPerspective(0.1f, 1000f, MathF.PI / 4f, 800, 600);
        var m = proj.GetMatrix();
        //透视投影 M33 非 1 M43 非 0
        return m.M33 != 1f;
    }

    private static bool TestCubeModelReturns6Quads()
    {
        var quads = CubeModel.Create(1f);
        return quads.Count == 6
            && quads[0].Direction == Direction.Down
            && quads[1].Direction == Direction.Up;
    }

    private static bool TestBakedQuadIndexer()
    {
        var q = new BakedQuad(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(1, 1, 0), new Vector3(0, 1, 0),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1),
            Direction.North);
        return q.Position(0) == new Vector3(0, 0, 0)
            && q.Position(2) == new Vector3(1, 1, 0)
            && q.Uv(1) == new Vector2(1, 0)
            && q.Direction == Direction.North;
    }

    private static bool TestItemSubmitSnapshotsPose()
    {
        var ps = new PoseStack();
        ps.Translate(5, 6, 7);
        var item = new ItemStackRenderState();
        item.SetQuads(CubeModel.Create(1f));
        var collector = new ItemSubmitCollector();
        item.Submit(ps, collector, ItemFeatureRenderer.FullBright, 0, 0);
        //提交后改 pose 不影响已快照的 node
        ps.Translate(100, 100, 100);
        return collector.Nodes.Count == 1
            && collector.Nodes[0].Pose.Translation == new Vector3(5, 6, 7);
    }

    private static bool TestExecuteWrites24Vertices()
    {
        var ps = new PoseStack();
        var item = new ItemStackRenderState();
        item.SetQuads(CubeModel.Create(1f));
        var collector = new ItemSubmitCollector();
        item.Submit(ps, collector, ItemFeatureRenderer.FullBright, 0, 0);
        var consumer = new VertexConsumer3D();
        ItemFeatureRenderer.Execute(collector, consumer);
        //6 面 × 4 顶点 = 24 顶点
        return consumer.VertexCount == 24;
    }

    private static bool TestItemItemAtlasDrawToSlot()
    {
        using var atlas = new ItemItemAtlas(new MockDevice(), 512, 16);
        atlas.RegisterItem("sword", 1f);
        //GetOrUpdate 触发 DrawToSlot Empty 状态
        var view = atlas.GetOrUpdate("sword", false);
        //24 顶点 × 10 float = 240
        return view != null && atlas.LastFrameVertices.VertexCount == 24;
    }

    private static bool TestItemItemAtlasGetOrUpdate()
    {
        using var atlas = new ItemItemAtlas(new MockDevice(), 512, 16);
        atlas.RegisterItem("apple", 1f);
        var view = atlas.GetOrUpdate("apple", false);
        //Vulkan 纹理 V=0 顶部 slot(0,0) 的 U0/V0 都为 0 slotUvSize=16/512=0.03125
        return view != null
            && MathF.Abs(view!.U0 - 0f) < 0.001f
            && MathF.Abs(view.V0 - 0f) < 0.001f;
    }

    private static bool TestItemPipRendererRenderToTexture()
    {
        var renderer = new ItemPipRenderer(new MockDevice());
        renderer.RegisterItem("helmet", 1f);
        var state = new ItemPipState(0, 0, 64, 64, 1f, ScreenRectangle.Empty, Matrix3x2.Identity, "helmet", 0f);
        var guiState = new GuiRenderState();
        renderer.Prepare(state, guiState, 1);
        //Prepare 触发 EnsureTextures+RenderToTexture+BlitTexture 生成 24 顶点
        return renderer.LastFrameVertices.VertexCount == 24;
    }

    //TestLightingItems3D 验证 Items3D 光方向经 item3DPose 矩阵变换后归一化
    private static bool TestLightingItems3D()
    {
        var lighting = new Lighting(null);
        var light = lighting.GetLightDirections(Lighting.Entry.Items3D);
        //Light0/Light1 是 Vector4 xyz 归一化 w=0
        return MathF.Abs(light.Light0.Length() - 1f) < 0.001f
            && MathF.Abs(light.Light1.Length() - 1f) < 0.001f
            && light.Light0.W == 0f
            && light.Light1.W == 0f;
    }

    //TestLightingSetupFor 验证 SetupFor 切换 Current entry
    private static bool TestLightingSetupFor()
    {
        var lighting = new Lighting(null);
        lighting.SetupFor(Lighting.Entry.ItemsFlat);
        if (lighting.Current != Lighting.Entry.ItemsFlat) return false;
        lighting.SetupFor(Lighting.Entry.Items3D);
        return lighting.Current == Lighting.Entry.Items3D;
    }

    //TestLightUniformSize 验证 LightUniform 是 Vector4+Vector4 = 32 bytes 匹配 std140 padding
    private static bool TestLightUniformSize()
    {
        var size = System.Runtime.InteropServices.Marshal.SizeOf<Lighting.LightUniform>();
        return size == 32;
    }

    //TestItemVertexFormatStride 验证 POSITION_COLOR_UV_LIGHT_NORMAL 顶点 stride = 40 bytes (10 float)
    private static bool TestItemVertexFormatStride()
    {
        var f = DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL;
        return f.Stride == 40
            && f.Elements.Count == 5
            && f.Elements[0].Name == "Position"
            && f.Elements[1].Name == "Color"
            && f.Elements[2].Name == "UV0"
            && f.Elements[3].Name == "Light"
            && f.Elements[4].Name == "Normal";
    }

    //TestItemBindGroupLayouts 验证 ITEM_MATRICES 和 ITEM_LIGHTING 各含 1 个 uniform
    private static bool TestItemBindGroupLayouts()
    {
        return BindGroupLayouts.ITEM_MATRICES.Uniforms.Count == 1
            && BindGroupLayouts.ITEM_MATRICES.Uniforms[0].Type == UniformType.Mat4
            && BindGroupLayouts.ITEM_LIGHTING.Uniforms.Count == 1
            && BindGroupLayouts.ITEM_LIGHTING.Uniforms[0].Type == UniformType.Vec4;
    }

    //TestItem3DPipelineRegistered 验证 ITEM_3D pipeline 注册到 location 表
    private static bool TestItem3DPipelineRegistered()
    {
        var p = RenderPipelines.ITEM_3D;
        return p.Location == "pipeline/item_3d"
            && p.VertexShader == "core/item_3d"
            && p.FragmentShader == "core/item_3d"
            && p.BindGroupLayouts.Count == 4
            && p.BindGroupLayouts[0] == BindGroupLayouts.ITEM_MATRICES
            && p.BindGroupLayouts[1] == BindGroupLayouts.ITEM_LIGHTING
            && p.BindGroupLayouts[2] == BindGroupLayouts.ITEM_LIGHTMAP
            && p.BindGroupLayouts[3] == BindGroupLayouts.ITEM_ATLAS
            && p.VertexFormatPerBuffer[0] == DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL
            && p.PrimitiveTopology == PrimitiveTopology.Quads
            && p.WantsDepthTexture()
            && RenderPipelines.GetByLocation("pipeline/item_3d") == p;
    }

    //TestItem3DPipelineHasLightmap 验证 ITEM_3D 第三个 bindgroup 是 lightmap sampler
    private static bool TestItem3DPipelineHasLightmap()
    {
        var p = RenderPipelines.ITEM_3D;
        return p.BindGroupLayouts.Count == 4
            && p.BindGroupLayouts[2] == BindGroupLayouts.ITEM_LIGHTMAP
            && BindGroupLayouts.ITEM_LIGHTMAP.Samplers.Count == 1
            && BindGroupLayouts.ITEM_LIGHTMAP.Samplers[0] == "LightmapSampler"
            && BindGroupLayouts.ITEM_LIGHTMAP.Uniforms.Count == 0;
    }

    //TestItem3DPipelineHasAtlas 验证 ITEM_3D 第四个 bindgroup 是 atlas sampler
    private static bool TestItem3DPipelineHasAtlas()
    {
        var p = RenderPipelines.ITEM_3D;
        return p.BindGroupLayouts.Count == 4
            && p.BindGroupLayouts[3] == BindGroupLayouts.ITEM_ATLAS
            && BindGroupLayouts.ITEM_ATLAS.Samplers.Count == 1
            && BindGroupLayouts.ITEM_ATLAS.Samplers[0] == "AtlasSampler"
            && BindGroupLayouts.ITEM_ATLAS.Uniforms.Count == 0;
    }

    //TestItem3DShaderCompiles 验证 ShaderManager 能加载并编译 item_3d vert shader 为 SPIR-V
    private static bool TestItem3DShaderCompiles()
    {
        var device = new MockDevice();
        var spirv = device.ShaderManager.LoadVertexShader("core/item_3d");
        return spirv != null && spirv.Length > 0;
    }

    //TestItem3DShaderCompilesWithLightmap 验证 vert+frag shader 含 lightmap 解包+采样能编译为 SPIR-V
    private static bool TestItem3DShaderCompilesWithLightmap()
    {
        var device = new MockDevice();
        var vert = device.ShaderManager.LoadVertexShader("core/item_3d");
        var frag = device.ShaderManager.LoadFragmentShader("core/item_3d");
        return vert != null && vert.Length > 0
            && frag != null && frag.Length > 0;
    }

    //TestItem3DShaderCompilesWithAtlas 验证 vert+frag shader 含 atlas 采样+tint 解包能编译为 SPIR-V
    private static bool TestItem3DShaderCompilesWithAtlas()
    {
        var device = new MockDevice();
        var vert = device.ShaderManager.LoadVertexShader("core/item_3d");
        var frag = device.ShaderManager.LoadFragmentShader("core/item_3d");
        return vert != null && vert.Length > 0
            && frag != null && frag.Length > 0;
    }

    //TestItemTextureAtlasGenerateTestTexture 验证棋盘格测试纹理尺寸和格子颜色
    private static bool TestItemTextureAtlasGenerateTestTexture()
    {
        var pixels = ItemTextureAtlas.GenerateTestTexture();
        //16x16 RGBA = 1024 bytes
        if (pixels.Length != 16 * 16 * 4) return false;
        //(0,0) cellX=0 cellY=0 isLight=true 浅灰 200
        var idx00 = 0;
        if (pixels[idx00] != 200 || pixels[idx00 + 3] != 255) return false;
        //(8,0) cellX=1 cellY=0 isLight=false 深灰 80
        var idx10 = (0 * 16 + 8) * 4;
        return pixels[idx10] == 80 && pixels[idx10 + 1] == 80;
    }

    //TestItemTintsPackColor 验证 PackColor 把 RGBA packed 成 ARGB int
    private static bool TestItemTintsPackColor()
    {
        //红色 R=255 G=0 B=0 A=255 packed ARGB = 0xFFFF0000
        var red = ItemTints.PackColor(255, 0, 0, 255);
        if (red != unchecked((int)0xFFFF0000)) return false;
        //白色 R=G=B=A=255 packed ARGB = 0xFFFFFFFF = -1
        var white = ItemTints.PackColor(255, 255, 255, 255);
        return white == -1 && white == ItemTints.White;
    }

    //TestItemTintsGetTint 验证 GetTint 对任意 tintIndex 返回白色 PoC 行为
    private static bool TestItemTintsGetTint()
    {
        return ItemTints.GetTint(-1) == ItemTints.White
            && ItemTints.GetTint(0) == ItemTints.White
            && ItemTints.GetTint(1) == ItemTints.White
            && ItemTints.GetTint(99) == ItemTints.White;
    }

    //TestItemAtlasLayout 验证 ITEM_ATLAS 含 1 个 sampler 无 uniform
    private static bool TestItemAtlasLayout()
    {
        return BindGroupLayouts.ITEM_ATLAS.Samplers.Count == 1
            && BindGroupLayouts.ITEM_ATLAS.Samplers[0] == "AtlasSampler"
            && BindGroupLayouts.ITEM_ATLAS.Uniforms.Count == 0;
    }

    //TestItemFeatureRendererWritesTintColor 验证 Execute 把 quad.TintIndex 查 ItemTints 写入顶点 color 字段
    private static bool TestItemFeatureRendererWritesTintColor()
    {
        var collector = new ItemSubmitCollector();
        var pose = new PoseStack();
        //用 tintIndex=-1 的 quad 提交 ItemFeatureRenderer.Execute 应写 ItemTints.White=-1 到 color 字段
        var quads = CubeModel.Create(1f);
        collector.SubmitItem(pose.Pose(), quads, ItemFeatureRenderer.FullBright, ItemFeatureRenderer.NoOverlay, 0);
        var consumer = new VertexConsumer3D();
        ItemFeatureRenderer.Execute(collector, consumer);
        //顶点格式 position3+color1+uv2+light1+normal3 = 10 float color 在 index 3
        if (consumer.VertexCount == 0) return false;
        var color = consumer.Vertices[3];
        //ItemTints.White = -1 转 float = -1.0f
        return MathF.Abs(color - (-1f)) < 0.001f;
    }

    //TestLightTexturePackCoords 验证 PackLightCoords 正确 packed block/sky 光等级
    private static bool TestLightTexturePackCoords()
    {
        //FullBright (15,15) packed = (15<<4)|(15<<20) = 0xF0|0xF00000 = 0x00F000F0
        var packed = LightTexture.PackLightCoords(15, 15);
        if (packed != 0x00F000F0) return false;
        if (LightTexture.UnpackBlockLight(packed) != 15) return false;
        if (LightTexture.UnpackSkyLight(packed) != 15) return false;
        //(7,3) packed = (7<<4)|(3<<20) = 0x70|0x300000 = 0x00300070
        var p2 = LightTexture.PackLightCoords(7, 3);
        return p2 == 0x00300070
            && LightTexture.UnpackBlockLight(p2) == 7
            && LightTexture.UnpackSkyLight(p2) == 3;
    }

    //TestLightTextureFullBrightEquals 验证 LightTexture.FullBrightCoords 与 ItemFeatureRenderer.FullBright 等价
    private static bool TestLightTextureFullBrightEquals()
    {
        return LightTexture.FullBrightCoords == ItemFeatureRenderer.FullBright
            && LightTexture.FullBrightCoords == LightTexture.PackLightCoords(15, 15);
    }

    //TestLightTextureComputePixelFullBright 验证 (15,15) 满亮返回 255,255,255
    private static bool TestLightTextureComputePixelFullBright()
    {
        var (r, g, b) = LightTexture.ComputePixel(15, 15);
        return r == 255 && g == 255 && b == 255;
    }

    //TestLightTextureGeneratePixels 验证像素数组尺寸和角点像素值
    private static bool TestLightTextureGeneratePixels()
    {
        var pixels = LightTexture.GeneratePixels(1.0f);
        //16x16 RGBA = 1024 bytes
        if (pixels.Length != 16 * 16 * 4) return false;
        //(0,0) block=0 sky=0 全黑 RGB=0 A=255
        var idx00 = 0;
        if (pixels[idx00] != 0 || pixels[idx00 + 3] != 255) return false;
        //(15,15) block=15 sky=15 满亮 RGB=255 A=255
        var idxFF = (15 * 16 + 15) * 4;
        return pixels[idxFF] == 255 && pixels[idxFF + 1] == 255 && pixels[idxFF + 2] == 255;
    }

    //TestItemLightmapLayout 验证 ITEM_LIGHTMAP 含 1 个 sampler 无 uniform
    private static bool TestItemLightmapLayout()
    {
        return BindGroupLayouts.ITEM_LIGHTMAP.Samplers.Count == 1
            && BindGroupLayouts.ITEM_LIGHTMAP.Samplers[0] == "LightmapSampler"
            && BindGroupLayouts.ITEM_LIGHTMAP.Uniforms.Count == 0;
    }

    //TestItemItemAtlasCpuPathOnlyOnMock 验证 MockDevice 走 CPU 路径不抛 GPU 渲染异常
    private static bool TestItemItemAtlasCpuPathOnlyOnMock()
    {
        using var atlas = new ItemItemAtlas(new MockDevice(), 512, 16);
        atlas.RegisterItem("sword", 1f);
        var view = atlas.GetOrUpdate("sword", false);
        //MockDevice SupportsGpuRendering=false 仅 CPU 顶点生成 LastFrameVertices 24 顶点
        return view != null && atlas.LastFrameVertices.VertexCount == 24;
    }

    //MockDevice 测试用 GpuDevice 桩支持 CreateImage/CreateSampler 不依赖 Vulkan
    private sealed class MockDevice : GpuDevice
    {
        public override DeviceLimits Limits { get; } = new(4096);
        public MockDevice() : base(new EmptyGpuContext()) { }
        public override GpuCommandBuffer CreateCommandBuffer() => throw new NotSupportedException();
        public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription d) => throw new NotSupportedException();
        public override GpuBuffer CreateBuffer(int size, GpuBufferUsage u) => throw new NotSupportedException();
        public override GpuImage CreateImage(GpuImageDescription desc) => new MockImage(desc);
        public override GpuShader CreateShader(GpuShaderStage s, byte[] c, string e = "main") => throw new NotSupportedException();
        public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription d) => throw new NotSupportedException();
        public override GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout l) => throw new NotSupportedException();
        public override GpuSampler CreateSampler(GpuSamplerDescription d) => null!;
    }

    private sealed class MockImage : GpuImage
    {
        public MockImage(GpuImageDescription desc) : base(desc) { }
        public override void Upload(ReadOnlySpan<byte> pixels) { }
    }
}
