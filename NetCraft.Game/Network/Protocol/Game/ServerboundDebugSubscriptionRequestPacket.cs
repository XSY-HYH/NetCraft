namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundDebugSubscriptionRequestPacket 数据包对应原版 ServerboundDebugSubscriptionRequestPacket
//字段 Subscriptions(Set<DebugSubscription<?>>)
public sealed record ServerboundDebugSubscriptionRequestPacket(object Subscriptions) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket> StreamCodec { get; } = new DebugSubscriptionRequestCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundDebugSubscriptionRequest;

    public void Handle(ServerGamePacketListener handler) => handler.HandleDebugSubscriptionRequest(this);

    private sealed class DebugSubscriptionRequestCodec : StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket>
    {
        public ServerboundDebugSubscriptionRequestPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundDebugSubscriptionRequestPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
