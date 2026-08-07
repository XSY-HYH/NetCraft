using NetCraft.Registry;
using NetCraft.Registry.State;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage.LevelGen;

//Heightmap 高度图实例对应原版 net.minecraft.world.level.levelgen.Heightmap
//每列存一个高度值用 BitStorage 紧凑存储
//Types 枚举引用 NetCraft.Registry.Heightmap.Types 避免重复定义
public sealed class Heightmap
{
    //每列最小高度对应原版 -1 表示空列
    public const int MinValue = -1;

    private readonly HeightmapRegistry.Types _type;
    private readonly int _minY;
    private readonly int _height;
    private readonly BitStorage _storage;

    public Heightmap(HeightmapRegistry.Types type, int minY, int height)
    {
        _type = type;
        _minY = minY;
        _height = height;
        //每列存储 height+1 个可能值(0..height)用 ceil(log2(height+1)) 位
        var bits = Math.Max(1, BitCount(height + 1));
        _storage = new SimpleBitStorage(bits, 16 * 16);
    }

    private Heightmap(HeightmapRegistry.Types type, int minY, int height, BitStorage storage)
    {
        _type = type;
        _minY = minY;
        _height = height;
        _storage = storage;
    }

    public HeightmapRegistry.Types Type => _type;
    public int MinY => _minY;
    public int Height => _height;

    //从 storage 中读列高度对应原版 getHeight
    public int GetFirstAvailable(int x, int z)
    {
        var raw = _storage.Get(GetIndex(x, z));
        return raw == 0 ? MinValue : _minY + raw - 1;
    }

    //更新指定列高度若新高度高于已存值则写入对应原版 update
    public bool Update(int x, int y, int z)
    {
        var index = GetIndex(x, z);
        var current = _storage.Get(index);
        var candidate = y - _minY + 1;
        if (candidate > current)
        {
            _storage.Set(index, candidate);
            return true;
        }
        return false;
    }

    //直接设置列高度对应原版 setHeight
    public void SetHeight(int x, int z, int y)
    {
        var raw = y < _minY ? 0 : y - _minY + 1;
        _storage.Set(GetIndex(x, z), raw);
    }

    //原始 long 数组对应原版 getData
    public long[] GetData() => _storage.GetRaw();

    //从原始数据重建高度图对应原版 setData
    public static Heightmap FromData(HeightmapRegistry.Types type, int minY, int height, long[] data)
    {
        var bits = Math.Max(1, BitCount(height + 1));
        var storage = new SimpleBitStorage(bits, 16 * 16, data);
        return new Heightmap(type, minY, height, storage);
    }

    private static int GetIndex(int x, int z) => (z & 15) * 16 + (x & 15);

    //计算最小位数对应原版 ceil(log2(value))
    private static int BitCount(int value)
    {
        var bits = 0;
        var v = value;
        while (v > 0) { bits++; v >>= 1; }
        return bits == 0 ? 1 : bits;
    }
}

//HeightmapFunction 高度图判定函数对应原版 Heightmap.Function
//判断方块状态是否参与该高度图计算
public delegate bool HeightmapFunction(BlockState state);
