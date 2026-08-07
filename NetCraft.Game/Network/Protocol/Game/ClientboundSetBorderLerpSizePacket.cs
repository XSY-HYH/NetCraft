namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderLerpSizePacket 边界大小插值包对应原版 ClientboundSetBorderLerpSizePacket
//字段 OldSize(double) NewSize(double) LerpTime(long)
public sealed record ClientboundSetBorderLerpSizePacket(double OldSize, double NewSize, long LerpTime) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderLerpSizePacket> StreamCodec { get; } = new SetBorderLerpSizeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderLerpSize;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderLerpSize(this);

    private sealed class SetBorderLerpSizeCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderLerpSizePacket>
    {
        public ClientboundSetBorderLerpSizePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderLerpSizePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
