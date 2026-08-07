using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceSystem 表面构建系统对应原版 net.minecraft.world.level.levelgen.SurfaceSystem
//阶段 E 接入 SurfaceRules.RuleSource 真实规则派发
//无 RuleSource 时走默认 GrassBlock/Dirt 替换规则占位
public sealed class SurfaceSystem
{
    private readonly SurfaceRules.RuleSource? _rule;
    private readonly int _seaLevel;
    private readonly BlockState _grassState;
    private readonly BlockState _dirtState;
    private readonly BlockState _stoneState;
    private readonly BlockState _airState;
    private readonly Biome _defaultBiome;

    public SurfaceSystem() : this(null, 63) { }

    //SurfaceSystem 构造接收可选规则源与海平面
    //rule 为 null 时走默认 GrassBlock/Dirt 替换逻辑
    public SurfaceSystem(SurfaceRules.RuleSource? rule, int seaLevel)
    {
        _rule = rule;
        _seaLevel = seaLevel;
        _grassState = Blocks.GRASS_BLOCK.DefaultBlockState;
        _dirtState = Blocks.DIRT.DefaultBlockState;
        _stoneState = Blocks.STONE.DefaultBlockState;
        _airState = Blocks.AIR.DefaultBlockState;
        _defaultBiome = new PlainsBiome();
    }

    //BuildSurface 应用表面规则到区块对应原版 SurfaceSystem.buildSurface
    //有 RuleSource 时按 SurfaceRules 派发到每个 (x, y, z)
    //无 RuleSource 时走默认 GrassBlock/Dirt 替换逻辑
    public void BuildSurface(ChunkAccess chunk, RandomSource random)
    {
        if (chunk is not ProtoChunk proto)
            return;

        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                if (_rule is null)
                    ApplyDefaultColumn(proto, localX, localZ);
                else
                    ApplyRuleColumn(proto, localX, localZ);
            }
        }
    }

    //ApplyDefaultColumn 默认表面规则无 RuleSource 时使用
    //从上往下扫第一个非空气方块作为地表改为 GrassBlock往下 3 格改 Dirt 其余保持
    private void ApplyDefaultColumn(ProtoChunk proto, int localX, int localZ)
    {
        var surfaceY = FindSurfaceY(proto, localX, localZ);
        if (surfaceY == int.MinValue) return;

        SetBlockAt(proto, localX, surfaceY, localZ, _grassState);
        for (var dy = 1; dy <= 3; dy++)
            SetBlockAt(proto, localX, surfaceY - dy, localZ, _dirtState);
    }

    //ApplyRuleColumn 按 SurfaceRules 派发到该列所有方块
    //每个方块构建 Context 后调用 RuleSource.Apply 决定是否替换
    private void ApplyRuleColumn(ProtoChunk proto, int localX, int localZ)
    {
        var surfaceY = FindSurfaceY(proto, localX, localZ);
        if (surfaceY == int.MinValue) return;

        var worldX = proto.Pos.X * 16 + localX;
        var worldZ = proto.Pos.Z * 16 + localZ;
        var biome = _defaultBiome;

        for (var sectionIdx = 0; sectionIdx < proto.SectionsCount; sectionIdx++)
        {
            var sectionY = proto.MinSectionY + sectionIdx;
            var section = proto.GetSection(sectionY);
            if (section is null) continue;

            for (var localY = 0; localY < 16; localY++)
            {
                var worldY = sectionY * 16 + localY;
                if (worldY > surfaceY + 4) continue;

                var current = section.GetBlockState(localX, localY, localZ);
                if (current == _airState) continue;

                var stoneDepthAbove = Math.Max(0, surfaceY - worldY);
                var ctx = new SurfaceRules.Context(
                    worldX, worldY, worldZ, biome,
                    stoneDepthAbove, surfaceY, _seaLevel);

                var next = _rule!.Apply(ctx, current);
                if (next is not null && next != current)
                    section.SetBlockState(localX, localY, localZ, next.Value);
            }
        }
    }

    //FindSurfaceY 从上往下扫第一个非空气方块作为地表
    //返回 int.MinValue 表示该列全空
    private int FindSurfaceY(ProtoChunk proto, int localX, int localZ)
    {
        for (var sectionIdx = proto.SectionsCount - 1; sectionIdx >= 0; sectionIdx--)
        {
            var sectionY = proto.MinSectionY + sectionIdx;
            var section = proto.GetSection(sectionY);
            if (section is null) continue;
            for (var localY = 15; localY >= 0; localY--)
            {
                var state = section.GetBlockState(localX, localY, localZ);
                if (state != _airState)
                    return sectionY * 16 + localY;
            }
        }
        return int.MinValue;
    }

    //SetBlockAt 按世界 Y 坐标写入方块到对应区段
    private static void SetBlockAt(ProtoChunk proto, int localX, int worldY, int localZ, BlockState state)
    {
        var sectionY = Math.DivRem(worldY, 16, out var localY);
        if (sectionY < proto.MinSectionY || sectionY >= proto.MinSectionY + proto.SectionsCount)
            return;
        proto.SetBlockState(sectionY, localX, localY, localZ, state);
    }
}
