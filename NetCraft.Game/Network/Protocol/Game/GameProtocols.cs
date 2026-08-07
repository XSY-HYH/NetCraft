namespace NetCraft.Game.Network.Protocol.Game;

//GameProtocols play 协议注册
//对应原版 net.minecraft.network.protocol.game.GameProtocols
//本轮注册最小子集进入游戏世界流程必需的核心包
//其余 188 个包按需扩展注册时照搬相同 AddPacket 模式
public static class GameProtocols
{
    //ServerboundTemplate SERVERBOUND play 协议模板
    public static readonly SimpleUnboundProtocol<ServerGamePacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerGamePacketListener>(
            ConnectionProtocol.Play, FlowDirection.Serverbound)
            .AddPacket(GamePacketTypes.ServerboundChat, ServerboundChatPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundClientCommand, ServerboundClientCommandPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundAcceptTeleportation, ServerboundAcceptTeleportationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMovePlayerPos, ServerboundMovePlayerPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerGamePacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND play 协议模板
    public static readonly SimpleUnboundProtocol<ClientGamePacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientGamePacketListener>(
            ConnectionProtocol.Play, FlowDirection.Clientbound)
            .AddPacket(GamePacketTypes.ClientboundLogin, ClientboundLoginPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerInfoUpdate, ClientboundPlayerInfoUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSystemChat, ClientboundSystemChatPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientGamePacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
