using NetCraft.Network;

namespace NetCraft.Test.Modules;

//Network 子库测试
//覆盖 FriendlyByteBuf 大端序读写 round-trip + VarInt 编码
internal static class NetworkTests
{
    public const string Module = "network";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("FriendlyByteBuf int round-trip", TestIntRoundTrip);
        yield return ("FriendlyByteBuf long round-trip", TestLongRoundTrip);
        yield return ("FriendlyByteBuf varInt small", TestVarIntSmall);
        yield return ("FriendlyByteBuf varInt large", TestVarIntLarge);
        yield return ("FriendlyByteBuf string round-trip", TestStringRoundTrip);
        yield return ("FriendlyByteBuf float round-trip", TestFloatRoundTrip);
        yield return ("FriendlyByteBuf bool round-trip", TestBoolRoundTrip);
        yield return ("PacketTypeRegistry register/find", TestPacketTypeRegistry);
        yield return ("CompressionHelper round-trip uncompressed", TestCompressionUncompressed);
        yield return ("CompressionHelper round-trip compressed", TestCompressionCompressed);
        yield return ("CryptoHelper round-trip", TestCryptoRoundTrip);
    }

    //压缩阈值以下数据不压缩 round-trip 仍正确
    private static bool TestCompressionUncompressed()
    {
        var data = new byte[] { 1, 2, 3 };
        var compressed = CompressionHelper.CompressIfNeeded(data, threshold: 100);
        var decompressed = CompressionHelper.Decompress(compressed);
        return decompressed.SequenceEqual(data);
    }

    //压缩阈值以上数据走 deflate 压缩 round-trip 仍正确
    private static bool TestCompressionCompressed()
    {
        var data = new byte[200];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 7);
        var compressed = CompressionHelper.CompressIfNeeded(data, threshold: 10);
        var decompressed = CompressionHelper.Decompress(compressed);
        return decompressed.SequenceEqual(data);
    }

    //AES-CFB 加解密 round-trip 同 key 同 IV 对称
    private static bool TestCryptoRoundTrip()
    {
        var key = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var data = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
        var encrypted = CryptoHelper.Encrypt(data, key);
        var decrypted = CryptoHelper.Decrypt(encrypted, key);
        return decrypted.SequenceEqual(data);
    }

    private static bool TestIntRoundTrip()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteInt(0x12345678);
        var data = buf.AsArray();
        if (data.Length != 4) return false;
        if (data[0] != 0x12 || data[3] != 0x78) return false;
        using var read = new FriendlyByteBuf(data);
        return read.ReadInt() == 0x12345678;
    }

    private static bool TestLongRoundTrip()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteLong(0x123456789ABCDEF0L);
        using var read = new FriendlyByteBuf(buf.AsArray());
        return read.ReadLong() == 0x123456789ABCDEF0L;
    }

    private static bool TestVarIntSmall()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteVarInt(127);
        if (buf.AsArray().Length != 1) return false;
        using var read = new FriendlyByteBuf(buf.AsArray());
        return read.ReadVarInt() == 127;
    }

    private static bool TestVarIntLarge()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteVarInt(0x12345678);
        using var read = new FriendlyByteBuf(buf.AsArray());
        return read.ReadVarInt() == 0x12345678;
    }

    private static bool TestStringRoundTrip()
    {
        using var buf = new FriendlyByteBuf();
        var s = "hello 喵 NetCraft";
        buf.WriteString(s);
        using var read = new FriendlyByteBuf(buf.AsArray());
        return read.ReadString() == s;
    }

    private static bool TestFloatRoundTrip()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteFloat(3.14f);
        using var read = new FriendlyByteBuf(buf.AsArray());
        return Math.Abs(read.ReadFloat() - 3.14f) < 0.0001f;
    }

    private static bool TestBoolRoundTrip()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteBoolean(true);
        buf.WriteBoolean(false);
        using var read = new FriendlyByteBuf(buf.AsArray());
        return read.ReadBoolean() && !read.ReadBoolean();
    }

    private static bool TestPacketTypeRegistry()
    {
        PacketTypeRegistry.Clear();
        PacketTypeRegistry.Register<object>(1, ConnectionProtocol.Play, FlowDirection.Clientbound);
        var type = PacketTypeRegistry.FindById(ConnectionProtocol.Play, FlowDirection.Clientbound, 1);
        return type != null && type.Id == 1;
    }
}
