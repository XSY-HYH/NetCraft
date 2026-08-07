namespace NetCraft.Registry.State;

//布尔型属性，对应原版 BooleanProperty
//两个可能值 true/false
public sealed class BooleanProperty : Property<bool>
{
    private static readonly IReadOnlyList<bool> PossibleValuesBool = new[] { false, true };
    private static readonly IReadOnlyList<object> PossibleValuesObj = new object[] { false, true };

    public BooleanProperty(string name) : base(name) { }

    public override IReadOnlyList<bool> PossibleValues => PossibleValuesBool;

    public override IReadOnlyList<object> PossibleValuesAsObjects => PossibleValuesObj;

    public override string GetName(bool value) => value ? "true" : "false";

    public override bool TryGetValue(string name, out bool value)
    {
        switch (name)
        {
            case "true": value = true; return true;
            case "false": value = false; return true;
            default: value = false; return false;
        }
    }

    public override int GetInternalIndex(bool value) => value ? 1 : 0;
}

//整型属性，对应原版 IntegerProperty
//取值范围 [min, min+count)
public sealed class IntegerProperty : Property<int>
{
    private readonly int _min;
    private readonly int _count;
    private readonly IReadOnlyList<int> _values;
    private readonly IReadOnlyList<object> _valuesObj;

    public IntegerProperty(string name, int min, int maxInclusive) : base(name)
    {
        _min = min;
        _count = maxInclusive - min + 1;
        if (_count < 2)
            throw new ArgumentException($"IntegerProperty {name} needs at least 2 values");
        var arr = new int[_count];
        for (var i = 0; i < _count; i++) arr[i] = min + i;
        _values = arr;
        _valuesObj = arr.Select(v => (object)v).ToArray();
    }

    public override IReadOnlyList<int> PossibleValues => _values;

    public override IReadOnlyList<object> PossibleValuesAsObjects => _valuesObj;

    public override string GetName(int value) => value.ToString();

    public override bool TryGetValue(string name, out int value)
    {
        if (int.TryParse(name, out value) && value >= _min && value < _min + _count)
            return true;
        value = default;
        return false;
    }

    public override int GetInternalIndex(int value) => value >= _min && value < _min + _count ? value - _min : -1;
}

//枚举型属性对应原版 EnumProperty
//泛型 T 约束为 struct Enum 取所有枚举值作为合法取值
public sealed class EnumProperty<T> : Property<T> where T : struct, Enum
{
    private static readonly T[] ValuesArr = Enum.GetValues<T>();
    private readonly Dictionary<string, T> _byName;

    public EnumProperty(string name) : base(name)
    {
        _byName = new(ValuesArr.Length, StringComparer.Ordinal);
        foreach (var v in ValuesArr)
            _byName[v.ToString()] = v;
    }

    public override IReadOnlyList<T> PossibleValues => ValuesArr;

    public override string GetName(T value) => value.ToString();

    public override bool TryGetValue(string name, out T value)
        => _byName.TryGetValue(name, out value);

    public override int GetInternalIndex(T value) => Array.IndexOf(ValuesArr, value);
}
