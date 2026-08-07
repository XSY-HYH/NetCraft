namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundEntityPositionSyncPacket 实体位置同步包对应原版 ClientboundEntityPositionSyncPacket
//字段 Id(int) Values(PositionMoveRotation) OnGround(boolean)
public sealed record ClientboundEntityPositionSyncPacket(int Id, object Values, bool OnGround) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundEntityPositionSyncPacket> StreamCodec { get; } = new EntityPositionSyncCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundEntityPositionSync;

    public void Handle(ClientGamePacketListener handler) => handler.HandleEntityPositionSync(this);

    private sealed class EntityPositionSyncCodec : StreamCodec<FriendlyByteBuf, ClientboundEntityPositionSyncPacket>
    {
        public ClientboundEntityPositionSyncPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundEntityPositionSyncPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
