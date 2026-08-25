---
title: "第 17 章：签名、访问控制与面向 F# 的 API"
description: "把 `.fsi` 文件用作经过检查的公共契约，隐藏实现表示，并为 F# 消费者设计小而惯用的表面。"
translationKey: part-03/ch-17-signatures-encapsulation
---

# 第 17 章：签名、访问控制与面向 F# 的 API {#overview}

实现包含让组件工作所需的一切。消费者通常只应依赖更少的内容：稳定的领域名称、安全的构造路径、有用的观察方式，以及类型能解释结果的操作。F# 签名文件把这份更小的视图变成编译器检查的契约。

`Library.fs` 回答“它如何工作？”，与之匹配的 `Library.fsi` 回答“实现文件之外的代码可以知道什么？”编译器会检查实现满足签名，并隐藏签名省略的声明。这不只是生成的文档，也比命名约定更精确。

## 学完后你能够做什么 {#outcomes}

学完本章，你应该能够：

- 阅读成对的 `.fsi` 签名与 `.fs` 实现；
- 在项目顺序中把签名紧邻放在实现之前；
- 用 `val` 而不是实现体声明值和函数；
- 公开类型名称，同时隐藏其联合案例或记录字段；
- 有意公开消费者应该匹配的错误联合案例；
- 区分签名省略与 `private`、`internal`、`public`；
- 预测常见的签名/实现不匹配；
- 用模块、柯里化函数和有类型的结果设计小型面向 F# 的 API；
- 从独立消费者程序集测试库，而不耦合到表示；
- 判断何时维护签名文件有价值，何时还为时过早。

## 签名是消费者的视图 {#signature-as-view}

本章库采用下面的编译形状：

```text
Library.fsi  ── 约束 ──▶  Library.fs
     │                        │
     └──── 可见契约 ──────────┴──▶ 后续文件和程序集
```

签名包含命名空间、模块、类型声明和值签名，却没有函数体。实现包含表示和可执行代码。签名公开的每项声明都必须由实现以兼容方式提供；额外的实现声明对于该实现文件之外的代码保持隐藏。

这是信息边界，而不是运行时调用层。调用 `Capacity.value` 时不会经过 `.fsi` 文件分派。签名早已影响编译和生成的可见性；运行时执行的是实现。

## 一对文件占据一个编译边界 {#paired-files}

项目显式记录了这一对文件：

```xml:line-numbers [Ch17.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Library.fsi" />
    <Compile Include="Library.fs" />
  </ItemGroup>
</Project>
```
签名和实现使用相同的基本文件名，而且签名紧邻实现之前：先是 `Library.fsi`，然后是 `Library.fs`。颠倒二者会让编译器在契约之前处理实现；在这对文件之间插入依赖代码，会让该代码处于错误的边界。

一个实现文件至多以这种形式拥有与之匹配的签名。签名不是供多个无关 `.fs` 文件不断追加的头文件，后续文件也不能重新打开实现以触及签名省略的声明。

第 16 章的“提供者先于消费者”规则仍然成立。这对文件共同充当一个提供者：签名声明其可见形状，实现满足它，后续文件只使用这个形状。

## 从外到内阅读签名 {#read-signature}

下面是测试所用的完整公共契约：

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
3. 错误联合类型公开具名案例及其载荷。
4. `type Capacity`、`type SeatCount` 和 `type Allocation` 公开名称，却不公开表示。
5. 同名模块发布安全的构造与观察函数。
6. `allocate` 发布工作流，容量在前，流动的请求在后。

`val` 关键字引入值或函数的类型。`raw`、`capacity` 和 `requested` 这样的参数标签会出现在工具与公共元数据中，因此它们应描述含义，而不只是照搬局部变量名。

调用方不必知道 `Capacity` 由联合、记录、类还是其他东西实现。调用方可以存储它、把它传给 `allocate`，并通过 `Capacity.value` 观察它。这就是抽象表示：类型仍可使用，其形状却不会成为依赖。

## 隐藏携带证明的值，公开可行动的分支 {#selective-exposure}

签名对不同类型作出了不同选择：

```fsharp
type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity
```

`CapacityError` 是透明的，因为调用方应该匹配拒绝结果并使用 `actual`。`Capacity` 是抽象的，因为调用方不能绕过 `Capacity.create`，也不应依赖其存储方式。“公开类型”不等于“公开表示”。

同样，`AllocationError` 公开 `InsufficientCapacity(requested, available)`，因为这些事实能指导响应。`Allocation` 隐藏记录字段，因为只有 `allocate` 可以建立下面的关系：

```text
0 < requested ≤ capacity
remaining = capacity − requested
```

其观察函数准确公开消费者所需内容，却不提供记录构造或复制更新。这延续了第 12 章的智能构造函数模式，但现在保护被明确地施加在跨文件和跨程序集边界上。

不要隐藏每个联合类型或记录。当完整案例集合就是消费者应该构造并穷尽匹配的领域词汇时，公开联合类型最合适。当透明数据组合就是预期 API 时，公开记录最合适。当构造携带证明、字段必须保持同步，或表示演进不应重写消费者代码时，再隐藏表示。

