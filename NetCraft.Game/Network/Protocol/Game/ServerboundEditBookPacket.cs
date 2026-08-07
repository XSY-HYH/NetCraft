namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundEditBookPacket 数据包对应原版 ServerboundEditBookPacket
//字段 Slot(int) Pages(List<String>) Title(Optional<String>)
public sealed record ServerboundEditBookPacket(int Slot, object Pages, object Title) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundEditBookPacket> StreamCodec { get; } = new EditBookCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundEditBook;

    public void Handle(ServerGamePacketListener handler) => handler.HandleEditBook(this);

    private sealed class EditBookCodec : StreamCodec<FriendlyByteBuf, ServerboundEditBookPacket>
    {
        public ServerboundEditBookPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundEditBookPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
