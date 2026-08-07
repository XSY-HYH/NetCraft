using System.IO;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Test.Modules;

//存档IO子系统测试
//覆盖RegionBitmap与ChunkPos与RegionFileVersion与RegionFile与RegionFileStorage的round-trip
//以及SerializableChunkData的完整round-trip
internal static class StorageTests
{
    public const string Module = "storage";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("RegionBitmap Force/Free/Allocate", TestRegionBitmap);
        yield return ("ChunkPos Pack/Unpack", TestChunkPosPack);
        yield return ("ChunkPos GetRegionXZ/Local", TestChunkPosRegion);
        yield return ("ChunkPos MinFromRegion/MaxFromRegion", TestChunkPosRegionRange);
        yield return ("RegionFileVersion FromId/IsValidVersion", TestRegionFileVersion);
        yield return ("RegionFile round-trip deflate", () => TestRegionFileRoundTrip(RegionFileVersion.VersionDeflate));
        yield return ("RegionFile round-trip gzip", () => TestRegionFileRoundTrip(RegionFileVersion.VersionGzip));
        yield return ("RegionFile round-trip none", () => TestRegionFileRoundTrip(RegionFileVersion.VersionNone));
        yield return ("RegionFile round-trip lz4", () => TestRegionFileRoundTrip(RegionFileVersion.VersionLz4));
        yield return ("RegionFile Clear", TestRegionFileClear);
        yield return ("RegionFile DoesChunkExist", TestRegionFileDoesChunkExist);
        yield return ("RegionFileStorage round-trip", TestRegionFileStorageRoundTrip);
        yield return ("RegionFileStorage cross-region", TestRegionFileStorageCrossRegion);
        yield return ("SerializableChunkData round-trip FULL", TestSerializableChunkDataFullRoundTrip);
        yield return ("SerializableChunkData round-trip EMPTY", TestSerializableChunkDataEmptyRoundTrip);
        yield return ("SerializableChunkData parse empty returns null", TestSerializableChunkDataParseEmpty);
        yield return ("SerializableChunkData sections with light data", TestSerializableChunkDataLightData);
        yield return ("DirectoryLock acquire and release", TestDirectoryLockAcquireRelease);
        yield return ("LevelStorageAccess acquires lock by default", TestLevelStorageAccessAcquiresLock);
        yield return ("PersistentServerLevel save and load round-trip", TestPersistentServerLevelRoundTrip);
        yield return ("PersistentServerLevel load missing returns null", TestPersistentServerLevelLoadMissing);
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static RegionStorageInfo NewInfo() => new("test", null!, "chunk");

    private static CompoundTag MakeChunk(int level, string name)
    {
        var tag = new CompoundTag();
        tag.Put("Level", new IntTag(level));
        tag.Put("Name", new StringTag(name));
        return tag;
    }

    private static bool AssertTag(CompoundTag? tag, int level, string name)
        => tag != null
            && tag.GetInt("Level")?.Value == level
            && tag.GetString("Name")?.Value == name;

    private static bool TestRegionBitmap()
    {
        var bmp = new RegionBitmap();
        bmp.Force(2, 3);
        if (bmp.Allocate(1) != 0) return false;
        if (bmp.Allocate(1) != 1) return false;
        if (bmp.Allocate(1) != 5) return false;
        bmp.Free(2, 1);
        if (bmp.Allocate(1) != 2) return false;
        return true;
    }

    private static bool TestChunkPosPack()
    {
        var pos = new ChunkPos(10, -20);
        long key = ChunkPos.Pack(10, -20);
        if (pos.Pack() != key) return false;
        var up = ChunkPos.Unpack(key);
        return up.X == 10 && up.Z == -20;
    }

    private static bool TestChunkPosRegion()
    {
        var pos = new ChunkPos(100, -64);
        if (pos.GetRegionX() != 3) return false;
        if (pos.GetRegionZ() != -2) return false;
        if (pos.GetRegionLocalX() != 4) return false;
        if (pos.GetRegionLocalZ() != 0) return false;
        return true;
    }

    private static bool TestChunkPosRegionRange()
    {
        var min = ChunkPos.MinFromRegion(1, 2);
        var max = ChunkPos.MaxFromRegion(1, 2);
        return min.X == 32 && min.Z == 64
            && max.X == 32 + 31 && max.Z == 64 + 31;
    }

