namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSeenAdvancementsPacket 数据包对应原版 ServerboundSeenAdvancementsPacket
//字段 Action(Action) Tab(Identifier)
public sealed record ServerboundSeenAdvancementsPacket(object Action, object Tab) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket> StreamCodec { get; } = new SeenAdvancementsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSeenAdvancements;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSeenAdvancements(this);

    private sealed class SeenAdvancementsCodec : StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket>
    {
        public ServerboundSeenAdvancementsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSeenAdvancementsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
