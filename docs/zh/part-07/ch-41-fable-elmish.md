---
title: "第 41 章：Fable、Elmish 与浏览器应用"
description: "根据运行时约束、状态复杂度、渲染需求、互操作和部署验证选择浏览器架构。"
translationKey: part-07/ch-41-fable-elmish
---

# 第 41 章：Fable、Elmish 与浏览器应用 {#overview}

在 Fable 项目中，源语言仍是 F#：记录、可辨识联合、模式匹配、模块、函数和类型推断照常工作。但运行时不再是 .NET。Fable 把受支持的 F# 与库功能转译为 JavaScript，再由浏览器按自身的安全、调度、数值、打包和 API 规则执行。

这个区别能阻止两个相反错误。.NET 团队不能假设每个 BCL 或 NuGet API 都能在浏览器中工作。JavaScript 团队也不能认为只用 Fable 就不再需要 npm、打包器、DOM 知识、可访问性或浏览器诊断。有用的问题是：“哪些逻辑能从 F# 建模获益，哪些边界属于 JavaScript 或浏览器，这个界面实际需要多少状态架构？”

## Fable 改变目标，不改变源语言 {#target-runtime}

浏览器 Fable 管道有四种彼此不同的产物：

```text
F# 源码 + .fsproj + 兼容的 NuGet 包
  -> Fable 生成的 JavaScript 模块
  -> JavaScript 打包器生成的生产资源
  -> 在 Web API 与浏览器策略下执行
```

F# 编译器仍会对项目做类型检查。随后 Fable 转译受支持的程序。Vite 或其他 JavaScript 工具解析模块、摇树优化、压缩、加哈希并输出资源。浏览器加载这些资源；它不会加载项目的 `.dll` 或启动 CLR。

这是转译到目标生态，不是远程控制某个 .NET 进程，也不同于 .NET WebAssembly。Fable 还能以其他语言为目标，但本章和浏览器样例只验证浏览器里的 JavaScript。

### 把三个兼容性问题分开 {#three-compatibility-questions}

对每个依赖或 API，都要问：

1. 普通 F# 是否能对这段源码完成类型检查？
2. Fable 是否支持或能为 JavaScript 转译所用的语言功能和库 API？
3. 输出的 JavaScript 是否在目标浏览器和打包配置中工作？

一个 `netstandard2.0` 资源本身既不能回答第二问，也不能回答第三问。反过来，一个 JavaScript 库即使没有 .NET 实现，也可以通过类型化 Fable 绑定很好地工作。

## 浏览器样例：一个最小浏览器边界 {#verified-slice}

浏览器样例有意避开 React 和 Elmish 包。它隔离出最小实用路径：F# 源码变成生产 JavaScript 包，绑定可访问的 DOM 事件并更新可见状态。真实应用仍须在其支持的浏览器中测试该产物。

### 锁定项目依赖 {#locked-project}

```xml:line-numbers [FableSample.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="App.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Fable.Browser.Dom" Version="2.20.0" />
    <PackageReference Include="Fable.Core" Version="5.2.0" />
  </ItemGroup>
</Project>
```
要在应用中复现这个样例，应记录以下各自演进的输入：

- .NET SDK 与 F# 语言版本；
- 本地 Fable 工具版本；
- `packages.lock.json` 中的 Fable.Core 与 Fable.Browser.Dom；
- JavaScript 锁文件中的包管理器与 Vite 版本；
- 自动与手工验收测试所用的浏览器版本。

编译器、Fable.Core 和浏览器绑定有各自的发布节奏。按看起来相同的主版本或次版本去配对不是兼容策略；要还原它们声明的依赖图、编译并在目标上运行。

### 先读 F#，再读生成的 JavaScript {#sample-code}

```fsharp:line-numbers [App.fs]
module FableSample.App

open Browser.Dom

type Model = { Count: int }

type Message =
    | Increment
    | Reset

let initialModel = { Count = 0 }

let update message model =
    match message with
    | Increment -> { model with Count = model.Count + 1 }
    | Reset -> initialModel

let private elementById id =
    match document.getElementById id with
    | null -> failwith $"Required element #{id} was not found."
    | element -> element

let private countOutput = elementById "count"
let private incrementButton = elementById "increment"
let private resetButton = elementById "reset"
let mutable private model = initialModel

let private render () =
    countOutput.textContent <- $"Count: {model.Count}"

    if model.Count = 0 then
        resetButton.setAttribute ("disabled", "")
    else
        resetButton.removeAttribute "disabled"

let private dispatch message =
    model <- update message model
    render ()

incrementButton.addEventListener ("click", fun _ -> dispatch Increment)
resetButton.addEventListener ("click", fun _ -> dispatch Reset)

render ()
document.documentElement.setAttribute ("data-fable-ready", "true")
```
`Model`、`Message`、`initialModel` 和 `update` 都是普通 F#。`update` 函数是确定性的：相同消息与模型产生相同的下一模型。它不知道元素、点击或渲染。

