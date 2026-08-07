namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundForgetLevelChunkPacket 遗忘区块包对应原版 ClientboundForgetLevelChunkPacket
//字段 Pos(ChunkPos)
public sealed record ClientboundForgetLevelChunkPacket(object Pos) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundForgetLevelChunkPacket> StreamCodec { get; } = new ForgetLevelChunkCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundForgetLevelChunk;

    public void Handle(ClientGamePacketListener handler) => handler.HandleForgetLevelChunk(this);

    private sealed class ForgetLevelChunkCodec : StreamCodec<FriendlyByteBuf, ClientboundForgetLevelChunkPacket>
    {
        public ClientboundForgetLevelChunkPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundForgetLevelChunkPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
