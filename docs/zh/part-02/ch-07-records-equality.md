---
title: "第 7 章：记录、更新、相等与比较"
description: "从位置元组过渡到命名记录，掌握匿名记录、不可变更新，并严格区分结构相等、比较、引用身份与哈希码。"
translationKey: part-02/ch-07-records-equality
---

# 第 7 章：记录、更新、相等与比较 {#overview}

元组把几个值组合起来，但每一项的含义只能由位置和周围代码说明。`("A-1", "Lin", 2)` 对编译器只是 `string * string * int`；它不知道三个位置分别是活动、参加者和座位数。数据经过多个函数后，调用方必须共同记住这些位置约定，很容易传错。

记录把这套位置约定变成类型：字段有名称，构造必须完整，多个函数可以复用同一定义。F# 记录默认不可变；字段类型支持时，还会自动获得结构相等、结构比较和配套哈希。不过，内容相同、引用同一对象、哈希码相同是三件事。下面用代码逐项区分。

## 三种形式都能把多个值组合在一起 {#product-types}

元组、记录和匿名记录都同时包含多个组成部分，因此都属于**乘积类型**。三者的差别在于名称、复用范围与类型身份，而不在于谁“更函数式”。

### 元组适合局部且清楚的位置约定 {#tuples}

元组类型把位置写进类型：

```fsharp
let request = "Lin", 2
let attendee, seats = request
```

这里类型为 `string * int`，第二行的模式按位置解构。它适合函数内部的临时成对结果、数学坐标或一眼能看完的局部转换。交换位置会得到不同类型；两个含义不同、但组成类型相同的二元组却仍是同一个静态类型。

当调用处开始出现 `fst`、`snd`，或读者必须记住第四个位置是什么时，元组已不能清楚表达领域。不要靠注释维护一个跨模块的位置协议。

### 命名记录建立可复用类型 {#records}

共享脚本定义了一个预约草稿：

```fsharp:line-numbers [ch07-records-equality.fsx]
type BookingDraft =
    { EventId: string
      Attendee: string
      Seats: int }

let original =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }
```
`BookingDraft` 是一个命名类型。字段标签参与构造与访问，字段顺序不再是调用者理解含义的唯一线索。普通记录默认是 .NET 引用类型，但其字段默认不可改写；“引用类型”和“可变对象”不是同义词。

两个分别声明的记录即使字段名称和类型完全相同，也仍是不同的名义类型。类型名会形成编译期区分：`BookingDraft` 不会仅因字段相似就自动成为另一种记录。

### 构造时消除记录类型歧义 {#construction}

记录表达式必须为所有字段提供值。字段标签足够独特时，编译器能推断记录类型；多个类型复用同一组标签时，则应写类型标注或限定首个标签：

```fsharp
let draft: BookingDraft =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let another =
    { BookingDraft.EventId = "A-2"
      Attendee = "Ada"
      Seats = 1 }
```

标注的目的不是重复每个字段类型，而是消除类型归属歧义。依赖“最近声明的同名字段类型”会让无关声明顺序改变推断结果，应避免。

记录模式可以按名称解构：

```fsharp
let label { EventId = eventId; Attendee = attendee; Seats = seats } =
    $"{eventId}:{attendee}:{seats}"
```

构造与模式使用相同字段语言，一个建立值，一个拆出值。

## 复制更新表达不可变变化 {#update}

共享示例把座位数从 `2` 改为 `3`：

```fsharp:line-numbers [ch07-records-equality.fsx]
let updated = { original with Seats = 3 }

printfn "Record update: original=%d updated=%d" original.Seats updated.Seats
```
`{ original with Seats = 3 }` 产生新的 `BookingDraft`。`original.Seats` 仍为 `2`，`updated.Seats` 为 `3`。它表达“新状态源自旧状态，仅这些字段改变”，无需重复其他字段。

复制更新执行浅层结构更新：它创建新的外层记录，并沿用所有未更新字段的值。因此，引用类型字段可以由新旧记录共同指向同一对象。不可变领域模型通常也使用不可变的嵌套值，让这种共享天然安全。

F# 7 起支持用字段路径更新嵌套记录，但简洁语法不会改变浅层更新的语义。先把嵌套模型说明白，再决定是否压缩多个 `with`。

## 匿名记录无需声明类型名 {#anonymous-records}

