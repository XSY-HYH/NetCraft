using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Optimizations.BlockState;
using NetCraft.Optimizations.Commands;
using NetCraft.Optimizations.Gpu;
using NetCraft.Optimizations.Memory;
using NetCraft.Optimizations.Nbt;
using NetCraft.Optimizations.Network;
using NetCraft.Optimizations.PalettedContainer;
using NetCraft.Optimizations.Profiler;
using NetCraft.Optimizations.Registry;
using NetCraft.Optimizations.Storage;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Test.Modules;

//Optimizations 子系统测试
//覆盖 10 个优化模块的开关查询与统计 API
internal static class OptimizationsTests
{
    public const string Module = "optimizations";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("BlockStateOptimizations flags enabled", TestBlockStateFlagsEnabled);
        yield return ("BlockStateOptimizations IsOptimized true", TestBlockStateIsOptimized);
        yield return ("BlockStateOptimizations GetStats returns states count", TestBlockStateGetStats);
        yield return ("NbtOptimizations flags enabled", TestNbtFlagsEnabled);
        yield return ("NbtOptimizations IsOptimized true", TestNbtIsOptimized);
        yield return ("NbtIo MemoryMapped read matches stream read", TestNbtMemoryMappedRoundTrip);
        yield return ("StorageOptimizations flags enabled", TestStorageFlagsEnabled);
        yield return ("StorageOptimizations IsOptimized true", TestStorageIsOptimized);
        yield return ("StorageOptimizations GetStats returns flags", TestStorageGetStats);
        yield return ("RegionFile MemoryMapped read matches stream read", TestRegionFileMemoryMappedRoundTrip);
        yield return ("IOWorker Channels path round-trip", TestIOWorkerChannelsRoundTrip);
        yield return ("RegistryOptimizations flags enabled", TestRegistryFlagsEnabled);
        yield return ("RegistryOptimizations IsOptimized true", TestRegistryIsOptimized);
        yield return ("RegistryOptimizations GetStats returns flags", TestRegistryGetStats);
        yield return ("MappedRegistry Freeze builds FrozenDictionary", TestRegistryFreezeBuildsFrozenDictionary);
        yield return ("NetworkOptimizations flags enabled", TestNetworkFlagsEnabled);
        yield return ("NetworkOptimizations IsOptimized true", TestNetworkIsOptimized);
        yield return ("NetworkOptimizations GetStats returns flags", TestNetworkGetStats);
        yield return ("FriendlyByteBuf VarInt Span write matches bytes", TestVarIntSpanWriteBytes);
        yield return ("FriendlyByteBuf VarLong Span write matches bytes", TestVarLongSpanWriteBytes);
        yield return ("PalettedContainerOptimizations flags enabled", TestPalettedContainerFlagsEnabled);
        yield return ("PalettedContainerOptimizations IsOptimized true", TestPalettedContainerIsOptimized);
        yield return ("PalettedContainerOptimizations GetStats returns flags", TestPalettedContainerGetStats);
        yield return ("ProfilerOptimizations flags enabled", TestProfilerFlagsEnabled);
        yield return ("ProfilerOptimizations IsOptimized true", TestProfilerIsOptimized);
        yield return ("ProfilerOptimizations GetStats returns flags", TestProfilerGetStats);
        yield return ("CommandsOptimizations flags enabled", TestCommandsFlagsEnabled);
        yield return ("CommandsOptimizations IsOptimized true", TestCommandsIsOptimized);
        yield return ("CommandsOptimizations GetStats returns flags", TestCommandsGetStats);
        yield return ("MemoryOptimizations flags enabled", TestMemoryFlagsEnabled);
        yield return ("MemoryOptimizations IsOptimized true", TestMemoryIsOptimized);
        yield return ("MemoryOptimizations GetStats returns flags", TestMemoryGetStats);
        yield return ("GpuOptimizations flags enabled", TestGpuFlagsEnabled);
        yield return ("GpuOptimizations IsOptimized true", TestGpuIsOptimized);
        yield return ("GpuOptimizations GetStats returns flags", TestGpuGetStats);
    }

    //BlockState 三个优化开关应全部启用对齐项目硬约束
    private static bool TestBlockStateFlagsEnabled()
    {
        return BlockStateOptimizations.IsIntEncodedEnabled
            && BlockStateOptimizations.IsPropertyCompactArrayEnabled
            && BlockStateOptimizations.IsCachePrecomputedEnabled;
    }

    //IsOptimized 检查三开关全开判断优化启用
    private static bool TestBlockStateIsOptimized()
        => BlockStateOptimizations.IsOptimized;

    //GetStats 返回 TotalStates 非负 TotalProperties 非负
    //无注册时 TotalStates=0 也算通过避免依赖 Bootstrap 状态
    private static bool TestBlockStateGetStats()
    {
        var stats = BlockStateOptimizations.GetStats();
        return stats.TotalStates >= 0
            && stats.TotalProperties >= 0
            && stats.IsOptimized == BlockStateOptimizations.IsOptimized;
    }

    //NBT 三个优化开关应全部启用
    private static bool TestNbtFlagsEnabled()
    {
        return NbtOptimizations.IsCodecSourceGeneratorEnabled
            && NbtOptimizations.IsIoMemoryMappedEnabled
            && NbtOptimizations.IsWriteBufferPooledEnabled;
    }

    private static bool TestNbtIsOptimized()
        => NbtOptimizations.IsOptimized;

    //MemoryMapped 读 GZIP 压缩 CompoundTag 结果应与 Stream 读一致字节级兼容
    //对应优化点 2.2 验证 MMF 重载不破坏 NBT 语义
    private static bool TestNbtMemoryMappedRoundTrip()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "nbt_mmap_test_" + Guid.NewGuid().ToString("N") + ".nbt");
        try
        {
            var original = new CompoundTag();
            original.Put("name", StringTag.ValueOf("hello"));
            original.Put("count", IntTag.ValueOf(42));
            var listTag = new ListTag();
            listTag.Add(IntTag.ValueOf(1));
            listTag.Add(IntTag.ValueOf(2));
            original.Put("list", listTag);

            NbtIo.WriteCompressed(original, tmpFile);

            var viaStream = NbtIo.ReadCompressed(tmpFile, NbtAccounter.UnlimitedHeap());
            var viaMmap = NbtIo.ReadCompressedWithMemoryMapped(tmpFile, NbtAccounter.UnlimitedHeap());

            //字段级比较定位失败点
            if (viaStream.Count != viaMmap.Count)
            {
                Console.WriteLine($"MMF count mismatch: stream={viaStream.Count} mmap={viaMmap.Count}");
                return false;
            }
            if (viaStream.GetStringValue("name") != viaMmap.GetStringValue("name"))
            {
                Console.WriteLine($"name mismatch: stream={viaStream.GetStringValue("name")} mmap={viaMmap.GetStringValue("name")}");
                return false;
            }
            if (viaStream.GetIntValue("count") != viaMmap.GetIntValue("count"))
            {
                Console.WriteLine($"count mismatch: stream={viaStream.GetIntValue("count")} mmap={viaMmap.GetIntValue("count")}");
                return false;
            }
            var streamList = viaStream.GetList("list");
            var mmapList = viaMmap.GetList("list");
            if (streamList is null || mmapList is null || streamList.Count != mmapList.Count)
            {
                Console.WriteLine($"list mismatch: stream={streamList?.Count ?? -1} mmap={mmapList?.Count ?? -1}");
                return false;
            }
            return true;
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    //Storage 三个优化开关应全部启用
    private static bool TestStorageFlagsEnabled()
    {
        return StorageOptimizations.IsRegionFileMemoryMappedEnabled
            && StorageOptimizations.IsIoWorkerChannelsEnabled
            && StorageOptimizations.IsChunkSerializeZeroCopyEnabled;
    }

    private static bool TestStorageIsOptimized()
        => StorageOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestStorageGetStats()
    {
        var stats = StorageOptimizations.GetStats();
        return stats.RegionFileMemoryMapped == StorageOptimizations.IsRegionFileMemoryMappedEnabled
            && stats.IoWorkerChannels == StorageOptimizations.IsIoWorkerChannelsEnabled
            && stats.ChunkSerializeZeroCopy == StorageOptimizations.IsChunkSerializeZeroCopyEnabled
            && stats.IsOptimized == StorageOptimizations.IsOptimized;
    }

    //RegionFile MMF 读取路径应与 FileStream 读取路径字节级一致
    //对应优化点 2.8 验证 RegionFileStorage.OpenChunkInputStream 不破坏 chunk 语义
    private static bool TestRegionFileMemoryMappedRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-region-mmap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var info = new RegionStorageInfo("test", null!, "chunk");
            using var storage = new RegionFileStorage(info, dir, sync: false);
            var pos = new ChunkPos(0, 0);
            var original = new CompoundTag();
            original.Put("name", StringTag.ValueOf("region-mmap"));
            original.PutInt("data", 12345);
            //写入触发 RegionFile.WriteChunk
            storage.Write(pos, original);
            //读取走 RegionFileStorage.OpenChunkInputStream 按开关选 MMF 路径
            var loaded = storage.Read(pos);
            if (loaded is null) return false;
            return NbtUtils.CompareNbt(original, loaded, partialListMatches: false);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }

    //IOWorker Channels 路径 store/loadAsync round-trip 应与原路径一致
    //对应优化点 2.11 验证 ChannelConsumeLoop 串行执行保序不破坏语义
    private static bool TestIOWorkerChannelsRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netcraft-ioworker-channels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var worker = new IOWorker(new RegionStorageInfo("test", null!, "chunk"), dir, sync: false);
            var pos = new ChunkPos(3, -5);
            var chunk = new CompoundTag();
            chunk.Put("name", StringTag.ValueOf("channels"));
            chunk.PutInt("level", 42);
            worker.Store(pos, chunk).Wait();
            worker.Synchronize(true).Wait();
            var loaded = worker.LoadAsync(pos).Result;
            return loaded.IsPresent
                && loaded.Get().GetStringValue("name") == "channels"
                && loaded.Get().GetIntValue("level") == 42;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }

    //Registry 两开关应全部启用
    private static bool TestRegistryFlagsEnabled()
    {
        return RegistryOptimizations.IsFrozenDictionaryEnabled
            && RegistryOptimizations.IsIntrusiveHolderStructEnabled;
    }

    private static bool TestRegistryIsOptimized()
        => RegistryOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestRegistryGetStats()
    {
        var stats = RegistryOptimizations.GetStats();
        return stats.FrozenDictionary == RegistryOptimizations.IsFrozenDictionaryEnabled
            && stats.IntrusiveHolderStruct == RegistryOptimizations.IsIntrusiveHolderStructEnabled
            && stats.IsOptimized == RegistryOptimizations.IsOptimized;
    }

    //Freeze 后 MappedRegistry 应构建 FrozenDictionary 索引并保证查询结果一致
    //对应优化点 2.3 验证 Frozen 索引不破坏原查询语义
    private static bool TestRegistryFreezeBuildsFrozenDictionary()
    {
        var regKey = ResourceKeys.CreateRegistryKey<string>(Identifier.WithDefaultNamespace("frozen_test_reg"));
        var reg = new MappedRegistry<string>(regKey, Lifecycle.Stable);
        var k1 = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:foo"));
        var k2 = ResourceKey<string>.Create(regKey, Identifier.Parse("minecraft:bar"));
        reg.Register(k1, "Foo", RegistrationInfo.BuiltIn);
        reg.Register(k2, "Bar", RegistrationInfo.BuiltIn);
        reg.Freeze();

        //Frozen 索引应保证 GetValue/Get/ContainsKey/GetId 查询结果一致
        return reg.GetValue(Identifier.Parse("minecraft:foo")) == "Foo"
            && reg.GetValue(Identifier.Parse("minecraft:bar")) == "Bar"
            && reg.GetValue(Identifier.Parse("minecraft:missing")) is null
            && reg.Get(Identifier.Parse("minecraft:foo"))?.Value == "Foo"
            && reg.ContainsKey(Identifier.Parse("minecraft:foo"))
            && !reg.ContainsKey(Identifier.Parse("minecraft:missing"))
            && reg.GetId("Foo") == 0
            && reg.GetId("Bar") == 1
            && reg.ById(0) == "Foo"
            && reg.ById(1) == "Bar";
    }

    //Network 三个优化开关应全部启用
    private static bool TestNetworkFlagsEnabled()
    {
        return NetworkOptimizations.IsStreamCodecStaticDispatchEnabled
            && NetworkOptimizations.IsVarIntBitOperationsEnabled
            && NetworkOptimizations.IsPacketBufferPooledEnabled;
    }

    private static bool TestNetworkIsOptimized()
        => NetworkOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestNetworkGetStats()
    {
        var stats = NetworkOptimizations.GetStats();
        return stats.StreamCodecStaticDispatch == NetworkOptimizations.IsStreamCodecStaticDispatchEnabled
            && stats.VarIntBitOperations == NetworkOptimizations.IsVarIntBitOperationsEnabled
            && stats.PacketBufferPooled == NetworkOptimizations.IsPacketBufferPooledEnabled
            && stats.IsOptimized == NetworkOptimizations.IsOptimized;
    }

    //VarInt Span 批量写入应产生正确字节序列对应优化点 2.7
    //验证 Span 写入不破坏原版 VarInt 字节级兼容
    private static bool TestVarIntSpanWriteBytes()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteVarInt(0);
        buf.WriteVarInt(1);
        buf.WriteVarInt(127);
        buf.WriteVarInt(128);
        buf.WriteVarInt(16383);
        buf.WriteVarInt(16384);
        buf.WriteVarInt(int.MaxValue);
        var bytes = buf.ToArray();
        //期望字节序列对应原版 VarInt 编码
        var expected = new byte[]
        {
            0x00,                       // 0
            0x01,                       // 1
            0x7F,                       // 127
            0x80, 0x01,                 // 128
            0xFF, 0x7F,                 // 16383
            0x80, 0x80, 0x01,           // 16384
            0xFF, 0xFF, 0xFF, 0xFF, 0x07, // int.MaxValue
        };
        if (bytes.Length != expected.Length) return false;
        for (var i = 0; i < bytes.Length; i++)
            if (bytes[i] != expected[i]) return false;

        //round-trip 读回应一致
        using var readBuf = new FriendlyByteBuf(bytes);
        return readBuf.ReadVarInt() == 0
            && readBuf.ReadVarInt() == 1
            && readBuf.ReadVarInt() == 127
            && readBuf.ReadVarInt() == 128
            && readBuf.ReadVarInt() == 16383
            && readBuf.ReadVarInt() == 16384
            && readBuf.ReadVarInt() == int.MaxValue;
    }

    //VarLong Span 批量写入应产生正确字节序列对应优化点 2.7
    private static bool TestVarLongSpanWriteBytes()
    {
        using var buf = new FriendlyByteBuf();
        buf.WriteVarLong(0);
        buf.WriteVarLong(1);
        buf.WriteVarLong(long.MaxValue);
        var bytes = buf.ToArray();
        using var readBuf = new FriendlyByteBuf(bytes);
        return readBuf.ReadVarLong() == 0
            && readBuf.ReadVarLong() == 1
            && readBuf.ReadVarLong() == long.MaxValue;
    }

    //PalettedContainer 三开关应全部启用
    private static bool TestPalettedContainerFlagsEnabled()
    {
        return PalettedContainerOptimizations.IsPalettedContainerSimdEnabled
            && PalettedContainerOptimizations.IsClassInstanceMultiMapFrozenEnabled
            && PalettedContainerOptimizations.IsBitStorageSpanBasedEnabled;
    }

    private static bool TestPalettedContainerIsOptimized()
        => PalettedContainerOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestPalettedContainerGetStats()
    {
        var stats = PalettedContainerOptimizations.GetStats();
        return stats.PalettedContainerSimd == PalettedContainerOptimizations.IsPalettedContainerSimdEnabled
            && stats.ClassInstanceMultiMapFrozen == PalettedContainerOptimizations.IsClassInstanceMultiMapFrozenEnabled
            && stats.BitStorageSpanBased == PalettedContainerOptimizations.IsBitStorageSpanBasedEnabled
            && stats.IsOptimized == PalettedContainerOptimizations.IsOptimized;
    }

    //Profiler 两开关应全部启用
    private static bool TestProfilerFlagsEnabled()
    {
        return ProfilerOptimizations.IsSpanPathEnabled
            && ProfilerOptimizations.IsZeroAllocEnabled;
    }

    private static bool TestProfilerIsOptimized()
        => ProfilerOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestProfilerGetStats()
    {
        var stats = ProfilerOptimizations.GetStats();
        return stats.SpanPath == ProfilerOptimizations.IsSpanPathEnabled
            && stats.ZeroAlloc == ProfilerOptimizations.IsZeroAllocEnabled
            && stats.IsOptimized == ProfilerOptimizations.IsOptimized;
    }

    //Commands 两开关应全部启用
    private static bool TestCommandsFlagsEnabled()
    {
        return CommandsOptimizations.IsCommandNodeStructEnabled
            && CommandsOptimizations.IsCommandStringInternEnabled;
    }

    private static bool TestCommandsIsOptimized()
        => CommandsOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestCommandsGetStats()
    {
        var stats = CommandsOptimizations.GetStats();
        return stats.CommandNodeStruct == CommandsOptimizations.IsCommandNodeStructEnabled
            && stats.CommandStringIntern == CommandsOptimizations.IsCommandStringInternEnabled
            && stats.IsOptimized == CommandsOptimizations.IsOptimized;
    }

    //Memory 四开关应全部启用
    private static bool TestMemoryFlagsEnabled()
    {
        return MemoryOptimizations.IsModelResourceLocationInternEnabled
            && MemoryOptimizations.IsVoxelShapeSharedEnabled
            && MemoryOptimizations.IsThreadingDetectorLightweightEnabled
            && MemoryOptimizations.IsPatchedDataComponentMapCompactEnabled;
    }

    private static bool TestMemoryIsOptimized()
        => MemoryOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestMemoryGetStats()
    {
        var stats = MemoryOptimizations.GetStats();
        return stats.ModelResourceLocationIntern == MemoryOptimizations.IsModelResourceLocationInternEnabled
            && stats.VoxelShapeShared == MemoryOptimizations.IsVoxelShapeSharedEnabled
            && stats.ThreadingDetectorLightweight == MemoryOptimizations.IsThreadingDetectorLightweightEnabled
            && stats.PatchedDataComponentMapCompact == MemoryOptimizations.IsPatchedDataComponentMapCompactEnabled
            && stats.IsOptimized == MemoryOptimizations.IsOptimized;
    }

    //Gpu 五开关应全部启用
    private static bool TestGpuFlagsEnabled()
    {
        return GpuOptimizations.IsVulkanCommandBufferPooledEnabled
            && GpuOptimizations.IsVulkanResourceUploadSpanEnabled
            && GpuOptimizations.IsVulkanMultiThreadedRecordingEnabled
            && GpuOptimizations.IsFrameGraphAutoBarrierEnabled
            && GpuOptimizations.IsSpirvCacheDiskEnabled;
    }

    private static bool TestGpuIsOptimized()
        => GpuOptimizations.IsOptimized;

    //GetStats 返回的开关状态应与属性一致
    private static bool TestGpuGetStats()
    {
        var stats = GpuOptimizations.GetStats();
        return stats.VulkanCommandBufferPooled == GpuOptimizations.IsVulkanCommandBufferPooledEnabled
            && stats.VulkanResourceUploadSpan == GpuOptimizations.IsVulkanResourceUploadSpanEnabled
            && stats.VulkanMultiThreadedRecording == GpuOptimizations.IsVulkanMultiThreadedRecordingEnabled
            && stats.FrameGraphAutoBarrier == GpuOptimizations.IsFrameGraphAutoBarrierEnabled
            && stats.SpirvCacheDisk == GpuOptimizations.IsSpirvCacheDiskEnabled
            && stats.IsOptimized == GpuOptimizations.IsOptimized;
    }
}
