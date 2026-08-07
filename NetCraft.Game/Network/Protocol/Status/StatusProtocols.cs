using NetCraft.Game.Network.Protocol.Ping;

namespace NetCraft.Game.Network.Protocol.Status;

//StatusProtocols status 协议对应原版 net.minecraft.network.protocol.status.StatusProtocols
//简化版只注册 status 请求/响应不混 ping
//原版在同一协议中注册 ping 包但因 C# 接口不协变 ServerboundPingRequestPacket 是 Packet<ServerPingPacketListener>
//不能直接作为 Packet<ServerStatusPacketListener> 注册
//ping 协议端到端测试用单独的 PingProtocols
public static class StatusProtocols
{
    //ServerboundTemplate SERVERBOUND status 协议模板只含 status 请求
    public static readonly SimpleUnboundProtocol<ServerStatusPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Serverbound)
            .AddPacket(StatusPacketTypes.ServerboundStatusRequest, ServerboundStatusRequestPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerStatusPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND status 协议模板只含 status 响应
    public static readonly SimpleUnboundProtocol<ClientStatusPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Clientbound)
            .AddPacket(StatusPacketTypes.ClientboundStatusResponse, ClientboundStatusResponsePacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientStatusPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}

//PingProtocols ping 协议独立模板对应原版在 StatusProtocols 中的 ping 注册
//简化版独立注册 ping 请求/响应用于 ping 端到端测试
public static class PingProtocols
{
    //ServerboundTemplate SERVERBOUND ping 协议模板
    public static readonly SimpleUnboundProtocol<ServerPingPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerPingPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Serverbound)
            .AddPacket(PingPacketTypes.ServerboundPingRequest, ServerboundPingRequestPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerPingPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND ping 协议模板
    public static readonly SimpleUnboundProtocol<ClientPongPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientPongPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Clientbound)
            .AddPacket(PingPacketTypes.ClientboundPongResponse, ClientboundPongResponsePacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientPongPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
