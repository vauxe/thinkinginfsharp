---
title: "第 4 章：分支与基本模式"
description: "把 if 与 match 理解为产生值的表达式，并用字面量、变量、通配符、元组和列表模式建立安全分支。"
translationKey: part-01/ch-04-branching-patterns
---

# 第 4 章：分支与基本模式 {#overview}

命令式语言常把分支描述为“决定下一条执行哪条语句”。在 F# 中，`if` 与 `match` 都是表达式：选中的分支会成为整个表达式的结果，因此各分支必须返回兼容类型。

`if` 根据一个布尔条件在两个结果之间选择；`match` 则把一个值与按顺序排列的**模式**比较。模式既能识别输入结构，也能在成功分支中为组成部分建立名称。编译器还会检查分支类型和模式是否穷尽。

本章只用元组和列表熟悉模式。第 7、8 章会把记录、可辨识联合与穷尽性用于领域建模；列表变换则留到下一章。

## `if` 选择一个结果 {#if-expression}

示例先把剩余容量映射成文本：

```fsharp:line-numbers
let availability remaining =
    if remaining > 0 then "available" else "full"

printfn "Availability: %s" (availability 3)
```
求值顺序是：先计算 `remaining > 0`；结果为 `true` 时求 `then` 分支，否则求 `else` 分支。恰好一个分支为整个 `if` 提供值，所以 `availability` 返回 `string`。

### 条件必须真的是 `bool` {#boolean-only}

F# 要求 `if` 条件具有 `bool` 类型，例如 `remaining > 0`、`name = "Lin"` 或以后学到的布尔函数调用。整数、字符串、列表和对象都要通过明确返回 `bool` 的判断来表达具体问题。

这项规则把意图写进源码。人数为正、字符串为空和值缺失是不同问题，各自由专门的谓词表达。

### 两个分支必须统一类型 {#branch-types}

因为 `if` 是一个表达式，`then` 与 `else` 的结果必须能统一为同一类型。一个分支返回 `string`、另一个返回 `int` 时，调用方无法获得单一静态结果类型，编译器会拒绝代码。

同一规则也适用于有副作用的分支。调用 `printfn` 的分支返回 `unit`，因此另一分支也要返回 `unit`。需要产生数据时，让两个分支都返回数据，再由外层统一打印；这样决策逻辑也更容易测试。

### 省略 `else` 只适合 `unit` {#else-unit}

F# 允许结果为 `unit` 的表达式省略 `else`，此时 `then` 分支也要返回 `unit`。这适合“条件满足时记录一条消息”之类只有副作用的操作。需要产生数据时，两种情况都应明确返回值。

当业务需要一个结果时，显式写出每个分支。缺少合理的另一个结果，往往说明模型需要 `option` 或 `Result`，而不是说明应该利用隐式 `()`；这些类型会在第 9 章出现。

## `match` 检查一个值的结构 {#match-expression}

`match input with` 先求值 `input` 一次，再从上到下尝试规则。每条规则由模式、可选守卫和结果表达式构成：

```text
match input with
| pattern when condition -> result
| pattern -> result
```

找到第一个模式匹配且守卫为真的规则后，只求该规则右侧。整个 `match` 的值就是右侧结果；所有可达规则的结果类型必须统一。

### 规则顺序会改变结果 {#rule-order}

模式构成一组有顺序的决策规则。程序选择从上到下遇到的第一条成功规则，因此顺序会直接影响结果。较早的宽泛规则会截住较晚的具体规则。把通配符 `_` 放在第一条，它会匹配所有输入，编译器通常会把后续规则报告为不可达。

按“特殊情况先于一般情况”排序通常清楚，但守卫代表的业务优先级也可能决定顺序。例如，无效请求必须在“请求数不超过余量”之前判断，否则 `(0, 0)` 可能被误判为可接受。

### 模式既测试也绑定 {#patterns-bind}

几种基础模式承担不同角色：

| 模式 | 匹配什么 | 是否建立名称 |
| --- | --- | --- |
| `0`、`1`、`"Lin"` | 与字面量相同的值 | 否 |
| `value` | 任何与上下文类型兼容的值 | 是，名称为 `value` |
| `_` | 任何值 | 否 |
| `(guest, seats)` | 一个二元组，并分解两个位置 | 是 |
| `[]` | 空列表 | 否 |
| `[ only ]` | 恰好一个元素的列表 | 是 |
| `head :: tail` | 非空列表的首项与剩余列表 | 是 |

小写变量模式如 `value` 会匹配任何值并创建新绑定；守卫负责与运行时已有值比较。看到模式中的小写名称时，把它当成新变量，就能避开早期 `match` 中一类较隐蔽的错误。

## 守卫增加布尔约束 {#guards}

数值范围无法用单个字面量模式表示。可以先用变量模式取得值，再用 `when` 守卫检查：

