# 更新记录 / Changelog

本项目所有重要变更记录在本文件中。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

All notable changes to this project are documented in this file. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning follows [Semantic Versioning](https://semver.org/).

当前处于 `0.1.x` 预发布阶段：里程碑 M1–M4 已达成，M5 进行中。版本号在 `Directory.Build.props` 的 `VersionPrefix` 中维护。

Currently in `0.1.x` pre-release: milestones M1–M4 reached, M5 in progress. Version is maintained in `VersionPrefix` of `Directory.Build.props`.

---

## [Unreleased]

尚未发版的开发中变更。下一次发版将进入 `0.2.0`。

Pending changes not yet released. Next release will be `0.2.0`.

### Added — 新增

#### 资源链路接通 / Resource pipeline wiring
- **`NetCraft.Resources/PreparableReloadListener.cs`**：资源重载监听器接口 + `ReloadContext` 上下文，同步签名适配启动期阻塞模型 / Synchronous reload listener interface + context
- **`NetCraft.Resources/SimpleReloadInstance.cs`**：同步重载调度器按注册顺序遍历 listener 调 `Reload` / Synchronous reload scheduler
- **`NetCraft.Resources/PackMetadataSection.cs`**：pack.mcmeta 元数据段解析（`pack_format` + `description`），description 允许聊天组件对象返回 `GetRawText` / pack.mcmeta metadata section parser
- **`NetCraft.Resources/ResourceManager`** 新增 `Reloaded` 事件 + `Reload()` 方法 + `ReloadEventArgs`，供 AddPack/RemovePack 后显式触发 Tags 等数据驱动重载 / Reloaded event + Reload method
- **`NetCraft.Bootstrap/TagsReloadListener.cs`**：包装 `Bootstrap.LoadBuiltinTags` + `TagManager.BindAll`，实现 `PreparableReloadListener`，每次 Reload 清空旧 loader 重扫 tag 文件 / Wraps LoadBuiltinTags + BindAll as a reload listener
- **`NetCraft.Game/ReloadableServerResources.cs`**：服务端可重载资源集合精简版，持 `ResourceManager` + `TagManager` + listeners，`LoadResources` 静态工厂注册 TagsReloadListener 并触发首次重载 / ReloadableServerResources minimal port
- **`NetCraft.Registry/BuiltInRegistries.CreateRegistryAccess()`**：反射收集所有 `Registry<T>` 静态字段构造不可变 `ImmutableRegistryAccess`，供 `ReloadableServerResources.LoadResources` 调 `TagManager.BindAll` 使用 / Reflective RegistryAccess factory
- **`NetCraft/AppPaths`** 新增 `DataDir`（`data/` 子目录）与 `DatapacksDir`（`datapacks/` 子目录）/ Added DataDir and DatapacksDir
- **`NetCraft.Game/AssetsExtractor.TryExtractJar`** 扩展同时提取 `assets/` 与 `data/` 前缀条目到各自目标目录，根 `pack.mcmeta` 复制到 `rootDir` 与 `assets`/`data` 同级，新增 `rootDir` 可选参数（null 时回退到 assetsDir 兼容旧行为）/ AssetsExtractor now extracts assets/ + data/ + root pack.mcmeta
- **`NetCraft.Game/ServerMain`** 与 **`NetCraft.Game/ClientMain`** 接入 `ResourceManager` + `ReloadableServerResources.LoadResources`：步骤 4.6 提取 jar 资源，步骤 4.7 构造 vanilla pack 并触发 Tags 数据驱动重载 / ServerMain and ClientMain wired to ResourceManager + LoadResources
- **`NetCraft.Game/Server/DedicatedServer`** 与 **`NetCraft.Game/Client/MinecraftClient`** 新增可选 `ReloadableServerResources?` 构造参数与 `ServerResources` 属性，为后续 `/reload` 命令重载 Tags/Recipes/Advancements 等铺路 / Optional rsr param on DedicatedServer and MinecraftClient
- **`NetCraft.Game/NetCraft.Game.csproj`** 新增 `NetCraft.Resources` + `NetCraft.Tags` ProjectReference / Game now references Resources and Tags

#### 测试 / Tests
- **`NetCraft.Test/Modules/ReloadableServerResourcesTests.cs`**：5 个用例覆盖 LoadResources 空 packs 不抛/TagsReloadListener 注册/Reload 重新触发/listeners 顺序/ReloadContext 序号 / 5 cases for ReloadableServerResources
- **`NetCraft.Test/Modules/PackMetadataSectionTests.cs`**：7 个用例覆盖 FromJson 标准/缺 description/缺 pack 节点/description 为对象/缺 pack_format/Reader 返回 null/Reader 从 StubPack 读取 / 7 cases for PackMetadataSection
- **`NetCraft.Test/Modules/AssetsExtractorTests.cs`**：5 个用例覆盖提取 assets+data+pack.mcmeta/跳过 class+META-INF/dataDir null 跳过 data/rootDir null 回退/jar 不存在返回 false / 5 cases for AssetsExtractor
- 全测试套件 814 → 831 条，0 失败 0 错误无回归 / Test suite 814 → 831, no regression

### Changed — 变更
- `NetCraft.Tags` 完成度 70% → 90%，TagsReloadListener 已接入 ReloadableServerResources 启动序列 / Tags 70% → 90%, TagsReloadListener wired to startup
- `NetCraft.Resources` 完成度 60% → 85%，新增重载框架（PreparableReloadListener/SimpleReloadInstance/PackMetadataSection/Reloaded 事件）/ Resources 60% → 85%, reload framework added
- `NetCraft.Game` 完成度 60% → 62%，启动流程接通资源链路 / Game 60% → 62%, resource pipeline wired to startup
- `NetCraft.Bootstrap` 完成度 75% → 80%，新增 TagsReloadListener.cs / Bootstrap 75% → 80%, TagsReloadListener added
- `NetCraft.Game/AssetsExtractor.Extract` 检测目标目录已存在且非空则跳过 jar 提取与音频复制，避免每次启动重复解压 / AssetsExtractor skips jar extraction and sound copy when target dir already non-empty

#### 文档 / Docs
- 仓库根 `README.md` 英文版项目入口 / English project entry at repo root `README.md`
- `docs/README.zh-CN.md` 中文版项目说明 / Chinese project description at `docs/README.zh-CN.md`
- 仓库根 `CHANGELOG.md` 提交更新记录（中英双语）/ Bilingual commit changelog at repo root `CHANGELOG.md`
- `NetCraft.Resources/Overview.md`/`NetCraft.Tags/Overview.md`/`NetCraft.Game/Overview.md` 更新完成度、文件清单、接通状态、测试段 / Module overviews updated
- `minecraft/子系统完成度与计划链.md` 矩阵更新 Resources/Tags/Game/Bootstrap 完成度与文件数 / Subsystem matrix updated

## [0.1.0] - 2026-08-03

首个内部里程碑版本，覆盖 M1–M4。

First internal milestone release, covering M1–M4.

### Added — 新增

#### 内核子系统 / Kernel subsystems
- **`NetCraft.Primitives`**：8 个值类型（`ChunkPos`/`BlockPos`/`SectionPos`/`QuartPos`/`GlobalPos`/`Vec3i`/`Vec3`/`Direction`），含 `asLong` 位运算 pack / 8 value types with `asLong` bit-packed encoding
- **`NetCraft.Config`**：`SharedConstants` 版本常量 + `Fixes`/`Optimizations`/`DebugFlags`，全部 `const` 编译期内联 / Compile-time `const` version flags and toggles
- **`NetCraft.Util`**：日志、`CrashReport`、`BitSet`、`Mth`、线程调度（`IExecutor`/`ConsecutiveExecutor`/`PriorityConsecutiveExecutor`）、`Xoroshiro128++`、`Profiler`/`MetricsRecorder` / Logging, crash reports, BitSet, math, executors, RNG, profiler
- **`NetCraft.Nbt`**：13 种 Tag、大端序、GZIP、流式 + 完整访问者、`NbtOps` 桥接 Codec、SNBT 文法解析（24/24 round-trip 字节级兼容 26.2）/ 13 tag types, big-endian, GZIP, streaming + full visitors, SNBT parser (24/24 byte-compatible round-trip with 26.2)
- **`NetCraft.Codec`**：`Codec`/`MapCodec`/`ScalarCodec`/`AbstractMapCodec`、`DataResult`/`Dynamic`/`OptionalDynamic`、`RecordCodecBuilder.Of2..Of4` / Codec framework with record builders
- **`NetCraft.DataFixer`**：DFU 阶段 A–E 全完成，HKT 模拟（`K1`/`K2`/`App`/`Kind1`）、Profunctor 光学体系、13 个 V1_21 Schema + 28 个 Fix（`v121fix` 端到端全绿）/ Full DFU stages A–E with HKT simulation, Profunctor optics, 13 V1_21 schemas + 28 fixes (v121fix end-to-end green)
- **`NetCraft.Storage`**：MCA 读写层、`SimpleBitStorage`/`ZeroBitStorage`、4 种 `PalettedContainer` 策略、`SerializableChunkData`、`ChunkSource`/`ChunkHolder`/`ChunkMap` 异步调度、`IOWorker` 三优先级抢占 / MCA, bit storage, 4 PalettedContainer strategies, async chunk pipeline, preemptive IOWorker
- **`NetCraft.Registry`**：`Identifier` 值类型、`ResourceKey<T>` per-T intern 池、`Holder<T>` 两阶段 `Direct`/`Reference` 绑定 / Identifier value type, interned ResourceKey, two-phase Holder binding
- **`NetCraft.Network`**：`ClientConnection`/`ServerConnection` 状态机、`Varint`/`Varlong` 编解码、包压缩 / Connection state machines, varint codecs, packet compression
- **`NetCraft.Commands`**：brigadier 完整移植（dispatcher、redirect、`ParsedCommandNode`），100% / Full brigadier port (dispatcher, redirect, parsed nodes), 100%

#### GPU / GUI
- **`NetCraft.Gpu`**：基于 Silk.NET 的 Vulkan 渲染器 / Vulkan renderer on Silk.NET
- **M1 PoC**：三角形 + 立方体 + GUI 在 Vulkan 上跑通 / Triangle + cube + GUI running on Vulkan
- **P1 渲染上下文**：CPU Pose 矩阵栈（`System.Numerics.Matrix3x2`）+ GPU 动态 Scissor（`vkCmdSetScissor`），Scissor 变化时按段分组绘制 / CPU Pose matrix stack + GPU dynamic Scissor, segment-grouped draws on Scissor change
- **P2 布局引擎**：`IGuiLayout` 接口 + `GuiLinearLayout`（水平/垂直堆叠、`Padding`、`Spacing`，跳过不可见子控件）/ IGuiLayout + GuiLinearLayout (horizontal/vertical stack, padding, spacing, skip invisible)
- **P3 多 `RenderPipeline` 切换**：反色管线 `GUI_INVERT`，fragment shader 对 RGB 取反保留 alpha，用于按钮按下等视觉反馈 / Inverted pipeline GUI_INVERT, RGB-inverting fragment shader for press feedback
- **P4 字体渲染增强**：`MeasureText`/`LineHeight` + `GuiTextAlign`（Left/Center/Right）/ Text measurement and alignment
- **P5 事件系统增强**：`TabStop`/`TabIndex` 属性 + `FocusNext` 方法实现 Tab 键焦点循环导航，深度遍历嵌套容器 / TabStop/TabIndex + FocusNext for cyclic focus navigation across nested containers
- **内核 GUI 业务穿透清理**：移除 `GuiWindow` 的 `EscPressed`/`F3Pressed`/`HotbarSlotSelected` 业务事件，改由 `VulkanGuiApp.RawKeyDown` 桥接 `MinecraftClient` → `ScreenManager.HandleRawKeyDown` 处理业务键识别 / Removed business events from kernel GuiWindow, bridged via RawKeyDown to ScreenManager

#### 业务层 / Game layer
- **`NetCraft.Game`**：方块、实体、物品、区块生成（`ChunkGenerator` 抽象类 + `BiomeSource`）、关卡（`ServerLevel`/`SimpleServerLevel`）、`MinecraftServer`/`MinecraftClient` 双模式主循环（窗口驱动 + Headless）、`DedicatedServer.Tick` 调度链接入 / Blocks, entities, items, chunk gen, levels, dual-mode main loop, DedicatedServer tick wiring
- **`NetCraft.Bootstrap`**：`BootstrapClass.bootStrap` 基础完成，`GameBootstrap.Bootstrap()` 注入 `NoiseGeneratorSettings` / Basic bootstrap, GameBootstrap noise settings injection
- **`NetCraft.Loader`**：CLI 启动器，jar `assets` 目录解压到程序根 `assets`，`--debug` 开启全局调试 / CLI launcher, jar assets extraction, --debug global flag
- **`NetCraft.TPGA`**：独立认证/代理服务 / Standalone auth/proxy service
  - 双端口：25565（Yggdrasil API）+ 25566（WSS/API）/ Dual ports: 25565 (Yggdrasil) + 25566 (WSS/API)
  - WSS 与 HTTPS 共用证书，证书路径为空时自动生成自签证书 / WSS/HTTPS share cert, auto self-signed fallback
  - API 请求需凭据认证，WSS 连接成功后生成 UUID 供后续 API 调用 / API auth required, WSS issues UUID after handshake
  - SQLite 主库（管理员 + api 账户）与玩家库（账户 + 令牌 + 档案 + 加入记录）物理隔离 / SQLite master/player DB split
  - 启动编排顺序：配置 → I18n → 日志 → DB → 证书+验证 → ASP.NET / Boot order: config → i18n → log → db → cert+auth → ASP.NET
  - webui 前端多入口（player/admin），构建脚本 `webui/build-frontend.ps1` 处理 `#` 路径 / Multi-entry webui, build script handles `#` in path

#### 测试 / Tests
- **`NetCraft.Test`**：814 个测试条目 / 814 test cases
  - 默认 802 条全过 / 802 passing by default
  - M4 端到端 TCP 握手 2/2 全过（默认跳过，显式指定时运行）/ M4 TCP handshake 2/2 (opt-in)
  - GPU 冒烟测试 10 条（默认跳过）/ GPU smoke tests 10 (opt-in)

### Changed — 变更
- 仓库远程地址重绑：`XSY-admin/NetCraft.git` → `XSY_xiaoqi/NetCraft.git` / Remote rebound to `XSY_xiaoqi`
- `git config http.sslVerify=false`（自签证书服务器）/ SSL verification disabled for self-signed cert server
- `Directory.Build.props`：全局启用 `Nullable enable`、`WarningsAsErrors` 覆盖 `CS8597/8601/8602/8603/8604/8625` / Nullable enabled, null-warning CS8597/8601/8602/8603/8604/8625 as errors

### Fixed — 修复
- **GUI 300 帧渲染崩溃（0xC0000005）** / 300-frame GUI render crash
  - 原因：每帧 Upload `DeviceLocal` vertex buffer 导致 GPU 同步阻塞、驱动状态累积崩溃 / Cause: per-frame DeviceLocal vertex buffer upload caused GPU sync stalls and driver state accumulation
  - 修复：改用 `HostVisible` 内存 + `map` + `memcpy` 直接更新，消除 staging 中转与 `QueueSubmit` 同步开销 / Fix: HostVisible memory + map + memcpy, removing staging transfer and QueueSubmit sync
- **`VulkanGuiApp.Dispose` 偶发 AV（0xC0000005）** / Occasional Dispose AV
  - 原因：`base.Dispose()` 销毁窗口后 `InputContext` 释放会访问已销毁的 GLFW 窗口句柄 / Cause: InputContext release accessed destroyed GLFW window handle
  - 修复：调整释放顺序为先 `_inputContext?.Dispose()` 再 `base.Dispose()` / Fix: dispose InputContext before base.Dispose
- **`FocusNext` 可空实参警告（CS8604）** / FocusNext nullable arg warning
  - 修复：`tabStops.IndexOf(_focusedControl)` → `_focusedControl is null ? -1 : tabStops.IndexOf(_focusedControl)` / Null-guard before IndexOf
- **`Matrix3x2.CreateRotationZ` 不存在** / Missing Matrix3x2.CreateRotationZ
  - 修复：2D 矩阵使用 `Matrix3x2.CreateRotation` 替代 / Use `CreateRotation` for 2D matrices

### Removed — 移除
- `NetCraft.TPGA/wwwroot/` 构建产物目录纳入 `.gitignore` / `NetCraft.TPGA/wwwroot/` build output added to `.gitignore`

## 早期提交 / Early commits

| Commit | 日期 / Date | 说明 / Description |
|---|---|---|
| `41f342c` | 2026-08-02 | Initial commit（仓库初始化 / repo init） |
| `0c87556` | 2026-08-03 | 初始提交：NetCraft 内核重写项目（M1–M4 全量代码导入 / M1–M4 full code import） |
| `753d906` | 2026-08-03 | Merge remote-tracking branch 'origin/main' |
| `cfc67bd` | 2026-08-03 | 忽略 `NetCraft.TPGA/wwwroot` 构建产物 / Ignore `NetCraft.TPGA/wwwroot` build output |
| `83e55e3` | 2026-08-03 | GUI 体系完善：P1 渲染上下文 + P2 布局引擎 + P3 反色管线 + P4 文本对齐 + P5 Tab 焦点导航，修复 300 帧崩溃与 Dispose AV / GUI polish: P1 render context + P2 layout + P3 inverted pipeline + P4 text alignment + P5 Tab focus nav, fix 300-frame crash and Dispose AV |
