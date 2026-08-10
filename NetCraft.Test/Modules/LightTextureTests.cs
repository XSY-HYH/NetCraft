using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//LightTextureTests 光照贴图单元测试
//覆盖 PackLightCoords/UnpackBlockLight/UnpackSkyLight 打包解包
//ComputePixel gamma 校正 GeneratePixels 16x16 像素生成
//FullBrightCoords 全亮常量供实体顶点 light 属性默认值
public class LightTextureTests
{
    public const string Module = "lighttexture";

    public static IEnumerable<(string Name, Func<bool> Test)> All() => Tests();

    public static IEnumerable<(string, Func<bool>)> Tests()
    {
        yield return ("FullBrightCoords 常量 0x00F000F0", TestFullBrightCoords);
        yield return ("PackLightCoords(15,15) 等于 FullBrightCoords", TestPackFullBright);
        yield return ("PackLightCoords(0,0) 等于 0", TestPackZero);
        yield return ("UnpackBlockLight 解包 block 光等级", TestUnpackBlockLight);
        yield return ("UnpackSkyLight 解包 sky 光等级", TestUnpackSkyLight);
        yield return ("Pack/Unpack 往返一致", TestPackUnpackRoundTrip);
        yield return ("PackLightCoords 截断超范围输入到 0-15", TestPackClampsInput);
        yield return ("ComputePixel(15,15) 满亮度返回 255", TestComputePixelFullBright);
        yield return ("ComputePixel(0,0) 零亮度返回 0", TestComputePixelZero);
        yield return ("ComputePixel gamma 校正中间值", TestComputePixelGamma);
        yield return ("ComputePixel block 主导 sky 为 0", TestComputePixelBlockDominant);
        yield return ("ComputePixel skyBrightness 衰减", TestComputePixelSkyBrightness);
        yield return ("GeneratePixels 尺寸 16x16x4", TestGeneratePixelsSize);
        yield return ("GeneratePixels alpha 全 255", TestGeneratePixelsAlpha);
        yield return ("GeneratePixels 满亮度位置纯白", TestGeneratePixelsFullBrightPixel);
        yield return ("GeneratePixels 零亮度位置纯黑", TestGeneratePixelsZeroPixel);
    }

    private static bool TestFullBrightCoords()
    {
        //block=15 sky=15 packed = (15<<4)|(15<<20) = 0xF0 | 0xF00000 = 0x00F000F0
        return LightTexture.FullBrightCoords == 0x00F000F0;
    }

    private static bool TestPackFullBright()
    {
        return LightTexture.PackLightCoords(15, 15) == LightTexture.FullBrightCoords;
    }

    private static bool TestPackZero()
    {
        return LightTexture.PackLightCoords(0, 0) == 0;
    }

    private static bool TestUnpackBlockLight()
    {
        return LightTexture.UnpackBlockLight(LightTexture.FullBrightCoords) == 15;
    }

    private static bool TestUnpackSkyLight()
    {
        return LightTexture.UnpackSkyLight(LightTexture.FullBrightCoords) == 15;
    }

    private static bool TestPackUnpackRoundTrip()
    {
        for (int b = 0; b <= 15; b++)
        {
            for (int s = 0; s <= 15; s++)
            {
                var packed = LightTexture.PackLightCoords(b, s);
                if (LightTexture.UnpackBlockLight(packed) != b) return false;
                if (LightTexture.UnpackSkyLight(packed) != s) return false;
            }
        }
        return true;
    }

    private static bool TestPackClampsInput()
    {
        //输入超 15 按 & 0xF 截断 16→0 17→1
        var packed = LightTexture.PackLightCoords(16, 17);
        return LightTexture.UnpackBlockLight(packed) == 0
            && LightTexture.UnpackSkyLight(packed) == 1;
    }

    private static bool TestComputePixelFullBright()
    {
        var (r, g, b) = LightTexture.ComputePixel(15, 15);
        return r == 255 && g == 255 && b == 255;
    }

    private static bool TestComputePixelZero()
    {
        var (r, g, b) = LightTexture.ComputePixel(0, 0);
        return r == 0 && g == 0 && b == 0;
    }

    private static bool TestComputePixelGamma()
    {
        //block=8 sky=0 gamma 校正后 0.533^0.4≈0.78 → 约 199
        var (r, g, b) = LightTexture.ComputePixel(8, 0);
        return r == g && g == b
            && r > 150 && r < 220;
    }

    private static bool TestComputePixelBlockDominant()
    {
        //block=10 sky=0 与 block=10 sky=10 取 max 应相等
        var (r1, g1, b1) = LightTexture.ComputePixel(10, 0);
        var (r2, g2, b2) = LightTexture.ComputePixel(10, 10);
        return r1 == r2 && g1 == g2 && b1 == b2;
    }

    private static bool TestComputePixelSkyBrightness()
    {
        //sky=15 skyBrightness=0.5 衰减后 combined=max(0, 0.5)=0.5 弱于满亮 1.0
        var (rFull, _, _) = LightTexture.ComputePixel(0, 15, 1.0f);
        var (rDim, _, _) = LightTexture.ComputePixel(0, 15, 0.5f);
        return rDim < rFull;
    }

    private static bool TestGeneratePixelsSize()
    {
        var pixels = LightTexture.GeneratePixels();
        return pixels.Length == LightTexture.Size * LightTexture.Size * 4;
    }

    private static bool TestGeneratePixelsAlpha()
    {
        var pixels = LightTexture.GeneratePixels();
        for (int i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 255) return false;
        return true;
    }

    private static bool TestGeneratePixelsFullBrightPixel()
    {
        var pixels = LightTexture.GeneratePixels();
        //位置 (x=15, y=15) 即 block=15 sky=15 idx=(15*16+15)*4=1020
        var idx = (15 * LightTexture.Size + 15) * 4;
        return pixels[idx] == 255 && pixels[idx + 1] == 255 && pixels[idx + 2] == 255;
    }

    private static bool TestGeneratePixelsZeroPixel()
    {
        var pixels = LightTexture.GeneratePixels();
        //位置 (x=0, y=0) 即 block=0 sky=0 idx=0
        return pixels[0] == 0 && pixels[1] == 0 && pixels[2] == 0;
    }
}
