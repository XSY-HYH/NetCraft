namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityMotionPacket 实体动量包对应原版 ClientboundSetEntityMotionPacket
//字段 id VarInt movement Vec3 拆为 xa ya za 三个 double 对应原版 Vec3.LP_STREAM_CODEC
public sealed record ClientboundSetEntityMotionPacket(int Id, double Xa, double Ya, double Za) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetEntityMotionPacket> StreamCodec { get; } = new SetEntityMotionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityMotion;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityMotion(this);

    private sealed class SetEntityMotionCodec : StreamCodec<FriendlyByteBuf, ClientboundSetEntityMotionPacket>
    {
        public ClientboundSetEntityMotionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());

        public void Encode(FriendlyByteBuf buf, ClientboundSetEntityMotionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteDouble(value.Xa);
            buf.WriteDouble(value.Ya);
            buf.WriteDouble(value.Za);
        }
    }
}
