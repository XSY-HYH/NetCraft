namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetCameraPacket 设置视角包对应原版 ClientboundSetCameraPacket
//字段 CameraId(int)
public sealed record ClientboundSetCameraPacket(int CameraId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket> StreamCodec { get; } = new SetCameraCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetCamera;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetCamera(this);

    private sealed class SetCameraCodec : StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket>
    {
        public ClientboundSetCameraPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetCameraPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
