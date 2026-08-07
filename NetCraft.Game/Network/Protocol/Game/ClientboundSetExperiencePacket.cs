namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetExperiencePacket 经验值包对应原版 ClientboundSetExperiencePacket
//字段 ExperienceProgress(float) TotalExperience(int) ExperienceLevel(int)
public sealed record ClientboundSetExperiencePacket(float ExperienceProgress, int TotalExperience, int ExperienceLevel) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket> StreamCodec { get; } = new SetExperienceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetExperience;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetExperience(this);

    private sealed class SetExperienceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket>
    {
        public ClientboundSetExperiencePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetExperiencePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
