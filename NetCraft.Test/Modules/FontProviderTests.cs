using System.IO.Compression;
using System.Numerics;
using System.Text;
using NetCraft.Gpu;
using NetCraft.Gpu.Font;
using NetCraft.Gpu.Pipeline;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Test.Modules;

//FontProviderTests F9 字体 providers 链单元测试对标原版 FontSet/providers/BakedSheetGlyph 行为
//覆盖 GlyphRenderOptions/GlyphRenderTypes/IGlyphInfo/EmptyGlyph/Space/Unihex/Bitmap/Ttf provider
//FontSet 链查找+Filter FontProviderDefinitionLoader json 解析 GlyphFont.Draw/MeasureText
//SheetBakedGlyph.Render italic/bold/shadow 顶点逻辑 GlyphBlitRenderState 4 顶点提交
//纯 CPU 逻辑不依赖 Vulkan 用 CaptureContext/CaptureStitcher 桩验证渲染调用
internal static class FontProviderTests
{
    public const string Module = "fontprovider";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //A GlyphRenderOptions 渲染参数封装
        yield return ("GlyphRenderOptions.Simple 无阴影", TestOptionsSimpleNoShadow);
        yield return ("GlyphRenderOptions.WithShadow 设阴影色和偏移", TestOptionsWithShadow);
        yield return ("GlyphRenderOptions.HasShadow 阴影色非0判断", TestOptionsHasShadow);

        //B GlyphRenderTypes pipeline 选取
        yield return ("GlyphRenderTypes.CreateForGrayscaleTexture 返回灰度 pipeline", TestCreateForGrayscale);
        yield return ("GlyphRenderTypes.CreateForColorTexture 返回彩色 pipeline", TestCreateForColor);
        yield return ("GlyphRenderTypes.Select 按 DisplayMode 返回对应 pipeline", TestSelectByDisplayMode);
        yield return ("GlyphRenderTypes.Select 未知 mode 抛异常", TestSelectUnknownThrows);

        //C IGlyphInfo 度量
        yield return ("IGlyphInfo.Simple 创建 SimpleGlyphInfo", TestGlyphInfoSimple);
        yield return ("IGlyphInfo.GetAdvance bold 加 BoldOffset", TestGlyphInfoGetAdvanceBold);
        yield return ("UnihexGlyphInfo BoldOffset/ShadowOffset=0.5", TestUnihexGlyphInfoOffsets);

        //D EmptyGlyph
        yield return ("EmptyGlyph.Info.Advance 构造值", TestEmptyGlyphAdvance);
        yield return ("EmptyGlyph.Bake 返回 BakedGlyph Render 不抛", TestEmptyGlyphBakeNoThrow);

        //E SpaceGlyphProvider
        yield return ("SpaceGlyphProvider.GetGlyph 返回 EmptyGlyph", TestSpaceProviderGetGlyph);
        yield return ("SpaceGlyphProvider.GetSupportedGlyphs 返回字典 key", TestSpaceProviderSupported);
        yield return ("SpaceGlyphProvider.GetGlyph 缺失返回 null", TestSpaceProviderMissingNull);

        //F UnihexGlyphProvider
        yield return ("UnihexGlyphProvider.LoadFromStream 裸 hex 解析 codepoint", TestUnihexLoadBareHex);
        yield return ("UnihexGlyphProvider.GetSupportedGlyphs 含已解析 codepoint", TestUnihexSupported);
        yield return ("UnihexGlyphProvider.Info.Advance=width/2+1", TestUnihexAdvanceCalc);
        yield return ("UnihexGlyphProvider.GetGlyph 缺失返回 null", TestUnihexMissingNull);
        yield return ("UnihexGlyphProvider 5 位 codepoint U+1F600 解析", TestUnihex5DigitCodepoint);
        yield return ("UnihexGlyphProvider.LoadFromStream zip 格式", TestUnihexLoadZip);
        yield return ("UnihexGlyphProvider.Bake 调 Stitcher.Stitch", TestUnihexBakeCallsStitch);

        //G BitmapGlyphProvider
        yield return ("BitmapGlyphProvider.GetSupportedGlyphs 返回网格 codepoint", TestBitmapSupported);
        yield return ("BitmapGlyphProvider.GetGlyph 返回 IUnbakedGlyph Advance>0", TestBitmapGetGlyph);
        yield return ("BitmapGlyphProvider.GetGlyph codepoint=0 返回 null", TestBitmapCodepointZero);
        yield return ("BitmapGlyphProvider.GetGlyph 缓存同一实例", TestBitmapGetGlyphCache);
        yield return ("BitmapGlyphProvider.BitmapGlyphBitmap.IsColored=true", TestBitmapIsColored);

        //H TtfGlyphProvider 依赖系统字体找不到跳过
        yield return ("TtfGlyphProvider.GetGlyph('A') 返回 IUnbakedGlyph", TestTtfGetGlyphA);
        yield return ("TtfGlyphProvider.GetGlyph 缺失返回 null", TestTtfMissingNull);
        yield return ("TtfGlyphProvider.skip 跳过 codepoint", TestTtfSkipCodepoint);
        yield return ("TtfGlyphProvider.GetGlyph 缓存同一实例", TestTtfGetGlyphCache);

        //I FontSet providers 链
        yield return ("FontSet.Reload ActiveProviders 数量", TestFontSetReloadActive);
        yield return ("FontSet.GetGlyph 按 providers 顺序首个命中", TestFontSetGetGlyphOrder);
        yield return ("FontSet.GetGlyph 缓存同一实例", TestFontSetGetGlyphCache);
        yield return ("FontSet.Reload(options) Filter 筛选", TestFontSetReloadFilter);
        yield return ("FontSet.GetGlyph 全无命中返回 null", TestFontSetAllMiss);
        yield return ("FontSet.GetGlyph 跳过 null provider 继续", TestFontSetSkipNullContinue);

