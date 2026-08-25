---
title: "第 19 章答案"
description: "分类可空边界，在不抹掉失败的前提下包装真实 .NET 可空返回，并证明 option 载荷为何仍可能为 null。"
translationKey: solutions/ch-19-dotnet-null-boundaries
---

# 第 19 章答案 {#overview}

在适配器中使用外部生产者的真实表示，再只转换一次，得到 F# 核心所需的含义。空值标注、领域缺失、无效输入和异常失败并不是同义词。

[返回第 19 章](../part-04/ch-19-dotnet-null-boundaries)。

## 练习 1：分类边界表示 {#exercise-01}

### 从边界两侧共同选择 {#exercise-01-classification}

| 输入 | 适配器表示 | 核心表示 | 原因 |
|---|---|---|---|
| C# `DateTimeOffset? LastSeen` | `Nullable<DateTimeOffset>` | 当领域允许缺失时使用 `DateTimeOffset option` | `DateTimeOffset` 是值类型；C# 可空值语法编译成 `Nullable<T>` |
| 带正常“未找到”的 `Customer? Find(string id)` | `Customer | null` | `Customer option` | API 使用可空引用；核心需要普通缺失 |
| 在 F# 内创建的可选中间名 | 不需要外部可空表示 | `MiddleName option` | 缺失直接属于领域模型 |
| 以 null 或空白到达的必填参与者文本 | `string | null` | `Result<AttendeeName, AttendeeNameError>` | 缺失与空白是无效构造事实，不是有效的可选状态 |
| 用 null 表示类型缺失、用异常表示格式错误输入 | `Type | null` 加上有文档的异常行为 | 普通缺失用 `Type option`，其他失败保留或有意翻译 | 返回 null 与抛出异常传达不同结果 |

核心类型还可以比表中更强。例如，`AttendeeName` 可以使用私有表示，让它只能由经过验证的非空白文本构造。

### 每项转换只放置一次 {#exercise-01-flow}

```text
C# DateTimeOffset? ── Option.ofNullable ──▶ DateTimeOffset option
Customer? 返回值 ──── Option.ofObj ────────▶ Customer option
原始必填文本 ──────── Null/NonNull + 检查 ─▶ Result<AttendeeName, Error>
Type.GetType 返回值 ─ Option.ofObj ────────▶ Type option
```

若后续传出 .NET 调用需要 `DateTimeOffset?` 或可空 `Customer`，应在该调用前立即用 `Option.toNullable` 或 `Option.toObj` 转换回去。不要让每个中间函数同时理解两种表示。

格式错误类型名异常不能变成“未找到”。要么让它传播到异常边界，要么只把有文档的异常用例翻译成不同错误联合用例。这样调用方才能区分缺失、无效输入与基础设施故障。

## 练习 2：包装一个真实可空 API {#exercise-02}

### 用 option 保留普通缺失 {#exercise-02-option}

最小包装器就是本章项目使用的代码：

```fsharp
open System

let tryResolveType (typeName: string) : Type option =
    Type.GetType(typeName, throwOnError = false)
    |> Option.ofObj

assert (tryResolveType "System.String" = Some typeof<string>)
assert (tryResolveType "Example.TypeThatDoesNotExist" = None)
```

`Option.ofObj` 只表达返回值的 null/非 null 分支。它不会捕获异常。这反而是优点：意外的加载器或解析器故障不会被错误标成普通缺失。

### 在错误中保留请求名称 {#exercise-02-result}

当调用方需要类型缺失的解释时，应显式改变领域契约：

```fsharp
open System

type ResolveTypeError =
    | TypeNotFound of requestedName: string

let resolveType (typeName: string) : Result<Type, ResolveTypeError> =
    match Type.GetType(typeName, throwOnError = false) with
    | Null -> Error(TypeNotFound typeName)
    | NonNull resolved -> Ok resolved

assert (resolveType "System.String" = Ok typeof<string>)

assert (
    resolveType "Example.TypeThatDoesNotExist" =
        Error(TypeNotFound "Example.TypeThatDoesNotExist")
)
```

这段代码仍然不会捕获所有异常。如果某个应用对有文档的 `ArgumentException` 或加载器故障制定了策略，应增加专门的错误用例，并只在适配器捕获该条件。笼统的 `with _ -> TypeNotFound typeName` 会丢掉堆栈、异常种类和运行原因，同时让返回错误变得不符合事实。

## 练习 3：审计 option 不变量 {#exercise-03}

### 证明反例 {#exercise-03-counterexample}

```fsharp
let suspicious : (string | null) option = Some null

let isSome, payloadIsNull =
    match suspicious with
    | None -> false, false
    | Some payload ->
        match payload with
        | Null -> true, true
        | NonNull _ -> true, false

assert isSome
assert payloadIsNull
```

外层联合用例以 `Some` 记录存在；载荷类型则独立地允许 null。因此，只检查 `Option.isSome` 不能为该类型建立载荷非空事实。

### 为普通缺失与无效输入提供不同 API {#exercise-03-boundaries}

```fsharp
open System

type RequiredTextError =
    | MissingText
    | BlankText

let optionalText (raw: string | null) : string option =
    Option.ofObj raw

let requiredText (raw: string | null) : Result<string, RequiredTextError> =
    match raw with
    | Null -> Error MissingText
    | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
    | NonNull value -> Ok(value.Trim())

assert (optionalText null = None)
assert (optionalText "" = Some "")
assert (requiredText null = Error MissingText)
assert (requiredText "" = Error BlankText)
assert (requiredText " F# " = Ok "F#")
```

`optionalText` 表示 null 是普通缺失；它有意把空字符串保留为存在的载荷。`requiredText` 表示必须得到可用值，并区分两种失败原因。两个 API 都没有使用未检查转换，也没有捕获无关异常。

在更大的领域边界中，应返回受保护的 `RequiredText` 类型，而不是普通字符串。转换策略保持不变：先规范化外部 null，再只在验证成功后构造领域值。

## 应注意什么 {#what-to-notice}

- 可空引用与可空值需要不同的 FSharp.Core 转换对。
- 应先根据生产者表示进行转换，再选择核心的领域表示。
- 返回 option 的包装器不应抹掉代表非缺失含义的异常。
- `Some` 描述 option 用例，而不是对每个载荷独立强制的不变量。
- 一个小型边界函数就能让 null 离开核心，无须发明大型适配器框架。
