---
title: "第 28 章：示例测试、测试替身与契约测试"
description: "根据失败风险选择纯值测试、手写测试替身和真实序列化契约测试，而不是测试实现细节。"
translationKey: part-05/ch-28-testing-boundaries
---

# 第 28 章：示例测试、测试替身与契约测试 {#overview}

测试应验证重要行为，而不是照着源码结构再写一遍。不同风险需要不同测试：

- 总价计算错误时，用最小的输入输出示例即可定位；
- 失败后仍然保存订单时，需要检查是否调用了存储依赖；
- JSON 字段名或反序列化选项改变时，需要使用真实序列化器检查双方约定。

因此，应先问“这个测试要发现哪类错误”，再选择测试层。每项测试都启动数据库，会让反馈变慢，也让失败难以定位。反过来，如果所有测试都替换序列化器和数据库，就无法确认真实组件能否协同工作。

大部分逻辑适合使用运行快、范围小的测试。只有风险确实来自组件集成时，才承担更高的测试成本。

本章代码是一份 xUnit 测试项目蓝图，不对应仓库中的独立示例项目。测试片段假定项目引用 xUnit，并以 `open Xunit` 开始；生产片段则共享下面这组完整的领域定义和 `System.Text.Json` 命名空间。这样，后文的 `PlaceOrderCommand`、`ProductSnapshot`、`OrderDraft` 与 `CommandError` 都不是未展示的前置条件。

```fsharp:line-numbers [OrderWorkflow.fs — 共享模型]
open System
open System.Text.Json
open System.Text.Json.Serialization

type CommandError =
    | MissingOrderId
    | MissingSku
    | NonPositiveQuantity of actual: int

type PlaceOrderCommand =
    private
        { OrderId: string
          Sku: string
          Quantity: int }

module PlaceOrderCommand =
    let create (orderId: string | null) (sku: string | null) quantity =
        if String.IsNullOrWhiteSpace orderId then
            Error MissingOrderId
        elif String.IsNullOrWhiteSpace sku then
            Error MissingSku
        elif quantity <= 0 then
            Error(NonPositiveQuantity quantity)
        else
            Ok
                { OrderId = orderId.Trim()
                  Sku = sku.Trim().ToUpperInvariant()
                  Quantity = quantity }

    let orderId command = command.OrderId
    let sku command = command.Sku
    let quantity command = command.Quantity

type ProductSnapshot =
    { Sku: string
      UnitPrice: decimal
      Available: int }

module ProductSnapshot =
    let create sku unitPrice available =
        { Sku = sku
          UnitPrice = unitPrice
          Available = available }

type OrderDraft =
    { OrderId: string
      Sku: string
      Quantity: int
      Total: decimal }

type OrderDecisionError =
    | ProductNotFound of sku: string
    | InsufficientStock of requested: int * available: int
```

## 选择覆盖风险的最低成本测试 {#risk-matrix}

“单元”不必等于一个类或一个函数。它是本次测试有意控制的工作单元。下面的层次各回答不同问题：

| 风险 | 最小充分测试 | 真实参与者 | 通常避免 |
|---|---|---|---|
| 计算、分支、不变量 | 纯值示例测试 | 领域函数与普通值 | 时钟、网络、文件、数据库 |
| 工作流如何使用依赖 | 带手写替身的单元测试 | 工作流；依赖由结果确定的函数替代 | mock 框架、真实基础设施 |
| 序列化、C# API、数据库映射 | 集成契约测试 | 真正的库、选项、元数据或适配器 | 整个应用宿主 |
| 多个组件和基础设施能否共同工作 | 集成测试 | 需要组合的真实组件 | 替换正在验证的集成点 |
| 用户关键路径 | 少量端到端测试 | 接近部署环境的完整路径 | 穷举所有领域分支 |

测试名称并不决定它能检查什么。名为“integration”的测试如果替换了真实协议，仍然无法验证该协议；在内存中运行的 JSON 契约测试却可以验证实际序列化配置。分类只用于说明范围，不值得争论标签。

## 纯函数最适合用值测试 {#pure-value-tests}

