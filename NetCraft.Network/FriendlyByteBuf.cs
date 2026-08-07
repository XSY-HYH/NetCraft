using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace NetCraft.Network;

//FriendlyByteBuf 协议缓冲对应原版 net.minecraft.network.FriendlyByteBuf
//包装 MemoryStream 大端序读写支持 VarInt/UTF-8 字符串
//非 sealed 允许 RegistryFriendlyByteBuf 继承扩展 RegistryAccess
public class FriendlyByteBuf : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly bool _ownsStream;

    public FriendlyByteBuf() : this(new MemoryStream(), true) { }

    public FriendlyByteBuf(byte[] data) : this(new MemoryStream(data), true) { }

    public FriendlyByteBuf(MemoryStream stream, bool ownsStream)
    {
        _stream = stream;
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        _ownsStream = ownsStream;
    }

    //ReadableBytes 可读剩余字节数
    public int ReadableBytes => (int)(_stream.Length - _stream.Position);

    //IsReadable 是否可读
    public bool IsReadable => ReadableBytes > 0;

    //ReadBoolean 读 1 字节布尔
    public bool ReadBoolean() => _reader.ReadBoolean();

    //ReadByte 读 1 字节
    public byte ReadByte() => _reader.ReadByte();

    //ReadShort 读 2 字节大端 short
    public short ReadShort() => BinaryPrimitives.ReadInt16BigEndian(_reader.ReadBytes(2));

    //ReadInt 读 4 字节大端 int
    public int ReadInt() => BinaryPrimitives.ReadInt32BigEndian(_reader.ReadBytes(4));

    //ReadVarInt 读可变长度 int 最多 5 字节
    public int ReadVarInt()
    {
        int result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = _reader.ReadByte();
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    //ReadLong 读 8 字节大端 long
    public long ReadLong() => BinaryPrimitives.ReadInt64BigEndian(_reader.ReadBytes(8));

    //ReadVarLong 读可变长度 long 最多 10 字节
    public long ReadVarLong()
    {
        long result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = _reader.ReadByte();
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    //ReadFloat 读 4 字节大端 float
    public float ReadFloat() => BinaryPrimitives.ReadSingleBigEndian(_reader.ReadBytes(4));

    //ReadDouble 读 8 字节大端 double
    public double ReadDouble() => BinaryPrimitives.ReadDoubleBigEndian(_reader.ReadBytes(8));

    //ReadString 读 VarInt 长度前缀的 UTF-8 字符串
    public string ReadString(int maxLength = 32767)
    {
        var length = ReadVarInt();
        if (length > maxLength * 4) throw new InvalidOperationException($"字符串字节长度超限 {length}");
        var bytes = _reader.ReadBytes(length);
        var str = Encoding.UTF8.GetString(bytes);
        if (str.Length > maxLength) throw new InvalidOperationException($"字符串长度超限 {str.Length}");
        return str;
    }

    //ReadByteArray 读 VarInt 长度前缀的字节数组
    public byte[] ReadByteArray(int maxLength = 32767)
    {
        var length = ReadVarInt();
        if (length > maxLength) throw new InvalidOperationException($"字节数组长度超限 {length}");
        return _reader.ReadBytes(length);
    }

    //ReadBytes 读固定长度字节数组
    public byte[] ReadBytes(int length) => _reader.ReadBytes(length);

    //ReadUuid 读 16 字节大端 Guid
    public Guid ReadUuid()
    {
        var bytes = _reader.ReadBytes(16);
        //Java UUID 大端序 .NET Guid 内部混合端字节序需手动构造
        return new Guid(
            (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]),
            (ushort)(bytes[4] << 8 | bytes[5]),
            (ushort)(bytes[6] << 8 | bytes[7]),
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
    }

    //WriteUuid 写 16 字节大端 Guid
    public FriendlyByteBuf WriteUuid(Guid value)
    {
        var bytes = value.ToByteArray();
        //.NET Guid.ToByteArray 是混合端序需转为大端序
        //反转前 3 段 4-2-2 字节
        Span<byte> buf = stackalloc byte[16];
        buf[0] = bytes[3]; buf[1] = bytes[2]; buf[2] = bytes[1]; buf[3] = bytes[0];
        buf[4] = bytes[5]; buf[5] = bytes[4];
        buf[6] = bytes[7]; buf[7] = bytes[6];
        for (int i = 8; i < 16; i++) buf[i] = bytes[i];
        _writer.Write(buf);
        return this;
    }

    //ReadIdentifier 读 namespace:path 格式的 Identifier
    public NetCraft.Registry.Identifier ReadIdentifier()
    {
        var str = ReadString(32767);
        return NetCraft.Registry.Identifier.Parse(str);
    }

    //WriteIdentifier 写 Identifier 为 namespace:path 字符串
    public FriendlyByteBuf WriteIdentifier(NetCraft.Registry.Identifier identifier)
        => WriteString(identifier.ToString());

    //ReadNullable 读可选值 reader 处理非空情况
    public T? ReadNullable<T>(Func<FriendlyByteBuf, T> reader) where T : class
        => ReadBoolean() ? reader(this) : null;

    //WriteNullable 写可选值 writer 处理非空情况
    public FriendlyByteBuf WriteNullable<T>(T? value, Action<FriendlyByteBuf, T> writer) where T : class
    {
        if (value == null)
        {
            WriteBoolean(false);
            return this;
        }
        WriteBoolean(true);
        writer(this, value);
        return this;
    }

    //SkipBytes 跳过指定字节数
    public FriendlyByteBuf SkipBytes(int length)
    {
        _reader.ReadBytes(length);
        return this;
    }

    //WriteBoolean 写 1 字节布尔
    public FriendlyByteBuf WriteBoolean(bool value) { _writer.Write(value); return this; }

    //WriteByte 写 1 字节
    public FriendlyByteBuf WriteByte(byte value) { _writer.Write(value); return this; }

    //WriteShort 写 2 字节大端 short
    public FriendlyByteBuf WriteShort(short value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteInt 写 4 字节大端 int
    public FriendlyByteBuf WriteInt(int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteVarInt 写可变长度 int
    //优化点2.7：用Span批量写入避免多次_writer.Write调用
    public FriendlyByteBuf WriteVarInt(int value)
    {
        Span<byte> buf = stackalloc byte[5];
        int idx = 0;
        var v = (uint)value;
        while ((v & ~0x7Fu) != 0)
        {
            buf[idx++] = (byte)((v & 0x7F) | 0x80);
            v >>>= 7;
        }
        buf[idx++] = (byte)v;
        _writer.Write(buf[..idx]);
        return this;
    }

    //WriteLong 写 8 字节大端 long
    public FriendlyByteBuf WriteLong(long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteVarLong 写可变长度 long
    //优化点2.7：用Span批量写入避免多次_writer.Write调用
    public FriendlyByteBuf WriteVarLong(long value)
    {
        Span<byte> buf = stackalloc byte[10];
        int idx = 0;
        var v = (ulong)value;
        while ((v & ~0x7FUL) != 0)
        {
            buf[idx++] = (byte)((v & 0x7F) | 0x80);
            v >>>= 7;
        }
        buf[idx++] = (byte)v;
        _writer.Write(buf[..idx]);
        return this;
    }

    //WriteFloat 写 4 字节大端 float
    public FriendlyByteBuf WriteFloat(float value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteDouble 写 8 字节大端 double
    public FriendlyByteBuf WriteDouble(double value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteString 写 VarInt 长度前缀的 UTF-8 字符串
    public FriendlyByteBuf WriteString(string value, int maxLength = 32767)
    {
        if (value.Length > maxLength) throw new InvalidOperationException($"字符串长度超限 {value.Length}");
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(bytes.Length);
        _writer.Write(bytes);
        return this;
    }

    //WriteByteArray 写 VarInt 长度前缀的字节数组
    public FriendlyByteBuf WriteByteArray(byte[] value)
    {
        WriteVarInt(value.Length);
        _writer.Write(value);
        return this;
    }

    //WriteByteArray 写 VarInt 长度前缀的字节数组并校验最大长度
    public FriendlyByteBuf WriteByteArray(byte[] value, int maxLength)
    {
        if (value.Length > maxLength)
            throw new ArgumentException($"字节数组长度 {value.Length} 超过最大 {maxLength}", nameof(value));
        return WriteByteArray(value);
    }

    //WriteBytes 写固定长度字节数组
    public FriendlyByteBuf WriteBytes(byte[] value) { _writer.Write(value); return this; }

    //ToArray 返回底层流的所有字节
    public byte[] ToArray() => _stream.ToArray();

    //AsArray 返回底层流的可用范围字节
    public byte[] AsArray() => _stream.GetBuffer()[..(int)_stream.Length];

    public void Dispose()
    {
        if (_ownsStream)
        {
            _reader.Dispose();
            _writer.Dispose();
            _stream.Dispose();
        }
    }
}
