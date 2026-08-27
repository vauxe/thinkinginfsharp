---
title: "第 27 章练习答案"
description: "把暴露出的 F# 结果转换为受控 .NET 响应，用重载演进查询，并用专用 DTO 隔离序列化要求。"
translationKey: solutions/ch-27-fsharp-api-for-csharp
---

# 第 27 章练习答案 {#overview}

三份答案遵循同一原则：领域模型不迁就调用方技术，调用方也不必学习实现语言。两种表示之间的转换与兼容成本由适配器承担。

[返回第 27 章](../part-05/ch-27-fsharp-api-for-csharp)。

## 练习 1：封装泄露的 F# 表示 {#exercise-01}

### 先定义公开 API 及其合法状态 {#exercise-01-surface}

内部函数的三个结果可投影如下：

| 内部结果 | 公开响应 | 必须成立的规律 |
|---|---|---|
| `Ok(code, remaining)` | `Accepted`、非 null 确认码、`RemainingSeats` 有值 | 错误和建议缺失 |
| `Error(message, Some seats)` | `Rejected`、错误非 null、`SuggestedSeats` 有值 | 确认码和剩余席位缺失 |
| `Error(message, None)` | `Rejected`、错误非 null、建议缺失 | 确认码和剩余席位缺失 |

公开类型可由 `BookingRequest`、`BookingResponse`、`BookingOutcome` 与 `BookingApi` 组成。响应构造函数必须是非公开的，否则 C# 可以组合任意枚举、null 和数值，重新引入内部联合已经排除的非法状态。请求可以公开构造，但构造器和入口都必须防卫其各自的契约。

核心仍返回联合；边界只做投影：

```fsharp:line-numbers [Library.fs]
module internal ResponseAdapter =
    let fromDecision decision =
        match decision with
        | Accepted(confirmationCode, remainingSeats) ->
            BookingResponse(BookingOutcome.Accepted, confirmationCode, Nullable remainingSeats, null, Nullable<int>())
        | Rejected(message, suggestedSeats) ->
            let suggestion =
                match suggestedSeats with
                | Some seats -> Nullable seats
                | None -> Nullable<int>()

            BookingResponse(BookingOutcome.Rejected, null, Nullable<int>(), message, suggestion)
```
公开入口验证跨边界参数，然后调用同一个核心：

```fsharp:line-numbers [Library.fs]
/// <summary>Provides the stable .NET entry point for booking decisions.</summary>
[<AbstractClass; Sealed>]
type BookingApi private () =
    /// <summary>Evaluates one request against the supplied available capacity.</summary>
    /// <param name="capacity">Available seats. Negative capacity is invalid configuration.</param>
    /// <param name="request">A non-null request to evaluate.</param>
    /// <returns>A response projected into ordinary .NET enum, class, string, and nullable-value members.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    static member Evaluate(capacity: int, request: BookingRequest) =
        ArgumentNullException.ThrowIfNull(request, nameof request)

        if capacity < 0 then
            raise (ArgumentOutOfRangeException(nameof capacity, capacity, "Capacity cannot be negative."))

        request |> Decision.evaluate capacity |> ResponseAdapter.fromDecision
```
可以另为 F# 调用者提供惯用 API，例如直接返回领域结果。但这个便利 API 不应定义 C# 契约；两套 API 都调用同一个内部函数。

## 练习 2：增加可选筛选而不破坏调用方 {#exercise-02}

### 用两个重载委托给一个实现 {#exercise-02-overloads}

内部实现仍可用 `option`，公开成员不需要：

```fsharp
open System
open System.Collections.Generic

[<AbstractClass; Sealed>]
type BookingSearch private () =
    static let validate name (value: string) =
        ArgumentNullException.ThrowIfNull(value, name)

        if String.IsNullOrWhiteSpace value then
            raise (ArgumentException("Value must not be blank.", name))

        value

    static let find requestId attendee =
        match attendee with
        | None -> [| requestId |] :> IReadOnlyList<string>
        | Some name -> [| $"{requestId}:{name}" |] :> IReadOnlyList<string>

    static member Find(requestId: string) : IReadOnlyList<string> =
        find (validate (nameof requestId) requestId) None

    static member Find(requestId: string, attendee: string) : IReadOnlyList<string> =
        let validRequestId = validate (nameof requestId) requestId
        let validAttendee = validate (nameof attendee) attendee
        find validRequestId (Some validAttendee)

assert (BookingSearch.Find("REQ-27") |> Seq.toList = [ "REQ-27" ])
assert (BookingSearch.Find("REQ-27", "Ada") |> Seq.toList = [ "REQ-27:Ada" ])
```

