using NetCraft.Codec;

namespace NetCraft.Storage.Paletted;

//PalettedContainer对应原版net.minecraft.world.level.chunk.PalettedContainer
//用palette与bitstorage紧凑存储重复值多的数据如方块状态与生物群系
public sealed class PalettedContainer<T> : PaletteResize<T>
{
    private volatile Data _data;
    private readonly Strategy<T> _strategy;

    public PalettedContainer(T initialValue, Strategy<T> strategy)
    {
        _strategy = strategy;
        _data = CreateOrReuseData(null, 0);
        _data.Palette.IdFor(initialValue, this);
    }

    private PalettedContainer(Strategy<T> strategy, Configuration configuration, BitStorage storage, Palette<T> palette)
    {
        _strategy = strategy;
        _data = new Data(configuration, storage, palette);
    }

    private PalettedContainer(PalettedContainer<T> source)
    {
        _strategy = source._strategy;
        _data = source._data.Copy();
    }

    //对应原版onResize palette空间不足时创建新Data并迁移数据
    public int OnResize(int bits, T lastAddedValue)
    {
        var oldData = _data;
        var newData = CreateOrReuseData(oldData, bits);
        newData.CopyFrom(oldData.Palette, oldData.Storage);
        _data = newData;
        return newData.Palette.IdFor(lastAddedValue, PaletteResize<T>.NoResizeExpected());
    }

    //读写访问对应原版acquire/release当前空实现
    public void Acquire() { }
    public void Release() { }

