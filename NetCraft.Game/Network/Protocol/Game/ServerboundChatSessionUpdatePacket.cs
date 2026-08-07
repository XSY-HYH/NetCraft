namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatSessionUpdatePacket 数据包对应原版 ServerboundChatSessionUpdatePacket
//字段 ChatSession(RemoteChatSession.Data)
public sealed record ServerboundChatSessionUpdatePacket(object ChatSession) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket> StreamCodec { get; } = new ChatSessionUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatSessionUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatSessionUpdate(this);

    private sealed class ChatSessionUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket>
    {
        public ServerboundChatSessionUpdatePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChatSessionUpdatePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
