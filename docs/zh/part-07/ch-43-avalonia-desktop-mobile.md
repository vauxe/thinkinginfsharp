---
title: "第 43 章：Avalonia、桌面端与移动端"
description: "从状态、生命周期、平台、工具链、打包和证据边界设计 F# 用户界面，而不是把跨平台编译当作跨平台验证。"
translationKey: part-07/ch-43-avalonia-desktop-mobile
kind: chapter
part: 7
chapter: 43
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-avalonia-desktop
exerciseIds:
  - ch43-exercise-01
  - ch43-exercise-02
  - ch43-exercise-03
termIds: []
sources:
  - id: avalonia-get-started
    url: https://docs.avaloniaui.net/docs/get-started/
    checked: "2026-08-25"
  - id: avalonia-templates
    url: https://github.com/AvaloniaUI/Avalonia.Templates
    checked: "2026-08-25"
  - id: avalonia-desktop-nuget
    url: https://www.nuget.org/packages/Avalonia.Desktop/12.1.1
    checked: "2026-08-25"
  - id: avalonia-supported-platforms
    url: https://docs.avaloniaui.net/docs/supported-platforms
    checked: "2026-08-25"
  - id: avalonia-cross-platform-architecture
    url: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture
    checked: "2026-08-25"
  - id: avalonia-cross-platform-solution
    url: https://docs.avaloniaui.net/docs/app-development/cross-platform-solution-setup
    checked: "2026-08-25"
  - id: avalonia-application-lifetimes
    url: https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes
    checked: "2026-08-25"
  - id: avalonia-12-breaking-changes
    url: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
    checked: "2026-08-25"
  - id: avalonia-xaml-compilation
    url: https://docs.avaloniaui.net/docs/xaml/compilation
    checked: "2026-08-25"
  - id: avalonia-coded-ui
    url: https://docs.avaloniaui.net/docs/fundamentals/coded-ui
    checked: "2026-08-25"
  - id: avalonia-threading
    url: https://docs.avaloniaui.net/docs/app-development/threading
    checked: "2026-08-25"
  - id: avalonia-responsive-layouts
    url: https://docs.avaloniaui.net/docs/layout/responsive-layouts
    checked: "2026-08-25"
  - id: avalonia-accessibility
    url: https://docs.avaloniaui.net/docs/app-development/accessibility
    checked: "2026-08-25"
  - id: avalonia-headless-testing
    url: https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
    checked: "2026-08-25"
  - id: avalonia-windows
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/windows
    checked: "2026-08-25"
  - id: avalonia-macos
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/macos
    checked: "2026-08-25"
  - id: avalonia-linux
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/linux
    checked: "2026-08-25"
  - id: avalonia-android
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/android/
    checked: "2026-08-25"
  - id: avalonia-ios
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/ios
    checked: "2026-08-25"
  - id: avalonia-deploy-macos
    url: https://docs.avaloniaui.net/docs/deployment/macos
    checked: "2026-08-25"
  - id: avalonia-deploy-linux
    url: https://docs.avaloniaui.net/docs/deployment/linux
    checked: "2026-08-25"
  - id: avalonia-deploy-ios
    url: https://docs.avaloniaui.net/docs/deployment/ios
    checked: "2026-08-25"
  - id: dotnet-publishing
    url: https://learn.microsoft.com/dotnet/core/deploying/
    checked: "2026-08-25"
  - id: dotnet-wpf-migration
    url: https://learn.microsoft.com/dotnet/desktop/wpf/migration/
    checked: "2026-08-25"
  - id: dotnet-maui
    url: https://learn.microsoft.com/dotnet/maui/?view=net-maui-10.0
    checked: "2026-08-25"
  - id: dotnet-maui-templates
    url: https://github.com/dotnet/maui/tree/main/src/Templates/src/templates
    checked: "2026-08-25"
---

# 第 43 章：Avalonia、桌面端与移动端 {#overview}

F# 用户界面并不是“在真正的程序外面放几个控件”。它是一个长生命周期边界：输入、时间、取消、可变的平台对象、无障碍能力、操作系统服务和发布机制都在这里相遇。F# 最能发挥价值的方式，是把这些事件转成显式数据，并让决策在窗口出现之前就可测试。

Avalonia 是带有官方 F# 模板的跨平台 .NET UI 框架。它绘制自己的控件并提供桌面、移动和浏览器宿主，但这并不会让所有平台完全相同。共享视图可能成功编译，而它的字体、输入、生命周期、权限、原生集成、安装包、签名或无障碍路径仍会在某个目标上失败。“跨平台”描述的是架构与支持面，不是测试结果。

因此，本章先讨论产品和平台约束，而不是 XAML 语法。我们用 X43 展示一个小而真实的桌面验证切片，再向外展开到状态模式、绑定边界、线程、平台服务、移动宿主、测试、打包和发布证据。

## 学完本章后你能做什么 {#outcomes}

学完本章后，你应该能够：

