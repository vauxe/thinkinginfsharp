---
title: "第 6 章练习答案"
description: "结构递归、累加器不变量、尾调用与左右折叠的推理答案。"
translationKey: solutions/ch-06-recursion-folds
---

# 第 6 章练习答案 {#overview}

递归答案必须解释递减、不变量和返回后工作；仅写最终整数没有验证算法结构。

[返回第 6 章](../part-01/ch-06-recursion-folds)。

## 练习 1：展开结构递归 {#exercise-01}

共享定义是：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let rec sumRecursive values =
    match values with
    | [] -> 0
    | head :: tail -> head + sumRecursive tail
```
完整展开为 `3 + sumRecursive [0; 4]`，再到 `3 + (0 + sumRecursive [4])`，再到 `3 + (0 + (4 + sumRecursive []))`，基础规则给出 `0`，最终为 `7`。

三次非空调用的 `(head, tail)` 分别是 `(3, [0; 4])`、`(0, [4])`、`(4, [])`。每次尾部长度减少一，有限输入最终到达空列表，所以终止。

每层都必须等待递归结果，再执行 `head + result`。这些三个加法是尚未完成的工作，因此自调用不在尾位置，调用深度随列表长度增加。

## 练习 2：证明累加器含义 {#exercise-02}

尾递归定义为：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
[<TailCall>]
let rec sumLoop accumulator values =
    match values with
    | [] -> accumulator
    | head :: tail -> sumLoop (accumulator + head) tail

let sumTailRecursive values = sumLoop 0 values
```
状态依次是 `(0, [3; 0; 4])`、`(3, [0; 4])`、`(3, [4])`、`(7, [])`。每一步中，累加器加剩余列表之和都为 `7`；最后剩余和为零，累加器就是结果。

若先递归再加 `head`，调用返回后仍有加法，`[<TailCall>]` 应给出非尾递归警告，在本书警告即错误设置下阻止构建。即使属性检查通过，它仍未证明输入有限、参数确实递减、算术不溢出或结果满足领域规则；这些要靠推理、类型选择和测试分别验证。

## 练习 3：展开折叠并选择抽象 {#exercise-03}

顺序示例是：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let leftAssociated = List.fold (fun state value -> state - value) 0 [ 1; 2; 3 ]
let rightAssociated = List.foldBack (fun value state -> value - state) [ 1; 2; 3 ] 0

printfn "Fold order: left=%d right=%d" leftAssociated rightAssociated
```
左折叠括号为 `((0 - 1) - 2) - 3`，结果 `-6`。右折叠括号为 `1 - (2 - (3 - 0))`，结果 `2`。两者的方向与 folder 参数顺序都不同。

用 `List.fold` 计数时，初始状态是 `0`；folder 可写成接收 `count` 与被忽略元素、返回 `count + 1`，抽象类型为 `int -> 'a -> int`。完整操作把 `'a list` 折叠成 `int`。

普通求和优先 `List.sum`，因为名称直接表达意图。寻找首个匹配项优先 `List.tryFind`，它能提前停止；普通 `fold` 通常遍历全部输入。二叉树应使用与叶/分支结构对齐的直接递归，直到第 10 章再抽取树折叠。

## 应该注意什么 {#what-to-notice}

- **结构递减与尾位置是两项检查：** 函数可以终止但非尾递归，也可以尾调用却永不接近基础情况。
- **累加器需要不变量：** 多一个参数不是证明，必须说明它在每步代表什么。
- **折叠顺序会影响结果：** 只有对相应结合律成立的操作，方向差异才可能被隐藏。
- **先用专用操作：** 能写 `List.sum` 或 `List.tryFind` 时，手写 fold 未必更清楚。

若你的长度 folder 使用元组状态或额外索引也能得到正确结果，但那增加了本题不需要的不变量。最小状态通常最容易验证。
