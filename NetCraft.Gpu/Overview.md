# NetCraft.Gpu

> **完成度**：M1 PoC 已达成（三角形+立方体+GUI）· P1 GUI 渲染上下文已达成（Pose 矩阵栈+Scissor 裁剪栈+段分组绘制+HostVisible vertex buffer 优化）· P2 布局引擎已达成（LinearLayout 水平/垂直堆叠+Padding/Spacing）· P3 多 RenderPipeline 切换已达成（反色管线 GUI_INVERT）· P4 字体渲染增强已达成（文本对齐 MeasureText/LineHeight+GuiTextAlign+CJK 已支持）· P5 事件系统增强已达成（Tab 焦点导航 TabStop/TabIndex+FocusNext）· **阶段**：M1+P1+P2+P3+P4+P5 已完成，方块/实体渲染待补 · **依赖**：Silk.NET Vulkan + GLFW · **计划链位置**：Layer 4 渲染原语层，对应原版 blaze3d/ (164 文件) 剔除 opengl

Layer 4 子库，渲染原语层。基于 Vulkan/Silk.NET 提供跨平台 GPU 渲染后端，包含 GPU 资源抽象接口与 Vulkan 具体实现、pipeline 声明式系统、纹理图集拼接算法、BakedModel/BakedQuad 纯数据容器等渲染原语，并附带 GUI 控件系统用于测试与调试可视化。

对应原版 blaze3d 渲染层的底层抽象。

## 架构边界

- 跨平台硬约束，禁止使用 DirectX/Metal 等平台特定 API
- 顶层定义抽象接口（GpuContext/GpuDevice/GpuBuffer 等），Vulkan 子目录提供具体实现
- EmptyGpuContext 提供空实现供无 GPU 环境或单元测试使用
- Gui 子目录是 GUI 控件系统，配合 VulkanGuiRenderer 渲染调试界面
- 定位为渲染原语层，可按需依赖其他 NetCraft.* 模块（如 Registry），但不承载业务逻辑（方块模型 JSON 解析、BlockState→模型映射等依赖 ResourceManager/BlockStateRegistry 的业务逻辑放 NetCraft.Game/Client/Render/）

## 实现

### GPU 抽象接口（根目录 10 个 cs）

- `GpuBackend` 后端枚举
- `GpuContext`/`GpuDevice` 上下文与设备抽象
- `GpuBuffer`/`GpuImage` GPU 资源抽象
- `GpuCommandBuffer` 命令缓冲抽象，含 `BindPipeline` 方法支持同一 RenderPass 内切换 graphics pipeline，阶段 P1 加 `SetScissor` 抽象支持动态裁剪
- `GpuDescriptorSet` 描述符集抽象
- `GpuShader` 着色器抽象
- `RenderPipeline` 渲染管线抽象
- `EmptyGpuContext`/`EmptyGpuCommandBuffer` 空实现

### Vulkan 实现（Vulkan/ 14 个 cs）

