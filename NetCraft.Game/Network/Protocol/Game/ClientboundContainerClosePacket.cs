namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerClosePacket 容器关闭包对应原版 ClientboundContainerClosePacket
//字段 ContainerId(int)
public sealed record ClientboundContainerClosePacket(int ContainerId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundContainerClosePacket> StreamCodec { get; } = new ContainerCloseCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundContainerClose;

    public void Handle(ClientGamePacketListener handler) => handler.HandleContainerClose(this);

    private sealed class ContainerCloseCodec : StreamCodec<FriendlyByteBuf, ClientboundContainerClosePacket>
    {
        public ClientboundContainerClosePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundContainerClosePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
