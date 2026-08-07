using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Paletted;

namespace NetCraft.Storage.Chunk;

//区块区段对应原版net.minecraft.world.level.chunk.LevelChunkSection
//持有states方块状态palette与biomes生物群系palette
//air 判定基于构造时记录的初始方块状态避免依赖 default(BlockState) 占位语义
public sealed class LevelChunkSection
{
    public const int BiomeContainerBits = 2;

    private short _nonEmptyBlockCount;
    private short _fluidCount;
    private short _tickingBlockCount;
    private short _tickingFluidCount;
    private readonly BlockState _airState;

    public PalettedContainer<BlockState> States { get; }
    public PalettedContainer<Holder<Biome>> Biomes { get; private set; }

    public LevelChunkSection(PalettedContainer<BlockState> states, PalettedContainer<Holder<Biome>> biomes)
    {
        States = states;
        Biomes = biomes;
        _airState = states.Get(0, 0, 0);
    }

    public LevelChunkSection(Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory)
    {
        States = statesFactory();
        Biomes = biomesFactory();
        _airState = States.Get(0, 0, 0);
    }

    public BlockState GetBlockState(int sectionX, int sectionY, int sectionZ)
        => States.Get(sectionX, sectionY, sectionZ);

    //SetBlockState 写入方块并维护非空计数对应原版 setBlockState
    //air 判定走构造时记录的 _airState 写入非 air 时计数加 1 反之减 1
    public BlockState SetBlockState(int sectionX, int sectionY, int sectionZ, BlockState state)
    {
        var old = States.GetAndSet(sectionX, sectionY, sectionZ, state);
        var wasAir = old.Equals(_airState);
        var isAir = state.Equals(_airState);
        if (wasAir && !isAir) _nonEmptyBlockCount++;
        else if (!wasAir && isAir) _nonEmptyBlockCount = (short)Math.Max(0, _nonEmptyBlockCount - 1);
        return old;
    }

    public bool HasOnlyAir() => _nonEmptyBlockCount == 0;

    public bool HasFluid() => _fluidCount > 0;

    public bool IsRandomlyTicking() => _tickingBlockCount > 0 || _tickingFluidCount > 0;

    public PalettedContainer<Holder<Biome>> GetBiomes() => Biomes;

    public LevelChunkSection Copy()
        => new(States.Copy(), (PalettedContainer<Holder<Biome>>)Biomes.Copy());

    public bool MaybeHas(Predicate<BlockState> predicate) => States.MaybeHas(predicate);

    public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        => Biomes.Get(quartX, quartY, quartZ);

    //SetBiome 按区段内 quart 坐标写入生物群系对应原版 setBiome
    //quart 坐标范围 0-3 每 4 个方块共享一个 biome
    public void SetBiome(int quartX, int quartY, int quartZ, Holder<Biome> biome)
        => Biomes.Set(quartX, quartY, quartZ, biome);
}
