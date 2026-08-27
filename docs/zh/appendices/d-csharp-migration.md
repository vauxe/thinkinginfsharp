---
title: "附录 D：从 C# 迁移到 F# 与互操作"
description: "从值、领域模型、失败、异步、集合和公共边界重新设计 C# 系统，而不是逐句转写语法。"
translationKey: appendices/d-csharp-migration
---

# 附录 D：从 C# 迁移到 F# 与互操作 {#overview}

成功的迁移会在保留行为的同时改进模型，而不是逐个把 C# 语法元素换成 F# 语法。两种语言都基于 .NET，因此现有程序集、测试、协议和部署方式可以继续使用；每次只迁移一个可独立替换的接口。

请把本附录当作决策指南。先确认调用方依赖什么、谁管理状态或资源，再选择 F# 表示；如果调用方需要另一种 API 形式，就保留适配器。直接翻译若会隐藏非法状态、副作用或求值时机，就应重新设计这小段接口。

## 非目标：逐行翻译 {#non-goal}

下列形式彼此相似，但没有一项是普适替换：

| 熟悉的 C# 形式 | 可能的 F# 形式 | 真正决定选择的问题 |
|---|---|---|
| 局部变量 | `let` 绑定 | 意图是重新绑定、可变更新，还是派生新值？ |
| `void` 方法 | 返回 `unit` 的函数 | 这是一个副作用，还是本应返回有用数据？ |
| 类或 C# record | F# 记录、联合、类或接口 | 类型表示字段组合、互斥情况、有身份的对象，还是一组行为？ |
| 可空引用 | 非空值、`'T option` 或 `'T | null` | 缺失属于领域，还是只属于边界条件？ |
| 异常 | 异常、`Result` 或领域联合 | 调用者能否把它当作预期分支恢复？ |
| `IEnumerable<T>` | `seq<'T>` | 延迟且可重复的枚举真的是契约吗？ |
| `Task<T>` | `Task<'T>` 或 `Async<'T>` | 工作是否必须接入 .NET Task，并应在何时开始？ |
| LINQ 链 | 集合模块管道 | 需要哪种集合、求值、顺序和分配行为？ |

当新模型能够如实表达问题时，迁移才算完成；新旧文件行数是否相等并不重要。

## 值与表达式 {#values-expressions}

C# 代码常用更新局部变量的语句传达过程；F# 通常为中间结果命名，并把最后一个表达式作为结果：

```fsharp
let subtotal = lines |> List.sumBy (fun line -> line.Price * decimal line.Quantity)
let discount = if subtotal >= 100M then subtotal * 0.10M else 0M
subtotal - discount
```

`let` 默认引入不可变绑定。内层作用域稍后出现的 `let subtotal = ...` 是遮蔽：它创建另一个绑定，并没有更新先前的值。只有在局部可变状态确实能让算法或互操作边界更清楚时，才使用 `let mutable` 与 `<-`。

应翻译意图，而不是照搬控制流写法：

| C# 意图 | F# 起点 | 不应假定 |
|---|---|---|
| 派生一个值 | `let`、`if`、`match`、管道 | 每个中间量都需要可变更新 |
| 转换全部元素 | `List.map`、`Array.map`、`Seq.map` | 三个集合模块具有相同求值方式 |
| 聚合 | `fold`、`sumBy`、小型循环 | 递归必然更清楚或更快 |
| 提前验证 | 在入口检查，再返回或匹配 | 每项检查都应该抛异常 |
| 有状态热点循环 | 局部可变状态或可变 .NET 集合 | 函数式风格禁止受控可变性 |
| 组合行为 | 函数与部分应用 | 每个方法都必须变成柯里化公共函数 |

F# 的 `if`、`match`、`try`、循环和计算表达式都会产生值，因此分支必须具有兼容的结果类型。`unit` 是只有一个值 `()` 的真实类型，并非“没有返回类型”。以副作用结束的函数可返回 `unit`；计算通常应返回它算出的结果。

类型推断消除的是重复，不是类型系统。在公共边界、递归定义、重载 .NET 调用以及领域类型能让意图更易审阅之处添加标注。修改实现前，先从外向内阅读函数签名。

## 按字段组合、互斥状态和对象身份建模 {#data-modeling}

从合法状态出发，而不是从旧声明关键字出发：