示例先把命令、产品快照、草稿和错误表示为常规 F# 值。`decide` 只接收产品快照与已验证命令，并返回一个 `Result`：

```fsharp:line-numbers [OrderWorkflow.fs]
module OrderDecision =
    let decide (product: ProductSnapshot option) (command: PlaceOrderCommand) : Result<OrderDraft, OrderDecisionError> =
        let requestedSku = PlaceOrderCommand.sku command
        let requestedQuantity = PlaceOrderCommand.quantity command

        match product with
        | None -> Error(ProductNotFound requestedSku)
        | Some snapshot when not (StringComparer.Ordinal.Equals(snapshot.Sku, requestedSku)) ->
            Error(ProductNotFound requestedSku)
        | Some snapshot when requestedQuantity > snapshot.Available ->
            Error(InsufficientStock(requestedQuantity, snapshot.Available))
        | Some snapshot ->
            Ok
                { OrderId = PlaceOrderCommand.orderId command
                  Sku = requestedSku
                  Quantity = requestedQuantity
                  Total = decimal requestedQuantity * snapshot.UnitPrice }
```
没有隐藏时钟、数据库或随机数，所以测试无需搭建对象图。安排输入，调用一次，再比较完整结果：

```fsharp
open Xunit

let private expectOk result =
    match result with
    | Ok value -> value
    | Error error -> failwithf "Expected Ok, received Error %A" error

let acceptedRequest =
    PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 |> expectOk

let acceptedSnapshot = ProductSnapshot.create "FSP-BOOK" 19.50M 5

[<Fact>]
let ``pure decision returns the complete accepted draft`` () =
    let expected =
        { OrderId = "ORD-28"
          Sku = "FSP-BOOK"
          Quantity = 2
          Total = 39.00M }

    Assert.Equal(
        Ok expected,
        OrderDecision.decide (Some acceptedSnapshot) acceptedRequest
    )

let request =
    PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 3 |> expectOk

let snapshot = ProductSnapshot.create "FSP-BOOK" 19.50M 2

[<Fact>]
let ``pure decision reports the exact stock counterexample`` () =
    Assert.Equal(
        Error(InsufficientStock(3, 2)),
        OrderDecision.decide (Some snapshot) request
    )
```

第一个测试把接受结果与一个 `OrderDraft` 值比较。记录、联合和 `Result` 的结构相等让断言保持在领域词汇中，不必逐字段调用 getter，也不必验证执行过哪个私有辅助函数。

第二个测试选择最小库存反例：请求 3、可用 2。`InsufficientStock(3, 2)` 既说明失败，也保留调用方诊断或恢复所需的上下文。如果将来算法仍拒绝请求，却交换了两个数字，测试会指出输出行为已经改变。

### 断言输出，不复制算法 {#assert-output}

测试中的期望值应直接写成小而具体的例子。不要在测试里重新计算 `decimal quantity * unitPrice`、复制生产过滤器，或用循环重写分支；否则同一个错误可能同时存在于实现与“期望算法”中。

一个测试可以有多个断言，只要它们共同检查同一行为。例如，JSON 输出测试同时检查三个字段名和值，描述的仍是一个 API 结果。反过来，一个测试若混合价格、保存失败和序列化，失败时就难以定位，应拆开。

结构相等并不意味着断言越大越好。若一个巨大聚合中有许多与当前行为无关的字段，构造完整期望值会让无关演进破坏测试。此时只断言表达该行为所必需的最小部分。

## 工作流测试需要可控依赖 {#port-tests}

纯核心之外，示例工作流读取产品、读取时间并保存订单。依赖被写成函数字段记录，一个简短的 `match` 清楚限定了副作用顺序：