- 区分领域状态、呈现状态、视图对象、平台宿主和可分发安装包；
- 从用户、设备、原生能力、团队技能和发布渠道选择 UI 方案；
- 解释为什么共享源码、共享 UI 和已验证行为是三种不同的声明；
- 判断 Avalonia、Windows 专用 UI、.NET MAUI、浏览器 UI 或薄原生壳何时值得做验证性试验（spike，即范围受限且便于删除的探索实现）；
- 把 Avalonia 项目理解为普通 .NET、XAML 构建任务与原生后端的组合；
- 让 F# `update` 函数独立于控件，并把 UI 事件分派成消息；
- 有意识地选择手写 MVU、MVVM 适配器、code-behind 或纯代码 UI；
- 在绑定边界显式处理 F# 记录、联合、option、命令和集合；
- 使用 Avalonia 12 的编译绑定，而不让动态反射藏在意外默认值后面；
- 区分经典桌面、单视图和 Android Activity 生命周期；
- 让阻塞工作远离 UI 线程，并只通过调度器回送视图更新；
- 把剪贴板、对话框、通知、文件、相机、权限和安全存储隔离在端口之后；
- 为窗口缩放、显示比例、键盘、触摸、本地化和辅助技术设计布局；
- 区分 XAML 构建、无头测试、原生启动、安装包安装、签名制品和应用商店发布；
- 按运行时标识符发布，并有意识地选择框架依赖或自包含交付；
- 准确说明 X43 验证了什么，以及它尝试的原生启动为什么没有通过；
- 用明确的证据矩阵设计可撤销的桌面或移动端采用试验。

## UI 应用是一组分层契约 {#ui-stack-contracts}

可以把客户端应用看成五个相连的层次：

```text
领域规则与持久数据
  -> 呈现模型与纯状态转换
  -> 工具包控件、布局、绑定与输入
  -> 平台宿主、生命周期、权限与原生服务
  -> 特定架构的安装包、签名、安装与更新
```

领域通常可以是普通 F# 库。呈现状态把领域结果和 UI 事件转成屏幕可渲染的状态。Avalonia 控件是由 UI 调度器拥有的可变 .NET 对象。宿主决定顶层是桌面窗口、Android Activity 还是单一移动视图。打包再加入操作系统身份、架构、元数据、签名、分发和升级行为。

较低层成功并不能证明更高层。纯转换测试不能证明 XAML 名称正确；XAML 构建不能证明原生显示可用；原生调试启动不能证明签名；签名安装包也不能证明升级安全或无障碍能力。

### 共享不等于相同 {#shared-not-identical}

讨论复用时，请分别给出三个比例：

1. **共享逻辑：** 领域规则、验证、网络契约、持久化抽象和呈现状态转换。
2. **共享 UI：** 视图、样式、资源、导航概念和工具包专用适配器。
3. **共享证据：** 真正在各个受支持操作系统、CPU 架构、输入模式和发布渠道运行的测试与观察。

对于强调原生体验的移动产品，第一项可以很高而第二项很低。在设备和打包测试之前，前两项都可能很高而第三项接近零。复用很有价值，但诚实的分母比好看的百分比更重要。

## 从产品边界开始选择 {#decision-map}

先从用户和发布约束出发：

| 首选候选 | 适合的场景 | F# 边界 | 仍需补齐的证据 |
| --- | --- | --- | --- |
| Avalonia 桌面端 | 新建或重写 Windows/macOS/Linux 客户端；可以接受一套自行绘制的控件系统 | 有官方 F# 应用与 MVVM 模板；纯核心可以保持惯用 F# | 每个桌面目标上的原生启动、DPI/输入、操作系统集成、打包、签名、安装/更新 |
| Avalonia 跨平台 | 在桌面与选定移动/浏览器目标间共享 Avalonia 视图层值得承担平台宿主 | 官方跨平台模板包含 F#；宿主应薄、状态应可移植 | Android/iOS 工作负载、生命周期、权限、设备 API、签名、模拟器/设备与商店证据 |
| WPF 或 WinUI 壳 | 产品明确仅限 Windows，或深度依赖既有 Windows 控件与 API | 薄 C# UI 壳可引用 F# 核心；直接 F# XAML 工具链需要单独证明 | 受支持 Windows 版本、安装器、企业分发、无障碍与 Windows 专用集成 |
| .NET MAUI 壳 | 移动优先产品需要 MAUI handler、控件、生态或原生平台集成 | 官方产品和模板呈 C#/XAML 形态；F# 核心加薄壳是低摩擦基线 | 工作负载、handler 行为、平台 SDK、设备、签名和商店；直接 F# UI 需单独工具链试验 |
| Fable 或其他 Web UI | 浏览器交付、URL 导航、Web 无障碍和即时更新占主导 | F# 可直接拥有浏览器状态；第 41 章讲解运行时边界 | 浏览器/设备矩阵、离线需求、可安装性、原生桥接、商店或包装器要求 |
| 薄原生宿主 | 平台惯例、相机/媒体、后台模式或原生控件比共享 UI 更重要 | 只在平台互操作和 AOT 路径得到证明时共享 F# 领域 | 每个原生宿主、ABI、生命周期、工具链、设备、签名与商店路径 |

这不是框架排行榜。既有经验与代码、无障碍要求、控件供应商、离线行为、更新策略、启动预算、包体大小、原生 API 深度和平台数量都会改变答案。

