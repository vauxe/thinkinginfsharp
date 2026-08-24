---
title: "第 5 章：列表、管道与数据流"
description: "用 map、filter、choose 和管道表达列表变换，并与 for、while 和局部可变绑定比较。"
translationKey: part-01/ch-05-lists-pipelines
kind: chapter
part: 1
chapter: 5
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch05-lists-pipelines
exerciseIds:
  - ch05-exercise-01
  - ch05-exercise-02
  - ch05-exercise-03
termIds:
  - eager-evaluation
  - effect
  - higher-order-function
  - immutability
  - list
  - mutable-binding
  - option
  - pipeline
sources:
  - id: microsoft-lists
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists
    checked: "2026-08-24"
  - id: fsharp-core-list
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html
    checked: "2026-08-24"
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
  - id: microsoft-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/
    checked: "2026-08-24"
  - id: microsoft-for-in
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression
    checked: "2026-08-24"
  - id: microsoft-while
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-while-do-expression
    checked: "2026-08-24"
---

# 第 5 章：列表、管道与数据流 {#overview}

处理一组数据时，最值得先问的不是“循环变量叫什么”，而是“每个阶段把什么形状变成什么形状”。F# 列表模块把常见阶段表示为高阶函数：`filter` 选择元素，`map` 变换元素，`choose` 同时选择并变换。管道 `|>` 再把阶段按数据流顺序排列。

F# 同时支持 `for`、`while` 和 `let mutable`。它不是纯函数式语言，本章也不会把循环当成失败。我们会在同一问题上比较三种实现，看到不可变变换何时更清楚、命令式迭代何时更直接，以及优化前必须诚实计算的遍历与分配成本。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 构造列表，并理解 `[]`、`::` 与 `@` 的基本成本；
- 从类型签名读懂 `List.map`、`List.filter` 与 `List.choose`；
- 把 `x |> f` 还原为 `f x`，而不是把管道当成魔法；
- 逐阶段追踪管道的输入、输出、顺序与立即求值；
- 用 `Some`/`None` 理解 `choose` 的最小选择协议；
- 写出返回 `unit` 的 `for` 与 `while` 循环；
- 将局部可变实现与不可变列表变换按可读性和成本比较。

本章中的列表都很小且已经在内存中。数组、惰性 `seq`、`Map`、`Set` 和重复枚举留到第 14 章；递归与 `fold` 在下一章展开。

## 列表是持久不可变结构 {#list-foundations}

共享脚本的输入是一个二元组列表：

```text
(string * int) list
```

每项包含来宾名称和请求座位数，所有项具有同一元素类型。列表字面量用方括号包围；同一行的项用分号分隔，按缩进分行时也可以省略分号。

F# 列表是不可变的单向链式结构。`item :: list` 在前端创建一个新节点，并让尾部共享原列表，通常是常数时间。`left @ right` 或 `List.append left right` 必须重建左侧链，成本与 `left` 长度成正比。因此循环里反复向尾部追加容易形成二次成本；常见做法是向前端累积，再在末尾 `List.rev` 一次。

不可变仍指结构边界。列表节点不会原地改写，但若元素本身是内部可变对象，多个列表共享该对象时仍能观察到对象变化。第 14 章会把集合选择与元素语义分开讨论。

## 三个核心变换 {#core-transformations}

先只看类型形状：

```text
List.map    : ('a -> 'b)        -> 'a list -> 'b list
List.filter : ('a -> bool)      -> 'a list -> 'a list
List.choose : ('a -> 'b option) -> 'a list -> 'b list
```

三者先接收一个函数值，再接收列表，所以都适合部分应用和管道。它们保持输入顺序，但职责不同。

### `map`：每项恰好产生一项 {#map}

`List.map transform source` 对每个输入调用 `transform`，并按原顺序组成新列表。输出长度与输入相同，但元素类型可由 `'a` 变为 `'b`。

