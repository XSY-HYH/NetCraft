namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacket 光照更新包对应原版 ClientboundLightUpdatePacket
//字段 X(int) Z(int) LightData(ClientboundLightUpdatePacketData)
public sealed record ClientboundLightUpdatePacket(int X, int Z, object LightData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLightUpdatePacket> StreamCodec { get; } = new LightUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLightUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLightUpdatePacket(this);

    private sealed class LightUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundLightUpdatePacket>
    {
        public ClientboundLightUpdatePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundLightUpdatePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
