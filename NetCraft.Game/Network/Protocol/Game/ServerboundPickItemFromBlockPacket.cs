namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromBlockPacket 数据包对应原版 ServerboundPickItemFromBlockPacket
//字段 Pos(BlockPos) IncludeData(boolean)
public sealed record ServerboundPickItemFromBlockPacket(object Pos, bool IncludeData) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPickItemFromBlockPacket> StreamCodec { get; } = new PickItemFromBlockCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPickItemFromBlock;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePickItemFromBlock(this);

    private sealed class PickItemFromBlockCodec : StreamCodec<FriendlyByteBuf, ServerboundPickItemFromBlockPacket>
    {
        public ServerboundPickItemFromBlockPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPickItemFromBlockPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