例如 `(string * int) -> string` 的 `formatRequest` 把每个预约元组变成标签；`List.map formatRequest` 因部分应用而成为 `(string * int) list -> string list`。如果某项不应进入结果，`map` 本身没有“跳过”语义。

### `filter`：决定保留原项 {#filter}

`List.filter predicate source` 要求 `predicate` 返回 `bool`。结果只包含返回 `true` 的原元素，类型仍是 `'a list`，相对顺序不变。

`filter` 适合选择与后续变换是两个清楚的概念阶段。它返回结果列表而不删除源列表中的元素；被保留的元素值本身可以由两个列表共同引用。不要依赖结果列表是否复用某段内部链，那是实现细节。

### `choose`：零项或一项输出 {#choose}

`List.choose chooser source` 让每个输入产生 `Some value` 或 `None`。`Some value` 把转换后的值放入结果，`None` 不产生结果项，因此一次表达选择与变换。

这里先把 `option` 当作最小协议：`Some x` 表示这次有值，`None` 表示没有值。第 9 章会完整讨论缺失建模、组合与 `Some null` 边界；现在不要把 `None` 当作普通空字符串或失败信息。

当筛选与映射共享同一判断、且中间列表没有独立意义时，`choose` 能更直接地写出“一次输入最多产生一项输出”。若两个阶段各有领域名称或需要单独观察，保留 `filter` 与 `map` 反而更清楚。

## 管道把最后实参放回数据流 {#pipelines}

管道操作符的核心等价关系很小：

```text
value |> functionValue

等价于 / is equivalent to

functionValue value
```

因此 `requests |> List.filter isValidRequest` 等价于 `List.filter isValidRequest requests`。第 3 章的参数顺序让列表位于最后，前面的谓词先被部分应用，管道才自然。

### 按阶段阅读 {#pipeline-stages}

共享管道如下：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#filter-map-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

从上到下记录类型和值：

| 阶段 | 类型 | 值摘要 |
| --- | --- | --- |
| `requests` | `(string * int) list` | 4 个请求 |
| `List.filter isValidRequest` 后 | `(string * int) list` | Lin 与 Sam 两项 |
| `List.map formatRequest` 后 | `string list` | `"Lin:3"` 与 `"Sam:2"` |

管道没有新增控制流。它只是把前一阶段的值作为后一函数的最后实参。每行左对齐让变换顺序可扫描，也减少多层调用括号。

### 列表管道是立即求值 {#eager-pipelines}

这里每个 `List` 操作在调用时遍历输入并完成结果列表。`filter` 先产生一个中间列表，然后 `map` 再遍历它；管道符不会把它们自动融合，也不会让列表变成惰性序列。

这不等于两阶段管道一定“慢”。中间结果可能提升可读性，数据也可能很小。先写清楚并测量真实瓶颈；当一次遍历确实重要且逻辑天然合一时，再用 `choose` 或第 6 章的 `fold` 表达。

### 管道也可能降低可读性 {#pipeline-boundaries}

一个直接调用 `List.isEmpty values` 往往比只为使用操作符而写的单阶段管道更清楚。参数没有为最后实参设计、阶段混入大量效果、或匿名函数过长时，管道也会隐藏信息。

把复杂阶段提取成有名称的函数，并让每个阶段的输出类型容易说清。管道是数据流记法，不是要求所有表达式都必须竖排的风格规则。

## 用 `choose` 合并相关阶段 {#choose-pipeline}

共享脚本把有效性与格式化组合成一个选择函数：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#choose-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`tryFormatRequest` 的类型是 `(string * int) -> string option`。有效请求产生 `Some label`，无效请求产生 `None`；`List.choose` 提取 `Some` 内的文本并保持顺序，最终同样得到 `string list`。

命名前缀 `try` 在 F#/.NET 代码中常提示操作可能不产生正常值，但具体失败表示仍要看类型。这里类型只区分有或无，没有错误原因；需要解释原因的验证不应塞进 `None`，以后应选择 `Result` 或累积验证。

## 效果使用 `iter` 或 `for` {#iteration-for-effects}

