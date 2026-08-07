namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandSignedPacket 数据包对应原版 ServerboundChatCommandSignedPacket
//字段 Command(String) TimeStamp(Instant) Salt(long) ArgumentSignatures(ArgumentSignatures) LastSeenMessages(LastSeenMessages.Update)
public sealed record ServerboundChatCommandSignedPacket(string Command, object TimeStamp, long Salt, object ArgumentSignatures, object LastSeenMessages) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatCommandSignedPacket> StreamCodec { get; } = new ChatCommandSignedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatCommandSigned;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSignedChatCommand(this);

    private sealed class ChatCommandSignedCodec : StreamCodec<FriendlyByteBuf, ServerboundChatCommandSignedPacket>
    {
        public ServerboundChatCommandSignedPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChatCommandSignedPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
