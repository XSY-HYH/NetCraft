# NetCraft.Test

> **完成度**：100%（814 个测试条目，默认跳过 GUI/TCP 后 802 全过）· **阶段**：持续维护 · **依赖**：全部子库 · **计划链位置**：测试基础设施，CI 集成

统一测试控制台项目。聚合所有子库的测试用例，支持命令行参数按模块或测试名筛选运行。

跨平台（net10.0，无平台特定 API）。已加入 slnx。

GUI 测试（gpugui，需 Vulkan 驱动+窗口）与 TCP 测试（networktcp，占用本地端口）默认跳过，需显式指定模块名或 `all` 参数触发。

## 用法

```powershell
# 运行全部测试
dotnet run --project NetCraft.Test

# 仅运行 NBT 模块
dotnet run --project NetCraft.Test -- nbt

# 仅运行 Registry 模块
dotnet run --project NetCraft.Test -- registry

# 多模块同时运行
dotnet run --project NetCraft.Test -- nbt registry

# 按测试名片段筛选（大小写不敏感）
dotnet run --project NetCraft.Test -- ByteTag

# 列出所有测试条目
dotnet run --project NetCraft.Test -- list
```

参数无需 `--` 前缀，直接给模块名或测试名片段。多个参数为 OR 关系，任一匹配即运行。无参数 = 全部。

参数匹配规则：若参数匹配某模块名则运行该模块全部测试，否则按测试名包含匹配。

退出码：0 = 全部通过，1 = 存在失败/错误或无匹配测试。

## 实现

- 统一测试入口，整合所有子库测试
- 按模块组织，新增子库测试只需在 Modules 下加文件并注册到 Program.cs
- 返回 0/1 退出码，便于 CI 集成

## 文件清单（47 个 cs）

| 文件 | 用途 |
|------|------|
| `Program.cs` | 入口，参数解析 + 测试运行器，收集各模块并按参数筛选，IsExclusiveModule 标记 GUI/TCP 模块默认跳过 |

Modules/ 下 45 个测试文件按子库分组，阶段 11.45 新增 storage 模块 4 个用例覆盖 DirectoryLock/LevelStorageAccess/PersistentServerLevel round-trip/load missing，阶段 11.46 新增 server 模块 3 个用例覆盖 DedicatedServer 接入 PersistentServerLevel/Run+Stop 刷盘/tick 间隔控制，阶段 11.47 新增 server 模块 3 个用例覆盖 DedicatedServer tick 推进 Overworld.LevelTick/PersistentServerLevel tick 推进 Entity/DedicatedServer tick 处理玩家 Connection，全测试套件 736/736 全过。

### 基础内核测试（10 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| NbtTests | 13 种 Tag round-trip + GZIP 压缩 + 流式访问者 + Modified UTF-8 + NbtUtils |
| SnbtTests | SNBT 字符串解析 + LenientCodec + CompoundTag.Codec |
| CodecTests | Codec 框架 + MapCodec + dispatch + Optional + Pair/List |
| PrimitivesTests | Identifier/ResourceKey/基本值类型 |
| CollectionTests | 集合工具 + LRU 缓存 |
| RandomTests | 伪随机数生成器 |
| ProfilingTests | 性能采样器 |
| OptimizationsTests | 优化点开关 + FrozenDictionary 索引 |
| ConfigTests | 配置加载 + YAML 解析 |
| InteropTests | 跨平台互操作 |

### Resources 测试（1 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| ResourcesTests | ResourceManager/FolderPackResources/VanillaPackResources |

### Registry/State 测试（4 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| RegistryTests | Identifier/ResourceKey/MappedRegistry/DefaultedMappedRegistry/RegistryAccess/BuiltInRegistries/TagKey + State 框架 |
| RegistryOpsTests | RegistryOps 装饰器 + Codec 解析时注册表查询 |
| BootstrapTagsTests | 标签 bootstrap 加载 |
| TagsTests | Tag 系统 + NamedHolderSet |

