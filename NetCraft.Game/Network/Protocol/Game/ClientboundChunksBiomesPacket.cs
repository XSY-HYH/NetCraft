namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunksBiomesPacket 区块生物群系包对应原版 ClientboundChunksBiomesPacket
//字段 ChunkBiomeData(List<ChunkBiomeData>)
public sealed record ClientboundChunksBiomesPacket(object ChunkBiomeData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChunksBiomesPacket> StreamCodec { get; } = new ChunksBiomesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChunksBiomes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunksBiomes(this);

    private sealed class ChunksBiomesCodec : StreamCodec<FriendlyByteBuf, ClientboundChunksBiomesPacket>
    {
        public ClientboundChunksBiomesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundChunksBiomesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
