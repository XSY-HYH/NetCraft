namespace NetCraft.Network;

//Packet 协议包接口对应原版 net.minecraft.network.protocol.Packet
//THandler 是协议处理器类型提供 Handle 方法接收处理器
public interface Packet<THandler>
{
    //PacketType 包类型标识用于注册和编码
    PacketType<THandler> Type { get; }

    //Handle 调用处理器的对应方法
    void Handle(THandler handler);

    //IsSkippable 是否可跳过默认 false 对齐原版 isSkippable
    //解码失败时若可跳过则丢弃包继续否则抛异常
    bool IsSkippable => false;

    //IsTerminal 是否终止包默认 false 对齐原版 isTerminal
    //终止包处理后关闭连接如 LoginDisconnectPacket
    bool IsTerminal => false;
}
