---
title: "第 18 章答案"
description: "分离纯检查、依赖检查和有副作用检查，实现有序错误累积，并把未说明的计算表达式改写为明确规则。"
translationKey: solutions/ch-18-workflow-validation
---

# 第 18 章答案 {#overview}

验证顺序取决于数据依赖。先一起验证彼此独立的原始字段，再把得到的类型化值交给依赖性领域检查和副作用操作。

[返回第 18 章](../part-03/ch-18-workflow-validation)。

## 练习 1：画出两个验证阶段 {#exercise-01}

### 对检查进行分类 {#exercise-01-classification}

给定内存中的原始命令：

| 检查 | 分类 | 原因 |
|---|---|---|
| 请求标识非空且格式正确 | 独立纯输入验证 | 只使用原始请求标识文本 |
| 参与者姓名非空 | 独立纯输入验证 | 只使用原始参与者文本 |
| 座位文本可解析为正数 | 与其他字段独立；内部有依赖 | 正数检查依赖解析成功，但不需要其他字段 |
| 请求座位数不超过给定 `Capacity` | 依赖性的纯领域验证 | 需要有效 `SeatCount` 与 `Capacity` |
| 请求标识在数据库中唯一 | 有副作用的边界工作 | 需要有效标识与可能立即过时的外部查询 |

如果当前容量也必须加载，而不是以领域值传入，那么获取容量同样有副作用。加载完成后，比较仍可保持为纯函数。

### 排列各个阶段 {#exercise-01-order}

```text
累积请求标识 + 参与者 + 座位文本错误
                    ↓ 仅当成功
把有效 SeatCount 与给定/当前 Capacity 比较
                    ↓ 仅当成功
查询有效 RequestId 的唯一性
                    ↓ 仅当成功
在提交时原子地强制容量与唯一性
```

第一阶段运行三项有用的输入检查。座位解析失败时，容量比较因缺少 `SeatCount` 而短路。唯一性查询要等廉价验证与领域决策通过后再执行，从而避免不必要的 I/O。

数据库唯一性查询只在写入前提供建议。查询之后，另一个请求仍可能抢先占用同一标识，所以提交边界必须原子地强制唯一性。实时容量同理：预检查无法阻止后续竞争。

测量成本或产品策略不同时，可以调整容量检查与唯一性查询的顺序，但二者都必须位于类型化前提之后。这是工作流主动选择的顺序，不是 `Result` 自带的性质。

## 练习 2：实现有序累积 {#exercise-02}

### 小型可复用 apply 函数 {#exercise-02-apply}

```fsharp
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

已有的累积函数位于左边，所以两边失败时追加 `earlier @ later`。这会把错误顺序固定为值结果进入管道的顺序。

### 验证三个字段 {#exercise-02-fields}

```fsharp
open System

type FormError =
    | MissingName
    | InvalidEmail of raw: string
    | InvalidSeats of raw: string

type ValidForm =
    { Name: string
      Email: string
      Seats: int }

let validateName (raw: string) =
    if String.IsNullOrWhiteSpace raw then Error [ MissingName ]
    else Ok(raw.Trim())

let validateEmail (raw: string) =
    if raw.Contains('@') then Ok raw
    else Error [ InvalidEmail raw ]

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, seats when seats > 0 -> Ok seats
    | _ -> Error [ InvalidSeats raw ]

let createForm name email seats =
    { Name = name
      Email = email
      Seats = seats }

let validateForm name email seats =
    Ok createForm
    |> applyValidation (validateName name)
    |> applyValidation (validateEmail email)
    |> applyValidation (validateSeats seats)
```

需要进行下面的检查：

```fsharp
assert (
    validateForm "" "wrong" "zero" =
        Error [ MissingName; InvalidEmail "wrong"; InvalidSeats "zero" ]
)

assert (
    validateForm " Lin " "lin@example.test" "3" =
        Ok { Name = "Lin"; Email = "lin@example.test"; Seats = 3 }
)
```

`Error []` 表示验证失败，却没有提供原因，这与 API 契约矛盾。普通列表无法阻止这个状态。若调用方或自定义组合函数能直接构造错误，应改用非空列表类型；若只有这些小型可信函数构造结果，经过测试的约定也可能足够。

## 练习 3：审查计算表达式的语义前提 {#exercise-03}

### 找出缺失的契约 {#exercise-03-builder}

`result` 必须是一个值，其类型提供计算表达式成员。FSharp.Core 定义了 `Result` 及其模块函数，却没有名为 `result`、能够建立该构建器的内置值。导入某个库或定义构建器后，这段代码可能编译；缺少该上下文时，它并不完整。

`let!` 主要使用构建器的 `Bind`。`and!` 主要使用 `MergeSources`，还可能使用可选的 `MergeSourcesN`、`BindN` 或 `BindNReturn` 优化。同一个 `let!`/`and!` 组中的请求标识与座位计算不能引用彼此绑定出来的值。

即使代码能够编译，是否累积仍取决于该构建器如何合并两个 `Error`。语法本身不会提供列表追加。

### 直接写出累积规则 {#exercise-03-rewrite}

对于返回错误列表的验证器，完整的两项检查规则是：

```fsharp
let validatePair raw =
    let requestIdResult = validateRequestId raw.RequestId
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, seatsResult with
    | Ok requestId, Ok seats -> Ok(requestId, seats)
    | Error requestErrors, Error seatErrors -> Error(requestErrors @ seatErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

两个验证器调用都在匹配之前发生，两个失败都被保留，而且请求标识错误在前。如果座位验证改为需要请求标识验证产生的值，这种组合就不诚实；应使用 `Result.bind` 短路该依赖。

之后可以用自定义验证构建器编码相同规则，并以直接函数为基准测试。这份重写也明确记录了 `MergeSources` 必须实现的语义。

## 要点 {#what-to-notice}

- 独立原始字段、依赖领域决策和外部查询属于不同阶段。
- 错误顺序来自组合函数与调用顺序。
- 有类型的预检查不能替代并发存储边界的原子强制。
- 可复用 apply 函数可以消除重复，而不隐藏它的四种情况。
- `list` 允许空错误集合；应判断这在 API 边界上是否重要。
- 计算表达式关键字由构建器解释，所以必须先说清构建器，再声称语义。
