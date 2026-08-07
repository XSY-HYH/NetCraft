namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundEntityPacket 实体声音包对应原版 ClientboundSoundEntityPacket
//sound/source 业务类型暂用 object 占位其他字段真实编解码
public sealed record ClientboundSoundEntityPacket(object Sound, object Source, int Id, float Volume, float Pitch, long Seed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSoundEntityPacket> StreamCodec { get; } = new SoundEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSoundEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSoundEntityEvent(this);

    private sealed class SoundEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundSoundEntityPacket>
    {
        public ClientboundSoundEntityPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("SoundEvent/SoundSource 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSoundEntityPacket value)
            => throw new NotImplementedException("SoundEvent/SoundSource 业务类型待实现");
    }
}
