namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundInteractPacket 数据包对应原版 ServerboundInteractPacket
//字段 EntityId(int) Hand(InteractionHand) Location(Vec3) UsingSecondaryAction(boolean)
public sealed record ServerboundInteractPacket(int EntityId, object Hand, object Location, bool UsingSecondaryAction) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundInteractPacket> StreamCodec { get; } = new InteractCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundInteract;

    public void Handle(ServerGamePacketListener handler) => handler.HandleInteract(this);

    private sealed class InteractCodec : StreamCodec<FriendlyByteBuf, ServerboundInteractPacket>
    {
        public ServerboundInteractPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundInteractPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
