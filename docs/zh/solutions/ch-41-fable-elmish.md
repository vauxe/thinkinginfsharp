---
title: "第 41 章练习答案"
description: "选择与问题成比例的浏览器架构，拒绝陈旧异步结果，并按诚实的运行时边界拆分共享定价库。"
translationKey: solutions/ch-41-fable-elmish
---

# 第 41 章练习答案 {#overview}

这些答案先选择一种架构，保留能推翻它的证据，并让目标运行时主张比源语言熟悉感更狭窄。

[返回第 41 章](../part-07/ch-41-fable-elmish)。

## 练习 1：选择三种浏览器架构 {#exercise-01}

### 情况 A：服务端渲染文档上的一个偏好开关 {#exercise-01-case-a}

从一个微小的隔离增强开始。如果偏好只靠原生 HTML 与 CSS 就能表示，就使用它们。如果它需要存储状态与事件处理，而且应用已经拥有 Fable 管道，那么普通 Fable 加 Browser.Dom 是第一个 F# 候选。

使用一个模型和一个类型化边界：

```fsharp
type Preference =
    | FollowSystem
    | Light
    | Dark

type Message =
    | PreferenceSelected of Preference
    | SystemPreferenceChanged of prefersDark: bool
```

纯转换决定有效偏好。一个适配器读取并验证存储，一个应用文档属性，一个拥有媒体查询监听器。存储失败会回退，但不会让控件不可用。开关是真实按钮或单选组，带可访问名称和可见焦点。

暂时不要加入 Elmish 或 React。它们会增加循环、渲染器、原生 npm 包、升级配对和生命周期概念，却没有为一个岛消除有意义风险。

反转证据包括：第二、第三个岛必须协调；监听器/释放代码重复；状态归 URL 所有；或异步工作让局部分派器不一致。Fable 本身也可能落选：如果页面没有 Fable 工具链，而几行经过审阅的原生 JavaScript 实质上更便宜，F# 就不是强制的浏览器依赖。

验收切片覆盖系统/浅色/深色初始化、无效存储输入、存储被拒、键盘操作、系统偏好变化、清理、320 像素布局、生产打包，以及零控制台或 HTTP 失败。

### 情况 B：带协调副作用的多步预约客户端 {#exercise-01-case-b}

从 Fable.Elmish 核心开始，并让渲染器保持暂定。问题包含长寿命协调状态、URL 所有权、多个可失败副作用、重试和竞速；这些正是消息循环能替代自定义生命周期机制的条件。

建模有意义状态，而不是一袋标志：

```fsharp
type BookingFlow =
    | Editing of Draft
    | CheckingAvailability of Draft * requestId: int
    | AwaitingConfirmation of PricedDraft
    | Paying of PricedDraft * attemptId: int
    | Completed of Confirmation
    | RecoverableFailure of Draft * SafeError
    | OutcomeUnknown of PricedDraft * reconciliationId: string
```

可用性成功必须匹配活动请求 ID。支付超时不会自动成为安全重试：第 36–38 章的服务端契约必须区分已知失败与结果未知，并支持幂等对账。浏览器永远不拥有权威价格、容量或预约结果。

命令覆盖可用性、提交、支付/对账与路由副作用。每条命令都返回成功、已声明失败、取消或传输不可用消息。订阅只留给网络状态或服务端事件通道等真正外部流，并带身份与清理。

从满足真实 UI 的最小渲染器开始。只有组件需求、渲染易用性或既有 React 表面能补偿新增 NuGet/npm 图时，才选择 Feliz/React。Elmish 与直接 DOM 可以在狭窄渲染适配器中共存；状态模型不应暴露渲染器节点。

反对 Elmish 的证据，是代表性流程中大部分状态其实彼此隔离，循环只增加映射仪式却没有简化副作用。支持更换渲染器的证据包括：手工 DOM 修补不可访问或易错、需要维护中的 React 组件，或已测得的渲染压力。反向证据——绑定滞后、升级冲突或包体成本——则可以淘汰 React。