```fsharp:line-numbers [OrderWorkflow.fs]
type PlacedOrder =
    { OrderId: string
      Sku: string
      Quantity: int
      Total: decimal
      PlacedAt: DateTimeOffset }

type OrderPorts =
    { FindProduct: string -> ProductSnapshot option
      GetUtcNow: unit -> DateTimeOffset
      SaveOrder: PlacedOrder -> unit }

module OrderWorkflow =
    let place (ports: OrderPorts) (command: PlaceOrderCommand) : Result<PlacedOrder, OrderDecisionError> =
        let product = command |> PlaceOrderCommand.sku |> ports.FindProduct

        match OrderDecision.decide product command with
        | Error error -> Error error
        | Ok draft ->
            let placed =
                { OrderId = draft.OrderId
                  Sku = draft.Sku
                  Quantity = draft.Quantity
                  Total = draft.Total
                  PlacedAt = ports.GetUtcNow() }

            ports.SaveOrder placed
            Ok placed
```
只有成功分支才读取时钟并保存，决策失败会直接返回错误。成功测试用几个闭包构造依赖：固定返回产品和时间，用 `ResizeArray` 记录查询与保存，再用计数器记录时钟读取：

```fsharp
let request =
    match PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 with
    | Ok value -> value
    | Error error -> failwithf "unexpected input: %A" error

let snapshot = ProductSnapshot.create "FSP-BOOK" 19.50M 5
let now = DateTimeOffset(2026, 8, 24, 9, 30, 0, TimeSpan.Zero)
let lookups = ResizeArray<string>()
let saved = ResizeArray<PlacedOrder>()
let mutable clockCalls = 0

let ports: OrderPorts =
    { FindProduct =
        fun sku ->
            lookups.Add sku
            Some snapshot
      GetUtcNow =
        fun () ->
            clockCalls <- clockCalls + 1
            now
      SaveOrder = saved.Add }

let expected: PlacedOrder =
    { OrderId = "ORD-28"
      Sku = "FSP-BOOK"
      Quantity = 2
      Total = 39.00M
      PlacedAt = now }

let outcome = OrderWorkflow.place ports request

Assert.Equal(Ok expected, outcome)
Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
Assert.True(([ expected ] = (saved |> Seq.toList)))
Assert.Equal(1, clockCalls)
```

这些值组合了几类测试替身。固定返回值的函数是 stub，收集调用的列表是 spy，完整但简化的内存实现通常称为 fake。“mock”一般指预先设定并验证交互期望的替身。术语会因团队和工具而异，代码应清楚显示它提供哪些值、记录哪些调用。

这里只需要函数和值，引入动态代理或重量级 mock 框架不会增加可信度。接口很大、需要跨语言代理或团队已有统一工具时，框架可能有用；但这仍不是把每个内部调用都写进测试的理由。

### 状态与行为断言各有位置 {#state-behavior}

成功测试先断言返回的 `PlacedOrder`，这是调用方可见状态；再检查查询的 SKU、保存的订单和一次时钟读取，也就是工作流对依赖的调用。失败测试检查错误，并确认既没有读取时钟，也没有保存，因为“失败没有副作用”是工作流的真实约定。

不要断言 `decide` 被调用一次、先执行哪个管道操作，或实现使用 `Result.map` 还是 `match`。这些都是实现选择；只要结果和依赖调用不变，等价重构就不应破坏测试。只有顺序会改变外部含义时，例如必须先提交数据库再发布消息，才专门测试顺序。

### 确定性来自受控输入 {#determinism}

测试把 `2026-08-24T09:30Z` 作为 `GetUtcNow` 的固定返回值。它不读取 `DateTimeOffset.UtcNow`，不用 `Sleep` 等待，不依赖当前区域设置，也不连接共享服务。相同代码与输入应在任意顺序、任意机器上得到相同结果。

如果时间、随机数、环境变量或 I/O 很难替换，先回到第 20 章：把产生副作用的操作作为参数或小型接口传入。清楚的依赖会自然提高可测试性，无须为了测试而公开私有实现成员。

并行测试尤其要避免共享可变全局状态。每个测试创建自己的记录列表和计数器；资源测试则用 `use` 或 `use!` 在测试内获取并释放资源。不要用重试掩盖偶发失败，应找出其中的时间、顺序或外部状态依赖。

## 契约测试要调用真实集成代码 {#contract-tests}

第 27 章把 DTO 与领域命令分开。这里使用真实的 `System.Text.Json` 和实际选项。测试覆盖 camel-case 输出、区分大小写的输入、拒绝未知字段，以及从 DTO 转换到经过智能构造的命令。

