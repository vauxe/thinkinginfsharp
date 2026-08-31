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

示例先保留一个故意薄弱的记录：

```fsharp:line-numbers
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

```fsharp:line-numbers
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

```fsharp:line-numbers
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

下面代码故意遗漏 `Cancelled`，不属于有效示例：

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

```fsharp:line-numbers
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

示例把确认写成纯函数：

```fsharp:line-numbers
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

## 练习 {#exercises}

### 练习 1：拆除标志组合 {#exercise-01}

一个通知请求含 `IsEmail`、`IsSms`、`IsDisabled` 三个标志，但规则要求恰好为邮件、短信或禁用之一。列出标志模型允许的组合数量与三个合法组合，再定义携带邮件地址、电话号码或禁用原因的联合。解释哪些非法状态从此无法构造。


::: details 参考答案

三个独立布尔值产生 `2³ = 8` 种组合，其中只有三种合法：

- `(true,false,false)` 表示邮件；
- `(false,true,false)` 表示短信；
- `(false,false,true)` 表示禁用。

其余五种组合都必须额外拒绝。

联合直接表达合法集合：

```fsharp
type NotificationTarget =
    | Email of address: string
    | Sms of phoneNumber: string
    | Disabled of reason: string
```

现在每个值都只选择一种目标：邮件地址、短信号码或禁用原因。智能构造函数或验证可以继续保证字符串格式，而不改变这三个 case。

:::

### 练习 2：证明穷尽性 {#exercise-02}

为 `BookingStatus` 写一个返回短标签的穷尽函数。然后在纸上增加 `Waitlisted of position: int`，指出编译器应提醒哪个匹配。比较“新增显式分支”和“先前已有 `_` 分支”对维护者提供的信息。


::: details 参考答案

完整匹配如下：

```fsharp
let shortLabel status =
    match status with
    | Pending -> "P"
    | Confirmed _ -> "C"
    | Cancelled _ -> "X"
```

增加 `Waitlisted of position: int` 后，这个匹配应产生 FS0025，维护者必须决定新标签，例如 `"W"`。若旧函数以 `_ -> "?"` 结尾，新增案例会静默得到 `"?"`；编译器无法区分这是有意兼容还是遗漏。

通配符并非始终错误。若函数只问“是否为 Pending”，且当前与未来的所有非 Pending case 确实都返回相同结果，`| _ -> false` 就能准确表达剩余集合。关键在于未来 case 是否真的共享同一规则。

:::

### 练习 3：设计转换策略 {#exercise-03}

为 `cancel reason status` 写纯函数：`Pending` 和 `Confirmed _` 变为 `Cancelled reason`，已取消状态保持原值。列出只返回 `BookingStatus` 会丢失什么信息，再提出一种能区分成功与禁止转换的返回类型；暂时无需组合 `Result`。


::: details 参考答案

最小函数为：

```fsharp
let cancel reason status =
    match status with
    | Pending
    | Confirmed _ -> Cancelled reason
    | Cancelled _ -> status
```

返回类型只有 `BookingStatus`，因此调用方无法区分“刚刚取消”与“先前已经取消”；也拿不到旧取消原因，无法判断重复请求是否一致。若某些转换不允许，接口可以返回 `Result<BookingStatus, string>`，成功携带新状态，失败携带原因。下一章会用领域化错误替代裸字符串，并组合这种结果。

:::


下一章会比较两种常见返回类型：`option` 表示可能缺失，`Result` 表示带具体原因的预期失败。

## 来源 {#sources}

- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：`match` 表达式与守卫](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
