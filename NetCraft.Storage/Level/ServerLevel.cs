using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ServerLevel 服务端关卡抽象类对应原版 net.minecraft.world.level.ServerLevel
//持有维度标识与关卡数据访问入口
//完整实现依赖实体系统/区块存储/调度器等子系统子类按需扩展
public abstract class ServerLevel
{
    //Dimension 维度注册名如下界/末地
    public abstract Identifier Dimension { get; }

    //DataVersion 关卡数据版本用于 DataFixer 升级判定
    public abstract int DataVersion { get; }

    //RegistryAccess 注册表访问入口用于 Codec 解析时查表
    //子类提供具体 RegistryAccess 实例对应原版 serverLevel.registryAccess()
    public abstract RegistryAccess RegistryAccess { get; }

    //GetChunk 按 ChunkPos 获取区块访问实例对应原版 getChunk
    //返回 null 表示区块未加载子类提供具体加载逻辑
    public abstract ChunkAccess? GetChunk(ChunkPos pos);
}
