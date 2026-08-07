namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameEventPacket 游戏事件包对应原版 ClientboundGameEventPacket
//字段 Event(Type) Param(float)
public sealed record ClientboundGameEventPacket(object Event, float Param) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameEventPacket> StreamCodec { get; } = new GameEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameEvent(this);

    private sealed class GameEventCodec : StreamCodec<FriendlyByteBuf, ClientboundGameEventPacket>
    {
        public ClientboundGameEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundGameEventPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
