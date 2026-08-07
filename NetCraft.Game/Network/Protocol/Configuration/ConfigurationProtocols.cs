namespace NetCraft.Game.Network.Protocol.Configuration;

//ConfigurationProtocols configuration 协议注册
//对应原版 net.minecraft.network.protocol.configuration.ConfigurationProtocols
//本轮只注册 ServerConfigurationPacketListener 直接对应的 3 个 Serverbound 包和
//ClientConfigurationPacketListener 直接对应的 6 个 Clientbound 包
//common/cookie 子协议包暂不注册因 PacketType<THandler> 不协变多 THandler 注册需要后续扩展 ProtocolInfoBuilder
public static class ConfigurationProtocols
{
    //ServerboundTemplate SERVERBOUND configuration 协议模板
    public static readonly SimpleUnboundProtocol<ServerConfigurationPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerConfigurationPacketListener>(
            ConnectionProtocol.Configuration, FlowDirection.Serverbound)
            .AddPacket(ConfigurationPacketTypes.ServerboundFinishConfiguration, ServerboundFinishConfigurationPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ServerboundSelectKnownPacks, ServerboundSelectKnownPacks.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ServerboundAcceptCodeOfConduct, ServerboundAcceptCodeOfConductPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerConfigurationPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND configuration 协议模板
    public static readonly SimpleUnboundProtocol<ClientConfigurationPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientConfigurationPacketListener>(
            ConnectionProtocol.Configuration, FlowDirection.Clientbound)
            .AddPacket(ConfigurationPacketTypes.ClientboundFinishConfiguration, ClientboundFinishConfigurationPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundResetChat, ClientboundResetChatPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundRegistryData, ClientboundRegistryDataPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundUpdateEnabledFeatures, ClientboundUpdateEnabledFeaturesPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundSelectKnownPacks, ClientboundSelectKnownPacks.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundCodeOfConduct, ClientboundCodeOfConductPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientConfigurationPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