### 决定采用前先做纵向切片 {#vertical-slice}

有用的试验应包含最难的真实交互，而不只是计数器。至少加入一次领域转换、异步请求、取消、错误与重试、持久设置、平台服务、响应式屏幕、无障碍遍历、特定架构发布、有代表性的签名或安装包、干净安装、升级、回滚和遥测路径。

记录哪些部分使用官方 F# 模板、哪些示例由 C# 翻译而来、涉及哪些生成代码或设计器，以及哪个平台必须在特定操作系统上构建。结果应足以支持采用、限制、包裹或拒绝该工具包的决定。

## Avalonia 心智模型 {#avalonia-mental-model}

Avalonia 提供保留式控件树、样式、布局、输入路由、数据绑定、无障碍自动化对等体、渲染和平台后端。它的控件是 Avalonia 控件，并非每个平台原生控件的一层包装。这提高了视觉一致性和 UI 共享度，但平台惯例与原生集成仍需要显式处理。

`UsePlatformDetect()` 会选择可用的桌面后端：Windows 使用 Win32，macOS 使用自己的 Objective-C++ 原生后端，Linux 默认使用 X11。Avalonia 12.1 提供实验性的显式启用 Wayland 后端，但 `UsePlatformDetect()` 不会自动选择它。

### XAML 与纯代码 UI 是两种构造形式 {#xaml-and-coded-ui}

AXAML 由 XamlX 编译，创建的运行时对象图与代码构造的对象图相同。XAML 提供声明式布局、样式、资源、预览和熟悉的设计器工作流。纯代码 UI 把构造保留在语言中，能改善重构和 F# 表达式流，也可使用 Avalonia.FuncUI 等社区 F# 优先库。两者可以混用。

应根据团队熟练度、工具链、绑定需求、样式规模、热重载或预览要求、对生成代码的容忍度和库成熟度来选择。纯代码不会让可变控件自动变纯；XAML 也不要求把领域逻辑塞进 view model。X43 使用 AXAML 加极小的 F# code-behind，因为这种形式能在不引入另一框架的情况下暴露边界。

## X43：一个已验证的桌面切片 {#verified-slice}

X43 刻意只做一个 `net10.0` 桌面可执行程序。它有五个主要文件，没有移动目标框架、平台工作负载、MVVM 依赖或打包配置。

### 固定版本的普通 .NET 项目 {#pinned-project}

<<< @/../examples/ecosystem/avalonia/AvaloniaSample.fsproj{xml:line-numbers} [AvaloniaSample.fsproj]

`Avalonia`、`Avalonia.Desktop` 和 `Avalonia.Themes.Fluent` 固定到 12.1.1，并通过锁文件解析。仓库也锁定 FSharp.Core 10.1.301。`WinExe` 选择图形可执行程序；`net10.0` 仍是通用桌面目标，而不是 `net10.0-macos` 或 `net10.0-windows`。

显式 F# 编译顺序很重要：`MainWindow.fs` 定义 `Program.fs` 使用的类型。AXAML 文件由 Avalonia 构建目标处理，不是 F# 编译项。

### 让纯转换负责决策 {#pure-transition}

<<< @/../examples/ecosystem/avalonia/MainWindow.fs{fsharp:line-numbers} [MainWindow.fs]

`Model`、`Message` 和 `Counter.update` 不知道按钮、调度器、窗口或 Avalonia。`RemoveSeat` 维护下界。视图持有当前模型，只因为这个样例刻意局部且短暂；真实工作流应另外决定哪些内容必须跨导航、挂起、重启或升级保存。

窗口加载 AXAML、取得命名控件、把点击转成消息、调用纯更新，再渲染结果。这是一个小型手写 model-view-update 循环，并不主张所有 UI 副作用都应该塞进一个构造函数。

### 标记描述形状，不承载业务规则 {#markup-shape}

<<< @/../examples/ecosystem/avalonia/MainWindow.axaml{xml:line-numbers} [MainWindow.axaml]

标记负责布局、控件身份、标签和初始视觉值。文本按钮已经能通过内容暴露有用的无障碍名称。生产屏幕还应加入稳定的自动化 ID、可见文本语义不清时的显式标签、本地化资源、键盘行为、对比度检查，以及大字号和窄宽度测试。

`GetControl<T>` 会在必需名称不存在时主动失败。传给 `GetControl` 的字符串不是类型化绑定路径，因此 XAML 编译成功不能证明每个查找正确；无头或原生构造测试才能补上这一缺口。

### 宿主选择桌面生命周期 {#desktop-lifetime}

<<< @/../examples/ecosystem/avalonia/Program.fs{fsharp:line-numbers} [Program.fs]

`BuildAvaloniaApp` 对应官方模板为工具和启动保留的接缝。`StartWithClassicDesktopLifetime` 创建 `IClassicDesktopStyleApplicationLifetime`；只有框架初始化完成后，`App` 才设置 `MainWindow`。入口点带有 `STAThread`，这与 COM、剪贴板等 Windows API 有关。

