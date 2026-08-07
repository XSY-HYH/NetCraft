namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetHeldSlotPacket 持物槽包对应原版 ClientboundSetHeldSlotPacket
//字段 Slot(int)
public sealed record ClientboundSetHeldSlotPacket(int Slot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket> StreamCodec { get; } = new SetHeldSlotCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetHeldSlot;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetHeldSlot(this);

    private sealed class SetHeldSlotCodec : StreamCodec<FriendlyByteBuf, ClientboundSetHeldSlotPacket>
    {
        public ClientboundSetHeldSlotPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetHeldSlotPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
