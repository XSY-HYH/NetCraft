namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTeleportEntityPacket 实体传送包对应原版 ClientboundTeleportEntityPacket
//字段 id VarInt change PositionMoveRotation 业务类型占位 relatives Set Relative 业务类型占位 onGround Boolean
public sealed record ClientboundTeleportEntityPacket(int Id, object Change, object Relatives, bool OnGround) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTeleportEntityPacket> StreamCodec { get; } = new TeleportEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTeleportEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTeleportEntity(this);

    private sealed class TeleportEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundTeleportEntityPacket>
    {
        public ClientboundTeleportEntityPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("PositionMoveRotation/Relative 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundTeleportEntityPacket value)
            => throw new NotImplementedException("PositionMoveRotation/Relative 业务类型待实现");
    }
}
