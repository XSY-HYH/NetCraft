using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;

//StringReader 别名避免与 System.IO.StringReader 冲突
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Test.Modules;

//Commands 子库测试对应brigadier官方测试套件
//覆盖StringReader异常体系注册解析执行SuggestionRedirectFork路径查找与歧义检测
internal static class CommandsTests
{
    public const string Module = "commands";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        //StringReader 基础
        yield return ("StringReader read unquoted word", TestReadUnquotedWord);
        yield return ("StringReader read quoted string", TestReadQuotedString);
        yield return ("StringReader read escaped quote", TestReadEscapedQuote);
        yield return ("StringReader read int", TestReadInt);
        yield return ("StringReader read long", TestReadLong);
        yield return ("StringReader read double", TestReadDouble);
        yield return ("StringReader read float", TestReadFloat);
        yield return ("StringReader read boolean true", TestReadBooleanTrue);
        yield return ("StringReader read boolean false case sensitive", TestReadBooleanFalseCaseSensitive);
        yield return ("StringReader expect symbol", TestExpectSymbol);
        yield return ("StringReader peek and skip whitespace", TestPeekSkipWhitespace);

        //异常体系
        yield return ("CommandSyntaxException built in int too low", TestBuiltInIntTooLow);
        yield return ("SimpleCommandExceptionType create with context", TestSimpleExceptionWithContext);
        yield return ("DynamicCommandExceptionType create with arg", TestDynamicExceptionWithArg);

        //注册解析
        yield return ("Dispatcher register literal", TestRegisterLiteral);
        yield return ("Dispatcher parse argument value", TestParseArgument);
        yield return ("Dispatcher parse mixed literal and argument", TestParseMixed);
        yield return ("Dispatcher parse unknown command throws", TestParseUnknownCommand);
        yield return ("Dispatcher parse unknown argument throws", TestParseUnknownArgument);

        //执行
        yield return ("Dispatcher execute callback returns SingleSuccess", TestExecuteCallback);
        yield return ("Dispatcher execute with argument passes value", TestExecuteWithArgument);
        yield return ("Dispatcher execute source propagates", TestExecuteSourcePropagates);

        //错误路径
        yield return ("IntegerArgumentType out of range throws", TestIntegerOutOfRange);
        yield return ("BoolArgumentType parse invalid throws", TestBoolInvalidThrows);
        yield return ("StringArgumentType greedy reads remaining", TestStringGreedy);

        //Suggestion
        yield return ("Dispatcher suggest literal prefix", TestSuggestLiteral);
        yield return ("Dispatcher suggest argument integers", TestSuggestInteger);
        yield return ("SuggestionsBuilder add skips same as remaining", TestSuggestionsBuilderSkipSame);

        //Redirect/Fork
        yield return ("Dispatcher redirect to existing node", TestRedirect);
        yield return ("Dispatcher fork expands multiple sources", TestFork);

        //findNode/getPath
        yield return ("Dispatcher getPath and findNode roundtrip", TestGetPathFindNode);

