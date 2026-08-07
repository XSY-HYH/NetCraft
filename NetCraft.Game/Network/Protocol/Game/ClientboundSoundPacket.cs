namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundPacket 声音包对应原版 ClientboundSoundPacket
//原版 x/y/z 为 8 倍精度 int 编解码用 Int 其他 sound/source 业务类型占位
public sealed record ClientboundSoundPacket(object Sound, object Source, int X, int Y, int Z, float Volume, float Pitch, long Seed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSoundPacket> StreamCodec { get; } = new SoundCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSound;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSoundEvent(this);

    private sealed class SoundCodec : StreamCodec<FriendlyByteBuf, ClientboundSoundPacket>
    {
        public ClientboundSoundPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("SoundEvent/SoundSource 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSoundPacket value)
            => throw new NotImplementedException("SoundEvent/SoundSource 业务类型待实现");
    }
}
