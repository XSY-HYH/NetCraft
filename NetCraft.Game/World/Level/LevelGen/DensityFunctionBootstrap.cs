using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//ObjectMapCodecAdapter MapCodec<T> 到 MapCodec<object> 的适配器
//解决 C# MapCodec<T> 不支持协变无法直接 cast 为 MapCodec<object> 的问题
//通过包装内层 MapCodec<T> 把 object cast 为 T 调用真实 DecodeEncodeTo 再 cast 回 object
//用于把 DensityFunction 子类 MapCodec<DensityFunction> 注册到 Registry<MapCodec<object>>
public sealed class ObjectMapCodecAdapter<T> : AbstractMapCodec<object> where T : class
{
    private readonly MapCodec<T> _inner;

    public ObjectMapCodecAdapter(MapCodec<T> inner) { _inner = inner; }

    public override DataResult<object> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode(ops, input).Map(t => (object)t!);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, object value, RecordBuilder<U> builder)
    {
        if (value is T typed)
            _inner.EncodeTo(ops, typed, builder);
        else
            builder.Add("error", DataResult<U>.Error(() => $"value is not of type {typeof(T).Name}").GetOrThrow());
        return builder;
    }
}

//DensityFunctionBootstrap 密度函数引导注册对应原版 DensityFunctions.BOOTSTRAP
//把各 DensityFunction 子类 MapCodec 注册到 DENSITY_FUNCTION_TYPE 注册表
//必须在 BuiltInRegistries.BootStrap 之前调用否则 Freeze 后无法注册
public static class DensityFunctionBootstrap
{
    private static bool _registered;

    //RegisterAll 注册所有 DensityFunction 子类 MapCodec 到 DENSITY_FUNCTION_TYPE
    //重复调用幂等返回避免重复注册
    public static void RegisterAll()
    {
        if (_registered) return;
        var registry = NetCraft.Registry.BuiltInRegistries.DENSITY_FUNCTION_TYPE;
        Register(registry, "constant", DensityFunctionCodecs.ConstantCodec);
        Register(registry, "clamp", DensityFunctionCodecs.ClampCodec);
        Register(registry, "add", DensityFunctionCodecs.MulOrAddCodec);
        Register(registry, "mul", DensityFunctionCodecs.MulOrAddCodec);
        Register(registry, "max", DensityFunctionCodecs.Ap2Codec);
        Register(registry, "min", DensityFunctionCodecs.Ap2Codec);
        Register(registry, "y_clamped_gradient", DensityFunctionCodecs.YClampedGradientCodec);
        _registered = true;
    }

    //Register 把 MapCodec<DensityFunction> 包装为 MapCodec<object> 注册到 DENSITY_FUNCTION_TYPE
    private static void Register(NetCraft.Registry.Registry<NetCraft.Codec.MapCodec<object>> registry, string name, MapCodec<DensityFunction> codec)
    {
        var adapter = new ObjectMapCodecAdapter<DensityFunction>(codec);
        NetCraft.Registry.Registry<NetCraft.Codec.MapCodec<object>>.Register(registry, name, adapter);
    }

    public static void Reset()
    {
        _registered = false;
    }
}
