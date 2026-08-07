namespace NetCraft.Codec;

//RecordCodecBuilder对应原版com.mojang.serialization.codecs.RecordCodecBuilder
//用N-ary重载模拟原版group(...).apply(instance, ctor)链式调用
//覆盖Of2..Of8常见record字段数
public static class RecordCodecBuilder
{
    public static Codec<T> Of2<T, F1, F2>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, Func<F1, F2, T> ctor)
        => new RecordCodec2<T, F1, F2>(f1, f2, ctor);

    public static Codec<T> Of3<T, F1, F2, F3>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        Func<F1, F2, F3, T> ctor)
        => new RecordCodec3<T, F1, F2, F3>(f1, f2, f3, ctor);

    public static Codec<T> Of4<T, F1, F2, F3, F4>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3, FieldCodec<T, F4> f4,
        Func<F1, F2, F3, F4, T> ctor)
        => new RecordCodec4<T, F1, F2, F3, F4>(f1, f2, f3, f4, ctor);

    public static Codec<T> Of5<T, F1, F2, F3, F4, F5>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3, FieldCodec<T, F4> f4,
        FieldCodec<T, F5> f5, Func<F1, F2, F3, F4, F5, T> ctor)
        => new RecordCodec5<T, F1, F2, F3, F4, F5>(f1, f2, f3, f4, f5, ctor);

    public static Codec<T> Of6<T, F1, F2, F3, F4, F5, F6>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3, FieldCodec<T, F4> f4,
        FieldCodec<T, F5> f5, FieldCodec<T, F6> f6, Func<F1, F2, F3, F4, F5, F6, T> ctor)
        => new RecordCodec6<T, F1, F2, F3, F4, F5, F6>(f1, f2, f3, f4, f5, f6, ctor);

    public static Codec<T> Of7<T, F1, F2, F3, F4, F5, F6, F7>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3, FieldCodec<T, F4> f4,
        FieldCodec<T, F5> f5, FieldCodec<T, F6> f6, FieldCodec<T, F7> f7,
        Func<F1, F2, F3, F4, F5, F6, F7, T> ctor)
        => new RecordCodec7<T, F1, F2, F3, F4, F5, F6, F7>(f1, f2, f3, f4, f5, f6, f7, ctor);

    public static Codec<T> Of8<T, F1, F2, F3, F4, F5, F6, F7, F8>(
        FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3, FieldCodec<T, F4> f4,
        FieldCodec<T, F5> f5, FieldCodec<T, F6> f6, FieldCodec<T, F7> f7, FieldCodec<T, F8> f8,
        Func<F1, F2, F3, F4, F5, F6, F7, F8, T> ctor)
        => new RecordCodec8<T, F1, F2, F3, F4, F5, F6, F7, F8>(f1, f2, f3, f4, f5, f6, f7, f8, ctor);
}

//2字段record codec
//decode逐字段Decode后用FlatMap组合调构造函数encode遍历字段EncodeTo写入builder
internal sealed class RecordCodec2<T, F1, F2> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly Func<F1, F2, T> _ctor;

    public RecordCodec2(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, Func<F1, F2, T> ctor)
    {
        _f1 = f1; _f2 = f2; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input).Map(v2 => _ctor(v1, v2)));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        return builder;
    }
}

//3字段record codec
internal sealed class RecordCodec3<T, F1, F2, F3> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly Func<F1, F2, F3, T> _ctor;

    public RecordCodec3(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        Func<F1, F2, F3, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input).Map(v3 => _ctor(v1, v2, v3))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        return builder;
    }
}

//4字段record codec
internal sealed class RecordCodec4<T, F1, F2, F3, F4> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly FieldCodec<T, F4> _f4;
    private readonly Func<F1, F2, F3, F4, T> _ctor;

    public RecordCodec4(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        FieldCodec<T, F4> f4, Func<F1, F2, F3, F4, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input)
                    .FlatMap(v3 => _f4.Codec.Decode(ops, input).Map(v4 => _ctor(v1, v2, v3, v4)))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        _f4.Codec.EncodeTo(ops, _f4.Getter(value), builder);
        return builder;
    }
}

//5字段record codec
internal sealed class RecordCodec5<T, F1, F2, F3, F4, F5> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly FieldCodec<T, F4> _f4;
    private readonly FieldCodec<T, F5> _f5;
    private readonly Func<F1, F2, F3, F4, F5, T> _ctor;

    public RecordCodec5(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        FieldCodec<T, F4> f4, FieldCodec<T, F5> f5, Func<F1, F2, F3, F4, F5, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4; _f5 = f5; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input)
                    .FlatMap(v3 => _f4.Codec.Decode(ops, input)
                        .FlatMap(v4 => _f5.Codec.Decode(ops, input).Map(v5 => _ctor(v1, v2, v3, v4, v5))))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        _f4.Codec.EncodeTo(ops, _f4.Getter(value), builder);
        _f5.Codec.EncodeTo(ops, _f5.Getter(value), builder);
        return builder;
    }
}

