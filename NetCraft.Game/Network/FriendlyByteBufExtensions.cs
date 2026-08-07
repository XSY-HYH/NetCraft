using NetCraft.Primitives;
using NetCraft.Network;

namespace NetCraft.Game.Network;

//FriendlyByteBufExtensions 业务包需要的扩展方法
//FriendlyByteBuf 内核只保留基础类型读写业务类型 BlockPos/SectionPos/Enum 等在此扩展
public static class FriendlyByteBufExtensions
{
    //ReadUnsignedByte 读 1 字节为 int 对齐原版 readUnsignedByte
    public static int ReadUnsignedByte(this FriendlyByteBuf buf)
        => buf.ReadByte();

    //ReadBlockPos 读 packed long 还原 BlockPos
    public static BlockPos ReadBlockPos(this FriendlyByteBuf buf)
        => BlockPos.FromLong(buf.ReadLong());

    //WriteBlockPos 写 BlockPos 为 packed long
    public static FriendlyByteBuf WriteBlockPos(this FriendlyByteBuf buf, BlockPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadSectionPos 读 packed long 还原 SectionPos
    public static SectionPos ReadSectionPos(this FriendlyByteBuf buf)
        => SectionPos.Of(buf.ReadLong());

    //WriteSectionPos 写 SectionPos 为 packed long
    public static FriendlyByteBuf WriteSectionPos(this FriendlyByteBuf buf, SectionPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadEnum 读 VarInt 还原枚举值按声明顺序
    public static T ReadEnum<T>(this FriendlyByteBuf buf) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = buf.ReadVarInt();
        return values[ordinal];
    }

    //WriteEnum 写枚举值的声明顺序为 VarInt
    public static FriendlyByteBuf WriteEnum<T>(this FriendlyByteBuf buf, T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = Array.IndexOf(values, value);
        return buf.WriteVarInt(ordinal);
    }

    //ReadIntIdList 读 VarInt 长度前缀的 int 数组
    public static int[] ReadIntIdList(this FriendlyByteBuf buf)
    {
        int length = buf.ReadVarInt();
        int[] ids = new int[length];
        for (int i = 0; i < length; i++)
            ids[i] = buf.ReadVarInt();
        return ids;
    }

    //WriteIntIdList 写 int 数组为 VarInt 长度前缀 + VarInt 数组
    public static FriendlyByteBuf WriteIntIdList(this FriendlyByteBuf buf, int[] ids)
    {
        buf.WriteVarInt(ids.Length);
        foreach (int id in ids)
            buf.WriteVarInt(id);
        return buf;
    }
}
