namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundLockDifficultyPacket 数据包对应原版 ServerboundLockDifficultyPacket
//字段 Locked(boolean)
public sealed record ServerboundLockDifficultyPacket(bool Locked) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket> StreamCodec { get; } = new LockDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundLockDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleLockDifficulty(this);

    private sealed class LockDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundLockDifficultyPacket>
    {
        public ServerboundLockDifficultyPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundLockDifficultyPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
