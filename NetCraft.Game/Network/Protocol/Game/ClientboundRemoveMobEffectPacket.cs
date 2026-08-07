namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRemoveMobEffectPacket 移除药水效果包对应原版 ClientboundRemoveMobEffectPacket
//字段 EntityId(int) Effect(Holder<MobEffect>)
public sealed record ClientboundRemoveMobEffectPacket(int EntityId, object Effect) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRemoveMobEffectPacket> StreamCodec { get; } = new RemoveMobEffectCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRemoveMobEffect;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRemoveMobEffect(this);

    private sealed class RemoveMobEffectCodec : StreamCodec<FriendlyByteBuf, ClientboundRemoveMobEffectPacket>
    {
        public ClientboundRemoveMobEffectPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRemoveMobEffectPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