- `VulkanGpuContext`/`VulkanGpuDevice` Vulkan 上下文与设备
- `VulkanBuffer` 缓冲实现，按 Usage 选择内存属性：UniformBuffer 走 HostVisible 直接 map+memcpy，VertexBuffer/IndexBuffer 走 DeviceLocal 通过 staging 中转；加 hostVisible 重载支持 GUI vertex buffer 每帧 map+memcpy 避免 staging 的 QueueSubmit 同步开销
- `VulkanImage` 图像实现，CreateView 按 Usage 选 aspectMask，Upload 支持深度图布局转换
- `VulkanCommandBuffer` 命令缓冲实现，含 BeginRenderPass 深度附件 clear value 处理，阶段 P1 加 `SetScissor` 调 `vkCmdSetScissor` clamp 非负宽高
- `VulkanRenderPipeline` 渲染管线实现，CreateRenderPass 支持 depthTest 参数追加 D32Sfloat 深度附件，CreateGraphicsPipeline 含 DepthStencilState，阶段 P1 支持 `DynamicScissorEnabled` 加 `VK_DYNAMIC_STATE_SCISSOR`
- `VulkanDescriptorSet` 描述符集实现
- `VulkanShader` 着色器实现
- `SpirvShaders` 内嵌 SPIR-V 字节码
- `VulkanAppBase` Vulkan 应用公共抽象基类，封装 swapchain/imageview/framebuffer/sync/DrawFrame/Cleanup 全部通用逻辑。MaxFramesInFlight=2 双帧并行，监听窗口 Resize 事件触发 RecreateSwapchain，阶段 11.49 加 FrameUpdate 事件挂 _window.Update 在 Render 前触发，加 OnInitialized 钩子供子类订阅输入，加 RequestClose 供外部触发窗口关闭
- `VulkanTriangleApp` 三角形渲染 PoC，继承 VulkanAppBase
- `VulkanCubeApp` 3D 立方体渲染示例，36 顶点+36 索引+MVP uniform buffer，验证深度测试+MVP uniform+索引绘制完整链路
- `VulkanGuiApp` GUI 应用示例，继承 VulkanAppBase，阶段 11.49 override OnInitialized 调 _window.CreateInput 订阅 Mice/Keyboards 事件入 ConcurrentQueue，暴露 PollInput 把队列派发到 GuiWindow.ProcessMouseDown/ProcessKeyDown
- `VulkanGuiRenderer` GUI 渲染器，四管线（矩形+文字+图像+反色）共享同一 RenderPass 在 BeginRenderPass 内切换，文字 pipeline 采样 FontAtlas。阶段 P1 加 Pose 矩阵栈（CPU 端 `System.Numerics.Matrix3x2` 变换 4 顶点）+ Scissor 裁剪栈（GPU `vkCmdSetScissor` 动态状态）+ 段分组绘制（Scissor 变化时开新段，同段 quad/text 合并上传、`DrawIndexed(firstIndex)` 偏移绘制）；vertex buffer 改用 HostVisible 内存每帧 map+memcpy 避免 staging 的 QueueSubmit+QueueWaitIdle 同步开销（300 帧崩溃根因修复，RenderPass 内 image Upload 安全）；阶段 P3 加反色管线（fragment shader 对 RGB 取反保留 alpha），`DrawQuadInverted` 走独立顶点缓冲复用 quad 索引，对应原版 `RenderPipelines.GUI_INVERT`

### GUI 控件系统（Gui/ 7 个 cs）

- `FontAtlas` TTF 加载与 ASCII 32-126 + CJK U+4E00..U+9FFF 字符光栅化到 2048×2048 R8 atlas，跨平台中文字体查找，字符回退到'?'
- `GuiCommon` 公共定义，阶段 P1 扩展 `IGuiRenderContext` 加 `PushPose/PopPose/PushScissor/PopScissor` 4 个栈接口；阶段 P3 加 `DrawQuadInverted` 反色矩形接口对应原版 GUI_INVERT；阶段 P4 加 `MeasureText/LineHeight` 文本测量接口 + `GuiTextAlign` 枚举
- `GuiContainer` 容器控件，override Update 遍历可见子控件调用 Update；阶段 P1 `Render` 自动 `PushScissor/PopScissor` 包裹子控件裁剪超出边界；阶段 P2 加 `Layout` 属性 Update 前调 `Measure` 排列子控件
- `GuiControl` 控件基类，阶段 11.52 加 `virtual void Update(double delta)` 默认空实现供子类重写推进动画状态；阶段 P5 加 `TabStop/TabIndex` 属性参与 Tab 焦点导航
- `GuiControls` 具体控件（Button/TextBox/Panel 等），阶段 P4 GuiLabel 加 `TextAlign` 属性按 MeasureText 计算对齐偏移
- `GuiLayout` 布局引擎，阶段 P2 加 `IGuiLayout` 接口 + `GuiLinearLayout` 线性布局（水平/垂直堆叠，Padding 内边距+Spacing 间距），对应原版 client.gui.layouts.LinearLayout
- `GuiWindow` 顶层窗口，继承 GuiContainer.Update 自动派发到子控件；阶段 P5 加 `FocusNext` 方法 Tab 键按 TabIndex 循环切换 TabStop 控件焦点

## 文件清单（31 个 cs）

### 根目录（10 个）