## 实现可以比契约更丰富 {#implementation}

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
在 `Library.fs` 内部，`Capacity` 和 `SeatCount` 的联合案例可用，也可以把 `Allocation` 构造成记录。在实现文件之外，即使 `.fs` 声明本身没有使用私有表示修饰符，与之匹配的 `.fsi` 也会把这些形状从可见 API 中移除。

这种分离允许未来的实现改用另一种数值类型、缓存派生值或替换分配记录，只要已发布的类型和行为仍然兼容即可。签名不会证明行为等价；测试仍需保护不变量和语义。

实现还可以包含签名中没有的辅助函数。省略通常比给每个辅助函数都加访问修饰符更清楚，但签名不应成为只生成一次、之后再也不看的倾倒场。应把每一行公开声明都视为支持承诺来评审。

## 签名与实现必须一致 {#matching-rules}

编译器检查的不只是名称。重要的一致项包括：

- 签名公开的命名空间、模块和类型必须存在于实现中；
- 函数输入/输出类型、泛型参数和约束必须匹配；
- 柯里化与元组化的参数结构——即元数——必须匹配；
- 相关的可访问性、`inline` 与 `mutable` 修饰符必须匹配；
- 字面量属性和值必须匹配；
- 记录或可辨识联合要么公开全部字段/案例，要么通过抽象声明全部隐藏；
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

## 访问控制有多个不同边界 {#access-control}

签名补充了普通访问修饰符：

| 机制 | 谁能使用声明？ | 典型用途 |
|---|---|---|
| `private` | 所在类型或模块中的代码 | 局部表示或辅助函数 |
| `internal` | 同一程序集中的任意代码 | 跨文件实现设施 |
| `public` 或常见的省略默认值 | 所在 API 允许的所有消费者 | 受支持的公共表面 |
| 从匹配 `.fsi` 中省略 | 仅实现文件中的代码 | 向后续代码隐藏原本会被推断出的声明 |
| `.fsi` 中的抽象 `type T` | 消费者可以使用 `T`，但不能使用省略的案例/字段 | 保留构造证明与表示自由 |

F# 自身编写的声明不使用 `protected` 关键字。也要记得第 12 章的修饰位置差异：`type private T = ...` 隐藏类型，而 `type T = private ...` 公开类型但隐藏其表示。

`internal` 智能构造绕过路径可被程序集中的每个文件使用，因此在该程序集内部并不是强不变量屏障。如果只有 `Library.fs` 应该使用某个辅助函数，就从签名中省略它，或把它设为所在模块的私有项。如果另一个实现文件确实需要它，就在签名和实现中都把它公开为 `internal`，并接受更宽的信任边界。

可访问性不能矛盾。公开函数不能泄露可访问性更低的参数或返回类型：否则消费者会看到自己无法命名的 API。应从预期消费者出发，让已发布签名里的每种类型至少与公开它的值一样可访问。

## 从代表性用法设计公共表面 {#fsharp-facing-api}

本章示例有意面向 F#。它的表面使用：

- PascalCase 的领域类型与联合案例；
- 职责集中的模块中的 camelCase 函数；
- `Capacity.create` 与 `Capacity.value` 这样的类型同名模块；
- 支持部分应用与管道的柯里化函数；
- 用于预期拒绝的 `Result` 与透明错误联合类型；
- 对构造时携带证明的值使用抽象表示。

这些选择让消费者代码保持直接：

```fsharp
let tryAllocate capacity requested =
    requested |> allocate capacity
```

不要为了假想的 C# 调用方扭曲 F# API。目标受众是 F# 时，F# 联合、option、柯里化函数和模块都很合适。第 27 章会另行设计 C# 边界，届时 .NET 命名、成员、委托和表示选择可能不同。

在冻结签名之前，先写出有代表性的成功、失败、管道和模式匹配调用点。表面紧凑并不自动等于好用：隐藏所有观察方式会迫使消费者求助反射或重复工作，而公开所有辅助函数又会阻碍实现变化。应为真实任务发布最小而完整的词汇。

## 通过消费者所见的同一边界测试 {#consumer-tests}

本章测试位于另一个项目中，并引用库程序集。它们只能通过 `Capacity.create` 和 `SeatCount.create` 构造值，只能通过公共函数分配，并通过已发布模块观察结果。它们覆盖两个智能构造函数、成功分配和容量不足。

这组正向测试证明表面足够使用。另一个独立的预期错误消费者证明它具有约束力：

```fsharp:line-numbers [Consumer.fs — 预期错误]
namespace ThinkingInFSharp.Ch17.InvalidConsumer

open ThinkingInFSharp.Ch17.SeatAllocation

module Consumer =
    let invalidCapacity = Capacity 0
```
`Capacity 0` 试图使用实现中的联合案例。公共签名只包含抽象类型名称，所以 F# 10 以 `FS0800` 拒绝该表达式。测试没有通过反射检查私有布局，因为消费者契约要忽略的恰恰就是布局。

编译期不透明性与行为测试回答不同问题：

