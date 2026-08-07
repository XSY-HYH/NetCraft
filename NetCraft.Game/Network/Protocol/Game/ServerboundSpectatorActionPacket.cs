namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSpectatorActionPacket 数据包对应原版 ServerboundSpectatorActionPacket
//字段 SpectateEntityId(OptionalInt)
public sealed record ServerboundSpectatorActionPacket(int SpectateEntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket> StreamCodec { get; } = new SpectatorActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSpectatorAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSpectatorAction(this);

    private sealed class SpectatorActionCodec : StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket>
    {
        public ServerboundSpectatorActionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSpectatorActionPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
