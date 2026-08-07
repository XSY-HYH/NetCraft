namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetBeaconPacket 数据包对应原版 ServerboundSetBeaconPacket
//字段 Primary(Optional<Holder<MobEffect>>) Secondary(Optional<Holder<MobEffect>>)
public sealed record ServerboundSetBeaconPacket(object Primary, object Secondary) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket> StreamCodec { get; } = new SetBeaconCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetBeacon;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetBeaconPacket(this);

    private sealed class SetBeaconCodec : StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket>
    {
        public ServerboundSetBeaconPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetBeaconPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