| 文件 | 用途 |
|------|------|
| `GpuBackend.cs` | 后端枚举 |
| `GpuContext.cs` | GPU 上下文抽象 |
| `GpuDevice.cs` | GPU 设备抽象 |
| `GpuBuffer.cs` | GPU 缓冲抽象 |
| `GpuImage.cs` | GPU 图像抽象 |
| `GpuCommandBuffer.cs` | 命令缓冲抽象含 BindPipeline |
| `GpuDescriptorSet.cs` | 描述符集抽象 |
| `GpuShader.cs` | 着色器抽象 |
| `RenderPipeline.cs` | 渲染管线抽象 |
| `EmptyGpuContext.cs` | 空实现供无 GPU 环境用 |

### Vulkan 子目录（14 个）

| 文件 | 用途 |
|------|------|
| `VulkanGpuContext.cs` | Vulkan 上下文实现 |
| `VulkanGpuDevice.cs` | Vulkan 设备实现 |
| `VulkanBuffer.cs` | 缓冲实现按内存分类 |
| `VulkanImage.cs` | 图像实现含深度图支持 |
| `VulkanCommandBuffer.cs` | 命令缓冲实现含深度 clear |
| `VulkanRenderPipeline.cs` | 渲染管线含深度测试 |
| `VulkanDescriptorSet.cs` | 描述符集实现 |
| `VulkanShader.cs` | 着色器实现 |
| `SpirvShaders.cs` | 内嵌 SPIR-V 字节码 |
| `VulkanAppBase.cs` | 应用公共基类封装 swapchain/sync + FrameUpdate 事件/OnInitialized/RequestClose |
| `VulkanTriangleApp.cs` | 三角形 PoC |
| `VulkanCubeApp.cs` | 3D 立方体示例 |
| `VulkanGuiApp.cs` | GUI 应用示例 + Input 订阅 + PollInput |
| `VulkanGuiRenderer.cs` | GUI 渲染器双管线 |

### Gui 子目录（7 个）

| 文件 | 用途 |
|------|------|
| `FontAtlas.cs` | TTF 光栅化到 R8 atlas |
| `GuiCommon.cs` | 公共定义 |
| `GuiContainer.cs` | 容器控件 + Layout 属性 |
| `GuiControl.cs` | 控件基类 |
| `GuiControls.cs` | 具体控件 Button/TextBox/Panel |
| `GuiLayout.cs` | 布局引擎 IGuiLayout + GuiLinearLayout |
| `GuiWindow.cs` | 顶层窗口 |

## 关键设计决策

- VulkanAppBase 抽象基类持有 swapchain/framebuffer/sync 全部状态，子类仅重写 WindowTitle/GetFramebufferRenderPass/OnCreatePipelineResources/OnRecordCommandBuffer/OnSwapchainRecreated
- swapchain 重建由窗口 Resize 事件驱动，DrawFrame 检测 _framebufferResized 或 QueuePresentKHR 返回 SuboptimalKhr/ErrorOutOfDateKhr 后调 RecreateSwapchain
- 深度附件用 D32Sfloat 单通道，MVP 立方体只需 depth 不需 stencil
- RenderPass 共享实例：VulkanGuiApp 矩形 pipeline 创建 RenderPass 文本 pipeline 通过 sharedRenderPass 参数共享，Dispose 时不销毁
- VulkanBuffer 内存分类对齐 Vulkan 最佳实践：UniformBuffer 高频 CPU 写走 HostVisible，VertexBuffer/IndexBuffer 一次创建只读走 DeviceLocal+staging；GUI 每帧更新的 vertex buffer 用 HostVisible 重载避免 staging 中转的 QueueSubmit 同步开销（RenderPass 内 Upload 安全因 map+memcpy 不涉及提交）

## 验证

- 0 编译错误
- `dotnet run -- gpugui` 跑 12/12 全过：Vulkan 三角形/立方体/GUI 各 60+300 帧稳定性 + 控件交互 4 个（Button click/TextBox input/Panel children/Button hover）+ 嵌套 scissor 300 帧 + 反色管线 60 帧
- `dotnet run -- guilogic` 跑 34/34 全过：控件 Update 派发 + ScreenManager 生命周期 + Slider/Checkbox 交互 + Scissor/Pose 栈 + LinearLayout 布局 4 个 + DrawQuadInverted 计数 + 文本对齐 2 个 + Tab 焦点导航 2 个

## 规划

