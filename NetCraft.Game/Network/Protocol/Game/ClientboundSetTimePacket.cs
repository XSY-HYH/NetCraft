namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTimePacket 时间设置包对应原版 ClientboundSetTimePacket
//字段 GameTime(long) ClockUpdates(Map<Holder<WorldClock>, ClockNetworkState>)
public sealed record ClientboundSetTimePacket(long GameTime, object ClockUpdates) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetTimePacket> StreamCodec { get; } = new SetTimeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetTime;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetTime(this);

    private sealed class SetTimeCodec : StreamCodec<FriendlyByteBuf, ClientboundSetTimePacket>
    {
        public ClientboundSetTimePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetTimePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
