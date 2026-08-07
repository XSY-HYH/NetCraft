namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChangeDifficultyPacket 难度变更包对应原版 ClientboundChangeDifficultyPacket
//字段 Difficulty(Difficulty) Locked(boolean)
public sealed record ClientboundChangeDifficultyPacket(object Difficulty, bool Locked) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket> StreamCodec { get; } = new ChangeDifficultyCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChangeDifficulty;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChangeDifficulty(this);

    private sealed class ChangeDifficultyCodec : StreamCodec<FriendlyByteBuf, ClientboundChangeDifficultyPacket>
    {
        public ClientboundChangeDifficultyPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundChangeDifficultyPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
