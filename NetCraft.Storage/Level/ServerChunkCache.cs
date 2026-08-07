using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ServerChunkCache 服务端区块缓存对应原版 net.minecraft.server.level.ServerChunkCache
//持有 ChunkMap 与加载回调异步调度区块加载避免主循环同步阻塞
//阶段 11.48 引入替代 PersistentServerLevel.GetChunk 同步等待
//阶段 11.52 加 generator 回调存档未命中时走 ChunkStatus 生成链生成新 chunk
//loader 回调由 PersistentServerLevel 传入 LoadChunkAsync 避免循环依赖
public sealed class ServerChunkCache : ChunkSource
{
    private readonly ChunkMap _chunkMap;
    private readonly Func<ChunkPos, Task<ChunkAccess?>> _loader;
    //generator 存档未命中时调用的生成器由 Game 层传入走 ChunkStatusProcessor 流水线
    private readonly Func<ChunkPos, ChunkAccess?>? _generator;
    private readonly Dictionary<long, ChunkHolder> _holders = new();
    private readonly Dictionary<long, ChunkAccess> _loaded = new();

    //ChunkMap 玩家视距管理器
    public ChunkMap ChunkMap => _chunkMap;

    //HoldersCount 当前 holder 数量供诊断
    public int HoldersCount => _holders.Count;

    //LoadedCount 已加载缓存数量供诊断
    public int LoadedCount => _loaded.Count;

    public ServerChunkCache(Func<ChunkPos, Task<ChunkAccess?>> loader, int viewDistance = 8,
        Func<ChunkPos, ChunkAccess?>? generator = null)
    {
        _loader = loader;
        _chunkMap = new ChunkMap(viewDistance);
        _generator = generator;
    }

    //GetChunk 按 chunkX/chunkZ 获取完整区块对应原版 getChunk
    //主循环安全未加载返回 null 不阻塞
    public override ChunkAccess? GetChunk(int x, int z)
        => GetChunk(x, z, ChunkStatus.FULL, false);

    //GetChunk 按 chunkX/chunkZ 与 ChunkStatus 获取区块对应原版 getChunk
    //require 为 true 时未加载同步等待加载完成后返回加载失败抛异常仅测试或必须同步场景用
    //require 为 false 时未加载返回 null 不阻塞主循环
    public override ChunkAccess? GetChunk(int x, int z, ChunkStatus status, bool require)
    {
        Log.Debug($"GetChunk 入口 x={x} z={z} status={status} require={require}");
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
        {
            Log.Debug($"GetChunk 出口 result=缓存命中");
            return cached;
        }
        if (_holders.TryGetValue(key, out var holder) && holder.IsDone)
        {
            var result = holder.Future.Result;
            if (result.IsSuccess)
            {
                _loaded[key] = result.Chunk!;
                Log.Debug($"GetChunk 出口 result=holder完成");
                return result.Chunk;
            }
            if (require) throw result.Error!;
            Log.Debug($"GetChunk 出口 result=null holder失败");
            return null;
        }
        if (require)
        {
            var r = GetChunkFuture(x, z, status).GetAwaiter().GetResult().OrElse(null);
            Log.Debug($"GetChunk 出口 result=同步等待");
            return r;
        }
        Log.Debug($"GetChunk 出口 result=null 未加载");
        return null;
    }

    //HasChunk 判断区块是否已加载对应原版 hasChunk
    public override bool HasChunk(int x, int z)
    {
        Log.Debug($"HasChunk 入口 x={x} z={z}");
        var key = ChunkPos.Pack(x, z);
        var result = _loaded.ContainsKey(key)
            || (_holders.TryGetValue(key, out var h) && h.IsDone && h.Future.Result.IsSuccess);
        Log.Debug($"HasChunk 出口 result={result}");
        return result;
    }

    //GetChunkFuture 异步获取区块 future 对应原版 getChunkFuture
    //已加载缓存命中立即返回未加载提交 LoadAsync 任务
    public Task<ChunkResult> GetChunkFuture(int x, int z, ChunkStatus status)
    {
        Log.Debug($"GetChunkFuture 入口 x={x} z={z} status={status}");
        var key = ChunkPos.Pack(x, z);
        if (_loaded.TryGetValue(key, out var cached))
        {
            Log.Debug($"GetChunkFuture 出口 result=缓存命中");
            return Task.FromResult(ChunkResult.Success(cached));
        }
        var holder = GetOrCreateHolder(x, z);
        if (holder.IsDone)
        {
            Log.Debug($"GetChunkFuture 出口 result=holder已完成");
            return holder.Future;
        }
        if (holder.MarkScheduled())
        {
            Log.Debug($"步骤1 提交LoadAsync holder={holder.Pos}");
            _ = LoadAsync(holder);
        }
        Log.Debug($"GetChunkFuture 出口 result=holder.Future");
        return holder.Future;
    }

