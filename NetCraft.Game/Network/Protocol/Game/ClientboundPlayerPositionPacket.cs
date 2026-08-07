namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerPositionPacket 玩家位置包对应原版 ClientboundPlayerPositionPacket
//字段 id VarInt change PositionMoveRotation 业务类型占位 relatives Set Relative 业务类型占位
public sealed record ClientboundPlayerPositionPacket(int Id, object Change, object Relatives) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket> StreamCodec { get; } = new PlayerPositionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerPosition;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMovePlayer(this);

    private sealed class PlayerPositionCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket>
    {
        public ClientboundPlayerPositionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("PositionMoveRotation/Relative 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerPositionPacket value)
            => throw new NotImplementedException("PositionMoveRotation/Relative 业务类型待实现");
    }
}