验收覆盖深链接/刷新/后退导航、无效草稿、两个乱序可用性检查、重复提交、支付已知失败与结果未知、取消、重试、错误后焦点、屏幕阅读器播报、锁定生产输出和真实服务契约测试。

### 情况 C：五个必需 React 组件，大部分状态局部化 {#exercise-01-case-c}

从 Feliz 加锁定的 `react` 与 `react-dom` 开始，并通过小型类型化绑定包装每个必需组件。对真正局部的状态使用组件局部 hook。不要仅因为客户端用 F# 编写，就引入一个全应用 Elmish 模型。

为每个组件盘点：

- 精确的原生包与 peer 版本；
- F# 绑定版本或本地自有绑定表面；
- 必需属性、事件、ref、promise、null/undefined 与清理；
- 生成的模块格式与生产打包器行为；
- 语义 DOM、焦点、键盘、本地化与错误行为；
- 许可证、安全公告、source map、包体与升级所有权。

只有组件内存在真实工作流时才引入 `Feliz.UseElmish` 或子 Elmish 程序。只有跨组件转换、副作用或路由需要一个清晰所有者时，才引入根 Elmish 程序。如果所选程序是 Elmish 且渲染器是 React，Fable.Elmish.React 才是连接器；它不会替代 React 本身。

反转证据包括绑定/原生兼容矩阵失败、不可接受的可访问性输出、仅生产模式出现的错误、过高包体/运行成本，或组件其实能被平台 HTML 替代。一个成功的 storybook 式演示若没有生产 Fable/Vite 管道和目标浏览器，证据仍不充分。

## 练习 2：建模陈旧搜索结果 {#exercise-02}

### 分开输入、待处理工作和可见结果 {#exercise-02-model}

为防抖计时器使用单调递增代次，为网络工作使用请求 ID：

```fsharp
type Pending =
    | NoPending
    | Debouncing of generation: int * query: string
    | Requesting of requestId: int * query: string

type VisibleResults =
    | NeverSearched
    | Showing of query: string * SearchItem list
    | Empty of query: string
    | Failed of query: string * safeMessage: string

type Model =
    { Input: string
      Pending: Pending
      Visible: VisibleResults
      NextGeneration: int
      NextRequestId: int
      Announcement: string option }
```

把 `Visible` 分开，能让刷新期间继续显示上一份成功结果。视图可以显示不阻塞的“正在更新结果”，而不是用加载圈替换有用内容。

消息携带拒绝陈旧工作所需的身份：

```fsharp
type Msg =
    | InputChanged of string
    | DebounceElapsed of generation: int * query: string
    | SearchSucceeded of requestId: int * query: string * SearchItem list
    | SearchFailed of requestId: int * query: string * safeMessage: string
    | SearchCancelled of requestId: int
```

### 保持 update 确定性 {#exercise-02-update}

`InputChanged text` 只按已声明搜索契约修剪，分配新防抖代次，清除旧播报，并返回取消活动请求/计时器及启动 250 ms 计时器的命令。空输入转为 `NoPending` 与 `NeverSearched`，不启动 HTTP。

`DebounceElapsed(generation, query)` 只有在 `Pending` 精确等于 `Debouncing(generation, query)` 且当前输入仍表示该查询时才启动请求。它分配 `requestId`，转为 `Requesting(requestId, query)`，保留 `Visible`，并返回启动搜索命令。

只有当请求 ID 与查询都匹配活动 `Requesting` 情况时，才接受成功、失败和取消。`fable` 已经活动后，任何 `fa` 完成都返回未变模型和空命令。记录聚合陈旧完成计数属于适配器职责，而不是暴露用户输入的模型转换。

非空成功结果变成 `Showing`；零结果变成 `Empty`；已声明失败变成带安全消息的 `Failed`。由新输入造成的取消不会覆盖可见内容，也不会播报错误。真正由用户请求的停止可以使用不同消息与提示。

### 在模型之外拥有计时器与 AbortController {#exercise-02-effects}

