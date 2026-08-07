using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//翻译内容对应原版net.minecraft.network.chat.contents.TranslatableContents
//key + fallback + args 三元组翻译模板运行时按 Language 实例 decompose
public sealed class TranslatableContents : ComponentContents
{
    public string Key { get; }
    public string? Fallback { get; }
    public object[] Args { get; }

    //NO_ARGS 空参数数组对应原版 NO_ARGS
    public static readonly object[] NoArgs = Array.Empty<object>();

    public TranslatableContents(string key, string? fallback, object[] args)
    {
        Key = key;
        Fallback = fallback;
        Args = args;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not TranslatableContents that) return false;
        return Key == that.Key && Fallback == that.Fallback && Args.SequenceEqual(that.Args);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Fallback);
        foreach (var arg in Args) hash.Add(arg);
        return hash.ToHashCode();
    }

    public override string ToString()
        => $"translation{{key='{Key}'{(Fallback is not null ? $", fallback='{Fallback}'" : "")}, args={string.Join(",", Args)}}}";
}
