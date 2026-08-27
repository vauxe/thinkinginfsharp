---
title: "第 10 章练习答案"
description: "从递归案例推导短路查询、map 定律和单次遍历的树摘要。"
translationKey: solutions/ch-10-recursive-types
---

# 第 10 章练习答案 {#overview}

每个答案都从递归类型的案例出发。这样能在编写语法之前，先看清基础行为、递归进展与组合策略。

[返回第 10 章](../part-02/ch-10-recursive-types)。

## 练习 1：从案例推导查询 {#exercise-01}

规则如下：

- `Empty` 不包含匹配值，所以返回 `false`；
- `Leaf value` 返回 `predicate value`；
- `Branch(left, right)` 在任一子树成功时成功。

直接翻译为：

```fsharp
let rec exists predicate tree =
    match tree with
    | Empty -> false
    | Leaf value -> predicate value
    | Branch(left, right) ->
        exists predicate left || exists predicate right
```

F# 的布尔 `||` 会短路。左侧调用返回 `true` 时，右侧调用不会运行。对于纯粹的存在性查询，这是理想行为，而且可能避开树的大部分。最坏情况下——不存在匹配，或匹配位于最后访问的叶子——函数仍会访问每个节点，花费 `O(n)` 时间。

不要把必须执行的副作用藏进 `predicate`，再假设每个叶子都会被访问。该函数只承诺回答是否存在，不承诺遍历全部节点。必须对每个值执行的工作，应使用另一项遍历。

## 练习 2：检验 map 定律 {#exercise-02}

fold 用相应的重建构造器替换每个构造器：

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))

let examples = [ emptyTree; leafTree; branchTree ]
let increment seats = seats + 1
let double seats = seats * 2

let identityHolds =
    examples
    |> List.forall (fun tree -> mapTreeWithFold id tree = tree)

let compositionHolds =
    examples
    |> List.forall (fun tree ->
        mapTreeWithFold (increment >> double) tree
        = (tree |> mapTreeWithFold increment |> mapTreeWithFold double))
```

对三个示例而言，两个值都是 `true`。这能发现几类常见实现错误，但递归类型允许任意规模和结构的树，三个值不可能枚举全部情况。

证明也遵循相同结构。定律对 `Empty` 与 `Leaf` 直接成立。对于 `Branch`，先假设它们对两棵更小子树成立，再证明重建会保持组合结果。这种结构归纳正是结构递归在推理上的对应物。

## 练习 3：用一次 fold 计算一份摘要 {#exercise-03}

空树规则没有最大值；叶子初始化三个字段；分支规则组合已经完成的摘要：

```fsharp
type TreeSummary =
    { LeafCount: int
      TotalSeats: int
      MaximumSeats: int option }

let emptySummary =
    { LeafCount = 0
      TotalSeats = 0
      MaximumSeats = None }

let summarizeLeaf seats =
    { LeafCount = 1
      TotalSeats = seats
      MaximumSeats = Some seats }

let combineSummaries left right =
    let maximum =
        match left.MaximumSeats, right.MaximumSeats with
        | None, other
        | other, None -> other
        | Some leftMax, Some rightMax -> Some(max leftMax rightMax)

    { LeafCount = left.LeafCount + right.LeafCount
      TotalSeats = left.TotalSeats + right.TotalSeats
      MaximumSeats = maximum }

let summarize tree =
    tree
    |> foldTree emptySummary summarizeLeaf combineSummaries
```

预期值如下：

| 树 | `LeafCount` | `TotalSeats` | `MaximumSeats` |
| --- | ---: | ---: | --- |
| `Empty` | 0 | 0 | `None` |
| `Leaf 2` | 1 | 2 | `Some 2` |
| 共享分支 | 3 | 9 | `Some 4` |

fold 会访问每个节点一次，因此时间为 `O(n)`。直接递归实现最多保留从根到当前节点的链以及待处理分支工作，所以调用栈深度为 `O(h)`。它在一次遍历中计算三个字段；编写三个独立 fold 在渐近意义上仍是 `O(n)`，但会访问树三遍。

## 应该注意什么 {#what-to-notice}

- **案例规则先于代码：** 它们会暴露缺失的基础案例与组合策略。
- **短路会改变访问范围：** `exists` 可能有意跳过整棵子树。
- **示例是检查而非证明：** 递归结构提供归纳论证。
- **`None` 是诚实的空树最大值：** 对负数叶子而言，`0` 等哨兵会给出错误答案。
- **一次复合 fold 共享遍历：** 一个状态可以携带多项相关聚合值。
