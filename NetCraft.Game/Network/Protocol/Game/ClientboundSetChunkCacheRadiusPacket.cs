namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetChunkCacheRadiusPacket 区块缓存半径包对应原版 ClientboundSetChunkCacheRadiusPacket
//字段 Radius(int)
public sealed record ClientboundSetChunkCacheRadiusPacket(int Radius) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheRadiusPacket> StreamCodec { get; } = new SetChunkCacheRadiusCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetChunkCacheRadius;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetChunkCacheRadius(this);

    private sealed class SetChunkCacheRadiusCodec : StreamCodec<FriendlyByteBuf, ClientboundSetChunkCacheRadiusPacket>
    {
        public ClientboundSetChunkCacheRadiusPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetChunkCacheRadiusPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
