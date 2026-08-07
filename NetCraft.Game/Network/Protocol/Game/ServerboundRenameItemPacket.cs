namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRenameItemPacket 数据包对应原版 ServerboundRenameItemPacket
//字段 Name(String)
public sealed record ServerboundRenameItemPacket(string Name) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket> StreamCodec { get; } = new RenameItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRenameItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRenameItem(this);

    private sealed class RenameItemCodec : StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket>
    {
        public ServerboundRenameItemPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundRenameItemPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