`elementById`、事件注册、可变的当前模型和 `render` 构成执行外部操作的浏览器适配层。这里的修改是有意的局部运行时状态，不是在邀请你让领域转换变得隐式。缺少必需标记时会在启动期间失败，而不是留下功能不完整的页面。

最后的属性是供浏览器冒烟使用的就绪契约。只有找到元素、绑定监听器并渲染初始模型后才会设置它。

### 这些检查覆盖什么 {#sample-evidence}

生产命令执行锁定的 .NET 工具与包还原、不复用缓存的 Fable 编译，以及 Vite 生产构建。当前输出把 15 个模块转换为一个 HTML 入口和一个带哈希的 JavaScript 资源。具体文件名与大小由构建产生，不属于公开应用契约。

自动冒烟只提供 `dist`，并通过锁定的 Playwright 客户端启动已安装的 Chrome。它等待 Fable 就绪，验证 `0 -> 3 -> 0` 和重置状态，拒绝浏览器与网络错误，并检查 360 像素宽度下的页面溢出。独立 DevTools 检查中，可访问性、最佳实践、SEO 与可代理浏览也全部为 100。

这些检查覆盖一次 DOM 交互、当前工具图、生产构建与受测 Chrome 环境。它们不覆盖 React 或 Elmish 兼容性、所有浏览器、路由、HTTP、离线行为、认证、本地化、水合、服务端渲染、长会话内存或生产托管响应头。

## 了解受支持的 F# 与 .NET API {#compatibility}

Fable 的官方 JavaScript 兼容性参考记录了大部分 FSharp.Core 和部分 BCL 类型的支持。它经常把熟悉类型映射到原生 JavaScript 形式或小型 Fable 运行库。这让源码读起来顺手，但目标语义仍然可见。

值得显式测试的例子包括：

- 大部分数值类型使用 JavaScript `number`，而 `int64`、`uint64`、`bigint` 和 `decimal` 使用不同表示；
- 小整数算术与数组边界不会自动复现每一种 CLR 溢出或边界行为；
- 日期、正则表达式、反射、泛型信息和对象成员存在有文档的限制或目标专属行为；
- 浏览器目标不提供 `Async.RunSynchronously`；
- `MailboxProcessor` 支持有限，而且浏览器 JavaScript 仍是单线程，除非显式引入 worker 边界；
- option 与其他 F# 值可能使用对 JavaScript 友好的运行期表示，这在原始互操作边界很重要。

不要把这份列表背成永久事实。把兼容性页面链接到升级记录，并测试领域实际依赖的操作。

### 共享可跨目标运行的行为 {#shared-code}

好的跨目标候选是依赖很少的记录、联合、验证、计算和纯状态转换模块。差的候选会直接读文件、打开数据库连接、启动线程、加载重度反射插件、读取进程环境，或依赖任意服务端 NuGet 图。

使用这样的边界：

```text
共享的纯契约与决策
  <- 服务端适配器：ASP.NET Core、数据库、秘密、时钟
  -> 浏览器适配器：DOM、fetch、存储、URL、浏览器时钟
```

让共享项目在 .NET 下编译，也经 Fable 编译；语义等价很重要时，在两个目标上运行相同的固定测试输入。“它在 .NET 上没有编译错误”不是跨目标证据。

条件编译可以隔离真正微小的目标差异，但若业务逻辑中到处都是重复的 `#if FABLE_COMPILER` 分支，通常说明缺少端口或模块其实不该共享。

## 把 JavaScript 互操作当作适配器 {#javascript-interop}

浏览器应用最终会调用 Web API 或 JavaScript 包。Fable.Core 提供类型化导入、全局值、动态辅助函数和 emit 特性。按以下顺序偏好它们：

