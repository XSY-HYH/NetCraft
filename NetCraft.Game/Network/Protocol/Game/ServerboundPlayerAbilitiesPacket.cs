namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerAbilitiesPacket 数据包对应原版 ServerboundPlayerAbilitiesPacket
//字段 IsFlying(boolean)
public sealed record ServerboundPlayerAbilitiesPacket(bool IsFlying) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerAbilitiesPacket> StreamCodec { get; } = new PlayerAbilitiesCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerAbilities;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerAbilities(this);

    private sealed class PlayerAbilitiesCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerAbilitiesPacket>
    {
        public ServerboundPlayerAbilitiesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerAbilitiesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
