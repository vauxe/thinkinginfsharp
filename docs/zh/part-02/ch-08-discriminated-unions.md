---
title: "第 8 章：可区分联合与状态建模"
description: "从布尔标志可能产生的矛盾状态入手，学习用可区分联合、分支专属数据和穷尽匹配来表示互斥状态及其转换。"
translationKey: part-02/ch-08-discriminated-unions
---

# 第 8 章：可区分联合与状态建模 {#overview}

假设我们用三个布尔字段表示预约状态：`IsPending`、`IsConfirmed` 和 `IsCancelled`。三个字段一共可以组成八种真假组合，但业务只允许三种状态：待处理、已确认或已取消。

那么，`true, true, false` 表示什么？一个预约能同时处于“待处理”和“已确认”状态吗？

问题在于，这三个字段允许我们构造业务上不该存在的值。于是，每一处使用预约状态的代码都要额外检查这些矛盾组合。

可区分联合提供了更直接的表示方法：一个值在同一时刻只能属于若干命名分支中的一个。每个分支还可以携带该状态需要的数据。例如，确认码只属于 `Confirmed`，取消原因只属于 `Cancelled`。

处理状态时，模式匹配还能帮助我们检查是否遗漏了某个分支。

本章的范围很具体：状态种类固定，状态转换同步完成，数据只保存在内存中。第 9 章会用 `Result` 表示转换失败，第 12 章会进一步保证构造出的值满足业务规则。持久化和并发问题留到后面的综合项目再讨论。

## 多个布尔标志会产生矛盾状态 {#flag-problem}

先看一种能够正常运行、但数据模型有漏洞的写法：

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

这段代码不依赖本章的其他定义。把它复制到 `.fsx` 文件中即可单独运行，输出如下：

```text
Flag model contradiction: pending=true confirmed=true cancelled=false
```

代码虽然能够成功运行，输出却暴露了模型的问题：这个预约同时是“待处理”和“已确认”。

记录类型只能保证三个字段都存在，而且都是 `bool`；它无法保证“恰好一个字段为 `true`”。每增加一个布尔字段，可能的组合数量就会翻倍。

如果继续使用这种模型，每次创建或更新预约后，都必须重新检查并排除矛盾组合。

问题还不止于状态本身。为了保存确认码和取消原因，我们可能还会给记录添加 `ConfirmationCode`、`CancellationReason` 等可空或可选字段。

这样一来，未确认的预约也会拥有“确认码字段”，已确认的预约也会拥有“取消原因字段”。哪些字段在哪种状态下有效，只能依靠约定，类型本身并没有表达这种关系。

布尔值本身没有问题。像 `HasDietaryRequirements` 和 `NeedsWheelchairAccess` 这样的事实彼此独立，完全可以同时为 `true`。

只有当多个布尔字段实际在模拟“几种状态只能选一种”时，才应该考虑使用联合类型。

## 用联合类型列出所有合法状态 {#union-definition}

预约状态的真实规则是“待处理、已确认、已取消三选一”。我们可以直接把这三种合法状态写进一个类型：

```fsharp:line-numbers
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string
```

如果想边读边运行，可以把这段定义保存到 `ch08-discriminated-unions.fsx`，再按本章顺序追加后续代码。

唯一的例外是稍后的“非穷尽匹配”反例。它故意遗漏一个分支，只用于观察编译器警告。

`BookingStatus` 是一个可区分联合。`Pending`、`Confirmed` 和 `Cancelled` 是它的三个**联合分支**，英文称为 *union case*，也常译作“联合案例”。下文统一简称“分支”。

一个 `BookingStatus` 值只能由其中一个分支创建，所以无法表示“同时待处理又已确认”。三个分支携带的数据也不同：

- `Pending` 不需要额外数据；
- `Confirmed` 必须带有一个字符串形式的确认码；
- `Cancelled` 必须带有一个字符串形式的取消原因。

因此，确认码不再是所有状态共享的可选字段，而是 `Confirmed` 状态的一部分。取消原因与 `Cancelled` 的关系也是如此。

可以先用一句话区分记录与联合：记录表示“这些字段同时存在”，联合表示“这些分支只能选择一个”。