    private static bool TestRegionFileVersion()
    {
        return RegionFileVersion.FromId(1) == RegionFileVersion.VersionGzip
            && RegionFileVersion.FromId(2) == RegionFileVersion.VersionDeflate
            && RegionFileVersion.FromId(3) == RegionFileVersion.VersionNone
            && RegionFileVersion.FromId(4) == RegionFileVersion.VersionLz4
            && RegionFileVersion.FromId(127) == RegionFileVersion.VersionCustom
            && RegionFileVersion.FromId(999) == null
            && RegionFileVersion.IsValidVersion(1)
            && !RegionFileVersion.IsValidVersion(99)
            && RegionFileVersion.GetSelected() == RegionFileVersion.VersionDeflate;
    }

    private static bool TestRegionFileRoundTrip(RegionFileVersion version)
    {
        var dir = NewTempDir();
        try
        {
            var info = NewInfo();
            var pos = new ChunkPos(5, 5);
            var path = Path.Combine(dir, "r.0.0.mca");
            var expected = MakeChunk(42, "hello");
            using (var region = new RegionFile(info, path, dir, version, false))
            {
                using var writer = region.GetChunkDataOutputStream(pos);
                NbtIo.Write(expected, new BinaryNbtWriter(writer));
            }
            using (var region = new RegionFile(info, path, dir, version, false))
            using (var reader = region.GetChunkDataInputStream(pos))
            {
                if (reader == null) return false;
                var loaded = NbtIo.Read(new BinaryNbtReader(reader), NbtAccounter.UnlimitedHeap());
                return AssertTag(loaded, 42, "hello");
            }
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestRegionFileClear()
    {
        var dir = NewTempDir();
        try
        {
            var info = NewInfo();
            var pos = new ChunkPos(1, 1);
            var path = Path.Combine(dir, "r.0.0.mca");
            using (var region = new RegionFile(info, path, dir, false))
            {
                using var w = region.GetChunkDataOutputStream(pos);
                NbtIo.Write(MakeChunk(1, "a"), new BinaryNbtWriter(w));
            }
            using (var region = new RegionFile(info, path, dir, false))
            {
                if (!region.HasChunk(pos)) return false;
                region.Clear(pos);
                return !region.HasChunk(pos);
            }
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestRegionFileDoesChunkExist()
    {
        var dir = NewTempDir();
        try
        {
            var info = NewInfo();
            var pos = new ChunkPos(2, 2);
            var path = Path.Combine(dir, "r.0.0.mca");
            using (var region = new RegionFile(info, path, dir, false))
            {
                if (region.DoesChunkExist(pos)) return false;
                using (var w = region.GetChunkDataOutputStream(pos))
                {
                    NbtIo.Write(MakeChunk(7, "x"), new BinaryNbtWriter(w));
                }
                return region.DoesChunkExist(pos);
            }
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestRegionFileStorageRoundTrip()
    {
        var dir = NewTempDir();
        try
        {
            var info = NewInfo();
            using var storage = new RegionFileStorage(info, dir, false);
            var pos = new ChunkPos(3, 7);
            storage.Write(pos, MakeChunk(100, "storage"));
            return AssertTag(storage.Read(pos), 100, "storage");
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestRegionFileStorageCrossRegion()
    {
        var dir = NewTempDir();
        try
        {
            var info = NewInfo();
            using var storage = new RegionFileStorage(info, dir, false);
            var p1 = new ChunkPos(0, 0);
            var p2 = new ChunkPos(31, 31);
            var p3 = new ChunkPos(32, 32);
            storage.Write(p1, MakeChunk(1, "a"));
            storage.Write(p2, MakeChunk(2, "b"));
            storage.Write(p3, MakeChunk(3, "c"));
            return AssertTag(storage.Read(p1), 1, "a")
                && AssertTag(storage.Read(p2), 2, "b")
                && AssertTag(storage.Read(p3), 3, "c");
        }
        finally { TryCleanup(dir); }
    }

    private static void TryCleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { }
    }

    //SerializableChunkData测试用的Mock Block无属性持有单一默认BlockState
    //通过BlockStateRegistry注册空属性state对应struct BlockState 由registry持数据
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

    //SerializableChunkData测试用的Mock Biome持有Identifier
    private sealed class MockBiome : Biome
    {
        public override Identifier Id { get; }
        public MockBiome(Identifier id) => Id = id;
    }

    //构造注册了Block与Biome的默认工厂
    //先注册 air 作为默认方块使后续写入 test_block 触发非空计数
    private static DefaultPalettedContainerFactory NewFactory()
    {
        var factory = new DefaultPalettedContainerFactory();
        var airBlock = new MockBlock(Identifier.WithDefaultNamespace("air"));
        factory.RegisterBlock(airBlock);
        var block = new MockBlock(Identifier.WithDefaultNamespace("test_block"));
        factory.RegisterBlock(block);
        var biome = new MockBiome(Identifier.WithDefaultNamespace("test_biome"));
        factory.RegisterBiome(Holder<Biome>.Direct(biome));
        return factory;
    }

    //构造含section with block_states/biomes的SerializableChunkData
    private static SerializableChunkData NewSampleChunk(DefaultPalettedContainerFactory factory, ChunkStatus status)
    {
        var section = new LevelChunkSection(
            factory.CreateForBlockStates(),
            factory.CreateForBiomes());
        var heightmaps = new Dictionary<Heightmap.Types, long[]>
        {
            { Heightmap.Types.WorldSurface, new long[] { 1L, 2L, 3L } }
        };
        var blockEntities = new List<CompoundTag>
        {
            new() { ["id"] = new StringTag("chest") }
        };
        return new SerializableChunkData(
            factory,
            new ChunkPos(5, -3),
            minSectionY: -4,
            lastUpdateTime: 12345L,
            inhabitedTime: 67890L,
            chunkStatus: status,
            blendingData: null,
            belowZeroRetrogen: null,
            upgradeData: UpgradeData.Empty,
            carvingMask: status == ChunkStatus.EMPTY ? new long[] { 0xDEADBEEFL } : null,
            heightmaps: heightmaps,
            packedTicks: new PackedTicks(),
            postProcessingSections: Array.Empty<List<short>?>(),
            lightCorrect: true,
            sectionData: new List<SerializableChunkData.SectionData> { new(0, section, null, null) },
            entities: status == ChunkStatus.EMPTY
                ? new List<CompoundTag> { new() { ["id"] = new StringTag("cow") } }
                : new List<CompoundTag>(),
            blockEntities: blockEntities,
            structureData: new CompoundTag());
    }

    private static bool TestSerializableChunkDataFullRoundTrip()
    {
        var factory = NewFactory();
        var original = NewSampleChunk(factory, ChunkStatus.FULL);
        var tag = original.Write();
        var parsed = SerializableChunkData.Parse(new SimpleLevelHeightAccessor(-4, 24), factory, tag);
        if (parsed is null) return false;
        return parsed.ChunkPos.X == 5 && parsed.ChunkPos.Z == -3
            && parsed.MinSectionY == -4
            && parsed.LastUpdateTime == 12345L
            && parsed.InhabitedTime == 67890L
            && parsed.ChunkStatus == ChunkStatus.FULL
            && parsed.LightCorrect
            && parsed.SectionDataList.Count == 1
            && parsed.SectionDataList[0].Y == 0
            && parsed.SectionDataList[0].ChunkSection is not null
            && parsed.BlockEntities.Count == 1
            && parsed.BlockEntities[0].GetStringValue("id") == "chest"
            && parsed.Heightmaps.ContainsKey(Heightmap.Types.WorldSurface)
            && parsed.Heightmaps[Heightmap.Types.WorldSurface].Length == 3;
    }

    private static bool TestSerializableChunkDataEmptyRoundTrip()
    {
        var factory = NewFactory();
        var original = NewSampleChunk(factory, ChunkStatus.EMPTY);
        var tag = original.Write();
        if (!tag.Contains("entities")) return false;
        if (!tag.Contains("carving_mask")) return false;
        var parsed = SerializableChunkData.Parse(new SimpleLevelHeightAccessor(-4, 24), factory, tag);
        if (parsed is null) return false;
        return parsed.ChunkStatus == ChunkStatus.EMPTY
            && parsed.Entities.Count == 1
            && parsed.Entities[0].GetStringValue("id") == "cow"
            && parsed.CarvingMask is not null
            && parsed.CarvingMask[0] == 0xDEADBEEFL;
    }

    private static bool TestSerializableChunkDataParseEmpty()
    {
        var factory = NewFactory();
        var empty = new CompoundTag();
        var parsed = SerializableChunkData.Parse(new SimpleLevelHeightAccessor(-4, 24), factory, empty);
        return parsed is null;
    }

    private static bool TestSerializableChunkDataLightData()
    {
        var factory = NewFactory();
        var section = new LevelChunkSection(
            factory.CreateForBlockStates(),
            factory.CreateForBiomes());
        var blockLight = new DataLayer(new byte[DataLayer.Size]);
        blockLight.Set(0, 0, 0, 7);
        var skyLight = new DataLayer(new byte[DataLayer.Size]);
        skyLight.Set(1, 1, 1, 15);
        var chunk = new SerializableChunkData(
            factory,
            new ChunkPos(0, 0),
            minSectionY: 0,
            lastUpdateTime: 0L,
            inhabitedTime: 0L,
            chunkStatus: ChunkStatus.FULL,
            blendingData: null,
            belowZeroRetrogen: null,
            upgradeData: UpgradeData.Empty,
            carvingMask: null,
            heightmaps: new Dictionary<Heightmap.Types, long[]>(),
            packedTicks: new PackedTicks(),
            postProcessingSections: Array.Empty<List<short>?>(),
            lightCorrect: false,
            sectionData: new List<SerializableChunkData.SectionData>
            {
                new(0, section, blockLight, skyLight)
            },
            entities: new List<CompoundTag>(),
            blockEntities: new List<CompoundTag>(),
            structureData: new CompoundTag());
        var tag = chunk.Write();
        var parsed = SerializableChunkData.Parse(new SimpleLevelHeightAccessor(0, 24), factory, tag);
        if (parsed is null) return false;
        var sd = parsed.SectionDataList[0];
        return sd.BlockLight is not null
            && sd.BlockLight.Get(0, 0, 0) == 7
            && sd.SkyLight is not null
            && sd.SkyLight.Get(1, 1, 1) == 15;
    }

    //构造同步写入的 SimpleRegionStorage 用于 PersistentServerLevel 测试
    private static SimpleRegionStorage NewRegionStorage(string dir)
    {
        var info = new RegionStorageInfo("test", null!, "chunk");
        return new SimpleRegionStorage(info, dir, new NoOpDataFixer(), true, DataFixTypes.Chunk);
    }

    private static bool TestDirectoryLockAcquireRelease()
    {
        var dir = NewTempDir();
        try
        {
            using (var lock1 = DirectoryLock.Acquire(dir))
            {
                //同一目录二次 Acquire 应抛 IOException 表示世界已被占用
                try
                {
                    using var lock2 = DirectoryLock.Acquire(dir);
                    return false;
                }
                catch (IOException) { }
            }
            //释放后可以再次 Acquire
            using var lock3 = DirectoryLock.Acquire(dir);
            return true;
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestLevelStorageAccessAcquiresLock()
    {
        var baseDir = NewTempDir();
        try
        {
            var storage = new LevelStorage(baseDir);
            using (var access = storage.CreateAccess("world1"))
            {
                if (!access.HasLock) return false;
                //已锁状态下二次打开同一世界应抛 IOException
                try
                {
                    using var access2 = storage.CreateAccess("world1");
                    return false;
                }
                catch (IOException) { }
            }
            //释放后可以再次打开
            using var access3 = storage.CreateAccess("world1");
            return access3.HasLock;
        }
        finally { TryCleanup(baseDir); }
    }

    private static bool TestPersistentServerLevelRoundTrip()
    {
        var dir = NewTempDir();
        try
        {
            var factory = NewFactory();
            var pos = new ChunkPos(3, -2);
            using (var regionStorage = NewRegionStorage(dir))
            {
                var level = new PersistentServerLevel(regionStorage, 0, 24, factory);
                var chunk = new LevelChunk(pos, 0, 24,
                    factory.CreateForBlockStates, factory.CreateForBiomes, level);
                //写入一个方块便于后续验证
                var stone = factory.LookupBlockState(Identifier.WithDefaultNamespace("test_block"));
                if (stone is null) return false;
                chunk.SetBlockState(0, 0, 0, 0, stone.Value);
                level.SaveChunkAsync(chunk).GetAwaiter().GetResult();
                level.SynchronizeAsync(true).GetAwaiter().GetResult();
            }
            //重新构造 PersistentServerLevel 模拟重启清空 in-memory 缓存
            using (var regionStorage2 = NewRegionStorage(dir))
            {
                var level2 = new PersistentServerLevel(regionStorage2, 0, 24, factory);
                var loaded = level2.LoadChunkAsync(pos).GetAwaiter().GetResult();
                if (loaded is null) return false;
                if (loaded.Pos.X != 3 || loaded.Pos.Z != -2) return false;
                if (loaded.MinSectionY != 0) return false;
                if (loaded.SectionsCount != 24) return false;
                if (loaded.ChunkStatus != ChunkStatus.FULL) return false;
                var section = loaded.GetSection(0);
                if (section is null) return false;
                //往返后读取写入位置验证 test_block 被持久化保留
                var state = section.GetBlockState(0, 0, 0);
                return state.Owner?.Id == Identifier.WithDefaultNamespace("test_block");
            }
        }
        finally { TryCleanup(dir); }
    }

    private static bool TestPersistentServerLevelLoadMissing()
    {
        var dir = NewTempDir();
        try
        {
            var factory = NewFactory();
            using var regionStorage = NewRegionStorage(dir);
            var level = new PersistentServerLevel(regionStorage, 0, 24, factory);
            var loaded = level.LoadChunkAsync(new ChunkPos(99, 99)).GetAwaiter().GetResult();
            return loaded is null;
        }
        finally { TryCleanup(dir); }
    }
}
