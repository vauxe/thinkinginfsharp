---
title: "第 5 章：列表、管道与数据流"
description: "用 map、filter、choose 和管道表达列表变换，并与 for、while 和局部可变绑定比较。"
translationKey: part-01/ch-05-lists-pipelines
---

# 第 5 章：列表、管道与数据流 {#overview}

串联集合操作前，先写出每个阶段的输入和输出类型。F# 的 `List` 模块把常见阶段写成高阶函数：`filter` 选择元素，`map` 变换元素，`choose` 同时选择并变换。管道运算符 `|>` 再按数据流顺序排列这些阶段。

F# 同时支持不可变变换与 `for`、`while`、`let mutable` 这些命令式工具。我们会用三种方式实现同一个问题，比较哪种最能直接表达意图，并找出优化前真正需要测量的成本。

本章中的列表都很小且已经在内存中。数组、惰性 `seq`、`Map`、`Set` 和重复枚举留到第 14 章；递归与 `fold` 在下一章展开。

## 列表不可变，并保留旧版本 {#list-foundations}

示例的输入是一个二元组列表：

```text
(string * int) list
```

每项包含来宾名称和请求座位数，所有项具有同一元素类型。列表字面量用方括号包围；同一行的项用分号分隔，按缩进分行时也可以省略分号。

先把下面的完整起点保存为 `ch05-lists-pipelines.fsx`。本章后续代码块按出现顺序承接这些定义：

```fsharp:line-numbers
let requests =
    [ ("Lin", 3)
      ("Ada", 0)
      ("Sam", 2)
      ("Mina", -1) ]

let isValidRequest (_, seats) = seats > 0

let formatRequest (guest, seats) =
    $"{guest}:{seats}"
```

`requests` 是待处理数据；`isValidRequest` 判断座位数是否有效；`formatRequest` 把一个请求变成显示标签。先给这些角色命名，后面的管道才有可追踪的上下文。

F# 列表是不可变的单向链式结构。`item :: list` 在头部创建一个新节点，并让尾部共享原列表，通常是常数时间。`left @ right` 或 `List.append left right` 必须重建左侧链，成本与 `left` 长度成正比。因此，循环里反复向尾部追加会让总时间达到 O(n²)；常见做法是向头部累积，再在末尾调用一次 `List.rev`。

列表节点本身不可变，但元素未必不可变。如果两个列表包含同一个可变对象，它们都能观察到对象内部的变化。第 14 章会分别讨论集合行为和元素行为。

## 三个核心变换 {#core-transformations}

先只看函数签名：

```text
List.map    : ('a -> 'b)        -> 'a list -> 'b list
List.filter : ('a -> bool)      -> 'a list -> 'a list
List.choose : ('a -> 'b option) -> 'a list -> 'b list
```

三者先接收一个函数值，再接收列表，所以都适合部分应用和管道。它们保持输入顺序，但职责不同。

### `map`：每项恰好产生一项 {#map}

`List.map transform source` 对每个输入调用 `transform`，并按原顺序组成新列表。输出长度与输入相同，但元素类型可由 `'a` 变为 `'b`。

例如 `(string * int) -> string` 的 `formatRequest` 把每个预约元组变成标签；`List.map formatRequest` 因部分应用而成为 `(string * int) list -> string list`。筛选交给 `filter` 或 `choose`；`map` 为每项输入保留一项输出。

### `filter`：决定保留原项 {#filter}

`List.filter predicate source` 要求 `predicate` 返回 `bool`。结果只包含返回 `true` 的原元素，类型仍是 `'a list`，相对顺序不变。

`filter` 适合选择与后续变换是两个清楚的概念阶段。它返回新的结果列表，同时保留源列表；被保留的元素值本身可以由两个列表共同引用。内部链是否复用属于实现细节。

### `choose`：零项或一项输出 {#choose}

`List.choose chooser source` 让每个输入产生 `Some value` 或 `None`。`Some value` 把转换后的值放入结果，`None` 不产生结果项，因此一次表达选择与变换。

这里先把 `Some x` 读成“存在一个值”，把 `None` 读成“没有值”。第 9 章会完整讨论缺失建模、组合与 `Some null`；调用方需要错误原因时，后续章节会使用 `Result`。