这里明确使用桌面假设。iOS 和浏览器使用 `ISingleViewApplicationLifetime`；Android 使用带视图工厂的 `IActivityApplicationLifetime`，因为 Activity 可能被重建。试图在移动端复用这条 `MainWindow` 启动路径，是设计错误，不是缺一个编译开关。

### 聚焦测试不启动 UI {#focused-test}

仓库的 xUnit 测试套件引用该样例，检查三次添加、重置、下界移除，以及初始值没有被改变。它无需初始化 Avalonia 即可运行，因为被测函数不依赖工具包。这种速度和确定性正是该边界的回报。

最终聚焦运行通过 1/1，仓库的完整示例测试套件也通过。样例在 .NET SDK 10.0.301 下的 Release 构建为零警告、零错误；包含其他 .NET 项目和 Fable 浏览器冒烟的完整锁定示例门禁也通过。

### 准确陈述原生启动结果 {#native-launch-result}

| 证据 | X43 结果 | 它证明什么 | 它不能证明什么 |
| --- | --- | --- | --- |
| 锁定还原 | 通过 | 记录的 NuGet 依赖图可以解析 | 未来版本或其他运行时依赖图 |
| Release 构建 | 通过，0 警告/错误 | F# 与 AXAML 可为 `net10.0` 编译 | 可用的原生窗口或安装包 |
| 纯转换测试 | 通过 | 被检查的状态转换 | 控件查找、布局、输入、渲染 |
| 完整示例门禁 | 通过 | 仓库集成仍可复现 | 桌面/移动平台行为 |
| macOS 原生启动 | 已尝试；在窗口出现前因 Avalonia.Native RenderTimer 错误 `-6661` 失败 | 进程到达原生 macOS 后端，并暴露自动化会话缺失图形显示上下文 | 已显示窗口、用户交互或应用逻辑缺陷 |
| Windows/Linux/移动端/安装包/商店 | 未运行 | 什么都没有 | 什么都没有 |

这次启动失败是有价值的证据，因为它阻止了夸大结论。应在已解锁的交互式桌面会话中重跑。没有理由修改纯模型、吞掉异常，或仅因编译通过就宣称 macOS 成功。

## 有意识地选择状态模式 {#state-patterns}

F# 提供多种有用边界；工具包选择并不会强制某一种架构。

| 模式 | 状态与决策 | 视图连接 | 主要压力 |
| --- | --- | --- | --- |
| 手写 MVU | 不可变模型加 `Msg -> Model -> Model * Effect` 函数 | 事件处理器分派，渲染器更新控件 | 屏幕增多后渲染器和副作用调度会重复 |
| MVVM 适配器 | 领域和呈现核心仍保持函数式；适配器暴露属性、通知和命令 | AXAML 绑定连接适配器 | 可变通知面、命令生命周期、适合绑定的类型形状 |
| Code-behind 编排 | 视图内只有小型局部状态和事件处理器，领域调用委托到外部 | 直接控件引用 | 容易让业务决策、I/O 和取消堆积到窗口中 |
| 纯代码/FuncUI 风格 | UI 树用 F# 表达，通常由消息和状态驱动 | 语言级组合器或 DSL | 社区依赖、API 演进、工具和性能需要单独评估 |

X43 在最小有用规模上采用手写 MVU：一个纯更新函数和一个命令式渲染器。更大的应用通常应把模型、更新、副作用描述、视图适配器、导航和组合拆到模块或项目中。

### 副作用应成为消息，而不是隐藏分支 {#effects-as-messages}

对于异步工作，让纯转换描述应该发生什么，并把结果作为另一条消息接收：

```fsharp
type LoadState<'value> =
    | Idle
    | Loading of requestId: Guid
    | Loaded of 'value
    | Failed of message: string

type Msg<'value> =
    | LoadRequested
    | LoadCompleted of requestId: Guid * Result<'value, string>
    | LoadCancelled of requestId: Guid
```

副作用运行器拥有 HTTP、文件、时间、取消和分派。请求标识符让 `update` 能拒绝来自旧屏幕或旧搜索的迟到结果。不要把即发即弃工作藏在属性 setter 或点击处理器中，让故障和过期结果无处建模。

## XAML 与绑定边界上的 F# {#fsharp-xaml-boundary}

Avalonia 为应用、MVVM 和跨平台提供官方 F# 模板。这一点很重要：项目布局、启动、AXAML code-behind 和模板生成都有经过测试的路径。但它并不表示每个文档示例、设计器、源生成器、第三方控件或 MVVM 包都有同等成熟的 F# 体验。

### 匹配类名并尊重文件顺序 {#classes-and-file-order}

AXAML 的 `x:Class` 必须匹配 F# 中被加载类型的命名空间和类型名。F# 源码顺序必须把定义放在使用者之前。C# 常见的生成式 partial class 约定不会消除这些规则。保持启动和视图代码足够小，才能把故障归因到 AXAML 编译、F# 编译、绑定或原生启动中的具体一层。

命名查找和事件连接适合小视图。对于重复呈现状态、模板、验证和命令，绑定更易扩展，但它增加了一个公开的对象形接口。应像对待其他 API 一样对待它。

### Avalonia 12 默认使用编译绑定 {#compiled-bindings}

