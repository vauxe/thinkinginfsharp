---
title: "第 28 章：示例测试、替身与边界测试"
description: "根据失败风险选择纯值测试、手写确定性替身和真实序列化契约测试，而不是测试实现细节。"
translationKey: part-05/ch-28-testing-boundaries
kind: chapter
part: 5
chapter: 28
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - foundation-example-tests
  - foundation-contract-tests
exerciseIds:
  - ch28-exercise-01
  - ch28-exercise-02
  - ch28-exercise-03
termIds: []
sources:
  - id: microsoft-dotnet-testing
    url: https://learn.microsoft.com/en-us/dotnet/core/testing/
    checked: "2026-08-24"
  - id: microsoft-fsharp-xunit
    url: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-fsharp-with-xunit
    checked: "2026-08-24"
  - id: microsoft-unit-test-practices
    url: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices
    checked: "2026-08-24"
  - id: system-text-json-unmapped
    url: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members
    checked: "2026-08-24"
  - id: system-text-json-casing
    url: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing
    checked: "2026-08-24"
---

# 第 28 章：示例测试、替身与边界测试 {#overview}

测试不是对源码结构的第二份描述，而是针对风险的可执行证据。计算总价写错，最小输入—输出例子就能定位；工作流在失败后仍保存订单，需要观察端口协议；JSON 字段名或反序列化选项漂移，只有调用真实序列化器的契约测试才能发现。

因此先问“这次失败需要证明什么”，再选择测试层。若每项测试都启动数据库，反馈会慢且难定位；若所有测试都替换序列化器和数据库，又无法证明真实边界仍匹配。好的测试组合让便宜证据覆盖大部分逻辑，把较昂贵证据集中在确有边界风险的地方。

## 学完本章你将能够 {#outcomes}

学完本章后，你应该能够：

- 根据纯计算、端口协议、库配置或真实基础设施风险选择测试层；
- 用 F# 值和结构相等直接断言纯函数结果；
- 为错误案例断言携带上下文的精确反例；
- 用函数记录组成小型、确定性的测试替身；
- 区分结果状态断言与必要的可观察交互断言；
- 用真实 `System.Text.Json` 配置测试 DTO 契约；
- 避免测试私有函数、内部调用顺序和无语义的 JSON 文本细节；
- 编写快速、隔离、可重复、自检且名称清楚的 xUnit 测试；
- 在红—绿—重构循环中让测试约束行为而非实现。

## 从风险选择最小充分证据 {#risk-matrix}

“单元”不必等于一个类或一个函数。它是本次测试有意控制的工作单元。下面的层次各回答不同问题：

| 风险 | 最小充分测试 | 真实参与者 | 通常避免 |
|---|---|---|---|
| 计算、分支、不变量 | 纯值示例测试 | 领域函数与普通值 | 时钟、网络、文件、数据库 |
| 工作流如何使用端口 | 带手写替身的单元测试 | 工作流；端口由确定性函数替代 | mock 框架、真实基础设施 |
| 序列化、C# 表面、数据库映射 | 边界契约测试 | 真正的库、选项、元数据或适配器 | 整个应用宿主 |
| 多个组件和基础设施能否共同工作 | 集成测试 | 需要组合的真实组件 | 替换正在验证的边界 |
| 用户关键路径 | 少量端到端测试 | 已部署形状的完整路径 | 穷举所有领域分支 |

测试名称不决定证据强度。一个名为“integration”的测试若替换了真实协议，仍不能证明该协议；一个在内存中运行的 JSON 契约测试却能真实验证序列化配置。分类用于解释范围，不用于争论标签。

## 纯函数最适合用值测试 {#pure-value-tests}

共享样例先把命令、产品快照、草稿和错误表示为普通 F# 值。`decide` 的唯一输入是产品快照与已验证命令，唯一结果是 `Result`：

<<< @/../examples/chapters/ch28/OrderWorkflow.fs#pure-decision{fsharp:line-numbers} [OrderWorkflow.fs]

没有隐藏时钟、数据库或随机数，所以测试无需搭建对象图。安排输入，调用一次，再比较完整结果：