//6字段record codec
internal sealed class RecordCodec6<T, F1, F2, F3, F4, F5, F6> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly FieldCodec<T, F4> _f4;
    private readonly FieldCodec<T, F5> _f5;
    private readonly FieldCodec<T, F6> _f6;
    private readonly Func<F1, F2, F3, F4, F5, F6, T> _ctor;

    public RecordCodec6(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        FieldCodec<T, F4> f4, FieldCodec<T, F5> f5, FieldCodec<T, F6> f6,
        Func<F1, F2, F3, F4, F5, F6, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4; _f5 = f5; _f6 = f6; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input)
                    .FlatMap(v3 => _f4.Codec.Decode(ops, input)
                        .FlatMap(v4 => _f5.Codec.Decode(ops, input)
                            .FlatMap(v5 => _f6.Codec.Decode(ops, input).Map(v6 => _ctor(v1, v2, v3, v4, v5, v6)))))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        _f4.Codec.EncodeTo(ops, _f4.Getter(value), builder);
        _f5.Codec.EncodeTo(ops, _f5.Getter(value), builder);
        _f6.Codec.EncodeTo(ops, _f6.Getter(value), builder);
        return builder;
    }
}

//7字段record codec
internal sealed class RecordCodec7<T, F1, F2, F3, F4, F5, F6, F7> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly FieldCodec<T, F4> _f4;
    private readonly FieldCodec<T, F5> _f5;
    private readonly FieldCodec<T, F6> _f6;
    private readonly FieldCodec<T, F7> _f7;
    private readonly Func<F1, F2, F3, F4, F5, F6, F7, T> _ctor;

    public RecordCodec7(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        FieldCodec<T, F4> f4, FieldCodec<T, F5> f5, FieldCodec<T, F6> f6, FieldCodec<T, F7> f7,
        Func<F1, F2, F3, F4, F5, F6, F7, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4; _f5 = f5; _f6 = f6; _f7 = f7; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input)
                    .FlatMap(v3 => _f4.Codec.Decode(ops, input)
                        .FlatMap(v4 => _f5.Codec.Decode(ops, input)
                            .FlatMap(v5 => _f6.Codec.Decode(ops, input)
                                .FlatMap(v6 => _f7.Codec.Decode(ops, input).Map(v7 => _ctor(v1, v2, v3, v4, v5, v6, v7))))))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        _f4.Codec.EncodeTo(ops, _f4.Getter(value), builder);
        _f5.Codec.EncodeTo(ops, _f5.Getter(value), builder);
        _f6.Codec.EncodeTo(ops, _f6.Getter(value), builder);
        _f7.Codec.EncodeTo(ops, _f7.Getter(value), builder);
        return builder;
    }
}

//8字段record codec
internal sealed class RecordCodec8<T, F1, F2, F3, F4, F5, F6, F7, F8> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F1> _f1;
    private readonly FieldCodec<T, F2> _f2;
    private readonly FieldCodec<T, F3> _f3;
    private readonly FieldCodec<T, F4> _f4;
    private readonly FieldCodec<T, F5> _f5;
    private readonly FieldCodec<T, F6> _f6;
    private readonly FieldCodec<T, F7> _f7;
    private readonly FieldCodec<T, F8> _f8;
    private readonly Func<F1, F2, F3, F4, F5, F6, F7, F8, T> _ctor;

    public RecordCodec8(FieldCodec<T, F1> f1, FieldCodec<T, F2> f2, FieldCodec<T, F3> f3,
        FieldCodec<T, F4> f4, FieldCodec<T, F5> f5, FieldCodec<T, F6> f6, FieldCodec<T, F7> f7,
        FieldCodec<T, F8> f8, Func<F1, F2, F3, F4, F5, F6, F7, F8, T> ctor)
    {
        _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4; _f5 = f5; _f6 = f6; _f7 = f7; _f8 = f8; _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        return _f1.Codec.Decode(ops, input)
            .FlatMap(v1 => _f2.Codec.Decode(ops, input)
                .FlatMap(v2 => _f3.Codec.Decode(ops, input)
                    .FlatMap(v3 => _f4.Codec.Decode(ops, input)
                        .FlatMap(v4 => _f5.Codec.Decode(ops, input)
                            .FlatMap(v5 => _f6.Codec.Decode(ops, input)
                                .FlatMap(v6 => _f7.Codec.Decode(ops, input)
                                    .FlatMap(v7 => _f8.Codec.Decode(ops, input).Map(v8 => _ctor(v1, v2, v3, v4, v5, v6, v7, v8)))))))));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _f1.Codec.EncodeTo(ops, _f1.Getter(value), builder);
        _f2.Codec.EncodeTo(ops, _f2.Getter(value), builder);
        _f3.Codec.EncodeTo(ops, _f3.Getter(value), builder);
        _f4.Codec.EncodeTo(ops, _f4.Getter(value), builder);
        _f5.Codec.EncodeTo(ops, _f5.Getter(value), builder);
        _f6.Codec.EncodeTo(ops, _f6.Getter(value), builder);
        _f7.Codec.EncodeTo(ops, _f7.Getter(value), builder);
        _f8.Codec.EncodeTo(ops, _f8.Getter(value), builder);
        return builder;
    }
}