C# 调用方式保持自然：

```csharp
var all = BookingSearch.Find(requestId: "REQ-27");
var filtered = BookingSearch.Find(requestId: "REQ-27", attendee: "Ada");
```

`requestId` 和 `attendee` 现在进入 C# 命名实参，因此以后重命名会破坏重新编译的调用方源码。新增第二个参数的重载保留了第一个方法的二进制签名；直接把原方法改成两个参数则会破坏旧二进制。

### 让选项增长触发一次有意迁移 {#exercise-02-evolution}

第三个独立筛选不必立刻导致四个重载。若多个筛选项共同构成查询条件，可新增 `BookingSearchOptions` 和对应的 `Find` 方法。保留旧重载并转发到新实现。文档注明默认值与组合规则，再用 `Obsolete` 指明迁移目标，不要突然删除桥接成员。

即使只是新增重载，也要重新编译已有 C# 消费方；方法组、泛型推断和 null 实参都可能产生歧义。API 基线工具检查二进制兼容性，消费方编译检查源码兼容性。

## 练习 3：把 JSON DTO 与领域请求分开 {#exercise-03}

### 允许 DTO 不完整，再显式解码 {#exercise-03-dto}

`CLIMutable` DTO 诚实承认默认构造后的值尚未验证。私有领域记录只能经转换函数得到：

```fsharp
open System

[<CLIMutable>]
type BookingRequestDto =
    { RequestId: string | null
      Attendee: string | null
      Seats: int }

type DtoError =
    | MissingBody
    | MissingRequestId
    | MissingAttendee
    | InvalidSeats of int

type DomainRequest =
    private
        { RequestId: string
          Attendee: string
          Seats: int }

module DomainRequest =
    let ofDto (dto: BookingRequestDto | null) =
        match dto with
        | null -> Error MissingBody
        | value ->
            match value.RequestId with
            | null -> Error MissingRequestId
            | requestId when String.IsNullOrWhiteSpace requestId ->
                Error MissingRequestId
            | requestId ->
                match value.Attendee with
                | null -> Error MissingAttendee
                | attendee when String.IsNullOrWhiteSpace attendee ->
                    Error MissingAttendee
                | _ when value.Seats <= 0 -> Error(InvalidSeats value.Seats)
                | attendee ->
                    Ok
                        { RequestId = requestId
                          Attendee = attendee
                          Seats = value.Seats }

    let toDto (request: DomainRequest) : BookingRequestDto =
        { RequestId = request.RequestId
          Attendee = request.Attendee
          Seats = request.Seats }

let empty = Activator.CreateInstance<BookingRequestDto>()
assert (DomainRequest.ofDto empty = Error MissingRequestId)

let valid: BookingRequestDto =
    { RequestId = "REQ-27"
      Attendee = "Lin"
      Seats = 2 }

match DomainRequest.ofDto valid with
| Ok request -> assert (DomainRequest.toDto request = valid)
| Error error -> failwithf "unexpected DTO error: %A" error
```

生产解码器还可以累积多个字段错误，规范化文本，并把 JSON 路径加入错误上下文。重要的是只有 `ofDto` 了解默认 null/零状态；工作流接收 `DomainRequest`，不在每一步重复验证 DTO。

### 字段改名是传输格式迁移 {#exercise-03-compatibility}

把 JSON 的 `requestId` 改成 `id` 首先破坏传输格式兼容性：已存文档和旧客户端仍发送旧名称。若 DTO 同时是公开程序集类型，重命名属性还会影响源码和二进制兼容性，这正说明不应无意复用契约。

安全迁移可以在一段时间内读取两个名称、只写新名称，并用模式版本或明确弃用期限移除旧名称。适配器把两种输入都映射到同一个领域字段；领域 `RequestId` 不需要跟着序列化拼写变化。

## 答案复盘 {#solution-review}

- 先写公共状态规律，再选类、枚举、可空值和受控构造。
- 让公开成员与惯用的 F# API 共享内部实现，不要让二者互相实现业务规则。
- 重载可以保留旧签名，但仍需用真实源码检查解析歧义。
- 选项形成概念时，用命名 options 类型和转发桥收束重载。
- `CLIMutable` 属于确有构造要求的 DTO，不属于领域不变量本身。
- DTO 解码器接收不完整状态；领域工作流只接收验证后的类型。
- 程序集、行为和传输格式属于不同的兼容性层面，应分别测试和迁移。