```fsharp:line-numbers [OrderWorkflow.fs]
[<CLIMutable>]
type PlaceOrderDto =
    { OrderId: string | null
      Sku: string | null
      Quantity: int }

type DtoError =
    | MissingBody
    | InvalidCommand of CommandError

module PlaceOrderDto =
    let toCommand (dto: PlaceOrderDto | null) =
        match dto with
        | null -> Error MissingBody
        | value ->
            PlaceOrderCommand.create value.OrderId value.Sku value.Quantity
            |> Result.mapError InvalidCommand

module PlaceOrderJson =
    let private options =
        let settings = JsonSerializerOptions()
        settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        settings.PropertyNameCaseInsensitive <- false
        settings.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        settings

    let serialize (dto: PlaceOrderDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserialize (json: string) : PlaceOrderDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceOrderDto>(json, options)
```
`CLIMutable` DTO 可以暂时包含 null 和零；`PlaceOrderDto.toCommand` 把 null body 与领域命令错误明确分开。领域工作流从不接收 DTO。

### 检查 JSON 含义，不检查无关字节 {#json-shape}

输出测试用 `JsonDocument` 解析真正的序列化结果，排序字段名后检查 `orderId`、`sku`、`quantity` 及其值：

```fsharp
let dto: PlaceOrderDto =
    { OrderId = "ORD-28"
      Sku = "FSP-BOOK"
      Quantity = 2 }

use document = JsonDocument.Parse(PlaceOrderJson.serialize dto)
let root = document.RootElement

let propertyNames =
    root.EnumerateObject()
    |> Seq.map (fun property -> property.Name)
    |> Seq.sort
    |> Seq.toArray

Assert.True((propertyNames = [| "orderId"; "quantity"; "sku" |]))
Assert.Equal("ORD-28", root.GetProperty("orderId").GetString())
Assert.Equal("FSP-BOOK", root.GetProperty("sku").GetString())
Assert.Equal(2, root.GetProperty("quantity").GetInt32())
```

测试没有比较整个 JSON 字符串，因为属性顺序和空白通常不影响 JSON 消费方。如果业务协议确实要求规范化字节用于签名或哈希，应把它视为另一项风险，单独编写规范化测试。

### 宽容还是严格必须主动决定 {#json-input}

`System.Text.Json` 默认忽略 DTO 上没有对应成员的输入字段。示例把 `UnmappedMemberHandling` 设置为 `Disallow`，所以未知的 `priority` 会抛出 `JsonException`。并非所有 API 都应严格；这个测试只是记录当前选择。

输入测试还会确认：合法 JSON 能通过智能构造，JSON `null` 仍是 `MissingBody`，缺失引用字段仍报告缺失错误，缺失 `int` 会变成零并被拒绝：

```fsharp
[<Fact>]
let ``unknown json members fail instead of disappearing silently`` () =
    Assert.Throws<JsonException>(fun () ->
        PlaceOrderJson.deserialize
            """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2,"priority":true}"""
        |> ignore)
```

如果协议为了前向兼容而忽略未知字段，就把选项设为宽容，并用同样的真实测试确认未知字段不会改变已知值。测试记录的是产品决策，不是某篇文档中的默认值。

契约测试也可用于第 27 章的 C# 可见签名、数据库列映射、消息头和 HTTP 状态码。真正负责转换的库或适配器必须参与；如果把它替换成假对象，测试只能说明假对象符合自身设定。

## 写出能长期存活的测试 {#durable-tests}

### 名称先描述场景与结果 {#test-names}

以下两个测试名无须查看实现就能说明行为：

- `pure decision reports the stock counterexample`；
- `failed decision does not read the clock or save`。

F# 的双反引号名称可以写成可读句子。`[<Fact>]` 标记无参数测试，`[<Theory>]` 适合多组具体数据。

Arrange—Act—Assert 是一种阅读约定，并不要求机械地添加注释。短测试可以用空行区分准备、一次操作与断言。如果准备代码淹没了被测行为，就提取一个只创建合法值的辅助函数；不要把断言或分支藏进通用测试框架。

### 先确认测试为什么失败 {#red-green-refactor}