```fsharp:line-numbers
let capacityBand remaining =
    match remaining with
    | value when value <= 0 -> "full"
    | 1 -> "last seat"
    | value when value <= 5 -> "limited"
    | _ -> "available"

printfn "Capacity bands: %s, %s, %s, %s" (capacityBand 0) (capacityBand 1) (capacityBand 4) (capacityBand 8)
```
对输入 `4`，第一条变量模式初步匹配，但 `4 <= 0` 为假，所以继续尝试。字面量 `1` 不匹配；第三条变量模式匹配且守卫为真，因此结果是 `"limited"`。

只有对应模式先匹配成功，程序才会计算守卫。守卫为真时选择当前规则，为假时继续尝试下一条。守卫最好是容易理解且没有副作用的布尔表达式，这样更容易判断每条规则何时生效。

守卫也解释了为什么 `| value when value = target -> ...` 能比较运行时参数 `target`。直接写 `| target -> ...` 会建立一个覆盖全部输入的新局部绑定，而不是读取外层 `target`。

## 用元组模式同时看多个位置 {#tuple-patterns}

第 3 章把元组作为一个组合实参。本章进一步看到，模式可以在函数参数或 `match` 中拆开它：

```fsharp:line-numbers
let bookingSummary (guest, seats) =
    let noun = if seats = 1 then "seat" else "seats"
    $"{guest} requested {seats} {noun}"

printfn "Booking: %s" (bookingSummary ("Lin", 3))
```
`(guest, seats)` 同时要求输入是二元组，并在函数主体中建立两个局部名称。元组模式按位置工作，项数和各位置类型都必须与输入相符。

模式负责分解结构并建立名称，`seats = 1` 这样的值判断仍是布尔表达式。这里用 `if` 选择单复数，因为问题只有一个直接的真假条件；把 `match` 留给真正受益于模式结构的决策。

在 `match remaining, requested with` 中，逗号先构造一个二元组作为匹配输入；规则中的 `(remaining, requested)` 拆开它。F# 常省略规则模式外层不必要的括号，因此 `| remaining, requested ->` 与二元组模式含义相同。

## 用列表模式区分结构 {#list-patterns}

F# 列表是由同类型元素组成的有序、不可变、单向链式集合。这里只学习识别列表结构所需的语法。`[]` 表示空列表，`[ a; b ]` 表示恰好两项，`head :: tail` 把非空列表拆成首项和剩余列表。

示例覆盖空、一项和至少两项：

```fsharp:line-numbers
let describeQueue queue =
    match queue with
    | [] -> "empty"
    | [ only ] -> $"one: {only}"
    | first :: second :: _ -> $"next: {first}, then {second}"

printfn "Queues: %s | %s | %s" (describeQueue []) (describeQueue [ "Lin" ]) (describeQueue [ "Lin"; "Ada"; "Sam" ])
```
`[ only ]` 只匹配长度为一的列表。`first :: second :: _` 按右结合读取为 `first :: (second :: _)`：先取第一项，再取第二项。最后的 `_` 接受余下列表但不建立名称，因此整个模式匹配任意至少两项的列表。

`[ first; second ]` 表示恰好两项；`first :: second :: _` 才表示长度至少为二的列表前两项。下一章会讲列表的构造与变换，第 6 章再用 `head :: tail` 建立结构递归。

## 穷尽性让遗漏可见 {#exhaustiveness}

一组规则具有**穷尽性**，意味着输入类型的每种可能结构都至少能匹配一条规则。否则，某个运行时输入可能触发匹配失败。编译器会对明显不完整的匹配发出警告；本书项目把警告当错误，要求先处理遗漏。

### 编译器能证明什么 {#compiler-checks}

编译器擅长分析有限的结构模式，例如列表为空或为 `head :: tail`。任意整数守卫位于这类结构证明之外，因此带守卫的范围通常还要用一条无守卫规则收尾。

守卫提供运行期条件，而非穷尽性证明。`| value when value > 0 -> ...` 覆盖正数路径；明确的兜底规则负责处理零和负数。

### 通配符既实用也会隐藏信息 {#wildcard-tradeoff}

对 `int` 或 `string` 这样的开放值域，最终 `_` 往往是合理兜底。它匹配任何剩余值但不建立名称；若结果需要原值，应使用具名变量模式。

通配符也可能过宽。未来处理具有有限具名用例的联合类型时，逐一列出用例能让新增状态触发编译器提醒；过早使用 `_` 会吞掉这种反馈。第 8 章会在真实领域状态中比较这两种选择。

## 选择 `if` 还是 `match` {#choosing-branching}

使用最能暴露决策依据的形式：

- 一个直接布尔条件在两个结果间选择时，`if` 通常最短；
- 需要按字面量、元组或列表结构分支并建立名称时，`match` 更自然；
- 多个相互排斥的结构规则适合 `match`；
- 一长串只比较数值范围的 `match` 可能仍可读，但要确保规则顺序清楚；
- 一个布尔选择已经表达清楚时，继续使用 `if`。

两者都产生值。选择时应看哪种写法最容易展示所有可能输入、规则优先级和遗漏情况。

## 练习 {#exercises}

