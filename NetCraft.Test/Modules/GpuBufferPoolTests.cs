using NetCraft.Gpu;

namespace NetCraft.Test.Modules;

//GpuBufferPoolTests 阶段 4 buffer 池单元测试
//覆盖池化复用/EndFrame 批量归还/Dispose 销毁
//用 mock factory 不依赖真实 GpuDevice
internal static class GpuBufferPoolTests
{
    public const string Module = "bufferpool";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("GetBuffer creates new when pool empty", TestCreatesNewWhenEmpty);
        yield return ("GetBuffer reuses available exact size", TestReusesExactSize);
        yield return ("GetBuffer reuses larger buffer when exact absent", TestReusesLarger);
        yield return ("GetBuffer picks smallest sufficient buffer", TestPicksSmallestSufficient);
        yield return ("ReturnBuffer puts buffer back to pool", TestReturnBuffer);
        yield return ("EndFrame returns all inUse buffers", TestEndFrameReturnsAll);
        yield return ("EndFrame clears inUse count", TestEndFrameClearsInUse);
        yield return ("Pool separates buffers by usage", TestSeparatesByUsage);
        yield return ("Dispose destroys all buffers", TestDisposeDestroysAll);
        yield return ("GetBuffer after Dispose throws ObjectDisposedException", TestThrowsAfterDispose);
    }

    //MockBuffer 测试用 GpuBuffer 桩记录 Dispose 调用
    private sealed class MockBuffer : GpuBuffer
    {
        public bool Disposed;
        public MockBuffer(int size, GpuBufferUsage usage) : base(size, usage) { }
        public override void Upload<T>(ReadOnlySpan<T> data) { }
        public override void Download<T>(Span<T> data) { }
        public override void Dispose() => Disposed = true;
    }

    //MakePool 创建带 mock factory 的池记录创建次数
    private static (GpuBufferPool pool, List<MockBuffer> created) MakePool()
    {
        var created = new List<MockBuffer>();
        var pool = new GpuBufferPool((size, usage) =>
        {
            var b = new MockBuffer(size, usage);
            created.Add(b);
            return b;
        });
        return (pool, created);
    }

    //TestCreatesNewWhenEmpty 验证空池时 GetBuffer 创建新 buffer
    private static bool TestCreatesNewWhenEmpty()
    {
        var (pool, created) = MakePool();
        var b = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        return b is MockBuffer && created.Count == 1 && b.Size == 1024;
    }

    //TestReusesExactSize 验证池有精确 size buffer 时复用不创建新
    private static bool TestReusesExactSize()
    {
        var (pool, created) = MakePool();
        var b1 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        pool.ReturnBuffer(b1);
        var b2 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        return ReferenceEquals(b1, b2) && created.Count == 1;
    }

    //TestReusesLarger 验证池有更大 buffer 时复用不创建新
    private static bool TestReusesLarger()
    {
        var (pool, created) = MakePool();
        var b1 = pool.GetBuffer(2048, GpuBufferUsage.VertexBuffer);
        pool.ReturnBuffer(b1);
        var b2 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        return ReferenceEquals(b1, b2) && b2.Size == 2048 && created.Count == 1;
    }

    //TestPicksSmallestSufficient 验证多个可用 buffer 取最小满足 size 的
    private static bool TestPicksSmallestSufficient()
    {
        var (pool, created) = MakePool();
        var b1 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        var b2 = pool.GetBuffer(4096, GpuBufferUsage.VertexBuffer);
        pool.ReturnBuffer(b1);
        pool.ReturnBuffer(b2);
        //需求 2048 应取 4096 的而非 1024(不够)
        var b3 = pool.GetBuffer(2048, GpuBufferUsage.VertexBuffer);
        return ReferenceEquals(b3, b2) && b3.Size == 4096;
    }

    //TestReturnBuffer 验证归还后 AvailableCount 增加
    private static bool TestReturnBuffer()
    {
        var (pool, _) = MakePool();
        var b = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        if (pool.AvailableCount(GpuBufferUsage.VertexBuffer) != 0) return false;
        pool.ReturnBuffer(b);
        return pool.AvailableCount(GpuBufferUsage.VertexBuffer) == 1 && pool.InUseCount == 0;
    }

    //TestEndFrameReturnsAll 验证 EndFrame 把所有 inUse 归还到可用池
    private static bool TestEndFrameReturnsAll()
    {
        var (pool, _) = MakePool();
        var b1 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        var b2 = pool.GetBuffer(2048, GpuBufferUsage.VertexBuffer);
        if (pool.InUseCount != 2) return false;
        pool.EndFrame();
        return pool.InUseCount == 0 && pool.AvailableCount(GpuBufferUsage.VertexBuffer) == 2;
    }

    //TestEndFrameClearsInUse 验证 EndFrame 后 inUse 清空可重新借出
    private static bool TestEndFrameClearsInUse()
    {
        var (pool, created) = MakePool();
        pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        pool.EndFrame();
        //重新借出应复用不创建新
        var b = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        return created.Count == 1 && ReferenceEquals(b, created[0]);
    }

    //TestSeparatesByUsage 验证不同 usage 的 buffer 分桶管理
    private static bool TestSeparatesByUsage()
    {
        var (pool, _) = MakePool();
        var vb = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        var ib = pool.GetBuffer(2048, GpuBufferUsage.IndexBuffer);
        pool.ReturnBuffer(vb);
        pool.ReturnBuffer(ib);
        return pool.AvailableCount(GpuBufferUsage.VertexBuffer) == 1
            && pool.AvailableCount(GpuBufferUsage.IndexBuffer) == 1;
    }

    //TestDisposeDestroysAll 验证 Dispose 销毁所有 buffer 含 inUse 和 available
    private static bool TestDisposeDestroysAll()
    {
        var (pool, created) = MakePool();
        var b1 = pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
        pool.ReturnBuffer(b1);
        var b2 = pool.GetBuffer(2048, GpuBufferUsage.VertexBuffer);
        //b1 在 available b2 在 inUse
        pool.Dispose();
        return ((MockBuffer)created[0]).Disposed && ((MockBuffer)created[1]).Disposed;
    }

    //TestThrowsAfterDispose 验证 Dispose 后 GetBuffer 抛 ObjectDisposedException
    private static bool TestThrowsAfterDispose()
    {
        var (pool, _) = MakePool();
        pool.Dispose();
        try
        {
            pool.GetBuffer(1024, GpuBufferUsage.VertexBuffer);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }
}
