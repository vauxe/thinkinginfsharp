---
title: "第 43 章：Avalonia、桌面端与移动端"
description: "从状态、生命周期、平台、工具链、打包和证据边界设计 F# 用户界面，而不是把跨平台编译当作跨平台验证。"
translationKey: part-07/ch-43-avalonia-desktop-mobile
---

# 第 43 章：Avalonia、桌面端与移动端 {#overview}

F# 用户界面不只是给程序加几个控件。它是一个长生命周期边界：输入、时间、取消、可变的平台对象、无障碍能力、操作系统服务和发布机制都在这里相遇。F# 最能发挥价值的方式，是把这些事件转成显式数据，并让决策在窗口出现之前就可测试。

Avalonia 是带有官方 F# 模板的跨平台 .NET UI 框架。它绘制自己的控件并提供桌面、移动和浏览器宿主，但这并不会让所有平台完全相同。共享视图可能成功编译，而它的字体、输入、生命周期、权限、原生集成、安装包、签名或无障碍路径仍会在某个目标上失败。“跨平台”描述的是架构与支持范围，不是测试结果。

因此，先讨论产品和平台约束，而不是 XAML 语法。运行一个验证范围明确的桌面样例后，再讨论状态模式、绑定边界、线程、平台服务、移动宿主、测试、打包和发布检查。

