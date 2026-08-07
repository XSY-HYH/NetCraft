namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetDefaultSpawnPositionPacket 默认出生点包对应原版 ClientboundSetDefaultSpawnPositionPacket
//字段 RespawnData(LevelData.RespawnData)
public sealed record ClientboundSetDefaultSpawnPositionPacket(object RespawnData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetDefaultSpawnPositionPacket> StreamCodec { get; } = new SetDefaultSpawnPositionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetDefaultSpawnPosition;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetSpawn(this);

    private sealed class SetDefaultSpawnPositionCodec : StreamCodec<FriendlyByteBuf, ClientboundSetDefaultSpawnPositionPacket>
    {
        public ClientboundSetDefaultSpawnPositionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetDefaultSpawnPositionPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
