namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundUseItemOnPacket 数据包对应原版 ServerboundUseItemOnPacket
//字段 BlockHit(BlockHitResult) Hand(InteractionHand) Sequence(int)
public sealed record ServerboundUseItemOnPacket(object BlockHit, object Hand, int Sequence) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundUseItemOnPacket> StreamCodec { get; } = new UseItemOnCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundUseItemOn;

    public void Handle(ServerGamePacketListener handler) => handler.HandleUseItemOn(this);

    private sealed class UseItemOnCodec : StreamCodec<FriendlyByteBuf, ServerboundUseItemOnPacket>
    {
        public ServerboundUseItemOnPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundUseItemOnPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
