using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.LevelGen;

//StructureFeatureManager 结构特征管理器对应原版 net.minecraft.world.level.StructureManager
//阶段 11.45 升级为真实管理类持有 StructureStart 字典按 ChunkPos 索引
//阶段 11.54-B 接入 CreateStarts 调 StructureFeature.FindGenerationPoint + CollectReferences 扫邻居
public sealed class StructureFeatureManager
{
    private readonly Dictionary<long, StructureStart> _starts = new();
    //每个 chunk 可能被多个跨 chunk 结构的 BoundingBox 覆盖故用 List 存引用
    private readonly Dictionary<long, List<StructureReference>> _references = new();

    //AllStarts 暴露所有已注册结构启动供外部查询与引用扫描使用
    public IEnumerable<StructureStart> AllStarts => _starts.Values;

    //HasStructureReferences 查询区块是否有结构引用
    public bool HasStructureReferences(ChunkPos pos)
        => _references.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var list) && list.Count > 0;

    //GetReferences 返回 chunk 的所有结构引用未命中返回空列表
    public IReadOnlyList<StructureReference> GetReferences(ChunkPos pos)
        => _references.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var list)
            ? list
            : Array.Empty<StructureReference>();

    //HasStructureStartsForChunk 查询区块是否有结构启动
    public bool HasStructureStartsForChunk(ChunkAccess chunk)
        => _starts.ContainsKey(ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z));

    //GetStructureStart 获取区块结构启动未命中返回 null
    public StructureStart? GetStructureStart(ChunkPos pos)
        => _starts.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var start) ? start : null;

    //AddStructureStart 添加结构启动到区块对应原版 addReference
    public void AddStructureStart(ChunkPos pos, StructureStart start)
        => _starts[ChunkPos.Pack(pos.X, pos.Z)] = start;

    //AddStructureReference 添加结构引用到区块
    public void AddStructureReference(ChunkPos pos, StructureReference reference)
    {
        Log.Debug($"AddStructureReference 入口 pos={pos} reference={reference.StructureId}");
        var key = ChunkPos.Pack(pos.X, pos.Z);
        if (!_references.TryGetValue(key, out var list))
        {
            list = new List<StructureReference>();
            _references[key] = list;
        }
        list.Add(reference);
        Log.Debug($"AddStructureReference 出口");
    }

    //CreateStarts 按 StructureSettings 查询当前 chunk 命中的结构并调 TryGenerate 生成
    //对应原版 StructureManager.createStarts 调 Structure.findGenerationPoint
    //返回生成的 start 数量0 表示该 chunk 无结构命中
    public int CreateStarts(ChunkAccess chunk, StructureSettings settings, long seed)
    {
        Log.Debug($"CreateStarts 入口 chunk={chunk.Pos} seed={seed}");
        var features = settings.GetFeaturesForChunk(chunk.Pos);
        var count = 0;
        Log.Debug($"步骤1 命中结构数={features.Count}");
        foreach (var feature in features)
        {
            var context = new StructureFeature.GenerationContext(chunk.Pos, seed);
            if (feature.TryGenerate(this, context))
                count++;
        }
        Log.Debug($"CreateStarts 出口 result={count}");
        return count;
    }

    //CollectReferences 扫描目标 chunk 周围 radius 半径内邻居的 starts
    //把 BoundingBox 与目标 chunk 相交的 start 记录为引用对应原版 collectReferences
    //radius=1 扫 3x3 邻居 radius=2 扫 5x5 依此类推
    public int CollectReferences(ChunkPos pos, int radius = 1)
    {
        Log.Debug($"CollectReferences 入口 pos={pos} radius={radius}");
        var targetBox = BoundingBoxInt.FromChunkPos(pos);
        var count = 0;
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                var neighborPos = new ChunkPos(pos.X + dx, pos.Z + dz);
                var key = ChunkPos.Pack(neighborPos.X, neighborPos.Z);
                if (!_starts.TryGetValue(key, out var start)) continue;
                if (!start.BoundingBox.Intersects(targetBox)) continue;
                AddStructureReference(pos, new StructureReference(start.StructureType, neighborPos));
                count++;
            }
        }
        Log.Debug($"CollectReferences 出口 result={count}");
        return count;
    }

    //CreateStructureCheck 占位返回 null对应原版 createStructureCheck
    public object? CreateStructureCheck() => null;
}

//StructureStart 结构启动对应原版 net.minecraft.world.level.levelgen.structure.StructureStart
//持有 structure 特征与 piece 列表 BoundingBox 由 pieces 推导
public sealed class StructureStart
{
    public StructureFeature Feature { get; }
    public Identifier StructureType => Feature.Id;
    public IReadOnlyList<StructurePiece> Pieces { get; }
    public BoundingBoxInt BoundingBox { get; }

    public StructureStart(StructureFeature feature, IReadOnlyList<StructurePiece> pieces)
    {
        Feature = feature;
        Pieces = pieces;
        BoundingBox = pieces.Count == 0
            ? new BoundingBoxInt(0, 0, 0, 0, 0, 0)
            : pieces[0].BoundingBox;
    }

    //LegacyConstructor 兼容旧测试直接传 Identifier 与 BoundingBox 占位
    public StructureStart(Identifier structureType, object? boundingBox = null)
        : this(new PlaceholderStructureFeature(structureType), Array.Empty<StructurePiece>()) { }
}

//StructureReference 结构引用占位对应原版结构引用数据
//持有引用 structure id 与目标 chunk pos真实接入需 StructureReference 子系统就绪
public sealed class StructureReference
{
    public Identifier StructureId { get; }
    public ChunkPos TargetChunk { get; }

    public StructureReference(Identifier structureId, ChunkPos targetChunk)
    {
        StructureId = structureId;
        TargetChunk = targetChunk;
    }
}

//PlaceholderStructureFeature 仅给 StructureStart 旧构造兼容测试场景用
internal sealed class PlaceholderStructureFeature : StructureFeature
{
    private readonly Identifier _id;
    public override Identifier Id => _id;
    public PlaceholderStructureFeature(Identifier id) => _id = id;
}