在 Avalonia 12 中，普通 `{Binding ...}` 默认映射成编译绑定。编译绑定需要 `x:DataType`，XAML 编译器才能拒绝不存在的路径和不兼容类型。只有值确实需要动态解析时才使用 `{ReflectionBinding ...}`，不要把它当作逃避类型错误的总开关。

X43 没有绑定表达式，所以不需要 `x:DataType`，也不能证明 view-model 绑定表面。真实绑定试验应包含嵌套模板、双向编辑、命令、验证、设计时数据、计划采用时的裁剪或 AOT，以及团队真实使用的 IDE。

### 适配函数式类型，而不是削弱它们 {#binding-adapters}

- 不可变 F# 记录会暴露可读的 .NET 属性，但不会自动触发 `INotifyPropertyChanged`；可以替换后重新渲染，或用通知适配器包裹。
- 可辨识联合非常适合呈现状态，但 XAML 往往需要 `IsBusy`、`ErrorText` 或选定模板等派生属性；在适配器边缘计算它们。
- 有意识地把 `option` 和 `Result` 转成可见性、可空载荷、错误文本或验证状态；不要让 null 约定悄悄定义领域含义。
- 不可变列表很适合作为模型值。中小集合可以替换 `ItemsSource` 快照；只有增量更新确实重要时才使用可观察适配器。
- 命令是副作用边界。为它提供显式可执行规则、取消、错误路由和生命周期，而不是把网络调用放进匿名 setter。
- 除非耦合是明确的产品决定，否则不要让工具包类型进入可复用领域。

适配器不是对函数式设计的背叛。它阻止绑定引擎的可变、反射、null 和通知约定向内泄漏。

## 线程、取消与生命周期 {#threading-and-lifetime}

Avalonia 使用单一 UI 线程。控件创建、属性访问、布局、渲染和输入都属于其调度器。`Dispatcher.UIThread.Post` 调度工作但不等待；`InvokeAsync` 让调用者等待完成。Avalonia 12 可在不同线程拥有多个调度器，但仍不支持多个 UI 线程；可复用控件代码在适当时应优先使用控件自己的调度器。

不要为了“避开 UI 线程”就把天然异步的 I/O 放进 `Task.Run`。等待 I/O，让 CPU 密集工作远离调度器，再把小型结果消息送回视图。切勿在事件处理器中使用 `.Result`、`.Wait()`，也不要执行长时间同步文件或数据库操作。

把每项操作绑定到一种生命周期：窗口、视图、导航项、应用或持久后台任务。生命周期结束时尽力取消，但仍要拒绝过期完成，因为取消存在竞态。把每个异常路由到已建模失败、日志边界或受监督任务；无人观察的即发即弃异常不是用户反馈。

### 桌面与移动端不共享同一种生命周期 {#platform-lifetimes}

桌面应用可以有多个窗口和多种关闭策略。iOS 与浏览器暴露一个主视图。Android 可能重建 Activity，因此 Avalonia 要求一个创建新视图的工厂。移动应用离开前台后，操作系统可能挂起或终止进程。

因此，不要把不可替代的工作仅保存在 `Window`、控件树、view model 实例或静态单例中。在明确检查点持久化草稿和标识符，从持久状态重新水化，并把导航与恢复建模为呈现状态机的显式输入。

## 把平台服务放在端口之后 {#platform-services}

通用 .NET 库覆盖网络、序列化、密码学原语和大量存储逻辑。UI 平台在对话框、剪贴板、通知、安全存储、文件选择器、深链、相机、生物识别、分享、后台执行、菜单、托盘图标和权限提示上仍然不同。

在共享项目中定义面向能力的端口：

```fsharp
type PickDocument = CancellationToken -> Task<Result<string option, string>>
type SaveDraft = Draft -> CancellationToken -> Task<Result<unit, string>>
type OpenExternalUri = Uri -> Task<Result<unit, string>>
```

在平台宿主中实现它们，并在组合时注入。用户需要不同恢复方式时，应分别建模“已取消”“不可用”“权限拒绝”和“失败”。避免全局 `if OperatingSystem.Is...` 森林，也不要让核心接口暴露 Android、UIKit、Win32 或 Avalonia 对象。

`OnPlatform` 与 `OnFormFactor` 适合小型资源或布局差异。当行为、权限或生命周期发生变化时，它们不能代替平台服务。

## 桌面端本来就是三个平台程序 {#desktop-platforms}

Avalonia 的一个桌面项目可以面向 Windows、macOS 和 Linux，但每个后端与分发系统仍然不同。支持层级和最低操作系统版本会变化；仓库在 2026-08-25 检查了官方矩阵，并应在每次发布前复查。

### Windows {#windows}

Avalonia 直接使用 Win32，其通用桌面目标不需要单独的 Windows .NET 工作负载。发布仍要选择 `win-x64`、`win-arm64` 或其他受支持 RID；框架依赖还是自包含交付；安装器技术；应用身份；图标；文件关联；签名；更新策略以及企业部署行为。

应测试键盘与高 DPI、多显示器、剪贴板和对话框、受支持时的远程会话、Windows 无障碍、干净安装、每用户/每机器数据、升级、修复和卸载。在 macOS 上构建出的安装包只是交叉编译证据，不是 Windows 运行时证据。

