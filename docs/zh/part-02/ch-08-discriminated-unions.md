---
title: "第 8 章：可辨识联合与状态建模"
description: "从矛盾布尔标志推导可辨识联合，用案例专属数据与穷尽匹配表达互斥状态和转换。"
translationKey: part-02/ch-08-discriminated-unions
---

# 第 8 章：可辨识联合与状态建模 {#overview}

设预约状态由三个布尔字段表示：`IsPending`、`IsConfirmed`、`IsCancelled`。三个独立开关共有八种组合，但业务也许只允许三种：待处理、已确认或已取消。`true, true, false` 究竟表示什么？如果类型允许构造这种值，每个调用方就不得不处理一个业务上本不该存在的问题。

可辨识联合规定一个值只能属于若干命名案例之一。每个案例只携带对应状态所需的数据：确认码只存在于 `Confirmed`，取消原因只存在于 `Cancelled`。模式匹配会要求代码考虑所有案例。

这里先讨论封闭、同步、内存中的状态表示。第 9 章用 `Result` 表示失败转换，第 12 章保护构造不变量，后面的综合项目再处理持久化与并发状态。

## 独立标志会产生所有真假组合 {#flag-problem}

共享脚本先保留一个故意薄弱的记录：

```fsharp:line-numbers [ch08-discriminated-unions.fsx]
type BookingFlags =
    { IsPending: bool
      IsConfirmed: bool
      IsCancelled: bool }

let contradictoryFlags =
    { IsPending = true
      IsConfirmed = true
      IsCancelled = false }

printfn
    "Flag model contradiction: pending=%b confirmed=%b cancelled=%b"
    contradictoryFlags.IsPending
    contradictoryFlags.IsConfirmed
    contradictoryFlags.IsCancelled
```
记录确保三个字段都存在且为 `bool`，却无法说明“恰好一个为真”。每增加一个开关，组合数量就会翻倍；验证函数必须在每次构造与更新后重新排除矛盾。

更糟的是，案例专属数据常被拆成可空或可选字段：`ConfirmationCode` 在未确认时为何存在？`CancellationReason` 在已确认时该是什么？数据关系只留在约定中。

这不是说布尔值不好。`HasDietaryRequirements` 与 `NeedsWheelchairAccess` 可能是彼此独立、可以同时为真的事实。只有当多个标志实际在模拟“恰好处于一种状态”时，联合才修复模型。

## 联合类型表达有限选择 {#union-definition}

用一个类型列出全部合法状态：

```fsharp:line-numbers [ch08-discriminated-unions.fsx]
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string
```
`BookingStatus` 是可辨识联合；`Pending`、`Confirmed`、`Cancelled` 是三个**联合案例**。一个值在任一时刻只能由其中一个案例构造，因此不存在“同时待处理又已确认”的值。

`Pending` 不带数据。`Confirmed` 带一个命名为 `confirmationCode` 的 `string` 字段，`Cancelled` 带一个 `reason`。这些字段不是三个案例共有的可选属性，而是对应案例的组成部分。

联合是和类型：合法值集合是各案例值集合之和。上一章的记录是积类型：一个值同时包含所有字段。两者经常组合，例如记录保存预约的稳定字段，其中一个字段是 `BookingStatus`。

## 案例名既构造也解构 {#construction}

案例名应用于数据时构造联合值：

```fsharp
let pending = Pending
let confirmed = Confirmed "C-42"
let cancelled = Cancelled "duplicate"
```

类型上下文不足或不同联合复用同名案例时，可以写限定名 `BookingStatus.Confirmed "C-42"`。大型领域也常用 `[<RequireQualifiedAccess>]` 要求限定访问。这里保持最小定义，重点理解案例及其数据。

在模式中，同一名称会识别案例并绑定其中的数据。`Confirmed code` 不是调用，而是说“若输入是 `Confirmed`，把内部字段绑定为 `code`”。构造和解构使用同一套名称，不必另行同步运行时标签。

## 穷尽匹配覆盖所有案例 {#exhaustive-match}

共享函数覆盖三个案例：

