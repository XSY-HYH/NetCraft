namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSelectTradePacket 数据包对应原版 ServerboundSelectTradePacket
//字段 Item(int)
public sealed record ServerboundSelectTradePacket(int Item) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket> StreamCodec { get; } = new SelectTradeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSelectTrade;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSelectTrade(this);

    private sealed class SelectTradeCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket>
    {
        public ServerboundSelectTradePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSelectTradePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
