namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAttackPacket 数据包对应原版 ServerboundAttackPacket
//字段 EntityId(int)
public sealed record ServerboundAttackPacket(int EntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAttackPacket> StreamCodec { get; } = new AttackCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAttack;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAttack(this);

    private sealed class AttackCodec : StreamCodec<FriendlyByteBuf, ServerboundAttackPacket>
    {
        public ServerboundAttackPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundAttackPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
