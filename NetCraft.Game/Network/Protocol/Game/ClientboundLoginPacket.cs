namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLoginPacket 登录包对应原版 ClientboundLoginPacket
//字段 PlayerId(int) Hardcore(boolean) Levels(Set<ResourceKey<Level>>) MaxPlayers(int) ChunkRadius(int) SimulationDistance(int)
public sealed record ClientboundLoginPacket(int PlayerId, bool Hardcore, object Levels, int MaxPlayers, int ChunkRadius, int SimulationDistance, bool ReducedDebugInfo, bool ShowDeathScreen, bool DoLimitedCrafting, object CommonPlayerSpawnInfo, bool OnlineMode, bool EnforcesSecureChat) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginPacket> StreamCodec { get; } = new LoginCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLogin;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLogin(this);

    private sealed class LoginCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginPacket>
    {
        public ClientboundLoginPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundLoginPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
