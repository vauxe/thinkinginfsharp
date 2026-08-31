---
title: "第 39 章：ASP.NET Core 与 F# Web 生态"
description: "了解 F# 如何使用 ASP.NET Core，并根据实际需求选择 Minimal API、控制器或 F# Web 库。"
translationKey: part-07/ch-39-web-ecosystem
---

# 第 39 章：ASP.NET Core 与 F# Web 生态 {#overview}

F# Web 程序可以直接使用 ASP.NET Core，不需要一套专门的 F# 服务器。Kestrel、路由、依赖注入、配置、认证、日志和测试工具都与 C# 项目相同。F# 社区库建立在这个平台之上，提供更适合函数组合的 API。

因此，本章不是框架排行榜。真正的问题是：哪种 API 写法能让当前项目更简单，同时仍让团队看得懂 ASP.NET Core 的行为？通常可以先理解平台原生的 Minimal API，再根据具体需求比较社区库。

本章属于可选的 Web 拓展，不是掌握 F# 语法的必修内容。代码只是页面中的项目模板，用来说明各种 API 的形状；你不需要把它重建成完整项目。当前仓库也不包含原先的 `examples/ecosystem/web` 项目或测试。

术语分成三类：

- `Minimal API`、`RequestDelegate` 和 `HttpContext` 来自 ASP.NET Core；
- `HttpHandler`、`EndpointHandler` 等名称来自具体社区库；
- 记录、可区分联合、模式匹配和计算表达式才是 F# 语言功能。

::: tip 第一次阅读
先理解“F# 可以直接使用 ASP.NET Core”和[抽象层次](#abstraction-level)即可。只有准备开发 Web API 时，才需要继续比较各个框架、测试与迁移方案。
:::

## 从共享平台开始 {#shared-platform}

ASP.NET Core 提供服务器和大多数通用运行时行为。端点可以写成平台委托、控制器操作或 F# 库处理器；无论哪种形式，生产工作都包括：

- 宿主启动、配置、依赖注入与生命周期；
- Kestrel 或另一种受支持的服务器集成；
- 中间件顺序与端点路由；
- 认证方案与授权策略；
- 请求上限、取消、超时、流式处理与响应开始语义；
- 日志、指标、分布式追踪、健康行为与部署；
- `HttpContext`、HTTP 语义、代理、TLS 与不可信输入。

某个库可以为其中一些事项提供更安全的默认值或更易组合的 API，却不能让这些运维语义消失。理解平台是下面每种选择都可迁移的知识。

Microsoft 的 [.NET 10 API 指南](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) 介绍了两种平台方案：Minimal API 和控制器。新建普通 HTTP API 时，可以先考虑 Minimal API；如果需要高级模型绑定、应用模型扩展或 OData，控制器可能更合适。这只是平台提供的起点，并不表示 F# 团队不能使用社区库。

## 检查代表性 Minimal API {#representative-sample}

Web 模板有意远小于第六部分的预约设计。它只回答一个问题：当输入、输出与错误保持显式时，直接的 F# 端点是什么样？

如果你选择重建模板，项目使用 `Microsoft.NET.Sdk.Web`，目标框架为 `net10.0`，并且不需要第三方包。首次还原后应保留新生成的锁文件，不要复制已删除项目的旧依赖结果。公开的 JSON 类型使用普通的 CLR 友好记录，而不是业务层的可区分联合：

阅读下方片段时，先补齐 `Program.fs` 的文件边界：

- 文件以 `namespace ThinkingInFSharp.Ecosystem.Web` 开头，并导入 `System`、JSON、任务以及 ASP.NET Core Builder/HTTP 命名空间；
- 三个 DTO 位于命名空间级；
- `jsonOptions`、`writeJson`、`writeError`、`greet` 和 `map` 位于 `[<RequireQualifiedAccess>] module WebSample`；
- 文件最后是入口模块 `Program`。

代码块为了聚焦而省略了这层外壳，不能各自单独运行。

```fsharp:line-numbers [Program.fs]
[<CLIMutable>]
type GreetingRequestDto =
    { [<JsonPropertyName("name")>]
      Name: string | null }

[<CLIMutable>]
type GreetingResponseDto =
    { [<JsonPropertyName("message")>]
      Message: string }

[<CLIMutable>]
type WebSampleErrorDto =
    { [<JsonPropertyName("code")>]
      Code: string
      [<JsonPropertyName("message")>]
      Message: string }
```
`GreetingRequestDto.Name` 接受 `null`，因为 JSON 是不可信边界。验证会先把这种表示转换成非空白局部 `name`，成功路径才继续。这重复了第六部分的核心教训：宽松的边界表示并不要求宽松的领域。