    public T GetAndSet(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            var result = GetAndSet(_strategy.GetIndex(x, y, z), value);
            Release();
            return result;
        }
        catch
        {
            Release();
            throw;
        }
    }

    public T GetAndSetUnchecked(int x, int y, int z, T value)
        => GetAndSet(_strategy.GetIndex(x, y, z), value);

    private T GetAndSet(int index, T value)
    {
        var id = _data.Palette.IdFor(value, this);
        var oldId = _data.Storage.GetAndSet(index, id);
        return _data.Palette.ValueFor(oldId);
    }

    public void Set(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            Set(_strategy.GetIndex(x, y, z), value);
            Release();
        }
        catch
        {
            Release();
            throw;
        }
    }

    private void Set(int index, T value)
    {
        var id = _data.Palette.IdFor(value, this);
        _data.Storage.Set(index, id);
    }

    public T Get(int x, int y, int z) => Get(_strategy.GetIndex(x, y, z));

    private T Get(int index)
    {
        var data = _data;
        return data.Palette.ValueFor(data.Storage.Get(index));
    }

    public void GetAll(Action<T> consumer)
    {
        var data = _data;
        var palette = data.Palette;
        var visited = new HashSet<int>();
        data.Storage.GetAll(id =>
        {
            if (visited.Add(id)) consumer(palette.ValueFor(id));
        });
    }

    public bool MaybeHas(Predicate<T> predicate) => _data.Palette.MaybeHas(predicate);

    public void ForEachInPalette(Action<T> consumer)
    {
        var data = _data;
        for (var i = 0; i < data.Palette.Size; i++)
            consumer(data.Palette.ValueFor(i));
    }

    public int BitsPerEntry => _data.Storage.Bits;

    public PalettedContainer<T> Copy() => new(this);

    //重建为只含默认值的容器对应原版recreate
    public PalettedContainer<T> Recreate()
        => new(_data.Palette.ValueFor(0), _strategy);

    //统计每个值出现次数对应原版count
    public void Count(Action<T, int> output)
    {
        var data = _data;
        if (data.Palette.Size == 1)
        {
            output(data.Palette.ValueFor(0), data.Storage.Size);
            return;
        }
        var counts = new Dictionary<int, int>();
        data.Storage.GetAll(id =>
        {
            counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
        });
        foreach (var (id, count) in counts)
            output(data.Palette.ValueFor(id), count);
    }

    //序列化打包对应原版pack用HashMapPalette重排id到紧凑序列
    public PackedData<T> Pack(Strategy<T> strategy)
    {
        Acquire();
        try
        {
            var currentStorage = _data.Storage;
            var currentPalette = _data.Palette;
            var newPalette = new HashMapPalette<T>(currentStorage.Bits);
            var entryCount = strategy.EntryCount;
            var newContents = ReencodeContents(currentStorage, currentPalette, newPalette);
            var storedConfiguration = strategy.GetConfigurationForPaletteSize(newPalette.Size);
            var bitsOnDisc = storedConfiguration.BitsInStorage;
            Optional<long[]> values;
            if (bitsOnDisc != 0)
            {
                var storage = new SimpleBitStorage(bitsOnDisc, entryCount, newContents);
                values = Optional<long[]>.Of(storage.GetRaw());
            }
            else
            {
                values = Optional<long[]>.Empty();
            }
            Release();
            return new PackedData<T>(newPalette.GetEntries(), values, bitsOnDisc);
        }
        catch
        {
            Release();
            throw;
        }
    }

    //GetPackedData 用自身 strategy 打包对外暴露用于网络序列化
    public PackedData<T> GetPackedData() => Pack(_strategy);

    private Data CreateOrReuseData(Data? oldData, int targetBits)
    {
        var configuration = _strategy.GetConfigurationForBitCount(targetBits);
        if (oldData is not null && configuration.Equals(oldData.Configuration)) return oldData;
        BitStorage? storage = configuration.BitsInMemory == 0
            ? new ZeroBitStorage(_strategy.EntryCount)
            : new SimpleBitStorage(configuration.BitsInMemory, _strategy.EntryCount);
        var palette = configuration.CreatePalette(_strategy, Array.Empty<T>());
        return new Data(configuration, storage, palette);
    }

    //把旧palette下的storage重新编码到新palette对应原版reencodeContents
    private static int[] ReencodeContents(BitStorage storage, Palette<T> oldPalette, Palette<T> newPalette)
    {
        var buffer = new int[storage.Size];
        storage.Unpack(buffer);
        var dummyResizer = PaletteResize<T>.NoResizeExpected();
        var lastReadId = -1;
        var lastWrittenId = -1;
        for (var index = 0; index < buffer.Length; index++)
        {
            var id = buffer[index];
            if (id != lastReadId)
            {
                lastReadId = id;
                lastWrittenId = newPalette.IdFor(oldPalette.ValueFor(id), dummyResizer);
            }
            buffer[index] = lastWrittenId;
        }
        return buffer;
    }

    //从PackedData反序列化为PalettedContainer对应原版unpack
    public static DataResult<PalettedContainer<T>> Unpack(Strategy<T> strategy, PackedData<T> discData)
    {
        var paletteEntries = discData.PaletteEntries;
        var entryCount = strategy.EntryCount;
        var storedConfiguration = strategy.GetConfigurationForPaletteSize(paletteEntries.Count);
        var bitsOnDisc = storedConfiguration.BitsInStorage;
        if (discData.BitsPerEntry != -1 && bitsOnDisc != discData.BitsPerEntry)
        {
            return DataResult<PalettedContainer<T>>.Error(
                () => $"Invalid bit count, calculated {bitsOnDisc}, but container declared {discData.BitsPerEntry}");
        }

        Palette<T> palette;
        BitStorage storage;
        if (storedConfiguration.BitsInMemory == 0)
        {
            palette = storedConfiguration.CreatePalette(strategy, paletteEntries);
            storage = new ZeroBitStorage(entryCount);
        }
        else
        {
            if (!discData.Storage.IsPresent)
            {
                return DataResult<PalettedContainer<T>>.Error(() => "Missing values for non-zero storage");
            }
            var data = discData.Storage.Get();
            try
            {
                if (storedConfiguration.AlwaysRepack || storedConfiguration.BitsInMemory != bitsOnDisc)
                {
                    var oldPalette = new HashMapPalette<T>(bitsOnDisc, paletteEntries);
                    var oldStorage = new SimpleBitStorage(bitsOnDisc, entryCount, data);
                    var newPalette = storedConfiguration.CreatePalette(strategy, paletteEntries);
                    var newContents = ReencodeContents(oldStorage, oldPalette, newPalette);
                    palette = newPalette;
                    storage = new SimpleBitStorage(storedConfiguration.BitsInMemory, entryCount, newContents);
                }
                else
                {
                    palette = storedConfiguration.CreatePalette(strategy, paletteEntries);
                    storage = new SimpleBitStorage(storedConfiguration.BitsInMemory, entryCount, data);
                }
            }
            catch (Exception exception)
            {
                return DataResult<PalettedContainer<T>>.Error(() => $"Failed to read PalettedContainer: {exception.Message}");
            }
        }
        return DataResult<PalettedContainer<T>>.Success(new PalettedContainer<T>(strategy, storedConfiguration, storage, palette));
    }

    //创建PalettedContainer的codec对应原版PalettedContainer.codecRW
    //用RecordCodecBuilder序列化PackedData再comapFlatMap转换
    public static Codec<PalettedContainer<T>> CreateCodec(Codec<T> elementCodec, Strategy<T> strategy, T defaultValue)
    {
        var packedCodec = RecordCodecBuilder.Of2<PackedData<T>, IReadOnlyList<T>, Optional<long[]>>(
            elementCodec.MapResult(defaultValue).ListOf().FieldOf("palette")
                .ForGetter((PackedData<T> d) => d.PaletteEntries),
            CodecExtras.LongArray.LenientOptionalFieldOf("data")
                .ForGetter((PackedData<T> d) => d.Storage),
            (palette, storage) => new PackedData<T>(palette, storage));
        return packedCodec.ComapFlatMap(
            discData => Unpack(strategy, discData),
            container => container.Pack(strategy));
    }

    //内部数据持有configuration storage palette对应原版Data
    private sealed class Data
    {
        public Configuration Configuration { get; }
        public BitStorage Storage { get; }
        public Palette<T> Palette { get; }

        public Data(Configuration configuration, BitStorage storage, Palette<T> palette)
        {
            Configuration = configuration;
            Storage = storage;
            Palette = palette;
        }

        public void CopyFrom(Palette<T> oldPalette, BitStorage oldStorage)
        {
            var dummyResizer = PaletteResize<T>.NoResizeExpected();
            for (var i = 0; i < oldStorage.Size; i++)
            {
                var value = oldPalette.ValueFor(oldStorage.Get(i));
                Storage.Set(i, Palette.IdFor(value, dummyResizer));
            }
        }

        public Data Copy() => new(Configuration, Storage.Copy(), Palette.Copy());
    }
}

//PackedData对应原版PalettedContainerRO.PackedData
//NBT序列化中间表示paletteEntries值列表storage压缩long数组bitsPerEntry位宽
public sealed record PackedData<T>(IReadOnlyList<T> PaletteEntries, Optional<long[]> Storage, int BitsPerEntry)
{
    public const int UnknownBitsPerEntry = -1;

    public PackedData(IReadOnlyList<T> paletteEntries, Optional<long[]> storage) : this(paletteEntries, storage, UnknownBitsPerEntry) { }
}
