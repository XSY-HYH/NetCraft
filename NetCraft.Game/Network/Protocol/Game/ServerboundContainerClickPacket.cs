namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerClickPacket 数据包对应原版 ServerboundContainerClickPacket
//字段 ContainerId(int) StateId(int) SlotNum(short) ButtonNum(byte) ContainerInput(ContainerInput) ChangedSlots(Int2ObjectMap<HashedStack>)
public sealed record ServerboundContainerClickPacket(int ContainerId, int StateId, short SlotNum, byte ButtonNum, object ContainerInput, object ChangedSlots, object CarriedItem) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerClickPacket> StreamCodec { get; } = new ContainerClickCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerClick;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerClick(this);

    private sealed class ContainerClickCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerClickPacket>
    {
        public ServerboundContainerClickPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundContainerClickPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
