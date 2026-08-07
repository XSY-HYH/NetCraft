using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//DensityFunctionCodecs 密度函数编解码集合对应原版 DensityFunctions.CODEC
//为每个 DensityFunction 子类提供 MapCodec<DensityFunction> 字段
//通过 DensityFunctionCodec dispatch 按 type 字段串查找子类 codec
public static class DensityFunctionCodecs
{
    //ConstantCodec 常量密度函数 codec 对应原版 Constant.CODEC
    //直接 double 序列化type 标识 "constant"
    public static readonly MapCodec<DensityFunction> ConstantCodec =
        Codecs.Double.ComapFlatMap(
            v => DataResult<DensityFunction>.Success(new Constant(v)),
            df => ((Constant)df).Value).FieldOf("argument");

    //ClampCodec 钳制密度函数 codec 对应原版 Clamp.CODEC
    //input/min/max 三字段
    public static readonly MapCodec<DensityFunction> ClampCodec =
        RecordCodecBuilder.Of3(
            DensityFunctionCodecHelper.DensityFunctionField("input").ForGetter<DensityFunction, DensityFunction>(df => ((Clamp)df).Input),
            Codecs.Double.FieldOf("min").ForGetter<DensityFunction, double>(df => ((Clamp)df).Min),
            Codecs.Double.FieldOf("max").ForGetter<DensityFunction, double>(df => ((Clamp)df).Max),
            (input, min, max) => new Clamp(input, min, max));

    //MulOrAddCodec 乘加密度函数 codec 对应原版 MulOrAdd.CODEC
    //type 字符串 add/mul + value + input 三字段
    public static readonly MapCodec<DensityFunction> MulOrAddCodec =
        RecordCodecBuilder.Of3(
            Codecs.String.FieldOf("type").ForGetter<DensityFunction, string>(df => ((MulOrAdd)df).Type == MulOrAdd.OpType.Add ? "add" : "mul"),
            Codecs.Double.FieldOf("argument").ForGetter<DensityFunction, double>(df => ((MulOrAdd)df).Value),
            DensityFunctionCodecHelper.DensityFunctionField("input").ForGetter<DensityFunction, DensityFunction>(df => ((MulOrAdd)df).Input),
            (type, value, input) => MulOrAdd.Of(type == "add" ? MulOrAdd.OpType.Add : MulOrAdd.OpType.Mul, value, input));

    //Ap2Codec 二元运算密度函数 codec 对应原版 Ap2.CODEC
    //type 字符串 max/min/add/mul + input1 + input2 三字段
    public static readonly MapCodec<DensityFunction> Ap2Codec =
        RecordCodecBuilder.Of3(
            Codecs.String.FieldOf("type").ForGetter<DensityFunction, string>(df => Ap2TypeString(((Ap2)df).Type)),
            DensityFunctionCodecHelper.DensityFunctionField("argument1").ForGetter<DensityFunction, DensityFunction>(df => ((Ap2)df).Input1),
            DensityFunctionCodecHelper.DensityFunctionField("argument2").ForGetter<DensityFunction, DensityFunction>(df => ((Ap2)df).Input2),
            (type, in1, in2) => new Ap2(ParseAp2Type(type), in1, in2));

    //YClampedGradientCodec Y 轴梯度 codec 对应原版 YClampedGradient.CODEC
    //fromY/toY/fromValue/toValue 四字段
    public static readonly MapCodec<DensityFunction> YClampedGradientCodec =
        RecordCodecBuilder.Of4(
            Codecs.Int.FieldOf("from_y").ForGetter<DensityFunction, int>(df => ((YClampedGradient)df).FromY),
            Codecs.Int.FieldOf("to_y").ForGetter<DensityFunction, int>(df => ((YClampedGradient)df).ToY),
            Codecs.Double.FieldOf("from_value").ForGetter<DensityFunction, double>(df => ((YClampedGradient)df).FromValue),
            Codecs.Double.FieldOf("to_value").ForGetter<DensityFunction, double>(df => ((YClampedGradient)df).ToValue),
            (fromY, toY, fromValue, toValue) => new YClampedGradient(fromY, toY, fromValue, toValue));

    private static string Ap2TypeString(Ap2.OpType type) => type switch
    {
        Ap2.OpType.Max => "max",
        Ap2.OpType.Min => "min",
        Ap2.OpType.Add => "add",
        Ap2.OpType.Mul => "mul",
        _ => throw new NotSupportedException($"Unsupported Ap2 type: {type}")
    };

