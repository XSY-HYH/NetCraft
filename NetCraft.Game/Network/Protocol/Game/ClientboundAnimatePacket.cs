namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAnimatePacket 实体动画包对应原版 ClientboundAnimatePacket
//字段 Id(int) Action(int)
public sealed record ClientboundAnimatePacket(int Id, int Action) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket> StreamCodec { get; } = new AnimateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAnimate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class AnimateCodec : StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket>
    {
        public ClientboundAnimatePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundAnimatePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