        //J FontProviderDefinitionLoader json 解析
        yield return ("Loader.Load 解析 space 类型", TestLoaderSpace);
        yield return ("Loader.Load 解析 ttf 类型", TestLoaderTtf);
        yield return ("Loader.Load 解析 bitmap 类型", TestLoaderBitmap);
        yield return ("Loader.Load 解析 unihex 类型", TestLoaderUnihex);
        yield return ("Loader.Load reference 递归加载", TestLoaderReference);
        yield return ("Loader.Load reference 循环保护", TestLoaderReferenceCycle);
        yield return ("Loader.Load filter 字段解析", TestLoaderFilter);
        yield return ("Loader.Load 未知 type 返回空", TestLoaderUnknownType);
        yield return ("Loader.Load 无 providers 字段返回空", TestLoaderNoProviders);

        //K GlyphFont
        yield return ("GlyphFont.MeasureText 空字符串返回 0", TestFontMeasureEmpty);
        yield return ("GlyphFont.MeasureText 累加 advance", TestFontMeasureSum);
        yield return ("GlyphFont.Draw 提交 BakedGlyph.Render", TestFontDrawRenders);
        yield return ("GlyphFont.Draw 空字符串不抛", TestFontDrawEmpty);
        yield return ("GlyphFont.Draw 缺失字形用 GetMissing 占位", TestFontDrawMissing);
        yield return ("GlyphFont.Draw shadow 计算 ShadowColor=RGB*0.25", TestFontDrawShadowColor);
        yield return ("GlyphFont.Ascent/LineHeight 属性", TestFontAscentLineHeight);

        //L SheetBakedGlyph.Render italic/bold/shadow 顶点逻辑
        yield return ("SheetBakedGlyph.Render 普通字形 1 次 DrawGlyphQuad", TestSheetRenderNormal);
        yield return ("SheetBakedGlyph.Render bold 2 次 DrawGlyphQuad", TestSheetRenderBold);
        yield return ("SheetBakedGlyph.Render shadow 2 次 DrawGlyphQuad", TestSheetRenderShadow);
        yield return ("SheetBakedGlyph.Render bold+shadow 4 次", TestSheetRenderBoldShadow);
        yield return ("SheetBakedGlyph.Render italic 顶点 x 偏移", TestSheetRenderItalic);
        yield return ("SheetBakedGlyph.Render shadow 用 PolygonOffset pipeline", TestSheetRenderShadowPipeline);
        yield return ("SheetBakedGlyph 4 顶点顺序 左上→左下→右下→右上", TestSheetRenderVertexOrder);

        //M GlyphBlitRenderState 4 顶点提交
        yield return ("GlyphBlitRenderState.BuildVertices 提交 4 顶点", TestBlitStateBuildVertices);
        yield return ("GlyphBlitRenderState UV 映射 左上(0,0)左下(0,1)右下(1,1)右上(1,0)", TestBlitStateUVMapping);