| 证据 | 证明什么 |
|---|---|
| `.fsi`/`.fs` 文件对构建通过 | 实现满足声明的 API |
| 外部消费者构建通过 | 公共表面无需隐藏名称即可使用 |
| 无效消费者构建失败 | 普通已编译调用方不能使用隐藏表示 |
| 行为测试通过 | 已发布操作保持所声明的结果与不变量 |

这些证据都不声称能抵御恶意反射、不安全代码、损坏的持久化数据或受信实现内部的 bug。应诚实声明边界。

## 把签名修改视为 API 修改 {#evolution}

只改变隐藏的实现细节，可以让消费者源代码保持不变。修改签名中的一行则会改变契约：

- 重命名参数标签会影响元数据与工具；重排则会改变调用含义，并可能改变推断类型；
- 改变柯里化/元组化形状或类型会破坏调用；
- 公开表示会让消费者形成难以撤回的依赖；
- 给公开联合类型添加案例会改变消费者必须处理的集合；
- 删除值或缩小其范围会直接破坏消费者。

添加函数通常与现有源代码兼容，但仍会扩大受支持表面，而且可能让宽泛打开模块的代码产生名称冲突。兼容性需要具体评估，不是 `.fsi` 扩展名自动提供的性质。

应把 XML 文档写在消费者可见的公共声明上。签名会成为一页简洁的评审材料，用来检查命名、参数顺序、错误形状和缺失的观察方式。实现仍然负责解释算法与局部决定。

## 在合适的时机添加签名 {#when-to-use}

显式签名文件适合以下情况：

- 库或组件拥有外部消费者，即使消费者仍在同一仓库；
- 表示必须跨文件保持隐藏；
- 评审者需要简洁且由编译器强制的公共 API 清单；
- 表面足够稳定，有意增加修改阻力是好事；
- 实现工作不应意外导出辅助函数。

对于短期实验、快速变化的私有应用代码，或者普通访问修饰符已经能表达所需边界的文件，签名可能为时过早。维护两份文件需要注意力；若表面尚未稳定，频繁而无害的实现修改也可能变得嘈杂。

应在代表性调用点揭示出正确 API 之后，再生成或编写签名，而不是在探索为问题命名之前。一旦采用，就保持文件对相邻，认真对待构建警告，并把签名差异作为公共契约来评审。

## 构建并验证示例 {#build-test}

在示例所在目录运行：

```console
dotnet build Ch17.fsproj -c Release --locked-mode
dotnet test ExampleTests.fsproj -c Release --no-restore --filter FullyQualifiedName~Ch17SignatureTests
```

聚焦测试集会通过。下面这条命令被有意设计为失败，并由独立检查验证：

```console
dotnet build Ch17HiddenRepresentation.fsproj -c Release
```

它必须产生 `FS0800`，以保护“表示被隐藏”这一主张。如果这个无效消费者构建成功，那是回归，不是示例通过。

## 练习 {#exercises}

### 练习 1：设计电子邮件地址文件对 {#exercise-01}

设计 `EmailAddress.fsi` 和 `EmailAddress.fs`。公开调用方需要抽象的 `EmailAddress`、带有 `Blank` 和 `MissingAtSign` 案例的透明 `EmailAddressError`、`EmailAddress.create` 以及 `EmailAddress.value`。实现还需要一个调用方不能看见的规范化辅助函数。

写出公共签名，勾勒实现，并声明项目顺序。解释后续文件可以使用哪些声明。

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

### 练习 3：修复元数并选择辅助函数边界 {#exercise-03}

某签名声明：

```fsharp
val apply: policy: Policy -> request: Request -> Result<Decision, DecisionError>
```

实现却定义了 `let apply (policy, request) = ...`，另有一个 `traceDecision` 辅助函数。解释 `apply` 为何不匹配，然后修复它。展示如何让 `traceDecision` 仅在实现文件内部可用，以及当同一程序集的另一个后续文件确实需要该辅助函数时，两份声明必须怎样改变。

[阅读本章答案](../solutions/ch-17-signatures-encapsulation)。

## 模型回顾 {#model-review}

- `.fsi` 文件是与之匹配的 `.fs` 实现经过编译器检查的消费者视图。
- 签名位于实现之前，发布声明而不是函数体。
- 抽象类型名称让消费者可以使用值，却不能构造或解构表示。
- 当调用方应该匹配可行动的分支时，透明错误联合类型很有用。
- 签名/实现的类型、元数、约束、修饰符和公开顺序必须一致。
- `private`、`internal`、签名省略与抽象表示分别保护不同范围。
- 好的面向 F# 表面小而完整：安全构造、有意义的操作、必要的观察方式和有类型的结果。
- 外部正向测试证明可用性；编译失败的消费者可以证明不透明性。
- 签名修改就是 API 修改，因此应在刻意稳定所带来的收益值得维护成本时再添加显式签名。

第 18 章将使用这套有边界的公共词汇来组合更大的工作流，对比首错停止的 `Result` 串联与独立验证错误的累积。

## 资料来源 {#sources}

- [Microsoft Learn：F# 签名文件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn：F# 访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：F# 模块](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