```fsharp
let private expectOk result =
    match result with
    | Ok value -> value
    | Error error -> failwithf "Expected Ok, received Error %A" error

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

第二个测试选择最小的库存反例：请求 3、可用 2。`InsufficientStock(3, 2)` 不只说明失败，还固定调用者诊断或恢复所需的上下文。如果将来算法仍拒绝请求但交换了两个数字，测试会指出契约已经改变。

### 断言输出，不复制算法 {#assert-output}

测试中的期望值应直接写成小而明确的例子。不要在测试里重新计算 `decimal quantity * unitPrice`、复制生产过滤器，或用循环重新实现分支；相同错误可能同时存在于实现与“期望算法”中。

一个测试可有多个断言，只要它们共同证明一个行为。例如 JSON 形状测试同时检查三个字段名与值，失败原因仍是一个输出契约。反过来，一个测试若混合价格、保存失败和序列化，任何红灯都难以定位，应拆开。

结构相等并不意味着断言越大越好。若一个巨大聚合中有许多与当前行为无关的字段，构造完整期望值会让无关演进破坏测试。此时断言能表达该行为的最小有意义投影。

## 工作流测试需要可控端口 {#port-tests}

纯核心之外，样例工作流读取产品、读取时间并保存订单。依赖被写成函数记录，效果顺序由一个短 `match` 清楚限定：

<<< @/../examples/chapters/ch28/OrderWorkflow.fs#ports-workflow{fsharp:line-numbers} [OrderWorkflow.fs]

成功分支才读取时钟并保存；决策失败直接返回错误。成功测试用几个闭包组成端口：固定返回产品和时间，用 `ResizeArray` 记录查询与保存，并用计数器记录时钟读取：

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

这些是测试替身的组合用法：固定返回产品的函数像 stub，收集调用的列表像 spy，一个完整但简化的内存实现通常称为 fake；“mock”常指预先编排并验证期望交互的替身。术语会因团队和工具而异，代码应明确说明它提供什么值、记录什么事实。

这里只需普通函数和值，所以引入动态代理或重量级 mock 框架不会增加证据。若接口很大、跨语言代理有价值或团队已有统一工具，框架可能合适；它仍不应成为把每个内部调用都写进测试的理由。

### 状态与行为断言各有位置 {#state-behavior}

成功测试先断言返回的 `PlacedOrder`，这是调用者可见状态；再断言查找的 SKU、保存的订单和一次时钟读取，这是端口协议。失败测试断言错误，并证明没有读取时钟或保存，因为“失败没有副作用”是工作流的真实承诺。

不要断言 `decide` 被调用一次、先执行了哪个管道操作、或使用了 `Result.map` 还是 `match`。这些是实现选择；只要结果和端口协议不变，重构不应破坏测试。只有顺序本身影响外部含义时——例如先提交数据库再发布消息——才把顺序提升为契约并明确测试。

### 确定性来自显式输入 {#determinism}

测试把 `2026-08-24T09:30Z` 作为 `GetUtcNow` 的固定返回值。它不读取 `DateTimeOffset.UtcNow`，不 `Sleep` 等待，不依赖当前区域设置，也不连接共享服务。相同源码和输入应在任意顺序、任意机器上得到相同结果。

若代码很难替换时间、随机数、环境变量或 I/O，先回到第 20 章：把效果捕获为参数或小端口。测试可用性是显式依赖设计的副产品，不需要为了测试给私有实现增加公开成员。

并行测试尤其要求没有共享可变全局状态。每个测试创建自己的记录列表和计数器；资源测试则用 `use`/`use!` 在测试内取得和释放所有权。用重试掩盖偶发失败会降低证据可信度，应先找出时间、顺序或外部状态依赖。

## 契约测试要调用真实边界 {#contract-tests}

第 27 章把 DTO 与领域命令分开。本章使用真实 `System.Text.Json` 和实际选项证明该边界：camel-case 输出、大小写敏感输入、未知字段拒绝，以及 DTO 到智能构造命令的转换。

<<< @/../examples/chapters/ch28/OrderWorkflow.fs#json-boundary{fsharp:line-numbers} [OrderWorkflow.fs]

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

它没有比较整个 JSON 字符串，因为对象属性顺序和空白通常不是 JSON 消费者的语义契约。若业务协议确实要求规范化字节以签名或哈希，那是另一项明确风险，应另写规范化测试。

### 宽容或严格都必须是有意选择 {#json-input}

`System.Text.Json` 默认忽略 DTO 上没有对应成员的输入字段；样例把 `UnmappedMemberHandling` 设置为 `Disallow`，所以未知的 `priority` 抛 `JsonException`。这不是所有 API 都应严格，而是让实际选择进入测试。

输入契约还验证合法 JSON 能通过智能构造、JSON `null` 仍是 `MissingBody`、缺失引用字段保留为缺失错误、缺失 `int` 形成默认零并被拒绝：

```fsharp
[<Fact>]
let ``unknown json members fail instead of disappearing silently`` () =
    Assert.Throws<JsonException>(fun () ->
        PlaceOrderJson.deserialize
            """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2,"priority":true}"""
        |> ignore)
