namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSignUpdatePacket 数据包对应原版 ServerboundSignUpdatePacket
//字段 Pos(BlockPos) IsFrontText(boolean)
public sealed record ServerboundSignUpdatePacket(object Pos, bool IsFrontText) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket> StreamCodec { get; } = new SignUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSignUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSignUpdate(this);

    private sealed class SignUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundSignUpdatePacket>
    {
        public ServerboundSignUpdatePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSignUpdatePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
