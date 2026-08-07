namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundMoveVehiclePacket 数据包对应原版 ServerboundMoveVehiclePacket
//字段 Position(Vec3) YRot(float) XRot(float) OnGround(boolean)
public sealed record ServerboundMoveVehiclePacket(object Position, float YRot, float XRot, bool OnGround) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket> StreamCodec { get; } = new MoveVehicleCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundMoveVehicle;

    public void Handle(ServerGamePacketListener handler) => handler.HandleMoveVehicle(this);

    private sealed class MoveVehicleCodec : StreamCodec<FriendlyByteBuf, ServerboundMoveVehiclePacket>
    {
        public ServerboundMoveVehiclePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundMoveVehiclePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