1. 维护中的 Fable 兼容包，并且有明确原生依赖契约；
2. 只覆盖实际使用 JavaScript API 的小型本地类型化绑定；
3. 为自有模块编写最小导入层；
4. 只在无法通过类型化 API 实现的极小范围内使用 `Emit` 或 `emitJsExpr`。

`Emit` 插入 F# 编译器无法验证的 JavaScript。把它散布在视图和领域代码中，会把重构变成字符串编辑。让它保持私有，测试生成行为，并暴露普通 F# 函数或接口。

### 同时管理两份包依赖图 {#two-package-graphs}

NuGet 解析 F# 源码包和绑定；npm/pnpm 解析打包器与原生 JavaScript 包。有些 Fable 包要求匹配的 npm 依赖，例如 React 与 `react-dom`。NuGet 还原成功不会安装该 JavaScript 运行时，npm 安装成功也不能证明 F# 绑定匹配它。

锁定两个依赖图并记录：

- 用来转译的 Fable 工具和 SDK；
- 直接 NuGet 包及其锁文件；
- 直接 npm 包及工作区锁；
- 绑定与原生 JavaScript 包之间任何必需配对；
- 允许的生命周期脚本和生成代码策略；
- 许可证、安全公告、目标浏览器和升级证据。

生成的 JavaScript 和包输出通常是构建产物。只有发布或审计契约要求时才提交，并在那时定义由谁重新生成与审阅。

## 浏览器 API 不是服务端 API {#browser-boundaries}

浏览器在来源和权限模型下提供 DOM、事件、URL/history、fetch、存储、worker、媒体等 Web API。它不提供服务端的文件系统布局、进程环境、数据库连接、ASP.NET Core 依赖注入容器或可信秘密存储。

### HTTP 仍是不可信契约 {#http-contract}

即使服务端也是 F#，浏览器值与服务端值也要跨越字节。显式定义 DTO 和传输格式。两端都验证。为不兼容变更定版本。测试缺失、额外、格式错误、null、Unicode、日期/时间、数字和错误载荷，不要因为共享记录就假设 JSON 相同。

浏览器应把传输状态映射为 `NotAsked`、`Loading`、`Loaded`、`Failed` 等面向领域的情况；一个被拒绝的 promise 或异常不是完整用户状态。当响应可能竞速时，保留关联与取消元数据。

Cookie、Bearer 令牌、CORS、CSRF、重定向、缓存和状态码都是传输/安全决定。Elmish 不会解决它们，两端使用同一种语言也不会消除信任边界。

### 客户端代码与存储都是可见的 {#client-security}

绝不要把秘密放入 F# 源码、浏览器构建期配置、生成的 JavaScript、source map、HTML 或浏览器存储。只要浏览器能使用某个凭据，用户和注入脚本就能在浏览器规则下观察或行使它。

对不可信内容优先使用 `textContent` 或渲染器文本节点。原始 HTML 需要显式净化与策略边界。把依赖、内容安全策略、嵌入、子资源加载、source map 发布和敏感诊断数据纳入部署审阅。

`localStorage`、`sessionStorage`、IndexedDB 与缓存是持久化机制，不是保密保险箱或最终可信的数据源。依赖它们前，定义数据寿命、模式迁移、配额失败、多标签页协调、登出清理和离线冲突行为。

### 可访问性属于渲染边界 {#accessibility}

F# 类型能让状态显式，但不会自动产生语义 HTML、可访问名称、焦点移动、键盘操作、播报、对比度、减少动态效果或响应式布局。浏览器样例使用真实标题层级、按钮、live `output`、禁用的重置状态、可见焦点和窄屏测试，因为这些都是浏览器契约。

要用可访问性树与键盘测试，而不只用 CSS 选择器。虚拟 DOM、Feliz DSL 或 Elmish 循环会改变构造方式；它们都不能让最终 DOM 免于可访问性要求。

## 从状态复杂度选择状态架构 {#state-architecture}

使用能保持行为可理解的最小显式状态模型。

| 问题 | 首选候选 | 为什么可能适合 | 何时重新考虑 |
|---|---|---|---|
| 只有几个元素的一项有界增强 | 普通 Fable + Browser.Dom | 依赖图最小且直接使用平台语义 | 渲染重复、转换纠缠、生命周期泄漏 |
| 多个协调状态、副作用和路由 | Elmish 核心 + 所选渲染器 | 单一消息流与可测试转换 | 简单局部状态需要过多固定代码，或 update 变得过大 |
| React 组件生态是硬需求 | Feliz 和/或 Fable.Elmish.React 加锁定 React | 在所需生态上提供类型化 F# 视图 API | 绑定/原生版本不匹配，包体或升级成本超过收益 |
| 主要是服务端渲染内容，只有少量交互 | 服务端 HTML 加隔离的 Fable 岛 | 保持简单导航和载荷 | 岛需要共享全局状态或重复大型依赖 |

