namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatPacket 数据包对应原版 ServerboundChatPacket
//字段 Message(String) TimeStamp(Instant) Salt(long) Signature(MessageSignature) LastSeenMessages(LastSeenMessages.Update)
public sealed record ServerboundChatPacket(string Message, object TimeStamp, long Salt, object Signature, object LastSeenMessages) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatPacket> StreamCodec { get; } = new ChatCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChat;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChat(this);

    private sealed class ChatCodec : StreamCodec<FriendlyByteBuf, ServerboundChatPacket>
    {
        public ServerboundChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChatPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
