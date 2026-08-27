---
title: "第 41 章练习答案"
description: "选择与问题成比例的浏览器架构，拒绝陈旧异步结果，并按诚实的运行时边界拆分共享定价库。"
translationKey: solutions/ch-41-fable-elmish
---

# 第 41 章练习答案 {#overview}

这些答案先选择起点，再说明何时重新考虑，并把运行时结论限制在各目标实际测试过的范围内。

[返回第 41 章](../part-07/ch-41-fable-elmish)。

## 练习 1：选择三种浏览器架构 {#exercise-01}

### 情况 A：服务端渲染文档上的一个偏好开关 {#exercise-01-case-a}

从一个很小的独立增强开始。如果原生 HTML 与 CSS 足以表示偏好，就使用它们。若还需保存状态和处理事件，而且应用已经使用 Fable 构建管道，则先考虑 Fable 加 Browser.Dom。

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

纯转换决定最终偏好。一个适配器读取并验证存储，另一个设置文档属性，第三个管理媒体查询监听器及其清理。存储失败时使用回退值，但控件仍可操作。开关应使用真正的按钮或单选组，并提供可访问名称和清晰焦点。

暂时不要加入 Elmish 或 React。对于单个交互区域，它们会增加消息循环、渲染器、npm 包、配套升级和生命周期概念，却没有减少实质风险。

多个交互区域需要协调、监听器清理代码重复、URL 成为状态来源，或异步工作让局部分派不一致时，应重新选择。Fable 本身也可能落选。若页面没有 Fable 工具链，而几行审阅过的 JavaScript 维护成本明显更低，就不必为此引入 F#。

验收检查覆盖系统、浅色和深色初始化，无效存储输入，存储被拒，键盘操作，系统偏好变化与清理。还要检查 320 像素布局、生产打包，以及控制台和 HTTP 均无失败。

### 情况 B：带协调副作用的多步预约客户端 {#exercise-01-case-b}

从 Fable.Elmish 核心开始，渲染器暂不定型。该流程包含持续协调状态、URL 状态、多个可能失败的外部操作、重试和竞态。在这种情况下，消息循环可以取代自定义生命周期代码。

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

可用性结果必须匹配当前请求 ID。支付超时不代表可以安全重试：第 36–38 章的服务端契约必须区分已知失败与结果未知，并支持幂等对账。最终价格、容量和预约结果均由服务端决定。

命令负责可用性检查、提交、支付或对账，以及路由更新。每条命令都返回成功、规定的失败、取消或传输不可用消息。订阅只用于网络状态、服务端事件通道等外部数据流，并具有 ID 与清理路径。

从能满足实际 UI 的最小渲染器开始。只有必需组件、渲染易用性或现有 React 代码足以抵消新增 NuGet 与 npm 依赖时，才选择 Feliz 和 React。Elmish 与直接 DOM 可以藏在同一个小型渲染适配器后；状态模型不应暴露渲染器节点。

若代表性流程表明大部分状态彼此独立，消息循环只增加映射而没有简化外部操作，就不选 Elmish。若手写 DOM 更新不利于无障碍或容易出错、必须使用持续维护的 React 组件，或测得渲染压力，则更换渲染器。绑定滞后、升级冲突或包体成本也可能淘汰 React。

验收覆盖深链接、刷新与后退导航，以及无效草稿、乱序可用性结果和重复提交。还要覆盖支付已知失败与结果未知、取消、重试、错误后焦点和屏幕阅读器播报。锁定的生产输出必须通过真实服务契约测试。

### 情况 C：五个必需 React 组件，大部分状态局部化 {#exercise-01-case-c}

从 Feliz 加锁定的 `react` 与 `react-dom` 开始，并通过小型类型化绑定包装每个必需组件。对真正局部的状态使用组件局部 hook。不要仅因为客户端用 F# 编写，就引入一个全应用 Elmish 模型。

为每个组件盘点：

- 锁定的原生包与 peer 版本；
- F# 绑定版本或本地维护的绑定 API；
- 必需属性、事件、ref、promise、null/undefined 与清理；
- 生成的模块格式与生产打包器行为；
- 语义 DOM、焦点、键盘、本地化与错误行为；
- 许可证、安全公告、source map、包体成本，以及谁负责升级。

只有组件内部存在真实工作流时，才引入 `Feliz.UseElmish` 或子 Elmish 程序。只有跨组件转换、外部操作或路由需要统一协调时，才引入根 Elmish 程序。Fable.Elmish.React 用于连接 Elmish 与 React，不会替代 React 本身。

若绑定与原生包的兼容矩阵失败、无障碍输出不合格、错误只在生产出现、包体或运行成本过高，或平台 HTML 足以替代组件，就放弃该方案。仅有成功的 storybook 式演示仍不够；还必须通过生产 Fable/Vite 管道和目标浏览器验证。

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

只有当前 `Pending` 是相同代次与查询的 `Debouncing`，且输入仍对应该查询时，`DebounceElapsed` 才启动请求。随后分配 `requestId`，转为 `Requesting`，保留 `Visible`，并返回搜索命令。

只有当请求 ID 与查询都匹配活动 `Requesting` 情况时，才接受成功、失败和取消。`fable` 已经活动后，任何 `fa` 完成都返回未变模型和空命令。记录聚合陈旧完成计数属于适配器职责，而不是暴露用户输入的模型转换。

非空成功结果变成 `Showing`；零结果变成 `Empty`；已声明失败变成带安全消息的 `Failed`。由新输入造成的取消不会覆盖可见内容，也不会播报错误。真正由用户请求的停止可以使用不同消息与提示。

### 在模型之外管理计时器与 AbortController {#exercise-02-effects}

