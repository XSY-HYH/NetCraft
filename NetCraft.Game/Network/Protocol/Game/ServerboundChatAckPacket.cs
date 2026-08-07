namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatAckPacket 数据包对应原版 ServerboundChatAckPacket
//字段 Offset(int)
public sealed record ServerboundChatAckPacket(int Offset) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatAckPacket> StreamCodec { get; } = new ChatAckCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatAck;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatAck(this);

    private sealed class ChatAckCodec : StreamCodec<FriendlyByteBuf, ServerboundChatAckPacket>
    {
        public ServerboundChatAckPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChatAckPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
