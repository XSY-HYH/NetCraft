using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//DynamicAtlasAllocatorTests 动态图集分配器单元测试
//覆盖 BitSet/GetOrAllocate 状态机/ReclaimSpaceFor/EndFrame/HasSpaceForAll
//GuiItemAtlas 数据层测试 ComputeTextureSizeFor/GetOrUpdate SlotState 流程
//纯 CPU 逻辑测试不依赖 Vulkan mock device 仅支持 CreateImage 返回桩
internal static class DynamicAtlasAllocatorTests
{
    public const string Module = "dynamicatlas";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("BitSet.Set sets range and NextSetBit finds first", TestBitSetSetRange);
        yield return ("BitSet.Clear clears bit and NextSetBit skips", TestBitSetClear);
        yield return ("BitSet.NextSetBit returns -1 when empty", TestBitSetEmpty);
        yield return ("Allocator.GetOrAllocate first call returns Empty state", TestGetOrAllocateEmpty);
        yield return ("Allocator.GetOrAllocate second call same key returns Ready", TestGetOrAllocateReady);
        yield return ("Allocator.GetOrAllocate stale slot returns Stale state", TestGetOrAllocateStale);
        yield return ("Allocator.GetOrAllocate returns null when full", TestGetOrAllocateFull);
        yield return ("Allocator.HasSpaceForAll true when enough slots", TestHasSpaceForAllTrue);
        yield return ("Allocator.HasSpaceForAll false when overflow", TestHasSpaceForAllFalse);
        yield return ("Allocator.ReclaimSpaceFor frees non-target keys", TestReclaimSpaceFor);
        yield return ("Allocator.ReclaimSpaceFor returns true when all keys exist", TestReclaimSpaceAllExist);
        yield return ("Allocator.EndFrame frees discardAfterFrame slots", TestEndFrameFreesAnimated);
        yield return ("Allocator.EndFrame keeps non-animated slots", TestEndFrameKeepsNonAnimated);
        yield return ("Allocator.FreeSlotCount decrements and increments", TestFreeSlotCount);
        yield return ("Allocator.UsedSlotKeys tracks allocated keys", TestUsedSlotKeys);
        yield return ("GuiItemAtlas.ComputeTextureSizeFor clamps to minimum 512", TestComputeTextureSizeMinimum);
        yield return ("GuiItemAtlas.ComputeTextureSizeFor returns power of two", TestComputeTextureSizePowerOfTwo);
        yield return ("GuiItemAtlas.ComputeTextureSizeFor scales with slot size", TestComputeTextureSizeScalesWithSlot);
        yield return ("GuiItemAtlas.ComputeTextureSizeWithDevice uses device.Limits", TestComputeTextureSizeWithDevice);
        yield return ("GuiItemAtlas.ComputeTextureSizeFor clamps to device limits", TestComputeTextureSizeClampsToDeviceLimits);
        yield return ("GuiItemAtlas.GetOrUpdate returns SlotView with correct UV", TestGetOrUpdateReturnsUV);
        yield return ("GuiItemAtlas.GetOrUpdate Empty calls DrawToSlot clear=false", TestGetOrUpdateEmptyCallsDraw);
        yield return ("GuiItemAtlas.GetOrUpdate Stale calls DrawToSlot clear=true", TestGetOrUpdateStaleCallsDraw);
        yield return ("GuiItemAtlas.GetOrUpdate Ready skips DrawToSlot", TestGetOrUpdateReadySkipsDraw);
        yield return ("GuiItemAtlas.TryPrepareFor returns true when fits", TestTryPrepareForFits);
        yield return ("GuiItemAtlas.TryPrepareFor reclaims when full", TestTryPrepareForReclaims);
        yield return ("GuiItemAtlas.EndFrame delegates to allocator", TestAtlasEndFrame);
    }

    private static bool TestBitSetSetRange()
    {
        var bs = new BitSet(100);
        bs.Set(10, 20);
        return bs.NextSetBit(0) == 10 && bs.NextSetBit(19) == 19 && bs.NextSetBit(20) == -1;
    }

    private static bool TestBitSetClear()
    {
        var bs = new BitSet(50);
        bs.Set(5, 10);
        bs.Clear(7);
        return bs.NextSetBit(5) == 5 && bs.NextSetBit(7) == 8 && bs.NextSetBit(9) == 9;
    }

    private static bool TestBitSetEmpty()
    {
        var bs = new BitSet(50);
        return bs.NextSetBit(0) == -1;
    }

    private static bool TestGetOrAllocateEmpty()
    {
        var alloc = new DynamicAtlasAllocator<string>(4, 4);
        var slot = alloc.GetOrAllocate("a", false);
        return slot != null && slot!.State == DynamicAtlasAllocator<string>.SlotState.Empty
            && slot.X == 0 && slot.Y == 0;
    }

    private static bool TestGetOrAllocateReady()
    {
        var alloc = new DynamicAtlasAllocator<string>(4, 4);
        alloc.GetOrAllocate("a", false);
        var slot2 = alloc.GetOrAllocate("a", false);
        return slot2 != null && slot2!.State == DynamicAtlasAllocator<string>.SlotState.Ready;
    }

    //TestGetOrAllocateStale 释放后重新分配同一槽位走 Stale 路径
    private static bool TestGetOrAllocateStale()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        var s1 = alloc.GetOrAllocate("a", false);
        //EndFrame 不释放 discardAfterFrame=false 的槽位 需 ReclaimSpaceFor 释放
        var keys = new HashSet<string> { "b" };
        alloc.ReclaimSpaceFor(keys);
        //a 被释放重新分配 b 到同槽位 fresh=false 走 Stale
        var s2 = alloc.GetOrAllocate("b", false);
        return s1!.X == s2!.X && s1.Y == s2.Y
            && s2.State == DynamicAtlasAllocator<string>.SlotState.Stale;
    }

    private static bool TestGetOrAllocateFull()
    {
        var alloc = new DynamicAtlasAllocator<string>(1, 1);
        alloc.GetOrAllocate("a", false);
        return alloc.GetOrAllocate("b", false) == null;
    }

    private static bool TestHasSpaceForAllTrue()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("a", false);
        var keys = new HashSet<string> { "a", "b" };
        return alloc.HasSpaceForAll(keys);
    }

    private static bool TestHasSpaceForAllFalse()
    {
        var alloc = new DynamicAtlasAllocator<string>(1, 1);
        alloc.GetOrAllocate("a", false);
        var keys = new HashSet<string> { "a", "b", "c" };
        return !alloc.HasSpaceForAll(keys);
    }

    //TestReclaimSpaceFor 分配 a/b/c 保留 b/c 释放 a 腾位
    private static bool TestReclaimSpaceFor()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("a", false);
        alloc.GetOrAllocate("b", false);
        alloc.GetOrAllocate("c", false);
        var keys = new HashSet<string> { "b", "c", "d" };
        var ok = alloc.ReclaimSpaceFor(keys);
        //a 被释放剩 b/c=2 槽位 FreeSlotCount=2 d 可分配
        return ok && alloc.FreeSlotCount == 2;
    }

    private static bool TestReclaimSpaceAllExist()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("a", false);
        alloc.GetOrAllocate("b", false);
        var keys = new HashSet<string> { "a", "b" };
        return alloc.ReclaimSpaceFor(keys) && alloc.FreeSlotCount == 2;
    }

    private static bool TestEndFrameFreesAnimated()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("anim", true);  //discardAfterFrame=true
        alloc.GetOrAllocate("static", false);
        alloc.EndFrame();
        //anim 被释放 static 保留
        var keys = new List<string>();
        foreach (var k in alloc.UsedSlotKeys) keys.Add(k);
        return keys.Count == 1 && keys[0] == "static";
    }

    private static bool TestEndFrameKeepsNonAnimated()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("a", false);
        alloc.GetOrAllocate("b", false);
        alloc.EndFrame();
        return alloc.FreeSlotCount == 2 && alloc.UsedSlotKeys.Count == 2;
    }

    private static bool TestFreeSlotCount()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        var initial = alloc.FreeSlotCount;
        alloc.GetOrAllocate("a", false);
        var afterAlloc = alloc.FreeSlotCount;
        var keys = new HashSet<string> { "b" };
        alloc.ReclaimSpaceFor(keys);
        var afterReclaim = alloc.FreeSlotCount;
        return initial == 4 && afterAlloc == 3 && afterReclaim == 4;
    }

    private static bool TestUsedSlotKeys()
    {
        var alloc = new DynamicAtlasAllocator<string>(2, 2);
        alloc.GetOrAllocate("a", false);
        alloc.GetOrAllocate("b", false);
        var keys = new HashSet<string>(alloc.UsedSlotKeys);
        return keys.Count == 2 && keys.Contains("a") && keys.Contains("b");
    }

    private static bool TestComputeTextureSizeMinimum()
    {
        //required=1 preferred=1 square=1 1*16=16 clamp 到 512
        return GuiItemAtlas.ComputeTextureSizeFor(16, 1, 4096) == 512;
    }

    private static bool TestComputeTextureSizePowerOfTwo()
    {
        //required=100 preferred=150 square=13 13*16=208 上取 2 的幂=256 clamp 512
        var size = GuiItemAtlas.ComputeTextureSizeFor(16, 100, 4096);
        return size == 512 && (size & (size - 1)) == 0;
    }

    private static bool TestComputeTextureSizeScalesWithSlot()
    {
        //required=100 preferred=150 square=13 13*32=416 上取 2 的幂=512 clamp 512
        var size16 = GuiItemAtlas.ComputeTextureSizeFor(16, 100, 4096);
        var size32 = GuiItemAtlas.ComputeTextureSizeFor(32, 100, 4096);
        return size32 >= size16;
    }

    //TestComputeTextureSizeWithDevice device.Limits.MaxTextureSize 查询替代硬编码 4096
    //MockGpuDevice 默认 4096 与显式 4096 重载结果一致验证 device 重载链路正确
    private static bool TestComputeTextureSizeWithDevice()
    {
        var device = new MockGpuDevice();
        var fromDevice = GuiItemAtlas.ComputeTextureSizeFor(device, 16, 100);
        var fromExplicit = GuiItemAtlas.ComputeTextureSizeFor(16, 100, 4096);
        return fromDevice == fromExplicit;
    }

    //TestComputeTextureSizeClampsToDeviceLimits 小 maxTextureSize 时 clamp 到设备限制
    //required=1000 preferred=1500 square=39 39*32=1248 取 2 的幂=2048 超 1024 clamp 1024
    private static bool TestComputeTextureSizeClampsToDeviceLimits()
    {
        var smallLimits = new DeviceLimits(1024);
        var size = GuiItemAtlas.ComputeTextureSizeFor(32, 1000, smallLimits.MaxTextureSize);
        return size == 1024;
    }

    //TestGetOrUpdateReturnsUV 首次分配 slot(0,0) UV u0=0 v0=0 slotUvSize=slotSize/texSize
    //Vulkan 纹理 V=0 对应图像顶部 slot(0,0) 顶部 UV 为 0
    private static bool TestGetOrUpdateReturnsUV()
    {
        using var atlas = new MockItemAtlas(512, 16);
        var view = atlas.GetOrUpdate("item1", false);
        var slotUv = 16f / 512f;
        return view != null
            && view!.U0 == 0f
            && view.V0 == 0f
            && Math.Abs(view.U1 - slotUv) < 1e-6f
            && Math.Abs(view.V1 - slotUv) < 1e-6f;
    }

    private static bool TestGetOrUpdateEmptyCallsDraw()
    {
        using var atlas = new MockItemAtlas(512, 16);
        atlas.GetOrUpdate("item1", false);
        return atlas.DrawCalls.Count == 1
            && atlas.DrawCalls[0].Clear == false
            && atlas.DrawCalls[0].SlotX == 0
            && atlas.DrawCalls[0].SlotY == 0;
    }

    private static bool TestGetOrUpdateStaleCallsDraw()
    {
        using var atlas = new MockItemAtlas(512, 16);
        atlas.GetOrUpdate("item1", false);
        //ReclaimSpaceFor 释放 item1 重新分配 fresh=false 走 Stale
        var keys = new HashSet<object> { "item2" };
        atlas.GetAllocator().ReclaimSpaceFor(keys);
        atlas.GetOrUpdate("item2", false);
        //第二次 DrawCalls clear=true
        return atlas.DrawCalls.Count == 2
            && atlas.DrawCalls[1].Clear == true;
    }

    private static bool TestGetOrUpdateReadySkipsDraw()
    {
        using var atlas = new MockItemAtlas(512, 16);
        atlas.GetOrUpdate("item1", false);
        atlas.DrawCalls.Clear();
        //第二次同 key 走 Ready 不调 DrawToSlot
        atlas.GetOrUpdate("item1", false);
        return atlas.DrawCalls.Count == 0;
    }

    private static bool TestTryPrepareForFits()
    {
        using var atlas = new MockItemAtlas(512, 16);
        var items = new HashSet<object> { "a", "b", "c" };
        return atlas.TryPrepareFor(items);
    }

    private static bool TestTryPrepareForReclaims()
    {
        using var atlas = new MockItemAtlas(16, 16);
        //1x1 槽位分配 a 后 b 进不来 ReclaimSpaceFor 释放 a
        atlas.GetOrUpdate("a", false);
        var items = new HashSet<object> { "b" };
        return atlas.TryPrepareFor(items);
    }

    private static bool TestAtlasEndFrame()
    {
        using var atlas = new MockItemAtlas(512, 16);
        atlas.GetOrUpdate("anim", true);
        atlas.EndFrame();
        //anim 帧末释放 再次 GetOrUpdate 应走 Empty（新分配）
        var view = atlas.GetOrUpdate("anim", false);
        //anim 被释放后重新分配 同槽位 fresh=false 走 Stale
        return view != null && atlas.DrawCalls.Count == 2;
    }

    //MockItemAtlas 测试用 GuiItemAtlas 子类记录 DrawToSlot 调用
    private sealed class MockItemAtlas : GuiItemAtlas
    {
        public readonly List<(int SlotX, int SlotY, bool Clear, object Item)> DrawCalls = new();
        public MockItemAtlas(int textureSize, int slotTextureSize)
            : base(new MockGpuDevice(), textureSize, slotTextureSize) { }
        //GetAllocator 暴露 protected Allocator 供测试调 ReclaimSpaceFor
        public DynamicAtlasAllocator<object> GetAllocator() => Allocator;
        protected override void DrawToSlot(int slotX, int slotY, bool clear, object itemIdentity)
            => DrawCalls.Add((slotX, slotY, clear, itemIdentity));
    }

    //MockGpuDevice 测试用 GpuDevice 桩支持 CreateImage 返回 MockGpuImage
    //Limits 默认 4096 测试可改 MaxTextureSize 验证 ComputeTextureSizeFor clamp 行为
    private sealed class MockGpuDevice : GpuDevice
    {
        public override DeviceLimits Limits { get; } = new(4096);
        public MockGpuDevice() : base(new EmptyGpuContext()) { }
        public override GpuCommandBuffer CreateCommandBuffer() => throw new NotSupportedException();
        public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription d) => throw new NotSupportedException();
        public override GpuBuffer CreateBuffer(int size, GpuBufferUsage u) => throw new NotSupportedException();
        public override GpuImage CreateImage(GpuImageDescription desc) => new MockGpuImage(desc);
        public override GpuShader CreateShader(GpuShaderStage s, byte[] c, string e = "main") => throw new NotSupportedException();
        public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription d) => throw new NotSupportedException();
        public override GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout l) => throw new NotSupportedException();
        public override GpuSampler CreateSampler(GpuSamplerDescription d) => throw new NotSupportedException();
    }

    //MockGpuImage 测试用 GpuImage 桩不依赖 Vulkan
    private sealed class MockGpuImage : GpuImage
    {
        public MockGpuImage(GpuImageDescription desc) : base(desc) { }
        public override void Upload(ReadOnlySpan<byte> pixels) { }
    }
}