- 方块/实体渲染（基于深度测试+MVP 链路扩展）
- 多 RenderPass 与离屏渲染
- GPU 资源池化与生命周期管理
- 着色器热加载
- 多后端抽象（未来 DX/Metal 后端复用 GpuCommandBuffer.BindPipeline 等接口）

## 原版 Minecraft 26.2 GUI 架构分析

### 三层分层

| 层 | 包路径 | 职责 | NetCraft 对应 |
|----|--------|------|---------------|
| 渲染内核层 | `com.mojang.blaze3d.*` | GPU 资源抽象+渲染状态管理+窗口+字形数据结构 | NetCraft.Gpu 根 + Vulkan/ |
| 业务 GUI 框架层 | `net.minecraft.client.gui.*` | 控件框架+事件+布局+字体渲染+屏幕+Tooltip/Narration | NetCraft.Gpu/Gui/ + NetCraft.Game/Gui/（当前混层） |
| 世界渲染层 | `net.minecraft.client.renderer.*` | 实体/方块/世界渲染，与 GUI 关联度低 | 待补 |

### blaze3d 渲染内核层职责

仅提供 GUI 渲染所需的底层基础，**不含任何控件管理、事件系统、布局引擎**：

- `RenderSystem`：scissor/projection/blend 等全局渲染状态控制
- `Window`：窗口与表面尺寸（guiScaledWidth/guiScaledHeight）
- `GpuTexture`/`GpuTextureView`/`GpuSampler`：GPU 资源抽象
- `RenderPipeline`：渲染管线描述
- `GlyphInfo`：字形数据结构（仅数据，不含渲染逻辑）

### client/gui 业务 GUI 框架层职责

完整 GUI 控件框架，依赖 `Minecraft.getInstance()` 业务实例和注册表 API：

- **控件框架**：`AbstractWidget`（implements LayoutElement/Renderable/GuiEventListener/NarratableEntry）+ `Button`/`EditBox`/`AbstractScrollWidget` 等具体控件
- **渲染上下文**：`GuiGraphicsExtractor`（原 GuiGraphics）桥接控件与底层 RenderSystem，提供 fill/text/blit/scissor/pose/itemStack/tooltip 等渲染接口，持有 `Minecraft` 与 `GuiRenderState`
- **事件系统**：`GuiEventListener`/`ContainerEventHandler`/`FocusNavigationEvent`/`ScreenRectangle`
- **布局引擎**：`LayoutElement`/`GridLayout`/`LinearLayout`/`LayoutSettings`
- **字体渲染**：`Font`/`FontSet`（实际渲染逻辑，blaze3d 仅提供字形数据）
- **屏幕系统**：`Screen` + `TitleScreen`/`OptionsScreen` 等业务屏幕
- **辅助系统**：`Tooltip`/`WidgetTooltipHolder` + `NarratableEntry`/`NarrationElementOutput` 无障碍

### 控件管理归属结论

**原版控件管理位于 client/gui 业务框架层，不属于 blaze3d 内核渲染层**。证据：

1. `AbstractWidget` 构造与 `mouseClicked` 直接 import 并调用 `Minecraft.getInstance()` 和 `SoundManager`
2. `GuiGraphicsExtractor` 构造时持 `Minecraft` 实例与 `AtlasManager`，访问 `AtlasIds.GUI` 业务注册表
3. 布局引擎 `GridLayout` 位于 `client.gui.layouts`，非 blaze3d
4. 控件 4 接口（LayoutElement/Renderable/GuiEventListener/NarratableEntry）全是 client/gui 内部接口

### 关键架构特征

- **渲染状态收集与提交分离**：控件 `extractRenderState` 只收集渲染状态塞入 `GuiRenderState`，由渲染层后续批量提交，非 immediate 模式
- **多 RenderPipeline 切换**：`RenderPipelines.GUI`/`GUI_INVERT`/`GUI_TEXT_HIGHLIGHT` 等多种管线在同一 RenderPass 内切换
- **矩阵栈与裁剪栈**：`Matrix3x2fStack pose()` 做变换嵌套，`ScissorStack` 做裁剪嵌套
- **Minecraft 业务语义紧密耦合**：控件层直接访问 Minecraft/SoundManager/AtlasManager/ItemStack/Component
