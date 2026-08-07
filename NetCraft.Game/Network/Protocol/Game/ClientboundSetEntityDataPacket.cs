namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityDataPacket 实体数据包对应原版 ClientboundSetEntityDataPacket
//字段 Id(int) PackedItems(List<SynchedEntityData.DataValue<?>>)
public sealed record ClientboundSetEntityDataPacket(int Id, object PackedItems) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetEntityDataPacket> StreamCodec { get; } = new SetEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityData(this);

    private sealed class SetEntityDataCodec : StreamCodec<FriendlyByteBuf, ClientboundSetEntityDataPacket>
    {
        public ClientboundSetEntityDataPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetEntityDataPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
