using System.Text.Json;
using NetCraft.Gpu;
using NetCraft.Gpu.Sprite;

namespace NetCraft.Test.Modules;

//SpriteScalingTests P0 九宫格 sprite 基础设施测试
//覆盖 GuiSpriteScaling 三模式 + NineSliceBorder 双格式 + GuiMetadataSection 解析
//纯逻辑不依赖 Vulkan 用 System.Text.Json 解析 mock JSON 字符串
internal static class SpriteScalingTests
{
    public const string Module = "sprite";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //A GuiSpriteScaling 默认值与基础类型
        yield return ("Stretch 是默认 scaling", TestStretchDefault);
        yield return ("stretch 显式解析", TestStretchExplicit);
        yield return ("tile 解析 width/height", TestTile);
        yield return ("未知 type 返回 Stretch 默认", TestUnknownTypeFallback);

        //B NineSlice border 双格式
        yield return ("NineSlice 整数 border 解析为 Uniform", TestNineSliceBorderInt);
        yield return ("NineSlice 对象 border 四边独立", TestNineSliceBorderObject);
        yield return ("NineSlice tab bottom=0 解析", TestNineSliceBorderBottomZero);

        //C stretch_inner 默认与显式
        yield return ("NineSlice stretch_inner 缺省为 false", TestNineSliceStretchInnerDefault);
        yield return ("NineSlice stretch_inner:true 解析为 true", TestNineSliceStretchInnerTrue);

        //D NineSlice 校验逻辑
        yield return ("NineSlice.IsValid 横向 border 之和小于 width", TestNineSliceValidateHorizontal);
        yield return ("NineSlice.IsValid 纵向 border 之和小于 height", TestNineSliceValidateVertical);
        yield return ("NineSlice.IsValid border 越界返回 false", TestNineSliceValidateInvalid);

        //E NineSliceBorder 工具方法
        yield return ("NineSliceBorder.Uniform 四边相同", TestBorderUniform);
        yield return ("NineSliceBorder 非四边相同 IsUniform=false", TestBorderNonUniform);