不要因为语言是 F# 就选 Elmish，不要因为应用“现代”就选 React，也不要因为第一版演示很短就坚持直接 DOM。应根据状态寿命、副作用并发、组件互操作、渲染频率、团队技能、可访问性和升级责任来选择。

## Elmish 让事件循环显式 {#elmish-loop}

Elmish 形式化 model-view-update：

```text
init -> Model + Cmd<Msg>
事件/副作用 -> Msg
update Msg Model -> Model + Cmd<Msg>
view Model dispatch -> 渲染后的 UI
```

模型是不可变快照。消息命名发生了什么。`update` 决定下一状态并描述命令。运行时执行命令、分派后续消息，再让渲染器更新视图。

浏览器样例在没有库的情况下，已经实现了这种模式的核心：`Message`、`Model` 与纯 `update`。手写的可变外壳负责分派和渲染。当标准命令、订阅、组合、插桩或渲染器集成能替代足够多的自定义生命周期代码时，引入 Elmish 才值得。

### 命令描述副作用；它不会净化副作用 {#commands}

HTTP 命令仍执行 I/O，仍可能失败、超时、延迟完成或被取消。收益在于 `update` 返回描述，并把结果作为另一条消息接收，而不是把副作用藏在状态修改里。

给每种副作用定义结果消息。搜索可以使用：

- `SearchStarted`；
- `SearchSucceeded`；
- `SearchFailed`；
- `SearchCancelled`。

这样视图能区分成功但结果为空、连接失败和取消；单个 `SetResults` 消息会混淆这些含义。

### 用订阅管理外部事件的生命周期 {#subscriptions}

计时器、WebSocket、浏览器 observer 和全局事件源可以独立于命令发出事件。Elmish 订阅把这些来源关联到依赖模型的身份，并在程序变化时启动或停止它们。

清理是硬要求。每个活动来源应只有一项当前注册；替换 socket 时先释放旧连接；过时订阅应停止分派。测试启动、替换、释放、重连和页面拆卸。

### 拒绝陈旧的异步结果 {#stale-results}

假设用户先搜索 `fa`，随后快速搜索 `fable`。第一个请求可能最后结束。有用的模型会携带请求 ID 或代次：

```fsharp
type RemoteData<'value> =
    | NotAsked
    | Loading of requestId: int * query: string
    | Loaded of requestId: int * query: string * value: 'value
    | Failed of requestId: int * query: string * message: string
```

完成消息到达时，`update` 只有在其 ID 仍与活动请求匹配时才接受。通过 `AbortController` 取消能节约工作，但身份检查仍然必要，因为取消可能竞速、可能不受支持，也可能在完成之后才到达。

把防抖与网络状态分别建模。计时器代次、请求内容、活动请求、可见结果和验证消息是不同事实。把它们压成 `bool isLoading` 会丢失解决竞态所需的信息。

## Elmish 不是渲染器 {#renderers}

Elmish 核心与 UI 无关。渲染器把模型变成 DOM 或渲染器专属元素，再通过 `dispatch` 把用户事件送回。

### React、Feliz 与 Elmish.React {#react-feliz}

Feliz 是 React 的类型化 F# API。它可以配合 React 组件局部状态与 hook、Elmish 程序，或在组件边界使用 `Feliz.UseElmish`。Fable.Elmish.React 负责把 Elmish 程序连接到 React 或 React Native；其包元数据要求应用另行安装原生 React 包。

这些工具解决不同问题：

- React 提供 JavaScript 渲染/组件运行时；
- Feliz 提供对 F# 友好的类型化构造 API；
- Elmish 提供模型—消息—更新—命令组织；
- Fable.Elmish.React 连接 Elmish 循环与 React 渲染器。

四个都加入并不会自动让程序更函数式。对于小型隔离组件，Feliz hook 可能足够。对于全应用工作流和协调副作用，Elmish 可能有帮助。对于一个计数器，浏览器样例的直接 DOM 外壳更容易审计。

