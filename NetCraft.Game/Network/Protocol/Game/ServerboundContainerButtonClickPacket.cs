namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerButtonClickPacket 数据包对应原版 ServerboundContainerButtonClickPacket
//字段 ContainerId(int) ButtonId(int)
public sealed record ServerboundContainerButtonClickPacket(int ContainerId, int ButtonId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket> StreamCodec { get; } = new ContainerButtonClickCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerButtonClick;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerButtonClick(this);

    private sealed class ContainerButtonClickCodec : StreamCodec<FriendlyByteBuf, ServerboundContainerButtonClickPacket>
    {
        public ServerboundContainerButtonClickPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundContainerButtonClickPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
