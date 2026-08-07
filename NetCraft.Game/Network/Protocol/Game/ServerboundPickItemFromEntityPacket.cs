namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromEntityPacket 数据包对应原版 ServerboundPickItemFromEntityPacket
//字段 Id(int) IncludeData(boolean)
public sealed record ServerboundPickItemFromEntityPacket(int Id, bool IncludeData) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPickItemFromEntityPacket> StreamCodec { get; } = new PickItemFromEntityCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPickItemFromEntity;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePickItemFromEntity(this);

    private sealed class PickItemFromEntityCodec : StreamCodec<FriendlyByteBuf, ServerboundPickItemFromEntityPacket>
    {
        public ServerboundPickItemFromEntityPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundPickItemFromEntityPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
