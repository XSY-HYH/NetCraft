namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerCommandPacket 数据包对应原版 ServerboundPlayerCommandPacket
//字段 Id(int) Action(Action) Data(int)
public sealed record ServerboundPlayerCommandPacket(int Id, object Action, int Data) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket> StreamCodec { get; } = new PlayerCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerCommand(this);

    private sealed class PlayerCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket>
    {
        public ServerboundPlayerCommandPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerCommandPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
