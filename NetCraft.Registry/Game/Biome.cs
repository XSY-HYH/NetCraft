using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//Biome 抽象基类对应原版 net.minecraft.world.level.biome.Biome
//原版持有 ClimateSettings/SpecialEffects/MobSpawnSettings 等属性
//此处简化为抽象类子类按需扩展
//CODEC 注册到 BuiltInRegistries.BIOME 持有 HasPrecipitation/Temperature/Downfall/Category 字段
public abstract class Biome
{
    //Id 生物群系的注册名子类必须实现
    public abstract Identifier Id { get; }

    //HasPrecipitation 是否有降水对应原版 Biome.hasPrecipitation
    public virtual bool HasPrecipitation => false;

    //Temperature 温度对应原版 Biome.temperature 默认 0.5
    public virtual float Temperature => 0.5f;

    //Downfall 降雨量对应原版 Biome.downfall 默认 0
    public virtual float Downfall => 0f;

    //Category 类别对应原版 Biome.BiomeCategory 默认 none
    public virtual string Category => "none";

    //BiomeCodec 用 dispatch 按 id 查注册表对应原版 Biome.CODEC
    //encode 把 Biome 写为 StringTag(Identifier)decode 从 BuiltInRegistries.BIOME 查表
    //BIOME 是 DefaultedRegistry 找不到的 id 返回 plains 默认值
    public static readonly Codec<Biome> Codec = IdentifierCodec.Instance.ComapFlatMap(
        id => DataResult<Biome>.Success(BuiltInRegistries.BIOME.GetValue(id)!),
        biome => biome.Id);

    //NetworkCodec 网络编解码用 VarInt id 查注册表对应原版 Biome.NETWORK_CODEC
    //简化为复用 Codec 用 Identifier 字符串序列化真实网络层用 VarInt
    public static Codec<Biome> NetworkCodec => Codec;
}