测试驱动循环分三步。先写一个最小测试，并确认它因为预期原因失败。再写最小实现使测试通过。最后，在测试仍然通过的前提下改善命名、重复和结构。如果从未看到测试失败，它可能根本没有执行目标路径。

示例最初因缺失类型而出现 FS0039 编译错误，随后实现共享 API，最后把 DTO 错误与领域错误分开，同时保持专项测试全部通过。如果编译失败能直接指出所需 API 尚不存在，它同样可以作为有效的红色测试。

### 测试公共行为，不要绑定私有实现 {#implementation-details}

以下现象通常说明测试过度耦合：

- 重命名私有辅助函数就失败；
- 把管道等价改写成 `match` 就失败；
- 增加无害缓存后因调用次数变化而失败；
- mock 配置比业务示例更长；
- 只为测试而公开原本私有的成员。

序列化字段名、一次且仅一次扣款、失败后跳过保存、事件顺序或幂等键都可能成为公共行为。调用方或外部系统能够观察并依赖某项交互时，就应测试它。

代码覆盖率显示哪些位置执行过，风险与不变量分析则决定哪些场景和断言真正重要。先完成后者，再用覆盖率寻找盲区。把精力放在行为上，而不是简单 getter、框架代码或目标百分比。

在自己的应用解决方案中，可用过滤器快速得到结果。下面是模板命令，必须把路径替换为真实解决方案：

```console
dotnet test path/to/YourSolution.slnx --configuration Release --filter FullyQualifiedName~Ch28
```

该过滤器会选择名称中含 `Ch28` 的测试。提交应用改动前，还应去掉过滤器运行同一解决方案，以检查跨项目连接：

```console
dotnet test path/to/YourSolution.slnx --configuration Release
```

## 练习 {#exercises}

### 练习 1：为三类风险选测试层 {#exercise-01}

为三类风险选择最低成本的测试层：折扣总价错误、库存不足后仍保存，以及 JSON 字段从 `orderId` 变成 `OrderId`。分别写出真实参与者、被替代的参与者与关键断言，并说明更大的测试为何没有增加有效覆盖。


::: details 参考答案

#### 每项测试只运行必要组件 {#exercise-01-selection}

| 风险 | 最小层 | 真实参与者 | 替代参与者 | 关键断言 |
|---|---|---|---|---|
| 折扣总价错误 | 纯值示例测试 | 折扣/定价函数 | 无 | 完整结果等于一个手写金额值 |
| 库存不足后仍保存 | 端口替身单元测试 | 工作流与纯决策 | 产品查询、时钟、保存 | 错误完全匹配；保存和时钟均未调用 |
| `orderId` 漂移为 `OrderId` | JSON 边界契约测试 | DTO、实际 options、`System.Text.Json` | 无序列化替身 | 解析后的输出含 `orderId` 且不含错误拼写 |

折扣规则不需要工作流或序列化器；加入它们只会增加无关失败来源。保存协议必须运行工作流，但固定产品和时间已经足够。真实数据库不会改变“工作流是否请求保存”这一判断。

字段拼写风险来自序列化器及其配置，因此两者都必须真实运行。测试仍可完全在内存中执行；测试真实边界不等于启动服务器。只有要检查 HTTP content type、数据库列名等其他风险时，才增加更外层测试。

先确认每项测试确实能发现目标风险。可临时改错总价期望、让失败分支执行保存，或改变 naming policy。确认对应测试失败后，再恢复实现。

:::

### 练习 2：写一个不锁定实现的替身测试 {#exercise-02}

为 `ProductNotFound` 路径编写测试。手写依赖以记录查询的 SKU，并确认时钟与保存都未调用。不要断言私有函数名、管道形式或没有外部含义的内部调用次数。


::: details 参考答案

#### 只记录调用者能观察的端口协议 {#exercise-02-double}

共享答案创建一个返回 `None` 的产品查询，记录传入 SKU，并为时钟和保存设置独立记录：