### 明确写出框架适配 {#explicit-adaptation}

处理器的类型是 `HttpContext -> Task`，随后包装成 ASP.NET Core 的 `RequestDelegate`。它检查媒体类型，采用区分大小写且拒绝未知成员的严格 JSON，验证名称，并且只产生稳定错误代码与消息。

```fsharp:line-numbers [Program.fs]
let private greet (context: HttpContext) : Task =
    task {
        if not (context.Request.HasJsonContentType()) then
            return!
                writeError
                    context
                    StatusCodes.Status415UnsupportedMediaType
                    "unsupported_media_type"
                    "Content-Type must be a JSON media type."
        else
            try
                let! request =
                    JsonSerializer.DeserializeAsync<GreetingRequestDto>(
                        context.Request.Body,
                        jsonOptions,
                        context.RequestAborted
                    )

                match request with
                | null ->
                    return! writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                | value ->
                    match value.Name with
                    | null ->
                        return!
                            writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                    | name when String.IsNullOrWhiteSpace name ->
                        return!
                            writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                    | name ->
                        return! writeJson context StatusCodes.Status200OK { Message = $"Hello, {name.Trim()}!" }
            with
            | :? JsonException ->
                return!
                    writeError
                        context
                        StatusCodes.Status400BadRequest
                        "invalid_json"
                        "The request body is not valid for this endpoint."
            | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
                return raise error
            | _ when context.Response.HasStarted -> context.Abort()
            | _ ->
                return!
                    writeError
                        context
                        StatusCodes.Status500InternalServerError
                        "internal_error"
                        "The request could not be completed."
    }
```
几个细节比代码行数更重要：

- `RequestAborted` 传入反序列化，并在客户端取消时重新抛出；
- 表示格式错误与必要业务输入缺失是不同错误；
- 无效正文和意外异常都不会被返回；
- 响应一旦开始，就不能安全地换成 JSON，因此会中止上下文；
- 处理器返回 `RequestDelegate` 所需的非泛型 `Task`。

F# 10 空值检查还迫使处理器先匹配 `value.Name`，然后才能调用 `Trim`。这种摩擦在此边界很有价值：编译器拒绝假装反序列化字符串一定非空。

最终映射没有隐藏框架：

```fsharp:line-numbers [Program.fs]
let map (application: WebApplication) =
    ArgumentNullException.ThrowIfNull(application, nameof application)

    application.MapPost("/api/greetings", RequestDelegate greet) |> ignore
```
该函数仍在 `WebSample` 模块内。文件末尾的宿主才使它成为可执行程序：

```fsharp:line-numbers [Program.fs]
module Program =
    [<EntryPoint>]
    let main arguments =
        let builder = WebApplication.CreateBuilder arguments
        use application = builder.Build()
        WebSample.map application
        application.Run()
        0
```
这种写法比 Minimal API 的自动参数绑定更底层，目的是把教学示例的输入和错误契约完整展示出来。它并不是建议你手工反序列化每一个请求。[.NET 10 Minimal API 参考](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0) 介绍了内建绑定、验证、响应、过滤器和授权。内建功能满足契约时就直接使用；只有兼容性或错误格式有特殊要求时，才需要接管这些步骤。

### 准确规划契约覆盖 {#sample-evidence}

重建项目后，最低 `TestServer` 契约用例应运行实际路由与处理器，并验证：

- 一个有效正文会被修剪，并返回完全符合预期的成功 JSON；
- 格式错误 JSON、名称缺失、名称空白、属性大小写错误和未知成员都会安全失败；
- 非 JSON 媒体类型返回 `415`，不会进入处理器契约。

即使这些用例通过，它们也没有测试真实套接字、代理、TLS、认证、速率限制、正文大小策略或部署；这些能力不会因为使用 Minimal API 就神秘出现。

## 先选择抽象层次 {#abstraction-level}

比较名称前，先决定团队想要哪类端点词汇。