### macOS {#macos}

默认 Avalonia macOS 后端自带原生库，无需 `net10.0-macos` 工作负载即可构建。分发仍需要结构正确的 `.app` 包和 `Info.plist`；常规外部分发要求代码签名和公证，而即使包结构可以跨平台生成，这些签名步骤仍需要 macOS/Xcode 工具。

若支持 Apple Silicon 与 Intel，应分别发布和测试。验证原生菜单、快捷键、文件对话框、沙箱或 entitlement 选择、无障碍、应用身份、隔离/Gatekeeper 行为、升级和卸载。X43 的 `-6661` 启动结果明确不是通过的 macOS 冒烟测试。

### Linux {#linux}

Avalonia 默认以 X11 为目标；Wayland 通常通过 XWayland 运行，除非显式选择实验性原生 Wayland 后端。Linux 发布范围必须写明发行版、版本、CPU 架构、显示后端、GPU/软件渲染路径、桌面环境、原生库、字体、安装包格式和更新渠道。

成功构建 `.deb` 不能证明 RPM、Flatpak、Snap、AppImage 或解压归档。应在干净的受支持镜像上测试桌面入口、图标、可执行权限、原生依赖缺失、区域设置/字体、AT-SPI 无障碍、安装、升级和移除。

## 移动端支持是一张项目图，不是复选框 {#mobile-boundary}

官方 Avalonia 跨平台结构包含共享 Core 项目、独立 Desktop、Android、iOS 以及可选 Browser 宿主。视图与呈现逻辑可以放进 Core；每个宿主提供自己的目标框架、入口点、SDK 集成、元数据、权限、原生服务、签名和部署路径。

在已检查的支持矩阵中，Avalonia 移动目标要求 .NET 10，Android 与 iOS 支持跟随 .NET MAUI 平台生命周期。这些约束具有时效性，因此应固定 SDK/工作负载集合，并在每个发布序列复查矩阵。

### Android {#android}

Android 宿主是一个 .NET Android 项目，`MainActivity` 派生自 Avalonia 的 Activity 基类。构建它需要 .NET Android 工作负载、Android SDK、JDK 和匹配的目标组件。运行时证据需要模拟器和有代表性的真机，而不是只构建共享项目。

测试 Activity 重建、返回导航、配置变化、进程死亡、权限、深链、键盘与 inset、触摸、无障碍、离线行为、后台限制、包身份、架构拆分、签名、升级和商店策略。需要跨重建保存的状态不能只留在 Activity 实例中。

### iOS 与 iPadOS {#ios}

iOS 宿主是带 Avalonia app delegate 和当前 scene 初始化方式的 .NET iOS 项目。工具链需要 iOS 工作负载；运行与设备验证需要带 Xcode 的 macOS 硬件。真机增加证书与 provisioning，商店分发还增加签名归档、App Store 元数据、审核和更新约束。

测试模拟器和真机路径、前后台切换、内存压力、权限、安全区域、旋转、键盘、适用时的触摸与指针、VoiceOver、深链、离线恢复、包身份、签名、升级和商店交付。Mac Catalyst 是独立的 UIKit 路径，不是默认 Avalonia macOS 桌面目标所用的同一个后端。

### F# 支持需要两句话 {#fsharp-support-boundary}

第一，Avalonia 官方模板明确为桌面、MVVM 和跨平台解决方案列出 F#。第二，周边移动 SDK、大多数原生示例、商店工具和许多第三方库仍呈 C# 形态。两者可以同时成立。

优先使用惯用的 F# 共享核心，以及被所选工具链验证过的最薄宿主。一个小型 C# 平台适配器，往往比强迫 F# 适应生成器或设计器约定更便宜。若直接 F# 宿主在选定模板与 IDE 中运行良好，可以保留，但仍要维持可复现的命令行构建和设备证据。

.NET MAUI 是另一个面向 Android、iOS、Mac Catalyst 和 Windows 的 UI 产品。其官方文档和当前模板源码以 C#/XAML 为主。这既不妨碍 F# 库驱动 MAUI 应用，也不能证明社区 F# 模板不合适；它只说明直接使用 F# 编写 MAUI UI 是另一项采用决定，而不是 Avalonia 的证据。

## 无障碍、输入与响应式布局都是行为 {#accessible-responsive-ui}

Avalonia 内置控件通过自动化对等体向平台无障碍 API 暴露语义。优先使用语义控件。可见内容不足时，提供 `AutomationProperties.Name`、`LabeledBy`、`HelpText`、实时通知设置或稳定 `AutomationId`。自定义控件需要明确的自动化行为。

测试键盘遍历、焦点顺序与恢复、快捷键、屏幕阅读器、对比度、大字号、缩放或显示比例、高 DPI、适用时的减少动态效果、错误播报，以及完全不用指针的输入。不能只用颜色传递含义。

响应式设计不只是检测“移动端”。应让布局响应可用空间；只有交互确实不同时才使用 form-factor 或平台条件。测试较长英文、紧凑中文标签、本地化膨胀、支持时的从右到左文本、窄窗口、触摸目标、软件键盘、安全区域、旋转和可调整大小的桌面窗口。

