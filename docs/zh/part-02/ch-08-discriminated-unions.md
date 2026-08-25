---
title: "第 8 章：可辨识联合与状态建模"
description: "从矛盾布尔标志推导可辨识联合，用案例专属数据与穷尽匹配表达互斥状态和转换。"
translationKey: part-02/ch-08-discriminated-unions
---

# 第 8 章：可辨识联合与状态建模 {#overview}

设预约状态由三个布尔字段表示：`IsPending`、`IsConfirmed`、`IsCancelled`。三个独立开关共有八种组合，但业务也许只允许三种：待处理、已确认或已取消。`true, true, false` 究竟表示什么？类型已经允许了一个调用方不该提出的问题。

可辨识联合把“若干字段可能怎样组合”改成“这个值只能是若干命名案例中的一个”。每个案例还能只携带该状态真正需要的数据：确认码只存在于已确认状态，取消原因只存在于已取消状态。模式匹配随后让处理逻辑与全部可能形状对齐。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 识别布尔标志组合产生的非法状态；
- 定义无数据和携带数据的联合案例；
- 把案例名同时读成构造函数和模式；
- 用穷尽 `match` 解构案例专属数据；
- 理解通配符为何可能掩盖新增案例；
- 把状态变化写成联合值之间的函数；
- 判断一个布尔值是独立事实，还是互斥状态的错误表示。

本章讨论封闭、同步、内存中的状态表示。第 9 章为失败转换增加 `Result`，第 12 章保护构造不变量，贯穿项目以后才处理持久化和并发状态。

## 独立标志制造笛卡尔积 {#flag-problem}

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
记录确保三个字段都存在且为 `bool`，却无法说明“恰好一个为真”。每增加一个开关，组合空间翻倍；验证函数必须在每次构造与更新后重新排除矛盾。

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

联合是和类型：合法值集合是各案例值集合之和。上一章的记录是积类型：一个值同时拥有所有字段。两者经常组合，例如记录保存预约的稳定字段，其中一个字段是 `BookingStatus`。

## 案例名既构造也解构 {#construction}

案例名应用于数据时构造联合值：

```fsharp
let pending = Pending
let confirmed = Confirmed "C-42"
let cancelled = Cancelled "duplicate"
```

类型上下文不足或不同联合复用同名案例时，可以写限定名 `BookingStatus.Confirmed "C-42"`。大型领域也常用 `[<RequireQualifiedAccess>]` 要求限定访问；本章保持最小定义，重点放在数据形状。

在模式中，同一案例名验证形状并绑定所携带数据。`Confirmed code` 不是调用，而是说“若输入是 `Confirmed`，把内部字段绑定为 `code`”。构造和解构共享一套词汇，减少标签与运行时标记之间的手工同步。

## 穷尽匹配把形状变成逻辑 {#exhaustive-match}

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
每个分支返回 `string`，因此整个 `match` 也是 `string` 表达式。编译器知道 `BookingStatus` 的封闭案例集合，能检查模式是否覆盖全部形状。

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

## 案例数据随证明一起出现 {#case-data}

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

关键不是多写一次 `match`，而是代码不能在 `Pending` 上直接读取不存在的 `ConfirmationCode`。案例标签是携带数据有效性的证据。

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

“无效转换保持原状”只是本章为了聚焦类型形状所选的策略，不一定适合真实预约系统。重复确认可能应幂等成功，也可能要报告冲突；取消后确认通常应返回有上下文的失败。第 9 章用 `Result` 把这种决策放进返回类型，而不是静默丢失信息。

联合保证输出是某个合法状态，却不自动保证所有转换规则正确。类型缩小了问题空间，函数仍要实现业务政策。

## 记录与联合各自承担什么 {#records-and-unions}

记录适合“同时拥有”：预约同时有 ID、参加者和状态。联合适合“只能其一”：状态是待处理、已确认或已取消。

```fsharp
type Booking =
    { BookingId: string
      Attendee: string
      Status: BookingStatus }
```

不要把所有记录都改成联合，也不要把案例数据重新摊平成一个大记录。一个常见领域模型是记录组成稳定字段，联合字段表达可变形状，联合案例内部再携带小记录或命名字段。