::: tip 分两轮阅读
初读时依次掌握 [UI 分层](#ui-stack-contracts)、[选型方法](#decision-map)和[桌面验证样例](#verified-slice)。实现或采用 UI 工具包时，再按需查阅状态、绑定、生命周期、平台、验证与发布各节。
:::

## UI 应用是一组分层契约 {#ui-stack-contracts}

可以把客户端应用看成五个相连的层次：

```text
领域规则与持久数据
  -> 呈现模型与纯状态转换
  -> 工具包控件、布局、绑定与输入
  -> 平台宿主、生命周期、权限与原生服务
  -> 特定架构的安装包、签名、安装与更新
```

领域通常可以是普通 F# 库。呈现层把领域结果和 UI 事件转成屏幕可渲染的状态。Avalonia 控件是可变 .NET 对象，只能通过 UI 调度器操作。宿主决定顶层是桌面窗口、Android Activity 还是单一移动视图。打包再加入操作系统身份、架构、元数据、签名、分发和升级行为。

每一层都要在自身边界验证。纯转换测试检查呈现逻辑，XAML 构建检查名称与标记，原生启动检查渲染与交互；安装包、升级和无障碍测试则覆盖发布路径。

### 共享不等于相同 {#shared-not-identical}

讨论复用时，请分别给出三个比例：

1. **共享逻辑：** 领域规则、验证、网络契约、持久化抽象和呈现状态转换。
2. **共享 UI：** 视图、样式、资源、导航概念和工具包专用适配器。
3. **已验证范围：** 真正在各个受支持操作系统、CPU 架构、输入模式和发布渠道运行的测试与观察。

对于强调原生体验的移动产品，共享逻辑比例可以很高，而共享 UI 比例很低。在设备和打包测试之前，前两项都可能很高，但经过验证的共享行为接近零。复用很有价值，但必须分别说明这些比例的计算范围。

## 从产品边界开始选择 {#decision-map}

先从用户和发布约束出发：

| 首选候选 | 适合的场景 | F# 边界 | 仍需完成的验证 |
| --- | --- | --- | --- |
| Avalonia 桌面端 | 新建或重写 Windows/macOS/Linux 客户端；可以接受一套自行绘制的控件系统 | 有官方 F# 应用与 MVVM 模板；纯核心可以保持惯用 F# | 每个桌面目标上的原生启动、DPI/输入、操作系统集成、打包、签名、安装/更新 |
| Avalonia 跨平台 | 在桌面与选定移动/浏览器目标间共享 Avalonia 视图层，收益足以覆盖维护平台宿主的成本 | 官方跨平台模板包含 F#；宿主应薄、状态应可移植 | Android/iOS 工作负载、生命周期、权限、设备 API、签名、模拟器/设备测试与商店验证 |
| WPF 或 WinUI 壳 | 产品明确仅限 Windows，或深度依赖既有 Windows 控件与 API | 薄 C# UI 壳可引用 F# 核心；直接 F# XAML 工具链需要单独验证 | 受支持 Windows 版本、安装器、企业分发、无障碍与 Windows 专用集成 |
| .NET MAUI 壳 | 移动优先产品需要 MAUI handler、控件、生态或原生平台集成 | 官方产品和模板主要使用 C#/XAML；F# 核心加简短外壳通常更容易实现 | 工作负载、handler 行为、平台 SDK、设备、签名和商店；直接 F# UI 需单独工具链试验 |
| Fable 或其他 Web UI | 浏览器交付、URL 导航、Web 无障碍和即时更新占主导 | F# 可直接管理浏览器状态；第 41 章讲解运行时边界 | 浏览器/设备矩阵、离线需求、可安装性、原生桥接、商店或包装器要求 |
| 薄原生宿主 | 平台惯例、相机/媒体、后台模式或原生控件比共享 UI 更重要 | 平台互操作和 AOT 验证通过后，再共享 F# 领域 | 每个原生宿主、ABI、生命周期、工具链、设备、签名与商店路径 |

这不是框架排行榜。既有经验与代码、无障碍要求、控件供应商、离线行为、更新策略、启动预算、包体大小、原生 API 深度和平台数量都会改变答案。

### 决定采用前先做端到端小样 {#vertical-slice}

有用的试验应包含最难的真实交互，而不只是计数器。至少覆盖四条路径：

- **应用行为：** 领域转换、异步请求、取消、错误与重试；
- **用户体验：** 持久设置、平台服务、响应式屏幕和无障碍遍历；
- **交付：** 特定架构发布、有代表性的签名或安装包、干净安装、升级与回滚；
- **运行反馈：** 一条可用的遥测路径。

记录哪些部分使用官方 F# 模板、哪些示例由 C# 翻译而来、涉及哪些生成代码或设计器，以及哪个平台必须在特定操作系统上构建。结果应足以支持采用、限制、包裹或拒绝该工具包的决定。

## Avalonia 的运行方式 {#avalonia-mental-model}

Avalonia 提供保留式控件树、样式、布局、输入路由、数据绑定、无障碍自动化对等体、渲染和平台后端。它的控件是 Avalonia 控件，并非每个平台原生控件的一层包装。这提高了视觉一致性和 UI 共享度，但平台惯例与原生集成仍需要显式处理。

`UsePlatformDetect()` 会选择可用的桌面后端。Windows 使用 Win32，macOS 使用自己的 Objective-C++ 原生后端，Linux 默认使用 X11。Avalonia 12.1 也提供需主动启用的实验性 Wayland 后端，但这个方法不会自动选择它。

### XAML 与纯代码 UI 是两种构造形式 {#xaml-and-coded-ui}

AXAML 由 XamlX 编译，创建的运行时对象图与代码构造的对象图相同。XAML 提供声明式布局、样式、资源、预览和熟悉的设计器工作流。纯代码 UI 把构造过程留在 F# 中，让重构和表达更直接，也可使用 Avalonia.FuncUI 等社区 F# 优先库。两者可以混用。

应根据团队熟练度、工具链、绑定需求、样式规模、热重载或预览要求、对生成代码的容忍度和库成熟度来选择。纯代码不会让可变控件自动变纯；XAML 也不要求把领域逻辑塞进 view model。桌面样例使用 AXAML 加极小的 F# code-behind，因为这种形式能在不引入另一框架的情况下暴露边界。

## 桌面样例：一个已验证的桌面程序 {#verified-slice}

这个已验证程序采用一个 `net10.0` 桌面可执行项目和五个主要文件。移动目标框架、平台工作负载、MVVM 基础设施与打包配置，会在相关需求进入测试后逐步加入。

### 固定版本的普通 .NET 项目 {#pinned-project}

```xml:line-numbers [AvaloniaSample.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>ThinkingInFSharp.AvaloniaSample</AssemblyName>
    <RootNamespace>ThinkingInFSharp.AvaloniaSample</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="MainWindow.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
  </ItemGroup>
</Project>
```
`Avalonia`、`Avalonia.Desktop` 和 `Avalonia.Themes.Fluent` 固定到 12.1.1，并通过锁文件解析。复制项目后，也可以固定 FSharp.Core 10.1.301。`WinExe` 选择图形可执行程序；`net10.0` 仍是通用桌面目标，而不是 `net10.0-macos` 或 `net10.0-windows`。

显式 F# 编译顺序很重要：`MainWindow.fs` 定义 `Program.fs` 使用的类型。AXAML 文件由 Avalonia 构建目标处理，不是 F# 编译项。

### 让纯转换负责决策 {#pure-transition}

```fsharp:line-numbers [MainWindow.fs]
namespace ThinkingInFSharp.AvaloniaSample

open Avalonia.Controls
open Avalonia.Markup.Xaml

type Model = { Seats: int }

type Message =
    | AddSeat
    | RemoveSeat
    | Reset

[<RequireQualifiedAccess>]
module Counter =
    let initial = { Seats = 0 }

    let update message model =
        match message with
        | AddSeat -> { model with Seats = model.Seats + 1 }
        | RemoveSeat ->
            { model with
                Seats = max 0 (model.Seats - 1) }
        | Reset -> initial

type MainWindow() as this =
    inherit Window()

    do
        AvaloniaXamlLoader.Load(this)

        let countText = this.GetControl<TextBlock>("CountText")
        let statusText = this.GetControl<TextBlock>("StatusText")
        let removeButton = this.GetControl<Button>("RemoveButton")
        let mutable model = Counter.initial

        let render state =
            countText.Text <- string state.Seats

            statusText.Text <-
                if state.Seats = 0 then "No seats selected"
                elif state.Seats = 1 then "1 seat selected"
                else $"{state.Seats} seats selected"

            removeButton.IsEnabled <- state.Seats > 0

        let dispatch message =
            model <- Counter.update message model
            render model

        this.GetControl<Button>("AddButton").Click.Add(fun _ -> dispatch AddSeat)
        removeButton.Click.Add(fun _ -> dispatch RemoveSeat)
        this.GetControl<Button>("ResetButton").Click.Add(fun _ -> dispatch Reset)
        render model
```
`Model`、`Message` 和 `Counter.update` 不知道按钮、调度器、窗口或 Avalonia。`RemoveSeat` 维护下界。视图持有当前模型，只因为这个样例刻意局部且短暂；真实工作流应另外决定哪些内容必须跨导航、挂起、重启或升级保存。

窗口加载 AXAML、取得命名控件、把点击转成消息、调用纯更新，再渲染结果。这是一个小型手写 model-view-update 循环，并不主张所有 UI 副作用都应该塞进一个构造函数。

### 标记描述界面结构，不承载业务规则 {#markup-shape}

```xml:line-numbers [MainWindow.axaml]
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="ThinkingInFSharp.AvaloniaSample.MainWindow"
    Title="Thinking in F# — Avalonia"
    Width="520"
    Height="400"
    MinWidth="420"
    MinHeight="340"
    WindowStartupLocation="CenterScreen">
  <Grid RowDefinitions="Auto,*,Auto" Margin="32">
    <StackPanel Grid.Row="0" Spacing="6">
      <TextBlock FontSize="13" FontWeight="SemiBold" Text="THINKING IN F#" />
      <TextBlock FontSize="28" FontWeight="Bold" Text="Pure update, thin view" />
      <TextBlock Opacity="0.72" Text="Avalonia owns the window; F# owns the state transition." />
    </StackPanel>

    <Border Grid.Row="1" Margin="0,24" Padding="24" CornerRadius="16" BorderThickness="1">
      <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
        <TextBlock HorizontalAlignment="Center" Opacity="0.72" Text="Seats requested" />
        <TextBlock
            x:Name="CountText"
            HorizontalAlignment="Center"
            FontSize="64"
            FontWeight="Bold"
            Text="0" />
        <TextBlock x:Name="StatusText" HorizontalAlignment="Center" Text="No seats selected" />
      </StackPanel>
    </Border>

    <StackPanel Grid.Row="2" HorizontalAlignment="Center" Orientation="Horizontal" Spacing="12">
      <Button x:Name="RemoveButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Remove" />
      <Button x:Name="ResetButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Reset" />
      <Button x:Name="AddButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Add a seat" />
    </StackPanel>
  </Grid>
</Window>
```
标记负责布局、控件身份、标签和初始视觉值。文本按钮已经能通过内容暴露有用的无障碍名称。生产屏幕还应加入稳定的自动化 ID、可见文本语义不清时的显式标签、本地化资源、键盘行为、对比度检查，以及大字号和窄宽度测试。

`GetControl<T>` 会在必需名称不存在时主动失败。传给 `GetControl` 的字符串不是类型化绑定路径，因此 XAML 编译成功不能证明每个查找正确；无头或原生构造测试才能补上这一缺口。

### 宿主选择桌面生命周期 {#desktop-lifetime}

```fsharp:line-numbers [Program.fs]
namespace ThinkingInFSharp.AvaloniaSample

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    override this.Initialize() = AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.MainWindow <- MainWindow()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

module Program =
    [<CompiledName("BuildAvaloniaApp")>]
    let buildAvaloniaApp () =
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(areas = Array.empty)

    [<EntryPoint; STAThread>]
    let main args =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(args)
```
`BuildAvaloniaApp` 是官方模板供工具和启动代码调用的连接点。调用 `StartWithClassicDesktopLifetime` 后，程序会创建经典桌面生命周期。框架初始化完成后，`App` 才设置 `MainWindow`。入口点带有 `STAThread`，这与 COM、剪贴板等 Windows API 有关。

这里明确使用桌面假设。iOS 和浏览器使用 `ISingleViewApplicationLifetime`；Android 使用带视图工厂的 `IActivityApplicationLifetime`，因为 Activity 可能被重建。试图在移动端复用这条 `MainWindow` 启动路径，是设计错误，不是缺一个编译开关。

### 小范围测试不启动 UI {#focused-test}

小范围 xUnit 测试可以引用纯状态模块，检查三次添加、重置、下界移除，以及初始值没有被改变。它无需初始化 Avalonia，因为被测函数不依赖工具包。分离边界带来的直接收益，就是这种速度和确定性。

### 准确陈述原生启动结果 {#native-launch-result}

| 检查项 | 本章状态 | 运行后验证什么 | 不能验证什么 |
| --- | --- | --- | --- |
| 项目与锁配置 | 已展示 | 记录的 NuGet 依赖图可以解析 | 未来版本或其他运行时依赖图 |
| Release 构建 | 复制后运行 | F# 与 AXAML 可为 `net10.0` 编译 | 可用的原生窗口或安装包 |
| 纯转换测试 | 已展示 | 被检查的状态转换 | 控件查找、布局、输入、渲染 |
| 原生启动 | 在每个支持的桌面系统运行 | 真实窗口能在该环境启动 | 其他操作系统或安装包 |
| 移动端/安装包/商店 | 未覆盖 | 什么都没有 | 移动生命周期、签名、安装或商店行为 |

构建成功不能验证原生启动。应在每个支持的操作系统上用交互式桌面会话运行应用。记录失败，但不要削弱纯模型或吞掉宿主异常。

## 有意识地选择状态模式 {#state-patterns}

F# 提供多种有用边界；工具包选择并不会强制某一种架构。

| 模式 | 状态与决策 | 视图连接 | 主要压力 |
| --- | --- | --- | --- |
| 手写 MVU | 不可变模型加 `Msg -> Model -> Model * Effect` 函数 | 事件处理器分派，渲染器更新控件 | 屏幕增多后渲染器和副作用调度会重复 |
| MVVM 适配器 | 领域和呈现核心仍保持函数式；适配器暴露属性、通知和命令 | AXAML 绑定连接适配器 | 可变通知接口、命令生命周期、绑定所需的类型形式 |
| Code-behind 编排 | 视图内只有小型局部状态和事件处理器，领域调用委托到外部 | 直接控件引用 | 容易让业务决策、I/O 和取消堆积到窗口中 |
| 纯代码/FuncUI 风格 | UI 树用 F# 表达，通常由消息和状态驱动 | 语言级组合器或 DSL | 社区依赖、API 演进、工具和性能需要单独评估 |

桌面样例在最小有用规模上采用手写 MVU：一个纯更新函数和一个命令式渲染器。更大的应用通常应把模型、更新、副作用描述、视图适配器、导航和组合拆到模块或项目中。

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

副作用执行器负责 HTTP、文件、时间、取消和分派。请求标识符让 `update` 能拒绝来自旧屏幕或旧搜索的迟到结果。不要把即发即弃工作藏在属性 setter 或点击处理器中，让故障和过期结果无处建模。

## XAML 与绑定边界上的 F# {#fsharp-xaml-boundary}

Avalonia 为应用、MVVM 和跨平台提供官方 F# 模板。这一点很重要：项目布局、启动、AXAML code-behind 和模板生成都有经过测试的路径。但它并不表示每个文档示例、设计器、源生成器、第三方控件或 MVVM 包都有同等成熟的 F# 体验。

### 匹配类名并尊重文件顺序 {#classes-and-file-order}

AXAML 的 `x:Class` 必须匹配 F# 中被加载类型的命名空间和类型名。F# 源码顺序必须把定义放在使用者之前。C# 常见的生成式 partial class 约定不会消除这些规则。保持启动和视图代码足够小，才能把故障归因到 AXAML 编译、F# 编译、绑定或原生启动中的具体一层。

命名查找和事件连接适合小视图。对于重复呈现状态、模板、验证和命令，绑定更易扩展，但它会增加一组面向绑定的公开属性和方法。应像对待其他 API 一样设计这组接口。

### Avalonia 12 默认使用编译绑定 {#compiled-bindings}

在 Avalonia 12 中，普通 `{Binding ...}` 默认映射成编译绑定。编译绑定需要 `x:DataType`，XAML 编译器才能拒绝不存在的路径和不兼容类型。只有值确实需要动态解析时才使用 `{ReflectionBinding ...}`，不要把它当作逃避类型错误的总开关。

桌面样例没有绑定表达式，所以不需要 `x:DataType`，也没有验证 view-model 绑定 API。真实绑定试验应包含嵌套模板、双向编辑、命令、验证、设计时数据、计划采用时的裁剪或 AOT，以及团队真实使用的 IDE。

### 适配函数式类型，而不是削弱它们 {#binding-adapters}

- 不可变 F# 记录会暴露可读的 .NET 属性，但不会自动触发 `INotifyPropertyChanged`；可以替换后重新渲染，或用通知适配器包裹。
- 可辨识联合非常适合呈现状态，但 XAML 往往需要 `IsBusy`、`ErrorText` 或选定模板等派生属性；在适配器边缘计算它们。
- 有意识地把 `option` 和 `Result` 转成可见性、可空载荷、错误文本或验证状态；不要让 null 约定悄悄定义领域含义。
- 不可变列表很适合作为模型值。中小集合可以替换 `ItemsSource` 快照；只有增量更新确实重要时才使用可观察适配器。
- 命令是副作用边界。为它提供显式可执行规则、取消、错误路由和生命周期，而不是把网络调用放进匿名 setter。
- 除非耦合是明确的产品决定，否则不要让工具包类型进入可复用领域。

适配器不是对函数式设计的背叛。它阻止绑定引擎的可变、反射、null 和通知约定向内泄漏。

## 线程、取消与生命周期 {#threading-and-lifetime}

Avalonia 使用单一 UI 线程。控件创建、属性访问、布局、渲染和输入都必须通过相应调度器。`Dispatcher.UIThread.Post` 调度工作但不等待；`InvokeAsync` 让调用者等待完成。Avalonia 12 可在不同线程使用多个调度器，但仍不支持多个 UI 线程；可复用控件代码应在适当时优先使用控件自己的调度器。

不要为了“避开 UI 线程”就把天然异步的 I/O 放进 `Task.Run`。等待 I/O，让 CPU 密集工作远离调度器，再把小型结果消息送回视图。切勿在事件处理器中使用 `.Result`、`.Wait()`，也不要执行长时间同步文件或数据库操作。

把每项操作绑定到一种生命周期：窗口、视图、导航项、应用或持久后台任务。生命周期结束时尽力取消，但仍要拒绝过期完成，因为取消存在竞态。把每个异常路由到已建模失败、日志边界或受监督任务；无人观察的即发即弃异常不是用户反馈。

### 桌面与移动端不共享同一种生命周期 {#platform-lifetimes}

桌面应用可以有多个窗口和多种关闭策略。iOS 与浏览器暴露一个主视图。Android 可能重建 Activity，因此 Avalonia 要求一个创建新视图的工厂。移动应用离开前台后，操作系统可能挂起或终止进程。

因此，不要把不可替代的工作仅保存在 `Window`、控件树、view model 实例或静态单例中。在明确检查点持久化草稿和标识符，从持久状态重新还原，并把导航与恢复建模为呈现状态机的显式输入。

## 用端口隔离平台服务 {#platform-services}

通用 .NET 库覆盖网络、序列化、密码学原语和大量存储逻辑。UI 平台在对话框、剪贴板、通知、安全存储、文件选择器、深链、相机、生物识别、分享、后台执行、菜单、托盘图标和权限提示上仍然不同。

在共享项目中定义面向能力的端口：

```fsharp
type PickDocument = CancellationToken -> Task<Result<string option, string>>
type SaveDraft = Draft -> CancellationToken -> Task<Result<unit, string>>
type OpenExternalUri = Uri -> Task<Result<unit, string>>
```

在平台宿主中实现它们，并在组合时注入。用户需要不同恢复方式时，应分别建模“已取消”“不可用”“权限拒绝”和“失败”。不要在各处散布操作系统判断，也不要让核心接口暴露 Android、UIKit、Win32 或 Avalonia 对象。

`OnPlatform` 与 `OnFormFactor` 适合小型资源或布局差异。当行为、权限或生命周期发生变化时，它们不能代替平台服务。

## 桌面端本来就是三个平台程序 {#desktop-platforms}

Avalonia 的一个桌面项目可以面向 Windows、macOS 和 Linux，但每个后端与分发系统仍然不同。支持层级和最低操作系统版本会变化；本章在 2026-08-25 检查了官方矩阵，应用仍应在每次发布前复查。

### Windows {#windows}

Avalonia 直接使用 Win32，其通用桌面目标不需要单独的 Windows .NET 工作负载。发布仍要选择 `win-x64`、`win-arm64` 或其他受支持 RID；框架依赖还是自包含交付；安装器技术；应用身份；图标；文件关联；签名；更新策略以及企业部署行为。

应测试键盘与高 DPI、多显示器、剪贴板和对话框、受支持时的远程会话、Windows 无障碍、干净安装、每用户/每机器数据、升级、修复和卸载。在 macOS 上构建出的安装包只是交叉编译证据，不是 Windows 运行时证据。

### macOS {#macos}

默认 Avalonia macOS 后端自带原生库，无需 `net10.0-macos` 工作负载即可构建。分发仍需要结构正确的 `.app` 包和 `Info.plist`；常规外部分发要求代码签名和公证，而即使包结构可以跨平台生成，这些签名步骤仍需要 macOS/Xcode 工具。

若支持 Apple Silicon 与 Intel，应分别发布和测试。验证原生菜单、快捷键、文件对话框、沙箱或 entitlement 选择、无障碍、应用身份、隔离/Gatekeeper 行为、升级和卸载。桌面样例的 `-6661` 启动结果明确不是通过的 macOS 冒烟测试。

### Linux {#linux}

Avalonia 默认以 X11 为目标；Wayland 通常通过 XWayland 运行，除非显式选择实验性原生 Wayland 后端。Linux 发布范围必须写明发行版、版本、CPU 架构、显示后端、GPU/软件渲染路径、桌面环境、原生库、字体、安装包格式和更新渠道。

成功构建 `.deb` 不能证明 RPM、Flatpak、Snap、AppImage 或解压归档。应在干净的受支持镜像上测试桌面入口、图标、可执行权限、原生依赖缺失、区域设置/字体、AT-SPI 无障碍、安装、升级和移除。

## 移动端支持是一张项目图，不是复选框 {#mobile-boundary}

官方 Avalonia 跨平台结构包含共享 Core 项目、独立 Desktop、Android、iOS 以及可选 Browser 宿主。视图与呈现逻辑可以放进 Core；每个宿主提供自己的目标框架、入口点、SDK 集成、元数据、权限、原生服务、签名和部署路径。

在已检查的支持矩阵中，Avalonia 移动目标要求 .NET 10，Android 与 iOS 支持跟随 .NET MAUI 平台生命周期。这些约束具有时效性，因此应固定 SDK/工作负载集合，并在每个发布周期复查矩阵。

### Android {#android}

Android 宿主是一个 .NET Android 项目，`MainActivity` 派生自 Avalonia 的 Activity 基类。构建它需要 .NET Android 工作负载、Android SDK、JDK 和匹配的目标组件。运行时证据需要模拟器和有代表性的真机，而不是只构建共享项目。

测试 Activity 重建、返回导航、配置变化、进程死亡、权限、深链、键盘与 inset、触摸、无障碍、离线行为、后台限制、包身份、架构拆分、签名、升级和商店策略。需要跨重建保存的状态不能只留在 Activity 实例中。

### iOS 与 iPadOS {#ios}

iOS 宿主是带 Avalonia app delegate 和当前 scene 初始化方式的 .NET iOS 项目。工具链需要 iOS 工作负载；运行与设备验证需要带 Xcode 的 macOS 硬件。真机增加证书与 provisioning，商店分发还增加签名归档、App Store 元数据、审核和更新约束。

测试模拟器和真机路径、前后台切换、内存压力、权限、安全区域、旋转、键盘、适用时的触摸与指针、VoiceOver、深链、离线恢复、包身份、签名、升级和商店交付。Mac Catalyst 是独立的 UIKit 路径，不是默认 Avalonia macOS 桌面目标所用的同一个后端。

### F# 支持需要两句话 {#fsharp-support-boundary}

第一，Avalonia 官方模板明确为桌面、MVVM 和跨平台解决方案列出 F#。第二，周边移动 SDK、大多数原生示例、商店工具和许多第三方库仍主要使用 C#。两者可以同时成立。

优先使用惯用的 F# 共享核心，以及被所选工具链验证过的最薄宿主。用一个小型 C# 平台适配器，成本往往低于强迫 F# 适应生成器或设计器约定。若直接 F# 宿主在选定模板与 IDE 中运行良好，可以保留，但仍要维持可复现的命令行构建和设备测试。

.NET MAUI 是另一个面向 Android、iOS、Mac Catalyst 和 Windows 的 UI 产品。其官方文档和当前模板以 C#/XAML 为主。F# 库仍可驱动 MAUI 应用，社区 F# 模板也可能适用。直接用 F# 编写 MAUI UI 需要单独评估；Avalonia 的结果不能代替这项验证。

## 无障碍、输入与响应式布局都是行为 {#accessible-responsive-ui}

Avalonia 内置控件通过自动化对等体向平台无障碍 API 暴露语义，应优先使用这类语义控件。可见内容不足时，再添加名称、标签关联、帮助文本、实时通知设置或稳定的自动化 ID。自定义控件需要明确的自动化行为。

测试键盘遍历、焦点顺序与恢复、快捷键、屏幕阅读器、对比度、大字号、缩放或显示比例、高 DPI、适用时的减少动态效果、错误播报，以及完全不用指针的输入。不能只用颜色传递含义。

响应式设计不只是检测“移动端”。应让布局响应可用空间；只有交互确实不同时才使用 form-factor 或平台条件。测试较长英文、紧凑中文标签、本地化膨胀、支持时的从右到左文本、窄窗口、触摸目标、软件键盘、安全区域、旋转和可调整大小的桌面窗口。

即使共享 AXAML 完全相同，无障碍和本地化缺陷仍是平台缺陷。平台矩阵中应包含有代表性的辅助技术和输入设备。

## 从纯逻辑逐层验证到发布包 {#testing-evidence-ladder}

先用成本最低的有效层次：

1. **纯测试与适配器测试：** 状态、验证、导航、过期结果拒绝、副作用、通知、命令、取消和平台端口。
2. **XAML 与无头测试：** 资源、编译绑定、模板、真实控件、布局、输入和视觉/自动化树。
3. **原生冒烟：** 在每个受支持后端检查键盘/指针/触摸、对话框、剪贴板、缩放、生命周期与关闭。
4. **发布与安装包测试：** 每个 RID、原生资产、签名、干净安装、脱离 SDK 启动、升级、回滚和卸载。
5. **设备与商店测试：** 生命周期、权限、深链、断网、无障碍、性能、分发、崩溃和更新。

无头 CI 不能认证原生后端、驱动、打包、签名或商店。每项结果都应记录 OS、CPU、安装包、区域、显示比例、输入方式、辅助技术、日期与提交。

## 发布不等于打包或上线 {#publishing-and-release}

`dotnet build` 负责编译；`dotnet publish` 创建部署布局；打包加入平台结构与元数据；签名建立发布者身份与完整性；分发通过安装器、软件仓库、MDM 或商店搬运制品；上线还包括滚动发布、观察、支持、更新和回滚。

### 为每个目标选择运行时交付方式 {#runtime-delivery}

框架依赖发布更小，并使用目标上兼容的已安装 .NET 运行时及其服务补丁。自包含发布携带所选运行时，获取运行时安全更新时必须重新构建。运行时标识符选择操作系统和架构；从 .NET 8 开始，RID 本身不再隐含自包含，因此要显式声明选择。

单文件、裁剪、ReadyToRun 和 Native AOT 会改变大小、启动、反射、诊断和兼容性。Avalonia 的 AOT 指南要求编译绑定。只有在警告清零，并针对绑定、序列化、依赖注入、插件、资源、原生库和每个打包架构完成运行测试后，才启用这些选项。

不要在发布多个 RID 时意外覆盖同一份共享锁图。使用明确的还原输入或隔离的输出/锁策略，并确认提交的依赖图保持不变。

### 设计安装、更新与恢复 {#install-update-recovery}

定义应用 ID、版本语义、数据目录、设置 schema、缓存策略、日志、崩溃报告、文件关联、协议处理器、证书、entitlement 和更新渠道。绝不能假设工作目录可写或稳定。不要把机密放进安装包或日志；适当时使用平台凭据存储。

更新可能同时改变可执行文件和用户数据。采用向后兼容的设置/数据迁移、原子写入、不可替代本地数据的备份、降级策略，以及启动失败时的恢复路径。测试干净安装、从每个受支持前序版本更新、更新中断、回滚或前向修复，以及带明确用户数据保留策略的卸载。

## 开展范围受限的采用试验 {#adoption-spike}

限定时间验证一个有代表性的端到端小样：

- 官方 F# 模板、锁定还原，以及 CLI/IDE 构建、编辑和调试流程；
- 一个不可变领域工作流和一个在代表性数据量下有压力的屏幕；
- 取消、过期完成、离线/重试/重启，以及一次平台权限拒绝；
- 键盘/触摸/焦点、屏幕阅读器、大字号、本地化和窄布局；
- 目标硬件性能，以及每个 RID 的发布、干净安装、签名、升级和卸载；
- 适用时的真机生命周期，以及维护、许可、支持、控件生态和退出条件。

比较实现与运维成本，而不是截图相似度。只有当团队能在产品真实平台矩阵内构建、诊断、分发、更新和支持它时，一个框架才算可接受。

## 练习 {#exercises}

### 练习 1：为三个产品选择 UI 边界 {#exercise-01}

分别评估以下产品：

1. 一套仅限 Windows 的交易工作站必须复用成熟 WPF 控件和企业分发，领域计算用新 F# 编写。
2. 一套离线现场工具需要 Windows、macOS 和两个指定 Linux 发行版，还要支持键盘、触摸和本地文档。它没有手机版。
3. 一款消费应用需要 Android 与 iOS、相机、推送通知、深链、后台上传、商店分发，以及一个小型桌面查看器。

为每个产品记录首选候选、被拒方案、尚未完成的验证，以及改用其他方案的触发条件。比较 Avalonia、围绕 F# 核心的 C# 平台壳、.NET MAUI 和浏览器界面；三个产品可以得到不同答案。


::: details 参考答案

关键问题不是“哪个框架能共享最多代码”，而是“哪个边界能减少平台专属代码与工具链，同时让重要产品决策保持可测试”。

#### A. 仅限 Windows 的交易工作站 {#windows-workstation}

**首选候选：** 保留或创建 WPF/Windows UI 壳，把计算、验证、订单状态转换和服务契约放进 F# 库。

需求已经包含两个决定性约束：范围仅限 Windows，并且存在成熟 WPF 控件。用 Avalonia 重写这些控件，是为了消除产品根本不想消除的平台限制而承担风险。若必需 Windows 功能或供应商路线图要求，WinUI 可做单独的现代化验证性试验（spike），但它并不会自动成为现有可用 WPF 资产的升级方案。

在 UI 壳与 F# 之间使用小型面向对象 API：

- F# 包含不可变市场快照、已验证标识符、定价函数、命令、结果和支持取消的服务端口；
- UI 适配器把联合与结果转成属性、命令、通知和可观察集合增量；
- C# 处理 XAML 代码生成、控件供应商集成、窗口与调度器服务，以及安装器专用钩子；
- 序列化与线程契约在边界两侧都接受测试。

**不作为首选的方案：**

- Avalonia 会增加控件替换风险，目前却没有跨平台收益。
- 浏览器界面可能不适合现有控件、延迟、多窗口流程或企业集成。
- 可以研究直接用 F# 编写 WPF UI，但领域层使用 F# 并不要求 UI 也使用 F#。

**尚需验证：** 代表性供应商控件、高频更新、UI 线程预算、无障碍、多显示器与 DPI、身份验证、崩溃恢复、企业安装器、签名更新，以及从现有安装版本升级。

**反转条件：** 若 WPF 控件或受支持的 Windows 版本阻碍路线图，或出现有预算的 macOS 或 Linux 需求，就重新评估 WPF。使用同一个 F# 核心，对比一条 Avalonia 纵向切片与另一条 Windows 现代化路径。

#### B. 离线跨平台现场工具 {#field-tool}

**首选方案：** 使用 Avalonia 桌面应用，共享 F# 领域与呈现核心，并先提供一个桌面宿主。然后分别建立 Windows、macOS 和指定 Linux 发行版的打包流程。

产品恰好需要 Avalonia 桌面宿主覆盖的平台，也不需要手机生命周期或商店。键盘加触摸、本地文档符合桌面 UI，但仍需响应式控件、更大触摸目标、文件选择器适配器、耐久原子存储、冲突/恢复策略和平台测试。

保持以下边界：

- 纯 F# 状态处理文档身份、验证、编辑历史、同步状态和重试决策；
- 持久化端口定义原子保存、备份、迁移和写入中断恢复；
- 平台适配器处理选择器、最近文档集成、协议与文件关联、安全凭据和外部链接；
- Avalonia 视图处理布局和输入，并使用编译绑定或指定的渲染器；
- 打包项目或流水线阶段处理各 RID、元数据、签名、安装器和更新渠道。

**不作为首选的方案：**

- WPF 无法覆盖 macOS 或 Linux。
- MAUI 不支持桌面 Linux，还会引入产品不需要的移动工具链。
- 只有离线文件、设备集成、更新和企业分发都优于桌面安装包时，浏览器或 PWA 才值得采用。

**尚需验证：**

- 最复杂的文档与列表屏幕，以及大数据量；
- 离线重启与文件锁；
- 字体、区域设置、键盘、触摸和屏幕阅读器；
- Windows、macOS 与指定 Linux 目标的原生启动，包括选定的 X11 或 XWayland 范围；
- 签名安装包、干净安装、升级、回滚和现场设备性能。

**反转条件：** 若原生文档集成、Linux 后端行为、控件性能或打包成本超过试验预算，则保留 F# 核心，并比较原生壳或浏览器界面。

#### C. 消费移动应用与配套查看器 {#consumer-mobile}

**首选候选：** 暂不选择共享 UI。在同一个 F# 核心之上，分别做两份移动纵向切片：(1) Avalonia 跨平台加薄 Android/iOS 宿主；(2) C# 移动壳——MAUI 或原生平台 UI。桌面查看器另作较小决定。

相机、推送通知、深链、后台上传、权限、进程死亡、签名和商店主导这个产品。它们是平台能力与生命周期契约，不是绘图原语。只有这些路径在设备上真正可用时，高共享 AXAML 比例才有价值。

比较应测量：

| 维度 | Avalonia 跨平台候选 | C# 移动壳候选 |
| --- | --- | --- |
| 共享视图 | 可能很高，包括桌面查看器 | 移动视图采用 MAUI/原生惯例；桌面端可独立 |
| F# 核心 | 高 | 高 |
| 原生集成 | Avalonia 宿主适配器加平台 SDK | MAUI handler/插件或直接原生 API |
| F# UI 体验 | Avalonia 有官方 F# 模板；周边样例仍常见 C# | F# 库直接；官方 UI/工具链呈 C# 形态 |
| 验证成本 | Android/iOS 宿主、所有移动路径及桌面宿主 | 两个移动宿主加独立查看器边界 |

**不作为首选的方案：** WPF 无法交付移动端。不能假设纯浏览器界面支持后台上传、推送、相机和商店。“一个代码库”不能替代真机测试。

**尚需验证：**

- 权限拒绝、采集中断和上传中断；
- 各生命周期状态下的通知点击、深链、离线队列和重复提交；
- Android Activity 重建，以及 iOS 挂起或终止；
- 设备无障碍、能耗、内存与启动；
- 签名、分阶段商店发布、崩溃符号和更新兼容性。

**反转条件：** 只有当共享 UI 实质降低总成本且所有关键原生路径仍可维护时才选 Avalonia。若原生集成、工具链或平台体验明显更安全，则选择 C# 壳。无论结果如何，F# 核心都保留。

:::

### 练习 2：把桌面样例变成桌面发布 {#exercise-02}

为把桌面样例变成受支持的 Windows/macOS/Linux 应用，准备两份交付物：

- **应用方案：** 模块边界、异步副作用、持久化、设置迁移、无障碍和本地化。
- **验证矩阵：** 无头测试、原生冒烟、运行时标识符、框架依赖与自包含交付、原生资产、安装包、签名或公证、干净安装、更新、回滚、崩溃诊断和准确平台矩阵。

在后续运行取得更强证据之前，继续在矩阵中保留现有 `-6661` 原生启动结果。


::: details 参考答案

先建立验证清单。本章展示锁定 Avalonia 12.1.1 的项目、`net10.0` 源码、AXAML 与纯状态测试。复制后，必须实际运行还原、Release 编译、测试与原生启动，才能声称它们通过。Windows、macOS、Linux、发布输出、安装包、签名、安装、更新和无障碍都需要独立结果。

#### 在不丢掉小核心的前提下重构 {#desktop-structure}

一套合乎比例的目标结构是：

```text
DesktopApp.Domain        不可变规则与已验证值
DesktopApp.Presentation  Model、Msg、update、Effect 描述
DesktopApp.Core          共享 Avalonia 视图与 UI 适配器
DesktopApp.Desktop       AppBuilder、生命周期、平台组合
DesktopApp.Tests         纯测试与适配器测试
DesktopApp.UiTests       无头控件/布局/输入测试
packaging/               每个受支持 OS/包格式一条维护轨道
```

不必立即变成七个项目。物理拆分之前，边界就已经重要。先把 `Model`、`Message` 和 `Counter.update` 从窗口文件移出；只有依赖方向或平台差异证明值得时，才引入更多模块或项目。

#### 加入受监督的副作用循环 {#desktop-effects}

用应用 store 取代局部可变计数器，由它管理当前模型、串行消息处理、外部操作和视图订阅。每项操作都接收取消令牌，并把成功、失败或取消报告为消息。每个请求都有 ID，只有匹配当前模型状态的完成结果才会被接受。

为文档、设置、安全凭据、对话框、外部链接、更新检查和崩溃报告使用端口，把实现细节留在桌面组合根。只持久化耐久应用数据；重新创建视图对象和派生呈现值。

对于本地文档：

1. 验证并序列化到目标文件系统中的新临时文件；
2. 按耐久契约需要进行 flush；
3. 在平台/文件系统支持时原子替换旧文件；
4. 对不可替代数据保留可恢复备份或日志；
5. 记录 schema 版本并测试每条受支持迁移；
6. 分别暴露权限、冲突、磁盘满、取消和损坏数据结果。

#### 补齐视图与无障碍缺口 {#desktop-view-quality}

若应用从桌面样例渲染器转向 MVVM，应采用带显式 `x:DataType` 的编译绑定。加入稳定自动化 ID 与标签、键盘导航与快捷键、焦点恢复、错误/实时播报、对比度、大字号、高 DPI、适用时的减少动态效果，以及屏幕阅读器检查。

外置字符串并测试英文、中文、长翻译、缺失字形、数字/日期格式和窄布局。不能从指针点击推断触摸支持；应在代表性硬件上测试触摸目标、滚动、选择、拖拽和软件键盘。

#### 逐层完成测试与发布矩阵 {#desktop-release-matrix}

| 层次 | Windows | macOS | 指定 Linux 目标 |
| --- | --- | --- | --- |
| 锁定构建 | `net10.0` 加所选 RID | `net10.0` 加受支持的 `osx-arm64`/`osx-x64` | 按需 `linux-x64`/`linux-arm64` |
| 无头 | 绑定、布局、输入、自动化树 | 同一共享测试套件 | 同一共享测试套件 |
| 原生冒烟 | Win32、DPI、键盘、对话框、关闭 | 已解锁原生后端、菜单、快捷键、对话框 | X11/XWayland 或显式选择后端、桌面环境 |
| 安装包 | 所选签名安装器 | `.app` 包、身份、签名、公证、所选归档 | 明确命名的 `.deb`/RPM/其他格式及原生依赖 |
| 生命周期 | 安装、首次运行、更新、回滚、卸载 | 隔离/Gatekeeper、安装、更新、回滚、卸载 | 干净发行版镜像、安装、更新、回滚、移除 |
| 无障碍 | Windows 屏幕阅读器与键盘 | VoiceOver 与键盘 | AT-SPI 屏幕阅读器与键盘 |

从锁定输入发布每个 RID。明确选择框架依赖型或自包含输出。若选择自包含，要为 .NET 安全补丁安排重建周期。只有启动时间或体积目标已经测量且确有需要时，才测试单文件、裁剪、ReadyToRun 或 AOT。兼容性警告应让构建失败，并且必须实际运行打包制品。

#### 打包、更新、观察与恢复 {#desktop-operations}

给应用稳定 ID、语义显示版本与单调递增构建版本、确定的数据/日志/缓存位置、签名更新元数据和渠道策略。在平台要求处签名和公证。生成并保留校验和、依赖清单、符号以及源码/提交来源。

为启动阶段、已处理/未处理故障、更新状态、迁移版本、性能和功能结果加入检测，但不收集文档内容或机密。明确崩溃报告的同意与隐私行为。

测试干净安装；从每个受支持前序版本升级；下载、安装和迁移中断；不兼容降级；回滚或前向修复；以及适用时同时覆盖“保留用户数据”和“移除数据”策略的卸载。只有当旧数据格式仍能打开用户状态时，最后已知良好包才有意义。

#### 写清原生验证的上限 {#desktop-evidence-limit}

在已解锁交互式会话中重跑 macOS 冒烟测试。记录操作系统、CPU、显示器、区域设置、显示比例、提交和结果。若通过，只能写“此构建在此 macOS 目标显示并响应操作”，不能写“所有 macOS 都能用”。若 `-6661` 在有效显示会话中再次出现，先缩减到官方模板，再用最小复现调查配置、依赖和框架问题。

不能用 macOS 结果填满 Windows 或 Linux 行。只发布团队准备支持、诊断、修补和退役的矩阵行。

:::

### 练习 3：把架构扩展到移动端 {#exercise-03}

为预约客户端设计 Core/Desktop/Android/iOS 项目图，并按四类问题组织设计：

- **共享行为：** 编辑草稿、提交预约、打开确认深链，并通过平台选择器导出收据。
- **生命周期与状态：** 跨旋转或 Activity 重建生存，在进程终止后恢复，持久化检查点，并拒绝过期结果。
- **平台边界：** 定义 F# 状态、消息和副作用，以及平台端口、权限结果与宿主语言选择。
- **交付证据：** 锁定工作负载，运行模拟器与设备测试，签名，分阶段发布到商店，收集遥测，并定义反转条件。

最后准确说明桌面构建能为移动目标证明什么。


::: details 参考答案

使用官方推荐的多项目结构，并保留清晰的决策点：

```text
Booking.Client.Domain
Booking.Client.Presentation
Booking.Client.Core          共享 Avalonia 视图与适配器
Booking.Client.Desktop       经典桌面生命周期
Booking.Client.Android       .NET Android + Activity/视图工厂
Booking.Client.iOS           .NET iOS + app delegate/scenes
Booking.Client.Tests         纯工作流、持久化、端口契约
Booking.Client.UiTests       共享无头视图行为
```

应用应固定 .NET SDK、Avalonia 包、NuGet 锁、工作负载清单/版本集合、Android SDK/JDK 要求和 Xcode 兼容性。平台 CI 镜像是工具链的一部分，不是不可见基础设施。

#### 建模草稿与提交状态 {#mobile-state}

一套紧凑共享模型可以区分：

```fsharp
type Submission =
    | Editing
    | Submitting of operationId: Guid
    | OutcomeUnknown of operationId: Guid
    | Confirmed of bookingId: string
    | Rejected of message: string

type Msg =
    | DraftChanged of DraftChange
    | SubmitRequested
    | SubmitFinished of operationId: Guid * Result<string, SubmitError>
    | AppSuspending
    | AppResumed
    | ConfirmationLinkOpened of Uri
    | ReceiptExportRequested
    | ReceiptExportFinished of Result<unit, ExportError>
```

`update` 验证草稿、分配一个稳定操作 ID、忽略旧 ID 的完成，并描述保存/提交/导出副作用。服务器必须尊重操作 ID 才能实现幂等；网络结果未知后，UI 无法凭空制造恰好一次提交。

在有意义编辑后用防抖持久化草稿，并在挂起/导航检查点保存。发送前持久化操作 ID。恢复或冷启动时，加载耐久状态，并先与服务器核对 `Submitting`/`OutcomeUnknown`，再允许产生新身份。绝不序列化控件、Activity 实例、取消令牌或打开流。

#### 定义平台端口与结果 {#mobile-ports}

共享项目可以定义以下端口：

- 带原子替换、迁移、损坏恢复和测试替身的草稿存储；
- 带取消和幂等身份的已认证预约提交与状态查询；
- 返回已完成、用户取消、权限拒绝、不可用或失败的收据导出；
- 把深链解析写成纯函数，把宿主注册留在外部；
- 能改善体验但绝不声称请求成功的连接状态提示；
- 带同意、脱敏、关联和离线缓冲策略的遥测。

Android 实现 Activity 入口、Intent、运行时权限、文档选择器、安全存储、通知渠道和后台调度。iOS 实现 scene/app delegate、URL 处理、权限说明、文档/分享 UI、Keychain 访问、通知和允许的后台模式。每个适配器把原生回调转换成共享消息。

#### 务实选择宿主语言 {#mobile-host-languages}

从官方 F# Avalonia 跨平台模板开始，并从 CLI 构建两个宿主。若生成项目、IDE、工作负载、绑定、原生回调、签名和设备路径都符合常规流程，就保留 F# 宿主。若平台源生成、样例或 SDK 约定让极小 C# 宿主明显更安全，就使用它。无论哪种方式，F# 项目都继续包含领域与呈现状态。

不要把原生 SDK 类型重写成复杂的语言中立框架。保持适配器薄、测试其契约，并在用户期望处允许平台专用行为。

#### 分别验证生命周期与分发 {#mobile-evidence-matrix}

| 场景 | Android 检查 | iOS 检查 |
| --- | --- | --- |
| 构建 | 锁定 `net10.0-android` 工作负载与目标 SDK | 锁定 `net10.0-ios` 工作负载与兼容 Xcode |
| 基础运行 | 受支持模拟器加代表性真机/架构 | 当前模拟器加代表性 iPhone/iPad 真机 |
| 重建 | 旋转/配置变化与 Activity 重建 | scene 转换及适用时的视图重建 |
| 进程丢失 | 后台杀进程、冷恢复、核对进行中操作 | 终止/挂起、冷恢复、核对进行中操作 |
| 链接/通知 | 冷启动、后台、前台 Intent 与通知点击 | 冷启动、后台、前台链接与通知响应 |
| 导出/权限 | 允许、拒绝、永久拒绝、取消、provider 不可用 | 允许、拒绝、取消、分享/文档目标不可用 |
| 无障碍/输入 | TalkBack、开关/键盘/触摸、大字体 | VoiceOver、开关/键盘/触摸、Dynamic Type |
| 分发 | 签名内部轨道、分阶段发布、升级、回滚计划 | provisioning 归档、TestFlight/分阶段发布、升级、回滚计划 |

还要测试：

- 离线、慢网与切网；
- 重复点击，以及服务器接受后的超时；
- 时钟变化、低存储、本地化和内存压力；
- 启动与交互预算；
- 崩溃符号上传、隐私披露与遥测查询。

商店审核只能验证分发流程，不能验证业务正确性。

发布一组不可变后端契约和兼容客户端序列。移动客户端更新缓慢，因此服务器必须在声明窗口内支持旧应用版本。功能开关和最低版本门槛需要离线与失败策略，且不能破坏草稿。

#### 陈述桌面推论的上限 {#mobile-inference-limit}

桌面样例构建只能验证编译器能构建当前桌面项目，以及纯计数器转换通过。抽出与移动平台无关的 Domain 和 Presentation 项目后，这些纯测试也能验证共享逻辑。

它没有验证以下内容：

- `net10.0-android` 或 `net10.0-ios` 还原，以及工作负载兼容性；
- 宿主启动、Activity 或 scene 生命周期，以及这些目标上的 AXAML；
- 权限、原生服务、触摸和无障碍；
- 包元数据、签名、真机、商店与生命周期恢复。

每一项都需要自己的矩阵行。

**反转条件：** 出现以下任一情况，就放弃共享 Avalonia UI，但保留 F# 核心：

- 关键相机、后台或通知集成缺乏可维护路径；
- 设备体验或无障碍未达到产品阈值；
- 平台回归问题主导交付；
- 打包与商店工作超过预算；
- 团队无法诊断原生故障。

薄 C# 或原生 UI 壳是预设出口，不是重写业务规则。

:::


## 资料来源 {#sources}

- [Avalonia 文档与 Avalonia 12 指南](https://docs.avaloniaui.net/)
- [Avalonia 支持平台矩阵](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia 应用生命周期](https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes)
- [NuGet：Avalonia 包版本](https://www.nuget.org/packages/Avalonia)

第 44 章转向另一种宿主：在 Unity 中使用 F# 领域代码。Unity 序列化、组件生命周期、IL2CPP 与 Player 构建仍留在专用适配层中。