命令适配器拥有以代次/请求 ID 为键的计时器句柄与 `AbortController` 实例。启动更新工作会中止并移除旧句柄。完成与释放也会移除它们。模型存储可序列化身份和状态，而不是浏览器控制器对象。

Abort 是优化与生命周期动作，不是正确性规则。身份守卫仍会拒绝抢在 abort 前完成的响应，或来自无法取消传输的响应。

如果项目一致使用按模型定键的 Elmish 订阅，也可以让订阅拥有防抖计时器。订阅 ID 包含活动代次；改变或清除它会停止之前的订阅。不要同时运行命令计时器与订阅计时器两种设计。

### 测试转换与可访问输出 {#exercise-02-tests}

纯测试覆盖：

- 空输入不启动任何工作；
- `fa` 分配代次 1，随后 `fable` 分配代次 2 并取消代次 1；
- 代次 1 tick 被忽略，代次 2 则启动请求 1；
- 后续编辑启动请求 2，请求 1 的成功/失败/取消都被忽略；
- 请求 2 的非空、空和安全失败结果形成不同可见状态；
- 重试分配新请求 ID，而不是复用含糊完成身份；
- 接受的完成清除待处理工作并产生一次有界播报；
- 被忽略的陈旧工作不改变内容、焦点或播报。

浏览器测试在受控响应门下输入两个查询，先完成 `fable`、最后完成 `fa`，并证明只保留 `fable`。它还检查键盘输入、焦点保持、不会播报每次按键的礼貌 live 状态、安全错误、取消清理、卸载、控制台/网络失败和生产包。

## 练习 3：审计共享库与发布 {#exercise-03}

### 提取目标中立的定价核心 {#exercise-03-core}

创建只包含定价契约和确定性决定的小型项目：

```fsharp
type Money =
    private
    | Money of currency: Currency * amount: decimal

type PricingInput =
    { At: DateTimeOffset
      Basket: Basket
      Rules: PricingRules }

val price: PricingInput -> Result<PricedBasket, PricingError list>
```

把时间和规则作为数据传入。让货币显式，并集中定义小数位数/舍入规则。核心不读取时钟、环境、文件、浏览器存储或序列化器。它也不决定配置如何到达。

服务端保持权威。浏览器可以为了响应速度计算预览，但结账会发送版本化请求并显示服务端定价结果。客户端包可以被修改、使用陈旧规则运行，或具有不同数值行为。

### 把目标副作用放入独立适配器 {#exercise-03-adapters}

服务端适配器读取获批配置、取得时钟时刻、在启动时加载/验证 JSON 规则文档、保护秘密，并通过显式序列化器映射 HTTP DTO。它向核心暴露经过验证的 `PricingRules` 与 `DateTimeOffset`。

浏览器适配器通过版本化 HTTP 或构建产物接收非秘密公开配置，只有产品契约允许客户端时间时才映射 Web API 时间，并使用与服务端线模式兼容的显式 JSON codec。它绝不会靠希望导入基于反射的服务端序列化器。

如果浏览器必须计算“现在”，就定义规则由服务端时间、客户端墙上时间还是单调持续时间所有。对于到期或价格权威，使用服务端签发的时刻/版本。客户端时钟受用户控制，也可能漂移。

旧反射序列化器保留在服务端适配器之后，直到契约测试证明可以替换。浏览器 codec 必须按同一份书面兼容策略拒绝格式错误、缺失、额外、null、数字、日期、货币和版本情况。

### 建立跨目标语义证据 {#exercise-03-cross-target}

让相同黄金向量经过 .NET 与 Fable 生成的 JavaScript。包含每种货币小数位、中点舍入、负/零/高金额、数量边界、折扣、税序、日期截止、Unicode 标识符与非法组合。比较声明的金额值、错误和规则版本，而不是内部对象表示。

在两个测试框架能共享确定性种子或序列化夹具时，加入生成式边界/性质用例。不一致会阻止发布客户端预览；没有领域决定时不得靠舍入掩盖。