| 层次 | 典型单元 | 组合风格 | 主要权衡 |
|---|---|---|---|
| 平台 Minimal API | 映射到路由的委托或 `RequestDelegate` | 端点路由与中间件 | 直接访问平台；部分 API 与重载按 C# 使用习惯设计 |
| 平台控制器 | 带特性的类/操作 | 过滤器、模型绑定、应用模型 | 扩展功能成熟；F# 中需要更多对象和特性代码 |
| 函数式微框架 | F# 处理器函数与组合器 | 管道、列表或端点 DSL | 符合 F# 习惯；多一套 API 与包生命周期 |
| 约定较强的应用框架 | 控制器/路由器/应用构建器或生成器 | 约定与结构化模块 | 约定契合时更快；升级或绕过框架的成本更高 |

不要通过计算“Hello world”的源码行数作决定。认证失败、JSON 演进、流式取消、OpenAPI 定制、测试替换和部署诊断才会暴露真实抽象边界。

## 平台原生 Minimal API {#minimal-apis}

当团队已经理解 ASP.NET Core、希望尽量减少依赖，或与 C# 服务共享约定和基础设施时，选择直接 Minimal API。每份平台文档、中间件包、宿主功能和诊断集成都可直接应用，无需经过包装翻译。

优势包括：

- 除 .NET 共享框架外没有社区框架依赖；
- 直接使用端点路由、过滤器、结果、DI、认证与 OpenAPI 集成；
- 易于和 C# 基础设施及示例互操作；
- 可调用 F# 函数式核心的小型组合根；
- 使用标准 ASP.NET 工具进行 `TestServer` 或 `WebApplicationFactory` 集成。

F# 使用 Minimal API 时，常见困难来自委托重载、可空输入、以特性为主的示例，以及偏向 C# 的文档。通常加上明确的类型注解、`RequestDelegate`、小型转换函数和边界 DTO 就能解决。如果端点层逐渐出现大量自制的处理器组合工具，再考虑是否有维护良好的 F# 库已经解决了这个问题。

## 基于控制器的 API {#controllers}

控制器仍是平台原生方案。需要控制器专属扩展点，或组织统一使用控制器过滤器与约定时，可以选择它。若混合语言团队更重视统一的类和操作 API，而不是 F# 端点 DSL，控制器也可能更合适。

F# 能定义控制器类、特性、方法、任务与 CLR DTO。摩擦来自架构而不是互操作不可能：继承、可变绑定模型、特性、操作重载和框架约定可能会压过本来可以用函数与显式数据表达的代码。

保持控制器纤薄。把边界 DTO 转换成有效领域输入，调用工作流，然后穷尽翻译结果。不要为了模仿 C# 教程而把领域状态移进可空控制器属性。

## Giraffe：可组合的中间件式处理器 {#giraffe}

