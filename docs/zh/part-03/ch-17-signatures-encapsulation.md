---
title: "第 17 章：签名、访问控制与面向 F# 的 API"
description: "把 `.fsi` 文件用作经过检查的公共契约，隐藏实现表示，并为 F# 消费者设计小而惯用的 API。"
translationKey: part-03/ch-17-signatures-encapsulation
---

# 第 17 章：签名、访问控制与面向 F# 的 API {#overview}

实现包含让组件工作所需的一切。调用方通常只应依赖更少的内容：稳定的领域名称、安全的构造路径、有用的读取方式，以及能用类型说明结果的操作。F# 签名文件把这份较小的视图变成编译器检查的公共 API。

`Library.fs` 回答“它如何工作？”，与之匹配的 `Library.fsi` 回答“实现文件之外的代码可以知道什么？”编译器会检查实现满足签名，并隐藏签名省略的声明。这不只是生成的文档，约束也比命名约定更严格。

## 签名是消费者的视图 {#signature-as-view}

示例库采用下面的编译结构：

```text
Library.fsi  ── 约束 ──▶  Library.fs
     │                        │
     └──── 可见契约 ──────────┴──▶ 后续文件和程序集
```

签名包含命名空间、模块、类型声明和值签名，却没有函数体。实现包含表示和可执行代码。签名公开的每项声明都必须由实现以兼容方式提供；额外的实现声明对于该实现文件之外的代码保持隐藏。

这是编译期可见性规则，不是运行时调用层。调用 `Capacity.value` 时不会经过 `.fsi` 文件分派。签名影响编译和生成的可见性；运行时执行的仍是实现。

## 签名与实现共同组成一个编译单元 {#paired-files}

项目显式记录了这一对文件：

```xml:line-numbers [Ch17.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Library.fsi" />
    <Compile Include="Library.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
这是仓库中 `examples/chapters/ch17/Ch17.fsproj` 的完整内容。签名和实现使用相同的基本文件名，而且签名紧邻实现之前：先是 `Library.fsi`，然后是 `Library.fs`；最后的 `Program.fs` 充当调用方。颠倒前两个文件的顺序会让编译器过早处理实现；在两者之间插入依赖代码，则会破坏这对文件应有的编译位置。

一个实现文件至多对应一个这样的同名签名文件。签名不是供多个无关 `.fs` 文件不断追加的头文件，后续文件也不能重新打开实现以访问签名省略的声明。

第 16 章的“提供者先于消费者”规则仍然成立。这对文件共同充当一个提供者：签名声明公共内容，实现满足这些声明，后续文件也只能使用这些内容。

## 从外到内阅读签名 {#read-signature}

下面是测试所用的完整公共 API：

```fsharp:line-numbers [Library.fsi]
namespace ThinkingInFSharp.Ch17

module SeatAllocation =
    type CapacityError = NonPositiveCapacity of actual: int

    type Capacity

    module Capacity =
        val create: raw: int -> Result<Capacity, CapacityError>
        val value: capacity: Capacity -> int

    type SeatCountError = NonPositiveSeatCount of actual: int

    type SeatCount

    module SeatCount =
        val create: raw: int -> Result<SeatCount, SeatCountError>
        val value: seats: SeatCount -> int

    type AllocationError = InsufficientCapacity of requested: int * available: int

    type Allocation

    module Allocation =
        val capacity: allocation: Allocation -> Capacity
        val requested: allocation: Allocation -> SeatCount
        val remaining: allocation: Allocation -> int

    val allocate: capacity: Capacity -> requested: SeatCount -> Result<Allocation, AllocationError>
