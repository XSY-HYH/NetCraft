namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundStopSoundPacket 停止声音包对应原版 ClientboundStopSoundPacket
//字段 Name(Identifier) Source(SoundSource)
public sealed record ClientboundStopSoundPacket(object Name, object Source) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundStopSoundPacket> StreamCodec { get; } = new StopSoundCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundStopSound;

    public void Handle(ClientGamePacketListener handler) => handler.HandleStopSoundEvent(this);

    private sealed class StopSoundCodec : StreamCodec<FriendlyByteBuf, ClientboundStopSoundPacket>
    {
        public ClientboundStopSoundPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundStopSoundPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
