namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateAttributesPacket 属性更新包对应原版 ClientboundUpdateAttributesPacket
//字段 EntityId(int) Attributes(List<AttributeSnapshot>)
public sealed record ClientboundUpdateAttributesPacket(int EntityId, object Attributes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateAttributesPacket> StreamCodec { get; } = new UpdateAttributesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateAttributes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateAttributes(this);

    private sealed class UpdateAttributesCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateAttributesPacket>
    {
        public ClientboundUpdateAttributesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateAttributesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