```
应分层阅读：

1. `namespace ThinkingInFSharp.Ch17` 给出稳定的外层路径。
2. `module SeatAllocation` 组织面向 F# 的词汇。
3. 错误联合类型公开具名案例及其数据。
4. `type Capacity`、`type SeatCount` 和 `type Allocation` 公开名称，却不公开表示。
5. 同名模块发布安全的构造与观察函数。
6. `allocate` 发布工作流，容量在前，流动的请求在后。

`val` 关键字引入值或函数的类型。`raw`、`capacity` 和 `requested` 这样的参数标签会出现在工具与公共元数据中，因此它们应描述含义，而不只是照搬局部变量名。

调用方不必知道 `Capacity` 由联合、记录、类还是其他东西实现。它仍可以存储该值、传给 `allocate`，并通过 `Capacity.value` 读取内容。类型可以正常使用，而存储布局不会成为依赖。

## 隐藏受保护值的表示，公开可处理的错误 {#selective-exposure}

签名对不同类型作出了不同选择：

```fsharp
type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity
```

`CapacityError` 是透明的，因为调用方应该匹配拒绝结果并使用 `actual`。`Capacity` 是抽象的，因为调用方不能绕过 `Capacity.create`，也不应依赖其存储方式。“公开类型”不等于“公开表示”。

同样，`AllocationError` 公开容量不足案例，因为请求量和可用量能指导响应。具体数据由 `InsufficientCapacity(requested, available)` 携带。`Allocation` 则隐藏记录字段，因为只有 `allocate` 可以建立下面的关系：

```text
0 < requested ≤ capacity
remaining = capacity − requested
```

读取函数只公开调用方需要的内容，不提供记录构造或复制更新。这延续了第 12 章的智能构造函数模式，并把限制扩展到其他文件和程序集。

不要隐藏每个联合类型或记录。调用方本就应该构造并穷尽匹配全部案例时，公开联合类型最合适；透明数据组合就是预期 API 时，公开记录最合适。只有构造会建立不变量、字段必须同步，或实现演进不应改写调用方代码时，才隐藏表示。

## 实现可以比公共 API 更丰富 {#implementation}

实现提供隐藏的案例、记录字段和函数体：

```fsharp:line-numbers [Library.fs]
namespace ThinkingInFSharp.Ch17

module SeatAllocation =
    type CapacityError = NonPositiveCapacity of actual: int

    type Capacity = Capacity of int

    module Capacity =
        let create raw =
            if raw > 0 then
                Ok(Capacity raw)
            else
                Error(NonPositiveCapacity raw)

        let value (Capacity capacity) = capacity

    type SeatCountError = NonPositiveSeatCount of actual: int

    type SeatCount = SeatCount of int

    module SeatCount =
        let create raw =
            if raw > 0 then
                Ok(SeatCount raw)
            else
                Error(NonPositiveSeatCount raw)

        let value (SeatCount seats) = seats

    type AllocationError = InsufficientCapacity of requested: int * available: int

    type Allocation =
        { Capacity: Capacity
          Requested: SeatCount
          Remaining: int }

    module Allocation =
        let capacity allocation = allocation.Capacity
        let requested allocation = allocation.Requested
        let remaining allocation = allocation.Remaining

    let allocate capacity requested =
        let available = Capacity.value capacity
        let requestedSeats = SeatCount.value requested

        if requestedSeats <= available then
            Ok
                { Capacity = capacity
                  Requested = requested
                  Remaining = available - requestedSeats }
        else
            Error(InsufficientCapacity(requestedSeats, available))
```
在 `Library.fs` 内部，`Capacity` 和 `SeatCount` 的联合案例可用，也可以把 `Allocation` 构造成记录。在实现文件之外，即使 `.fs` 声明没有使用私有修饰符，与之匹配的 `.fsi` 也会隐藏这些表示。

这种分离允许未来的实现改用另一种数值类型、缓存派生值或替换分配记录，只要已发布的类型和行为仍然兼容即可。签名不会证明行为等价；测试仍需保护不变量和语义。

实现还可以包含签名中没有的辅助函数。省略通常比给每个辅助函数都加访问修饰符更清楚，但签名不应只是自动生成后便不再维护的清单。每一项公开声明都是需要长期维护的 API 承诺。

## 签名与实现必须一致 {#matching-rules}

编译器检查的不只是名称。重要的一致项包括：

- 签名公开的命名空间、模块和类型必须存在于实现中；
- 函数输入/输出类型、泛型参数和约束必须匹配；
- 柯里化与元组化的参数结构——即元数——必须匹配；
- 相关的可访问性、`inline` 与 `mutable` 修饰符必须匹配；
- 字面量属性和值必须匹配；
- 记录或可区分联合要么公开全部字段/案例，要么通过抽象声明全部隐藏；
- 公开声明的顺序必须与实现顺序兼容。

例如，下面是两个不同的 API：

```fsharp
// 签名：两个柯里化参数
val allocate: capacity: Capacity -> requested: SeatCount -> Result<Allocation, AllocationError>

// 实现：一个元组化参数——不满足上述签名
let allocate (capacity, requested) =
    // ...
