namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSwingPacket 数据包对应原版 ServerboundSwingPacket
//字段 Hand(InteractionHand)
public sealed record ServerboundSwingPacket(object Hand) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSwingPacket> StreamCodec { get; } = new SwingCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSwing;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class SwingCodec : StreamCodec<FriendlyByteBuf, ServerboundSwingPacket>
    {
        public ServerboundSwingPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSwingPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