在类型理论中，记录属于积类型，联合属于和类型。这里的“积”和“和”描述的是合法值如何组合，不需要把它理解成普通的算术运算。

## 分支名既能创建值，也能拆开值 {#construction}

创建联合值时，直接写出分支名。需要携带数据的分支，还要在分支名后提供相应数据：

```fsharp
let pending = Pending
let confirmed = Confirmed "C-42"
let cancelled = Cancelled "duplicate"
```

F# 通常能根据上下文推断出这些分支属于 `BookingStatus`。如果上下文不够明确，或者多个联合使用了同名分支，可以写出完整名称：`BookingStatus.Confirmed "C-42"`。

大型项目有时还会用 `[<RequireQualifiedAccess>]` 强制使用完整名称。本章暂不展开这个属性，先关注分支及其数据。

在 `match` 模式中，同一个分支名可以用来拆开联合值。`Confirmed code` 的意思不是“调用 `Confirmed`”，而是：如果输入值属于 `Confirmed` 分支，就把其中的确认码命名为 `code`。

创建和拆开联合值使用同一组分支定义，不需要再维护额外的状态标签或布尔字段。

## 用穷尽匹配处理所有分支 {#exhaustive-match}

下面的 `describeStatus` 分别处理三个分支：

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

这段代码需要前面定义的 `BookingStatus`。按顺序放入同一个脚本后，输出如下：

```text
Statuses: ["pending"; "confirmed:C-42"; "cancelled:duplicate"]
```

三个分支都返回 `string`，所以整个 `match` 表达式的结果也是 `string`。

`BookingStatus` 的全部分支已经在类型定义中列出，编译器因此可以检查这个 `match` 是否处理了每一种情况。这种覆盖所有分支的写法叫作穷尽匹配。

假设以后给 `BookingStatus` 增加一个分支：`Waitlisted of position: int`。凡是明确列出旧分支、却没有处理 `Waitlisted` 的 `match`，编译器都会给出警告。

这样，修改类型后需要同步更新哪些代码，可以直接从编译器提示中找到，不必等到程序运行在某条少见路径上才发现遗漏。

### 故意漏掉分支，观察编译器提示 {#non-exhaustive-diagnostic}

下面是一个反例。它故意遗漏 `Cancelled`，只用于观察编译器如何提示问题，不是推荐的最终写法：

```fsharp
let incomplete status =
    match status with
    | Pending -> "pending"
    | Confirmed code -> $"confirmed:{code}"
```

F# 编译器会报告 FS0025，指出这个模式匹配没有覆盖所有情况，并给出一个未覆盖值的示例。如果项目把警告视为错误，这段代码将无法通过构建。

不要为了消除警告而机械地添加 `| _ -> "other"`。如果每种状态都有不同含义，通配符会让以后新增的分支悄悄进入旧的默认逻辑，编译器也就无法提醒你。

只有在剩余分支确实遵循同一规则，而且你有意让未来新增的分支也遵循该规则时，才适合使用通配符。

## 匹配到分支后，才能读取其中的数据 {#case-data}

确认码只存在于 `Confirmed` 分支中。因此，要读取确认码，必须先用模式匹配确认当前状态确实是 `Confirmed`：

```fsharp:line-numbers
let confirmationCode status =
    match status with
    | Confirmed code -> Some code
    | Pending
    | Cancelled _ -> None

printfn "Confirmed case carries code: %s" (confirmationCode (Confirmed "C-42") |> Option.defaultValue "none")
```

这段代码仍然使用前面定义的 `BookingStatus`，输出 `Confirmed case carries code: C-42`。

`confirmationCode` 返回 `string option`。输入是 `Confirmed` 时，结果为 `Some code`；输入是其他状态时，结果为 `None`。

这里只需要沿用第 5 章使用 `List.choose` 时建立的基本认识：`Some` 表示有值，`None` 表示没有值。第 9 章会系统讲解 `option`。

这项限制正是联合类型带来的保护：`Pending` 中根本没有确认码，所以代码也无法从 `Pending` 中读取它。只有匹配到 `Confirmed code` 后，才能安全使用变量 `code`。

