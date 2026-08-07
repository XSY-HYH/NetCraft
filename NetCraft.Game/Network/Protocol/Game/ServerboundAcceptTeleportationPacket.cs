namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAcceptTeleportationPacket 数据包对应原版 ServerboundAcceptTeleportationPacket
//字段 Id(int)
public sealed record ServerboundAcceptTeleportationPacket(int Id) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket> StreamCodec { get; } = new AcceptTeleportationCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAcceptTeleportation;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAcceptTeleportPacket(this);

    private sealed class AcceptTeleportationCodec : StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket>
    {
        public ServerboundAcceptTeleportationPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundAcceptTeleportationPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