匿名记录用 `{| ... |}` 构造，不先声明类型名。共享脚本从命名记录投影并增加计算字段：

```fsharp:line-numbers [ch07-records-equality.fsx]
let summary =
    {| updated with
        IsGroup = updated.Seats > 1 |}

printfn "Anonymous summary: %s -> %d seats, group=%b" summary.Attendee summary.Seats summary.IsGroup
```
`summary` 的类型由全部字段名、字段类型以及是否使用 `struct` 共同决定。只有这些信息全部相同，两个匿名记录才是同一类型；这里不存在“至少包含这些字段”的结构子类型。

匿名记录支持字段访问、结构相等/比较与复制更新，还能在更新时增加字段。它目前不支持记录模式匹配，通常用点访问读取字段。

它适合函数内部投影、查询结果或短距离适配。数据若有领域名称、出现在公共 API、需要集中维护不变量或会被广泛复用，命名记录通常更清楚。不要用匿名记录回避真正需要的领域类型名。

## 结构相等比较内容 {#equality}

两个 `BookingDraft` 即使分别构造，只要对应字段结构相等，F# 的 `=` 就返回 `true`：

```fsharp:line-numbers [ch07-records-equality.fsx]
let equalCopy =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let alias = original
let structurallyEqual = original = equalCopy
let physicallyEqual = LanguagePrimitives.PhysicalEquality original equalCopy
let aliasIsSameReference = LanguagePrimitives.PhysicalEquality original alias
let equalHashesAgree = hash original = hash equalCopy

printfn "Equality: structural=%b physical=%b alias=%b" structurallyEqual physicallyEqual aliasIsSameReference
printfn "Hashes agree for equal records: %b" equalHashesAgree
```
记录自动生成的相等会递归使用字段类型的相等语义。这个能力是组合性的：若组成类型不支持 F# 相等约束，外层记录也不能无条件获得正常的结构相等。第 11 章会把这条规则写成泛型约束。

结构相等回答“内容按该类型的规则是否相等”。它不回答两个变量是否指向同一内存对象，也不能判断两个值是否代表同一个业务实体。例如，两个预约草稿的内容可以相等，但真实预约仍需要各自的请求标识。

## 引用身份检查同一对象 {#identity}

`LanguagePrimitives.PhysicalEquality` 对引用类型检查物理/引用相等。示例中：

- `original = equalCopy` 为 `true`，因为字段内容相同；
- `PhysicalEquality original equalCopy` 为 `false`，因为它们分别构造；
- `PhysicalEquality original alias` 为 `true`，因为 `alias` 指向原对象。

普通记录虽然默认是引用类型，日常领域判断仍通常使用结构相等。只有缓存、对象图、互操作或明确依赖共享实例的低层逻辑才常关心身份。不要用物理相等替代业务标识，也不要从结构相等推断对象共享。

`PhysicalEquality` 要求引用类型；对 `struct` 记录或其他值类型讨论引用身份没有同样意义。

## 哈希码先缩小查找范围 {#hash}

记录的结构相等实现配套生成结构哈希。示例只断言一个必要方向：结构相等的两个记录得到相同 `hash` 结果。

哈希只提供单向保证：相等值具有相同哈希码，不同值却可能发生哈希碰撞。哈希集合先用哈希码缩小查找范围，再用相等比较确认结果。普通记录会配套生成两种操作并维持这项保证。

哈希码只适合在当前运行时中缩小候选范围。持久身份应使用数据库键或请求 ID；持久化与传输值应采用有稳定保证的格式；安全摘要则应使用专门的密码学算法。

## 结构比较提供默认顺序 {#comparison}

当所有字段支持比较时，记录也自动支持结构比较，因此可以直接 `List.sort`：

```fsharp:line-numbers [ch07-records-equality.fsx]
let drafts =
    [ { EventId = "B-2"
        Attendee = "Lin"
        Seats = 2 }
      { EventId = "A-1"
        Attendee = "Lin"
        Seats = 1 }
      { EventId = "A-1"
        Attendee = "Ada"
        Seats = 2 } ]

let sortedLabels =
    drafts
    |> List.sort
    |> List.map (fun draft -> $"{draft.EventId}:{draft.Attendee}:{draft.Seats}")

printfn "Structural sort: %A" sortedLabels
```
本例依记录声明的字段次序比较：先 `EventId`，相同时比较 `Attendee`，再比较 `Seats`。所以 `A-1:Ada:2` 位于 `A-1:Lin:1` 之前。

