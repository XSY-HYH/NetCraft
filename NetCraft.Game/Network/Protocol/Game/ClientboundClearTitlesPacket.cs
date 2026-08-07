namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundClearTitlesPacket 清除标题包对应原版 ClientboundClearTitlesPacket
//字段 ResetTimes(boolean)
public sealed record ClientboundClearTitlesPacket(bool ResetTimes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundClearTitlesPacket> StreamCodec { get; } = new ClearTitlesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundClearTitles;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTitlesClear(this);

    private sealed class ClearTitlesCodec : StreamCodec<FriendlyByteBuf, ClientboundClearTitlesPacket>
    {
        public ClientboundClearTitlesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundClearTitlesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
