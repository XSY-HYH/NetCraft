namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeDifficultyPacket 数据包对应原版 ServerboundChangeDifficultyPacket
//字段 Difficulty(Difficulty)
public sealed record ServerboundChangeDifficultyPacket(object Difficulty) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeDifficulty;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeDifficultyPacket>
    {
        public ServerboundChangeDifficultyPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChangeDifficultyPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