```

若协议选择前向兼容并忽略未知字段，就把选项设为宽容，并用同样的真实测试证明未知字段不会改变已知值。测试固定的是产品决策，而不是某篇文档的默认值。

契约测试也可用于第 27 章的 C# 可见签名、数据库列映射、消息头或 HTTP 状态。关键是让真正负责转换的库或适配器参与；若把它替换为假对象，测试只能证明假对象符合自己的设定。

## 写出能长期存活的测试 {#durable-tests}

### 名称先描述场景与结果 {#test-names}

`pure decision reports the exact stock counterexample` 和 `failed decision does not read the clock or save` 不需要打开实现就能说明行为。F# 的双反引号名称适合写可读句子；`[<Fact>]` 让 xUnit 发现无参数事实，`[<Theory>]` 则适合一组明确的数据行。

Arrange—Act—Assert 是阅读边界，不要求机械注释。较短测试可以用空行区分准备、唯一动作和断言；若准备代码淹没行为，就提取只创建合法值的测试 helper，而不要把断言或分支隐藏进通用框架。

### 先看红灯是否可信 {#red-green-refactor}

测试驱动循环是：写最小失败测试，运行并确认它因预期原因失败；写最小实现使其通过；在绿灯保护下改善名称、重复与边界。一个从未观察过的绿灯可能根本没有执行目标路径。

本章样例先以缺失类型获得 FS0039 编译红灯，再实现共享 API，最后把 DTO 错误从领域错误中分离并保持 8 项测试绿色。编译失败也可以是有效红灯，只要它准确证明所需契约尚不存在。

### 测试公共行为，不给私有实现上锁 {#implementation-details}

以下信号常表示测试过度耦合：重命名私有 helper 就失败；等价地把管道改成 `match` 就失败；插入无害缓存就因调用次数失败；mock 配置比业务例子长；为了测试而公开原本私有的成员。

相反，边界字段名、一次且仅一次扣款、失败不保存、事件顺序或幂等键可能确实是公共行为。判断标准不是“交互永远不能测”，而是调用者或外部系统是否能观察并依赖它。

代码覆盖率能指出从未执行的位置，不能证明断言有意义，也不能替代遗漏场景分析。先列风险与不变量，再用覆盖率寻找盲区；不要为了百分比测试简单 getter 或框架代码。

## 运行聚焦测试与完整测试 {#running-tests}

从仓库根目录只运行本章两个测试模块：

```console
dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~Ch28
```

该过滤器分别运行 ExampleTests 与 ContractTests 中的 4 项测试。提交前再运行 `pnpm check:examples`，它锁定还原、构建整个解决方案、运行全部测试并执行所有登记示例。聚焦命令缩短反馈，完整门发现跨章节接线回归；两者不是替代关系。

## 一份实用的选择清单 {#selection-checklist}

为一个行为添加测试时，依次询问：

1. 能否把规则变成纯函数，并直接比较输入与输出值？
2. 若有效果，哪些端口结果必须可控，哪些调用是公开协议？
3. 一个短函数记录是否足够，还是确实需要可复用 fake 或框架？
4. 风险是否来自序列化器、数据库驱动、HTTP 栈、运行时元数据或其他真实边界？
5. 测试是否控制了时间、随机、区域设置、环境、并发与资源所有权？
6. 等价重构能否在保持行为时继续通过？
7. 失败消息能否指出场景、期望和实际反例？

若前两个问题就能提供充分证据，不必升级为端到端测试。若第四个问题为真，也不要用更多 mock 来逃避真正的契约。

## 练习 {#exercises}

### 练习 1：为三类风险选测试层 {#exercise-01}

分别为“折扣总价计算错误”“库存不足后仍保存”“JSON 字段从 `orderId` 漂移成 `OrderId`”选择最小测试层。写出每项测试中真实参与者、替代参与者和最关键断言，并说明为何更大测试没有增加必要证据。

### 练习 2：写一个不锁定实现的替身测试 {#exercise-02}

为 `ProductNotFound` 路径写测试。手写端口记录查询 SKU，并证明时钟与保存未发生。不要断言私有函数名、管道结构或无外部含义的内部调用次数。

### 练习 3：设计一次 JSON 契约演进 {#exercise-03}

产品要增加可选 `note` 字段。决定旧读取方、旧写入方和未知字段策略；列出发布前应增加的输入与输出契约测试。说明若将 `PropertyNameCaseInsensitive` 改为 `true`，哪些输入开始被接受以及这属于什么行为变化。

[阅读本章练习答案](../solutions/ch-28-testing-boundaries)。

## 模型复盘 {#model-review}

- 测试层由风险决定，不由目录名或框架决定。
- 纯函数用小型值和结构相等提供最快、最清楚的证据。
- 精确错误值应保留调用者需要的反例上下文。
- 小函数与记录通常足够构造确定性的端口替身。
- 状态断言优先；只有可观察协议才值得行为断言。
- 契约测试让真实转换库、选项和适配器参与。
- JSON 属性顺序通常不是契约；字段名、类型、缺失与未知策略可能是。
- 时间、随机、共享状态、休眠和真实服务会损害测试可重复性。
- 先确认可信红灯，再实现绿灯，并在行为保护下重构。
- 测试应允许等价重构，同时阻止公共行为与边界契约漂移。

## 来源 {#sources}

- [Microsoft Learn：.NET 中的测试类型、工具与运行方式](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Microsoft Learn：使用 dotnet test 与 xUnit 测试 F#](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-fsharp-with-xunit)
- [Microsoft Learn：单元测试最佳实践](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Microsoft Learn：反序列化时处理未映射 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn：System.Text.Json 属性名大小写匹配](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing)
