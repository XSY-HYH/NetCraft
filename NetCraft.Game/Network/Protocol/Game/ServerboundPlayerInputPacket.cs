namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerInputPacket 数据包对应原版 ServerboundPlayerInputPacket
//字段 Input(Input)
public sealed record ServerboundPlayerInputPacket(object Input) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerInputPacket> StreamCodec { get; } = new PlayerInputCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerInput;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerInput(this);

    private sealed class PlayerInputCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerInputPacket>
    {
        public ServerboundPlayerInputPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerInputPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