Fable.React 包页面本身建议新 React 项目使用 Feliz，因为 Fable.React 维护较少。要把它当作当前维护者指引，而不是在没有迁移证据时重写稳定应用的理由。

### 其他渲染器与直接绑定 {#other-renderers}

Fable 可以直接面向 DOM，也能使用其他 JavaScript UI 库的绑定。要从维护中的包元数据、原生 peer 依赖、水合或路由需求、可访问性输出、调试、包体成本和升级历史评估每个选项。只在 .NET 或 JavaScript 一侧流行，都不能证明两者之间的绑定可靠。

让渲染器值远离领域和应用模块。这样渲染器迁移、服务端复用和纯测试都会实质性缩小。

## 扩展 Elmish 模型而不造出一个巨型 update {#elmish-composition}

按内聚功能和清晰的消息职责拆分，而不是任意建立“models”“views”“updates”文件夹。一个功能可以暴露自己的 `Model`、`Msg`、`init`、`update` 和视图边界。父级包装子消息，并把子命令映射回父消息类型。

父级负责跨功能决定。子功能不应直接访问另一功能的可变存储。如果两个功能必须协调，就建模父消息或共享领域转换，而不是搭建隐藏事件总线。

不要把每个缓存响应、文本字段、模态框、路由和短暂 hover 都塞进一个全局模型。让状态位于需要协调的最窄生命周期。反过来，在多个组件中复制作为唯一依据的预约状态会增加同步工作；应确定唯一数据来源。

### 让非法 UI 状态难以表达 {#ui-state-modeling}

优先使用联合，而不是互不相关的标志：

```fsharp
type BookingPage =
    | Editing of Draft
    | Submitting of Draft * requestId: int
    | Accepted of Confirmation
    | Rejected of Draft * ValidationError list
    | Unavailable of Draft * safeMessage: string
```

这个模型不可能同时处于提交中与已接受。它也会在可恢复失败时保留草稿。真实产品可能需要更多正交状态，但每个新增字段都应代表独立事实，而不是修补矛盾标志集。

表单类型需要克制。原始文本属于编辑状态，因为输入到一半的数字和日期还不是领域值。在有意的转换点解析与验证；不要强迫每次按键都变成领域类型，也不要把所有验证推迟到服务端响应。

### 用 URL 承载可导航状态 {#routing}

如果某种状态应在刷新、深链接、历史导航或分享后保留，就判断它是否应进入 URL。把路由解析为验证后的应用状态，并明确渲染未知路由。若没有同步规则，不要在路由状态、全局模型和组件状态中保留互不相关的副本。

客户端路由要求托管回退配置。浏览器样例是只有一个入口的 MPA，并且有意没有验证 SPA 重写。一个在 `/` 工作的包，在用户从静态托管直接请求 `/bookings/42` 时仍可能返回 404。

## 在多个层次测试浏览器应用 {#testing}

使用多个层次，因为每层捕获不同类型的错误。

### 纯转换测试 {#pure-tests}

尽可能在没有浏览器时测试 `update`、验证、路由解析器、reducer、编码器和派生视图数据。对成功、无效输入、重复输入、陈旧消息、重试和取消消息断言下一模型与发出的副作用描述。

如果同一源码意图同时运行于 .NET 与 JavaScript，就为语义热点在两个目标上运行。仅 .NET 测试证明源码逻辑在 CLR 下工作，不证明生成的 JavaScript 表示。

### 绑定与组件测试 {#binding-tests}

针对固定的 JavaScript 包版本测试本地绑定。覆盖可选成员、回调 `this`、promise 拒绝、事件清理、null/undefined、模块格式和生产压缩。类型声明可能错误，也可能落后于运行时。

组件测试应按用户感知的角色、名称、标签、文本和状态查询。CSS 类选择器把测试耦合到渲染细节，而且可能漏掉不可访问的 UI。

### 契约与浏览器测试 {#browser-tests}

只在范围小、结果确定的场景中模拟传输。为成功与每种已定义错误保留服务端契约样本；当浏览器客户端与服务必须在凭据、CORS、序列化和状态映射上一致时，至少运行一次真实 HTTP 请求。

生产浏览器冒烟必须使用构建后的资源，而不只是开发服务器。捕获控制台错误、页面异常、失败与错误响应、初始就绪、一个有意义交互、键盘/可访问性语义和窄屏溢出。只有产品支持要求时才增加浏览器/版本矩阵。

