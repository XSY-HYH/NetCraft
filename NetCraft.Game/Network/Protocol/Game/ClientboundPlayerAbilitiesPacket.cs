namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerAbilitiesPacket 玩家能力包对应原版 ClientboundPlayerAbilitiesPacket
//字段 Invulnerable(boolean) IsFlying(boolean) CanFly(boolean) Instabuild(boolean) FlyingSpeed(float) WalkingSpeed(float)
public sealed record ClientboundPlayerAbilitiesPacket(bool Invulnerable, bool IsFlying, bool CanFly, bool Instabuild, float FlyingSpeed, float WalkingSpeed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerAbilitiesPacket> StreamCodec { get; } = new PlayerAbilitiesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerAbilities;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerAbilities(this);

    private sealed class PlayerAbilitiesCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerAbilitiesPacket>
    {
        public ClientboundPlayerAbilitiesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerAbilitiesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
