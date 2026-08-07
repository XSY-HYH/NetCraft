using NetCraft.Registry.State;

namespace NetCraft.Registry;

//所有 stub 游戏类型，待具体子系统就绪后逐步迁移到对应项目

public interface Action { }
public interface Activity { }
public interface Advancement { }
public interface ArgumentTypeInfo<T1, T2> { }
public interface Attribute { }
public interface AttributeType<T1> { }
public interface BannerPattern { }
//BiomeSource 生物群系源接口对应原版 net.minecraft.world.level.biome.BiomeSource
//提供按坐标查询生物群系的能力stub 阶段仅定义方法签名
//具体实现 FixedBiomeSource/MultiNoiseBiomeSource 待 Game 层接入
public interface BiomeSource
{
    //GetBiome 按世界坐标查询生物群系占位签名待子接入具体逻辑
    Biome GetBiome(int x, int y, int z);
}
public interface BlockEntityType<T1> { }
public interface BlockPredicateType<T1> { }
public interface BlockStateProviderType<T1> { }
public interface CatSoundVariant { }
public interface CatVariant { }
public interface ChickenSoundVariant { }
public interface ChickenVariant { }
public interface ConfiguredFeature<T1, T2> { }
public interface ConfiguredWorldCarver<T1> { }
public interface ConsumeEffectType<T1> { }
public interface Consumer<T1> { }
public interface CowSoundVariant { }
public interface CowVariant { }
public interface CreativeModeTab { }
public interface CriterionTrigger<T1> { }
public interface DamageType { }
public interface DataComponentPredicateType<T1> { }
public interface DebugSubscription<T1> { }
public interface DecoratedPotPattern { }
public interface Dialog { }
public interface DialogBody { }
public interface DimensionType { }
public interface Enchantment { }
public interface EnchantmentEntityEffect { }
public interface EnchantmentLocationBasedEffect { }
public interface EnchantmentProvider { }
public interface EnchantmentValueEffect { }
public interface EntitySubPredicate { }
public interface EnvironmentAttribute<T1> { }
public interface Feature<T1> { }
public interface FeatureSizeType<T1> { }
public interface FlatLevelGeneratorPreset { }
public interface FloatProvider { }
public interface FoliagePlacerType<T1> { }
public interface FrogVariant { }
public interface GameEvent { }
public interface GameRule<T1> { }
public interface GameTestHelper { }
public interface GameTestInstance { }
public interface HeightProviderType<T1> { }
public interface IncomingRpcMethod<T1, T2> { }
public interface InputControl { }
public interface Instrument { }
public interface IntProvider { }
public interface JukeboxSong { }
public interface Level { }
public interface LevelBasedValue { }
public interface LevelStem { }
public interface LootItemCondition { }
public interface LootItemFunction { }
public interface LootPoolEntryContainer { }
public interface LootTable { }
public interface MapDecorationType { }
public interface MemoryModuleType<T1> { }
public interface MobEffect { }
public interface NbtProvider { }
public interface NumberFormatType<T1> { }
public interface NumberProvider { }
public interface OutgoingRpcMethod<T1, T2> { }
public interface PaintingVariant { }
public interface ParticleType<T1> { }
public interface Permission { }
public interface PermissionCheck { }
public interface PigSoundVariant { }
public interface PigVariant { }
public interface PlacedFeature { }
public interface PlacementModifierType<T1> { }
//PoiType 兴趣点类型接口对应原版 net.minecraft.world.entity.ai.village.poi.PoiType
//stub 升级为持 Identifier 的接口供 SimplePoiManager 索引
public interface PoiType { Identifier Id { get; } }
public interface PoolAliasBinding { }
public interface PositionSourceType<T1> { }
public interface PosRuleTestType<T1> { }
public interface Potion { }
public interface Recipe<T1> { }
public interface RecipeBookCategory { }
public interface RecipeDisplayType<T1> { }
public interface RecipeSerializer<T1> { }
public interface RecipeType<T1> { }
public interface RootPlacerType<T1> { }
public interface RuleBlockEntityModifierType<T1> { }
public interface RuleTestType<T1> { }
public interface ScoreboardNameProvider { }
public interface SensorType<T1> { }
public interface SlotDisplayType<T1> { }
public interface SlotSource { }
public interface SoundEvent { }
public interface SpawnCondition { }
public interface StatType<T1> { }
public interface Structure { }
public interface StructurePieceType { }
public interface StructurePlacementType<T1> { }
public interface StructurePoolElementType<T1> { }
public interface StructureProcessor { }
public interface StructureProcessorList { }
public interface StructureSet { }
public interface StructureTemplatePool { }
public interface StructureType<T1> { }
public interface SulfurCubeArchetype { }
public interface TestEnvironmentDefinition<T1> { }
public interface TicketType { }
public interface Timeline { }
public interface TradeSet { }
public interface TreeDecoratorType<T1> { }
public interface TrialSpawnerConfig { }
public interface TrimMaterial { }
public interface TrimPattern { }
public interface TrunkPlacerType<T1> { }
public interface VillagerProfession { }
public interface VillagerTrade { }
public interface VillagerType { }
public interface WolfSoundVariant { }
public interface WolfVariant { }
public interface WorldCarver<T1> { }
public interface WorldClock { }
public interface WorldPreset { }
public interface ZombieNautilusVariant { }