默认顺序适合需要确定性、且结构顺序确实符合意图的值。业务顺序往往不同，例如按座位数降序再按参加者排序。此时应显式写 `List.sortBy` 或 `List.sortWith`，让规则出现在代码中。新增或重排记录字段不应悄悄改变一项重要业务规则。

相等约束与比较约束也不是一回事：有的类型能判断相等却没有有意义的全序。第 11 章会准确写出两类约束，第 14 章再说明有序 `Map`/`Set` 与哈希集合分别依赖什么。

## 选择数据表示 {#choosing-shape}

| 情况 | 通常先考虑 | 原因 |
| --- | --- | --- |
| 一个函数内部的短暂成对结果 | 元组 | 位置含义局部且明显，解构简洁 |
| 反复传递的领域数据 | 命名记录 | 字段有名称，类型有身份，可集中演进 |
| 局部投影或短距离适配 | 匿名记录 | 无需声明额外名称，仍保留字段标签 |
| 多种互斥状态 | 下一章的可辨识联合 | 一个固定字段集合无法表达“只能是其中一种” |

不要只追求少写几行。记录名与字段名是模型词汇；匿名记录与元组则能避免为局部中间数据声明无用的公共类型。

## 运行共享示例 {#run-example}

在仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch07-records-equality.fsx
```

应得到：

```text
Record update: original=2 updated=3
Anonymous summary: Lin -> 3 seats, group=true
Equality: structural=true physical=false alias=true
Hashes agree for equal records: true
Structural sort: ["A-1:Ada:2"; "A-1:Lin:1"; "B-2:Lin:2"]
```

五行依次展示不可变更新、匿名记录投影、内容相等与引用身份的区别、相等值的哈希保证，以及结构排序。请按顺序比较它们。脚本不输出具体哈希整数，因为该数值不保证在不同运行环境中保持不变。

## 先说清你在比较什么 {#debugging}

遇到“相同数据却行为不同”时，先写出问题属于哪一层：

1. 类型是否相同，还是两个字段相似的不同命名记录；
2. 比较的是字段内容、引用身份还是领域 ID；
3. 组成字段是否都支持所需的相等或比较；
4. 排序是否无意采用记录的默认字段顺序；
5. 哈希码是否被错误当成相等证明或永久键。

更新后旧值也变化，通常说明记录字段引用了某个可变对象，而不是复制更新改写了旧记录。画出新旧记录共同引用的嵌套对象，比笼统地说“不可变失效”更准确。

记录构造推断成错误类型时，检查是否有多个记录复用标签；在绑定或函数参数上加一个有信息量的类型标注，不要靠声明顺序碰运气。

## 练习 {#exercises}

### 练习 1：从元组迁移到记录 {#exercise-01}

把 `("A-1", "Lin", 2)` 及一个接收 `string * string * int` 的格式化函数改为 `BookingDraft`。写出类型定义、构造、字段访问和记录模式版本。说明迁移消除了哪些位置错误，又没有自动保证哪些领域规则。

### 练习 2：追踪复制与身份 {#exercise-02}

从 `original` 创建一个仅改变 `Seats` 的记录，再创建一个字段完全相同但单独构造的记录。预测并验证三组结构相等和 `PhysicalEquality` 结果。若记录含一个可变列表或数组字段，解释复制更新后可能共享什么；无需在本题引入可变字段。

### 练习 3：设计相等、哈希与顺序 {#exercise-03}

为三种需求分别选择结构相等、引用身份、领域 ID 或显式排序键：去除内容相同的草稿、确认两个变量是否为同一缓存对象、按座位数降序展示预约。解释为什么不能以 `hash x = hash y` 判断前两者相等，并写出第三项的 `List.sortByDescending` 键。

[查看本章练习答案](../solutions/ch-07-records-equality)。

下一章处理记录无法单独表达的要求：预约只能处于少数几种互斥状态之一，不能是若干布尔标志的任意组合。

## 来源 {#sources}

- [Microsoft Learn：F# 记录](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn：记录复制与更新表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/copy-and-update-record-expressions)
- [Microsoft Learn：匿名记录](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/anonymous-records)
- [FSharp.Core：`LanguagePrimitives`、结构哈希与物理相等](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
- [Microsoft Learn：`Object.GetHashCode`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gethashcode?view=net-10.0)
