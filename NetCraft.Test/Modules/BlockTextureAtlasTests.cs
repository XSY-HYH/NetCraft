using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//BlockTextureAtlasTests 方块纹理图集单元测试
//覆盖 TextureStitcher shelf 打包算法：单 sprite/多 sprite 行/换行/尺寸倍增/装不下
//覆盖 TextureAtlasSprite UV 映射正确性
//纯算法测试不依赖 GpuDevice
internal static class BlockTextureAtlasTests
{
    public const string Module = "blocktextureatlas";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("TextureStitcher single sprite returns 16x16 atlas", TestSingleSprite);
        yield return ("TextureStitcher multiple 16x16 sprites pack in one row", TestMultipleInRow);
        yield return ("TextureStitcher wraps to new row when row full", TestWrapToNewRow);
        yield return ("TextureStitcher doubles atlas size when needed", TestDoublesSize);
        yield return ("TextureStitcher returns false when exceeds max size", TestExceedsMaxSize);
        yield return ("TextureStitcher empty input returns 16x16", TestEmptyInput);
        yield return ("TextureStitcher preserves input order in Placed", TestPreservesOrder);
        yield return ("TextureAtlasSprite UV maps correctly", TestSpriteUV);
        yield return ("TextureAtlasSprite MapU maps local UV to atlas", TestSpriteMapU);
    }

    private static bool TestSingleSprite()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        var sprites = new List<TextureStitcher.SpriteInput>
        {
            new("minecraft:block/stone", 16, 16, new byte[16 * 16 * 4])
        };
        if (!stitcher.Stitch(sprites)) return false;
        return stitcher.AtlasWidth == 16 && stitcher.AtlasHeight == 16;
    }

    private static bool TestMultipleInRow()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        //3 个 16x16 sprite 加 padding 16*3+1*3=51 超过 32 但 64 能装一行
        var sprites = new List<TextureStitcher.SpriteInput>
        {
            new("a", 16, 16, new byte[16 * 16 * 4]),
            new("b", 16, 16, new byte[16 * 16 * 4]),
            new("c", 16, 16, new byte[16 * 16 * 4])
        };
        if (!stitcher.Stitch(sprites)) return false;
        //3 个 16+padding 51 > 32 但 <= 64
        return stitcher.AtlasWidth == 64 && stitcher.AtlasHeight == 64;
    }

    private static bool TestWrapToNewRow()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        //64 宽每个 16+1=17 padding 3 个 51 第 4 个 68>64 换行
        var sprites = new List<TextureStitcher.SpriteInput>();
        for (int i = 0; i < 5; i++)
            sprites.Add(new TextureStitcher.SpriteInput($"s{i}", 16, 16, new byte[16 * 16 * 4]));
        if (!stitcher.Stitch(sprites)) return false;
        if (stitcher.AtlasWidth != 64) return false;
        var placed = stitcher.Placed;
        //前 3 个 y=0 第 4 个换行 y=17 第 5 个同 y=17
        return placed[0].AtlasY == 0
            && placed[1].AtlasY == 0
            && placed[2].AtlasY == 0
            && placed[3].AtlasY == 17
            && placed[4].AtlasY == 17;
    }

    private static bool TestDoublesSize()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        //一个大 32x32 sprite 需要至少 32 尺寸图集
        var sprites = new List<TextureStitcher.SpriteInput>
        {
            new("big", 32, 32, new byte[32 * 32 * 4])
        };
        if (!stitcher.Stitch(sprites)) return false;
        return stitcher.AtlasWidth == 32 && stitcher.AtlasHeight == 32;
    }

    private static bool TestExceedsMaxSize()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 64);
        //sprite 64x64 能装但加 padding 后第二个装不下
        var sprites = new List<TextureStitcher.SpriteInput>
        {
            new("a", 64, 64, new byte[64 * 64 * 4]),
            new("b", 64, 64, new byte[64 * 64 * 4])
        };
        //两个 64x64 128 能装但 maxAtlasSize=64 限制失败
        return stitcher.Stitch(sprites) == false;
    }

    private static bool TestEmptyInput()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        var sprites = new List<TextureStitcher.SpriteInput>();
        if (!stitcher.Stitch(sprites)) return false;
        return stitcher.AtlasWidth == 16 && stitcher.AtlasHeight == 16;
    }

    private static bool TestPreservesOrder()
    {
        var stitcher = new TextureStitcher(maxAtlasSize: 256);
        var sprites = new List<TextureStitcher.SpriteInput>
        {
            new("first", 16, 16, new byte[16 * 16 * 4]),
            new("second", 16, 16, new byte[16 * 16 * 4]),
            new("third", 16, 16, new byte[16 * 16 * 4])
        };
        if (!stitcher.Stitch(sprites)) return false;
        var placed = stitcher.Placed;
        return placed[0].Name == "first"
            && placed[1].Name == "second"
            && placed[2].Name == "third";
    }

    private static bool TestSpriteUV()
    {
        //512x512 图集 sprite 在 (16,32) 16x16
        var sprite = new TextureAtlasSprite("minecraft:block/stone", 16, 32, 16, 16, 512, 512, null);
        //U0=16/512 V0=32/512 U1=32/512 V1=48/512
        return Math.Abs(sprite.U0 - 16f / 512f) < 1e-6f
            && Math.Abs(sprite.V0 - 32f / 512f) < 1e-6f
            && Math.Abs(sprite.U1 - 32f / 512f) < 1e-6f
            && Math.Abs(sprite.V1 - 48f / 512f) < 1e-6f;
    }

    private static bool TestSpriteMapU()
    {
        //512x512 图集 sprite 在 (0,0) 16x16
        var sprite = new TextureAtlasSprite("minecraft:block/stone", 0, 0, 16, 16, 512, 512, null);
        //MapU(0)=U0=0 MapU(1)=U1=16/512 MapV(0.5)=8/512
        return Math.Abs(sprite.MapU(0f) - 0f) < 1e-6f
            && Math.Abs(sprite.MapU(1f) - 16f / 512f) < 1e-6f
            && Math.Abs(sprite.MapV(0.5f) - 8f / 512f) < 1e-6f;
    }
}
