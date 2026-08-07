namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAddEntityPacket 添加实体包对应原版 ClientboundAddEntityPacket
//字段 id VarInt uuid UUID type EntityType 业务类型占位 x/y/z 3 double movement Vec3 拆 3 double xRot/yRot/yHeadRot 3 byte data VarInt
public sealed record ClientboundAddEntityPacket(int Id, Guid Uuid, object Kind, double X, double Y, double Z, double Xa, double Ya, double Za, byte XRot, byte YRot, byte YHeadRot, int Data) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundAddEntityPacket> StreamCodec { get; } = new AddEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAddEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAddEntity(this);

    private sealed class AddEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundAddEntityPacket>
    {
        public ClientboundAddEntityPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("EntityType 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundAddEntityPacket value)
            => throw new NotImplementedException("EntityType 业务类型待实现");
    }
}
