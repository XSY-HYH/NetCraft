namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockChangedAckPacket 方块变更确认包对应原版 ClientboundBlockChangedAckPacket
//字段 Sequence(int)
public sealed record ClientboundBlockChangedAckPacket(int Sequence) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockChangedAckPacket> StreamCodec { get; } = new BlockChangedAckCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockChangedAck;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockChangedAck(this);

    private sealed class BlockChangedAckCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockChangedAckPacket>
    {
        public ClientboundBlockChangedAckPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundBlockChangedAckPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