        //F GuiSpriteManager 缓存与懒加载
        yield return ("GuiSpriteManager.GetSprite 加载并缓存 sprite", TestSpriteManagerLoadSprite);
        yield return ("GuiSpriteManager.GetSprite 缓存命中不重复加载", TestSpriteManagerCacheHit);
        yield return ("GuiSpriteManager.GetSprite 不存在返回 null", TestSpriteManagerMissingReturnsNull);
        yield return ("GuiSpriteManager.ClearCache 清空后重新加载", TestSpriteManagerClearCache);
    }

    //TestableSpriteManager 测试用派生类覆盖 LoadSprite 避免真实文件 IO 和 GpuDevice 依赖
    //LoadCallCount 统计 LoadSprite 调用次数验证缓存命中
    private sealed class TestableSpriteManager : GuiSpriteManager
    {
        public int LoadCallCount;
        private readonly Func<string, GuiSprite?> _loader;

        public TestableSpriteManager(Func<string, GuiSprite?> loader)
            : base(string.Empty, null!)
        {
            _loader = loader;
        }

        protected override GuiSprite? LoadSprite(string identifier)
        {
            LoadCallCount++;
            return _loader(identifier);
        }
    }

    //MakeMockSprite 构造测试用 GuiSprite 用 NoTexture 占位避免依赖 GpuImage
    private static GuiSprite MakeMockSprite(string identifier)
    {
        var scaling = new NineSliceScaling(200, 20, NineSliceBorder.Uniform(3), false);
        return new GuiSprite(1, TextureSetup.NoTexture, 200, 20, scaling);
    }

    //F1 TestSpriteManagerLoadSprite GetSprite 加载并缓存 sprite
    private static bool TestSpriteManagerLoadSprite()
    {
        var mgr = new TestableSpriteManager(MakeMockSprite);
        var sprite = mgr.GetSprite("minecraft:textures/gui/sprites/widget/button");
        return sprite is not null && sprite.Width == 200 && mgr.LoadCallCount == 1;
    }

    //F2 TestSpriteManagerCacheHit 同 identifier 第二次调用命中缓存不重复加载
    private static bool TestSpriteManagerCacheHit()
    {
        var mgr = new TestableSpriteManager(MakeMockSprite);
        mgr.GetSprite("minecraft:widget/button");
        mgr.GetSprite("minecraft:widget/button");
        return mgr.LoadCallCount == 1;
    }

    //F3 TestSpriteManagerMissingReturnsNull 不存在 identifier 返回 null
    private static bool TestSpriteManagerMissingReturnsNull()
    {
        var mgr = new TestableSpriteManager(_ => null);
        return mgr.GetSprite("minecraft:nonexistent") is null;
    }

    //F4 TestSpriteManagerClearCache ClearCache 后重新加载
    private static bool TestSpriteManagerClearCache()
    {
        var mgr = new TestableSpriteManager(MakeMockSprite);
        mgr.GetSprite("minecraft:widget/button");
        mgr.ClearCache();
        mgr.GetSprite("minecraft:widget/button");
        return mgr.LoadCallCount == 2;
    }

    //ParseJson 工具方法解析 JSON 字符串到 JsonElement
    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        //Clone RootElement 避免 using 释放后失效
        return doc.RootElement.Clone();
    }

    //A1 TestStretchDefault 无 gui.scaling 段返回 Stretch 默认值
    private static bool TestStretchDefault()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson("{}"));
        return scaling is StretchScaling;
    }

    //A2 TestStretchExplicit 显式 type=stretch
    private static bool TestStretchExplicit()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson("{\"gui\":{\"scaling\":{\"type\":\"stretch\"}}}"));
        return scaling is StretchScaling;
    }

    //A3 TestTile type=tile 解析 width/height
    private static bool TestTile()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"tile\",\"width\":16,\"height\":16}}}"));
        return scaling is TileScaling tile && tile.Width == 16 && tile.Height == 16;
    }

    //A4 TestUnknownTypeFallback 未知 type 字符串返回默认 Stretch
    private static bool TestUnknownTypeFallback()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"unknown\"}}}"));
        return scaling is StretchScaling;
    }

    //B1 TestNineSliceBorderInt button.png.mcmeta 整数 border=3 解析为 Uniform
    private static bool TestNineSliceBorderInt()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":200,\"height\":20,\"border\":3}}}"));
        if (scaling is not NineSliceScaling ns) return false;
        return ns.Width == 200 && ns.Height == 20
            && ns.Border.Left == 3 && ns.Border.Top == 3
            && ns.Border.Right == 3 && ns.Border.Bottom == 3
            && ns.Border.IsUniform;
    }

    //B2 TestNineSliceBorderObject slider_handle.png.mcmeta 对象 border 四边独立
    private static bool TestNineSliceBorderObject()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":8,\"height\":20," +
            "\"border\":{\"left\":2,\"top\":2,\"right\":2,\"bottom\":3}}}}"));
        if (scaling is not NineSliceScaling ns) return false;
        return ns.Border.Left == 2 && ns.Border.Top == 2
            && ns.Border.Right == 2 && ns.Border.Bottom == 3
            && !ns.Border.IsUniform;
    }

    //B3 TestNineSliceBorderBottomZero tab.png.mcmeta bottom=0 解析
    private static bool TestNineSliceBorderBottomZero()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":130,\"height\":24," +
            "\"border\":{\"left\":2,\"top\":2,\"right\":2,\"bottom\":0}}}}"));
        if (scaling is not NineSliceScaling ns) return false;
        return ns.Border.Bottom == 0 && ns.Border.Left == 2;
    }

    //C1 TestNineSliceStretchInnerDefault 无 stretch_inner 字段默认 false
    private static bool TestNineSliceStretchInnerDefault()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":200,\"height\":20,\"border\":3}}}"));
        return scaling is NineSliceScaling ns && !ns.StretchInner;
    }

    //C2 TestNineSliceStretchInnerTrue 显式 stretch_inner:true 解析为 true
    private static bool TestNineSliceStretchInnerTrue()
    {
        var scaling = GuiMetadataSection.Parse(ParseJson(
            "{\"gui\":{\"scaling\":{\"type\":\"nine_slice\",\"width\":200,\"height\":20," +
            "\"border\":3,\"stretch_inner\":true}}}"));
        return scaling is NineSliceScaling ns && ns.StretchInner;
    }

    //D1 TestNineSliceValidateHorizontal 横向 border 之和 < width 时 IsValid=true
    private static bool TestNineSliceValidateHorizontal()
    {
        var ns = new NineSliceScaling(20, 20, new NineSliceBorder(3, 3, 3, 3), false);
        //3+3=6 < 20
        return ns.IsValid;
    }

    //D2 TestNineSliceValidateVertical 纵向 border 之和 < height 时 IsValid=true
    private static bool TestNineSliceValidateVertical()
    {
        var ns = new NineSliceScaling(20, 20, new NineSliceBorder(3, 5, 3, 5), false);
        //5+5=10 < 20
        return ns.IsValid;
    }

    //D3 TestNineSliceValidateInvalid border 越界返回 false
    private static bool TestNineSliceValidateInvalid()
    {
        //border.left+border.right=20 不小于 width=20 失败
        var ns = new NineSliceScaling(20, 20, new NineSliceBorder(10, 3, 10, 3), false);
        return !ns.IsValid;
    }

    //E1 TestBorderBorder Uniform(3) 四边相同 IsUniform=true
    private static bool TestBorderUniform()
    {
        var b = NineSliceBorder.Uniform(3);
        return b.Left == 3 && b.Top == 3 && b.Right == 3 && b.Bottom == 3 && b.IsUniform;
    }

    //E2 TestBorderNonUniform 四边不同 IsUniform=false
    private static bool TestBorderNonUniform()
    {
        var b = new NineSliceBorder(2, 2, 2, 3);
        return !b.IsUniform;
    }
}