    private static Ap2.OpType ParseAp2Type(string s) => s switch
    {
        "max" => Ap2.OpType.Max,
        "min" => Ap2.OpType.Min,
        "add" => Ap2.OpType.Add,
        "mul" => Ap2.OpType.Mul,
        _ => throw new NotSupportedException($"Unknown Ap2 type string: {s}")
    };
}

//DensityFunctionCodecHelper 密度函数 codec 辅助工具
//提供 DensityFunction 字段定义与 dispatch codec 入口
public static class DensityFunctionCodecHelper
{
    //DensityFunctionField 创建 DensityFunction 字段对应原版 DensityFunction.HOLDER_CODEC.fieldOf(name)
    //dispatch 通过 DensityFunctionCodec 单例解码子类型
    public static MapCodec<DensityFunction> DensityFunctionField(string name)
        => DensityFunctionCodec.Instance.FieldOf(name);
}

//DensityFunctionCodec 密度函数 dispatch codec 对应原版 DensityFunctions.CODEC
//按 "type" 字段字符串查找子类 MapCodec<DensityFunction>
//未识别 type 返回 Error
public sealed class DensityFunctionCodec : AbstractMapCodec<DensityFunction>
{
    public static readonly DensityFunctionCodec Instance = new();

    private static readonly IReadOnlyDictionary<string, MapCodec<DensityFunction>> _byId = new Dictionary<string, MapCodec<DensityFunction>>
    {
        ["constant"] = DensityFunctionCodecs.ConstantCodec,
        ["clamp"] = DensityFunctionCodecs.ClampCodec,
        ["add"] = DensityFunctionCodecs.MulOrAddCodec,
        ["mul"] = DensityFunctionCodecs.MulOrAddCodec,
        ["max"] = DensityFunctionCodecs.Ap2Codec,
        ["min"] = DensityFunctionCodecs.Ap2Codec,
        ["y_clamped_gradient"] = DensityFunctionCodecs.YClampedGradientCodec
    };

    //LookupId 运行时类型到 type 字符串对应原版 dispatch 写入 type 字段
    private static string? LookupId(DensityFunction df)
    {
        return df switch
        {
            Constant => "constant",
            Clamp => "clamp",
            MulOrAdd m => m.Type == MulOrAdd.OpType.Add ? "add" : "mul",
            Ap2 a => a.Type switch
            {
                Ap2.OpType.Max => "max",
                Ap2.OpType.Min => "min",
                Ap2.OpType.Add => "add",
                Ap2.OpType.Mul => "mul",
                _ => null
            },
            YClampedGradient => "y_clamped_gradient",
            _ => null
        };
    }

    //Ap2 add/mul 与 MulOrAdd add/mul 共享 type 字符串 dispatch 时区分
    //实际原版通过 MapCodec 自身 type 字段区分此处简化用 LookupId 输出实际类型字符串
    public override DataResult<DensityFunction> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeOpt = input.Get("type");
        if (!typeOpt.IsPresent)
            return DataResult<DensityFunction>.Error(() => "missing type field for DensityFunction");
        var typeResult = ops.GetStringValue(typeOpt.Get()).Result();
        if (!typeResult.IsPresent)
            return DataResult<DensityFunction>.Error(() => "type field is not a string");
        var type = typeResult.Get();
        if (!_byId.TryGetValue(type, out var codec))
            return DataResult<DensityFunction>.Error(() => $"unknown DensityFunction type: {type}");

        //MulOrAdd 与 Ap2 共用 add/mul type 字符串dispatch 时按字段名区分
        //MulOrAdd 用 input 单字段Ap2 用 argument1/argument2 两字段
        if (type is "add" or "mul")
        {
            if (input.Get("argument1").IsPresent)
                return DensityFunctionCodecs.Ap2Codec.Decode(ops, input);
            return DensityFunctionCodecs.MulOrAddCodec.Decode(ops, input);
        }
        return codec.Decode(ops, input);
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DensityFunction value, RecordBuilder<U> builder)
    {
        var id = LookupId(value);
        if (id is null)
        {
            builder.Add("type", DataResult<U>.Error(() => $"unsupported DensityFunction type: {value?.GetType().Name}").GetOrThrow());
            return builder;
        }
        builder.Add("type", ops.CreateString(id));
        var codec = _byId[id];
        codec.EncodeTo(ops, value, builder);
        return builder;
    }
}
