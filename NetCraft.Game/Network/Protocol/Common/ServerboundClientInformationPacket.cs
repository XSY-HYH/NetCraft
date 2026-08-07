namespace NetCraft.Game.Network.Protocol.Common;

//ServerboundClientInformationPacket 客户端信息包对应原版 net.minecraft.network.protocol.common.ServerboundClientInformationPacket
//原版依赖 net.minecraft.server.level.ClientInformation 简化版直接内联字段
//字段含语言/视距/聊天可见性/聊天颜色/主手/显示皮肤部件/显示帽子/显示夹克/显示左袖/显示右袖/显示左裤腿/显示右裤腿/显示头盔/显示文本过滤/允许列表/粒子模式
public sealed record ServerboundClientInformationPacket(
    string Language,
    int ViewDistance,
    ChatVisibility ChatVisibility,
    bool ChatColors,
    int ModelCustomisation,
    bool ShowHat,
    bool ShowJacket,
    bool ShowLeftSleeve,
    bool ShowRightSleeve,
    bool ShowLeftPants,
    bool ShowRightPants,
    bool ShowCape,
    bool TextFiltering,
    bool AllowListing,
    ParticleStatus ParticleStatus) : Packet<ServerCommonPacketListener>
{
    public const int MaxLanguageLength = 16;
    public const int MaxViewDistance = 32;

    public static StreamCodec<FriendlyByteBuf, ServerboundClientInformationPacket> StreamCodec { get; } = new ClientInfoCodec();

    public PacketType<ServerCommonPacketListener> Type => ConfigurationPacketTypes.ServerboundClientInformation;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleClientInformation(this);

    private sealed class ClientInfoCodec : StreamCodec<FriendlyByteBuf, ServerboundClientInformationPacket>
    {
        public ServerboundClientInformationPacket Decode(FriendlyByteBuf buf)
            => new(
                buf.ReadString(MaxLanguageLength),
                Math.Clamp(buf.ReadVarInt(), 0, MaxViewDistance),
                (ChatVisibility)buf.ReadVarInt(),
                buf.ReadBoolean(),
                (int)buf.ReadByte(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                (ParticleStatus)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundClientInformationPacket value)
        {
            buf.WriteString(value.Language, MaxLanguageLength);
            buf.WriteVarInt(value.ViewDistance);
            buf.WriteVarInt((int)value.ChatVisibility);
            buf.WriteBoolean(value.ChatColors);
            buf.WriteByte((byte)value.ModelCustomisation);
            buf.WriteBoolean(value.ShowHat);
            buf.WriteBoolean(value.ShowJacket);
            buf.WriteBoolean(value.ShowLeftSleeve);
            buf.WriteBoolean(value.ShowRightSleeve);
            buf.WriteBoolean(value.ShowLeftPants);
            buf.WriteBoolean(value.ShowRightPants);
            buf.WriteBoolean(value.ShowCape);
            buf.WriteBoolean(value.TextFiltering);
            buf.WriteBoolean(value.AllowListing);
            buf.WriteVarInt((int)value.ParticleStatus);
        }
    }
}

//ChatVisibility 聊天可见性枚举对应原版 net.minecraft.world.entity.player.ChatVisiblity
public enum ChatVisibility
{
    Full,
    System,
    Hidden
}

//ParticleStatus 粒子状态枚举对应原版 net.minecraft.client.ParticleStatus
public enum ParticleStatus
{
    All,
    Decreased,
    Minimal
}