```fsharp:line-numbers [ch08-discriminated-unions.fsx]
let describeStatus status =
    match status with
    | Pending -> "pending"
    | Confirmed confirmationCode -> $"confirmed:{confirmationCode}"
    | Cancelled reason -> $"cancelled:{reason}"

let statuses = [ Pending; Confirmed "C-42"; Cancelled "duplicate" ]

let descriptions = statuses |> List.map describeStatus

printfn "Statuses: %A" descriptions
```
每个分支返回 `string`，因此整个 `match` 也是 `string` 表达式。编译器知道 `BookingStatus` 的封闭案例集合，能检查模式是否覆盖全部案例。

若以后增加 `Waitlisted of position: int`，所有没有覆盖它的显式匹配会产生诊断。这把模型演进转化为一份由编译器定位的待办清单，而不是等待罕见运行路径发现遗漏。

### 非穷尽版本只用于读诊断 {#non-exhaustive-diagnostic}

下面代码故意遗漏 `Cancelled`，不属于共享有效脚本：

```fsharp
let incomplete status =
    match status with
    | Pending -> "pending"
    | Confirmed code -> $"confirmed:{code}"
```

F# 编译器会报告 FS0025 非穷尽模式警告，并举出未覆盖值。启用“警告即错误”的项目会在构建时拒绝这种代码。

不要机械添加 `| _ -> "other"` 消除警告。若每个状态具有不同业务语义，通配符会让新增案例悄悄落入旧分支。只有剩余案例确实共享同一规则、并且你有意接受未来案例也走该规则时，通配符才合适。

## 匹配案例后才能读取它的专属数据 {#case-data}

要读取确认码，必须先证明状态是 `Confirmed`：

```fsharp:line-numbers [ch08-discriminated-unions.fsx]
let confirmationCode status =
    match status with
    | Confirmed code -> Some code
    | Pending
    | Cancelled _ -> None

printfn "Confirmed case carries code: %s" (confirmationCode (Confirmed "C-42") |> Option.defaultValue "none")
```
`confirmationCode` 返回 `string option`：确认状态得到 `Some code`，其他状态得到 `None`。这里先复用第 5 章为 `List.choose` 建立的最小 `option` 直觉；下一章会系统讨论缺失值组合。

代码不能从 `Pending` 读取不存在的确认码。成功匹配 `Confirmed code` 后，才能确定案例是 `Confirmed`，并安全使用 `code`。

多个案例可以共享一个分支：

```fsharp
| Pending
| Cancelled _ -> None
```

OR 模式的替代项必须绑定兼容的名称与类型；这里两者都不需要内部数据，所以合并安全。

## 状态转换是值到值的函数 {#transitions}

共享示例把确认写成纯函数：

```fsharp:line-numbers [ch08-discriminated-unions.fsx]
let confirm code status =
    match status with
    | Pending -> Confirmed code
    | Confirmed _
    | Cancelled _ -> status

let transitioned = Pending |> confirm "C-99"

printfn "Transition: pending -> %s" (describeStatus transitioned)
printfn "All descriptions: %d" (List.length descriptions)
```
`confirm` 在 `Pending` 上构造 `Confirmed code`；对已确认或已取消状态返回原值。函数不改写输入，输出仍由 `BookingStatus` 限制在合法案例中。

“无效转换保持原状”只是教学选择，不是通用预约规则。重复确认可以算幂等成功，也可以视为冲突；取消后再确认通常应返回具体失败。第 9 章用 `Result` 把这项决策写进返回类型。

联合保证输出是某个合法状态，却不自动保证所有转换规则正确。类型缩小了问题空间，函数仍要实现业务政策。

## 记录与联合各自承担什么 {#records-and-unions}

记录表达“同时包含”：预约同时有 ID、参加者和状态。联合表达“只能其一”：状态只能是待处理、已确认或已取消中的一种。

```fsharp
type Booking =
    { BookingId: string
      Attendee: string
      Status: BookingStatus }
```

不要把所有记录都改成联合，也不要把案例数据重新摊平成一个大记录。常见做法是用记录保存稳定字段，用联合字段表示不同状态；每个联合案例还可携带命名字段或小记录。

普通联合和记录一样，在组成数据支持时自动获得结构相等与比较。`Confirmed "C-42" = Confirmed "C-42"` 为真；不同案例不相等。是否应把这种相等当作业务实体相等，仍由需求决定。