如果几个联合分支需要执行相同逻辑，可以让它们共用一个匹配结果：

```fsharp
| Pending
| Cancelled _ -> None
```

这种写法叫作 OR 模式。OR 模式中的各个选项必须绑定相同名称、相同类型的变量。

这里的 `Pending` 没有内部数据，`Cancelled _` 又用 `_` 忽略了取消原因，两者都没有绑定变量，因此可以安全地合并。

## 用纯函数表示状态转换 {#transitions}

`confirm` 接收一个确认码和当前状态，然后返回转换后的状态。这个过程可以写成一个纯函数：

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

这段代码还需要前面的 `describeStatus` 和 `descriptions`，输出如下：

```text
Transition: pending -> confirmed:C-99
All descriptions: 3
```

表达式 `Pending |> confirm "C-99"` 等价于 `confirm "C-99" Pending`：管道把 `Pending` 作为最后一个参数传给 `confirm`。

输入为 `Pending` 时，函数返回 `Confirmed "C-99"`。输入已经是 `Confirmed` 或 `Cancelled` 时，函数直接返回原状态。

`confirm` 不会修改传入的状态，而是根据输入计算并返回一个新值。返回值仍然是 `BookingStatus`，所以只可能是类型列出的三个合法分支之一。

这里把“不能确认的状态”处理为“保持原状”，只是为了简化示例，并不是所有预约系统都应该采用的规则。重复确认可以被视为幂等成功，也可以被视为冲突；取消后再次确认，通常还需要说明失败原因。

第 9 章会使用 `Result` 把成功或失败明确写进返回类型。

可区分联合只能保证状态值的形状合法，不能替我们决定业务规则。类型负责排除不可能的状态，转换函数仍然要准确实现业务规则。

## 记录表示“同时拥有”，联合表示“只能选一” {#records-and-unions}

记录和联合解决的是两类不同的问题：

- 记录表示“同时拥有”：一个预约同时拥有 ID、参加者和状态；
- 联合表示“只能选一”：预约状态只能是待处理、已确认或已取消中的一种。

```fsharp
type Booking =
    { BookingId: string
      Attendee: string
      Status: BookingStatus }
```

实际建模时，两者经常像上面这样配合使用：记录保存每个预约都有的稳定数据，联合字段表示可能变化的状态。联合的每个分支还可以携带自己的命名字段或小记录。

不要把所有记录都改成联合，也不要为了使用记录而把分支中的数据重新摊平成一组可选字段。先判断数据之间是“同时存在”还是“只能选一”，再选择对应的类型。

与普通记录一样，只要内部数据支持相等和比较，普通联合也会自动获得结构相等和结构比较。例如，`Confirmed "C-42" = Confirmed "C-42"` 的结果为 `true`，而两个不同分支的值不会相等。

不过，两个状态值在结构上相等，并不一定表示它们属于同一个业务实体。如何判断两个业务实体是否相同，仍然取决于具体需求。

## 何时使用 `.IsConfirmed` 等判断属性 {#case-tests}

从 F# 9 开始，联合值会生成 `.IsConfirmed` 等判断属性。只想得到“是否属于某个分支”的布尔结果时，可以使用这些属性。

例如，`.IsConfirmed` 可以告诉你当前状态是否为 `Confirmed`，但不能取出其中的确认码。

如果需要读取分支中的数据、分别处理多个分支，或者让编译器检查是否覆盖全部分支，使用 `match` 会更直接。

不要用一串 `.IsPending`、`.IsConfirmed` 判断把联合重新写回布尔标志模型。那样会丢掉联合将状态与数据绑定在一起的优势。

## 练习 {#exercises}

### 练习 1：拆除标志组合 {#exercise-01}

某个通知请求使用 `IsEmail`、`IsSms` 和 `IsDisabled` 三个布尔字段表示通知方式，但业务规则要求三者只能选择一个。请按以下步骤完成练习：

1. 计算三个布尔字段一共能产生多少种组合。
2. 列出表示邮件、短信和禁用的三个合法组合。
3. 定义一个联合类型，让三个分支分别携带邮件地址、电话号码和禁用原因。
4. 说明改用联合后，哪些非法状态将无法构造。


