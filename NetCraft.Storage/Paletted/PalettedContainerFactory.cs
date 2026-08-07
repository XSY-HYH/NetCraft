using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Paletted;

//PalettedContainerFactory抽象类对应原版net.minecraft.world.level.chunk.PalettedContainerFactory
//提供方块状态与生物群系容器的创建与codec
public abstract class PalettedContainerFactory
{
    public abstract PalettedContainer<BlockState> CreateForBlockStates();
    public abstract PalettedContainer<Holder<Biome>> CreateForBiomes();
    public abstract Codec<PalettedContainer<BlockState>> BlockStatesContainerCodec();
    public abstract Codec<PalettedContainer<Holder<Biome>>> BiomeContainerCodec();

    //从PackedData反序列化为PalettedContainer对应原版read用网络序列化路径
    //LevelChunkSerializer.Read 用此重建 section.States 与 section.Biomes
    public abstract PalettedContainer<BlockState> UnpackBlockStates(PackedData<BlockState> discData);
    public abstract PalettedContainer<Holder<Biome>> UnpackBiomes(PackedData<Holder<Biome>> discData);

    //按Identifier查BlockState用于网络反序列化时palette entries重建
    //LevelChunkSerializer.ReadBlockState 优先调此方法找不到再回退BuiltInRegistries.BLOCK
    public abstract BlockState? LookupBlockState(Identifier id);
    public abstract Holder<Biome>? LookupBiome(Identifier id);

    public static readonly PalettedContainerFactory Default = new DefaultPalettedContainerFactory();
}

//默认工厂实现用IdMapper作GlobalMap注册表按Identifier查Block与Holder<Biome>
public sealed class DefaultPalettedContainerFactory : PalettedContainerFactory
{
    //GlobalMap按需注册测试时不进入GlobalPalette也能跑
    private readonly IdMapper<BlockState> _blockStatesGlobalMap = new();
    private readonly IdMapper<Holder<Biome>> _biomesGlobalMap = new();
    private readonly Strategy<BlockState> _blockStatesStrategy;
    private readonly Strategy<Holder<Biome>> _biomesStrategy;
    private readonly Codec<BlockState> _blockStateCodec;
    private readonly Codec<Holder<Biome>> _biomeCodec;

    //按Identifier查Block与Holder<Biome>用于codec反序列化
    private readonly Dictionary<Identifier, Block> _blocksById = new();
    private readonly Dictionary<Identifier, Holder<Biome>> _biomesById = new();

    //默认值由最近一次RegisterBlock/RegisterBiome设置用作空容器初始值
    private BlockState? _defaultBlockState;
    private Holder<Biome>? _defaultBiome;

    public DefaultPalettedContainerFactory()
    {
        _blockStatesStrategy = Strategy<BlockState>.CreateForBlockStates(_blockStatesGlobalMap);
        _biomesStrategy = Strategy<Holder<Biome>>.CreateForBiomes(_biomesGlobalMap);

        //BlockState codec encode用Owner.Id decode按Identifier查Block
        _blockStateCodec = IdentifierCodec.Instance.ComapFlatMap(
            id => _blocksById.TryGetValue(id, out var block)
                ? DataResult<BlockState>.Success(block.DefaultBlockState)
                : DataResult<BlockState>.Error(() => $"Unknown block: {id}"),
            state => state.Owner.Id);

        //Biome codec encode用Value.Id decode按Identifier查Holder<Biome>
        _biomeCodec = IdentifierCodec.Instance.ComapFlatMap(
            id => _biomesById.TryGetValue(id, out var holder)
                ? DataResult<Holder<Biome>>.Success(holder)
                : DataResult<Holder<Biome>>.Error(() => $"Unknown biome: {id}"),
            holder => holder.Value.Id);
    }

    //注册Block按Identifier建立codec反查表同时加入GlobalMap与默认值缓存
    //默认值仅在首次注册时设置后续注册不覆盖对齐原版 AIR 作为默认方块
    public void RegisterBlock(Block block)
    {
        _blocksById[block.Id] = block;
        _blockStatesGlobalMap.Add(block.DefaultBlockState);
        _defaultBlockState ??= block.DefaultBlockState;
    }

    //注册Biome按Identifier建立codec反查表同时加入GlobalMap与默认值缓存
    //默认值仅在首次注册时设置后续注册不覆盖
    public void RegisterBiome(Holder<Biome> holder)
    {
        _biomesById[holder.Value.Id] = holder;
        _biomesGlobalMap.Add(holder);
        _defaultBiome ??= holder;
    }

    public override PalettedContainer<BlockState> CreateForBlockStates()
        => new(_defaultBlockState ?? default, _blockStatesStrategy);

    public override PalettedContainer<Holder<Biome>> CreateForBiomes()
        => new(_defaultBiome ?? default!, _biomesStrategy);

    public override Codec<PalettedContainer<BlockState>> BlockStatesContainerCodec()
        => PalettedContainer<BlockState>.CreateCodec(_blockStateCodec, _blockStatesStrategy, _defaultBlockState.Value);

    public override Codec<PalettedContainer<Holder<Biome>>> BiomeContainerCodec()
        => PalettedContainer<Holder<Biome>>.CreateCodec(_biomeCodec, _biomesStrategy, _defaultBiome!);

    //UnpackBlockStates 用 strategy 反序列化 PackedData 为容器
    //失败时回退到空容器避免抛异常中断网络读取
    public override PalettedContainer<BlockState> UnpackBlockStates(PackedData<BlockState> discData)
        => PalettedContainer<BlockState>.Unpack(_blockStatesStrategy, discData).Result()
            .OrElse(CreateForBlockStates());

    public override PalettedContainer<Holder<Biome>> UnpackBiomes(PackedData<Holder<Biome>> discData)
        => PalettedContainer<Holder<Biome>>.Unpack(_biomesStrategy, discData).Result()
            .OrElse(CreateForBiomes());

    //LookupBlockState 按 Identifier 查 _blocksById 返回对应 DefaultBlockState
    //找不到返回 null 由调用方回退 BuiltInRegistries.BLOCK
    public override BlockState? LookupBlockState(Identifier id)
        => _blocksById.TryGetValue(id, out var block) ? block.DefaultBlockState : null;

    //LookupBiome 按 Identifier 查 _biomesById 返回对应 Holder
    //找不到返回 null 由调用方回退 PlainsBiome 占位
    public override Holder<Biome>? LookupBiome(Identifier id)
        => _biomesById.TryGetValue(id, out var holder) ? holder : null;
}
