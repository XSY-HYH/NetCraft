namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundClientCommandPacket 数据包对应原版 ServerboundClientCommandPacket
//字段 Action(Action)
public sealed record ServerboundClientCommandPacket(object Action) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket> StreamCodec { get; } = new ClientCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundClientCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandleClientCommand(this);

    private sealed class ClientCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket>
    {
        public ServerboundClientCommandPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundClientCommandPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
