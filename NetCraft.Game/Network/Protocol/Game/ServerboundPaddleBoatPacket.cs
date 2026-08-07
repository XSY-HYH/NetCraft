namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPaddleBoatPacket 数据包对应原版 ServerboundPaddleBoatPacket
//字段 Left(boolean) Right(boolean)
public sealed record ServerboundPaddleBoatPacket(bool Left, bool Right) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPaddleBoatPacket> StreamCodec { get; } = new PaddleBoatCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPaddleBoat;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePaddleBoat(this);

    private sealed class PaddleBoatCodec : StreamCodec<FriendlyByteBuf, ServerboundPaddleBoatPacket>
    {
        public ServerboundPaddleBoatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPaddleBoatPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
