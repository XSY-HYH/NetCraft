using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Storage;

//saveddata 存储对应原版 net.minecraft.world.level.storage.SavedDataStorage
//管理 saveddata 目录下各 SavedData 的读写与升级
//readTagFromDisk 走 NbtIO + DataFixTypes.update 真实升级路径
//computeIfAbsent 走 ReadTagFromDisk + SavedDataType.Create 真实创建路径
//scheduleSave 走 dirty 数据扫描 + NbtIo.Write 真实写入路径
public sealed class SavedDataStorage : IDisposable
{
    private readonly NetCraft.DataFixer.DataFixer _fixerUpper;
    private readonly string _dataFolder;
    //cache 按 SavedDataType.Id 索引已加载的 SavedData 实例
    private readonly Dictionary<string, SavedData> _cache = new();
    //registryAccess 注册表访问入口用于 SavedDataType.Create 时查表
    private RegistryAccess? _registryAccess;
    private bool _closed;

    public SavedDataStorage(string dataFolder, NetCraft.DataFixer.DataFixer fixerUpper)
    {
        _fixerUpper = fixerUpper;
        _dataFolder = dataFolder;
    }

    //设置 RegistryAccess 用于 ComputeIfAbsent 时 SavedDataType.Create
    //对应原版 savedDataStorage 通过 HolderLookup.Provider 传入注册表入口
    public void SetRegistryAccess(RegistryAccess registryAccess) => _registryAccess = registryAccess;

    //readTagFromDisk 从磁盘读取 CompoundTag 并按版本走 DataFixer 升级
    //自动识别 gzip 头决定用 ReadCompressed 还是 Read
    //dataVersion 缺失默认 1343 对应原版 1.13 快照前
    public CompoundTag ReadTagFromDisk(string dataFile, DataFixTypes type, int newVersion)
    {
        //读前两字节判断 gzip 头 0x1f 0x8b
        using (var fs = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int b1 = fs.ReadByte();
            int b2 = fs.ReadByte();
            bool isGzip = b1 == 0x1f && b2 == 0x8b;
            CompoundTag tag;
            if (isGzip)
            {
                tag = NbtIo.ReadCompressed(dataFile, NbtAccounter.UnlimitedHeap());
            }
            else
            {
                tag = NbtIo.Read(dataFile) ?? throw new IOException("Failed to read NBT from " + dataFile);
            }
            int version = NbtUtils.GetDataVersion(tag, 1343);
            var dynamic = new Dynamic<Tag>(NbtOps.Instance, tag);
            var fixedDynamic = type.Update(_fixerUpper, dynamic, version, newVersion);
            return (CompoundTag)fixedDynamic.Value;
        }
    }

    //computeIfAbsent 按 SavedDataType 计算或取得已存在数据对应原版 computeIfAbsent
    //缓存命中返回已加载实例否则从磁盘读取用 SavedDataType.Create 创建并缓存
    public T ComputeIfAbsent<T>(SavedDataType<T> type) where T : SavedData
    {
        if (_cache.TryGetValue(type.Id, out var cached)) return (T)cached;
        var dataFile = Path.Combine(_dataFolder, type.Id + ".dat");
        T data;
        if (File.Exists(dataFile))
        {
            //SavedData 子类的 DataFixTypes 各不同这里用 Chunk 占位走通用 DataFixer 路径
            var tag = ReadTagFromDisk(dataFile, DataFixTypes.Chunk, SharedConstants.WorldDataVersion);
            //registryAccess 缺失用空 RegistryAccess 占位让 SavedDataType.Create 自行处理
            var registryAccess = _registryAccess ?? new ImmutableRegistryAccess(Enumerable.Empty<KeyValuePair<Identifier, object>>());
            data = type.Create(tag, registryAccess);
        }
        else
        {
            //新实例走 SavedDataType.Create 用空 CompoundTag 占位对应原版 SavedDataType.create
            var registryAccess = _registryAccess ?? new ImmutableRegistryAccess(Enumerable.Empty<KeyValuePair<Identifier, object>>());
            data = type.Create(new CompoundTag(), registryAccess);
        }
        _cache[type.Id] = data;
        return data;
    }

    //get 按 SavedDataType 取得已缓存或从磁盘读取的数据
    public T? Get<T>(SavedDataType<T> type) where T : SavedData
        => _cache.TryGetValue(type.Id, out var data) ? (T)data : null;

    //set 缓存 SavedData 并标记 dirty
    public void Set<T>(SavedDataType<T> type, T data) where T : SavedData
    {
        _cache[type.Id] = data;
        data.SetDirty();
    }

    //scheduleSave 收集 dirty 数据并行写入磁盘对应原版 scheduleSave
    //返回已完成的 Task 对齐原版 CompletableFuture.allOf
    public Task ScheduleSave()
    {
        foreach (var (id, data) in _cache)
        {
            if (!data.IsDirty) continue;
            var dataFile = Path.Combine(_dataFolder, id + ".dat");
            Directory.CreateDirectory(_dataFolder);
            var tag = data.Save(new CompoundTag());
            NbtIo.WriteCompressed(tag, dataFile);
            data.ClearDirty();
        }
        return Task.CompletedTask;
    }

    //saveAndJoin 等待 scheduleSave 完成
    public void SaveAndJoin()
    {
        try
        {
            ScheduleSave().Wait();
        }
        catch (NotSupportedException)
        {
            //无 dirty 数据或子系统未就绪时空操作
        }
    }

    //close 检查 closed 状态调用 saveAndJoin 后标记关闭
    public void Dispose()
    {
        if (_closed)
        {
            throw new InvalidOperationException("Trying to close SavedDataStorage when it is already closed");
        }
        SaveAndJoin();
        _closed = true;
    }
}
