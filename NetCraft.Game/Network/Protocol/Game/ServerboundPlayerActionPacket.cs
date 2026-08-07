namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerActionPacket 数据包对应原版 ServerboundPlayerActionPacket
//字段 Pos(BlockPos) Direction(Direction) Action(Action) Sequence(int)
public sealed record ServerboundPlayerActionPacket(object Pos, object Direction, object Action, int Sequence) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerActionPacket> StreamCodec { get; } = new PlayerActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerAction(this);

    private sealed class PlayerActionCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerActionPacket>
    {
        public ServerboundPlayerActionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerActionPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
