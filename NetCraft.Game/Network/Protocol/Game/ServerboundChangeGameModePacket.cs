namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeGameModePacket 数据包对应原版 ServerboundChangeGameModePacket
//字段 Mode(GameType)
public sealed record ServerboundChangeGameModePacket(object Mode) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket> StreamCodec { get; } = new ChangeGameModeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeGameMode;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeGameMode(this);

    private sealed class ChangeGameModeCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket>
    {
        public ServerboundChangeGameModePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundChangeGameModePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