```

编译器会在消费者使用它之前拒绝这对文件。它还会把签名中的参数名称用作公共名称；让签名标签与实现标签一致，可以避免调试和分析信息产生误导。如果项目希望编译器报告参数名称不匹配，可以启用警告 3218。

编译器可以生成初始签名视图，F# Interactive 也会为输入的定义打印推断签名。应把生成结果当作清单，而不是设计：删除辅助项、有意选择抽象、改善参数名称、添加文档，然后让编译器持续同步两份文件。

## 访问控制保护不同范围 {#access-control}

签名补充了普通访问修饰符：

| 机制 | 谁能使用声明？ | 典型用途 |
|---|---|---|
| `private` | 所在类型或模块中的代码 | 局部表示或辅助函数 |
| `internal` | 同一程序集中的任意代码 | 跨文件实现设施 |
| `public` 或常见的省略默认值 | 所在 API 允许的所有消费者 | 受支持的公共 API |
| 从匹配 `.fsi` 中省略 | 仅实现文件中的代码 | 向后续代码隐藏原本会被推断出的声明 |
| `.fsi` 中的抽象 `type T` | 调用方可以使用 `T`，但不能使用省略的案例/字段 | 保留构造不变量与表示自由 |

F# 自身编写的声明不使用 `protected` 关键字。也要记得第 12 章的修饰位置差异：`type private T = ...` 隐藏类型，而 `type T = private ...` 公开类型但隐藏其表示。

`internal` 智能构造绕过路径可被程序集中的每个文件使用，因此无法防住程序集内代码。如果只有 `Library.fs` 需要某个辅助函数，就从签名中省略或设为私有。另一个实现文件确实需要时，再在签名和实现中都声明为 `internal`，并接受更大的可信范围。

可访问性不能矛盾。公开函数不能泄露可访问性更低的参数或返回类型：否则消费者会看到自己无法命名的 API。应从预期消费者出发，让已发布签名里的每种类型至少与公开它的值一样可访问。

## 从代表性用法设计公共 API {#fsharp-facing-api}

本章示例有意面向 F#。它的公共 API 使用：

- PascalCase 的领域类型与联合案例；
- 职责集中的模块中的 camelCase 函数；
- `Capacity.create` 与 `Capacity.value` 这样的类型同名模块；
- 支持部分应用与管道的柯里化函数；
- 用于预期拒绝的 `Result` 与透明错误联合类型；
- 对构造时建立不变量的值使用抽象表示。

这些选择让消费者代码保持直接。下面的片段假定已经打开本章模块；`capacity` 是先前由 `Capacity.create` 得到的值：

```fsharp
open ThinkingInFSharp.Ch17.SeatAllocation

let tryAllocate capacity requested =
    requested |> allocate capacity
```

不要为了假想的 C# 调用方扭曲 F# API。目标受众是 F# 时，F# 联合、option、柯里化函数和模块都很合适。第 27 章会另行设计 C# 边界，届时 .NET 命名、成员、委托和表示选择可能不同。

在冻结签名之前，先写出有代表性的成功、失败、管道和模式匹配调用点。API 紧凑并不自动等于好用：隐藏所有观察方式会迫使消费者求助反射或重复工作，而公开所有辅助函数又会阻碍实现变化。应为真实任务发布最小而完整的一组类型和操作。

## 通过调用方可见的公共 API 测试 {#consumer-tests}

本章用两个调用方检查契约。`Program.fs` 排在签名/实现之后，只能通过 `Capacity.create` 和 `SeatCount.create` 构造值，再通过公共函数完成分配和读取结果：

```fsharp:line-numbers [Program.fs — 核心调用]
let capacity = Capacity.create 6 |> getOk "capacity"
let requested = SeatCount.create 4 |> getOk "requested seats"
let allocation = allocate capacity requested |> getOk "allocation"

printfn
    "allocated requested=%d remaining=%d"
    (allocation |> Allocation.requested |> SeatCount.value)
    (allocation |> Allocation.remaining)
```

这里的 `getOk` 是同一文件中用于示例断言的辅助函数：收到 `Ok value` 就返回 `value`，收到 `Error` 就让示例立即失败。完整文件还检查容量不足路径。这样，片段中的 `capacity`、`requested` 和输出来源都明确，不需要猜测隐藏上下文。

成功调用方证明公开 API 足以完成任务。另一个独立项目引用该程序集，并用预期编译失败的调用证明实现确实被隐藏：

```fsharp:line-numbers [Consumer.fs — 预期错误]
namespace ThinkingInFSharp.Ch17.InvalidConsumer

open ThinkingInFSharp.Ch17.SeatAllocation

module Consumer =
    let invalidCapacity = Capacity 0
