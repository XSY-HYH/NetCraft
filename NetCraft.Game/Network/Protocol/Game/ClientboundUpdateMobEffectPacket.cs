namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateMobEffectPacket 药水效果更新包对应原版 ClientboundUpdateMobEffectPacket
//字段 EntityId(int) Effect(Holder<MobEffect>) EffectAmplifier(int) EffectDurationTicks(int) Flags(byte)
public sealed record ClientboundUpdateMobEffectPacket(int EntityId, object Effect, int EffectAmplifier, int EffectDurationTicks, byte Flags) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateMobEffectPacket> StreamCodec { get; } = new UpdateMobEffectCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateMobEffect;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateMobEffect(this);

    private sealed class UpdateMobEffectCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateMobEffectPacket>
    {
        public ClientboundUpdateMobEffectPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateMobEffectPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
