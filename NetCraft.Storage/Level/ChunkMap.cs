using System.Collections.Generic;

namespace NetCraft.Storage;

//ChunkMap 玩家视距管理对应原版 net.minecraft.server.level.ChunkMap
//阶段 11.48 简化实现仅追踪玩家中心 chunk 与视距计算
//完整原版会做增量 holder ticket 更新与玩家追踪此处走 stub
public sealed class ChunkMap
{
    private readonly int _viewDistance;
    private int _centerX = int.MinValue;
    private int _centerZ = int.MinValue;

    //ViewDistance 玩家视距半径单位 chunk
    public int ViewDistance => _viewDistance;

    //CenterX 当前中心 chunkX
    public int CenterX => _centerX;

    //CenterZ 当前中心 chunkZ
    public int CenterZ => _centerZ;

    public ChunkMap(int viewDistance)
    {
        //视距下限 3 上限 32 对齐原版 server-view-distance
        _viewDistance = Math.Clamp(viewDistance, 3, 32);
    }

    //UpdatePlayerPos 更新玩家中心 chunk 返回视距内所有 chunk 坐标对应原版 move 目标集
    //调用方拿到列表后对每个 chunk 调 holder.UpdateTicketLevel 提升 ticket
    public IEnumerable<(int x, int z)> UpdatePlayerPos(int chunkX, int chunkZ)
    {
        _centerX = chunkX;
        _centerZ = chunkZ;
        for (int dx = -_viewDistance; dx <= _viewDistance; dx++)
            for (int dz = -_viewDistance; dz <= _viewDistance; dz++)
                yield return (chunkX + dx, chunkZ + dz);
    }

    //InViewDistance 判断 chunk 是否在玩家视距内对应原版 checkerboardDistance
    public bool InViewDistance(int chunkX, int chunkZ)
    {
        if (_centerX == int.MinValue) return false;
        var dx = chunkX - _centerX;
        var dz = chunkZ - _centerZ;
        return dx * dx + dz * dz <= _viewDistance * _viewDistance;
    }
}
