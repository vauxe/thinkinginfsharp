---
title: "第 5 章练习答案"
description: "列表变换、管道、choose、for、while 与局部可变状态的推理答案。"
translationKey: solutions/ch-05-lists-pipelines
---

# 第 5 章练习答案 {#overview}

先核对每个阶段的类型和顺序。最终输出相同，仍可能掩盖多余遍历、泄漏的可变状态或重复副作用。

[返回第 5 章](../part-01/ch-05-lists-pipelines)。

## 练习 1：逐阶段追踪管道 {#exercise-01}

共享管道是：

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
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

## 练习 2：用 `choose` 合并选择与变换 {#exercise-02}

答案区域是：

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
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

## 练习 3：比较循环状态 {#exercise-03}

`for` 与 `while` 版本分别是：

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
```
```fsharp:line-numbers [ch05-lists-pipelines.fsx]
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

## 应该注意什么 {#what-to-notice}

- **管道不改变求值模型：** 列表阶段仍立即完成，各自产生结果。
- **顺序是契约的一部分：** 前插高效但反转顺序，最后需要恢复。
- **局部可变性可以被封装：** 外部只看到输入与不可变结果，但内部仍需追踪每次更新。
- **`option` 只表达有无：** 若消费者需要失败原因，`None` 信息不足。

如果你直接用 `@ [ label ]` 在每轮向尾部追加，结果可能相同，但每次都会遍历不断增长的左列表。正确性通过之后仍应检查这种渐进成本。