### Storage 测试（10 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| StorageTests | RegionFile/RegionFileStorage/RegionBitmap/SerializableChunkData/DirectoryLock/PersistentServerLevel |
| PalettedTests | PalettedContainer/Palette/CrudeIncrementalIntIdentityHashBiMap/SingleValuePalette |
| IOWorkerTests | 异步 IO 调度器 + 优先级抢占 |
| SimpleRegionStorageTests | 通用区域存储入口 |
| LevelStorageTests | LevelStorage/LevelStorageAccess + 维度路径 |
| EntityStorageTests | 实体存储 + 区块实体聚合 |
| SavedDataStorageTests | 维度数据存储 |
| TagValueIOTests | TagValueInput/TagValueOutput + ValueIO 适配 |
| ChunkAccessTests | ChunkAccess/ProtoChunk + 区段访问 |
| HeightmapTests | 高度图类型 + QueueTracker |

### Game 业务测试（11 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| BlockTests | ConcreteBlock/BlockBehaviour + 方块注册 |
| EntityTests | Entity/Player/Mob/EntityTypes |
| NoiseTests | ImprovedNoise/PerlinNoise/SimplexNoise/NormalNoise |
| DensityFunctionTests | 10 个 DensityFunction 子类 + dispatch codec 往返 |
| WorldGenTests | ProtoChunk/NoiseChunk/Aquifer/FillFromNoise/SurfaceSystem/WorldGenRegion/LevelChunk/Climate/MultiNoiseBiomeSource/ChunkStatus 流水线 |
| SurfaceRulesTests | SurfaceRules 条件源/规则源 + BlockStateRule |
| LevelChunkSerializerTests | LevelChunkSerializer Write/Read 往返 + LightData 往返 |
| ServerTests | MinecraftServer/DedicatedServer/MinecraftClient 主循环 + tick 间隔控制 + DedicatedServer 接入 PersistentServerLevel + tick 调度 Connection/Level/Entity |
| DataComponentsTests | DataComponentMap + 组件注册 + FilterMask 过滤 |
| ItemStackPacketTests | ItemStack.OptionalStreamCodec + 6 个 Play 包端到端往返 |
| MenuTypePacketTests | MenuType.StreamCodec + ClientboundOpenScreenPacket 端到端 |

### Network 测试（3 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| NetworkTests | StreamCodec + FriendlyByteBuf + Connection |
| NetworkHandshakeTests | Handshake/Status/Login 协议包（内存管道端到端） |
| NetworkTcpTests | 真实 TCP 握手端到端（TcpListener/TcpClient，默认跳过需显式 `networktcp` 触发） |

### DFU 测试（2 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| DataFixerTests | DFU 框架 + Schema + Fix 注册 |
| V1_21FixEndToEndTests | V1_21 升级链端到端 + MemoryExpiryDataFix |

### Component/Chat 测试（1 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| ComponentTests | Component 文本组件 + 50 个用例 |

### Gpu 测试（2 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| BootstrapGpuTests | GPU bootstrap + Vulkan 实例 |
| GpuTests / GpuGuiTests | Vulkan 渲染 + GUI 渲染器 |
| GuiLogicTests | GUI 控件纯逻辑（Update 派发链路不依赖 Vulkan） |

### Commands 测试（1 个模块）

| 模块 | 覆盖路径 |
|------|---------|
| CommandsTests | 命令解析 + 调度 |

## 测试统计

- 47 个测试模块，814 个测试条目
- 默认套件 802/802 全过无回归（跳过 GUI/TCP 模块）
- TCP 端到端握手 2/2 全过（`dotnet run -- networktcp`）
- 单模块运行示例：`dotnet run --project NetCraft.Test -- registry`（45 个用例）

合并自已删除的 `NetCraft.Nbt.Tests` 和 `NetCraft.Registry.Tests`。