| 领域结构 | F# 默认候选 | 在何时保留类或接口 |
|---|---|---|
| 所有具名字段同时存在 | 记录 | 身份、继承、封装可变性或框架构造占主导时 |
| 多个情况中恰有一个 | 可辨识联合 | 公共 API 面向许多 .NET 调用者，而他们难以直接使用编译后的联合类型时 |
| 带不变量的单个基础值 | 私有单案例联合加智能构造函数 | 包装没有增加不变量或语义区分时无需使用 |
| 可选领域值 | `'T option` | 边界契约采用 CLR null 或 `Nullable<T>` 时 |
| 可替换能力 | 函数或小接口 | 生命周期、多成员、模拟工具或 DI 注册更适合对象契约时 |
| 有生命周期的可变实体 | 类，或函数背后的私有可变状态 | 值语义能更准确描述它时无需保留 |

带 `IsAccepted`、`IsRejected` 布尔值和可空载荷字段的 C# 模型可能允许矛盾状态。内部联合可让每个结果只携带该情况的合法数据。除非“未知”确实是领域状态，否则不要只为模仿默认枚举值而添加 `Unknown` 案例。

在 F# 内部用 `option` 表示领域中的缺失。到了公共 .NET API，引用缺失可以写成 `string | null`，值缺失可以写成 `Nullable<int>`。可空标注帮助调用方做静态分析；反射、旧程序集和 null 宽容运算符仍可绕过分析，所以公共入口仍需要运行时参数检查。

`[<CLIMutable>]` 会为 F# 记录生成默认构造函数和 setter。只有序列化器或框架 DTO 确实要求这种构造方式时才有意识地使用它，随后验证并转换为领域类型。它不是让所有记录都变得“对 C# 友好”的捷径。

## 让每种失败对应调用者动作 {#failure-modeling}

按照调用者能够采取的动作分类失败：

| 失败 | 典型表示 | 调用者动作 |
|---|---|---|
| 预期业务分支 | 内部用 `Result` 或领域联合；广泛 .NET 边界用显式响应 | 分支处理、展示、修改输入后重试 |
| 程序员违反 API 契约 | `ArgumentNullException`、`ArgumentException` 或 `ArgumentOutOfRangeException` | 修正调用 |
| 外部资源不可用或状态损坏 | 异常，必要时在应用边界映射 | 按策略重试、降级、记录或中止 |
| 取消 | 保留取消令牌以及取消异常或任务状态 | 停止工作，而不把它报告成普通失败 |

应根据调用方的处理方式选择表示。内存分配失败和不变量破坏通常继续以异常传播；预期的订位拒绝则进入 `Result` 或领域联合。若调用方经常根据某项结果分支，就应让该分支出现在返回类型里。

适配已有异常约定的 C# API 时，先确认哪些异常类型是 API 承诺的一部分。只捕获你能解释的异常，必要时保留原始原因；绝不能捕获所有异常，再把取消转换成一般错误。

## 先选择异步语义，再选择语法 {#asynchrony}

`Async<'T>` 与 `Task<'T>` 都有用，但含义不同。F# async 工作流在显式启动前是冷的；task 表达式会生成任务，并立即执行其同步前缀。已有 .NET API、C# 调用者、ASP.NET Core 和大多数框架扩展点天然使用 `Task`。

| 情况 | 首选起点 | 原因 |
|---|---|---|
| 面向一般 .NET 调用者的公共 API | `Task` / `Task<'T>` | 原生 C# `await` 与常规 .NET 约定 |
| 组合基于 Task 的 .NET API | `task { ... }` | 避免不必要的表示转换 |
| 需要先构造、稍后再启动的 F# 内部工作流 | `async { ... }` | 调用方明确决定何时启动以及如何并行 |
| 同步 CPU 工作 | 先用同步函数，再测量 | 把工作包进任务不会使它变成非阻塞 |
| 高频结果可能受益于 `ValueTask` | 先测量 | 复用和消费规则会增加 API 复杂度 |

调用者需要取消时接受 `CancellationToken`，并把它传给支持取消的操作。不要在异步请求路径内用 `.Result`、`.Wait()` 或 `Async.RunSynchronously` 阻塞。桥接 `Async` 与 `Task` 时保留异常和取消行为，并测试工作是在构造时还是 await/启动时开始。

## 集合是行为契约 {#collections}

`seq<'T>` 是 F# 对 `IEnumerable<T>` 的类型缩写，但仅凭这一事实无法证明它适合作为公共类型。应明确调用者得到的是快照还是实时视图、枚举是延迟还是可重复、顺序是否稳定，以及谁能修改存储。

