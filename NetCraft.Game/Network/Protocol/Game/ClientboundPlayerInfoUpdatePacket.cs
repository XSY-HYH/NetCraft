namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerInfoUpdatePacket 玩家信息更新包对应原版 ClientboundPlayerInfoUpdatePacket
//字段 actions EnumSet Action 业务类型占位 entries List Entry 业务类型占位
public sealed record ClientboundPlayerInfoUpdatePacket(object Actions, object Entries) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoUpdatePacket> StreamCodec { get; } = new PlayerInfoUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerInfoUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerInfoUpdate(this);

    private sealed class PlayerInfoUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoUpdatePacket>
    {
        public ClientboundPlayerInfoUpdatePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Action/Entry 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerInfoUpdatePacket value)
            => throw new NotImplementedException("Action/Entry 业务类型待实现");
    }
}