先写出每个输入访问的第一条成功规则，再运行脚本。答案需要说明被跳过的规则为何失败。

### 练习 1：统一 `if` 的结果 {#exercise-01}

针对 `availability`：

1. 分别求 `availability 3` 与 `availability 0`；
2. 写出条件、两个分支和整个函数的类型；
3. 解释为什么不能让 `then` 返回 `"available"`、让 `else` 只调用 `printfn`；
4. 说明在什么情况下省略 `else` 才合法。


::: details 参考答案

共享定义是：

```fsharp:line-numbers
let availability remaining =
    if remaining > 0 then "available" else "full"

printfn "Availability: %s" (availability 3)
```
`availability 3` 的条件为 `true`，结果是 `"available"`；`availability 0` 的条件为 `false`，结果是 `"full"`。条件 `remaining > 0` 是 `bool`，两个分支都是 `string`，因此整个函数是 `int -> string`。

若 `then` 返回字符串而 `else` 只调用 `printfn`，两个结果分别为 `string` 与 `unit`，无法统一。输出副作用不会变成字符串结果。只有当整个条件表达式只执行副作用、`then` 也返回 `unit` 时，才可以省略 `else`；未命中路径也返回 `()`。

:::

### 练习 2：追踪规则与守卫 {#exercise-02}

先检查 `capacityBand -2`、`capacityBand 0` 和 `capacityBand 1`，再检查输入 `5` 与 `6`。为每个输入写出第一个成功规则和结果。

然后回答：若把 `_ -> "available"` 移到第一条，会发生什么？若只保留两个带守卫的变量规则，编译器为何仍不能把它们视为可靠穷尽？


::: details 参考答案

| 输入 | 首个成功规则 | 结果 |
| --- | --- | --- |
| `-2` | `value when value <= 0` | `"full"` |
| `0` | `value when value <= 0` | `"full"` |
| `1` | 字面量 `1` | `"last seat"` |
| `5` | `value when value <= 5` | `"limited"` |
| `6` | `_` | `"available"` |

输入 `1` 也初步匹配第一条变量模式，但守卫为假，所以继续到字面量规则。输入 `6` 会依次让两个守卫为假，也不匹配字面量，最终由通配符接住。

若通配符移到第一条，它会先匹配全部输入，其他规则不可达。只留下带守卫的变量规则也不构成编译器可证明的穷尽集合：守卫是一般布尔表达式，可能同时为假，还可能以后改变。无守卫兜底规则才明确覆盖余下输入。

:::

### 练习 3：分解组合输入 {#exercise-03}

编写一个同时查看余量与请求数的 `classifyRequest` 函数。请求数不大于零时返回 `"invalid"`；正数请求不超过余量时返回 `"accepted"`；其他情况返回 `"too large"`。然后：

1. 解释 `(5, 0)`、`(5, 3)` 与 `(2, 3)` 的结果；
2. 说明为什么“无效请求”规则必须先于“请求不超过余量”；
3. 写出函数完整类型；
4. 对队列 `[ "Lin"; "Ada" ]` 和四项队列，说明 `describeQueue` 命中哪个模式以及 `_` 代表什么。


::: details 参考答案

定义如下：

```fsharp:line-numbers
let classifyRequest (remaining, requested) =
    match remaining, requested with
    | _, requested when requested <= 0 -> "invalid"
    | remaining, requested when requested <= remaining -> "accepted"
    | _ -> "too large"

printfn "Requests: %s, %s, %s" (classifyRequest (5, 0)) (classifyRequest (5, 3)) (classifyRequest (2, 3))
```
`(5, 0)` 先命中请求数不大于零，结果 `"invalid"`；`(5, 3)` 跳过第一条，在第二条满足 `3 <= 5`，结果 `"accepted"`；`(2, 3)` 两个守卫都失败，由 `_` 得到 `"too large"`。函数类型是 `int * int -> string`。

顺序很重要：若接受规则在前，`(0, 0)` 会先满足 `0 <= 0`，无效请求被误收。规则顺序在这里直接表达业务优先级。

队列部分是：

```fsharp:line-numbers
let describeQueue queue =
    match queue with
    | [] -> "empty"
    | [ only ] -> $"one: {only}"
    | first :: second :: _ -> $"next: {first}, then {second}"

printfn "Queues: %s | %s | %s" (describeQueue []) (describeQueue [ "Lin" ]) (describeQueue [ "Lin"; "Ada"; "Sam" ])
```
两项和四项列表都命中 `first :: second :: _`。前两个名称分别绑定前两项，`_` 匹配剩余列表：两项时余下 `[]`，四项时余下两项。它不是第三个元素，也不创建可读取名称。

:::


下一章会从列表结构走向列表变换，用 `map`、`filter`、`choose` 和管道把分支函数组合成可读的数据流，并比较循环和可变状态的取舍。

## 来源 {#sources}

- [Microsoft Learn：`if...then...else` 条件表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/conditional-expressions-if-then-else)
- [Microsoft Learn：`match` 表达式与守卫](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Microsoft Learn：列表](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