| 需求 | F# 内部候选 | 跨语言边界候选 |
|---|---|---|
| 不可变、从头处理 | list | 投影为约定的只读视图或数组；不要意外泄漏 `FSharpList<T>` |
| 固定、可索引快照 | array | 只有谁能修改数组已经明确时才直接返回，否则用只读抽象或专用结果 |
| 单向或延迟枚举 | `seq<'T>` | `IEnumerable<T>`，并记录生命周期和可重复性 |
| 由单一组件持有的可增长缓冲区 | `ResizeArray<'T>` | 保持私有，只暴露所需集合操作或快照 |
| 不可变有序查找 | `Map` / `Set` | 若调用方不应依赖 F# 比较规则和编译表示，就转换后再返回 |
| 可变哈希查找 | `Dictionary` / `HashSet` | 若要控制可变性和未来实现，就用接口或领域集合 |

不要把每个 LINQ 查询都替换成 `Seq`：list 与 array 函数是立即求值的，许多 sequence 函数是延迟的，重复枚举还可能重复 I/O 或计算。复杂度、顺序和键契约见[附录 C](./c-collections)。

公共 API 应选择最宽泛、但仍能准确表达需求的输入类型。若确实需要索引、稳定计数，或数据源只能遍历一次，`IEnumerable<T>` 就太弱。表示“零个元素”时绝不返回 `null`，而应返回文档所述类型的空集合。

## 分别采用 F# 与 .NET 调用方熟悉的 API {#api-boundary}

仅面向 F# 的 API 可以暴露记录、联合、option、柯里化函数与 `Async`。面向 C#、VB、重度反射框架或混合团队的库，通常应暴露调用者熟悉的 CLR 类型，同时让内部继续使用地道 F#。

| 内部或面向 F# 的形式 | 通用 .NET API 的选择 | 决策说明 |
|---|---|---|
| 柯里化函数 | 使用元组式参数的类/模块方法 | 参数名会影响调用方的命名实参；改名可能破坏源码兼容 |
| F# 函数值 | `Func<...>` / `Action<...>` 或接口 | 单个操作用委托，较丰富生命周期用接口 |
| `'T option` | 可空引用、`Nullable<T>`、`Try...`、重载或响应对象 | 区分缺失、无效和失败 |
| `Result<'T,'Error>` 或联合 | 响应类/枚举、异常或有文档的层次结构 | 保留所有有意义情况，又不要求 F# 辅助函数 |
| 元组 | 具名记录、类或结构体 | 相比 `Item1`，名称在工具和未来审阅中更清楚 |
| `Async<'T>` | `Task<T>` | 采用调用者的异步约定 |
| F# list/map/set | 常规 .NET 抽象、数组或专用集合 | 保留顺序、资源责任、相等和更新语义 |
| 模块函数 | PascalCase 静态成员式 API 或普通类型成员 | 检查生成后的 C# 调用点，而不只看 F# 源码 |

除非调用方明确选择 F# 专用 API，否则应避免泄漏 `Microsoft.FSharp.*` 类型。把公共类型放入命名空间，遵循 .NET 命名，使用 XML 注释记录公共成员，准确标注可空性，并在运行时验证不受信任的输入。API 稳定后，可用 `.fsi` 签名文件明确导出内容，方便审阅。

二进制、源码、行为和传输格式兼容彼此独立。
参数改名可能破坏 C# 命名参数；新增重载可能让旧源码产生歧义；把返回的拒绝改为异常会改变行为；修改 DTO 字段会改变持久化或网络数据。优先增加兼容桥与 obsolete 迁移路径，而不是悄悄修改已发布成员。

## 阅读可执行的互操作示例 {#executable-pair}

第 27 章把领域选择留在 F# 内部：

```fsharp:line-numbers [Library.fs]
type internal Decision =
    | Accepted of confirmationCode: string * remainingSeats: int
    | Rejected of message: string * suggestedSeats: int option

module internal Decision =
    let evaluate capacity (request: BookingRequest) =
        if String.IsNullOrWhiteSpace request.RequestId then
            Rejected("request id must not be blank", None)
        elif String.IsNullOrWhiteSpace request.Attendee then
            Rejected("attendee must not be blank", None)
        elif request.Seats <= 0 then
            Rejected("seat count must be positive", None)
        elif request.Seats > capacity then
            let suggestion = if capacity > 0 then Some capacity else None

            Rejected($"requested {request.Seats} exceeds available {capacity}", suggestion)
        else
            let normalizedRequestId = request.RequestId.Trim().ToUpperInvariant()
            Accepted($"CONF-{normalizedRequestId}", capacity - request.Seats)
```
一个适配器把内部联合类型及其 `option` 数据转换成四个普通 CLR 公共类型：