[Giraffe](https://github.com/giraffe-fsharp/Giraffe) 自称函数式 ASP.NET Core 微框架。其核心 `HttpHandler` 模型组合处理器，并能短路或继续 ASP.NET Core 管道。对于希望把路由、绑定、响应与授权表达为可复用 F# 函数的团队，这套词汇很合适。

选择它的理由：

- 成熟的函数式处理器模型，以及大量示例与扩展；
- 在 ASP.NET Core 内显式组合与复用；
- 通过相关包支持 API 和服务器渲染 HTML；
- 现有 Giraffe 与 SAFE 栈代码库熟悉的选择。

需要评估的摩擦：

- 团队需要学习延续传递式处理器类型及其运算符；
- 如果不执行边界，处理器专用抽象可能扩散进应用代码；
- JSON/视图/授权扩展选择会增加各自的兼容矩阵；
- 平台功能可能要求同时理解 Giraffe 顺序与 ASP.NET 中间件顺序。

本书核对的稳定包是 2026 年 7 月发布的 [Giraffe 8.3.0](https://www.nuget.org/packages/Giraffe/8.3.0)。应锁定所选包，并在升级时阅读发布说明；本章没有编译 Giraffe 样例，因此不作更强兼容性主张。

## Falco：面向端点的函数式工具箱 {#falco}

[Falco](https://www.falcoframework.com/) 是 ASP.NET Core 上的函数式优先工具箱。其文档风格从路由与响应函数构建端点值，再把它们安装到 `WebApplication`。它还提供原生 F# 标记引擎及相关 OpenAPI/HTMX 包。

选择它的理由：

- 紧凑且接近 ASP.NET Core 的端点词汇；
- 统一的请求读取与响应函数；
- 产品需要时可使用服务器渲染 F# 标记；
- 标准中间件仍然可用。

需要评估的摩擦：

- 生态更小，因此每项必要集成都值得试验；
- 相关标记、OpenAPI 或 HTMX 包是独立版本决定；
- 安全、宿主与运维仍要求平台知识；
- 日后切换处理器词汇会修改外层 Web 层。

这里核对的稳定包是 [Falco 5.2.0](https://www.nuget.org/packages/Falco/5.2.0)，包含 `net8.0`、`net9.0` 与 `net10.0` 资产。复核当天 NuGet 也列出 6.0 预发布版；本书不会默默把预发布版当成稳定推荐。

## Oxpecker：端点路由之上的 F# 处理器 {#oxpecker}

[Oxpecker](https://github.com/Lanayx/Oxpecker) 构建在 ASP.NET Core 端点路由之上，并继承 Giraffe 许多成功的 API 词汇。文档中的 `EndpointHandler` 是 `HttpContext -> Task`，`EndpointMiddleware` 则围绕下一个处理器组合。相关包覆盖视图、HTMX、OpenAPI 及其他全栈事项。

选择它的理由：

- 直接对齐端点路由，终止处理器类型也接近 `RequestDelegate`；
- 包含类型化路由与响应辅助函数的 F# 优先组合 API；
- 为服务器渲染与 HTMX 型应用提供集成选项；
- 为熟悉 Giraffe 的团队提供迁移指南。

需要评估的摩擦：

- 它更年轻，因此 API 生命周期与生产使用经验不同于较老选择；
- 包目标可能让服务更早绑定到较新的 .NET 运行时；
- 广泛的全栈家族可能诱使团队采用不需要的功能；
- 迁移时看起来相似，不代表每项 Giraffe 行为都相同。

核对的稳定包是 [Oxpecker 2.1.1](https://www.nuget.org/packages/Oxpecker/2.1.1)，其包资产目标为 `net10.0`。运行时规划必须考虑这一事实，但它不表示框架天然更好或更差。

## Saturn：约定丰富的函数式 MVC {#saturn}

[Saturn](https://github.com/SaturnFramework/Saturn) 在 Giraffe 之上提供约定较强的服务器端函数式 MVC 模型，包含应用、路由器和控制器约定。当这些约定契合产品时，它可以减少组件连接工作，对现有 Saturn 或 SAFE 应用尤其如此。

新的 .NET 10 服务应仔细核对 Saturn 的维护状态和目标框架适配。稳定版 [Saturn 0.17.0](https://www.nuget.org/packages/Saturn/0.17.0) 发布于 2024 年 4 月，包含 `net6.0` 资产，并依赖 Giraffe 6.4 或更高版本。NuGet 会计算它与后续 TFM 兼容，但这不能确认每个生成器、依赖、认证路径或部署行为都支持 .NET 10。

所以，既不要把 Saturn 标成“已死”，也不要只看旧教程就选用它。现有系统可能因升级记录与约定价值而继续使用；新系统则应比较当前议题与发布活动、模板输出、传递依赖和所需功能。

## 让版本表准确反映检查结果 {#version-table}

下表是带日期的观察，不是永恒排名：

| 选择 | 2026-08-31 核对的稳定版本 | 本章状态 | 关键采用问题 |
|---|---|---:|---|
| ASP.NET Core Minimal API | 本地核对：.NET SDK 10.0.302；ASP.NET Core 运行时 10.0.10 | 页内项目模板 | 团队能否把面向 C# 的 API 适配限制在边界处？ |
| 控制器 API | ASP.NET Core 10 平台文档 | 仅研究 | 必要的控制器扩展点是否值得增加这些固定代码？ |
| Giraffe | NuGet 8.3.0 | 仅研究 | 延续式处理器组合是否契合团队？ |
| Falco | NuGet 5.2.0 稳定版 | 仅研究 | 聚焦端点与相关包能否覆盖必要集成？ |
| Oxpecker | NuGet 2.1.1，`net10.0` 资产 | 仅研究 | 较新的端点和全栈 API 是否符合运维与升级要求？ |
| Saturn | NuGet 0.17.0，`net6.0` 资产 | 仅研究 | 约定价值是否超过所需的 .NET 10 兼容性验证成本？ |

“页内项目模板”表示正文给出了重建结构，不表示书站附带可执行服务。“仅研究”也不是负面质量判断；采用前应在真实应用中评估。

## 分开那些经常被捆绑的决定 {#separate-decisions}

一个框架名称不应默默替你决定所有 Web 事项。

### API 契约与序列化 {#contract-serialization}

决定外部 JSON 是映射 CLR DTO、通过具名转换器使用 F# 联合，还是遵循 schema 优先契约。明确大小写敏感性、未知成员、空值、版本、错误响应结构和大小上限。只有框架的便捷绑定器能产生预期契约时，它才真正有用。

### OpenAPI 与客户端 {#openapi-clients}

只有生成的 OpenAPI 文档与真实路由、错误响应一致时，才值得信任。决定由特性、端点元数据还是独立 schema 定义规范。至少保留一个消费者测试，例如最终项目的 C# 客户端；格式有效的文档仍可能描述难以使用的 API。

### HTML、HTMX 或独立前端 {#html-frontend}

对于服务器渲染 HTML，应比较默认转义、类型化标记、布局、流式处理、表单、防伪、国际化与工具。Giraffe、Falco 和 Oxpecker 各有不同的视图生态。对于独立浏览器 SPA，后端选择无需决定前端语言；第 41 章会单独介绍 Fable。

### 认证与授权 {#auth}

除非某个包装带来具体且已验证的优势，否则优先使用 ASP.NET Core 认证方案与授权策略。确认中间件顺序、质询/禁止行为、端点元数据、测试替换以及代理/TLS 假设。函数式 `requiresRole` 辅助函数本身不会配置身份验证。

### 依赖注入 {#dependency-injection}

宿主容器在基础设施边界很实用。在组合时取得依赖，或通过显式处理器参数传入，再把小函数传进核心。在业务逻辑里到处访问 `HttpContext.RequestServices`，只是用隐藏依赖代替构造函数中的显式依赖。

## 按使用场景选择 {#scenario-guide}

把下面内容当成待测试的起始假设：

| 场景 | 首选候选 | 重新考虑的理由 |
|---|---|---|
| 混合 C#/F# 平台团队的小型 JSON 服务 | Minimal API | 重复委托/绑定适配器正在变成私有框架 |
| F# 团队想要可复用的函数式 HTTP 管道 | Giraffe | 延续模型或扩展兼容带来的摩擦大于价值 |
| 紧凑 API 或服务器渲染应用需要聚焦 F# 端点工具箱 | Falco | 必要集成缺少可信支持 |
| 团队需要端点路由、Giraffe 式处理器和现代视图/HTMX 选项 | Oxpecker | 运行时目标或较新 API 生命周期与部署策略冲突 |
| 现有约定丰富的 Saturn/SAFE 应用 | Saturn | 升级路径或当前平台验证不足 |
| API 要求控制器专属应用模型或 OData | 控制器 | 需求可用端点路由更简单地满足 |

产品在迁移期间可以使用多种 API 形式，但不要在责任不清时永久保留两种表达同一策略的方式。把函数式框架与平台端点并用在技术上可行，代价是重复的约定、过滤器、错误、元数据和测试。

## 保护函数式核心免受框架变动 {#framework-boundary}

第六部分的架构可以直接迁移：

```text
HTTP framework handler
  -> parse and validate boundary representation
  -> call a small application function
  -> exhaustively map declared result
  -> write stable transport representation
```

把 `HttpContext`、框架处理器类型、绑定特性和响应辅助函数留在 Web 项目。领域与应用项目不应仅因为可执行程序使用某个框架，就引用 Giraffe、Falco、Oxpecker、Saturn 或 ASP.NET Core。

这种限制让框架变化保持有限，但不会让它免费：路由元数据、认证、流式处理、多部分输入、OpenAPI、过滤器和集成测试仍然位于边界。不过，业务不变量与副作用协议可在适配器变化时保持稳定。

## 用四个层级测试边界 {#testing-strategy}

使用四个层级：

1. 验证与工作流决策的纯测试；
2. 只有在无框架调用有意义时才写聚焦处理器测试；
3. 用 `TestServer` 集成测试绑定、路由、中间件、错误正文、认证元数据与取消；
4. 用小型真实进程冒烟检查启动配置、套接字与部署打包。

按照 Microsoft 的[集成测试指南](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)，把较宽测试留给重要基础设施场景。中间件行为重要时，库辅助工具只能补充而不能替代 ASP.NET 管道。让每个候选运行相同契约；断言更少不等于框架更简单。

## 用真实路径评估性能与安全 {#performance-security}

框架基准可以揭示开销模式，却不包含你的 JSON 载荷、认证、日志、数据库、提供商延迟、响应大小、分配特征或失败路径。沿用第 31 章的测量纪律：定义工作负载与预算，剖析完整候选，然后优化测得的瓶颈。

安全性取决于最终运行的整个 HTTP 管线。至少要测试请求体和响应头上限、错误编码、认证失败行为与授权规则。根据应用功能，还可能需要检查 CSRF、输出编码、文件上传、重定向、代理头、速率限制、超时和日志。代码是“函数式”“最小”或“类型化”的，并不自动表示它是安全的。

第三方包会扩大供应链与升级范围。锁定直接依赖、保留锁文件、检查传递变化、阅读发布说明并重新运行契约和安全测试。可复现项目设置中不要复制带 `*` 的安装命令。

## 开展范围受限的采用试验 {#adoption-spike}

限定时间验证一个有代表性的端到端小样：

- 真实 DTO/序列化契约的成功、验证失败与意外失败；
- 必要的认证、授权、取消与 OpenAPI/客户端生成；
- 一条穿过中间件的诊断相关值；
- 锁定的 Release 构建、`TestServer`、真实进程启动与发布；
- 一次兼容升级和明确的删除条件。

先比较正确性缺口、概念、包图、诊断、测试、文档和维护责任，再比较行数。删除落败试验，不要维护多套技术栈。

## 练习 {#exercises}

### 练习 1：为三个团队作选择 {#exercise-01}

分别评估以下团队：

1. 混合 C#/F# 团队在组织统一的 ASP.NET 平台下构建小型内部 JSON API。
2. F# 团队使用可复用函数式处理器与 HTMX 构建服务器渲染 HTML。
3. 一个现有 Saturn 服务迁移到 .NET 10，同时不增加产品功能。

为每个团队选择起始 Web API 形式，至少比较两个候选，并说明它们的包边界与运维边界。最后列出哪些证据会改变选择。


::: details 参考答案

#### 团队 A：统一平台上的混合语言 {#exercise-01-team-a}

从平台 Minimal API 开始。

组织已经运维 ASP.NET Core，服务是小型内部 JSON API，而且 C# 与 F# 工程师都要理解 HTTP 边界。直接端点路由能减少额外的框架间接层，复用标准授权与诊断约定，并把 F# 特有设计留在领域和工作流中。

在两天试验中与 Falco 5.2.0 比较。如果直接实现中反复出现的请求/响应适配占据主体，而且 Falco 的端点词汇显著改善审阅，它就可能胜出。对两者运行相同 DTO、严格 JSON、策略授权、取消、OpenAPI、`TestServer` 与发布检查。如果必要的组织中间件或端点元数据需要无人负责的自定义桥接，就淘汰 Falco。

出现以下情况时，应重新考虑初始选择：

- 五个代表端点反复对抗委托重载或绑定行为；
- Falco 在保留组织现有契约的同时减少边界代码；
- 两种语言的维护者都能顺利调试 Falco 与 ASP.NET 两层；
- 锁定包图通过组织的支持与漏洞策略；
- 升级和事故练习仍然可理解。

不要只因为 C# 工程师熟悉控制器就选择它们。只有真正存在控制器专属扩展——例如所需应用模型或 OData 路径——时才选择。Microsoft 的 [API 概述](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) 明确作出了这种区分。

#### 团队 B：F#、服务器 HTML 与 HTMX {#exercise-01-team-b}

从 Giraffe 8.3.0 与 Oxpecker 2.1.1 的比较开始；只有其 `net10.0` 目标和较新生命周期通过团队支持检查后，才暂定选择 Oxpecker。

Oxpecker 与端点路由一致，提供 `HttpContext -> Task` 请求终结函数，也有配套的视图和 HTMX 包。这些能力符合需求。Giraffe 仍是采用风险较低的备选，因为其处理器生态更成熟，也能搭配独立的视图或 HTMX 集成。

试验必须超越一个计数按钮：

- 转义动态 HTML，并刻意审阅原始 HTML；
- 使用实际 Cookie/认证拓扑的防伪表单；
- 为完整页面与 HTMX 请求渲染验证错误；
- 认证质询与授权禁止行为；
- 路由生成、布局、国际化、静态资源与 CSP 策略；
- `TestServer` 加一项浏览器交互和可访问性检查；
- 包升级与部署诊断。

如果成熟度、现有知识或某项扩展比 Oxpecker 的端点路由优势更重要，就选择 Giraffe。只有 Falco 5.2.0 的标记与 HTMX 支持更适合现有模板时，才把它作为第三候选。不要同时组合三者。

#### 团队 C：现有 Saturn 服务 {#exercise-01-team-c}

不要从重写开始。产品只要求迁移到 .NET 10，功能保持不变。先盘点 Saturn 0.17.0、它的 `net6.0` 资产、Giraffe 依赖、认证包、生成器，以及全部直接和传递警告。

创建一个分支：

1. 在警告即错误下以 `net10.0` 为目标；
2. 从新审阅的锁文件还原；
3. 运行所有现有契约与浏览器测试；
4. 在类生产运行时镜像中发布并启动；
5. 演练认证、错误处理、静态资源、诊断、关闭与负载；
6. 记录不受支持或过时的 API 用法，以及维护者发布的支持状态。

如果它通过组织支持策略，就单独交付运行时迁移，暂时保留 Saturn。成功升级运行时可以降低不确定性，也避免把平台风险与框架重写混在一起。

如果它出现实质问题，就比较迁移到 Giraffe 与迁移到 Minimal API：前者最接近 Saturn 底层处理器模型，后者的长期依赖更少。根据三个代表路由估算，不必先迁移全部 40 个。改善内部风格前，先保留公开契约与认证行为。

:::

### 练习 2：在试验中保留问候契约 {#exercise-02}

从 Giraffe、Falco 或 Oxpecker 中选择一个，设计一项范围很小的试验。它只能替换 `WebSample.map` 及其处理器，并且必须保留：

- 现有的成功和错误 JSON 格式；
- 严格的属性检查；
- 取消行为；
- 原有 HTTP 契约测试。

另外列出包版本、新增的传递依赖、新引入的框架概念，以及什么情况下应删除这项试验。DTO 验证仍留在 Web 边界，不要因为换框架就把它移入业务项目。


::: details 参考答案

#### 限制 Falco 5.2.0 实验范围 {#exercise-02-scope}

创建锁定 Falco 5.2.0 的临时项目或分支。官方包元数据还会把 Falco.Markup 和 FSharp.Core 最低版本带入待审阅的依赖图。记录解析后的锁文件，不要安装不受约束的版本。

只有外层适配器可以变化：

```text
POST /api/greetings
  -> Falco endpoint/handler
  -> existing strict JsonSerializerOptions
  -> existing boundary DTO and name validation
  -> existing stable success/error writers
```

首次实现不强制使用 Falco 的请求与响应辅助函数；现有契约才是实验对照。若辅助函数无法按规定的 `invalid_json` 格式拒绝属性大小写错误或未知成员，就保留手写的严格反序列化器。也可以明确提出契约变更，但不能悄悄削弱测试。

处理器必须把请求取消令牌传给反序列化，并重新抛出客户端取消。它不能把已取消任务转换成 `internal_error`。还必须保留“响应已经开始”后的处理规则，或验证框架提供了等价行为。

#### 运行同一套可执行检查 {#exercise-02-evidence}

先根据本章列出的期望行为为直接模板新建 `WebSampleTests`，再让 Falco 试验原样运行同一组契约用例。只添加真正重要的框架专属断言，例如端点元数据或中间件顺序。然后运行：

- 锁定还原与警告即错误的 Release 构建；
- 未修改的 `TestServer` 契约；
- 一个真实进程 `curl` 成功与格式错误请求；
- 如果属于目标要求，则检查授权与诊断；
- 针对预期部署目标的 `dotnet publish`；
- 包漏洞/许可证审阅，以及一次兼容版本升级。

比较：

| 维度 | 直接样例基线 | Falco 试验问题 |
|---|---|---|
| 公开 HTTP 契约 | 通过的契约用例 | 是否不变？ |
| 概念 | `RequestDelegate`、端点路由 | 端点列表、处理器、请求/响应辅助函数 |
| 直接依赖 | 只有共享框架 | Falco 加解析后的包图 |
| 平台访问 | 直接 | 是否仍能清楚地直接访问 ASP.NET Core？ |
| 诊断 | 标准 ASP.NET 上下文 | 路由名/状态/相关值是否仍可见？ |
| 维护 | Microsoft .NET 支持 | 谁负责 Falco 升级？ |

出现以下任一情况，就删除试验：

- 没有产品理由却改变契约；
- 必要中间件需要自定义桥接；
- 取消或诊断更难追踪；
- 无法通过发布策略；
- 减少的重复代码不足以抵消第二套 API 的维护成本。

若试验胜出，只替换旧路由一次，记录约定，并把框架类型限制在验证与应用模块之外。

本设计刻意不声称试验已经通过。本章展示直接 Minimal API 方法；无论选择哪种实现，都必须在采用它的应用中验证。

:::

### 练习 3：设计可逆迁移 {#exercise-03}

一个服务有 40 个端点。它的处理器、认证辅助函数、OpenAPI 生成和集成测试都依赖某个 F# 框架。请设计逐步迁移到 Minimal API 或另一个函数式框架的方案，并说明：

- 如何一次迁移一条路由并保持兼容；
- 新旧实现如何共享错误格式和 DTO；
- 认证由哪一层负责；
- 如何避免新旧路由冲突；
- 如何比较迁移前后的契约；
- 发布时观察哪些信号；
- 满足哪些条件后才能删除旧框架包。


::: details 参考答案

#### 先盘点行为，再盘点处理器 {#exercise-03-inventory}

建立路由目录，包含方法、规范化模板、请求/响应模式、状态码、认证方案、授权策略、过滤器/中间件、流式/正文上限、OpenAPI 操作、诊断与当前契约测试。搜索 Web 项目之外的框架类型；那是耦合工作，不是路由重写。

提取或确认三处与框架无关的衔接点：

- 边界 DTO 与严格序列化器/错误策略；
- 输入和 `Result` 输出都不提 Web 框架的应用函数；
- 由宿主配置管理的 ASP.NET Core 认证与授权策略。

不要创建一个试图囊括所有框架功能的巨型“通用处理器”层。稳定的接口应由小型应用端口和公开传输契约组成。

#### 迁移一条路由且不产生冲突 {#exercise-03-routing}

维护一份机器检查的路由分配表：每个 `(method, route pattern)` 只属于一个旧或新映射器。迁移期间组合根同时安装两者。构建或启动测试枚举端点数据源，并拒绝重复的方法与模板组合。

选择一条风险较低、但仍涵盖 JSON、授权、一个应用结果和一个错误的代表路由。在新框架适配器中映射它，并在同一变更中从旧映射移除。然后原样运行原契约测试。

如需进一步比较，可在测试中用私有前缀分别托管新旧版本，发送相同请求，再比较规范化状态、响应头和 JSON 语义。不要把两个前缀暴露到生产，也不要逐字节比较会变化的响应头。

认证继续由 ASP.NET Core 方案与策略管理。验证新端点携带等价策略元数据，并产生相同的质询与禁止行为。使用已认证的测试配置得到 `200`，并没有覆盖未认证与未授权路径。

OpenAPI 比较应先规范化顺序和已知生成器元数据，再检测被删除操作、模式变化、状态变化与安全要求。审阅有意变化，不要自动覆盖基线。

#### 推出并移除旧包 {#exercise-03-rollout}

分批迁移路由，同时观察每个模板的流量、状态、延迟、取消与错误分类。部署平台支持时，对同一个不可变产物做金丝雀。回滚在产物层完成。除非已有明确需求，否则不要按请求动态切换两个处理器。

只有满足以下条件，才能移除旧框架：

- 没有路由、中间件、视图、绑定类型或测试辅助函数使用它；
- 没有应用/领域项目直接引用它，或按设计传递引用它；
- 端点枚举只显示一次预期完整路由集；
- 认证、OpenAPI、诊断、真实进程冒烟与发布测试通过；
- 锁文件与部署产物不再包含该包；
- 最后一个旧框架产物的回滚窗口已关闭。

如果迁移停滞，就明确记录每条路由由谁处理。即使暂时保留混合边界，也比在隐藏辅助函数仍控制策略时假装旧包已消失更安全。

:::


第 40 章会从 HTTP 边界转向数据访问、类型提供程序、分析、可视化与机器学习——在这些领域，工作负载特征同样比万能技术栈更重要。
