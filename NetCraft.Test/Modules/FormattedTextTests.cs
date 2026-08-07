using System.Text;
using NetCraft.Network.Chat;

namespace NetCraft.Test.Modules;

//FormattedTextTests 富文本遍历体系测试对标原版 FormattedCharSequence/StringDecomposer
//覆盖 FormattedCharSequences 工厂(Empty/Codepoint/Forward/Backward/Composite)
//StringDecomposer(Iterate 代理对/IterateFormatted §颜色码/IterateBackwards/FilterBrokenSurrogates)
//纯逻辑不依赖 Vulkan 用 List 捕获 sink 回调验证 codepoint/style/position
internal static class FormattedTextTests
{
    public const string Module = "formattedtext";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //A FormattedCharSequences 工厂
        yield return ("FormattedCharSequences.Empty 不调 sink", TestEmptyNoCallback);
        yield return ("FormattedCharSequences.Codepoint 单字符回调", TestCodepointSingle);
        yield return ("FormattedCharSequences.Forward ABC 回调 3 次", TestForwardABC);
        yield return ("FormattedCharSequences.Forward 空串返回 Empty", TestForwardEmpty);
        yield return ("FormattedCharSequences.Backward 反向顺序", TestBackwardOrder);
        yield return ("FormattedCharSequences.Composite 两段顺序", TestCompositePair);
        yield return ("FormattedCharSequences.Composite 多段", TestCompositeList);
        yield return ("FormattedCharSequences.Composite 空列表返回 Empty", TestCompositeEmptyList);

        //B StringDecomposer.Iterate 代理对解码
        yield return ("StringDecomposer.Iterate BMP 字符 codepoint", TestIterateBmp);
        yield return ("StringDecomposer.Iterate 代理对合并 U+1F600", TestIterateSurrogatePair);
        yield return ("StringDecomposer.Iterate 孤立高代理 ReplacementChar", TestIterateLoneHighSurrogate);
        yield return ("StringDecomposer.Iterate 末尾高代理 ReplacementChar", TestIterateTrailingHighSurrogate);
        yield return ("StringDecomposer.Iterate sink 返回 false 停止", TestIterateStopOnFalse);

        //C StringDecomposer.IterateBackwards
        yield return ("StringDecomposer.IterateBackwards 反向遍历", TestIterateBackwards);
        yield return ("StringDecomposer.IterateBackwards 代理对合并", TestIterateBackwardsSurrogate);

        //D StringDecomposer.IterateFormatted § 颜色码解析
        yield return ("IterateFormatted §l 应用 Bold", TestFormattedBold);
        yield return ("IterateFormatted §o 应用 Italic", TestFormattedItalic);
        yield return ("IterateFormatted §r RESET 重置", TestFormattedReset);
        yield return ("IterateFormatted §c 应用 Red 颜色", TestFormattedColor);
        yield return ("IterateFormatted §和格式码字符不回调 sink", TestFormattedSkipsSectionSign);
        yield return ("IterateFormatted 末尾孤立 § 跳过", TestFormattedTrailingSectionSign);
        yield return ("IterateFormatted resetStyle 参数 RESET 回退", TestFormattedResetStyle);