## `.IsCase` 属性与模式 {#case-tests}

F# 9 起，联合值会生成 `.IsConfirmed` 等案例测试属性。只需要布尔判断时可以使用，但它不会取出确认码。需要案例数据、多个分支或穷尽检查时，`match` 更直接。

不要用一串 `.IsPending`、`.IsConfirmed` 把联合重新写成标志判断。联合把案例与数据绑定，也让编译器掌握完整案例集合。

## 设计检查表 {#design-checklist}

考虑可辨识联合时问：

1. 这些情况是否互斥并构成一个封闭集合；
2. 每种情况是否携带不同数据；
3. 案例名是否来自领域语言，而不是技术实现；
4. 新增案例时，哪些匹配应由编译器提醒；
5. 是否误把可同时成立的独立事实强塞进互斥案例。

案例过多且彼此只差几个独立开关，可能说明需要“记录 + 若干小联合/布尔字段”，而不是一个巨大联合列出笛卡尔积。类型应压缩非法组合，不应枚举所有偶然组合。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --exec ch08-discriminated-unions.fsx
```

应得到：

```text
Flag model contradiction: pending=true confirmed=true cancelled=false
Statuses: ["pending"; "confirmed:C-42"; "cancelled:duplicate"]
Confirmed case carries code: C-42
Transition: pending -> confirmed:C-99
All descriptions: 3
```

第一行展示布尔标志模型允许的矛盾值，后四行展示联合构造、穷尽匹配、案例专属数据和纯转换。有效脚本本身不存在非穷尽匹配；请按顺序比较五行。

## 检查案例携带的数据 {#debugging}

若新增案例后出现 FS0025，逐个匹配判断新案例的真实规则，而不是先加通配符。若多个分支重复，确认它们是否真的有同一语义，再用 OR 模式合并。

构造案例时报类型错误时，先看 `of` 后声明的数据。`Confirmed of string` 接收一个字符串；`Case of string * int` 接收所写的二元组。构造和模式都必须与声明一致。

代码频繁先测案例、随后又匹配取数据，通常可以合成一次 `match`。若状态转换返回原值却调用方无法判断是否发生变化，返回类型可能需要第 9 章的 `Result`。

## 练习 {#exercises}

### 练习 1：拆除标志组合 {#exercise-01}

一个通知请求含 `IsEmail`、`IsSms`、`IsDisabled` 三个标志，但规则要求恰好为邮件、短信或禁用之一。列出标志模型允许的组合数量与三个合法组合，再定义携带邮件地址、电话号码或禁用原因的联合。解释哪些非法状态从此无法构造。

### 练习 2：证明穷尽性 {#exercise-02}

为 `BookingStatus` 写一个返回短标签的穷尽函数。然后在纸上增加 `Waitlisted of position: int`，指出编译器应提醒哪个匹配。比较“新增显式分支”和“先前已有 `_` 分支”对维护者提供的信息。

### 练习 3：设计转换策略 {#exercise-03}

为 `cancel reason status` 写纯函数：`Pending` 和 `Confirmed _` 变为 `Cancelled reason`，已取消状态保持原值。列出只返回 `BookingStatus` 会丢失什么信息，再提出一种能区分成功与禁止转换的返回类型；暂时无需组合 `Result`。

[查看本章练习答案](../solutions/ch-08-discriminated-unions)。

## 核心结论 {#summary}

- 多个布尔标志会产生组合空间，并可能让矛盾状态通过类型检查。
- 可辨识联合规定一个值只能属于若干命名案例之一，每个案例可携带专属数据。
- 案例名在表达式中构造值，在模式中识别案例并绑定数据。
- 穷尽匹配让新增案例形成编译器定位的修改清单；无意通配符会削弱这项反馈。
- 联合只允许合法状态，转换函数仍需实现正确业务政策。
- 记录表达“同时有这些字段”，联合表达“只能是这些案例之一”，两者通常组合使用。
- 独立事实仍可用布尔值；不要把联合变成另一种巨大组合枚举。

下一章会比较两种常见返回类型：`option` 表示可能缺失，`Result` 表示带具体原因的预期失败。

## 来源 {#sources}

- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：`match` 表达式与守卫](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
