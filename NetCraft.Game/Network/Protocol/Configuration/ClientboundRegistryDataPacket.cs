using System.Collections.Generic;

namespace NetCraft.Game.Network.Protocol.Configuration;

//ClientboundRegistryDataPacket 服务端发送注册表数据
//对应原版 net.minecraft.network.protocol.configuration.ClientboundRegistryDataPacket
//原版依赖 ResourceKey 和 RegistrySynchronization.PackedRegistryEntry 简化为
//  Identifier RegistryKey 标识哪个注册表
//  byte[] Entries 透传原版 PackedRegistryEntry 列表的序列化字节
//  EntriesCodec 内部按 VarInt 长度前缀的字节数组解码无内容解析
//  未来 RegistrySynchronization 内核实现后改回强类型解析
public sealed record ClientboundRegistryDataPacket(Identifier RegistryKey, byte[] Entries) : Packet<ClientConfigurationPacketListener>
{
    public const int MaxEntriesLength = 1048576;

    public static StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket> StreamCodec { get; } = new RegistryDataCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundRegistryData;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleRegistryData(this);

    private sealed class RegistryDataCodec : StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket>
    {
        public ClientboundRegistryDataPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxEntriesLength));

        public void Encode(FriendlyByteBuf buf, ClientboundRegistryDataPacket value)
        {
            buf.WriteIdentifier(value.RegistryKey);
            buf.WriteByteArray(value.Entries, MaxEntriesLength);
        }
    }
}
