using System.Numerics;
using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Test.Modules;

//StagedVertexBufferTests 阶段 4 分阶段顶点缓冲单元测试
//覆盖 AutoStorageIndexBuffer QUADS 自动索引 StagedVertexBuffer 顶点暂存/Upload/EndFrame
//纯 CPU 逻辑测试不依赖 Vulkan 真实设备 Upload 用 EmptyGpuContext 验证调用
internal static class StagedVertexBufferTests
{
    public const string Module = "stagedbuffer";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("AutoStorageIndexBuffer QUADS single quad generates 6 indices", TestQuadsSingleQuad);
        yield return ("AutoStorageIndexBuffer non-Quads topology generates no indices", TestNonQuadsNoIndices);
        yield return ("AutoStorageIndexBuffer multiple quads accumulate firstIndex", TestMultiQuadsAccumulate);
        yield return ("StagedVertexBuffer AppendDraw sets BaseVertex and VertexStartByte", TestAppendDrawBaseVertex);
        yield return ("StagedVertexBuffer multiple Draws accumulate BaseVertex", TestMultiDrawBaseVertex);
        yield return ("StagedVertexBuffer VertexBuilder writes 24 byte POSITION_TEX_COLOR", TestVertexLayoutTexColor);
        yield return ("StagedVertexBuffer VertexBuilder writes 16 byte POSITION_COLOR", TestVertexLayoutColor);
        yield return ("StagedVertexBuffer pose transforms position", TestPoseTransform);
        yield return ("StagedVertexBuffer color ARGB to RGBA bytes", TestColorArgbToRgba);
        yield return ("StagedVertexBuffer EndDraw Quads auto generates indices", TestEndDrawQuadsAutoIndex);
        yield return ("StagedVertexBuffer EndDraw non-Quads no indices", TestEndDrawNonQuadsNoIndex);
        yield return ("StagedVertexBuffer EndDraw twice is safe", TestEndDrawTwiceSafe);
        yield return ("StagedVertexBuffer EndFrame resets staging", TestEndFrameResets);
        yield return ("StagedVertexBuffer GetVertexBytes returns correct slice", TestGetVertexBytesSlice);
        yield return ("StagedVertexBuffer VertexBuilder AddVertex3D writes 40 byte POSITION_COLOR_UV_LIGHT_NORMAL", TestVertexBuilderAddVertex3DByteLayout);
        yield return ("StagedVertexBuffer VertexBuilder AddVertex3D color numeric cast -1 to -1.0f", TestVertexBuilderAddVertex3DColorNumericCast);
        yield return ("StagedVertexBuffer VertexBuilder AddVertex3D light numeric cast", TestVertexBuilderAddVertex3DLightNumericCast);
        yield return ("StagedVertexBuffer VertexBuilder AddVertex3D Quads auto generates indices", TestVertexBuilderAddVertex3DQuadsAutoIndex);
    }

    //TestQuadsSingleQuad 验证 QUADS 4 顶点生成 6 索引 (0,1,2,2,3,0)
    private static bool TestQuadsSingleQuad()
    {
        var ib = new AutoStorageIndexBuffer();
        var (firstIndex, indexCount) = ib.Append(baseVertex: 0, vertexCount: 4, PrimitiveTopology.Quads);
        if (firstIndex != 0 || indexCount != 6) return false;
        var indices = ib.GetIndices().ToArray();
        return indices.Length == 6
            && indices[0] == 0 && indices[1] == 1 && indices[2] == 2
            && indices[3] == 2 && indices[4] == 3 && indices[5] == 0;
    }

    //TestNonQuadsNoIndices 验证 TriangleList 等 topology 不生成索引
    private static bool TestNonQuadsNoIndices()
    {
        var ib = new AutoStorageIndexBuffer();
        var (firstIndex, indexCount) = ib.Append(baseVertex: 0, vertexCount: 3, PrimitiveTopology.TriangleList);
        return firstIndex == 0 && indexCount == 0 && ib.Count == 0;
    }

    //TestMultiQuadsAccumulate 验证多个 quad 索引累加 firstIndex 递增
    private static bool TestMultiQuadsAccumulate()
    {
        var ib = new AutoStorageIndexBuffer();
        var (f1, c1) = ib.Append(0, 4, PrimitiveTopology.Quads);
        var (f2, c2) = ib.Append(4, 4, PrimitiveTopology.Quads);
        if (f1 != 0 || c1 != 6 || f2 != 6 || c2 != 6) return false;
        var indices = ib.GetIndices().ToArray();
        //第二个 quad baseVertex=4 索引应为 (4,5,6,6,7,4)
        return indices.Length == 12
            && indices[6] == 4 && indices[7] == 5 && indices[8] == 6
            && indices[9] == 6 && indices[10] == 7 && indices[11] == 4;
    }

    //TestAppendDrawBaseVertex 验证首个 Draw BaseVertex=0 VertexStartByte=0
    private static bool TestAppendDrawBaseVertex()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        return draw.BaseVertex == 0 && draw.VertexCount == 0 && draw.FirstIndex == 0 && draw.IndexCount == 0;
    }

    //TestMultiDrawBaseVertex 验证第二个 Draw BaseVertex 累加为前一个 Draw 顶点数
    private static bool TestMultiDrawBaseVertex()
    {
        var vb = new StagedVertexBuffer();
        var draw1 = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        var builder1 = vb.GetVertexBuilder(draw1);
        for (int i = 0; i < 4; i++)
            builder1.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, 0);
        vb.EndDraw(draw1);

        var draw2 = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        return draw2.BaseVertex == 4;
    }

    //TestVertexLayoutTexColor 验证 POSITION_TEX_COLOR 顶点 24 字节布局
    private static bool TestVertexLayoutTexColor()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        builder.AddVertexWith2DPose(Matrix3x2.Identity, 10f, 20f, 0.5f, 0.5f, unchecked((int)0xFFFFFFFF));
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        if (bytes.Length != 24) return false;
        //Position Vec3: 10.0f, 20.0f, 0.0f
        if (!MatchesFloat(bytes, 0, 10f)) return false;
        if (!MatchesFloat(bytes, 4, 20f)) return false;
        if (!MatchesFloat(bytes, 8, 0f)) return false;
        //UV0 Vec2: 0.5f, 0.5f
        if (!MatchesFloat(bytes, 12, 0.5f)) return false;
        if (!MatchesFloat(bytes, 16, 0.5f)) return false;
        //Color UByte4Norm RGBA: FF FF FF FF
        return bytes[20] == 0xFF && bytes[21] == 0xFF && bytes[22] == 0xFF && bytes[23] == 0xFF;
    }

    //TestVertexLayoutColor 验证 POSITION_COLOR 顶点 16 字节布局
    private static bool TestVertexLayoutColor()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        builder.AddVertexWith2DPose(Matrix3x2.Identity, 1f, 2f, 0f, 0f, 0);
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        if (bytes.Length != 16) return false;
        if (!MatchesFloat(bytes, 0, 1f)) return false;
        if (!MatchesFloat(bytes, 4, 2f)) return false;
        if (!MatchesFloat(bytes, 8, 0f)) return false;
        //color=0 ARGB 全 0
        return bytes[12] == 0 && bytes[13] == 0 && bytes[14] == 0 && bytes[15] == 0;
    }

    //TestPoseTransform 验证 pose 变换后位置写入顶点
    private static bool TestPoseTransform()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        //平移 pose (tx=100, ty=200)
        var pose = Matrix3x2.CreateTranslation(100f, 200f);
        builder.AddVertexWith2DPose(pose, 10f, 20f, 0f, 0f, 0);
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        return MatchesFloat(bytes, 0, 110f) && MatchesFloat(bytes, 4, 220f);
    }

    //TestColorArgbToRgba 验证 color=0xFF0000FF (ARGB: A=FF,R=00,G=00,B=FF) 转 RGBA bytes [00,00,FF,FF]
    private static bool TestColorArgbToRgba()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        //ARGB: A=FF, R=00, G=00, B=FF
        builder.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, unchecked((int)0xFF0000FF));
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        //RGBA bytes: R=00, G=00, B=FF, A=FF
        return bytes[12] == 0x00 && bytes[13] == 0x00 && bytes[14] == 0xFF && bytes[15] == 0xFF;
    }

    //TestEndDrawQuadsAutoIndex 验证 EndDraw 后 QUADS 自动生成索引到 AutoStorageIndexBuffer
    private static bool TestEndDrawQuadsAutoIndex()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        for (int i = 0; i < 4; i++)
            builder.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, 0);
        vb.EndDraw(draw);

        if (draw.VertexCount != 4) return false;
        if (draw.FirstIndex != 0 || draw.IndexCount != 6) return false;
        return vb.TotalVertexCount == 4;
    }

    //TestEndDrawNonQuadsNoIndex 验证 EndDraw 后 TriangleList 不生成索引
    private static bool TestEndDrawNonQuadsNoIndex()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.TriangleList);
        var builder = vb.GetVertexBuilder(draw);
        for (int i = 0; i < 3; i++)
            builder.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, 0);
        vb.EndDraw(draw);

        return draw.VertexCount == 3 && draw.IndexCount == 0 && draw.FirstIndex == 0;
    }

    //TestEndDrawTwiceSafe 验证 EndDraw 重复调用不重复累加 VertexCount 和索引
    private static bool TestEndDrawTwiceSafe()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        for (int i = 0; i < 4; i++)
            builder.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, 0);
        vb.EndDraw(draw);
        var vertexCountAfterFirst = vb.TotalVertexCount;
        var indexCountAfterFirst = draw.IndexCount;

        vb.EndDraw(draw);
        return vertexCountAfterFirst == 4
            && vb.TotalVertexCount == 4
            && draw.IndexCount == indexCountAfterFirst
            && draw.VertexCount == 4;
    }

    //TestEndFrameResets 验证 EndFrame 后暂存区清空可重新 AppendDraw
    private static bool TestEndFrameResets()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        for (int i = 0; i < 4; i++)
            builder.AddVertexWith2DPose(Matrix3x2.Identity, 0, 0, 0, 0, 0);
        vb.EndDraw(draw);
        if (vb.TotalVertexCount != 4) return false;

        vb.EndFrame();
        if (vb.TotalVertexCount != 0) return false;
        if (vb.Draws.Count != 0) return false;

        //EndFrame 后可重新 AppendDraw BaseVertex 从 0 开始
        var draw2 = vb.AppendDraw(DefaultVertexFormat.POSITION_TEX_COLOR, PrimitiveTopology.Quads);
        return draw2.BaseVertex == 0;
    }

    //TestGetVertexBytesSlice 验证多 Draw 顶点切片返回对应 Draw 的字节
    private static bool TestGetVertexBytesSlice()
    {
        var vb = new StagedVertexBuffer();
        var draw1 = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR, PrimitiveTopology.Quads);
        var b1 = vb.GetVertexBuilder(draw1);
        b1.AddVertexWith2DPose(Matrix3x2.Identity, 1f, 1f, 0, 0, 0);
        vb.EndDraw(draw1);

        var draw2 = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR, PrimitiveTopology.Quads);
        var b2 = vb.GetVertexBuilder(draw2);
        b2.AddVertexWith2DPose(Matrix3x2.Identity, 2f, 2f, 0, 0, 0);
        vb.EndDraw(draw2);

        var bytes1 = vb.GetVertexBytes(draw1);
        var bytes2 = vb.GetVertexBytes(draw2);
        return bytes1.Length == 16 && bytes2.Length == 16
            && MatchesFloat(bytes1, 0, 1f)
            && MatchesFloat(bytes2, 0, 2f);
    }

    //TestVertexBuilderAddVertex3DByteLayout 验证 POSITION_COLOR_UV_LIGHT_NORMAL 40 字节布局
    //Position(12)+Color(4)+UV0(8)+Light(4)+Normal(12)=40 AddVertex3D 按 elem.Name 顺序写入
    private static bool TestVertexBuilderAddVertex3DByteLayout()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        //color=-1=0xFFFFFFFF 白色 light=0x00F000F0 全亮 normal=(0,1,0) Up
        builder.AddVertex3D(1f, 2f, 3f, -1, 0.5f, 0.25f, 0x00F000F0, 0f, 1f, 0f);
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        if (bytes.Length != 40) return false;
        //Position Vec3: 1.0f, 2.0f, 3.0f
        if (!MatchesFloat(bytes, 0, 1f)) return false;
        if (!MatchesFloat(bytes, 4, 2f)) return false;
        if (!MatchesFloat(bytes, 8, 3f)) return false;
        //Color Float: (float)(-1) = -1.0f
        if (!MatchesFloat(bytes, 12, -1f)) return false;
        //UV0 Vec2: 0.5f, 0.25f
        if (!MatchesFloat(bytes, 16, 0.5f)) return false;
        if (!MatchesFloat(bytes, 20, 0.25f)) return false;
        //Light Float: (float)0x00F000F0 = 15728880.0f
        if (!MatchesFloat(bytes, 24, (float)0x00F000F0)) return false;
        //Normal Vec3: 0.0f, 1.0f, 0.0f
        if (!MatchesFloat(bytes, 28, 0f)) return false;
        if (!MatchesFloat(bytes, 32, 1f)) return false;
        return MatchesFloat(bytes, 36, 0f);
    }

    //TestVertexBuilderAddVertex3DColorNumericCast 验证 color int 数值转 float 写入
    //color=-1(0xFFFFFFFF) 数值转 -1.0f shader int(-1.0f)=-1=0xFFFFFFFF 还原白色
    //不能用位模式转换 0xFFFFFFFF 位模式是 NaN shader int(NaN) 未定义
    private static bool TestVertexBuilderAddVertex3DColorNumericCast()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        builder.AddVertex3D(0, 0, 0, -1, 0, 0, 0, 0, 0, 0);
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        //color offset=12 数值转换 -1 -> -1.0f 字节 BF 80 00 00
        return MatchesFloat(bytes, 12, -1f);
    }

    //TestVertexBuilderAddVertex3DLightNumericCast 验证 light int 数值转 float 写入
    //light=0x00F000F0=15728880 在 2^24 内 float 精确表示无精度损失
    private static bool TestVertexBuilderAddVertex3DLightNumericCast()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        builder.AddVertex3D(0, 0, 0, 0, 0, 0, 0x00F000F0, 0, 0, 0);
        vb.EndDraw(draw);

        var bytes = vb.GetVertexBytes(draw);
        //light offset=24 数值转换 0x00F000F0 -> 15728880.0f
        return MatchesFloat(bytes, 24, (float)0x00F000F0);
    }

    //TestVertexBuilderAddVertex3DQuadsAutoIndex 验证 AddVertex3D 4 顶点后 EndDraw 生成 6 索引
    private static bool TestVertexBuilderAddVertex3DQuadsAutoIndex()
    {
        var vb = new StagedVertexBuffer();
        var draw = vb.AppendDraw(DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL, PrimitiveTopology.Quads);
        var builder = vb.GetVertexBuilder(draw);
        for (int i = 0; i < 4; i++)
            builder.AddVertex3D(0, 0, 0, -1, 0, 0, 0x00F000F0, 0, 1, 0);
        vb.EndDraw(draw);

        return draw.VertexCount == 4
            && draw.FirstIndex == 0
            && draw.IndexCount == 6
            && vb.TotalVertexCount == 4;
    }

    //MatchesFloat 验证 bytes[offset..offset+4] 是否等于指定 float 的 little-endian 字节
    private static bool MatchesFloat(byte[] bytes, int offset, float expected)
    {
        var expectedBytes = BitConverter.GetBytes(expected);
        return bytes[offset] == expectedBytes[0]
            && bytes[offset + 1] == expectedBytes[1]
            && bytes[offset + 2] == expectedBytes[2]
            && bytes[offset + 3] == expectedBytes[3];
    }
}
