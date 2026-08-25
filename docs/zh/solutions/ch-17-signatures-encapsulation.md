---
title: "第 17 章答案"
description: "规定抽象电子邮件类型，收窄不一致的分配表面，并让函数元数与辅助函数可访问性在签名文件对中保持一致。"
translationKey: solutions/ch-17-signatures-encapsulation
---

# 第 17 章答案 {#overview}

先确定调用方必须构造、观察和决策什么。签名应公开这套完整词汇，不多公开任何只是方便当前实现的东西。

[返回第 17 章](../part-03/ch-17-signatures-encapsulation)。

## 练习 1：设计电子邮件地址文件对 {#exercise-01}

### 公共签名 {#exercise-01-signature}

`EmailAddress.fsi` 可以公开可行动的错误与抽象的成功值：

```fsharp
namespace Contacts

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress

module EmailAddress =
    val create: raw: string -> Result<EmailAddress, EmailAddressError>
    val value: address: EmailAddress -> string
```

消费者可以匹配 `Blank` 和 `MissingAtSign`，却没有公开的 `EmailAddress` 联合案例。它们只能通过 `create` 等已发布函数获得该类型。

### 与之匹配的实现 {#exercise-01-implementation}

`EmailAddress.fs` 提供表示，并让规范化辅助函数不出现在签名中：

```fsharp
namespace Contacts

open System

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress = EmailAddress of string

module NormalizedText =
    let create (raw: string) = raw.Trim()

module EmailAddress =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error Blank
        else
            let normalized = NormalizedText.create raw

            if normalized.Contains('@') then
                Ok(EmailAddress normalized)
            else
                Error(MissingAtSign normalized)

    let value (EmailAddress address) = address
```

`NormalizedText` 是普通实现声明，但从匹配签名中省略它，就会让它在该实现文件之外不可用。也可以额外把它声明为 `private`；对于后续消费者，签名省略已经足够。

项目顺序是 `EmailAddress.fsi`、`EmailAddress.fs`，随后才是任何消费者文件。后续文件能看到错误案例、抽象类型、`create` 和 `value`，却看不到 `NormalizedText` 或 `EmailAddress` 联合案例。

该示例只检查空白与是否含有 `@`；它不声称实现完整的电子邮件地址语法。公共错误名称准确声明了这项有意保持简单的策略。

## 练习 2：收窄过度公开的分配 API {#exercise-02}

### 用工作流代替构造 {#exercise-02-redesign}

假设 `Capacity` 和 `SeatCount` 已经是受保护类型。分配表面可以是：

```fsharp
type AllocationError =
    | InsufficientCapacity of requested: int * available: int

type Allocation

module Allocation =
    val capacity: allocation: Allocation -> Capacity
    val requested: allocation: Allocation -> SeatCount
    val remaining: allocation: Allocation -> int

val allocate:
    capacity: Capacity ->
    requested: SeatCount ->
    Result<Allocation, AllocationError>
```

这里没有 `unsafeCreate`。`allocate` 是唯一已发布的生产者，所以实现可以建立 `remaining = capacity - requested`，并拒绝超过容量的请求。前两个访问器返回受保护的组件类型，从而保留已有证明；剩余座位返回 `int` 是诚实的，因为允许为零。

### 让有用的错误保持透明 {#exercise-02-error}

`AllocationError` 应保持透明，因为调用方需要区分容量不足，并可以在 UI 或 API 响应中使用两个数字。隐藏错误表示会需要替代的谓词或格式化函数，让正常控制流变得更不直接。

如果 `Allocation` 有意作为数据传输或报告快照、其字段类型所允许的每种组合都合法，而且直接构造与复制更新属于消费者契约，那么透明记录很合适。只要三个整数还声称存在调用方可以破坏的关系，它就不合适。

不透明性应保护真实规则，而不是只为禁止方便的记录语法。已发布的观察方式仍须让调用方完成每项受支持任务。

## 练习 3：修复元数并选择辅助函数边界 {#exercise-03}

### 匹配柯里化签名 {#exercise-03-arity}

签名描述两次应用：

```fsharp
apply policy request
```

元组化实现只接收一个值对，所以元数不同。去掉元组模式即可修复：

```fsharp
let apply policy request =
    // 计算 Result<Decision, DecisionError>
    // ...
```

把签名改为 `val apply: policy: Policy * request: Request -> ...` 也能让文件对一致，但会发布另一种调用约定。当用一个策略进行部分应用是代表性用法时，应保留柯里化形式。

### 选择最小的辅助函数作用域 {#exercise-03-helper}

如果追踪只在实现文件中使用，就从签名中省略它，并显式表达局部意图：

```fsharp
let private traceDecision decision =
    // ...
```

如果同一程序集的后续文件确实需要它，签名必须公开仅程序集可用的值：

```fsharp
val internal traceDecision: decision: Decision -> string
```

实现必须匹配：

```fsharp
let internal traceDecision decision =
    // ...
```

现在程序集中的后续文件可以调用它，外部程序集则不能。只在 `Library.fs` 中写 `internal`，却从 `Library.fsi` 省略该值，仍会让它在实现文件之外保持隐藏，因为签名就是可见清单。

不要只为方便白盒测试就扩大辅助函数的范围。应优先通过已发布决策测试 `apply`；只有另一个真实实现消费者拥有这项依赖时，才扩大可见性。

## 要点 {#what-to-notice}

- 透明错误案例与抽象成功类型可以共存于一个有意设计的 API 中。
- 即便实现原本会把辅助函数推断为公开，从签名中省略它也会将其隐藏。
- 隐藏的记录需要足够的观察函数，而不是不安全逃生口。
- 即便提到相同的输入类型，柯里化函数和元组化函数的公共元数也不同。
- 跨文件需要的 `internal` 值必须以匹配的可访问性同时出现在两个文件中。
- 签名设计从受支持的消费者工作出发，而不是从实现当前存在的每个名称出发。
