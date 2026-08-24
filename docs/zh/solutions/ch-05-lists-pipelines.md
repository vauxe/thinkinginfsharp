---
title: "第 5 章练习答案"
description: "列表变换、管道、choose、for、while 与局部可变状态的推理答案。"
translationKey: solutions/ch-05-lists-pipelines
kind: solution
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
termIds: []
sources:
  - id: microsoft-lists
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists
    checked: "2026-08-24"
  - id: microsoft-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/
    checked: "2026-08-24"
---

# 第 5 章练习答案 {#overview}

先核对每个阶段的形状和顺序。只有最终输出相同，仍可能掩盖多余遍历、状态未封装或效果重复。

[返回第 5 章](../part-01/ch-05-lists-pipelines)。

## 练习 1：逐阶段追踪管道 {#exercise-01}

共享管道是：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#filter-map-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`requests` 与过滤结果都是 `(string * int) list`；前者依次为 Lin 3、Ada 0、Sam 2、Mina -1，后者只保留 Lin 3、Sam 2。映射结果是 `string list`，顺序为 `[ "Lin:3"; "Sam:2" ]`。

不使用管道的等价表达式是先求 `List.filter isValidRequest requests`，再把结果作为 `List.map formatRequest` 的最后实参；嵌套写成 `List.map formatRequest (List.filter isValidRequest requests)`。源列表没有变化。

这段执行两个立即求值列表阶段：过滤遍历四项并产生中间列表，映射再遍历两项并产生最终列表。调用次数和元素访问次数不是同一个数字，但确实存在两次列表操作。

## 练习 2：用 `choose` 合并选择与变换 {#exercise-02}

答案区域是：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#choose-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`tryFormatRequest` 的完整类型为 `(string * int) -> string option`。它依次产生 `Some "Lin:3"`、`None`、`Some "Sam:2"`、`None`。`List.choose` 只提取两个 `Some` 的内部值，保持顺序，得到与过滤后映射相同的 `string list`。

若“有效请求”列表需要单独记录、测试或交给其他步骤，`filter` 与 `map` 的分段更清楚。若只有有效项才有可构造的输出，且中间列表没有领域意义，`choose` 更准确。这里 `None` 丢掉了请求为何无效以及原请求内容；需要原因时应使用携带错误的模型。

## 练习 3：比较循环状态 {#exercise-03}

`for` 与 `while` 版本分别是：

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#for-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#while-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

两个版本的 `reversedLabels` 变化相同：处理 Lin 后是 `[ "Lin:3" ]`；Ada 产生 `None`，不变；Sam 用 `::` 加到前端后是 `[ "Sam:2"; "Lin:3" ]`；Mina 产生 `None`，仍不变。`List.rev` 恢复输入相对顺序，否则结果会把有效项倒置。

`while` 还必须让 `remaining` 从完整列表依次变为各级 `tail`，最后为 `[]`。任何非空路径忘记更新都会让条件一直为真并重复处理同一项。

“打印每个标签”首选 `for` 或 `List.iter`，因为目标是 `unit` 效果。“产生新标签列表”首选 `choose`，因为返回类型直接表达输出；若分析证明这是热路径且需要定制单遍历实现，再比较局部可变循环，而不是先假设。

## 应该注意什么 {#what-to-notice}

- **管道不改变求值模型：** 列表阶段仍立即完成，各自产生结果。
- **顺序是契约的一部分：** 前插高效但反转顺序，需显式恢复。
- **局部可变性可以被封装：** 外部只看到普通输入与不可变结果，但内部仍需追踪每次更新。
- **`option` 只表达有无：** 若消费者需要失败原因，`None` 信息不足。

如果你直接用 `@ [ label ]` 在每轮向尾部追加，结果可能相同，但每次都会遍历不断增长的左列表。正确性通过之后仍应检查这种渐进成本。
