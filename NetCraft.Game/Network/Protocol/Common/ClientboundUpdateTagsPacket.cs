using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Common;

//ClientboundUpdateTagsPacket 更新标签包对应原版 net.minecraft.network.protocol.common.ClientboundUpdateTagsPacket
//含 Dictionary<Identifier, int[]> 标签内容原版用 TagNetworkSerialization 简化为直接 map
//键是注册表 Identifier 值是该注册表下的标签条目 ID 数组
public sealed record ClientboundUpdateTagsPacket(Dictionary<Identifier, int[]> Tags) : Packet<ClientCommonPacketListener>
{
    public const int MaxEntries = 32767;
    public const int MaxTagLength = 32767;

    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateTagsPacket> StreamCodec { get; } = new UpdateTagsCodec();

    public PacketType<ClientCommonPacketListener> Type => ConfigurationPacketTypes.ClientboundUpdateTags;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleUpdateTags(this);

    private sealed class UpdateTagsCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateTagsPacket>
    {
        public ClientboundUpdateTagsPacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var tags = new Dictionary<Identifier, int[]>(Math.Min(count, MaxEntries));
            for (int i = 0; i < count; i++)
            {
                var key = buf.ReadIdentifier();
                var len = Math.Min(buf.ReadVarInt(), MaxTagLength);
                var arr = new int[len];
                for (int j = 0; j < len; j++)
                    arr[j] = buf.ReadVarInt();
                tags[key] = arr;
            }
            return new(tags);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateTagsPacket value)
        {
            buf.WriteVarInt(value.Tags.Count);
            foreach (var kv in value.Tags)
            {
                buf.WriteIdentifier(kv.Key);
                buf.WriteVarInt(kv.Value.Length);
                foreach (var id in kv.Value)
                    buf.WriteVarInt(id);
            }
        }
    }
}