### 诊断生成代码与打包代码 {#diagnostics}

当互操作或大小问题需要时阅读生成的 JavaScript，但在策略允许时通过 source map 从 F# 调试。检查浏览器网络面板、可访问性树、事件监听器、性能跟踪、存储和包图。F# 编译成功无法显示缺失资源、CSP 拒绝、陈旧 service worker、水合不匹配或缺少可访问名称。

没有显式访问策略时，不要发布包含敏感源码或路径的 source map。如果生产诊断要把 map 上传到服务，应把上传与公开产物暴露分开。

## 把开发与生产管道分开 {#build-deploy}

官方 Fable/Vite 工作流可以让 Fable 监听并配合 Vite，实现快速开发。生产仍必须从锁定的干净输入开始并创建不可变产物。

浏览器样例的生产序列在概念上是：

```sh
dotnet tool restore
dotnet restore path/to/FableSample.fsproj --locked-mode
dotnet fable --outDir generated --noRestore --noCache
vite build
vite preview
```

启动预览服务器后，应在真实浏览器中检查生产产物；风险足够高时再自动执行。开发服务器运行不能验证生产产物。

### 部署产物，而不是开发拓扑 {#static-deployment}

把 `dist` 部署到静态托管，并提供正确 MIME 类型、缓存规则、压缩、安全头和基础路径。对带哈希的不可变资源使用长期缓存；让 HTML 入口可以重新验证，以便指向新哈希。若网站不托管在 `/`，要在真实子路径下测试。

有意选择 MPA、SPA 回退或服务端路由。定义发布期间旧 HTML 如何配合新资源，若有 service worker 则定义它如何更新，以及如何同时回滚资源与 API 兼容性。

浏览器样例的产物不需要应用服务器。本地预览服务器只是开发工具，不是生产依赖，也不代表托管选择。

### 测量包体与运行成本 {#browser-performance}

在代表性设备上测量压缩传输、JavaScript 解析/执行、主线程工作、渲染、内存、网络瀑布和交互延迟。小源文件可以导入大型原生包；大的生成目录也可能摇树成小包。要检查生产结果，而不是按任一输入的行数判断。

只为已证明的路由或功能边界使用代码分割，而不是自动碎片化。加载指示、chunk 失败、缓存失效、预加载和离线行为都会成为状态模型的一部分。

## 让版本表准确反映检查结果 {#version-table}

下面是带日期的观察，不是预先批准的技术栈：

| 选择 | 2026-08-25 检查的稳定版本 | 本章状态 | 采用问题 |
|---|---|---|---|
| Fable 工具 | 5.13.0；工具面向 .NET 10 | 已示例 | 生成的 JavaScript 是否保留此应用需要的语义？ |
| Fable.Core | 5.2.0；`netstandard2.0` 资源 | 已示例 | 每个所用辅助函数是否受 JavaScript 目标支持？ |
| Fable.Browser.Dom | 2.20.0；浏览器绑定图 | 已示例 | 所需 Web API 与目标浏览器是否得到覆盖？ |
| Vite | 6.4.3 | 已示例 | 基础路径、资源、生产模式和托管行为是否验证？ |
| Fable.Elmish | 5.0.2 | 仅研究 | 状态与副作用是否复杂到值得引入该循环？ |
| Fable.Elmish.React | 5.6.0 稳定版；存在 6.0 beta | 仅研究 | F# 绑定是否兼容所选 React/npm 矩阵？ |
| Feliz | 3.3.3 | 仅研究 | 它的类型化 React API 是否适合组件与升级需求？ |
| Fable.React | 9.4.0 稳定版；包建议新项目使用 Feliz | 仅研究 | 这是维护既有技术栈，而不是新的默认选择吗？ |

“已示例”表示本章包含最小配置或用法，不表示书站仓库内保留着可执行浏览器工程。“仅研究”表示采用前必须在真实应用中继续评估。

## 开展可逆的浏览器技术栈试验 {#adoption-spike}

使用一个有代表性的端到端小样：

- 一个带真实导航约束的路由或嵌入岛；
- 一个包含原始、有效、无效和服务端拒绝状态的表单；
- 一个会乱序完成的重叠异步请求；
- 一个位于类型化适配器之后的 JavaScript 包或 Web API；
- 一个带已声明错误映射的认证 HTTP 调用；
- 一条可访问键盘流程和一种窄屏布局；
- 锁定的 NuGet/npm 还原、生产包、静态服务与 Chrome 冒烟；
- 包体、交互、内存、诊断、CSP 和 source map 检查；
- 一次依赖升级和有文档的回滚/删除路径。

