---
title: "第 25 章答案"
description: "移除仪式性类、比较函数与接口策略边界，并重新设计结构体，让其默认表示有效。"
translationKey: solutions/ch-25-objects-interfaces
---

# 第 25 章答案 {#overview}

这些答案根据可观察语义选择表示。它们不比较代码行数，也不宣称所有类都有问题：只有在移除类会抹掉真实身份、生命周期、封装、分派或生态契约时，类才应保留。

[返回第 25 章](../part-05/ch-25-objects-interfaces)。

## 练习 1：移除仪式性类 {#exercise-01}

### 让数据与验证准确表达自身含义 {#exercise-01-record}

```fsharp
open System

type SeatRequest =
    { RequestId: string
      Seats: int }

type ValidationError =
    | EmptyRequestId
    | NonPositiveSeats of actual: int

module SeatRequest =
    let create requestId seats =
        if String.IsNullOrWhiteSpace requestId then
            Error EmptyRequestId
        elif seats <= 0 then
            Error(NonPositiveSeats seats)
        else
            Ok
                { RequestId = requestId.Trim()
                  Seats = seats }

let accepted = SeatRequest.create "  REQ-25  " 2
let rejected = SeatRequest.create "REQ-25" 0

assert (accepted = Ok { RequestId = "REQ-25"; Seats = 2 })
assert (rejected = Error(NonPositiveSeats 0))
```

记录公开不可变的乘积数据，并自动获得结构相等；这正符合该请求的需求。模块负责规范化和预期验证，不必把构造变成异常控制流。

若每项请求都需要引用身份、受保护的可变状态、必须释放的资源、虚成员/接口分派或框架基类，类就有存在理由。只偏爱属性调用语法还不够。

## 练习 2：选择策略边界 {#exercise-02}

### 在不改变含义的前提下比较同一规则 {#exercise-02-policies}

```fsharp
type QuoteRequest =
    { Seats: int
      UnitPrice: decimal }

type DiscountPolicy = QuoteRequest -> decimal

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

let groupRate request =
    if request.Seats >= 5 then 0.10M else 0M

let totalWith (rate: DiscountPolicy) request =
    let subtotal = decimal request.Seats * request.UnitPrice
    subtotal * (1M - rate request)

let objectPolicy =
    { new IDiscountPolicy with
        member _.Rate request = groupRate request }

let request = { Seats = 5; UnitPrice = 10M }
let functionTotal = totalWith groupRate request
let interfaceTotal = totalWith objectPolicy.Rate request

assert (functionTotal = 45M)
assert (interfaceTotal = functionTotal)
```

对仅供 F# 使用且只有一项无状态操作的库，`DiscountPolicy` 是更小、也更容易组合的公开边界。当 .NET 框架要求接口，或其他语言需要基于成员的契约时，可以使用接口。多项操作属于一个整体，或有状态实现必须通过运行时分派选择时，接口也合理。

对象表达式保持局部，只包含一个转发成员。如果策略开始依赖其他组件、维护缓存、负责释放资源或包含大量规则，具名实现会让这些责任更清楚。

## 练习 3：审计结构体不变量 {#exercise-03}

### 先展示不安全默认值，再把默认建模为状态 {#exercise-03-default}

```fsharp
[<Struct>]
type PositiveRevision = private | PositiveRevision of int

module PositiveRevision =
    let create raw =
        if raw > 0 then Ok(PositiveRevision raw) else Error raw

    let value (PositiveRevision raw) = raw

let positive =
    PositiveRevision.create 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)
let copied = positive
let invalidDefault = Unchecked.defaultof<PositiveRevision>

assert (PositiveRevision.value copied = 3)
assert (not (obj.ReferenceEquals(box positive, box copied)))
assert (PositiveRevision.value invalidDefault = 0)

[<Struct>]
type Revision =
    private
    | Unassigned
    | Assigned of value: int

module Revision =
    let assign raw =
        if raw > 0 then Ok(Assigned raw) else Error raw

    let describe revision =
        match revision with
        | Unassigned -> "unassigned"
        | Assigned value -> $"assigned:{value}"

let initial = Unchecked.defaultof<Revision>
let assigned =
    Revision.assign 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)

assert (Revision.describe initial = "unassigned")
assert (Revision.describe assigned = "assigned:3")
```

私有 case 能保护直接构造，却不能阻止 CLR 零初始化。重新设计后，标签零（第一个 case）表示 `Unassigned`；默认值因此具有明确领域含义，而调用方创建 `Assigned` 时仍须经过验证。

只有其他需求已经说明结构体确实合适时，才采用这种设计。如果“未分配”没有意义，应优先使用非结构体领域模型，把会产生默认值的互操作留在模型外，或在每个入口立即拒绝零。`Unchecked.defaultof` 对引用表示也能制造问题值；它是不安全逃生通道，不是正常构造方式。

## 答案复盘 {#solution-review}

- 只有先辨明包装是否承载真实对象语义，才能决定移除它。
- 记录加模块函数能明确区分不可变数据与预期验证。
- 单一 F# 策略通常只是函数；接口适合真正的对象/.NET 边界。
- 对象表达式是紧凑的局部实现，不是隐藏子系统的方法。
- 私有结构体构造函数无法消除零初始化。
- 值类型必须承受默认构造时，应让全零表示有效，或在每个入口边界拒绝它。
