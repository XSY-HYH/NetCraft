using System.Text;
using NetCraft.Network.Chat;
using NetCraft.Network.Chat.Contents;
using NetCraft.Network;

namespace NetCraft.Test.Modules;

//Component 文本组件系统测试覆盖Component/Style/TextColor/ChatFormatting等核心类型
internal static class ComponentTests
{
    public const string Module = "component";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //Component 基础
        yield return ("Component literal GetString", TestLiteralGetString);
        yield return ("Component empty string", TestEmptyGetString);
        yield return ("Component null to empty", TestNullToEmpty);
        yield return ("Component tryCollapseToString literal", TestTryCollapseLiteral);
        yield return ("Component tryCollapseToString with siblings returns null", TestTryCollapseWithSiblings);
        yield return ("Component tryCollapseToString with style returns null", TestTryCollapseWithStyle);
        yield return ("Component getString limit", TestGetStringLimit);
        yield return ("Component append sibling", TestAppendSibling);
        yield return ("Component toFlatList", TestToFlatList);
        yield return ("Component contains", TestContains);
        yield return ("Component plainCopy", TestPlainCopy);
        yield return ("Component copy deep", TestCopyDeep);
        yield return ("Component translatable", TestTranslatable);
        yield return ("Component keybind", TestKeybind);
        yield return ("Component implements IMessage", TestImplementsIMessage);

        //Style
        yield return ("Style empty is empty", TestStyleEmpty);
        yield return ("Style with color", TestStyleWithColor);
        yield return ("Style with bold", TestStyleWithBold);
        yield return ("Style applyFormat ChatFormatting", TestStyleApplyFormat);
        yield return ("Style applyFormats multiple", TestStyleApplyFormats);
        yield return ("Style applyTo merge", TestStyleApplyTo);
        yield return ("Style applyLegacyFormat resets", TestStyleApplyLegacyFormat);
        yield return ("Style withFont", TestStyleWithFont);
        yield return ("Style withClickEvent", TestStyleWithClickEvent);
        yield return ("Style withoutShadow", TestStyleWithoutShadow);

        //TextColor
        yield return ("TextColor named black value", TestTextColorNamedBlack);
        yield return ("TextColor fromRgb", TestTextColorFromRgb);
        yield return ("TextColor parseColor named", TestTextColorParseColorNamed);
        yield return ("TextColor parseColor hex", TestTextColorParseColorHex);
        yield return ("TextColor parseColor invalid returns null", TestTextColorParseColorInvalid);
        yield return ("TextColor serialize named", TestTextColorSerializeNamed);
        yield return ("TextColor serialize hex", TestTextColorSerializeHex);
        yield return ("TextColor fromLegacyFormat", TestTextColorFromLegacyFormat);
        yield return ("TextColor equals by value", TestTextColorEqualsByValue);

        //ChatFormatting
        yield return ("ChatFormatting get by code", TestChatFormattingGetByCode);
        yield return ("ChatFormatting get by code invalid", TestChatFormattingGetByCodeInvalid);
        yield return ("ChatFormatting get by name", TestChatFormattingGetByName);
        yield return ("ChatFormatting strip formatting", TestChatFormattingStripFormatting);
        yield return ("ChatFormatting toString prefix", TestChatFormattingToString);

        //FormattedText
        yield return ("FormattedText of text visit", TestFormattedTextVisit);
        yield return ("FormattedText composite visit", TestFormattedTextCompositeVisit);
        yield return ("FormattedText empty visit", TestFormattedTextEmptyVisit);

        //CommonComponents
        yield return ("CommonComponents empty", TestCommonComponentsEmpty);
        yield return ("CommonComponents newLine", TestCommonComponentsNewLine);

