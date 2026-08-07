namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelParticlesPacket 世界粒子包对应原版 ClientboundLevelParticlesPacket
//字段 X(double) Y(double) Z(double) XDist(float) YDist(float) ZDist(float)
public sealed record ClientboundLevelParticlesPacket(double X, double Y, double Z, float XDist, float YDist, float ZDist, float MaxSpeed, int Count, bool OverrideLimiter, bool AlwaysShow, object Particle) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLevelParticlesPacket> StreamCodec { get; } = new LevelParticlesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelParticles;

    public void Handle(ClientGamePacketListener handler) => handler.HandleParticleEvent(this);

    private sealed class LevelParticlesCodec : StreamCodec<FriendlyByteBuf, ClientboundLevelParticlesPacket>
    {
        public ClientboundLevelParticlesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundLevelParticlesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
