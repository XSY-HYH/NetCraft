using System.Collections.Generic;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Test.Modules;

//ChunkAccess 抽象基类测试
//覆盖 GetOrCreateHeightmapForType/GetHeight/区段范围推导/GetSection 命中与越界
internal static class ChunkAccessTests
{
    public const string Module = "chunkaccess";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("ChunkAccess MinSectionY/SectionsCount 推导 MaxSectionY", TestMaxSectionY);
        yield return ("ChunkAccess GetOrCreateHeightmapForType 空数据创建新实例", TestGetOrCreateEmpty);
        yield return ("ChunkAccess GetOrCreateHeightmapForType 有数据 FromData", TestGetOrCreateFromData);
        yield return ("ChunkAccess GetHeight 取列高度", TestGetHeight);
        yield return ("ChunkAccess GetSection 越界返回 null", TestGetSectionOutOfRange);
        yield return ("ChunkAccess GetSection 命中返回区段", TestGetSectionHit);
        yield return ("ChunkAccess Heightmaps 字典为空时 GetHeight 返回 -1", TestGetHeightEmptyReturnsMinValue);
        yield return ("ChunkAccess 多类型 Heightmap 独立存储", TestMultipleHeightmapTypes);
        yield return ("ChunkAccess MinBuildHeight/MaxBuildHeight 推导", TestBuildHeightRange);
    }

    //TestChunkAccess 测试用 ChunkAccess 具体子类
    private sealed class TestChunkAccess : ChunkAccess
    {
        private readonly ChunkPos _pos;
        private readonly Dictionary<HeightmapRegistry.Types, long[]> _heightmaps;
        private readonly LevelChunkSection? _section;

        public override ChunkPos Pos => _pos;
        public override int MinSectionY { get; }
        public override int SectionsCount { get; }
        public override ChunkStatus ChunkStatus { get; }
        public override IDictionary<HeightmapRegistry.Types, long[]> Heightmaps => _heightmaps;
        //LevelHeightAccessor 接口默认方法在抽象类中需显式重写否则子类访问不可见
        public int MinBuildHeight => MinSectionY * 16;
        public int MaxBuildHeight => (MaxSectionY + 1) * 16;

        public TestChunkAccess(ChunkPos pos, int minSectionY, int sectionsCount, LevelChunkSection? section = null)
        {
            _pos = pos;
            MinSectionY = minSectionY;
            SectionsCount = sectionsCount;
            _heightmaps = new Dictionary<HeightmapRegistry.Types, long[]>();
            _section = section;
            ChunkStatus = ChunkStatus.EMPTY;
        }

        public override LevelChunkSection? GetSection(int sectionY)
            => sectionY == MinSectionY ? _section : null;
    }

    //MockBlock 测试用 Block 子类注册空属性 BlockState 到 BlockStateRegistry
    private sealed class MockBlock : Block
    {
        public override Identifier Id { get; }
        public override BlockState DefaultBlockState { get; }

        public MockBlock(Identifier id)
        {
            Id = id;
            var state = BlockStateRegistry.Register(this, Array.Empty<PropertyBase>(), Array.Empty<object?>());
            BlockStateRegistry.InitializeNeighbors(state.Id, Array.Empty<int[]>());
            DefaultBlockState = state;
        }
    }

    //MockBiome 测试用 Biome 子类仅持有 Identifier
    private sealed class MockBiome : Biome
    {
        public override Identifier Id { get; }
        public MockBiome(Identifier id) => Id = id;
    }

    //NewFactory 注册 mock block 与 biome 返回可用工厂
    private static DefaultPalettedContainerFactory NewFactory()
    {
        var factory = new DefaultPalettedContainerFactory();
        factory.RegisterBlock(new MockBlock(Identifier.WithDefaultNamespace("test_block")));
        factory.RegisterBiome(Holder<Biome>.Direct(new MockBiome(Identifier.WithDefaultNamespace("test_biome"))));
        return factory;
    }

    private static bool TestMaxSectionY()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        return chunk.MinSectionY == -4
            && chunk.SectionsCount == 24
            && chunk.MaxSectionY == 19;
    }

    private static bool TestGetOrCreateEmpty()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        var map = chunk.GetOrCreateHeightmapForType(HeightmapRegistry.Types.WorldSurface);
        return map is not null && map.Type == HeightmapRegistry.Types.WorldSurface;
    }

    private static bool TestGetOrCreateFromData()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        var protoMap = new Storage.LevelGen.Heightmap(HeightmapRegistry.Types.WorldSurface, -64, 384);
        protoMap.Update(0, 100, 0);
        chunk.Heightmaps[HeightmapRegistry.Types.WorldSurface] = protoMap.GetData();

        var restored = chunk.GetOrCreateHeightmapForType(HeightmapRegistry.Types.WorldSurface);
        return restored.GetFirstAvailable(0, 0) == 100;
    }

    private static bool TestGetHeight()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        var map = chunk.GetOrCreateHeightmapForType(HeightmapRegistry.Types.OceanFloor);
        map.Update(5, 50, 5);
        return chunk.GetHeight(HeightmapRegistry.Types.OceanFloor, 5, 5) == 50;
    }

    private static bool TestGetSectionOutOfRange()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        return chunk.GetSection(100) is null && chunk.GetSection(-100) is null;
    }

    private static bool TestGetSectionHit()
    {
        var factory = NewFactory();
        var section = new LevelChunkSection(factory.CreateForBlockStates(), factory.CreateForBiomes());
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24, section);
        //命中 MinSectionY 返回注入的 section 越界返回 null
        return ReferenceEquals(chunk.GetSection(-4), section) && chunk.GetSection(0) is null;
    }

    private static bool TestGetHeightEmptyReturnsMinValue()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        return chunk.GetHeight(HeightmapRegistry.Types.WorldSurface, 0, 0) == Storage.LevelGen.Heightmap.MinValue;
    }

    private static bool TestMultipleHeightmapTypes()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        var ws = chunk.GetOrCreateHeightmapForType(HeightmapRegistry.Types.WorldSurface);
        var of = chunk.GetOrCreateHeightmapForType(HeightmapRegistry.Types.OceanFloor);
        ws.Update(0, 100, 0);
        of.Update(0, 50, 0);
        //两类型独立存储互不影响
        return ws.GetFirstAvailable(0, 0) == 100
            && of.GetFirstAvailable(0, 0) == 50
            && chunk.GetHeight(HeightmapRegistry.Types.WorldSurface, 0, 0) == 100
            && chunk.GetHeight(HeightmapRegistry.Types.OceanFloor, 0, 0) == 50;
    }

    private static bool TestBuildHeightRange()
    {
        var chunk = new TestChunkAccess(new ChunkPos(0, 0), -4, 24);
        return chunk.MinBuildHeight == -64 && chunk.MaxBuildHeight == 320;
    }
}
