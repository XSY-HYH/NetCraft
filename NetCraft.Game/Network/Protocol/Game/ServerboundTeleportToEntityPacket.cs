namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundTeleportToEntityPacket 数据包对应原版 ServerboundTeleportToEntityPacket
//字段 Uuid(UUID)
public sealed record ServerboundTeleportToEntityPacket(Guid Uuid) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket> StreamCodec { get; } = new TeleportToEntityCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundTeleportToEntity;

    public void Handle(ServerGamePacketListener handler) => handler.HandleTeleportToEntityPacket(this);

    private sealed class TeleportToEntityCodec : StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket>
    {
        public ServerboundTeleportToEntityPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundTeleportToEntityPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