使用 decimal 不能证明运行期行为相同。Fable 记录了目标专属数值表示。在跨目标测试与代表性浏览器测量通过前，只能声称两个目标都能编译。即便通过，服务端仍重新计算权威价格。

编译成功也无法证明反射、时区、序列化器或文件系统等价；这些功能应从共享核心中有意移除，而不是模拟。

### 锁定、构建、加固并发布 {#exercise-03-release}

发布管道执行：

1. 锁定的 SDK、工具、NuGet 与 JavaScript 依赖还原；
2. .NET 单元/性质测试和服务端序列化器契约；
3. Fable 编译，以及生成 JavaScript 对共享黄金向量的执行；
4. Vite 生产构建和包体/许可证/安全公告审阅；
5. 真实服务端/浏览器 HTTP 契约与结账冒烟；
6. 可访问性、CSP、存储、缓存、source map 与秘密扫描；
7. 静态托管的 MIME、基础路径、压缩、缓存、直接路由和错误文档检查；
8. 产物摘要、规则/API 兼容性、发布、监控与回滚记录。

先部署对新 DTO/规则版本的服务端支持，再发布需要它的客户端。在浏览器缓存与回滚窗口内保留旧服务端兼容性。静态资源使用不可变哈希；HTML 入口重新验证，并能回滚到之前兼容的资源集。

以有界计数观察规则版本、客户端/服务端价格不一致、被拒 DTO 版本、结账错误和回滚选择，但不记录购物篮、令牌或个人数据。不一致会阻止提交或显示权威服务端价格；绝不会静默收取客户端预览价格。

只有每个服务端调用方都使用服务端适配器、Fable 项目只编译中立核心、两个目标都运行黄金测试、生产不再从核心代码加载文件/环境路径、缓存客户端仍兼容，且回滚不再需要旧布局后，才删除原混合项目。

## 答案回顾 {#solution-review}

- 一个隔离偏好控件从平台 HTML 或普通 Fable 开始，而不是自动引入应用框架。
- 协调的预约状态、副作用、路由、重试和结果未知可以证明 Elmish 合理，同时让渲染器保持暂定。
- 必需 React 组件先证明 Feliz/React 绑定合理，再证明一个全局 Elmish 模型合理。
- 绑定与原生 npm 版本、可访问性、生产输出和清理都需要同一份兼容矩阵。
- 防抖代次拒绝旧计时器；请求 ID 拒绝旧网络完成。
- AbortController 节约工作，但身份匹配提供正确性。
- 可见结果、待处理工作、空状态、安全失败和播报彼此独立。
- 浏览器控制器属于副作用适配器，而不是可序列化模型。
- 共享定价接收时间、规则和输入；它不读取目标专属环境。
- 浏览器定价只是预览；服务端保持权威。
- Decimal、日期、反射和序列化需要跨目标证据，而不是源代码层信心。
- 锁定两个依赖图，并在 CLR 与生成的 JavaScript 下执行黄金向量。
- 先发布服务端兼容边界，再发布使用它且可能被缓存的静态客户端。
- 回滚包含 HTML/资源、API/规则版本、缓存和旧客户端兼容性。

## 资料来源 {#sources}

- [Fable：构建与运行](https://fable.io/docs/javascript/build-and-run.html)
- [Fable：.NET 与 F# 兼容性](https://fable.io/docs/javascript/compatibility.html)
- [Fable：JavaScript 特性与互操作](https://fable.io/docs/javascript/features.html)
- [Elmish 概览](https://elmish.github.io/elmish/)
- [Elmish 订阅](https://elmish.github.io/elmish/docs/subscription.html)
- [NuGet：Fable.Elmish 5.0.2](https://www.nuget.org/packages/Fable.Elmish/5.0.2)
- [NuGet：Fable.Elmish.React 5.6.0](https://www.nuget.org/packages/Fable.Elmish.React/5.6.0)
- [NuGet：Feliz 3.3.3](https://www.nuget.org/packages/Feliz/3.3.3)
