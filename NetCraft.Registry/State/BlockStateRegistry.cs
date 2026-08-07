using System.Text;

namespace NetCraft.Registry.State;

//BlockState 数据存储与查表对应原版优化点2.5
//所有 BlockState 实例的属性数据与neighbors集中存此避免每实例持有数组
//对齐 FerriteCore FastMap 思路struct BlockState 只持 int Id 数据查此表
public static class BlockStateRegistry
{
    private static readonly List<BlockStateData> _all = new();
    private static readonly Dictionary<int, BlockState> _statesById = new();

    //注册一个新 BlockState 返回 struct 包装
    public static BlockState Register(Block owner, PropertyBase[] keys, object?[] values)
    {
        if (keys.Length != values.Length)
            throw new ArgumentException("propertyKeys/propertyValues length mismatch");
        var id = _all.Count;
        _all.Add(new BlockStateData(owner, keys, values));
        var state = new BlockState(id);
        _statesById[id] = state;
        return state;
    }

    //注入预计算的 neighbors 表用 int 索引避免 BlockState 引用
    public static void InitializeNeighbors(int stateId, int[][] neighbors)
    {
        if (_all[stateId].Neighbors is not null)
            throw new InvalidOperationException("Neighbors already initialized");
        _all[stateId].Neighbors = neighbors;
    }

    public static int Count => _all.Count;

    public static Block Owner(int id) => _all[id].Owner;

    public static IReadOnlyCollection<PropertyBase> GetProperties(int id) => _all[id].PropertyKeys;

    public static bool IsSingletonState(int id) => _all[id].PropertyKeys.Length == 0;

    //属性索引查表线性查找对齐原版 ValueIndex
    private static int ValueIndex(int id, PropertyBase property)
    {
        var keys = _all[id].PropertyKeys;
        for (var i = 0; i < keys.Length; i++)
            if (keys[i] == property) return i;
        return -1;
    }

    public static bool HasProperty(int id, PropertyBase property) => ValueIndex(id, property) != -1;

    public static T GetValue<T>(int id, Property<T> property) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1)
            throw new ArgumentException($"Cannot get property {property} as it does not exist in {Owner(id)}");
        return (T)_all[id].PropertyValues[index]!;
    }

    public static T? GetOptionalValue<T>(int id, Property<T> property) where T : IComparable
    {
        var index = ValueIndex(id, property);
        return index == -1 ? default : (T)_all[id].PropertyValues[index]!;
    }

    public static T GetValueOrElse<T>(int id, Property<T> property, T defaultValue) where T : IComparable
    {
        var index = ValueIndex(id, property);
        return index == -1 ? defaultValue : (T)_all[id].PropertyValues[index]!;
    }

    public static BlockState SetValue<T>(int id, Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1)
            throw new ArgumentException($"Cannot set property {property} as it does not exist in {Owner(id)}");
        var valueIndex = property.GetInternalIndex(value);
        if (valueIndex < 0)
            throw new ArgumentException($"Cannot set property {property} to {value} on {Owner(id)}, not an allowed value");
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    public static BlockState TrySetValue<T>(int id, Property<T> property, T value) where T : IComparable
    {
        var index = ValueIndex(id, property);
        if (index == -1) return _statesById[id];
        var valueIndex = property.GetInternalIndex(value);
        if (valueIndex < 0) return _statesById[id];
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    //非泛型SetValue用于Codec反序列化不要求T为IComparable
    public static BlockState SetValue(int id, PropertyBase property, object value)
    {
        var index = ValueIndex(id, property);
        if (index == -1) return _statesById[id];
        var valueIndex = property.GetInternalIndexForValue(value);
        if (valueIndex < 0) return _statesById[id];
        return _statesById[_all[id].Neighbors![index][valueIndex]];
    }

    public static BlockState Cycle<T>(int id, Property<T> property) where T : IComparable
        => SetValue(id, property, FindNextInCollection(property.PossibleValues, GetValue(id, property)!));

    private static T FindNextInCollection<T>(IReadOnlyList<T> list, T t)
    {
        var count = list.Count;
        for (var i = 0; i < count; i++)
            if (EqualityComparer<T>.Default.Equals(list[i], t))
                return i + 1 == count ? list[0] : list[i + 1];
        return list[0];
    }

    public static IEnumerable<PropertyValue> GetValues(int id)
    {
        var data = _all[id];
        for (var i = 0; i < data.PropertyKeys.Length; i++)
            yield return new PropertyValue(data.PropertyKeys[i], data.PropertyValues[i]!);
    }

    public static string ToString(int id)
    {
        var data = _all[id];
        var builder = new StringBuilder();
        builder.Append(data.Owner);
        if (data.PropertyKeys.Length > 0)
        {
            builder.Append('[');
            builder.Append(string.Join(",", GetValues(id).Select(v => v.ToString())));
            builder.Append(']');
        }
        return builder.ToString();
    }

    //重置仅供测试使用清空所有已注册数据
    public static void Reset()
    {
        _all.Clear();
        _statesById.Clear();
    }

    private sealed class BlockStateData
    {
        public Block Owner { get; }
        public PropertyBase[] PropertyKeys { get; }
        public object?[] PropertyValues { get; }
        public int[][]? Neighbors { get; set; }

        public BlockStateData(Block owner, PropertyBase[] keys, object?[] values)
        {
            Owner = owner;
            PropertyKeys = keys;
            PropertyValues = values;
        }
    }
}
