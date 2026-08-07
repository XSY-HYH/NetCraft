namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerInfoRemovePacket 玩家信息移除包对应原版 ClientboundPlayerInfoRemovePacket
//字段 ProfileIds(List<UUID>)
public sealed record ClientboundPlayerInfoRemovePacket(object ProfileIds) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoRemovePacket> StreamCodec { get; } = new PlayerInfoRemoveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerInfoRemove;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerInfoRemove(this);

    private sealed class PlayerInfoRemoveCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoRemovePacket>
    {
        public ClientboundPlayerInfoRemovePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerInfoRemovePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
