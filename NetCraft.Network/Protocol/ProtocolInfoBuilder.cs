namespace NetCraft.Network.Protocol;

//ProtocolInfoBuilder 协议信息构建器对应原版 net.minecraft.network.protocol.ProtocolInfoBuilder
//按协议+方向注册 PacketType → StreamCodec 映射最后构建 SimpleUnboundProtocol
//THandler 是包处理器类型所有注册的包都继承 Packet<THandler>
public sealed class ProtocolInfoBuilder<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<CodecEntry> _codecs = new();
    private BundlerInfo<THandler>? _bundlerInfo;

    public ProtocolInfoBuilder(ConnectionProtocol protocol, FlowDirection flow)
    {
        _protocol = protocol;
        _flow = flow;
    }

    //AddPacket 注册包类型和对应编解码器无 CodecModifier
    //serializer 接受 RegistryFriendlyByteBuf 因 StreamCodec B 逆变 FriendlyByteBuf codec 可隐式传入
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
    {
        _codecs.Add(new CodecEntry(type, WrapCodec(serializer)));
        return this;
    }

    //AddPacket 注册包类型和对应编解码器带 CodecModifier
    public ProtocolInfoBuilder<THandler> AddPacket<TPacket>(
        PacketType<THandler> type,
        StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer,
        CodecModifier<RegistryFriendlyByteBuf, TPacket, object> modifier)
        where TPacket : Packet<THandler>
    {
        //简化版 CodecModifier 暂不应用直接用原始 serializer
        _codecs.Add(new CodecEntry(type, WrapCodec(serializer)));
        return this;
    }

    //WithBundlePacket 注册 bundle 包类型分隔符包和打包信息
    public ProtocolInfoBuilder<THandler> WithBundlePacket<TBundle>(
        PacketType<THandler> bundlerPacketType,
        Func<IEnumerable<Packet<THandler>>, TBundle> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
        where TBundle : BundlePacket<THandler>
    {
        _codecs.Add(new CodecEntry(delimiterPacket.Type, WrapCodec(CreateUnitCodec(delimiterPacket))));
        _bundlerInfo = BundlerInfo<THandler>.CreateForPacket(
            bundlerPacketType,
            packets => constructor(packets),
            delimiterPacket);
        return this;
    }

    //BuildUnbound 构建未绑定协议返回 SimpleUnboundProtocol
    public SimpleUnboundProtocol<THandler> BuildUnbound()
    {
        var listCopy = _codecs.ToList();
        var bundlerInfo = _bundlerInfo;
        var details = new DetailsImpl<THandler>(_protocol, _flow, listCopy);
        return new SimpleUnboundProtocolImpl<THandler>(_protocol, _flow, listCopy, bundlerInfo, details);
    }

    //CreateUnitCodec 创建恒定值编解码器对应原版 StreamCodec.unit
    private static StreamCodec<RegistryFriendlyByteBuf, TPacket> CreateUnitCodec<TPacket>(TPacket instance)
        where TPacket : Packet<THandler>
        => new UnitStreamCodec<RegistryFriendlyByteBuf, TPacket>(instance);

    //WrapCodec 把子包编解码器包装为 Packet<THandler> 编解码器
    private static StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> WrapCodec<TPacket>(StreamCodec<RegistryFriendlyByteBuf, TPacket> serializer)
        where TPacket : Packet<THandler>
        => new WrappedCodec<TPacket, THandler>(serializer);

    public readonly struct CodecEntry
    {
        public PacketType<THandler> Type { get; }
        public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Serializer { get; }
        public CodecEntry(PacketType<THandler> type, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> serializer)
        {
            Type = type;
            Serializer = serializer;
        }
    }
}

//DetailsImpl ProtocolInfo.Details 实现
file sealed class DetailsImpl<THandler> : ProtocolInfo<THandler>.Details
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<ProtocolInfoBuilder<THandler>.CodecEntry> _codecs;

    public DetailsImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        List<ProtocolInfoBuilder<THandler>.CodecEntry> codecs)
    {
        _protocol = protocol;
        _flow = flow;
        _codecs = codecs;
    }

    public ConnectionProtocol Id => _protocol;
    public Protocol.PacketFlow Flow => _flow.ToPacketFlow();

    public void ListPackets(ProtocolInfo<THandler>.Details.PacketVisitor output)
    {
        for (int i = 0; i < _codecs.Count; i++)
        {
            var entry = _codecs[i];
            output.Accept(entry.Type, entry.Type.Id);
        }
    }
}

//SimpleUnboundProtocolImpl SimpleUnboundProtocol 实现
file sealed class SimpleUnboundProtocolImpl<THandler> : SimpleUnboundProtocol<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly List<ProtocolInfoBuilder<THandler>.CodecEntry> _codecs;
    private readonly BundlerInfo<THandler>? _bundlerInfo;
    private readonly ProtocolInfo<THandler>.Details _details;

    public SimpleUnboundProtocolImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        List<ProtocolInfoBuilder<THandler>.CodecEntry> codecs,
        BundlerInfo<THandler>? bundlerInfo,
        ProtocolInfo<THandler>.Details details)
    {
        _protocol = protocol;
        _flow = flow;
        _codecs = codecs;
        _bundlerInfo = bundlerInfo;
        _details = details;
    }

    public ProtocolInfo<THandler>.Details Details => _details;

    //Bind 构建 ProtocolInfo 直接构造 IdDispatchStreamCodec
    public ProtocolInfo<THandler> Bind()
    {
        var byId = new Dictionary<int, (PacketType<THandler>, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>)>();
        foreach (var entry in _codecs)
            byId[entry.Type.Id] = (entry.Type, entry.Serializer);
        var codec = new IdDispatchStreamCodec<THandler>(byId);
        return new ProtocolInfoImpl<THandler>(_protocol, _flow, codec, _bundlerInfo);
    }
}

//ProtocolInfoImpl ProtocolInfo 实现
file sealed class ProtocolInfoImpl<THandler> : ProtocolInfo<THandler>
{
    private readonly ConnectionProtocol _protocol;
    private readonly FlowDirection _flow;
    private readonly StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> _codec;
    private readonly BundlerInfo<THandler>? _bundlerInfo;

    public ProtocolInfoImpl(
        ConnectionProtocol protocol,
        FlowDirection flow,
        StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> codec,
        BundlerInfo<THandler>? bundlerInfo)
    {
        _protocol = protocol;
        _flow = flow;
        _codec = codec;
        _bundlerInfo = bundlerInfo;
    }

    public ConnectionProtocol Id => _protocol;
    public Protocol.PacketFlow Flow => _flow.ToPacketFlow();
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Codec => _codec;
    public BundlerInfo<THandler> BundlerInfo => _bundlerInfo ?? EmptyBundlerInfo<THandler>.Instance;
}

//EmptyBundlerInfo 无 bundle 时使用的空 BundlerInfo
file sealed class EmptyBundlerInfo<THandler> : BundlerInfo<THandler>
{
    public static EmptyBundlerInfo<THandler> Instance { get; } = new();

    public void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output)
        => output(packet);

    public BundlerInfo<THandler>.Bundler<THandler>? StartPacketBundling(Packet<THandler> packet)
        => null;
}
