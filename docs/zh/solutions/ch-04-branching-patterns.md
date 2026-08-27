---
title: "第 4 章练习答案"
description: "条件结果、匹配顺序、守卫、元组与列表模式的推理答案。"
translationKey: solutions/ch-04-branching-patterns
---

# 第 4 章练习答案 {#overview}

分支答案的重点不是最终字符串，而是首个成功规则、跳过原因与统一结果类型。

[返回第 4 章](../part-01/ch-04-branching-patterns)。

## 练习 1：统一 `if` 的结果 {#exercise-01}

共享定义是：

```fsharp:line-numbers
let availability remaining =
    if remaining > 0 then "available" else "full"

printfn "Availability: %s" (availability 3)
```
`availability 3` 的条件为 `true`，结果是 `"available"`；`availability 0` 的条件为 `false`，结果是 `"full"`。条件 `remaining > 0` 是 `bool`，两个分支都是 `string`，因此整个函数是 `int -> string`。

若 `then` 返回字符串而 `else` 只调用 `printfn`，两个结果分别为 `string` 与 `unit`，无法统一。输出副作用不会变成字符串结果。只有当整个条件表达式只执行副作用、`then` 也返回 `unit` 时，才可以省略 `else`；未命中路径也返回 `()`。

## 练习 2：追踪规则与守卫 {#exercise-02}

| 输入 | 首个成功规则 | 结果 |
| --- | --- | --- |
| `-2` | `value when value <= 0` | `"full"` |
| `0` | `value when value <= 0` | `"full"` |
| `1` | 字面量 `1` | `"last seat"` |
| `5` | `value when value <= 5` | `"limited"` |
| `6` | `_` | `"available"` |

输入 `1` 也初步匹配第一条变量模式，但守卫为假，所以继续到字面量规则。输入 `6` 会依次让两个守卫为假，也不匹配字面量，最终由通配符接住。

若通配符移到第一条，它会先匹配全部输入，其他规则不可达。只留下带守卫的变量规则也不构成编译器可证明的穷尽集合：守卫是一般布尔表达式，可能同时为假，还可能以后改变。无守卫兜底规则才明确覆盖余下输入。

## 练习 3：分解组合输入 {#exercise-03}

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

## 应该注意什么 {#what-to-notice}

- **分支产生值：** 优先比较右侧类型，而不只是条件是否正确。
- **模式与守卫分工：** 模式处理结构和绑定，守卫处理额外布尔关系。
- **第一条成功规则获胜：** 重排规则可能改变业务含义。
- **穷尽性需要无条件覆盖：** 人能猜到的守卫关系不是编译器的一般证明。

若另一种实现使用嵌套 `if` 得到相同结果，它可能仍然正确；比较的是哪个形式更清楚地表现两项输入、优先级与兜底，而不是 `match` 出现次数。
