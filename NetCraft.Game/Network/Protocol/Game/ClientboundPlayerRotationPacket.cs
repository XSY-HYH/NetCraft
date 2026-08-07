namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerRotationPacket 玩家旋转包对应原版 ClientboundPlayerRotationPacket
//字段 YRot(float) RelativeY(boolean) XRot(float) RelativeX(boolean)
public sealed record ClientboundPlayerRotationPacket(float YRot, bool RelativeY, float XRot, bool RelativeX) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerRotationPacket> StreamCodec { get; } = new PlayerRotationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerRotation;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRotatePlayer(this);

    private sealed class PlayerRotationCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerRotationPacket>
    {
        public ClientboundPlayerRotationPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerRotationPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
