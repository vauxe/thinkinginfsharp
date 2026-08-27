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

## 避免常见 UI 错误 {#common-mistakes}

- 把共享构建当作平台验证，或在说清平台、原生 API、输入、商店与更新策略前选择框架。
- 把业务决策和 I/O 堆进 code-behind，或把可变 view model 当成领域模型。
- 直接向 XAML 暴露联合/option，或为隐藏类型不匹配而禁用编译绑定。
- 阻塞调度器、启动无人监管任务，或在导航/更新后接受迟到结果。
- 把不可替代状态留在 UI/进程本地存储，或散布 OS 判断而不注入平台能力。
- 假定控件拥有相同生命周期、无障碍和原生行为，或把固定桌面画布称作移动支持。
- 自定义可点击视觉却没有语义，或用无头测试证明原生渲染/打包。
- 把单一架构、可变依赖图或未签名目录称为上线；启用裁剪/单文件/AOT 却不测试反射与原生资产。
- 忽略数据迁移、更新中断、回滚、卸载或保留策略。
- 在极薄 C# 适配器更安全时仍强迫每个宿主使用 F#。
- 从一次自动化失败或一次本地成功推出普遍结论。

## 练习 {#exercises}

### 练习 1：为三个产品选择 UI 边界 {#exercise-01}

分别评估以下产品：

1. 一套仅限 Windows 的交易工作站必须复用成熟 WPF 控件和企业分发，领域计算用新 F# 编写。
2. 一套离线现场工具需要 Windows、macOS 和两个指定 Linux 发行版，还要支持键盘、触摸和本地文档。它没有手机版。
3. 一款消费应用需要 Android 与 iOS、相机、推送通知、深链、后台上传、商店分发，以及一个小型桌面查看器。

为每个产品记录首选候选、被拒方案、尚未完成的验证，以及改用其他方案的触发条件。比较 Avalonia、围绕 F# 核心的 C# 平台壳、.NET MAUI 和浏览器界面；三个产品可以得到不同答案。

### 练习 2：把桌面样例变成桌面发布 {#exercise-02}

为把桌面样例变成受支持的 Windows/macOS/Linux 应用，准备两份交付物：

- **应用方案：** 模块边界、异步副作用、持久化、设置迁移、无障碍和本地化。
- **验证矩阵：** 无头测试、原生冒烟、运行时标识符、框架依赖与自包含交付、原生资产、安装包、签名或公证、干净安装、更新、回滚、崩溃诊断和准确平台矩阵。

在后续运行取得更强证据之前，继续在矩阵中保留现有 `-6661` 原生启动结果。

### 练习 3：把架构扩展到移动端 {#exercise-03}

为预约客户端设计 Core/Desktop/Android/iOS 项目图，并按四类问题组织设计：

- **共享行为：** 编辑草稿、提交预约、打开确认深链，并通过平台选择器导出收据。
- **生命周期与状态：** 跨旋转或 Activity 重建生存，在进程终止后恢复，持久化检查点，并拒绝过期结果。
- **平台边界：** 定义 F# 状态、消息和副作用，以及平台端口、权限结果与宿主语言选择。
- **交付证据：** 锁定工作负载，运行模拟器与设备测试，签名，分阶段发布到商店，收集遥测，并定义反转条件。

最后准确说明桌面构建能为移动目标证明什么。

[阅读本章练习答案](../solutions/ch-43-avalonia-desktop-mobile)。

## 资料来源 {#sources}

- [Avalonia 文档与 Avalonia 12 指南](https://docs.avaloniaui.net/)
- [Avalonia 支持平台矩阵](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia 应用生命周期](https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes)
- [NuGet：Avalonia 包版本](https://www.nuget.org/packages/Avalonia)

第 44 章转向另一种宿主：在 Unity 中使用 F# 领域代码。Unity 序列化、组件生命周期、IL2CPP 与 Player 构建仍留在专用适配层中。
