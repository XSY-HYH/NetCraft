# NetCraft.Bootstrap

> **完成度**：75% · **阶段**：基础引导完成，完整 TagLoader 绑定待补 · **依赖**：NetCraft.Registry + NetCraft.Resources · **计划链位置**：启动入口前置，对应原版 Bootstrap.java

引导子库。承载内核启动前的早期初始化逻辑。

对应原版 `net.minecraft.server.Bootstrap` 类。

## 实现

`Bootstrap` 静态类提供 `BootStrap` 入口、`ValidateRegistries` 注册表校验、`LoadBuiltinTags` 标签扫描三个能力。静态构造器调用 `Log.SetClassSource` 设置日志源，覆盖独立调用 `ValidateRegistries`/`LoadBuiltinTags` 不经主入口的场景。

`BootStrap` 幂等：首次调用触发 `BuiltInRegistries.BootStrap` + `ValidateRegistries`，再次调用直接返回。`Reset` 仅供测试隔离用。

`ValidateRegistries` 用反射遍历 `BuiltInRegistries` 所有 `Registry<T>` 字段，对每个调用 `Freeze` 并读取 `Size` 属性统计注册总数。用反射而非直接调用是为绕开 C# 泛型不变性（`Registry<Block>` 无法强转为 `Registry<object>`）。

`LoadBuiltinTags` 扫描 `ResourceManager` 中 `PackType.ServerData` 下所有 `tags` 目录的资源，统计文件数。完整 `TagLoader` 绑定需具体注册表就绪后逐项接通。

## 文件清单（1 个 cs）

| 文件 | 用途 |
|------|------|
| `Bootstrap.cs` | 引导静态类 `BootStrap` 入口 + `ValidateRegistries` 注册表校验 + `LoadBuiltinTags` 标签扫描 |

## 规划

- `LoadBuiltinTags` 接通 `TagLoader`：绑定具体注册表逐 category 加载标签
- `ValidateRegistries` 扩展 `DefaultedRegistry` 默认值存在性校验