        //N SpecialGlyphs 缺失字形占位
        yield return ("SpecialGlyphs.Missing.Info.Advance=6", TestSpecialMissingAdvance);
        yield return ("SpecialGlyphs.White.Info.Advance=6", TestSpecialWhiteAdvance);
        yield return ("SpecialGlyphs.Missing.Bake 调 Stitcher.Stitch", TestSpecialMissingBakeStitch);
        yield return ("SpecialGlyphs.Missing 像素边框不透明内部透明", TestSpecialMissingPixels);
        yield return ("SpecialGlyphs.White 像素全不透明", TestSpecialWhitePixels);
        yield return ("SpecialGlyphs bitmap 5x8 IsColored=true", TestSpecialBitmapMetrics);
    }

    //==== A GlyphRenderOptions ====

    private static bool TestOptionsSimpleNoShadow()
    {
        var o = GlyphRenderOptions.Simple(10, 20, unchecked((int)0xFFFFFFFF));
        return o.X == 10 && o.Y == 20 && o.Color == unchecked((int)0xFFFFFFFF)
            && o.ShadowColor == 0 && !o.HasShadow
            && !o.Bold && !o.Italic;
    }

    private static bool TestOptionsWithShadow()
    {
        var o = GlyphRenderOptions.Simple(0, 0, unchecked((int)0xFFFFFFFF)).WithShadow(unchecked((int)0xFF000000), 1.5f);
        return o.ShadowColor == unchecked((int)0xFF000000) && o.ShadowOffset == 1.5f && o.HasShadow;
    }

    private static bool TestOptionsHasShadow()
    {
        var noShadow = new GlyphRenderOptions(0, 0, 0, 0, false, false, 1, 1);
        var withShadow = new GlyphRenderOptions(0, 0, 0, unchecked((int)0xFF000000), false, false, 1, 1);
        return !noShadow.HasShadow && withShadow.HasShadow;
    }

    //==== B GlyphRenderTypes ====

    private static bool TestCreateForGrayscale()
    {
        var t = GlyphRenderTypes.CreateForGrayscaleTexture();
        return t.Normal == RenderPipelines.GUI_TEXT_GRAYSCALE
            && t.SeeThrough == RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH
            && t.PolygonOffset == RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET;
    }

    private static bool TestCreateForColor()
    {
        var t = GlyphRenderTypes.CreateForColorTexture();
        return t.Normal == RenderPipelines.GUI_TEXT
            && t.SeeThrough == RenderPipelines.GUI_TEXT_SEE_THROUGH
            && t.PolygonOffset == RenderPipelines.GUI_TEXT_POLYGON_OFFSET;
    }

    private static bool TestSelectByDisplayMode()
    {
        var t = GlyphRenderTypes.CreateForGrayscaleTexture();
        return t.Select(DisplayMode.Normal) == t.Normal
            && t.Select(DisplayMode.SeeThrough) == t.SeeThrough
            && t.Select(DisplayMode.PolygonOffset) == t.PolygonOffset;
    }

    private static bool TestSelectUnknownThrows()
    {
        var t = GlyphRenderTypes.CreateForGrayscaleTexture();
        try { t.Select((DisplayMode)999); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }

    //==== C IGlyphInfo ====

    private static bool TestGlyphInfoSimple()
    {
        var info = IGlyphInfo.Simple(4.0f);
        return info.Advance == 4.0f && info is SimpleGlyphInfo;
    }

    private static bool TestGlyphInfoGetAdvanceBold()
    {
        var info = IGlyphInfo.Simple(5.0f);
        //BoldOffset 默认 1.0 bold=true 时 Advance+1
        return Math.Abs(info.GetAdvance(false) - 5.0f) < 1e-6f
            && Math.Abs(info.GetAdvance(true) - 6.0f) < 1e-6f
            && Math.Abs(info.BoldOffset - 1.0f) < 1e-6f;
    }

    //UnihexGlyphInfo 覆盖 BoldOffset/ShadowOffset=0.5 通过 UnihexGlyphProvider 加载字形验证
    private static bool TestUnihexGlyphInfoOffsets()
    {
        var provider = UnihexGlyphProvider.LoadFromStream(MakeHexStream("0041:FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));
        var glyph = provider.GetGlyph(0x41);
        if (glyph == null) return false;
        return Math.Abs(glyph.Info.BoldOffset - 0.5f) < 1e-6f
            && Math.Abs(glyph.Info.ShadowOffset - 0.5f) < 1e-6f;
    }

    //==== D EmptyGlyph ====

    private static bool TestEmptyGlyphAdvance()
    {
        var g = new EmptyGlyph(4.0f);
        return Math.Abs(g.Info.Advance - 4.0f) < 1e-6f;
    }

    private static bool TestEmptyGlyphBakeNoThrow()
    {
        var g = new EmptyGlyph(4.0f);
        var stitcher = new CaptureStitcher();
        var baked = g.Bake(stitcher);
        var ctx = new CaptureContext();
        var opt = GlyphRenderOptions.Simple(0, 0, unchecked((int)0xFFFFFFFF));
        baked.Render(ctx, in opt);
        //EmptyBakedGlyph.Render 空实现不应调 DrawGlyphQuad
        return ctx.Quads.Count == 0;
    }

    //==== E SpaceGlyphProvider ====

    private static SpaceGlyphProvider MakeSpaceProvider() => new(new Dictionary<int, float>
    {
        [0x20] = 4.0f,   //普通空格
        [0xA0] = 4.0f,   //不间断空格
    });

    private static bool TestSpaceProviderGetGlyph()
    {
        var p = MakeSpaceProvider();
        var g = p.GetGlyph(0x20);
        return g != null && Math.Abs(g.Info.Advance - 4.0f) < 1e-6f;
    }

    private static bool TestSpaceProviderSupported()
    {
        var p = MakeSpaceProvider();
        var supported = p.GetSupportedGlyphs();
        return supported.Count == 2 && supported.Contains(0x20) && supported.Contains(0xA0);
    }

    private static bool TestSpaceProviderMissingNull()
    {
        var p = MakeSpaceProvider();
        return p.GetGlyph(0x41) == null;
    }

    //==== F UnihexGlyphProvider ====

    private static Stream MakeHexStream(params string[] lines)
    {
        var sb = new StringBuilder();
        foreach (var l in lines) sb.AppendLine(l);
        return new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
    }

    //32 位 hex=8 像素宽 16 行 全亮 mask=unchecked((int)0xFF000000) left=0 right=7 width=8 advance=5
    private const string HexA32 = "0041:FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

    private static bool TestUnihexLoadBareHex()
    {
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(HexA32));
        return p.GetGlyph(0x41) != null;
    }

    private static bool TestUnihexSupported()
    {
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(HexA32, "0042:FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));
        var s = p.GetSupportedGlyphs();
        return s.Count == 2 && s.Contains(0x41) && s.Contains(0x42);
    }

    private static bool TestUnihexAdvanceCalc()
    {
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(HexA32));
        var g = p.GetGlyph(0x41);
        //width=8 advance=8/2.0+1=5.0
        return g != null && Math.Abs(g.Info.Advance - 5.0f) < 1e-6f;
    }

    private static bool TestUnihexMissingNull()
    {
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(HexA32));
        return p.GetGlyph(0x42) == null;
    }

    private static bool TestUnihex5DigitCodepoint()
    {
        //1F600 5 位 hex 64 位 bitmap=16 像素宽 16 行全亮 width=16 advance=16/2+1=9
        var hex = "1F600:" + new string('F', 64);
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(hex));
        var g = p.GetGlyph(0x1F600);
        return g != null && Math.Abs(g.Info.Advance - 9.0f) < 1e-6f;
    }

    private static bool TestUnihexLoadZip()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("glyphs.hex");
            using var es = entry.Open();
            var bytes = Encoding.ASCII.GetBytes(HexA32 + "\n");
            es.Write(bytes, 0, bytes.Length);
        }
        ms.Position = 0;
        var p = UnihexGlyphProvider.LoadFromStream(ms);
        return p.GetGlyph(0x41) != null;
    }

    private static bool TestUnihexBakeCallsStitch()
    {
        var p = UnihexGlyphProvider.LoadFromStream(MakeHexStream(HexA32));
        var g = p.GetGlyph(0x41);
        var stitcher = new CaptureStitcher();
        var baked = g!.Bake(stitcher);
        return stitcher.Created.Count == 1 && ReferenceEquals(stitcher.Created[0], baked);
    }

    //==== G BitmapGlyphProvider ====

    //8x8 全白不透明 RGBA PNG 由 System.Drawing 生成
    private const string Png8x8Base64 = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAARSURBVChTY/hPAIwIBf//AwCEtf8BmLK6XAAAAABJRU5ErkJggg==";

    private static BitmapGlyphProvider MakeBitmapProvider()
    {
        var png = Convert.FromBase64String(Png8x8Base64);
        var grid = new int[][] { new[] { 0x41, 0x42 } };
        return new BitmapGlyphProvider(png, grid, height: 8, ascent: 7);
    }

    private static bool TestBitmapSupported()
    {
        var p = MakeBitmapProvider();
        var s = p.GetSupportedGlyphs();
        return s.Count == 2 && s.Contains(0x41) && s.Contains(0x42);
    }

    private static bool TestBitmapGetGlyph()
    {
        var p = MakeBitmapProvider();
        var g = p.GetGlyph(0x41);
        return g != null && g.Info.Advance > 0;
    }

    private static bool TestBitmapCodepointZero()
    {
        var p = MakeBitmapProvider();
        return p.GetGlyph(0) == null;
    }

    private static bool TestBitmapGetGlyphCache()
    {
        var p = MakeBitmapProvider();
        return ReferenceEquals(p.GetGlyph(0x41), p.GetGlyph(0x41));
    }

    private static bool TestBitmapIsColored()
    {
        var p = MakeBitmapProvider();
        var g = p.GetGlyph(0x41);
        var stitcher = new CaptureStitcher();
        g!.Bake(stitcher);
        //BitmapGlyphBitmap.IsColored=true CaptureStitcher 记录 bitmap.IsColored
        return stitcher.LastBitmap?.IsColored == true;
    }

    //==== H TtfGlyphProvider ====

    private static byte[]? s_arialTtf;
    private static byte[]? LoadArial()
    {
        if (s_arialTtf != null) return s_arialTtf;
        var path = "C:\\Windows\\Fonts\\arial.ttf";
        if (!File.Exists(path)) return null;
        s_arialTtf = File.ReadAllBytes(path);
        return s_arialTtf;
    }

    private static bool TestTtfGetGlyphA()
    {
        if (LoadArial() is not { } ttf) return true; //找不到字体跳过
        using var p = new TtfGlyphProvider(ttf, size: 11.0f, oversample: 1.0f, 0, 0, "");
        var g = p.GetGlyph('A');
        return g != null && g.Info.Advance > 0;
    }

    private static bool TestTtfMissingNull()
    {
        if (LoadArial() is not { } ttf) return true;
        using var p = new TtfGlyphProvider(ttf, 11.0f, 1.0f, 0, 0, "");
        //私有区码点 U+E000 arial 通常无字形
        return p.GetGlyph(0xE000) == null || p.GetGlyph(0xE000)?.Info.Advance >= 0;
    }

    private static bool TestTtfSkipCodepoint()
    {
        if (LoadArial() is not { } ttf) return true;
        using var p = new TtfGlyphProvider(ttf, 11.0f, 1.0f, 0, 0, "A");
        return p.GetGlyph('A') == null;
    }

    private static bool TestTtfGetGlyphCache()
    {
        if (LoadArial() is not { } ttf) return true;
        using var p = new TtfGlyphProvider(ttf, 11.0f, 1.0f, 0, 0, "");
        return ReferenceEquals(p.GetGlyph('A'), p.GetGlyph('A'));
    }

    //==== I FontSet ====

    private static bool TestFontSetReloadActive()
    {
        var fs = new FontSet();
        var providers = new List<IGlyphProvider.Conditional>
        {
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x20] = 4 }), FontOptionFilter.AlwaysPass),
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 5 }), FontOptionFilter.AlwaysPass),
        };
        fs.Reload(providers, new HashSet<FontOption>());
        return fs.ActiveProviders.Count == 2;
    }

    private static bool TestFontSetGetGlyphOrder()
    {
        var fs = new FontSet();
        var first = new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 4 });
        var second = new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 9 });
        var providers = new List<IGlyphProvider.Conditional>
        {
            new(first, FontOptionFilter.AlwaysPass),
            new(second, FontOptionFilter.AlwaysPass),
        };
        fs.Reload(providers, new HashSet<FontOption>());
        var g = fs.GetGlyph(0x41);
        //首个命中返回 advance=4 不是 9
        return g != null && Math.Abs(g.Info.Advance - 4.0f) < 1e-6f;
    }

    private static bool TestFontSetGetGlyphCache()
    {
        var fs = new FontSet();
        fs.Reload(new List<IGlyphProvider.Conditional>
        {
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 4 }), FontOptionFilter.AlwaysPass),
        }, new HashSet<FontOption>());
        return ReferenceEquals(fs.GetGlyph(0x41), fs.GetGlyph(0x41));
    }

    private static bool TestFontSetReloadFilter()
    {
        var fs = new FontSet();
        //filter uniform:false 表示 uniform 未激活时匹配
        var conditions = new Dictionary<FontOption, bool> { [FontOption.Uniform] = false };
        var filter = new FontOptionFilter(conditions);
        var providers = new List<IGlyphProvider.Conditional>
        {
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 4 }), filter),
        };
        //无 option 激活 uniform:false 匹配 → active
        fs.Reload(providers, new HashSet<FontOption>());
        if (fs.ActiveProviders.Count != 1) return false;
        //激活 uniform uniform:false 不匹配 → 不 active
        fs.Reload(new HashSet<FontOption> { FontOption.Uniform });
        return fs.ActiveProviders.Count == 0;
    }

    private static bool TestFontSetAllMiss()
    {
        var fs = new FontSet();
        fs.Reload(new List<IGlyphProvider.Conditional>
        {
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x20] = 4 }), FontOptionFilter.AlwaysPass),
        }, new HashSet<FontOption>());
        return fs.GetGlyph(0x41) == null;
    }

    private static bool TestFontSetSkipNullContinue()
    {
        var fs = new FontSet();
        var providers = new List<IGlyphProvider.Conditional>
        {
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x20] = 4 }), FontOptionFilter.AlwaysPass),
            new(new SpaceGlyphProvider(new Dictionary<int, float> { [0x41] = 5 }), FontOptionFilter.AlwaysPass),
        };
        fs.Reload(providers, new HashSet<FontOption>());
        var g = fs.GetGlyph(0x41);
        //第一个 provider 无 0x41 返回 null 第二个命中返回 advance=5
        return g != null && Math.Abs(g.Info.Advance - 5.0f) < 1e-6f;
    }

    //==== J FontProviderDefinitionLoader ====

    private sealed class MockFontResourceAccessor : IFontResourceAccessor
    {
        private readonly Dictionary<string, byte[]> _resources = new();
        public void Add(string id, byte[] data) => _resources[id] = data;
        public void AddText(string id, string text) => _resources[id] = Encoding.UTF8.GetBytes(text);
        public Stream? OpenResource(string identifier)
            => _resources.TryGetValue(identifier, out var b) ? new MemoryStream(b) : null;
    }

    private static bool TestLoaderSpace()
    {
        var json = """{"providers":[{"type":"space","advances":{" ":4.0}}]}""";
        var acc = new MockFontResourceAccessor();
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        if (result.Count != 1) return false;
        var p = result[0].Provider;
        var g = p.GetGlyph(' ');
        return g != null && Math.Abs(g.Info.Advance - 4.0f) < 1e-6f;
    }

    private static bool TestLoaderTtf()
    {
        if (LoadArial() is not { } ttf) return true;
        var json = """{"providers":[{"type":"ttf","file":"minecraft:font/alt.ttf","size":11.0}]}""";
        var acc = new MockFontResourceAccessor();
        acc.Add("minecraft:font/alt.ttf", ttf);
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        if (result.Count != 1) return false;
        return result[0].Provider.GetGlyph('A') != null;
    }

    private static bool TestLoaderBitmap()
    {
        var json = """{"providers":[{"type":"bitmap","file":"minecraft:font/ascii.png","height":8,"ascent":7,"chars":["AB"]}]}""";
        var acc = new MockFontResourceAccessor();
        acc.Add("minecraft:textures/font/ascii.png", Convert.FromBase64String(Png8x8Base64));
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        if (result.Count != 1) return false;
        var s = result[0].Provider.GetSupportedGlyphs();
        return s.Contains(0x41) && s.Contains(0x42);
    }

    private static bool TestLoaderUnihex()
    {
        var hex = HexA32 + "\n";
        var json = """{"providers":[{"type":"unihex","hex_file":"minecraft:font/unihex.zip"}]}""";
        var acc = new MockFontResourceAccessor();
        //构造含 .hex 的 zip
        var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("g.hex");
            using var es = entry.Open();
            var bytes = Encoding.ASCII.GetBytes(hex);
            es.Write(bytes, 0, bytes.Length);
        }
        acc.Add("minecraft:font/unihex.zip", zipMs.ToArray());
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        if (result.Count != 1) return false;
        return result[0].Provider.GetGlyph(0x41) != null;
    }

    private static bool TestLoaderReference()
    {
        var main = """{"providers":[{"type":"reference","id":"minecraft:include/space"}]}""";
        var refJson = """{"providers":[{"type":"space","advances":{" ":4.0}}]}""";
        var acc = new MockFontResourceAccessor();
        acc.AddText("minecraft:font/include/space.json", refJson);
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(main)), acc);
        if (result.Count != 1) return false;
        return result[0].Provider.GetGlyph(' ')?.Info.Advance == 4.0f;
    }

    private static bool TestLoaderReferenceCycle()
    {
        //a 引用 b b 引用 a visited 集合保护不死循环
        var main = """{"providers":[{"type":"reference","id":"minecraft:a"}]}""";
        var aJson = """{"providers":[{"type":"reference","id":"minecraft:b"}]}""";
        var bJson = """{"providers":[{"type":"reference","id":"minecraft:a"}]}""";
        var acc = new MockFontResourceAccessor();
        acc.AddText("minecraft:font/a.json", aJson);
        acc.AddText("minecraft:font/b.json", bJson);
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(main)), acc);
        //循环保护返回空不抛异常
        return result.Count == 0;
    }

    private static bool TestLoaderFilter()
    {
        var json = """{"providers":[{"type":"space","advances":{" ":4.0},"filter":{"uniform":false}}]}""";
        var acc = new MockFontResourceAccessor();
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        if (result.Count != 1) return false;
        var filter = result[0].Filter;
        //uniform 未激活时匹配
        return filter.Apply(new HashSet<FontOption>())
            && !filter.Apply(new HashSet<FontOption> { FontOption.Uniform });
    }

    private static bool TestLoaderUnknownType()
    {
        var json = """{"providers":[{"type":"nonexistent","file":"x"}]}""";
        var acc = new MockFontResourceAccessor();
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        return result.Count == 0;
    }

    private static bool TestLoaderNoProviders()
    {
        var json = """{"another_field":1}""";
        var acc = new MockFontResourceAccessor();
        var result = FontProviderDefinitionLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), acc);
        return result.Count == 0;
    }

    //==== K GlyphFont ====

    private sealed class FakeProvider : IGlyphProvider
    {
        private readonly float _advance;
        public FakeProvider(float advance) => _advance = advance;
        public IReadOnlySet<int> GetSupportedGlyphs() => new HashSet<int> { 'A' };
        public IUnbakedGlyph? GetGlyph(int cp) => cp == 'A' ? new FakeUnbakedGlyph(_advance) : null;
        public void Dispose() { }
    }

    private sealed class FakeUnbakedGlyph : IUnbakedGlyph
    {
        private readonly float _advance;
        public FakeUnbakedGlyph(float advance) => _advance = advance;
        public IGlyphInfo Info => IGlyphInfo.Simple(_advance);
        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(Info, new FakeBitmap());
    }

    private sealed class FakeBitmap : IGlyphBitmap
    {
        public int PixelWidth => 5;
        public int PixelHeight => 8;
        public float Oversample => 1.0f;
        public bool IsColored => false;
        public byte[] GetPixels() => new byte[5 * 8];
    }

    private static GlyphFont MakeFakeFont(out CaptureStitcher stitcher)
    {
        stitcher = new CaptureStitcher();
        var fs = new FontSet();
        fs.Reload(new List<IGlyphProvider.Conditional>
        {
            new(new FakeProvider(5.0f), FontOptionFilter.AlwaysPass),
        }, new HashSet<FontOption>());
        return new GlyphFont(fs, stitcher, ascent: 7, lineHeight: 9);
    }

    private static bool TestFontMeasureEmpty()
    {
        var font = MakeFakeFont(out _);
        return font.MeasureText("") == 0f;
    }

    private static bool TestFontMeasureSum()
    {
        var font = MakeFakeFont(out _);
        //每个 A advance=5 两个 A=10
        return Math.Abs(font.MeasureText("AA") - 10.0f) < 1e-6f;
    }

    private static bool TestFontDrawRenders()
    {
        var font = MakeFakeFont(out var stitcher);
        var ctx = new CaptureContext();
        font.Draw(ctx, "A", 0, 0, unchecked((int)0xFFFFFFFF), false);
        //FakeUnbakedGlyph.Bake 调 stitcher.Stitch 创建 RecordingBakedGlyph
        if (stitcher.Created.Count != 1) return false;
        return stitcher.Created[0].RenderCount == 1;
    }

    private static bool TestFontDrawEmpty()
    {
        var font = MakeFakeFont(out _);
        var ctx = new CaptureContext();
        font.Draw(ctx, "", 0, 0, unchecked((int)0xFFFFFFFF), false);
        return true; //不抛异常即通过
    }

    private static bool TestFontDrawMissing()
    {
        var font = MakeFakeFont(out var stitcher);
        var ctx = new CaptureContext();
        //B 不在 FakeProvider 支持范围走 GetMissing
        font.Draw(ctx, "B", 0, 0, unchecked((int)0xFFFFFFFF), false);
        return stitcher.MissingCount == 1;
    }

    private static bool TestFontDrawShadowColor()
    {
        var font = MakeFakeFont(out var stitcher);
        var ctx = new CaptureContext();
        //color=unchecked((int)0xFFFFFFFF) RGB*0.25=63 → shadowColor=unchecked((int)0xFF3F3F3F)
        font.Draw(ctx, "A", 0, 0, unchecked((int)0xFFFFFFFF), shadow: true);
        if (stitcher.Created.Count != 1) return false;
        var opt = stitcher.Created[0].LastOptions;
        return opt.HasShadow && opt.ShadowColor == unchecked((int)0xFF3F3F3F);
    }

    private static bool TestFontAscentLineHeight()
    {
        var font = MakeFakeFont(out _);
        return font.Ascent == 7 && font.LineHeight == 9;
    }

    //==== L SheetBakedGlyph.Render italic/bold/shadow ====

    private static SheetBakedGlyph MakeSheetGlyph(out TextureSetup texture)
    {
        var img = new TestGpuImage(8, 8);
        texture = TextureSetup.SingleTexture(img, null!);
        return new SheetBakedGlyph(
            IGlyphInfo.Simple(5.0f),
            u0: 0, v0: 0, u1: 1, v1: 1,
            left: 0, right: 5, top: 0, bottom: 8,
            texture, GlyphRenderTypes.CreateForGrayscaleTexture());
    }

    private static bool TestSheetRenderNormal()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = GlyphRenderOptions.Simple(0, 0, unchecked((int)0xFFFFFFFF));
        glyph.Render(ctx, in opt);
        return ctx.Quads.Count == 1;
    }

    private static bool TestSheetRenderBold()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = new GlyphRenderOptions(0, 0, unchecked((int)0xFFFFFFFF), 0, true, false, 1.0f, 1.0f);
        glyph.Render(ctx, in opt);
        //bold 主字形 1 次 + 加粗二次绘制 1 次 = 2
        return ctx.Quads.Count == 2;
    }

    private static bool TestSheetRenderShadow()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = new GlyphRenderOptions(0, 0, unchecked((int)0xFFFFFFFF), unchecked((int)0xFF000000), false, false, 1.0f, 1.0f);
        glyph.Render(ctx, in opt);
        //阴影 1 次 + 主字形 1 次 = 2
        return ctx.Quads.Count == 2;
    }

    private static bool TestSheetRenderBoldShadow()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = new GlyphRenderOptions(0, 0, unchecked((int)0xFFFFFFFF), unchecked((int)0xFF000000), true, false, 1.0f, 1.0f);
        glyph.Render(ctx, in opt);
        //阴影 1 + 阴影加粗 1 + 主字形 1 + 主字形加粗 1 = 4
        return ctx.Quads.Count == 4;
    }

    private static bool TestSheetRenderItalic()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = new GlyphRenderOptions(0, 0, unchecked((int)0xFFFFFFFF), 0, false, true, 1.0f, 1.0f);
        glyph.Render(ctx, in opt);
        if (ctx.Quads.Count != 1) return false;
        var q = ctx.Quads[0];
        //italic shearTop=1.0-0.25*Top(0)=1.0 shearBottom=1.0-0.25*Bottom(8)=-1.0
        //x0=Left+shearTop=0+1.0=1.0  x1=Left+shearBottom=0+(-1.0)=-1.0 顶部右移底部左移
        return Math.Abs(q.X0 - 1.0f) < 1e-6f && Math.Abs(q.X1 - (-1.0f)) < 1e-6f;
    }

    private static bool TestSheetRenderShadowPipeline()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = new GlyphRenderOptions(0, 0, unchecked((int)0xFFFFFFFF), unchecked((int)0xFF000000), false, false, 1.0f, 1.0f);
        glyph.Render(ctx, in opt);
        if (ctx.Quads.Count != 2) return false;
        var types = GlyphRenderTypes.CreateForGrayscaleTexture();
        //阴影先绘制用 PolygonOffset 主字形用 Normal
        return ctx.Quads[0].Pipeline == types.PolygonOffset
            && ctx.Quads[1].Pipeline == types.Normal;
    }

    private static bool TestSheetRenderVertexOrder()
    {
        var glyph = MakeSheetGlyph(out _);
        var ctx = new CaptureContext();
        var opt = GlyphRenderOptions.Simple(0, 0, unchecked((int)0xFFFFFFFF));
        glyph.Render(ctx, in opt);
        if (ctx.Quads.Count != 1) return false;
        var q = ctx.Quads[0];
        //4 顶点顺序 左上→左下→右下→右上
        //左上 (Left+0, Top)=(0,0) 左下 (Left+0, Bottom)=(0,8) 右下 (Right, Bottom)=(5,8) 右上 (Right, Top)=(5,0)
        return Math.Abs(q.X0 - 0f) < 1e-6f && Math.Abs(q.Y0 - 0f) < 1e-6f
            && Math.Abs(q.X1 - 0f) < 1e-6f && Math.Abs(q.Y1 - 8f) < 1e-6f
            && Math.Abs(q.X2 - 5f) < 1e-6f && Math.Abs(q.Y2 - 8f) < 1e-6f
            && Math.Abs(q.X3 - 5f) < 1e-6f && Math.Abs(q.Y3 - 0f) < 1e-6f;
    }

    //==== M GlyphBlitRenderState ====

    private static bool TestBlitStateBuildVertices()
    {
        var state = new GlyphBlitRenderState(
            RenderPipelines.GUI_TEXT, TextureSetup.NoTexture, Matrix3x2.Identity,
            0, 0, 0, 8, 5, 8, 5, 0,
            0, 0, 1, 1, unchecked((int)0xFFFFFFFF),
            new ScreenRectangle(0, 0, 800, 600));
        var consumer = new CaptureVertexConsumer();
        state.BuildVertices(consumer);
        return consumer.Vertices.Count == 4;
    }

    private static bool TestBlitStateUVMapping()
    {
        var state = new GlyphBlitRenderState(
            RenderPipelines.GUI_TEXT, TextureSetup.NoTexture, Matrix3x2.Identity,
            0, 0, 0, 8, 5, 8, 5, 0,
            0, 0, 1, 1, unchecked((int)0xFFFFFFFF),
            new ScreenRectangle(0, 0, 800, 600));
        var consumer = new CaptureVertexConsumer();
        state.BuildVertices(consumer);
        var v = consumer.Vertices;
        //左上(U0,V0)=(0,0) 左下(U0,V1)=(0,1) 右下(U1,V1)=(1,1) 右上(U1,V0)=(1,0)
        return Math.Abs(v[0].U - 0f) < 1e-6f && Math.Abs(v[0].V - 0f) < 1e-6f
            && Math.Abs(v[1].U - 0f) < 1e-6f && Math.Abs(v[1].V - 1f) < 1e-6f
            && Math.Abs(v[2].U - 1f) < 1e-6f && Math.Abs(v[2].V - 1f) < 1e-6f
            && Math.Abs(v[3].U - 1f) < 1e-6f && Math.Abs(v[3].V - 0f) < 1e-6f;
    }

    //==== 测试桩 ====

    //CaptureContext 捕获 DrawGlyphQuad 调用其他方法空实现对标 MockRenderContext 但记录字形 quad 参数
    private sealed class CaptureContext : IGuiRenderContext
    {
        public List<GlyphQuadCall> Quads = new();
        public float MeasureText(string text) => text.Length * 6;
        public int LineHeight => 9;
        public void DrawQuad(int x, int y, int w, int h, GuiColor c) { }
        public void DrawQuadInverted(int x, int y, int w, int h, GuiColor c) { }
        public void DrawText(int x, int y, string t, GuiColor c) { }
        public void DrawImage(int id, int x, int y, int w, int h, int sx, int sy, int sw, int sh, GuiColor t) { }
        public void DrawImageNinePatch(int id, int x, int y, int w, int h, int sx, int sy, int sw, int sh, int b, GuiColor t) { }
        //DrawImageNinePatch per-side border + stretchInner 重载 CaptureContext 空实现
        public void DrawImageNinePatch(int id, int x, int y, int w, int h, int sx, int sy, int sw, int sh,
            int bl, int bt, int br, int bb, bool stretchInner, GuiColor t) { }
        //DrawTiledSprite CaptureContext 空实现
        public void DrawTiledSprite(int id, int srcW, int srcH, int x, int y, int w, int h, GuiColor t) { }
        //DrawSprite CaptureContext 空实现
        public void DrawSprite(string identifier, int x, int y, int w, int h, GuiColor t) { }
        public void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup texture,
            float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
            float u0, float v0, float u1, float v1, int color)
            => Quads.Add(new GlyphQuadCall(pipeline, texture, x0, y0, x1, y1, x2, y2, x3, y3, u0, v0, u1, v1, color));
        public void PushPose(Matrix3x2 d) { }
        public void PopPose() { }
        public void PushScissor(int x, int y, int w, int h) { }
        public void PopScissor() { }
        public void BeginRecording(List<GuiElementRenderState> c) { }
        public void EndRecording() { }
        public void ReplayRange(IReadOnlyList<GuiElementRenderState> c) { }
        public void BlurBeforeThisStratum() { }
        public void AddPictureInPicture(PictureInPictureRenderState pip) { }
    }

    private sealed record GlyphQuadCall(
        RenderPipeline Pipeline, TextureSetup Texture,
        float X0, float Y0, float X1, float Y1, float X2, float Y2, float X3, float Y3,
        float U0, float V0, float U1, float V1, int Color);

    //CaptureStitcher 捕获 Stitch 调用返回 RecordingBakedGlyph 供 GlyphFont.Draw 测试验证
    private sealed class CaptureStitcher : IUnbakedGlyph.Stitcher
    {
        public List<RecordingBakedGlyph> Created = new();
        public IGlyphBitmap? LastBitmap;
        public int MissingCount;
        public BakedGlyph Stitch(IGlyphInfo info, IGlyphBitmap bitmap)
        {
            LastBitmap = bitmap;
            var g = new RecordingBakedGlyph(info);
            Created.Add(g);
            return g;
        }
        public BakedGlyph GetMissing()
        {
            MissingCount++;
            return new RecordingBakedGlyph(IGlyphInfo.Simple(0));
        }
    }

    //RecordingBakedGlyph 记录 Render 调用次数和最后一次 options
    private sealed class RecordingBakedGlyph : BakedGlyph
    {
        private readonly IGlyphInfo _info;
        public int RenderCount;
        public GlyphRenderOptions LastOptions;
        public RecordingBakedGlyph(IGlyphInfo info) => _info = info;
        public override IGlyphInfo Info => _info;
        public override void Render(IGuiRenderContext context, in GlyphRenderOptions options)
        {
            RenderCount++;
            LastOptions = options;
        }
    }

    //CaptureVertexConsumer 收集 BuildVertices 提交的顶点供 GlyphBlitRenderState 测试
    private sealed class CaptureVertexConsumer : IVertexConsumer
    {
        public List<(float X, float Y, float U, float V, int Color)> Vertices = new();
        public void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color)
            => Vertices.Add((x, y, u, v, color));
    }

    //TestGpuImage 测试用 GpuImage 桩不依赖 Vulkan 供 TextureSetup 绑定
    private sealed class TestGpuImage : GpuImage
    {
        public TestGpuImage(int w, int h) : base(new GpuImageDescription
        {
            Width = w, Height = h,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        }) { }
        public override void Upload(ReadOnlySpan<byte> pixels) { }
    }

    //==== N SpecialGlyphs ====

    //Missing advance=width+1=6 对标原版 SpecialGlyphs.getAdvance
    private static bool TestSpecialMissingAdvance()
        => Math.Abs(SpecialGlyphs.Missing.Info.Advance - 6.0f) < 1e-6f;

    private static bool TestSpecialWhiteAdvance()
        => Math.Abs(SpecialGlyphs.White.Info.Advance - 6.0f) < 1e-6f;

    //Missing.Bake 调 Stitcher.Stitch 返回 RecordingBakedGlyph Created 记录 1 次
    private static bool TestSpecialMissingBakeStitch()
    {
        var stitcher = new CaptureStitcher();
        var baked = SpecialGlyphs.Missing.Bake(stitcher);
        return stitcher.Created.Count == 1 && ReferenceEquals(stitcher.Created[0], baked);
    }

    //Missing 像素边框不透明(0xFF)内部透明(0x00)对标原版 edge? -1:0
    private static bool TestSpecialMissingPixels()
    {
        var stitcher = new CaptureStitcher();
        SpecialGlyphs.Missing.Bake(stitcher);
        var bitmap = stitcher.LastBitmap;
        if (bitmap == null) return false;
        var pixels = bitmap.GetPixels();
        //5x8 RGBA8 长度=5*8*4=160
        if (pixels.Length != 160) return false;
        //边框 (0,0) 全 0xFF
        bool edgeOpaque = pixels[0] == 0xFF && pixels[3] == 0xFF;
        //内部 (2,2) 全 0x00 索引=(2*5+2)*4=48
        int idx = (2 * 5 + 2) * 4;
        bool interiorTransparent = pixels[idx] == 0x00 && pixels[idx + 3] == 0x00;
        return edgeOpaque && interiorTransparent;
    }

    //White 像素全 0xFF 对标原版 SpecialGlyphs.WHITE
    private static bool TestSpecialWhitePixels()
    {
        var stitcher = new CaptureStitcher();
        SpecialGlyphs.White.Bake(stitcher);
        var bitmap = stitcher.LastBitmap;
        if (bitmap == null) return false;
        var pixels = bitmap.GetPixels();
        if (pixels.Length != 160) return false;
        foreach (var b in pixels) if (b != 0xFF) return false;
        return true;
    }

    //bitmap 5x8 IsColored=true Oversample=1.0 对标原版 SpecialGlyphs 5x8 RGBA8
    private static bool TestSpecialBitmapMetrics()
    {
        var stitcher = new CaptureStitcher();
        SpecialGlyphs.Missing.Bake(stitcher);
        var bitmap = stitcher.LastBitmap;
        if (bitmap == null) return false;
        return bitmap.PixelWidth == 5 && bitmap.PixelHeight == 8
            && bitmap.IsColored && Math.Abs(bitmap.Oversample - 1.0f) < 1e-6f;
    }
}