普通联合和记录一样，在组成数据支持时自动获得结构相等与比较。`Confirmed "C-42" = Confirmed "C-42"` 为真；不同案例不相等。是否应把这种相等当作业务实体相等，仍由需求决定。

## `.IsCase` 属性与模式 {#case-tests}

F# 9 起，联合值会生成如 `.IsConfirmed` 的案例测试属性。它适合只需要布尔测试的窄边界，但不会解构确认码。需要案例数据、多个分支或穷尽性时，`match` 仍更直接。

不要用一串 `.IsPending`、`.IsConfirmed` 再把联合写回标志风格。联合的价值在于案例与数据绑定，以及编译器理解完整形状集合。

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

第一行保留反例，后四行证明联合构造、穷尽解构、案例专属数据和纯转换。有效脚本自身不存在非穷尽匹配；请按顺序比较五行。

## 调试：检查形状，而不只是分支 {#debugging}

若新增案例后出现 FS0025，逐个匹配判断新案例的真实规则，而不是先加通配符。若多个分支重复，确认它们是否真的有同一语义，再用 OR 模式合并。

构造案例时报类型错误时，先看 `of` 后的数据形状。`Confirmed of string` 接收一个字符串；`Case of string * int` 的案例字段形成一个案例负载形状，构造与模式都必须一致。

代码频繁先测案例、随后又匹配取数据，通常可以合成一次 `match`。若状态转换返回原值却调用方无法判断是否发生变化，返回类型可能需要第 9 章的 `Result`。

## 练习 {#exercises}

### 练习 1：拆除标志组合 {#exercise-01}

一个通知请求含 `IsEmail`、`IsSms`、`IsDisabled` 三个标志，但规则要求恰好为邮件、短信或禁用之一。列出标志模型允许的组合数量与三个合法组合，再定义携带邮件地址、电话号码或禁用原因的联合。解释哪些非法状态从此无法构造。

### 练习 2：证明穷尽性 {#exercise-02}

为 `BookingStatus` 写一个返回短标签的穷尽函数。然后在纸上增加 `Waitlisted of position: int`，指出编译器应提醒哪个匹配。比较“新增显式分支”和“先前已有 `_` 分支”对维护者提供的信息。

### 练习 3：设计转换策略 {#exercise-03}

为 `cancel reason status` 写纯函数：`Pending` 和 `Confirmed _` 变为 `Cancelled reason`，已取消状态保持原值。列出这种返回 `BookingStatus` 的接口丢失了什么信息，并预告一种能区分成功与不允许转换的返回形状；暂时不需要实现 `Result` 组合。

[查看本章练习答案](../solutions/ch-08-discriminated-unions)。

## 小结 {#summary}

- 多个布尔标志会产生组合空间，并可能让矛盾状态通过类型检查。
- 可辨识联合规定一个值只能属于若干命名案例之一，每个案例可携带专属数据。
- 案例名在表达式中构造值，在模式中验证形状并绑定数据。
- 穷尽匹配让新增案例形成编译器定位的修改清单；无意通配符会削弱这项反馈。
- 联合保证状态形状合法，转换函数仍需实现正确业务政策。
- 记录表达“同时拥有这些字段”，联合表达“只能是这些形状之一”，两者通常组合使用。
- 独立事实仍可用布尔值；不要把联合变成另一种巨大组合枚举。

下一章会区分两种常见返回形状：`option` 表达可能缺失，`Result` 表达有上下文的预期失败。

## 词汇 {#vocabulary}

- **可辨识联合（discriminated union）：** 规定值只能是有限命名案例之一的类型，各案例可携带不同数据。
- **联合案例（union case）：** 联合中的一种命名形状，同时充当构造器和模式标签。
- **穷尽性（exhaustiveness）：** 一组模式覆盖输入类型全部可能形状的性质。
- **案例专属数据（case-specific data）：** 只有某个案例成立时才存在并可解构的数据。
- **状态转换（state transition）：** 把当前状态值映射为新状态或失败信息的函数。

## 来源 {#sources}

- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：`match` 表达式与守卫](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
