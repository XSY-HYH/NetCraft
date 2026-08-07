using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacketData 光照更新数据对应原版 ClientboundLightUpdatePacketData
//阶段 E 最小可用实现持有 TrustEdges 与区块光数据占位真实光照引擎接入后扩展
//原版持有 LightData/SkyYMask/BlockYMask 等字段此处简化为字节数组占位
public sealed record ClientboundLightUpdatePacketData(
    bool TrustEdges,
    byte[] SkyLight,
    byte[] BlockLight)
{
    //Empty 空光照数据用于无光照场景或编解码占位
    public static ClientboundLightUpdatePacketData Empty { get; }
        = new(false, Array.Empty<byte>(), Array.Empty<byte>());

    //Write 写入 FriendlyByteBuf 对齐原版 ClientboundLightUpdatePacketData 序列化
    //格式 TrustEdges (bool) + SkyLight (VarInt 长度前缀字节数组) + BlockLight (VarInt 长度前缀字节数组)
    public void Write(FriendlyByteBuf buf)
    {
        buf.WriteBoolean(TrustEdges);
        buf.WriteByteArray(SkyLight);
        buf.WriteByteArray(BlockLight);
    }

    //Read 从 FriendlyByteBuf 读取 ClientboundLightUpdatePacketData
    public static ClientboundLightUpdatePacketData Read(FriendlyByteBuf buf)
    {
        var trustEdges = buf.ReadBoolean();
        var skyLight = buf.ReadByteArray();
        var blockLight = buf.ReadByteArray();
        return new ClientboundLightUpdatePacketData(trustEdges, skyLight, blockLight);
    }
}