        //findAmbiguities
        yield return ("Dispatcher findAmbiguities detects overlap", TestFindAmbiguities);
    }

    //StringReader 测试

    private static bool TestReadUnquotedWord()
    {
        var reader = new StringReader("hello world");
        return reader.ReadUnquotedString() == "hello";
    }

    private static bool TestReadQuotedString()
    {
        var reader = new StringReader("\"hello world\"");
        return reader.ReadString() == "hello world";
    }

    private static bool TestReadEscapedQuote()
    {
        var reader = new StringReader("\"say \\\"hi\\\"\"");
        return reader.ReadString() == "say \"hi\"";
    }

    private static bool TestReadInt()
    {
        var reader = new StringReader("42");
        return reader.ReadInt() == 42;
    }

    private static bool TestReadLong()
    {
        var reader = new StringReader("12345678901");
        return reader.ReadLong() == 12345678901L;
    }

    private static bool TestReadDouble()
    {
        var reader = new StringReader("3.14");
        return Math.Abs(reader.ReadDouble() - 3.14) < 0.0001;
    }

    private static bool TestReadFloat()
    {
        var reader = new StringReader("2.71");
        return Math.Abs(reader.ReadFloat() - 2.71f) < 0.001f;
    }

    private static bool TestReadBooleanTrue()
    {
        var reader = new StringReader("true");
        return reader.ReadBoolean();
    }

    private static bool TestReadBooleanFalseCaseSensitive()
    {
        var reader = new StringReader("FALSE");
        try
        {
            reader.ReadBoolean();
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestExpectSymbol()
    {
        var reader = new StringReader("foo:bar");
        reader.ReadUnquotedString();
        try
        {
            reader.Expect(':');
            return reader.Cursor == 4;
        }
        catch (CommandSyntaxException)
        {
            return false;
        }
    }

    private static bool TestPeekSkipWhitespace()
    {
        var reader = new StringReader("  foo");
        reader.SkipWhitespace();
        return reader.Peek() == 'f';
    }

    //异常体系测试

    private static bool TestBuiltInIntTooLow()
    {
        var type = CommandSyntaxException.BuiltInExceptions.IntegerTooLow();
        var ex = type.Create(5, 10);
        return ex.Message.Contains("Integer must not be less than 10");
    }

    private static bool TestSimpleExceptionWithContext()
    {
        var type = new SimpleCommandExceptionType(new LiteralMessage("bad input"));
        var reader = new StringReader("foo bar");
        reader.ReadUnquotedString();
        var ex = type.CreateWithContext(reader);
        return ex.Cursor == 3 && ex.Input == "foo bar";
    }

    private static bool TestDynamicExceptionWithArg()
    {
        var type = new DynamicCommandExceptionType(arg => new LiteralMessage("unknown " + arg));
        var ex = type.Create("thing");
        return ex.Message == "unknown thing";
    }

    //注册解析测试

    private static bool TestRegisterLiteral()
    {
        var dispatcher = new CommandDispatcher<object>();
        var node = dispatcher.Register(LiteralArgumentBuilder<object>.Literal("say"));
        return dispatcher.GetRoot().GetChildren().Count == 1
            && dispatcher.GetRoot().GetChild("say") == node;
    }

    private static bool TestParseArgument()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("say")
            .Then(RequiredArgumentBuilder<object, int>.Argument("count", IntegerArgumentType.Integer(0, 100))));
        var parse = dispatcher.Parse("say 42", new object());
        var context = parse.GetContext().Build(parse.GetReader().String);
        return parse.GetReader().CanRead() == false
            && context.GetArgument<int>("count") == 42;
    }

    private static bool TestParseMixed()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("give")
            .Then(LiteralArgumentBuilder<object>.Literal("to")
                .Then(RequiredArgumentBuilder<object, int>.Argument("count", IntegerArgumentType.Integer(0, 64))
                    .Then(RequiredArgumentBuilder<object, string>.Argument("name", StringArgumentType.Word())))));
        var parse = dispatcher.Parse("give to 5 sword", new object());
        var context = parse.GetContext().Build(parse.GetReader().String);
        return !parse.GetReader().CanRead()
            && context.GetArgument<int>("count") == 5
            && context.GetArgument<string>("name") == "sword";
    }

    private static bool TestParseUnknownCommand()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("foo").Executes(_ => 1));
        try
        {
            dispatcher.Execute("bar", new object());
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestParseUnknownArgument()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("foo").Executes(_ => 1));
        try
        {
            dispatcher.Execute("foo bar", new object());
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    //执行测试

    private static bool TestExecuteCallback()
    {
        var dispatcher = new CommandDispatcher<object>();
        int captured = 0;
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("ping")
            .Executes(ctx => { captured = 42; return CommandConstants.SingleSuccess; }));
        var result = dispatcher.Execute("ping", new object());
        return result == 1 && captured == 42;
    }

    private static bool TestExecuteWithArgument()
    {
        var dispatcher = new CommandDispatcher<object>();
        int capturedCount = 0;
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("say")
            .Then(RequiredArgumentBuilder<object, int>.Argument("count", IntegerArgumentType.Integer(0, 100))
                .Executes(ctx => { capturedCount = ctx.GetArgument<int>("count"); return 1; })));
        var result = dispatcher.Execute("say 7", new object());
        return result == 1 && capturedCount == 7;
    }

    private static bool TestExecuteSourcePropagates()
    {
        var dispatcher = new CommandDispatcher<object>();
        var source = new object();
        object captured = new object();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("src")
            .Executes(ctx => { captured = ctx.GetSource(); return 1; }));
        dispatcher.Execute("src", source);
        return ReferenceEquals(source, captured);
    }

    //错误路径测试

    private static bool TestIntegerOutOfRange()
    {
        var reader = new StringReader("300");
        var arg = IntegerArgumentType.Integer(0, 200);
        try
        {
            arg.Parse(reader);
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestBoolInvalidThrows()
    {
        var reader = new StringReader("maybe");
        try
        {
            BoolArgumentType.Bool().Parse(reader);
            return false;
        }
        catch (CommandSyntaxException)
        {
            return true;
        }
    }

    private static bool TestStringGreedy()
    {
        var reader = new StringReader("hello world with spaces");
        reader.ReadUnquotedString();
        reader.SkipWhitespace();
        var value = StringArgumentType.GreedyString().Parse(reader);
        return value == "world with spaces";
    }

    //Suggestion 测试

    private static bool TestSuggestLiteral()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("foo").Executes(_ => 1));
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("bar").Executes(_ => 1));
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("baz").Executes(_ => 1));
        var parse = dispatcher.Parse("b", new object());
        var suggestions = dispatcher.GetCompletionSuggestions(parse).Result;
        var texts = suggestions.List.Select(s => s.Text).ToList();
        return texts.Count == 2 && texts.Contains("bar") && texts.Contains("baz");
    }

    private static bool TestSuggestInteger()
    {
        var dispatcher = new CommandDispatcher<object>();
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("count")
            .Then(RequiredArgumentBuilder<object, int>.Argument("value", IntegerArgumentType.Integer(0, 10))
                .Suggests((ctx, builder) =>
                {
                    builder.Add(1);
                    builder.Add(2);
                    builder.Add(3);
                    return builder.BuildFuture();
                })));
        var parse = dispatcher.Parse("count ", new object());
        var suggestions = dispatcher.GetCompletionSuggestions(parse).Result;
        return suggestions.List.Count == 3 && suggestions.List.Any(s => s.Text == "1");
    }

    private static bool TestSuggestionsBuilderSkipSame()
    {
        var builder = new SuggestionsBuilder("foo", "foo".ToLowerInvariant(), 0);
        builder.Add("foo");
        return builder.Build().List.Count == 0;
    }

    //Redirect/Fork 测试

    private static bool TestRedirect()
    {
        var dispatcher = new CommandDispatcher<object>();
        var target = dispatcher.Register(LiteralArgumentBuilder<object>.Literal("target")
            .Then(RequiredArgumentBuilder<object, int>.Argument("value", IntegerArgumentType.Integer())
                .Executes(ctx => ctx.GetArgument<int>("value"))));
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("go")
            .Redirect(target));
        return dispatcher.Execute("go 42", new object()) == 42;
    }

    private static bool TestFork()
    {
        var dispatcher = new CommandDispatcher<object>();
        var count = new[] { 0 };
        var target = dispatcher.Register(LiteralArgumentBuilder<object>.Literal("target")
            .Then(RequiredArgumentBuilder<object, int>.Argument("value", IntegerArgumentType.Integer())
                .Executes(ctx => { count[0]++; return CommandConstants.SingleSuccess; })));
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("fork")
            .Fork(target, ctx => new object[] { new(), new(), new() }));
        var result = dispatcher.Execute("fork 42", new object());
        return result == 3 && count[0] == 3;
    }

    //findNode/getPath 测试

    private static bool TestGetPathFindNode()
    {
        var dispatcher = new CommandDispatcher<object>();
        var node = dispatcher.Register(LiteralArgumentBuilder<object>.Literal("foo")
            .Then(LiteralArgumentBuilder<object>.Literal("bar")
                .Then(LiteralArgumentBuilder<object>.Literal("baz").Executes(_ => 1))));

        var barNode = dispatcher.FindNode(new[] { "foo", "bar" });
        if (barNode == null || barNode.GetName() != "bar") return false;

        var path = dispatcher.GetPath(node);
        return path.Count == 1 && path[0] == "foo";
    }

    //findAmbiguities 测试

    private static bool TestFindAmbiguities()
    {
        var dispatcher = new CommandDispatcher<object>();
        //StringArgumentType.Word 和 IntegerArgumentType 都能匹配 "123" 触发歧义
        dispatcher.Register(LiteralArgumentBuilder<object>.Literal("cmd")
            .Then(RequiredArgumentBuilder<object, string>.Argument("name", StringArgumentType.Word())
                .Executes(_ => 1))
            .Then(RequiredArgumentBuilder<object, int>.Argument("id", IntegerArgumentType.Integer())
                .Executes(_ => 1)));

        var ambiguities = new List<(CommandNode<object> parent, CommandNode<object> child, CommandNode<object> sibling, ICollection<string> inputs)>();
        dispatcher.FindAmbiguities((parent, child, sibling, inputs) =>
            ambiguities.Add((parent, child, sibling, inputs)));

        return ambiguities.Count > 0;
    }
}
