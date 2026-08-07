namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerSlotStateChangedPacket 数据包对应原版 ServerboundContainerSlotStateChangedPacket
//字段 SlotId(int) ContainerId(int) NewState(boolean)
public sealed record ServerboundContainerSlotStateChangedPacket(int SlotId, int ContainerId, bool NewState) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket> StreamCodec { get; } = new ContainerSlotStateChangedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerSlotStateChanged;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerSlotStateChanged(this);

    private sealed class ContainerSlotStateChangedCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerSlotStateChangedPacket>
    {
        public ServerboundContainerSlotStateChangedPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundContainerSlotStateChangedPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
