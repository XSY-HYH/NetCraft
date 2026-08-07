using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//GuiLayoutTests 原版风格独立 Layout 体系测试
//覆盖 LayoutSettings/Divisor/GridLayout/LinearLayout/FrameLayout/EqualSpacingLayout/HeaderAndFooterLayout/SpacerElement
//纯逻辑不依赖 Vulkan 用 MockElement 验证布局引擎定位计算
internal static class GuiLayoutTests
{
    public const string Module = "guilayout";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("LayoutSettings Defaults zero padding align", TestDefaultsZero);
        yield return ("LayoutSettings Padding chain fluent", TestPaddingChain);
        yield return ("LayoutSettings Align chain fluent", TestAlignChain);
        yield return ("LayoutSettings Copy independent", TestCopyIndependent);
        yield return ("Divisor distributes total with remainder", TestDivisorDistributes);
        yield return ("GridLayout single cell positions child", TestGridLayoutSingleCell);
        yield return ("GridLayout multi row col with spacing", TestGridLayoutMultiRowCol);
        yield return ("GridLayout span rows cols divides", TestGridLayoutSpan);
        yield return ("GridLayout align centers child in wide cell", TestGridLayoutAlignCenters);
        yield return ("LinearLayout vertical stacks children", TestLinearLayoutVertical);
        yield return ("LinearLayout horizontal stacks children", TestLinearLayoutHorizontal);
        yield return ("FrameLayout default centers child", TestFrameLayoutDefaultCenters);
        yield return ("FrameLayout align offsets child", TestFrameLayoutAlignOffsets);
        yield return ("FrameLayout centerInRectangle", TestFrameLayoutCenterInRectangle);
        yield return ("FrameLayout alignInRectangle", TestFrameLayoutAlignInRectangle);
        yield return ("EqualSpacingLayout horizontal even gaps", TestEqualSpacingHorizontal);
        yield return ("EqualSpacingLayout vertical even gaps", TestEqualSpacingVertical);
        yield return ("HeaderAndFooterLayout three sections", TestHeaderAndFooterThreeSections);
        yield return ("HeaderAndFooterLayout content not past footer", TestHeaderAndFooterContentClamped);
        yield return ("SpacerElement holds dimensions", TestSpacerElementDimensions);
        yield return ("CommonLayouts LabeledElement vertical label above", TestCommonLayoutsLabeledElement);
        yield return ("CommonLayouts LabeledElement applies settings", TestCommonLayoutsLabeledElementWithSettings);
    }

    //MockElement 最小 ILayoutElement 实现 Width/Height 构造只读 X/Y 可由布局引擎移动
    private sealed class MockElement : ILayoutElement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; }
        public int Height { get; }

        public MockElement(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    //Defaults 默认 padding=0 align=0,0 左上对齐
    private static bool TestDefaultsZero()
    {
        var s = LayoutSettings.Defaults().GetExposed();
        return s.PaddingLeftValue == 0 && s.PaddingTopValue == 0
            && s.PaddingRightValue == 0 && s.PaddingBottomValue == 0
            && s.XAlignment == 0f && s.YAlignment == 0f;
    }

    //Padding 链式四向独立设置
    private static bool TestPaddingChain()
    {
        var s = LayoutSettings.Defaults().Padding(1, 2, 3, 4).GetExposed();
        return s.PaddingLeftValue == 1 && s.PaddingTopValue == 2
            && s.PaddingRightValue == 3 && s.PaddingBottomValue == 4;
    }

    //Align 链式设置 xAlignment/yAlignment
    private static bool TestAlignChain()
    {
        var s = LayoutSettings.Defaults().Align(0.5f, 0.5f).GetExposed();
        if (s.XAlignment != 0.5f || s.YAlignment != 0.5f) return false;
        var s2 = LayoutSettings.Defaults().AlignHorizontally(1f).GetExposed();
        var s3 = LayoutSettings.Defaults().AlignVertically(1f).GetExposed();
        return s2.XAlignment == 1f && s3.YAlignment == 1f;
    }

    //Copy 副本独立修改不影响原件
    private static bool TestCopyIndependent()
    {
        var original = LayoutSettings.Defaults().Padding(5);
        var copy = original.Copy().Padding(10);
        return original.GetExposed().PaddingLeftValue == 5
            && copy.GetExposed().PaddingLeftValue == 10;
    }

    //Divisor total 均分 parts 份余数分到前几个总和不差
    private static bool TestDivisorDistributes()
    {
        var d = new Divisor(10, 3);
        int a = d.NextInt(), b = d.NextInt(), c = d.NextInt();
        //base=3 remainder=1 第一份得 4 余下 3 3
        return a == 4 && b == 3 && c == 3 && a + b + c == 10;
    }

    //GridLayout 单格定位子元素到自身原点
    private static bool TestGridLayoutSingleCell()
    {
        var grid = new GridLayout(10, 20);
        var a = new MockElement(50, 20);
        grid.AddChild(a, 0, 0);
        grid.ArrangeElements();
        return a.X == 10 && a.Y == 20 && grid.Width == 50 && grid.Height == 20;
    }

    //GridLayout 多行多列含 spacing 按列宽行高累加偏移
    private static bool TestGridLayoutMultiRowCol()
    {
        var grid = new GridLayout().ColumnSpacing(5).RowSpacing(5);
        var a = new MockElement(50, 20);
        var b = new MockElement(30, 20);
        var c = new MockElement(50, 40);
        grid.AddChild(a, 0, 0);
        grid.AddChild(b, 0, 1);
        grid.AddChild(c, 1, 0);
        grid.ArrangeElements();
        //a(0,0) b 列1 x=55 y=0 c 行1 x=0 y=25
        return a.X == 0 && a.Y == 0
            && b.X == 55 && b.Y == 0
            && c.X == 0 && c.Y == 25
            && grid.Width == 85 && grid.Height == 65;
    }

    //GridLayout 跨多行多列元素尺寸用 Divisor 均分到各行列
    private static bool TestGridLayoutSpan()
    {
        var grid = new GridLayout();
        var big = new MockElement(100, 60);
        grid.AddChild(big, 0, 0, 2, 2);
        grid.ArrangeElements();
        //单元素跨2x2 width=100 height=60
        return big.X == 0 && big.Y == 0 && grid.Width == 100 && grid.Height == 60;
    }

    //GridLayout align 在宽于元素的 cell 内居中偏移
    private static bool TestGridLayoutAlignCenters()
    {
        var grid = new GridLayout();
        var wide = new MockElement(100, 20);
        var small = new MockElement(40, 20);
        grid.AddChild(wide, 0, 0);
        grid.AddChild(small, 1, 0, grid.NewCellSettings().AlignHorizontally(0.5f));
        grid.ArrangeElements();
        //列宽 100 small 在 100 宽 cell 内居中 offset=(100-40)/2=30
        return wide.X == 0 && wide.Y == 0 && small.X == 30 && small.Y == 20;
    }

    //LinearLayout vertical 按 row 递增堆叠含 spacing
    private static bool TestLinearLayoutVertical()
    {
        var layout = new LinearLayout(0, 0, LinearLayout.Orientation.Vertical).Spacing(5);
        var a = new MockElement(50, 20);
        var b = new MockElement(50, 30);
        layout.AddChild(a);
        layout.AddChild(b);
        layout.ArrangeElements();
        //a row0 y=0 b row1 y=20+5=25 总高 20+5+30=55
        return a.Y == 0 && b.Y == 25 && layout.Height == 55;
    }

    //LinearLayout horizontal 按 col 递增堆叠含 spacing
    private static bool TestLinearLayoutHorizontal()
    {
        var layout = new LinearLayout(0, 0, LinearLayout.Orientation.Horizontal).Spacing(5);
        var a = new MockElement(50, 20);
        var b = new MockElement(30, 20);
        layout.AddChild(a);
        layout.AddChild(b);
        layout.ArrangeElements();
        //a col0 x=0 b col1 x=50+5=55
        return a.X == 0 && b.X == 55 && layout.Width == 85;
    }

    //FrameLayout 默认 align 0.5,0.5 子元素在 minDim 矩形内居中
    private static bool TestFrameLayoutDefaultCenters()
    {
        var frame = new FrameLayout(100, 100);
        var a = new MockElement(50, 20);
        frame.AddChild(a);
        frame.ArrangeElements();
        //x=(100-50)/2=25 y=(100-20)/2=40
        return a.X == 25 && a.Y == 40 && frame.Width == 100 && frame.Height == 100;
    }

    //FrameLayout 自定义 align 偏移子元素到指定角
    private static bool TestFrameLayoutAlignOffsets()
    {
        var frame = new FrameLayout(100, 100);
        var a = new MockElement(50, 20);
        frame.AddChild(a, frame.NewChildLayoutSettings().Align(1f, 1f));
        frame.ArrangeElements();
        //align=1 贴右下 x=100-50=50 y=100-20=80
        return a.X == 50 && a.Y == 80;
    }

    //FrameLayout.CenterInRectangle 静态方法将元素居中到矩形
    private static bool TestFrameLayoutCenterInRectangle()
    {
        var a = new MockElement(50, 20);
        FrameLayout.CenterInRectangle(a, 0, 0, 100, 100);
        return a.X == 25 && a.Y == 40;
    }

    //FrameLayout.AlignInRectangle 按 alignX/alignY 对齐元素到矩形内
    private static bool TestFrameLayoutAlignInRectangle()
    {
        var a = new MockElement(50, 20);
        FrameLayout.AlignInRectangle(a, 0, 0, 100, 100, 0f, 0f);
        if (a.X != 0 || a.Y != 0) return false;
        FrameLayout.AlignInRectangle(a, 0, 0, 100, 100, 1f, 1f);
        return a.X == 50 && a.Y == 80;
    }

    //EqualSpacingLayout horizontal 子元素等间距分布副轴对齐
    private static bool TestEqualSpacingHorizontal()
    {
        var layout = new EqualSpacingLayout(200, 30, EqualSpacingLayout.Orientation.Horizontal);
        var a = new MockElement(50, 30);
        var b = new MockElement(50, 30);
        var c = new MockElement(50, 30);
        layout.AddChild(a);
        layout.AddChild(b);
        layout.AddChild(c);
        layout.ArrangeElements();
        //total=150 remaining=50 两个间隙各 25 a=0 b=75 c=150
        return a.X == 0 && b.X == 75 && c.X == 150 && layout.Height == 30;
    }

    //EqualSpacingLayout vertical 子元素主轴 Y 等间距
    private static bool TestEqualSpacingVertical()
    {
        var layout = new EqualSpacingLayout(30, 200, EqualSpacingLayout.Orientation.Vertical);
        var a = new MockElement(30, 50);
        var b = new MockElement(30, 50);
        layout.AddChild(a);
        layout.AddChild(b);
        layout.ArrangeElements();
        //total=100 remaining=100 一个间隙 a=0 b=150
        return a.Y == 0 && b.Y == 150 && layout.Width == 30;
    }

    //HeaderAndFooterLayout header 贴顶 footer 贴底 content 居中
    private static bool TestHeaderAndFooterThreeSections()
    {
        var layout = new HeaderAndFooterLayout(200, 300);
        var header = new MockElement(100, 33);
        var content = new MockElement(150, 50);
        var footer = new MockElement(100, 33);
        layout.AddToHeader(header);
        layout.AddToContents(content);
        layout.AddToFooter(footer);
        layout.ArrangeElements();
        //header 居中 x=(200-100)/2=50 y=0
        //content 居中 x=(200-150)/2=25 y=33+30=63
        //footer 居中 x=50 y=300-33=267
        return header.X == 50 && header.Y == 0
            && content.X == 25 && content.Y == 63
            && footer.X == 50 && footer.Y == 267;
    }

    //HeaderAndFooterLayout content 不越过 footer 上沿当内容过高时上移
    private static bool TestHeaderAndFooterContentClamped()
    {
        var layout = new HeaderAndFooterLayout(200, 300);
        var content = new MockElement(150, 250);
        layout.AddToContents(content);
        layout.ArrangeElements();
        //preferredY=33+30=63 maxContentY=300-33-250=17 取 min=17
        return content.Y == 17;
    }

    //SpacerElement 持有构造尺寸另一方向为 0
    private static bool TestSpacerElementDimensions()
    {
        var w = SpacerElement.OfWidth(50);
        var h = SpacerElement.OfHeight(30);
        return w.Width == 50 && w.Height == 0 && h.Width == 0 && h.Height == 30;
    }

    //CommonLayouts LabeledElement 构造垂直布局标签在上元素在下间距 4
    private static bool TestCommonLayoutsLabeledElement()
    {
        var element = new MockElement(50, 20);
        var layout = CommonLayouts.LabeledElement(element, "Label");
        var children = new List<ILayoutElement>();
        layout.VisitChildren(c => children.Add(c));
        if (children.Count != 2) return false;
        if (children[0] is not GuiLabel) return false;
        layout.ArrangeElements();
        //标签在上元素在下垂直间距 4
        return children[1].Y == children[0].Y + children[0].Height + 4;
    }

    //CommonLayouts LabeledElement 带 settings 调用 Action 定制元素 cell
    private static bool TestCommonLayoutsLabeledElementWithSettings()
    {
        var element = new MockElement(50, 20);
        bool settingsCalled = false;
        var layout = CommonLayouts.LabeledElement(element, "Label", s =>
        {
            settingsCalled = true;
            s.AlignHorizontally(0.5f).Padding(2);
        });
        layout.ArrangeElements();
        return settingsCalled;
    }
}