::: details 参考答案

三个独立布尔值会产生 `2³ = 8` 种组合，其中只有三种合法：

- `(true, false, false)` 表示邮件；
- `(false, true, false)` 表示短信；
- `(false, false, true)` 表示禁用。

其余五种组合要么全为 `false`，要么有多个字段同时为 `true`，都需要额外检查并拒绝。

可以改用联合类型直接列出三种合法选择：

```fsharp
type NotificationTarget =
    | Email of address: string
    | Sms of phoneNumber: string
    | Disabled of reason: string
```

现在，每个 `NotificationTarget` 值只能是以下三者之一：带邮件地址的 `Email`、带电话号码的 `Sms`，或者带禁用原因的 `Disabled`。

“没有选择任何方式”和“同时选择多种方式”都无法通过这个类型构造出来。

联合只解决了“三选一”的问题。邮件地址、电话号码和原因字符串的格式是否有效，仍然需要验证函数或专门的构造函数来保证。

:::

### 练习 2：证明穷尽性 {#exercise-02}

请按以下步骤观察穷尽匹配带来的维护帮助：

1. 为 `BookingStatus` 编写一个函数，用短标签表示每种状态，并明确处理全部三个分支。
2. 假设类型中新增了 `Waitlisted of position: int`，判断哪个模式匹配会收到编译器警告。
3. 比较两种旧写法：一种明确列出三个分支，另一种用 `_` 处理剩余情况。思考新增 `Waitlisted` 后，哪一种能给维护者更明确的提醒。


::: details 参考答案

一种完整写法如下：

```fsharp
let shortLabel status =
    match status with
    | Pending -> "P"
    | Confirmed _ -> "C"
    | Cancelled _ -> "X"
```

增加 `Waitlisted of position: int` 后，`shortLabel` 没有处理新分支，因此编译器会报告 FS0025。维护者必须为等待队列状态选择一个标签，例如 `"W"`。

如果旧函数以 `_ -> "?"` 结尾，新增的 `Waitlisted` 会直接得到 `"?"`，编译器不会发出提醒。维护者也就难以判断这是真正想要的兼容行为，还是遗漏了新状态。

通配符并非始终错误。例如，函数只需要判断“是否为 `Pending`”，而所有其他状态都应该返回 `false`，那么 `| _ -> false` 就准确表达了规则。

关键在于：剩余分支，包括未来新增的分支，是否确实应该使用同一套处理逻辑。

:::

### 练习 3：设计转换策略 {#exercise-03}

请按以下步骤设计取消操作：

1. 编写纯函数 `cancel reason status`：输入为 `Pending` 或 `Confirmed _` 时，返回 `Cancelled reason`；输入已经是 `Cancelled` 时，保持原状态。
2. 说明函数只返回 `BookingStatus` 时，调用方无法从返回值中知道哪些转换信息。
3. 提出一种返回类型，用它区分“转换成功”和“禁止转换”。本题暂时不要求组合多个 `Result`。


::: details 参考答案

一种直接写法如下：

```fsharp
let cancel reason status =
    match status with
    | Pending
    | Confirmed _ -> Cancelled reason
    | Cancelled _ -> status
```

`BookingStatus` 只能描述函数执行后的状态，不能单独描述这次转换发生了什么。仅查看返回值时，调用方无法区分“本次调用刚刚取消成功”和“输入原本就已经取消”。

虽然 `Cancelled` 仍然带有一个原因，但返回类型没有说明这个原因来自本次请求还是原状态，也没有表示重复取消是否被接受。

如果某些转换可能被禁止，函数可以返回 `Result<BookingStatus, string>`：`Ok` 携带转换后的状态，`Error` 携带失败原因。

下一章会用专门的领域错误类型代替普通字符串，并介绍如何组合多个 `Result`。

:::


下一章会比较两种常见返回类型：`option` 表示“可能没有值”，`Result` 表示“操作可能因为某个已知原因失败”。

## 来源 {#sources}

- [Microsoft Learn：可区分联合](https://learn.microsoft.com/zh-cn/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：`match` 表达式与守卫](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
