---
title: "第 3 章练习答案"
description: "函数类型、匿名函数、高阶函数、柯里化、元组参数与部分应用的推理答案。"
translationKey: solutions/ch-03-functions-as-values
---

# 第 3 章练习答案 {#overview}

先核对类型结构，再核对数值。两个调用输出相同，不代表它们以同样方式接收实参。

[返回第 3 章](../part-01/ch-03-functions-as-values)。

## 练习 1：解码箭头 {#exercise-01}

| 名称 | 加括号后的类型 | 阅读方式 |
| --- | --- | --- |
| `lineTotal` | `decimal -> (int -> decimal)` | 接收单价，返回接收座位数并产生金额的函数 |
| `standardLineTotal` | `int -> decimal` | 接收座位数，产生已经固定单价的金额 |
| `applyTwice` | `('a -> 'a) -> ('a -> 'a)` | 接收一个保持输入输出类型一致的函数，返回同样从 `'a` 到 `'a` 的函数 |
| `identity` | `'a -> 'a` | 接收任意某一类型的值，返回同一类型的值 |

`applyTwice` 也可逐个位置读成 `('a -> 'a) -> 'a -> 'a`：先给变换，再给值，最后得值。右结合让最后两个位置构成返回的 `'a -> 'a` 函数。相同 `'a` 要求一次实例化中的所有位置一致；它不是“这里可以各自放任意类型”。

`lineTotal` 的第一个实参必须是 `decimal`，第二个必须是 `int`。只提供第一个实参时，得到一个与 `standardLineTotal` 类型相同的函数，而不是金额。

## 练习 2：传入行为 {#exercise-02}

命名函数和匿名函数如下：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
```
共享脚本中的命名调用是：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
```
等价的匿名调用写作 `applyTwice (fun seats -> seats + 1) 3`，结果同样为 `5`。匿名函数与 `increment` 都是 `int -> int`，因此 `applyTwice` 在这次调用中把 `'a` 实例化为 `int`。

一个 `int -> string` 函数不能直接使用，因为第一次变换产生 `string`，第二次调用却仍要求输入 `int`。`applyTwice` 的约束是 `'a -> 'a`，不是 `'a -> 'b`。若业务真的要连续不同变换，需要另一个明确描述两阶段类型的函数，而不是削弱这里的一致性。

## 练习 3：选择参数形式 {#exercise-03}

两个可运行定义分别是：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```
```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
```
柯里化版本类型为 `decimal -> int -> decimal`，调用 `lineTotal 19.50m 3`；元组版本类型为 `decimal * int -> decimal`，调用 `lineTotalTupled (19.50m, 3)`。只有前者能直接用 `lineTotal 19.50m` 固定单价，得到 `int -> decimal`。

`addServiceFee` 保留 `2.00m`，剩余输入是小计，所以类型为 `decimal -> decimal`；这个函数形成了闭包。如果单价与座位数在领域中本来就是一个整体，元组输入能直接表达“只接受完整一对”。是否需要部分应用，不是唯一设计标准。

## 应该注意什么 {#what-to-notice}

- **箭头方向不等于求值顺序图：** 先按右结合读类型，再按左结合读具体应用。
- **形参数量不决定调用形式：** 两个名称可能是连续形参，也可能是一个元组模式中的两项。
- **部分应用返回函数：** 在剩余实参到达前，最终主体结果尚未产生。
- **泛型仍有一致性：** `'a` 可被多种具体类型实例化，但相同字母出现的位置必须对齐。

若你为了给元组版本固定一项而写一个新的包装函数，那可以工作，却正好说明它不能像柯里化版本那样直接部分应用。设计时应根据调用方式选择，而不是宣布一种形式永远优越。