        //E FilterBrokenSurrogates
        yield return ("FilterBrokenSurrogates 替换孤立代理", TestFilterBrokenSurrogates);
        yield return ("FilterBrokenSurrogates 保留代理对", TestFilterKeepsValidPair);
    }

    //CaptureSink 捕获 sink 回调到 List 供断言验证
    private sealed class CaptureSink
    {
        public List<(int Pos, Style Style, int Codepoint)> Calls = new();
        public bool AlwaysTrue(int pos, Style style, int cp)
        {
            Calls.Add((pos, style, cp));
            return true;
        }
    }

    //==== A FormattedCharSequences 工厂 ====

    private static bool TestEmptyNoCallback()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Empty(sink.AlwaysTrue);
        return sink.Calls.Count == 0;
    }

    private static bool TestCodepointSingle()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Codepoint(0x41, Style.Empty)(sink.AlwaysTrue);
        return sink.Calls.Count == 1 && sink.Calls[0].Codepoint == 0x41;
    }

    private static bool TestForwardABC()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Forward("ABC", Style.Empty)(sink.AlwaysTrue);
        return sink.Calls.Count == 3
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[1].Codepoint == 'B'
            && sink.Calls[2].Codepoint == 'C';
    }

    private static bool TestForwardEmpty()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Forward("", Style.Empty)(sink.AlwaysTrue);
        return sink.Calls.Count == 0;
    }

    private static bool TestBackwardOrder()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Backward("ABC", Style.Empty)(sink.AlwaysTrue);
        //反向 A 在前但 IterateBackwards 从末尾 C B A
        return sink.Calls.Count == 3
            && sink.Calls[0].Codepoint == 'C'
            && sink.Calls[1].Codepoint == 'B'
            && sink.Calls[2].Codepoint == 'A';
    }

    private static bool TestCompositePair()
    {
        var sink = new CaptureSink();
        var first = FormattedCharSequences.Forward("AB", Style.Empty);
        var second = FormattedCharSequences.Forward("CD", Style.Empty);
        FormattedCharSequences.Composite(first, second)(sink.AlwaysTrue);
        return sink.Calls.Count == 4
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[3].Codepoint == 'D';
    }

    private static bool TestCompositeList()
    {
        var sink = new CaptureSink();
        var parts = new[]
        {
            FormattedCharSequences.Forward("A", Style.Empty),
            FormattedCharSequences.Forward("B", Style.Empty),
            FormattedCharSequences.Forward("C", Style.Empty)
        };
        FormattedCharSequences.Composite(parts)(sink.AlwaysTrue);
        return sink.Calls.Count == 3;
    }

    private static bool TestCompositeEmptyList()
    {
        var sink = new CaptureSink();
        FormattedCharSequences.Composite(Array.Empty<FormattedCharSequence>())(sink.AlwaysTrue);
        return sink.Calls.Count == 0;
    }

    //==== B StringDecomposer.Iterate 代理对解码 ====

    private static bool TestIterateBmp()
    {
        var sink = new CaptureSink();
        StringDecomposer.Iterate("AB", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 2
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[1].Codepoint == 'B'
            && sink.Calls[0].Pos == 0
            && sink.Calls[1].Pos == 1;
    }

    private static bool TestIterateSurrogatePair()
    {
        var sink = new CaptureSink();
        //U+1F600 GRINNING FACE 高代理 D800 低代理 DE00
        StringDecomposer.Iterate("\uD83D\uDE00", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && sink.Calls[0].Codepoint == 0x1F600;
    }

    private static bool TestIterateLoneHighSurrogate()
    {
        var sink = new CaptureSink();
        //孤立高代理后跟普通字符 ReplacementChar + A
        StringDecomposer.Iterate("\uD83DA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 2
            && sink.Calls[0].Codepoint == 0xFFFD
            && sink.Calls[1].Codepoint == 'A';
    }

    private static bool TestIterateTrailingHighSurrogate()
    {
        var sink = new CaptureSink();
        //末尾孤立高代理 ReplacementChar
        StringDecomposer.Iterate("A\uD83D", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 2
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[1].Codepoint == 0xFFFD;
    }

    private static bool TestIterateStopOnFalse()
    {
        var sink = new CaptureSink();
        //sink 在 'B' 返回 false 停止遍历
        StringDecomposer.Iterate("ABC", Style.Empty, (pos, style, cp) =>
        {
            sink.Calls.Add((pos, style, cp));
            return cp != 'B';
        });
        return sink.Calls.Count == 2
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[1].Codepoint == 'B';
    }

    //==== C StringDecomposer.IterateBackwards ====

    private static bool TestIterateBackwards()
    {
        var sink = new CaptureSink();
        StringDecomposer.IterateBackwards("ABC", Style.Empty, sink.AlwaysTrue);
        //反向 C B A position 仍源索引
        return sink.Calls.Count == 3
            && sink.Calls[0].Codepoint == 'C' && sink.Calls[0].Pos == 2
            && sink.Calls[1].Codepoint == 'B' && sink.Calls[1].Pos == 1
            && sink.Calls[2].Codepoint == 'A' && sink.Calls[2].Pos == 0;
    }

    private static bool TestIterateBackwardsSurrogate()
    {
        var sink = new CaptureSink();
        StringDecomposer.IterateBackwards("A\uD83D\uDE00", Style.Empty, sink.AlwaysTrue);
        //反向 emoji 在前 position=1 然后 A position=0
        return sink.Calls.Count == 2
            && sink.Calls[0].Codepoint == 0x1F600 && sink.Calls[0].Pos == 1
            && sink.Calls[1].Codepoint == 'A' && sink.Calls[1].Pos == 0;
    }

    //==== D StringDecomposer.IterateFormatted § 颜色码解析 ====

    private static bool TestFormattedBold()
    {
        var sink = new CaptureSink();
        //§l 应用 Bold 后 A 用 bold style
        StringDecomposer.IterateFormatted("\u00A7lA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1
            && sink.Calls[0].Codepoint == 'A'
            && sink.Calls[0].Style.IsBold;
    }

    private static bool TestFormattedItalic()
    {
        var sink = new CaptureSink();
        StringDecomposer.IterateFormatted("\u00A7oA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && sink.Calls[0].Style.IsItalic;
    }

    private static bool TestFormattedReset()
    {
        var sink = new CaptureSink();
        //§l Bold 后 §r Reset 再 A 应非 bold
        StringDecomposer.IterateFormatted("\u00A7l\u00A7rA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && !sink.Calls[0].Style.IsBold;
    }

    private static bool TestFormattedColor()
    {
        var sink = new CaptureSink();
        //§c Red 颜色应用到 style
        StringDecomposer.IterateFormatted("\u00A7cA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && sink.Calls[0].Style.Color != null;
    }

    private static bool TestFormattedSkipsSectionSign()
    {
        var sink = new CaptureSink();
        //§l 两字符不回调 sink 只 A 回调 1 次
        StringDecomposer.IterateFormatted("\u00A7lA", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1;
    }

    private static bool TestFormattedTrailingSectionSign()
    {
        var sink = new CaptureSink();
        //末尾孤立 § 跳过 A 回调 1 次
        StringDecomposer.IterateFormatted("A\u00A7", Style.Empty, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && sink.Calls[0].Codepoint == 'A';
    }

    private static bool TestFormattedResetStyle()
    {
        var sink = new CaptureSink();
        //resetStyle 参数 §r 后回退到 resetStyle 而非 currentStyle
        var baseStyle = Style.Empty.WithBold(true);
        var resetStyle = Style.Empty;
        //§r 后 style 应回退到 resetStyle 无 bold
        StringDecomposer.IterateFormatted("\u00A7rA", 0, baseStyle, resetStyle, sink.AlwaysTrue);
        return sink.Calls.Count == 1 && !sink.Calls[0].Style.IsBold;
    }

    //==== E FilterBrokenSurrogates ====

    private static bool TestFilterBrokenSurrogates()
    {
        //孤立高代理替换为 U+FFFD
        var result = StringDecomposer.FilterBrokenSurrogates("A\uD83DB");
        //A + U+FFFD + B
        return result.Length == 3
            && result[0] == 'A'
            && result[1] == '\uFFFD'
            && result[2] == 'B';
    }

    private static bool TestFilterKeepsValidPair()
    {
        //有效代理对保留为 1 个 codepoint
        var result = StringDecomposer.FilterBrokenSurrogates("A\uD83D\uDE00B");
        //A + emoji(2 char) + B = 4 char
        return result.Length == 4 && result[1] == '\uD83D' && result[2] == '\uDE00';
    }
}
