namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundInitializeBorderPacket 初始化边界包对应原版 ClientboundInitializeBorderPacket
//字段 NewCenterX(double) NewCenterZ(double) OldSize(double) NewSize(double) LerpTime(long) NewAbsoluteMaxSize(int)
public sealed record ClientboundInitializeBorderPacket(double NewCenterX, double NewCenterZ, double OldSize, double NewSize, long LerpTime, int NewAbsoluteMaxSize, int WarningBlocks, int WarningTime) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundInitializeBorderPacket> StreamCodec { get; } = new InitializeBorderCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundInitializeBorder;

    public void Handle(ClientGamePacketListener handler) => handler.HandleInitializeBorder(this);

    private sealed class InitializeBorderCodec : StreamCodec<FriendlyByteBuf, ClientboundInitializeBorderPacket>
    {
        public ClientboundInitializeBorderPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundInitializeBorderPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
