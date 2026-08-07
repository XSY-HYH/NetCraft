namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEventPacket 方块事件包对应原版 ClientboundBlockEventPacket
//字段 Pos(BlockPos) B0(int) B1(int) Block(Block)
public sealed record ClientboundBlockEventPacket(object Pos, int B0, int B1, object Block) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket> StreamCodec { get; } = new BlockEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEvent(this);

    private sealed class BlockEventCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEventPacket>
    {
        public ClientboundBlockEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundBlockEventPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
