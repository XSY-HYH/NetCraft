namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMovePlayerPacket 玩家移动包基类对应原版 ServerboundMovePlayerPacket
//字段 
public sealed record ServerboundMovePlayerPacket() : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket> StreamCodec { get; } = new MovePlayerCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundMovePlayerPos;

    public void Handle(ServerGamePacketListener handler) => handler.HandleMovePlayer(this);

    private sealed class MovePlayerCodec : StreamCodec<FriendlyByteBuf, ServerboundMovePlayerPacket>
    {
        public ServerboundMovePlayerPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ServerboundMovePlayerPacket value)
            { }
    }
}