```fsharp:line-numbers [Library.fs]
module internal ResponseAdapter =
    let fromDecision decision =
        match decision with
        | Accepted(confirmationCode, remainingSeats) ->
            BookingResponse(BookingOutcome.Accepted, confirmationCode, Nullable remainingSeats, null, Nullable<int>())
        | Rejected(message, suggestedSeats) ->
            let suggestion =
                match suggestedSeats with
                | Some seats -> Nullable seats
                | None -> Nullable<int>()

            BookingResponse(BookingOutcome.Rejected, null, Nullable<int>(), message, suggestion)
```
C# 消费者看到的是普通静态调用、枚举、属性、可空引用和可空值：

```csharp:line-numbers [Program.cs]
var accepted = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-27", attendee: "Lin", seats: 2));

Require(accepted.Outcome == BookingOutcome.Accepted, "accepted outcome");
Require(default(BookingOutcome) == BookingOutcome.None, "valid enum zero value");
Require(accepted.IsAccepted, "accepted flag");
Require(accepted.ConfirmationCode == "CONF-REQ-27", "confirmation code");
Require(accepted.RemainingSeats == 3, "remaining seats");
Require(accepted.ErrorMessage is null, "accepted error must be null");
Require(accepted.SuggestedSeats is null, "accepted suggestion must be null");

Console.WriteLine(
    $"Accepted: outcome={accepted.Outcome} code={accepted.ConfirmationCode} remaining={accepted.RemainingSeats}");
```
同一客户端还用反射断言：只导出 `BookingApi`、`BookingOutcome`、`BookingRequest` 和 `BookingResponse`；任何公共签名都不含 `Microsoft.FSharp.*`；可空元数据正确；XML 文档随程序集一同发布。这些断言直接测试编译后的 API，而不是猜测 F# 源码在 C# 中会是什么样子。

在仓库根目录运行这组已校验项目：

```console
dotnet build examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release
dotnet run --project examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release --no-build
```

## 按可独立替换的接口迁移，而不是按文件夹迁移 {#migration-workflow}

1. **盘点外部依赖。** 记录公共签名、序列化、数据库格式、异常、时序、顺序、null 行为和部署限制。
2. **固定代表性行为。** 换语言前，围绕重要路径和已知边缘情况添加调用方测试。
3. **选择一个切入点。** 依赖很少的纯规则、解析器、计算或适配器，比整层架构更适合先迁移。
4. **在 F# 中建模核心。** 让非法状态更难表示；把时间、I/O、随机性和可变状态通过明确参数或小接口传入。
5. **保留旧调用契约。** 维持薄适配器，让核心在背后改变时，现有 C# 代码仍能编译并保持行为。
6. **编译真实调用方。** 从 C# 测试并检查元数据、可空性、文档、异常和集合行为，而不只运行 F# 单元测试。
7. **验证后再扩大范围。** 接口更简单且测试稳定后，再迁移下一处；若翻译只是在搬运复杂度，就停下来。
8. **按计划移除兼容层。** 删除公共兼容层前，先标记弃用、确定版本并记录迁移方式。

F#/C# 混合解决方案可以是最终形态，并非未完成的迁移。让每种语言留在最适合的位置，并保持共享 API 清楚易用。

## 审阅清单 {#review-checklist}

- 每个重要状态都能构造吗？是否仍能构造矛盾状态？
- 缺失、拒绝、无效输入、基础设施失败与取消是否彼此区分？
- 是否写清了集合的求值时机、可重复性、顺序、所有者与可变性？
- 异步工作是否在调用者预期的时机启动和取消？
- C# 调用点是否无需理解 FSharp.Core 表示类型就能自然阅读？
- 公共入口的运行时检查是否与可空标注一致？
- 是否用测试覆盖参数名、异常、XML 文档和生成的公共类型？
- 是否分别评估源码、二进制、行为与传输格式兼容性？
- 适配器是否足够小，从而让领域规则只有一份？
- 迁移改进了模型，还是只改变了语法？

## 资料来源 {#sources}

- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn：F# 的 null 值与可空检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn：F# 可空值类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [Microsoft Learn：F# 异步编程](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async)
- [Microsoft Learn：F# task 表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [Microsoft Learn：集合设计指南](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/guidelines-for-collections)
- [Microsoft Learn：.NET 库的破坏性变更](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
