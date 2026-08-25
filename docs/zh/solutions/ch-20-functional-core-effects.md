---
title: "第 20 章答案"
description: "暴露隐藏运行时输入，选择最小而诚实的依赖形状，并保留预期边界失败而不压平契约违规。"
translationKey: solutions/ch-20-functional-core-effects
---

# 第 20 章答案 {#overview}

暴露消费者所需的最小能力；当消费者不需要再次观察的权力时，优先传入已捕获数据。注入让取得过程可见；只有取得完成后才进入纯核心。

[返回第 20 章](../part-04/ch-20-functional-core-effects)。

## 练习 1：暴露三个隐藏输入 {#exercise-01}

### 把已捕获事实与决策分开 {#exercise-01-core}

```fsharp
open System

type OfferPolicy =
    { EndsAt: DateTimeOffset
      WinningDrawExclusive: int }

type OfferFacts =
    { ObservedAt: DateTimeOffset
      Draw: int
      Region: string }

type OfferDecision =
    | Expired
    | NotSelected
    | Selected of region: string

let decideOffer policy facts =
    if facts.ObservedAt >= policy.EndsAt then
        Expired
    elif facts.Draw >= policy.WinningDrawExclusive then
        NotSelected
    else
        Selected facts.Region
```

该函数无法再次读取时间、推进随机源或检查进程环境。两个实参描述了其完整决策输入。

### 把取得过程放进一个编排函数 {#exercise-01-boundary}

```fsharp
type OfferEffects =
    { UtcNow: unit -> DateTimeOffset
      NextInt: int -> int
      ReadSetting: string -> string option }

let captureOffer effects =
    { ObservedAt = effects.UtcNow()
      Draw = effects.NextInt 100
      Region =
        effects.ReadSetting "OFFER_REGION"
        |> Option.defaultValue "global" }

let fixedInstant = DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)
let mutable calls = 0

let fixedEffects =
    { UtcNow = fun () -> calls <- calls + 1; fixedInstant
      NextInt = fun upper -> calls <- calls + 1; assert (upper = 100); 7
      ReadSetting = fun name -> calls <- calls + 1; assert (name = "OFFER_REGION"); Some "eu" }

let policy =
    { EndsAt = fixedInstant.AddHours(1.0)
      WinningDrawExclusive = 10 }

let facts = captureOffer fixedEffects
let first = decideOffer policy facts
let replay = decideOffer policy facts

assert (first = Selected "eu")
assert (replay = first)
assert (calls = 3)
```

可变计数器只是测试仪表。两次决策使用相同事实，而且不会改变它。正式适配器可以替换三个函数字段，无须改变 `decideOffer`。

## 练习 2：选择数据、函数、闭包或接口 {#exercise-02}

### 让能力与需要相称 {#exercise-02-choices}

| 情况 | 选择 | 原因 |
|---|---|---|
| 一次到期比较使用同一时刻 | 捕获 `DateTimeOffset` 数据 | 消费者不应拥有再次读取时间的权力 |
| 重试策略在每次失败后请求新延迟 | `int -> TimeSpan` 等函数 | 每次尝试都有意请求另一个值 |
| 格式化器保留不可变区域性与前缀 | 闭包 | 配置只捕获一次，并隐藏在一项格式化操作后面 |
| 跨语言存储客户端拥有必须释放的连接和相关读写操作 | 扩展或暴露释放策略的接口 | 操作、身份与生命周期组成一个组件契约 |
| 一个内部工作流需要时钟、抽取与设置查询 | 工作流专用的小型函数记录 | 具名局部能力一起传递；领域函数只接收已捕获数据 |

重试函数的生命周期必须覆盖重试操作，其失败契约也必须说明产生延迟是否会失败。只有当格式化操作与捕获值都纯净时，格式化闭包才纯净。存储接口不会让 I/O 变纯；它为有副作用组件提供稳定边界与生命周期。

不要把整个工作流依赖记录传给到期比较。那会授予不必要的能力，并让真实依赖变得不明显。

## 练习 3：让边界失败显式 {#exercise-03}

### 返回预期设置缺失 {#exercise-03-result}

使用本章的 `Campaign`、`Candidate` 与 `RuntimeEffects` 类型：

```fsharp
type CaptureError =
    | MissingRequiredSetting of name: string

let captureCandidateRequired campaign effects =
    let submittedAt = effects.UtcNow()
    let draw = effects.NextInt 10_000

    if draw < 0 || draw >= 10_000 then
        invalidArg (nameof effects) "NextInt returned a value outside its requested range."

    match effects.ReadSetting "BOOKING_REGION" with
    | None -> Error(MissingRequiredSetting "BOOKING_REGION")
    | Some raw when String.IsNullOrWhiteSpace raw ->
        Error(MissingRequiredSetting "BOOKING_REGION")
    | Some raw ->
        Ok
            { SubmittedAt = submittedAt
              Draw = draw
              Region = raw.Trim() }
```

测试应在不读取进程环境的情况下覆盖两个分支：

```fsharp
let missingEffects =
    { UtcNow = fixedClock instant
      NextInt = fixedDraw 42
      ReadSetting = fun _ -> None }

assert (
    captureCandidateRequired campaign missingEffects =
        Error(MissingRequiredSetting "BOOKING_REGION")
)
```

必填配置缺失是预期的启动或请求边界事实，调用方能够报告它。超出范围的结果违反 `NextInt` 函数契约；本答案把它保留为 `ArgumentException`，因为普通业务恢复无法让提供者变正确。如果提供者属于不可信输入，而且调用方可以选择另一个提供者，那么专门的 `InvalidDrawProvider` 错误用例也可能诚实。

不要把两个条件合并成 `Error "capture failed"`。一项标识配置缺失；另一项标识损坏的依赖契约，需要不同诊断与所有者。

## 应注意什么 {#what-to-notice}

- 已捕获数据可以表达比注入能力更强的快照保证。
- 即使每项依赖都显式，编排函数仍可能有副作用。
- 闭包是函数契约的实现，不是与形参竞争的另一种形式。
- 接口通过连贯操作、生命周期、工具或公开边界需求证明其成本合理。
- 预期缺失与提供者契约违规应该保持可区分。