当筛选与映射共享同一判断、且中间列表没有独立意义时，`choose` 能更直接地写出“一次输入最多产生一项输出”。若两个阶段各有领域名称或需要单独观察，保留 `filter` 与 `map` 反而更清楚。

## 管道把数据作为函数的最后一个参数 {#pipelines}

管道操作符的核心等价关系很小：

```text
value |> functionValue

等价于

functionValue value
```

例如：

```text
requests |> List.filter isValidRequest

等价于

List.filter isValidRequest requests
```

第 3 章的参数顺序把列表放在最后。谓词先被部分应用，管道再传入数据，因此读起来很自然。

### 按阶段阅读 {#pipeline-stages}

共享管道如下：

```fsharp:line-numbers
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
这段代码承接本章开头的三个定义，输出为：

```text
Pipeline labels: ["Lin:3"; "Sam:2"]
```

从上到下记录类型和值：

| 阶段 | 类型 | 值摘要 |
| --- | --- | --- |
| `requests` | `(string * int) list` | 4 个请求 |
| `List.filter isValidRequest` 后 | `(string * int) list` | Lin 与 Sam 两项 |
| `List.map formatRequest` 后 | `string list` | `"Lin:3"` 与 `"Sam:2"` |

管道把前一阶段的值作为后一个函数的最后一个实参，执行方式仍是普通函数调用。各阶段按数据流顺序排列，更容易从左到右阅读，也减少了多层嵌套的括号。

### 列表管道是立即求值 {#eager-pipelines}

这里每个 `List` 操作在调用时都会遍历输入并完成结果列表。`filter` 先产生一个中间列表，然后 `map` 再遍历它；管道符不会改变这两次立即求值。若要融合阶段或延迟求值，需要使用其他操作或数据结构。

中间结果能够提升可读性、数据规模又小时，两阶段管道通常很合适。先写清楚并测量真实瓶颈；当一次遍历确实重要且逻辑天然合一时，再用 `choose` 或第 6 章的 `fold` 表达。

### 管道也可能降低可读性 {#pipeline-boundaries}

`List.isEmpty values` 这样的直接调用往往更能清楚表达单项操作。连续把数据作为最后一个实参传递、且每个阶段都很短时，管道最合适。

把复杂阶段提取成有名称的函数，并让每个阶段的输出类型容易说清。数据流清楚时使用管道；普通应用更直接时就使用普通应用。

## 用 `choose` 合并相关阶段 {#choose-pipeline}

示例把有效性与格式化组合成一个选择函数：

```fsharp:line-numbers
let tryFormatRequest request =
    if isValidRequest request then
        Some(formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
```
这段代码继续使用开头的 `requests`、`isValidRequest` 和 `formatRequest`，输出为：

```text
Chosen labels: ["Lin:3"; "Sam:2"]
```

`tryFormatRequest` 的类型是 `(string * int) -> string option`。有效请求产生 `Some label`，无效请求产生 `None`；`List.choose` 提取 `Some` 内的文本并保持顺序，最终同样得到 `string list`。

命名前缀 `try` 在 F#/.NET 代码中常提示操作可能返回正常值之外的结果，而类型会准确规定这种结果。这里的 `option` 只区分存在与缺失；需要解释原因的验证会在后续章节使用 `Result` 或累积验证。

## 副作用使用 `iter` 或 `for` {#iteration-for-effects}

变换函数回答“新数据是什么”。若目标是依次执行输出等副作用而不收集结果，`List.iter action` 或 `for item in source do ...` 更贴切。它们的动作或循环主体返回 `unit`，整个迭代也返回 `unit`。

下面的代码承接前面的 `pipelineLabels`，用 `for` 展示标签顺序：

```fsharp:line-numbers
printf "Iteration order:"

for label in pipelineLabels do
    printf " %s" label

printfn ""
```
输出为：

```text
Iteration order: Lin:3 Sam:2
```

`for` 遍历可枚举输入，并可在循环变量位置使用模式。它适合日志、写入已有缓冲区或调用命令式 API。若真正需要一个新列表，循环必须另外管理累积状态，而 `map`/`filter` 已经把这个意图编码在返回类型中。

用 `map` 产生数据，用 `iter` 或 `for` 对每项执行副作用。后两者返回 `unit`，既明确表达意图，也不会产生一份无用的结果列表。

## 可变绑定需要明确声明 {#mutable-bindings}

`let mutable name = initial` 建立可变存储位置，`name <- next` 更新它。`=` 仍用于绑定或相等判断，不用于更新。

可变状态增加了时间顺序：要知道某一行的 `name` 是什么值，必须先确认此前哪些路径执行过 `<-`。把状态留在一个小函数内，并且不把其引用交给外部，就更容易追踪。示例中的两个命令式版本都遵守这条规则。

### `for` 版本：枚举由语言管理 {#for-version}

这个版本承接前面定义的 `tryFormatRequest`：

```fsharp:line-numbers
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
```
循环按输入顺序调用 `tryFormatRequest`。有效标签用 `::` 在常数时间内加到 `reversedLabels` 头部，所以暂时顺序相反；循环结束后调用一次 `List.rev` 恢复原顺序。

`for` 主体的两条 `match` 分支都是 `unit`：更新 `<-` 的结果是 `()`，`None` 分支显式返回 `()`。函数最后一个表达式 `List.rev reversedLabels` 才产生 `string list`。

### `while` 版本：条件与进度都由代码管理 {#while-version}

这个版本也承接 `tryFormatRequest`，但显式维护尚未处理的列表：

```fsharp:line-numbers
let labelsWithWhile source =
    let mutable remaining = source
    let mutable reversedLabels = []

    while not (List.isEmpty remaining) do
        match remaining with
        | request :: tail ->
            remaining <- tail

            match tryFormatRequest request with
            | Some label -> reversedLabels <- label :: reversedLabels
            | None -> ()
        | [] -> ()

    List.rev reversedLabels
```
`while` 在条件为 `true` 时反复执行返回 `unit` 的主体。代码必须手工维护 `remaining`，并在每次非空匹配后更新为 `tail`；漏掉这一步会产生无限循环。虽然循环条件检查了列表非空，但 `remaining` 的类型仍然是列表，所以内部匹配仍要处理空列表。

这个版本能工作，也只在局部修改两个绑定，但需要手工维护的状态比 `for` 或 `choose` 更多。`while` 更适合下一步是否继续确实取决于变化状态、且没有现成集合遍历函数的问题，例如与某些底层 API 对接。

## 三种实现怎样选择 {#choosing-style}

先运行下面的续接代码，用结构相等确认四个写法产生相同标签和顺序：

```fsharp:line-numbers
let forLabels = labelsWithFor requests
let whileLabels = labelsWithWhile requests

let sameLabels =
    pipelineLabels = chosenLabels
    && pipelineLabels = forLabels
    && pipelineLabels = whileLabels

printfn "All implementations agree: %b" sameLabels
```

输出为 `All implementations agree: true`。确认行为一致后，再根据所需结果和实测成本选择：

| 目标 | 通常先考虑 | 原因 |
| --- | --- | --- |
| 从集合产生新集合 | `map`、`filter`、`choose`、以后 `fold` | 返回类型直接表达变换 |
| 对每项执行副作用 | `List.iter` 或 `for` | `unit` 意图明确，不制造无用结果 |
| 是否继续取决于不断变化的状态 | 小范围 `while` + `mutable` | 状态机可能比勉强套用集合变换更直接 |
| 热路径需要减少遍历/分配 | 先测量，再合并阶段或使用合适集合 | 有清楚基线的测量比猜测可靠 |

两种风格都可能带来多余成本：函数式版本可能分配过多中间列表，命令式版本可能因错误的尾部追加退化。先保证结果、顺序和对外行为相同，再用基准或分析工具比较真实成本。

## 练习 {#exercises}

每题先写阶段类型与中间值，再运行。最终列表相同还不够；还要比较源数据、顺序与副作用。

### 练习 1：逐阶段追踪管道 {#exercise-01}

针对“按阶段阅读”一节的 `pipelineLabels`：

1. 写出 `requests`、过滤后列表和映射后列表的类型；
2. 写出两个中间列表的确切元素顺序；
3. 展开两个 `|>`，改写为不使用管道的等价调用；
4. 说明源列表是否变化，以及该管道遍历几次列表阶段。


::: details 参考答案

共享管道是：

```fsharp:line-numbers
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
`requests` 与过滤结果都是 `(string * int) list`；前者依次为 Lin 3、Ada 0、Sam 2、Mina -1，后者只保留 Lin 3、Sam 2。映射结果是 `string list`，顺序为 `[ "Lin:3"; "Sam:2" ]`。

不使用管道时，分两步求值：

1. 计算 `List.filter isValidRequest requests`；
2. 把结果传给 `List.map formatRequest`。

也可嵌套写成 `List.map formatRequest (List.filter isValidRequest requests)`。两种写法都不会改变源列表。

这段执行两个立即求值列表阶段：过滤遍历四项并产生中间列表，映射再遍历两项并产生最终列表。调用次数和元素访问次数不是同一个数字，但确实存在两次列表操作。

:::

### 练习 2：用 `choose` 合并选择与变换 {#exercise-02}

解释 `tryFormatRequest` 对四个请求分别返回什么，并写出完整类型。然后说明 `List.choose` 如何得到与 `filter` 加 `map` 相同的结果。

比较两种写法：什么情况下保留独立过滤结果更清楚？什么情况下 `choose` 更准确？`None` 在这个示例中丢失了什么信息？


::: details 参考答案

使用本章开头的共享定义，核心代码是：

```fsharp:line-numbers
let tryFormatRequest request =
    if isValidRequest request then
        Some(formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
```
`tryFormatRequest` 的完整类型为 `(string * int) -> string option`。它依次产生 `Some "Lin:3"`、`None`、`Some "Sam:2"`、`None`。`List.choose` 只提取两个 `Some` 的内部值，保持顺序，得到与过滤后映射相同的 `string list`。

若“有效请求”列表需要单独记录、测试或交给其他步骤，`filter` 与 `map` 的分段更清楚。若只有有效项才有可构造的输出，且中间列表没有领域意义，`choose` 更准确。这里 `None` 丢掉了请求为何无效以及原请求内容；需要原因时应使用携带错误的模型。

:::

### 练习 3：比较循环状态 {#exercise-03}

对 `labelsWithFor` 和 `labelsWithWhile`：

1. 在处理 Lin、Ada、Sam、Mina 后分别写出 `reversedLabels`；
2. 解释为什么末尾需要 `List.rev`；
3. 指出 `while` 每轮必须推进的状态，以及遗漏会怎样；
4. 为“打印每个标签”和“产生新标签列表”分别选择首选形式并说明原因。


::: details 参考答案

`for` 与 `while` 版本分别是：

```fsharp:line-numbers
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
```
```fsharp:line-numbers
let labelsWithWhile source =
    let mutable remaining = source
    let mutable reversedLabels = []

    while not (List.isEmpty remaining) do
        match remaining with
        | request :: tail ->
            remaining <- tail

            match tryFormatRequest request with
            | Some label -> reversedLabels <- label :: reversedLabels
            | None -> ()
        | [] -> ()

    List.rev reversedLabels
```
两个版本的 `reversedLabels` 变化相同：处理 Lin 后是 `[ "Lin:3" ]`；Ada 产生 `None`，不变；Sam 用 `::` 加到前端后是 `[ "Sam:2"; "Lin:3" ]`；Mina 产生 `None`，仍不变。`List.rev` 恢复输入相对顺序，否则结果会把有效项倒置。

`while` 还必须让 `remaining` 从完整列表依次变为各级 `tail`，最后为 `[]`。任何非空路径忘记更新都会让条件一直为真并重复处理同一项。

“打印每个标签”首选 `for` 或 `List.iter`，因为目标是产生输出副作用。“生成新标签列表”首选 `choose`，因为返回类型直接表达结果。只有性能分析确认这是热点后，才值得比较定制的单遍历可变循环。

:::


下一章会把“向头部累积再反转”推广为递归与累加器，再用 `fold` 把一类显式递归重写成可复用集合操作，并说明递归调用在什么条件下位于尾位置。

## 来源 {#sources}

- [Microsoft Learn：列表与 `map`/`filter`/`choose`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core：List 模块参考](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [Microsoft Learn：函数与管道](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：值与可变绑定](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn：`for...in`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [Microsoft Learn：`while...do`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-while-do-expression)
