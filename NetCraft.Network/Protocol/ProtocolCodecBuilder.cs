namespace NetCraft.Network.Protocol;

//ProtocolCodecBuilder 协议编解码器构建器对应原版 net.minecraft.network.protocol.ProtocolCodecBuilder
//按方向注册 PacketType → StreamCodec 映射最后构建 IdDispatchCodec
//THandler 是包处理器类型所有注册的包都继承 Packet<THandler>
public sealed class ProtocolCodecBuilder<THandler>
{
    private readonly FlowDirection _flow;
    private readonly List<Entry> _entries = new();

    public ProtocolCodecBuilder(FlowDirection flow)
    {
        _flow = flow;
    }

    //Add 注册包类型和对应编解码器
    //type.Direction 必须与构建器 Flow 一致否则抛异常
    //TPacket 必须继承 Packet<THandler>
    //serializer 接受 RegistryFriendlyByteBuf 因 StreamCodec B 逆变 FriendlyByteBuf codec 可隐式传入
    public ProtocolCodecBuilder<THandler> Add<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        if (type.Direction != _flow)
            throw new ArgumentException($"包 {type} 方向不匹配，期望 {_flow}");
        _entries.Add(new Entry(type, WrapCodec(serializer)));
        return this;
    }

    //Build 构建 IdDispatchStreamCodec 包网络 ID 按 type.Id 分配
    //返回按 VarInt 网络 ID 分发的编解码器
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Build()
    {
        var byId = new Dictionary<int, (PacketType<THandler> Type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer)>();
        foreach (var entry in _entries)
            byId[entry.Type.Id] = (entry.Type, entry.Serializer);
        return new IdDispatchStreamCodec<THandler>(byId);
    }

    //WrapCodec 把子包编解码器包装为 Packet<THandler> 编解码器
    //解决 C# 泛型不协变问题 TPacket → Packet<THandler>
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    private readonly struct Entry
    {
        public PacketType<THandler> Type { get; }
        public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer { get; }
        public Entry(PacketType<THandler> type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> serializer)
        {
            Type = type;
            Serializer = serializer;
        }
    }
}

//WrappedCodec 把 StreamCodec<RegistryFriendlyByteBuf, TPacket> 包装为 StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>
//TPacket : Packet<THandler> 强制 cast 安全
//internal 跨 Protocol 子命名空间文件可见
internal sealed class WrappedCodec<TPacket, THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>
    where TPacket : Packet<THandler>
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, TPacket> _inner;

    public WrappedCodec(StreamCodec<RegistryFriendlyByteBuf, TPacket> inner) => _inner = inner;

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf) => _inner.Decode(buf);

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
        => _inner.Encode(buf, (TPacket)value);
}

//IdDispatchStreamCodec 按 VarInt 网络 ID 分发的编解码器
//对应原版 net.minecraft.network.codec.IdDispatchCodec
//解码读 VarInt ID 查表调用子编解码器解码
//编码先查 Packet 的 Type 再用对应子编解码器编码
public sealed class IdDispatchStreamCodec<THandler> : StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>
{
    private readonly Dictionary<int, (PacketType<THandler> Type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer)> _byId;

    public IdDispatchStreamCodec(Dictionary<int, (PacketType<THandler>, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>)> byId)
    {
        _byId = byId;
    }

    public Packet<THandler> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        if (!_byId.TryGetValue(id, out var entry))
            throw new IOException($"未知包网络 ID {id}");
        return entry.Serializer.Decode(buf);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Packet<THandler> value)
    {
        var type = value.Type;
        buf.WriteVarInt(type.Id);
        if (!_byId.TryGetValue(type.Id, out var entry))
            throw new IOException($"包类型 {type} 未注册");
        entry.Serializer.Encode(buf, value);
    }
}
