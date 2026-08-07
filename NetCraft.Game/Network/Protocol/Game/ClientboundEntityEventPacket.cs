namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityEventPacket 实体事件包对应原版 ClientboundEntityEventPacket
//字段 EntityId(int) EventId(byte)
public sealed record ClientboundEntityEventPacket(int EntityId, byte EventId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundEntityEventPacket> StreamCodec { get; } = new EntityEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundEntityEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityEvent(this);

    private sealed class EntityEventCodec : StreamCodec<FriendlyByteBuf, ClientboundEntityEventPacket>
    {
        public ClientboundEntityEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundEntityEventPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
