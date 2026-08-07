using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Login;

//LoginPacketTypes login 包类型注册对应原版 net.minecraft.network.protocol.login.LoginPacketTypes
//5 个 clientbound + 4 个 serverbound 包类型
public static class LoginPacketTypes
{
    //ClientboundCustomQuery 自定义查询请求包 minecraft:custom_query
    public static readonly PacketType<ClientLoginPacketListener> ClientboundCustomQuery =
        CreateClientbound<ClientLoginPacketListener>(0, "custom_query");

    //ClientboundLoginFinished 登录完成包 minecraft:login_finished
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginFinished =
        CreateClientbound<ClientLoginPacketListener>(1, "login_finished");

    //ClientboundHello 加密握手包 minecraft:hello
    public static readonly PacketType<ClientLoginPacketListener> ClientboundHello =
        CreateClientbound<ClientLoginPacketListener>(2, "hello");

    //ClientboundLoginCompression 压缩通知包 minecraft:login_compression
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginCompression =
        CreateClientbound<ClientLoginPacketListener>(3, "login_compression");

    //ClientboundLoginDisconnect 登录断开包 minecraft:login_disconnect
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginDisconnect =
        CreateClientbound<ClientLoginPacketListener>(4, "login_disconnect");

    //ServerboundCustomQueryAnswer 自定义查询应答包 minecraft:custom_query_answer
    public static readonly PacketType<ServerLoginPacketListener> ServerboundCustomQueryAnswer =
        CreateServerbound<ServerLoginPacketListener>(0, "custom_query_answer");

    //ServerboundHello 登录 hello 包 minecraft:hello
    public static readonly PacketType<ServerLoginPacketListener> ServerboundHello =
        CreateServerbound<ServerLoginPacketListener>(1, "hello");

    //ServerboundKey 加密密钥包 minecraft:key
    public static readonly PacketType<ServerLoginPacketListener> ServerboundKey =
        CreateServerbound<ServerLoginPacketListener>(2, "key");

    //ServerboundLoginAcknowledged 登录确认包 minecraft:login_acknowledged
    public static readonly PacketType<ServerLoginPacketListener> ServerboundLoginAcknowledged =
        CreateServerbound<ServerLoginPacketListener>(3, "login_acknowledged");

    //CreateClientbound 注册 clientbound 包类型 id 为网络 ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(
            id, ConnectionProtocol.Login, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound 注册 serverbound 包类型
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(
            id, ConnectionProtocol.Login, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