用同一个端到端小样比较普通 Fable、Elmish 和所需渲染器。统计概念、依赖、生命周期代码、测试、构建步骤和运维责任，而不只比较视图语法。

只有当更大技术栈消除的风险超过新增风险时才采用。让落选试验小到可以删除，并让领域代码不含渲染器类型，使反转仍然可信。

## 避免常见浏览器错误 {#common-mistakes}

- 把 Fable 称为“浏览器里的 .NET”，并假设任意程序集可用。
- 把 `netstandard` 兼容性当作 Fable 和浏览器兼容性。
- 忘记 npm peer/原生包与 NuGet 绑定彼此独立。
- 在生成的 JavaScript、浏览器配置、存储或 source map 中暴露秘密。
- 把 `Emit`、动态值、原始 DOM 转型或渲染器节点散布到领域代码。
- 在 `update` 中执行副作用，然后仍称该函数为纯函数。
- 用一个布尔值表示加载、空、错误、已取消和陈旧状态。
- 取消请求，却不按身份拒绝延迟完成。
- 重复注册计时器、socket 或监听器，却没有明确由谁清理。
- 为简单局部状态选择 Elmish，或在自定义 dispatch 代码已经变成框架后仍拒绝它。
- 把 Elmish、React、Feliz 与 Fable.Elmish.React 当作同义词。
- 与浏览器目标共享读取文件、环境、线程或数据库的服务端项目。
- 只在开发服务器下测试，或只在 .NET 下测试。
- DOM 测试只按 CSS 实现细节查询，漏掉角色、名称、焦点和键盘行为。
- 发布静态包，却没有检查基础路径、直接路由、缓存、CSP、MIME 与回滚。
- 把生成目录大小当成包体，而不测量生产传输与执行。
- 因为审阅了当前版本页面却从未构建，就声称支持某个包选项。

## 练习 {#exercises}

### 练习 1：选择三种浏览器架构 {#exercise-01}

分别评估以下浏览器界面：

1. 服务端渲染的文档页需要一个可访问的偏好开关，并且没有共享应用状态。
2. 预约客户端包含多步草稿、URL 导航、重叠的可用性与支付请求、重试和可恢复失败。
3. 产品必须集成前端团队已有的五个维护中 React 组件，大部分状态仍局限于各组件内部。

为每种界面选择首选候选和反转条件。比较普通 Fable DOM、Elmish 与 Feliz/React；三种界面可以采用不同架构。

### 练习 2：建模陈旧搜索结果 {#exercise-02}

为带 250 ms 防抖、取消、结果、空状态、安全失败和乱序完成的搜索框设计 `Model`、`Msg`、`update` 以及命令/订阅职责。用户先输入 `fa`，再输入 `fable`；`fa` 请求最后结束。说明比较哪些身份、刷新期间保留什么可见内容、可访问地播报什么，以及哪些转换属于纯测试用例。

### 练习 3：审计共享库与发布 {#exercise-03}

团队希望在 Fable 结账页共享服务端定价项目。它现在使用记录和 decimal 算术，但也读取 `DateTime.UtcNow`、环境变量、JSON 文件和基于反射的序列化器。设计目标中立核心、服务端与浏览器适配器、DTO 与传输协议边界、跨目标测试、包锁、浏览器安全审阅、生产构建、静态托管检查、发布与回滚。说明在测量前不能声明哪些行为等价。

[阅读本章练习答案](../solutions/ch-41-fable-elmish)。

## 资料来源 {#sources}

- [Fable：支持目标与稳定性级别](https://fable.io/docs/index.html)
- [Fable：创建项目并安装工具](https://fable.io/docs/getting-started/your-first-fable-project.html)
- [Fable：配合 Vite 的开发与生产构建](https://fable.io/docs/javascript/build-and-run.html)
- [Fable CLI 选项与目标行为](https://fable.io/docs/getting-started/cli.html)
- [NuGet：Fable 工具版本](https://www.nuget.org/packages/Fable)
- [Vite：生产构建指南](https://vite.dev/guide/build)

第 42 章从静态浏览器产物转向已部署的服务拓扑：容器、云边界、Serverless 约束与 .NET Aspire 编排。