```
`Capacity 0` 试图使用实现中的联合案例。公共签名只包含抽象类型名称，所以 F# 10 以 `FS0800` 拒绝该表达式。测试没有用反射检查私有布局，因为公共抽象正是要隐藏布局。

编译期不透明性与行为测试回答不同问题：

| 检查 | 能说明什么 |
|---|---|
| `.fsi`/`.fs` 文件对构建通过 | 实现满足声明的 API |
| 外部消费者构建通过 | 公共 API 无需隐藏名称即可使用 |
| 无效消费者构建失败 | 普通已编译调用方不能使用隐藏表示 |
| 行为测试通过 | 已发布操作保持所声明的结果与不变量 |

这些检查都不能抵御恶意反射、不安全代码、损坏的持久化数据或可信实现内部的 bug。应准确说明保证范围。

在仓库根目录运行正向示例：

```console
dotnet run --project examples/chapters/ch17/Ch17.fsproj -c Release
```

输出固定为：

```text
allocated requested=4 remaining=2
rejected requested=7 available=6
```

再运行隐藏表示用例：

```console
dotnet build examples/expected-errors/ch17-hidden-representation/Ch17HiddenRepresentation.fsproj -c Release
```

第二条命令必须以 `FS0800` 失败；若它构建成功，才表示封装发生了回归。

## 把签名修改视为 API 修改 {#evolution}

只改变隐藏的实现细节，可以让调用方源码保持不变。修改签名中的一行则会改变公共 API：

- 重命名参数标签会影响元数据与工具；重排则会改变调用含义，并可能改变推断类型；
- 改变柯里化/元组化形式或类型会破坏调用；
- 公开表示会让消费者形成难以撤回的依赖；
- 给公开联合类型添加案例会改变消费者必须处理的集合；
- 删除值或缩小其范围会直接破坏消费者。

添加函数通常与现有源代码兼容，但仍会扩大受支持 API，而且可能让宽泛打开模块的代码产生名称冲突。兼容性需要具体评估，不是 `.fsi` 扩展名自动提供的性质。

应把 XML 文档写在调用方可见的公共声明上。签名会成为一页简洁的评审材料，用来检查命名、参数顺序、错误类型和缺少的读取方式。实现仍然负责解释算法与局部决定。

## 在合适的时机添加签名 {#when-to-use}

签名文件适合以下情况：

- 库或组件供外部调用方使用，即使调用方仍在同一仓库；
- 表示必须跨文件保持隐藏；
- 评审者需要简洁且由编译器强制的公共 API 清单；
- API 足够稳定，并且希望任何公共修改都经过明确评审；
- 实现工作不应意外导出辅助函数。

对于短期实验、快速变化的私有应用代码，或者普通访问修饰符已经足够的文件，签名可能为时过早。维护两份文件需要精力；若 API 尚未稳定，频繁而无害的实现修改也可能产生噪声。

应在代表性调用方式显示出合适的 API 后，再生成或编写签名，不要在问题尚未清楚时过早固定它。一旦采用，就让签名与实现文件相邻，认真处理构建警告，并把签名差异作为公共 API 修改来评审。

## 练习 {#exercises}

### 练习 1：设计电子邮件地址文件对 {#exercise-01}

设计 `EmailAddress.fsi` 和 `EmailAddress.fs`。公共 API 需要抽象 `EmailAddress`，以及带 `Blank` 和 `MissingAtSign` 案例的透明 `EmailAddressError`。还要公开 `EmailAddress.create` 与 `EmailAddress.value`，并隐藏实现中的规范化辅助函数。

写出公共签名，勾勒实现，并声明项目顺序。解释后续文件可以使用哪些声明。


::: details 参考答案

#### 公共签名 {#exercise-01-signature}

`EmailAddress.fsi` 可以公开调用方能处理的错误，以及抽象的成功值：

```fsharp
namespace Contacts

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress

module EmailAddress =
    val create: raw: string -> Result<EmailAddress, EmailAddressError>
    val value: address: EmailAddress -> string
```

消费者可以匹配 `Blank` 和 `MissingAtSign`，却没有公开的 `EmailAddress` 联合案例。它们只能通过 `create` 等已发布函数获得该类型。

#### 与之匹配的实现 {#exercise-01-implementation}

`EmailAddress.fs` 提供表示，并让规范化辅助函数不出现在签名中：

```fsharp
namespace Contacts

open System

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress = EmailAddress of string

module NormalizedText =
    let create (raw: string) = raw.Trim()

module EmailAddress =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error Blank
        else
            let normalized = NormalizedText.create raw

            if normalized.Contains('@') then
                Ok(EmailAddress normalized)
            else
                Error(MissingAtSign normalized)

    let value (EmailAddress address) = address