        //ComponentSerialization
        yield return ("ComponentSerialization plain text json round-trip", TestSerializationPlainText);
        yield return ("ComponentSerialization styled json round-trip", TestSerializationStyled);
        yield return ("ComponentSerialization siblings json round-trip", TestSerializationSiblings);
        yield return ("ComponentSerialization translatable json round-trip", TestSerializationTranslatable);
        yield return ("ComponentSerialization string optimization", TestSerializationStringOptimization);
        yield return ("ComponentSerialization stream codec round-trip", TestSerializationStreamCodec);
    }

    //Component 测试

    private static bool TestLiteralGetString()
    {
        var component = Component.Literal("hello");
        return component.GetString() == "hello";
    }

    private static bool TestEmptyGetString()
    {
        var component = Component.Empty();
        return component.GetString() == "";
    }

    private static bool TestNullToEmpty()
    {
        var fromNull = Component.NullToEmpty(null);
        var fromText = Component.NullToEmpty("text");
        return fromNull == CommonComponents.Empty && fromText.GetString() == "text";
    }

    private static bool TestTryCollapseLiteral()
    {
        var component = Component.Literal("collapsed");
        return component.TryCollapseToString() == "collapsed";
    }

    private static bool TestTryCollapseWithSiblings()
    {
        var component = Component.Literal("a").Append("b");
        return component.TryCollapseToString() is null;
    }

    private static bool TestTryCollapseWithStyle()
    {
        var component = Component.Literal("a").WithColor(0xFF0000);
        return component.TryCollapseToString() is null;
    }

    private static bool TestGetStringLimit()
    {
        var component = Component.Literal("hello world");
        return component.GetString(5) == "hello";
    }

    private static bool TestAppendSibling()
    {
        var component = Component.Literal("hello").Append(" ").Append("world");
        return component.GetString() == "hello world" && component.Siblings.Count == 2;
    }

    private static bool TestToFlatList()
    {
        var component = Component.Literal("a").Append(Component.Literal("b").WithColor(0xFF0000));
        var list = component.ToFlatList();
        return list.Count == 2 && list[0].GetString() == "a" && list[1].GetString() == "b";
    }

    private static bool TestContains()
    {
        //contains是子组件序列匹配需把outer拆成多段兄弟
        var outer = Component.Literal("hello").Append(" ").Append("world");
        var inner = Component.Literal("hello");
        return outer.Contains(inner);
    }

    private static bool TestPlainCopy()
    {
        var component = Component.Literal("a").Append("b").WithColor(0xFF0000);
        var plain = component.PlainCopy();
        //plainCopy只复制内容不含兄弟和样式GetString应为a
        return plain.GetString() == "a" && plain.Style.IsEmpty && plain.Siblings.Count == 0;
    }

    private static bool TestCopyDeep()
    {
        var component = Component.Literal("a").Append("b").WithColor(0xFF0000);
        var copy = component.Copy();
        return copy.GetString() == "ab"
            && copy.Style.Color == TextColor.FromRgb(0xFF0000)
            && copy.Siblings.Count == 1;
    }

    private static bool TestTranslatable()
    {
        var component = Component.Translatable("key.test");
        var contents = (TranslatableContents)component.Contents;
        return contents.Key == "key.test" && contents.Args.Length == 0;
    }

    private static bool TestKeybind()
    {
        var component = Component.Keybind("key.forward");
        var contents = (KeybindContents)component.Contents;
        return contents.Name == "key.forward";
    }

    private static bool TestImplementsIMessage()
    {
        var component = Component.Literal("hello");
        NetCraft.Commands.IMessage message = component;
        return message.GetString() == "hello";
    }

    //Style 测试

    private static bool TestStyleEmpty()
    {
        return Style.Empty.IsEmpty && Style.Empty.Color is null && !Style.Empty.IsBold;
    }

    private static bool TestStyleWithColor()
    {
        var style = Style.Empty.WithColor(TextColor.Red);
        return style.Color == TextColor.Red && !style.IsEmpty;
    }

    private static bool TestStyleWithBold()
    {
        var style = Style.Empty.WithBold(true);
        return style.IsBold && !style.IsEmpty;
    }

    private static bool TestStyleApplyFormat()
    {
        var style = Style.Empty.ApplyFormat(ChatFormatting.Bold);
        return style.IsBold;
    }

    private static bool TestStyleApplyFormats()
    {
        var style = Style.Empty.ApplyFormats(ChatFormatting.Bold, ChatFormatting.Italic);
        return style.IsBold && style.IsItalic;
    }

    private static bool TestStyleApplyTo()
    {
        var baseStyle = Style.Empty.WithColor(TextColor.Red);
        var overlay = Style.Empty.WithBold(true);
        var merged = baseStyle.ApplyTo(overlay);
        return merged.Color == TextColor.Red && merged.IsBold;
    }

    private static bool TestStyleApplyLegacyFormat()
    {
        var style = Style.Empty.WithBold(true).ApplyLegacyFormat(ChatFormatting.Red);
        return !style.IsBold && style.Color == TextColor.Red;
    }

    private static bool TestStyleWithFont()
    {
        var font = new FontDescription.Resource(NetCraft.Registry.Identifier.WithDefaultNamespace("alt"));
        var style = Style.Empty.WithFont(font);
        return style.Font.Equals(font);
    }

    private static bool TestStyleWithClickEvent()
    {
        var click = new ClickEvent.OpenUrl(new Uri("https://example.com"));
        var style = Style.Empty.WithClickEvent(click);
        return style.ClickEvent == click;
    }

    private static bool TestStyleWithoutShadow()
    {
        var style = Style.Empty.WithShadowColor(0xFF0000).WithoutShadow();
        return style.ShadowColor == 0;
    }

    //TextColor 测试

    private static bool TestTextColorNamedBlack()
    {
        return TextColor.Black.Value == 0 && TextColor.Black.Serialize() == "black";
    }

    private static bool TestTextColorFromRgb()
    {
        var color = TextColor.FromRgb(0xFF0000);
        return color.Value == 0xFF0000 && color.Serialize() == "#FF0000";
    }

    private static bool TestTextColorParseColorNamed()
    {
        var color = TextColor.ParseColor("red");
        return color == TextColor.Red;
    }

    private static bool TestTextColorParseColorHex()
    {
        var color = TextColor.ParseColor("#00FF00");
        return color is not null && color.Value == 0x00FF00;
    }

    private static bool TestTextColorParseColorInvalid()
    {
        return TextColor.ParseColor("invalid") is null && TextColor.ParseColor("#GGGGGG") is null;
    }

    private static bool TestTextColorSerializeNamed()
    {
        return TextColor.Blue.Serialize() == "blue";
    }

    private static bool TestTextColorSerializeHex()
    {
        var color = TextColor.FromRgb(0x123456);
        return color.Serialize() == "#123456";
    }

    private static bool TestTextColorFromLegacyFormat()
    {
        return TextColor.FromLegacyFormat(ChatFormatting.Red) == TextColor.Red;
    }

    private static bool TestTextColorEqualsByValue()
    {
        var c1 = TextColor.FromRgb(0xFF0000);
        var c2 = TextColor.FromRgb(0xFF0000);
        return c1.Equals(c2) && c1 == c2;
    }

    //ChatFormatting 测试

    private static bool TestChatFormattingGetByCode()
    {
        return ChatFormatting.GetByCode('c') == ChatFormatting.Red
            && ChatFormatting.GetByCode('C') == ChatFormatting.Red;
    }

    private static bool TestChatFormattingGetByCodeInvalid()
    {
        return ChatFormatting.GetByCode('z') is null;
    }

    private static bool TestChatFormattingGetByName()
    {
        return ChatFormatting.GetByName("red") == ChatFormatting.Red;
    }

    private static bool TestChatFormattingStripFormatting()
    {
        var input = "\u00a7chello\u00a7r world";
        return ChatFormatting.StripFormatting(input) == "hello world";
    }

    private static bool TestChatFormattingToString()
    {
        return ChatFormatting.Red.ToString() == "\u00a7c";
    }

    //FormattedText 测试

    private static bool TestFormattedTextVisit()
    {
        var text = FormattedText.Of("hello");
        var builder = new StringBuilder();
        text.Visit(contents =>
        {
            builder.Append(contents);
            return NetCraft.Codec.Optional<object>.Empty();
        });
        return builder.ToString() == "hello";
    }

    private static bool TestFormattedTextCompositeVisit()
    {
        var text = FormattedText.Composite(FormattedText.Of("a"), FormattedText.Of("b"));
        var builder = new StringBuilder();
        text.Visit(contents =>
        {
            builder.Append(contents);
            return NetCraft.Codec.Optional<object>.Empty();
        });
        return builder.ToString() == "ab";
    }

    private static bool TestFormattedTextEmptyVisit()
    {
        var builder = new StringBuilder();
        FormattedText.EMPTY.Visit(contents =>
        {
            builder.Append(contents);
            return NetCraft.Codec.Optional<object>.Empty();
        });
        return builder.Length == 0;
    }

    //CommonComponents 测试

    private static bool TestCommonComponentsEmpty()
    {
        return CommonComponents.Empty.GetString() == "";
    }

    private static bool TestCommonComponentsNewLine()
    {
        return CommonComponents.NewLine.GetString() == "\n";
    }

    //ComponentSerialization 测试

    private static bool TestSerializationPlainText()
    {
        var component = Component.Literal("hello");
        var json = ComponentSerialization.ToJson(component);
        var restored = ComponentSerialization.FromJson(json);
        return json == "\"hello\"" && restored.GetString() == "hello";
    }

    private static bool TestSerializationStyled()
    {
        var component = Component.Literal("hello").WithColor(TextColor.Red).WithBold(true);
        var json = ComponentSerialization.ToJson(component);
        var restored = ComponentSerialization.FromJson(json);
        return restored.GetString() == "hello"
            && restored.Style.Color == TextColor.Red
            && restored.Style.IsBold;
    }

    private static bool TestSerializationSiblings()
    {
        var component = Component.Literal("a").Append(Component.Literal("b").WithColor(TextColor.Blue));
        var json = ComponentSerialization.ToJson(component);
        var restored = ComponentSerialization.FromJson(json);
        return restored.GetString() == "ab"
            && restored.Siblings.Count == 1
            && restored.Siblings[0].Style.Color == TextColor.Blue;
    }

    private static bool TestSerializationTranslatable()
    {
        var component = Component.Translatable("key.test");
        var json = ComponentSerialization.ToJson(component);
        var restored = ComponentSerialization.FromJson(json);
        var contents = restored.Contents as TranslatableContents;
        return contents is not null && contents.Key == "key.test";
    }

    private static bool TestSerializationStringOptimization()
    {
        //纯文本无样式无兄弟应优化为JSON字符串
        var component = Component.Literal("plain");
        var json = ComponentSerialization.ToJson(component);
        return json == "\"plain\"";
    }

    private static bool TestSerializationStreamCodec()
    {
        var component = Component.Literal("hello").Append(" ").Append("world").WithColor(TextColor.Green);
        var stream = new MemoryStream();
        var buf = new FriendlyByteBuf(stream, false);
        ComponentSerialization.StreamCodec.Encode(buf, component);
        stream.Position = 0;
        var restored = ComponentSerialization.StreamCodec.Decode(buf);
        return restored.GetString() == "hello world"
            && restored.Style.Color == TextColor.Green
            && restored.Siblings.Count == 2;
    }
}
