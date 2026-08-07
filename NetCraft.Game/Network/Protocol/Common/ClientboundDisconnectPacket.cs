namespace NetCraft.Game.Network.Protocol.Common;

//ClientboundDisconnectPacket 客户端断开连接包对应原版 net.minecraft.network.protocol.common.ClientboundDisconnectPacket
//含 reason 断开原因原版用 Component 简化版用 string
//IsTerminal true 表示断开后连接关闭
public sealed record ClientboundDisconnectPacket(string Reason) : Packet<ClientCommonPacketListener>
{
    //MaxReasonLength reason 字符串最大长度对齐 FriendlyByteBuf.MAX_COMPONENT_STRING_LENGTH
    public const int MaxReasonLength = 262144;

    public static StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket> StreamCodec { get; } = new DisconnectCodec();

    public PacketType<ClientCommonPacketListener> Type => ConfigurationPacketTypes.ClientboundDisconnect;

    //IsTerminal 断开包后连接关闭
    public bool IsTerminal => true;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleDisconnect(this);

    private sealed class DisconnectCodec : StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket>
    {
        public ClientboundDisconnectPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxReasonLength));

        public void Encode(FriendlyByteBuf buf, ClientboundDisconnectPacket value)
            => buf.WriteString(value.Reason, MaxReasonLength);
    }
}