```fsharp
let request =
    match PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 with
    | Ok value -> value
    | Error error -> failwithf "unexpected input: %A" error

let lookups = ResizeArray<string>()
let saved = ResizeArray<PlacedOrder>()
let mutable clockCalls = 0

let ports: OrderPorts =
    { FindProduct =
        fun sku ->
            lookups.Add sku
            None
      GetUtcNow =
        fun () ->
            clockCalls <- clockCalls + 1
            DateTimeOffset.MaxValue
      SaveOrder = saved.Add }

Assert.Equal(
    Error(ProductNotFound "FSP-BOOK"),
    OrderWorkflow.place ports request
)

Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
Assert.Equal(0, clockCalls)
Assert.Empty saved
```

测试检查四项相关行为：工作流用规范化后的 `FSP-BOOK` 查询；返回 `ProductNotFound "FSP-BOOK"`；不读取时间；不保存。后两项共同保证“决策失败后不执行成功路径的副作用”。

它没有断言 `OrderDecision.decide` 的调用次数，也没有知道工作流是管道还是 `match`。实现可以增加纯缓存、重命名 helper 或改变组合形式，只要可观察结果和端口协议不变，测试仍通过。

如果产品查询本身抛异常，本章工作流会让异常传播，而且时钟与保存不会运行。是否把它映射为领域错误是另一项产品决策，应以单独测试驱动，而不要在本测试里用 `try/with` 混入第二个场景。

:::

### 练习 3：设计一次 JSON 模式变更 {#exercise-03}

产品要增加可选 `note` 字段。决定旧读取方、旧写入方和未知字段策略；列出发布前应增加的输入与输出契约测试。说明若将 `PropertyNameCaseInsensitive` 改为 `true`，哪些输入开始被接受以及这属于什么行为变化。


::: details 参考答案

#### 先写兼容矩阵再改 DTO {#exercise-03-matrix}

假设 `note` 真正可选且缺失与 null 都表示“没有备注”。发布前至少覆盖：

| 写入方 | 读取方 | 输入/输出 | 期望 |
|---|---|---|---|
| 旧写入方 | 新读取方 | 没有 `note` | 成功，领域得到无备注 |
| 新写入方 | 新读取方 | `note` 为文本 | 成功并保留文本 |
| 新写入方 | 新读取方 | `note` 为 null | 按已记录策略得到无备注或明确拒绝 |
| 新写入方 | 旧读取方 | 含 `note` | 取决于旧读取方未知字段策略 |
| 新写入方 | 任意读取方 | 输出 JSON | 原字段名和类型完全不变；只增加 `note` |

当前样例对未知成员使用 `Disallow`，所以旧读取方若保持该策略，会拒绝新写入方的 `note`。这意味着“给 JSON 增加可选字段”对该消费关系并不兼容。可选字段只描述新 DTO 的验证，不自动保证旧解析器接受它。

有三种可行选择：

1. 先部署能忽略或识别 `note` 的读取方，再部署写入方。
2. 创建有版本的消息或端点。
3. 允许未知字段，同时接受拼写错误可能被忽略的代价。

选择取决于部署顺序和错误检测需求，不能只改一个记录字段。

输出契约测试应解析 JSON 并检查现有三个字段仍在、`note` 按策略出现或省略；输入测试覆盖上述四种载荷以及错误类型。无需固定属性顺序或空白，除非协议另有规范化要求。

#### 大小写宽容是行为变化 {#exercise-03-casing}

启用 `PropertyNameCaseInsensitive` 后，`OrderId`、`ORDERID` 等大小写变体会匹配 `orderId`。正确的 camelCase 输入仍然有效。输入接受集合由此扩大，属于行为兼容性变化，而非二进制签名变化。

扩大接受集合可能帮助迁移，也可能隐藏发送方拼写漂移。用测试确认需要支持的变体可以被接受。还要检查同一对象中出现两个仅大小写不同的字段时会怎样处理。如果结果依赖字段顺序或产生歧义，协议就应拒绝这种输入，或者明确规定唯一行为。

:::


## 来源 {#sources}

- [Microsoft Learn：.NET 中的测试类型、工具与运行方式](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Microsoft Learn：使用 dotnet test 与 xUnit 测试 F#](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-fsharp-with-xunit)
- [Microsoft Learn：单元测试最佳实践](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Microsoft Learn：反序列化时处理未映射 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn：System.Text.Json 属性名大小写匹配](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing)
