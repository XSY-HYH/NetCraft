namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandPacket 数据包对应原版 ServerboundChatCommandPacket
//字段 Command(String)
public sealed record ServerboundChatCommandPacket(string Command) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatCommandPacket> StreamCodec { get; } = new ChatCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatCommand(this);

    private sealed class ChatCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundChatCommandPacket>
    {
        public ServerboundChatCommandPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChatCommandPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