即使共享 AXAML 完全相同，无障碍和本地化缺陷仍是平台缺陷。平台矩阵中应包含有代表性的辅助技术和输入设备。

## 建立证据阶梯 {#testing-evidence-ladder}

先用成本最低的有效层次，但不要停在那里：

1. **纯测试：** 更新、验证、导航决策、过期结果拒绝、格式化输入和副作用描述。
2. **适配器测试：** 通知、命令、集合增量、验证投影、取消和平台端口替身。
3. **XAML/编译测试：** 资源、类、编译绑定路径、模板和目标框架依赖图。
4. **Avalonia 无头测试：** 构造真实控件、应用样式与布局、模拟输入、检查视觉或自动化树，并可选择比较图像。
5. **原生调试冒烟：** 启动已解锁的原生后端，操作键盘/指针/触摸、对话框、剪贴板、缩放和关闭。
6. **发布与安装包测试：** 生成每个 RID、检查原生资产与元数据、签名、在干净目标安装、脱离 SDK 启动、升级、回滚和卸载。
7. **设备与商店测试：** 权限、挂起/恢复、进程死亡、深链、断网、无障碍、性能、签名、分阶段分发、崩溃报告和更新行为。

无头测试对 CI 很有价值，但它替换了原生窗口和渲染后端。它无法认证 Win32、macOS、X11/Wayland、Android、iOS、驱动、打包、签名或商店行为。

维护一张按操作系统版本、CPU、安装包、区域设置、显示比例、输入、辅助技术、测试日期、提交和结果索引的证据表。只有明确“机器”是什么，“在我的机器上能用”才开始有价值。

## 发布不等于打包或上线 {#publishing-and-release}

`dotnet build` 负责编译；`dotnet publish` 创建部署布局；打包加入平台结构与元数据；签名建立发布者身份与完整性；分发通过安装器、软件仓库、MDM 或商店搬运制品；上线还包括滚动发布、观察、支持、更新和回滚。

### 为每个目标选择运行时交付方式 {#runtime-delivery}

框架依赖发布更小，并使用目标上兼容的已安装 .NET 运行时及其服务补丁。自包含发布携带所选运行时，获取运行时安全更新时必须重新构建。运行时标识符选择操作系统和架构；从 .NET 8 开始，RID 本身不再隐含自包含，因此要显式声明选择。

单文件、裁剪、ReadyToRun 和 Native AOT 会改变大小、启动、反射、诊断和兼容性。Avalonia 的 AOT 指南要求编译绑定。只有在警告清零，并针对绑定、序列化、依赖注入、插件、资源、原生库和每个打包架构完成运行测试后，才启用这些选项。

不要在发布多个 RID 时意外覆盖同一份共享锁图。使用显式还原输入或隔离的输出/锁策略，并证明提交的依赖图保持不变。

### 设计安装、更新与恢复 {#install-update-recovery}

定义应用 ID、版本语义、数据目录、设置 schema、缓存策略、日志、崩溃报告、文件关联、协议处理器、证书、entitlement 和更新渠道。绝不能假设工作目录可写或稳定。不要把机密放进安装包或日志；适当时使用平台凭据存储。

更新可能同时改变可执行文件和用户数据。采用向后兼容的设置/数据迁移、原子写入、不可替代本地数据的备份、降级策略，以及启动失败时的恢复路径。测试干净安装、从每个受支持前序版本更新、更新中断、回滚或前向修复，以及带明确用户数据保留策略的卸载。

## 开展范围受限的采用试验 {#adoption-spike}

对于严肃客户端，应限时完成一个有代表性的纵向切片并测量：

- 官方 F# 模板创建、锁定还原、CLI 构建、IDE 编辑/预览和调试器行为；
- 一个不可变领域工作流穿过所选状态模式和绑定或渲染器；
- 一个虚拟化列表或其他有真实压力的屏幕，在代表性数据量上运行；
- 异步取消、过期完成、离线错误、重试和重启恢复；
- 一个包含权限拒绝与取消路径的平台服务；
- 键盘、触摸、焦点、屏幕阅读器、大字号、本地化和窄布局；
- 目标硬件上的启动、交互延迟、内存、包体和崩溃诊断；
- 每个 RID 的发布、干净安装、签名或代表性签名、升级和卸载；
- 若移动端在范围内，则包括移动生命周期和真机行为；
- 依赖维护、许可、支持策略、控件生态和退出条件。

比较实现与运维成本，而不是截图相似度。只有当团队能在产品真实平台矩阵内构建、诊断、分发、更新和支持它时，一个框架才算可接受。

## 避免常见 UI 错误 {#common-mistakes}