变换函数回答“新数据是什么”。若目标是依次执行输出等效果而不收集结果，`List.iter action` 或 `for item in source do ...` 更贴切。它们的动作/循环主体返回 `unit`，整个迭代也返回 `unit`。

共享示例用 `for` 证明标签顺序：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#list-iteration{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`for` 遍历可枚举输入，并可在循环变量位置使用模式。它适合日志、写入已有缓冲区或调用命令式 API。若真正需要一个新列表，循环必须另外管理累积状态，而 `map`/`filter` 已经把这个意图编码在返回类型中。

不要把效果放进 `map` 只因为它会访问每项：那会同时创建一个可能被忽略的结果列表，并把“产生数据”与“执行效果”混在一起。使用 `iter` 或 `for` 让 `unit` 意图显式。

## 可变绑定是显式工具 {#mutable-bindings}

`let mutable name = initial` 建立可变存储位置，`name <- next` 更新它。`=` 仍用于绑定或相等判断，不用于更新。

可变状态增加时间顺序：要理解某一行的 `name`，必须知道此前哪些路径执行过 `<-`。把状态限制在一个小函数内部、不给外部持有引用，可以把这种推理成本封装起来。共享脚本中的两个命令式版本都遵守这个边界。

### `for` 版本：枚举由语言管理 {#for-version}

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#for-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

循环按输入顺序调用 `tryFormatRequest`。有效标签用 `::` 常数时间加到 `reversedLabels` 前端，所以暂时顺序相反；循环结束后 `List.rev` 一次恢复原顺序。

`for` 主体的两条 `match` 分支都是 `unit`：更新 `<-` 的结果是 `()`，`None` 分支显式返回 `()`。函数最后一个表达式 `List.rev reversedLabels` 才产生 `string list`。

### `while` 版本：条件与进度都由代码管理 {#while-version}

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#while-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`while` 在条件为 `true` 时反复执行 `unit` 主体。这里代码必须手工维护 `remaining`，并在每次非空匹配后更新为 `tail`；漏掉这一步会产生无限循环。空列表规则仍然存在，因为编译器不会根据外部循环条件删除类型上的可能形状。

这个版本能工作，也只在局部修改两个绑定，但比 `for` 或 `choose` 暴露更多机械状态。`while` 更适合下一步是否继续真正取决于变化状态、且没有现成集合遍历抽象的问题，例如与某些底层 API 对接。

## 三种实现怎样选择 {#choosing-style}

共享脚本用结构相等证明三种实现产生相同标签和顺序。选择不应依据“函数式永远好”或“循环一定快”，而应依据问题：

| 目标 | 通常先考虑 | 原因 |
| --- | --- | --- |
| 从集合产生新集合 | `map`、`filter`、`choose`、以后 `fold` | 返回类型直接表达变换 |
| 对每项执行效果 | `List.iter` 或 `for` | `unit` 意图明确，不制造无用结果 |
| 条件依赖显式变化状态 | 小范围 `while` + `mutable` | 状态机可能比扭曲的变换更直接 |
| 热路径需要减少遍历/分配 | 先测量，再合并阶段或使用合适集合 | 清晰基线与证据比猜测可靠 |

函数式版本也可能分配过多中间列表；命令式版本也可能因错误的尾部追加退化。语法范式不是性能证明。先保证结果、顺序和边界相同，再用基准或分析工具比较真实成本。

## 运行共享示例 {#run-example}

从仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch05-lists-pipelines.fsx
```

应得到：

```text
Pipeline labels: ["Lin:3"; "Sam:2"]
Chosen labels: ["Lin:3"; "Sam:2"]
For/while agree: true
Iteration order: Lin:3 Sam:2
```

manifest 按顺序检查四行，包括三个实现的相等性和效果遍历顺序。源 `requests` 从未改变；每个结果都是新列表值。

## 调试：在每个管道边界停一下 {#debugging}

管道报错时不要一次读完整链：

1. 写出当前阶段输入类型；
2. 查看右侧函数已部分应用后的剩余参数类型；
3. 确认管道值正好适合最后一个参数；
4. 在 FSI 中临时绑定中间结果；
5. 检查下一阶段是否需要值、列表还是 `option`。

输出顺序不对时，寻找 `::` 前插与遗漏的 `List.rev`。循环不结束时，确认每条循环路径都推进条件相关状态。列表结果长度不对时，分别统计 `filter` 的真值项或 `choose` 的 `Some` 项。

效果发生两次时，检查是否在验证阶段重复调用了带效果的映射函数。本章列表立即求值，每次显式调用都会重新遍历；第 14 章的惰性序列会带来另一种重复枚举风险。

## 练习 {#exercises}

每题先写阶段类型与中间值，再运行。相同最终列表不是唯一证据；还要比较源数据、顺序与效果。

### 练习 1：逐阶段追踪管道 {#exercise-01}

针对 `filter-map-pipeline`：

1. 写出 `requests`、过滤后列表和映射后列表的类型；
2. 写出两个中间列表的确切元素顺序；
3. 展开两个 `|>`，改写为不使用管道的等价调用；
4. 说明源列表是否变化，以及该管道遍历几次列表阶段。

### 练习 2：用 `choose` 合并选择与变换 {#exercise-02}

解释 `tryFormatRequest` 对四个请求分别返回什么，并写出完整类型。然后说明 `List.choose` 如何得到与 `filter` 加 `map` 相同的结果。

比较两种写法：什么情况下保留独立过滤结果更清楚？什么情况下 `choose` 更准确？`None` 在这个示例中丢失了什么信息？

### 练习 3：比较循环状态 {#exercise-03}

对 `labelsWithFor` 和 `labelsWithWhile`：

1. 在处理 Lin、Ada、Sam、Mina 后分别写出 `reversedLabels`；
2. 解释为什么末尾需要 `List.rev`；
3. 指出 `while` 每轮必须推进的状态，以及遗漏会怎样；
4. 为“打印每个标签”和“产生新标签列表”分别选择首选形式并说明原因。

[查看本章练习答案](../solutions/ch-05-lists-pipelines)。

## 小结 {#summary}

- F# 列表是有序不可变单向链；`::` 前插通常为常数时间，追加必须遍历左侧。
- `map` 每项产生一项，`filter` 保留原项，`choose` 用 `Some`/`None` 表达零项或一项输出。
- `x |> f` 只是 `f x` 的数据流写法；它依赖适合部分应用的参数顺序。
- `List` 管道立即求值，多阶段可能产生中间列表；管道不自动惰性化或融合。
- 只有效果时使用 `iter` 或 `for`，避免用 `map` 制造被忽略的列表。
- `let mutable` 与 `<-` 明确表示变化存储；小范围封装能控制推理成本。
- `while` 要手工维护进度，适合真正状态驱动的问题，而不是默认集合遍历方式。

下一章会把“向前端累积再反转”推广为递归与累加器，再用 `fold` 把一类显式递归重写成可复用集合操作，并准确讨论尾调用边界。

## 词汇 {#vocabulary}

- **列表（list）：** 同类型元素构成的有序不可变单向链式集合。
- **管道（pipeline）：** 用 `|>` 把左侧值作为右侧函数的最后实参。
- **立即求值（eager evaluation）：** 操作被调用时就完成计算，而非延迟到以后枚举。
- **option：** `Some value` 表示有值，`None` 表示无值的类型。
- **效果（effect）：** 输出或状态修改等不能只由返回值描述的可观察行为。
- **可变绑定（mutable binding）：** 用 `let mutable` 建立、可通过 `<-` 更新的存储位置。

## 来源 {#sources}

- [Microsoft Learn：列表与 `map`/`filter`/`choose`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core：List 模块参考](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [Microsoft Learn：函数与管道](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：值与可变绑定](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn：`for...in`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [Microsoft Learn：`while...do`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-while-do-expression)
