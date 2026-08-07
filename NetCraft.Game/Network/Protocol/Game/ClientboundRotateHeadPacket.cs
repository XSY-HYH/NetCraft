namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRotateHeadPacket 头部旋转包对应原版 ClientboundRotateHeadPacket
//字段 EntityId(int) YHeadRot(byte)
public sealed record ClientboundRotateHeadPacket(int EntityId, byte YHeadRot) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket> StreamCodec { get; } = new RotateHeadCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRotateHead;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRotateMob(this);

    private sealed class RotateHeadCodec : StreamCodec<FriendlyByteBuf, ClientboundRotateHeadPacket>
    {
        public ClientboundRotateHeadPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRotateHeadPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
