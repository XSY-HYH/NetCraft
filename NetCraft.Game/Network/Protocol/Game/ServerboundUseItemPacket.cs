namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemPacket 数据包对应原版 ServerboundUseItemPacket
//字段 Hand(InteractionHand) Sequence(int) YRot(float) XRot(float)
public sealed record ServerboundUseItemPacket(object Hand, int Sequence, float YRot, float XRot) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket> StreamCodec { get; } = new UseItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundUseItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleUseItem(this);

    private sealed class UseItemCodec : StreamCodec<FriendlyByteBuf, ServerboundUseItemPacket>
    {
        public ServerboundUseItemPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundUseItemPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
