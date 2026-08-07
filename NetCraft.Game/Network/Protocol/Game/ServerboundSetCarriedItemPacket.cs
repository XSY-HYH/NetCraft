namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCarriedItemPacket 数据包对应原版 ServerboundSetCarriedItemPacket
//字段 Slot(int)
public sealed record ServerboundSetCarriedItemPacket(int Slot) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCarriedItemPacket> StreamCodec { get; } = new SetCarriedItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCarriedItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCarriedItem(this);

    private sealed class SetCarriedItemCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCarriedItemPacket>
    {
        public ServerboundSetCarriedItemPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetCarriedItemPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
