namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTitlesAnimationPacket 标题动画包对应原版 ClientboundSetTitlesAnimationPacket
//字段 FadeIn(int) Stay(int) FadeOut(int)
public sealed record ClientboundSetTitlesAnimationPacket(int FadeIn, int Stay, int FadeOut) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetTitlesAnimationPacket> StreamCodec { get; } = new SetTitlesAnimationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetTitlesAnimation;

    public void Handle(ClientGamePacketListener handler) => handler.SetTitlesAnimation(this);

    private sealed class SetTitlesAnimationCodec : StreamCodec<FriendlyByteBuf, ClientboundSetTitlesAnimationPacket>
    {
        public ClientboundSetTitlesAnimationPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetTitlesAnimationPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