- 把共享项目构建成功当作 Windows、macOS、Linux、Android 和 iOS 验证。
- 还没说清所需平台、原生 API、输入模式、商店和更新策略，就先选择框架。
- 让业务决策、HTTP、文件和取消逐渐堆进窗口 code-behind。
- 把可变 view model 称作“模型”，丢失它下面的不可变领域状态。
- 没有明确适配器契约就把可辨识联合或 option 直接暴露给 XAML。
- 为了消除 `x:DataType` 或公开类型形状错误而全局禁用编译绑定。
- 阻塞 UI 调度器，或从事件处理器启动无人监督的即发即弃任务。
- 导航离开或更新请求后，仍接受迟到的异步结果。
- 把不可替代状态只保存在窗口、Activity、控件、单例、缓存或工作目录中。
- 在共享逻辑里散落操作系统判断，而不是注入平台能力。
- 以为控件相同就代表生命周期、无障碍、字体、输入或原生服务相同。
- 设计固定桌面画布，却称它支持移动端。
- 使用自定义可点击视觉元素，却没有键盘、焦点、语义或自动化对等体。
- 只做无头测试，却声称获得原生渲染或打包结果。
- 发布一种架构、可变依赖图或未签名目录，就称之为上线。
- 仅为缩小体积启用裁剪、单文件或 AOT，却不测试反射和原生资产。
- 忘记设置/数据迁移、更新中断、回滚、卸载和用户数据策略。
- 强迫每个平台宿主都使用 F#，即使极薄 C# 适配器能降低工具链风险。
- 把一次图形自动化启动失败当作应用逻辑有缺陷的证明。
- 把一次本地成功启动当作安装器、商店和受支持操作系统版本可用的证明。

## 练习 {#exercises}

### 练习 1：为三个产品选择 UI 边界 {#exercise-01}

为每个产品选择首选候选、被拒方案、证据缺口和反转条件：(a) 一套仅限 Windows 的交易工作站必须复用成熟 WPF 控件和企业分发；领域计算用新 F# 编写；(b) 一套离线现场工具需要 Windows、macOS 和两个指定 Linux 发行版，支持键盘与触摸、本地文档，但不发布手机版；(c) 一款消费应用需要 Android 与 iOS、相机、推送通知、深链、后台上传、商店分发，以及一个小型桌面查看器。比较 Avalonia、围绕 F# 核心的 C# 平台壳、.NET MAUI 和浏览器界面，不要强迫三个产品共用一个答案。

### 练习 2：把 X43 变成桌面发布 {#exercise-02}

设计把 X43 变成受支持 Windows/macOS/Linux 应用所需的最小修改和证据。覆盖模块边界、异步副作用、持久化、设置迁移、无障碍、本地化、无头测试、原生冒烟、运行时标识符、框架依赖与自包含交付、原生资产、安装包、签名/公证、干净安装、更新、回滚、崩溃诊断和准确平台矩阵。保留现有 `-6661` 启动结果的诚实边界。

### 练习 3：把架构扩展到移动端 {#exercise-03}

为预约客户端设计 Core/Desktop/Android/iOS 项目图。共享屏幕编辑草稿、提交预约、跨旋转或 Activity 重建生存、进程终止后恢复、打开确认深链，并通过平台选择器导出收据。定义 F# 状态/消息/副作用、平台端口、生命周期所有权、持久化检查点、权限结果、过期结果保护、宿主语言选择、工作负载锁、模拟器/设备测试、签名、分阶段商店发布、遥测和反转条件。说明桌面构建能证明移动目标的什么内容。

[阅读本章练习答案](../solutions/ch-43-avalonia-desktop-mobile)。

## 本章回顾 {#chapter-review}

- 客户端是一组领域、呈现、工具包、宿主和分发契约。
- 分别衡量共享逻辑、共享 UI 和共享证据。
- 从用户、设备、原生能力、团队技能和发布渠道选择 UI 边界。
- Avalonia 提供官方 F# 模板、编译 AXAML、共享控件和多个平台宿主，但不会消除平台行为。
- X43 固定 Avalonia 12.1.1，并把纯 `Counter.update` 与命令式桌面视图分离。
- 它的锁定还原、构建、测试和仓库门禁通过；自动化 macOS 原生启动没有通过，因此不声称原生成功。
- 手写 MVU、MVVM 适配器、code-behind 和纯代码 UI 是具有不同压力点的选择。
- Avalonia 12 默认使用编译绑定并要求显式数据类型；反射绑定是有意的例外。
- 在 UI 边界适配不可变记录、联合、option、集合和命令，而不是削弱领域模型。
- 控件属于调度器；把异步结果和取消建模成消息，并拒绝过期结果。
- 桌面窗口、iOS/浏览器单视图和 Android Activity 工厂具有不同生命周期。
- 把原生能力放在端口之后，不让平台对象进入可复用核心。
- 一个桌面项目仍需要独立 Windows、macOS 和 Linux 运行与安装包证据。
- 移动端需要平台项目、.NET 10 工作负载、SDK、权限、签名、设备和商店。
- 无障碍、响应式布局、本地化、键盘、触摸和生命周期都是行为，而不是润色。
- 从纯测试逐步走向无头控件、原生冒烟、安装包、设备和商店证据。
- 发布、打包、签名、分发、更新、观察和恢复是不同的发布阶段。

第 44 章将跨越另一种宿主边界：在 Unity 中使用 F# 领域代码，同时把 Unity 序列化、组件生命周期、IL2CPP 与 Player 构建保留在显式适配层中。