```

`NormalizedText` 是实现内部声明，但只要匹配签名省略它，该文件之外就无法使用。也可以额外声明为 `private`；对后续调用方而言，签名省略已经足够。

项目顺序是 `EmailAddress.fsi`、`EmailAddress.fs`，随后才是任何消费者文件。后续文件能看到错误案例、抽象类型、`create` 和 `value`，却看不到 `NormalizedText` 或 `EmailAddress` 联合案例。

该示例只检查空白与是否含有 `@`；它不声称实现完整的电子邮件地址语法。公共错误名称准确声明了这项有意保持简单的策略。

:::

### 练习 2：收窄过度公开的分配 API {#exercise-02}

评审下面这份拟议的公共签名：

```fsharp
type Allocation =
    { Capacity: int
      Requested: int
      Remaining: int }

val unsafeCreate: capacity: int -> requested: int -> remaining: int -> Allocation
```

重新设计它，使消费者无法构造字段不一致的值。包含最小的构造/工作流和观察函数，并判断容量不足错误的案例是否应该保持可见。再给出一个适合改用透明记录的需求。


::: details 参考答案

#### 用工作流代替构造 {#exercise-02-redesign}

假设 `Capacity` 和 `SeatCount` 已经是受保护类型。公开的分配 API 可以是：

```fsharp
type AllocationError =
    | InsufficientCapacity of requested: int * available: int

type Allocation

module Allocation =
    val capacity: allocation: Allocation -> Capacity
    val requested: allocation: Allocation -> SeatCount
    val remaining: allocation: Allocation -> int

val allocate:
    capacity: Capacity ->
    requested: SeatCount ->
    Result<Allocation, AllocationError>
```

这里没有 `unsafeCreate`。`allocate` 是唯一公开构造入口，因此能建立 `remaining = capacity - requested`，并拒绝超过容量的请求。前两个访问器保留已经验证的受保护类型；剩余座位可以直接返回 `int`，因为零是合法值。

#### 让有用的错误保持透明 {#exercise-02-error}

`AllocationError` 应保持透明，因为调用方需要区分容量不足，并可以在 UI 或 API 响应中使用两个数字。隐藏错误表示会需要替代的谓词或格式化函数，让正常控制流变得更不直接。

如果 `Allocation` 有意作为数据传输或报告快照、其字段类型所允许的每种组合都合法，而且直接构造与复制更新属于消费者契约，那么透明记录很合适。只要三个整数还声称存在调用方可以破坏的关系，它就不合适。

不透明性应保护真实规则，而不是只为禁止方便的记录语法。已发布的观察方式仍须让调用方完成每项受支持任务。

:::

### 练习 3：修复元数并选择辅助函数可见性 {#exercise-03}

某签名声明：

```fsharp
val apply: policy: Policy -> request: Request -> Result<Decision, DecisionError>
```

实现却定义了 `let apply (policy, request) = ...`，另有一个 `traceDecision` 辅助函数。解释 `apply` 为何不匹配，然后修复它。展示如何让 `traceDecision` 仅在实现文件内部可用，以及当同一程序集的另一个后续文件确实需要该辅助函数时，两份声明必须怎样改变。


::: details 参考答案

#### 匹配柯里化签名 {#exercise-03-arity}

签名描述两次应用：

```fsharp
apply policy request
```

元组化实现只接收一个值对，所以元数不同。去掉元组模式即可修复：

```fsharp
let apply policy request =
    // 计算 Result<Decision, DecisionError>
    // ...
```

把签名改为 `val apply: policy: Policy * request: Request -> ...` 也能让文件对一致，但会发布另一种调用约定。当用一个策略进行部分应用是代表性用法时，应保留柯里化形式。

#### 选择最小的辅助函数作用域 {#exercise-03-helper}

如果追踪只在实现文件中使用，就从签名中省略它，并明确表达局部用途：

```fsharp
let private traceDecision decision =
    // ...
```

如果同一程序集的后续文件确实需要它，签名必须公开仅程序集可用的值：

```fsharp
val internal traceDecision: decision: Decision -> string
```

实现必须匹配：

```fsharp
let internal traceDecision decision =
    // ...
```

现在程序集中的后续文件可以调用它，外部程序集则不能。只在 `Library.fs` 中写 `internal`，却从 `Library.fsi` 省略该值，仍会让它在实现文件之外保持隐藏，因为签名就是可见清单。

不要只为方便白盒测试就扩大辅助函数的范围。应优先通过公开行为测试 `apply`；只有其他实现代码确实依赖该函数时，才扩大可见性。

:::


第 18 章将用这些公共类型与操作组合更大的工作流，对比首错停止的 `Result` 串联与独立验证错误累积。

## 资料来源 {#sources}

- [Microsoft Learn：F# 签名文件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn：F# 访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：F# 模块](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