命令适配器管理按代次或请求 ID 索引的计时器句柄与 `AbortController`。启动新工作时，中止并移除旧句柄；完成或释放时也要移除。模型只保存可序列化的 ID 与状态，不保存浏览器控制器对象。

Abort 是优化与生命周期动作，不是正确性规则。身份守卫仍会拒绝抢在 abort 前完成的响应，或来自无法取消传输的响应。

如果项目始终使用由模型定键的 Elmish 订阅，也可以由订阅管理防抖计时器。订阅 ID 包含当前代次；改变或清除 ID 就会停止旧订阅。不要同时采用命令计时器与订阅计时器。

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

浏览器测试在受控响应下输入两个查询，先完成 `fable`，最后完成 `fa`，并确认页面只保留 `fable`。它还检查键盘输入、焦点保持、不会每次按键都播报的礼貌级 live region、安全错误、取消清理、卸载、控制台与网络失败，以及生产包。

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

把时间和规则作为数据传入。金额必须包含货币，并集中定义小数位数与舍入规则。核心不读取时钟、环境、文件、浏览器存储或序列化器，也不决定配置如何到达。

服务端决定最终价格。浏览器可以为提升响应速度计算预览，但结账仍发送带版本的请求，并显示服务端定价结果。客户端包可能被修改、使用旧规则运行，或具有不同数值行为。

### 把各运行时的 I/O 放入独立适配器 {#exercise-03-adapters}

服务端适配器读取获批配置和当前时间，在启动时加载并验证 JSON 规则文档，保护机密，并通过指定序列化器映射 HTTP DTO。它向核心传入已验证的 `PricingRules` 与 `DateTimeOffset`。

浏览器适配器通过带版本的 HTTP 响应或构建产物接收非机密公开配置。只有产品契约允许使用客户端时间时，它才映射 Web API 时间。JSON codec 必须与服务端传输格式兼容；不要尝试复用依赖反射的服务端序列化器。

如果浏览器必须计算“现在”，就规定使用服务端时间、客户端墙上时间，还是单调时长。到期与最终价格应使用服务端签发的时刻和版本。客户端时钟受用户控制，也可能漂移。

旧反射序列化器留在服务端适配器后，直到契约测试确认替代实现兼容。浏览器 codec 必须按照同一份书面策略处理格式错误，以及字段缺失、多余、null、数字、日期、货币和版本问题。

### 在两个目标上验证语义 {#exercise-03-cross-target}

让相同黄金向量经过 .NET 与 Fable 生成的 JavaScript。包含每种货币小数位、中点舍入、负/零/高金额、数量边界、折扣、税序、日期截止、Unicode 标识符与非法组合。比较声明的金额值、错误和规则版本，而不是内部对象表示。

如果两个测试框架能共享确定性种子或序列化测试数据，就加入生成式边界与属性用例。结果不一致时禁止发布客户端预览；没有领域规则依据时，不能用舍入掩盖差异。

使用 decimal 不能证明运行期行为相同。Fable 记录了目标专属数值表示。在跨目标测试与代表性浏览器测量通过前，只能声称两个目标都能编译。即便通过，服务端仍会重新计算最终价格。

编译成功也无法证明反射、时区、序列化器或文件系统等价；这些功能应从共享核心中有意移除，而不是模拟。

### 锁定、构建、加固并发布 {#exercise-03-release}

发布管道执行：

1. 锁定的 SDK、工具、NuGet 与 JavaScript 依赖还原；
2. .NET 单元测试、属性测试和服务端序列化器契约；
3. Fable 编译，以及生成 JavaScript 对共享黄金向量的执行；
4. Vite 生产构建和包体/许可证/安全公告审阅；
5. 真实服务端/浏览器 HTTP 契约与结账冒烟；
6. 可访问性、CSP、存储、缓存、source map 与秘密扫描；
7. 静态托管的 MIME、基础路径、压缩、缓存、直接路由和错误文档检查；
8. 产物摘要、规则/API 兼容性、发布、监控与回滚记录。

先部署对新 DTO/规则版本的服务端支持，再发布需要它的客户端。在浏览器缓存与回滚窗口内保留旧服务端兼容性。静态资源使用不可变哈希；HTML 入口重新验证，并能回滚到之前兼容的资源集。

以有界计数观察规则版本、客户端/服务端价格不一致、被拒 DTO 版本、结账错误和回滚选择，但不记录购物篮、令牌或个人数据。价格不一致时应阻止提交或显示服务端计算的最终价格；绝不能静默收取客户端预览价格。

满足以下全部条件后，才能删除原混合项目：

- 每个服务端调用方都使用服务端适配器；
- Fable 项目只编译中立核心；
- 两个目标都运行黄金测试；
- 生产不再从核心代码加载文件或环境路径；
- 缓存客户端仍然兼容；
- 回滚不再需要旧布局。

## 答案回顾 {#solution-review}

- 一个独立偏好控件从平台 HTML 或 Fable 开始，不自动引入应用框架。
- 协调的预约状态、外部操作、路由、重试和结果未知可能适合 Elmish，渲染器仍可暂定。
- 必需 React 组件可以支持选择 Feliz/React，但不会自动支持全局 Elmish 模型。
- 绑定与原生 npm 版本、可访问性、生产输出和清理都需要同一份兼容矩阵。
- 防抖代次拒绝旧计时器；请求 ID 拒绝旧网络完成。
- AbortController 节约工作，但身份匹配提供正确性。
- 可见结果、待处理工作、空状态、安全失败和播报彼此独立。
- 浏览器控制器属于副作用适配器，而不是可序列化模型。
- 共享定价接收时间、规则和输入；它不读取目标专属环境。
- 浏览器定价只作预览；服务端计算最终价格。
- Decimal、日期、反射和序列化需要跨目标检查，不能只凭源代码判断。
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