    //GetOrCreateHolder 获取或创建 holder 对应原版 ChunkMap.getOrCreateHolder
    public ChunkHolder GetOrCreateHolder(int x, int z)
    {
        Log.Debug($"GetOrCreateHolder 入口 x={x} z={z}");
        var key = ChunkPos.Pack(x, z);
        if (_holders.TryGetValue(key, out var h))
        {
            //Log.Debug($"GetOrCreateHolder 出口 result=已存在");
            return h;
        }
        h = new ChunkHolder(new ChunkPos(x, z));
        _holders[key] = h;
        //Log.Debug($"GetOrCreateHolder 出口 result=新建");
        return h;
    }

    //TryGetHolder 查询 holder 不创建对应原版 getHolder
    public ChunkHolder? TryGetHolder(int x, int z)
        => _holders.TryGetValue(ChunkPos.Pack(x, z), out var h) ? h : null;

    //LoadAsync 异步加载区块完成或失败后回填 holder 对应原版 schedule chunk load
    //loader 返回 null 表示存档无此区块走 generator 走 ChunkStatus 生成链生成新 chunk
    //generator 也为 null 时 holder.Fail 传递 UnloadedChunkException
    private async Task LoadAsync(ChunkHolder holder)
    {
        try
        {
            var chunk = await _loader(holder.Pos);
            if (chunk is null && _generator is not null)
                chunk = _generator(holder.Pos);
            if (chunk is null)
                holder.Fail(new UnloadedChunkException($"Chunk {holder.Pos} not found in storage and no generator"));
            else
                holder.Complete(chunk);
        }
        catch (Exception e)
        {
            holder.Fail(new UnloadedChunkException($"Failed to load chunk {holder.Pos}", e));
        }
    }

    //Tick 推进区块调度对应原版 ServerChunkCache.tick
    //完成的 holder 移入缓存视距外且 ticket level 达 MaxLevel 的 holder 回收
    public override void Tick()
    {
        //Log.Debug($"Tick 入口 holders={_holders.Count} loaded={_loaded.Count}");
        List<long>? expired = null;
        foreach (var (key, holder) in _holders)
        {
            if (holder.IsDone)
            {
                var result = holder.Future.Result;
                if (result.IsSuccess && !_loaded.ContainsKey(key))
                    _loaded[key] = result.Chunk!;
                if (holder.TicketLevel >= ChunkHolder.MaxLevel)
                {
                    expired ??= new List<long>();
                    expired.Add(key);
                }
            }
        }
        if (expired is not null)
        {
            //Log.Debug($"步骤1 回收过期holder count={expired.Count}");
            foreach (var key in expired) _holders.Remove(key);
        }
        //Log.Debug($"Tick 出口");
    }

    //UpdatePlayerPos 更新玩家所在 chunk 触发视距内 holder ticket 提升
    //对应原版 ServerPlayer.trackChunk 后 ChunkMap.move 的 ticket 更新
    //玩家位置追踪子系统未完整实现此处走 stub 仅提升视距内 holder ticket
    public void UpdatePlayerPos(int chunkX, int chunkZ)
    {
        Log.Debug($"UpdatePlayerPos 入口 chunkX={chunkX} chunkZ={chunkZ}");
        foreach (var (x, z) in _chunkMap.UpdatePlayerPos(chunkX, chunkZ))
        {
            var holder = GetOrCreateHolder(x, z);
            holder.UpdateTicketLevel(ChunkHolder.TickingLevel);
        }
        Log.Debug($"UpdatePlayerPos 出口");
    }

    protected override void Dispose(bool disposing)
    {
        Log.Debug($"Dispose 入口 disposing={disposing}");
        if (disposing)
        {
            _holders.Clear();
            _loaded.Clear();
        }
        base.Dispose(disposing);
        Log.Debug($"Dispose 出口");
    }
}
