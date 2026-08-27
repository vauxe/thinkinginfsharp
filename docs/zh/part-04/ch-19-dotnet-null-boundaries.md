---
title: "第 19 章：.NET API 与空值边界"
description: "在 F# 中调用普通 .NET 构造函数、成员、重载与接口，再在单一边界把可空引用和值转换成能准确表达含义的领域类型。"
translationKey: part-04/ch-19-dotnet-null-boundaries
---

# 第 19 章：.NET API 与空值边界 {#overview}

F# 本身就是一门 .NET 语言。调用 `Uri`、`String.Join` 或 `IReadOnlyCollection<'T>` 都是普通的、有类型的 F# 代码。真正需要决定的是：外部 API 的构造规则、重载、异常与缺失值约定，应该在哪个边界转换成程序自己的模型。

真正执行 I/O 之前，先确定外部值的转换位置。下面先调用构造函数、成员、重载方法与接口，再区分三种常被统称为“可空”的表示：可空引用 `T | null`、可空值 `Nullable<T>` 和领域选择 `T option`。

## 把 .NET 调用读成有类型的表达式 {#dotnet-calls}

示例从不含 I/O 的代码开始：

```fsharp:line-numbers [NullBoundaries.fs]
let createAbsoluteUri (raw: string) : Uri = Uri(raw, UriKind.Absolute)

let uriHost (uri: Uri) : string = uri.Host

let joinLabels (labels: string array) : string = String.Join(" / ", labels)

let countItems (items: IReadOnlyCollection<'T>) : int = items.Count
```
每个定义都是普通函数，其结果都是值。大写的类型名与成员名遵循 .NET 约定；管道、局部绑定、模式匹配和领域类型仍然可以围绕它们使用。

### 构造与成员访问 {#construction-members}

`Uri(raw, UriKind.Absolute)` 会调用构造函数。构造类时可以省略 `new`，因此这里的 `new Uri(...)` 含义相同。构造函数实参放在圆括号中并以逗号分隔，与 .NET 方法调用语法一致。

`uri.Host` 读取实例属性。无形参 .NET 方法要使用圆括号，例如 `uri.ToString()`。属性和方法都可能执行计算；单凭语法不能保证纯净或廉价。应阅读 API 文档。

`Uri` 构造函数可能抛出异常来拒绝格式错误的输入。这个小包装器有意保留该行为。若格式错误的 URI 文本是预期领域结果，应使用 `Uri.TryCreate` 验证，或在专门的适配层翻译异常。第 21 章会讨论异常策略；这里不会把每项异常都悄悄变成 `None`。

### 重载选择服从已有类型 {#overloads}

`String.Join` 有多个重载。在 `joinLabels` 中，标注 `labels: string array` 和字符串分隔符共同选中接收字符串分隔符与字符串数组的重载。编译器不会依据你希望得到的返回类型选择重载；它使用静态可知的实参类型与上下文。

重载选择不清楚时，在调用处加入最少且准确的标注：

```fsharp
let joinLabels (labels: string array) : string =
    String.Join(" / ", labels)
```

不要为了让代码编译而随意添加转换。转换可能改变所选 API，并掩盖建模错误。应先检查重载签名，再写清实际实参的类型。

### 只要求实际需要的接口 {#interfaces}

`countItems` 只需要 `IReadOnlyCollection<'T>.Count`，所以它的形参声明该接口，而不是数组或具体列表：

```fsharp
let countItems (items: IReadOnlyCollection<'T>) : int =
    items.Count
```

数组实现了该接口。若上下文没有自动向上转换，应直接写出 `:>`：

```fsharp
let items = [| 1; 2; 3 |] :> IReadOnlyCollection<int>
countItems items
```

静态向上转换运算符 `:>` 由编译器检查，因此在运行时安全。向下转换运算符 `:?>` 用于另一种场景，并执行运行时检查。接收接口会缩小所需能力；纯净性、不可变性和线程安全仍是独立性质。

## “缺失”有三种不同表示 {#three-representations}

下面这些类型回答不同问题：

| 表示 | 它表示什么 | 运行时形式 | 典型用途 |
|---|---|---|---|
| `T | null` | 一个引用可能是空引用 | 引用本身可能为 null | 可空 .NET 标注与互操作 |
| `Nullable<T>` | 一个值类型可能没有值 | 带 `HasValue` 和 `Value` 的 `System.Nullable<T>` | 使用可空结构体的 .NET API |
| `T option` | 程序把缺失建模为 `None`，存在建模为 `Some value` | F# 可辨识联合 | F# 领域与工作流 API |

没有任何一种可以普遍替代另外两种。启用空值检查后，`T | null` 用于引用类型；`Nullable<T>` 要求值类型。`option` 可包装值类型或引用类型，并让调用方匹配领域缺失，但内部值的类型仍然重要。

## 可空引用是编译期契约 {#nullable-references}

### 选择启用，并标注真实输入 {#nullable-opt-in}

F# 可空引用检查需要选择启用。示例项目写明：

```xml
<Nullable>enable</Nullable>
```

启用检查后，`string` 表示编译器期待非空字符串，`string | null` 则允许 null。该标注不会在运行时包装值，也无法保证反射、旧版元数据、未检查代码、反序列化或其他语言遵守它。

应使用范围最窄且含义准确的类型。只有生产者确实可能提供 null 时，才把输入标为 `T | null`；不要“以防万一”而让每个内部引用都可空。转换之后，让核心通过构造保持非空。

### 用 `Null` 与 `NonNull` 收窄一次 {#null-narrowing}

适配器与错误类型来自可执行共享代码：

```fsharp:line-numbers [NullBoundaries.fs]
type BoundaryTextError =
    | MissingText
    | BlankText
```
```fsharp:line-numbers [NullBoundaries.fs]
let requireText (raw: string | null) : Result<string, BoundaryTextError> =
    match raw with
    | Null -> Error MissingText
    | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
    | NonNull value -> Ok(value.Trim())
```
`Null` 处理空引用。在每个 `NonNull value` 分支中，分析会把 `value` 收窄成非空 `string`，因此可以按声明类型调用 `Trim()`。空白是另一种无效情况，所以得到不同错误。

字面量 `null` 模式也能工作。需要给收窄后的非空值命名时，`Null`/`NonNull` 很方便。`NonNullQuick` 会在 null 上抛出 `NullReferenceException`；只有预期行为就是抛出时才使用，不要借它逃避空值处理。

### 当领域需要 option 时立即转换可空返回值 {#nullable-return}

`Type.GetType(name, throwOnError = false)` 是真实的 .NET API；找不到类型时，其返回值可能为 null。适配器只转换一次这种约定：

```fsharp:line-numbers [NullBoundaries.fs]
let tryResolveType (typeName: string) : Type option =
    Type.GetType(typeName, throwOnError = false) |> Option.ofObj
```
`Option.ofObj` 把 null 映射成 `None`，把非空引用映射成 `Some value`。下游 F# 代码现在看到的是 `Type option`，而不是必须在各处重复检查的可空引用。

实参 `throwOnError = false` 表示“找不到类型时返回 null”；.NET 文档还列出其他可能抛出的条件。这里用 `option` 表达普通缺失，同时保留表示其他失败原因的异常。

## `Nullable<T>` 是可空值类型 {#nullable-values}

### 读取 `Value` 前先检查是否存在 {#nullable-inspection}

`Nullable<int>` 就是 `System.Nullable<int>`，它是可以表示整数缺失或存在的结构体。它不写作 `int | null`，因为后一语法针对可空引用。其基本成员是：

```fsharp
let absent = Nullable<int>()
let present = Nullable 4

absent.HasValue   // false
present.HasValue  // true
present.Value     // 4
```

当 `HasValue` 为 false 时读取 `Value` 会抛出 `InvalidOperationException`。应先检查；只有当该默认值确实符合调用方含义时才使用 `GetValueOrDefault`；或者转换成 F# 表示。

### 在 .NET 接口处转换，不要散布到核心 {#nullable-value-conversion}

FSharp.Core 提供了具名转换：

```fsharp:line-numbers [NullBoundaries.fs]
let nullableIntToOption (value: Nullable<int>) : int option = Option.ofNullable value

let optionToNullableInt (value: int option) : Nullable<int> = Option.toNullable value
```
`Option.ofNullable` 把没有值的可空值映射成 `None`；`Option.toNullable` 把 `None` 映射回空的 `Nullable<T>`。存在值时，两者都会保留其中的值。这些函数要求适当的值类型。

外部成员明确要求或返回 `Nullable<T>` 时，就在调用处保留它。缺失进入 F# 模型后，优先使用 `option`。核心内部反复转换，说明互操作逻辑已经泄漏得太深。

## `option` 不保证内部值绝不为 null {#option-boundary}

### 引用转换使用另一组函数 {#reference-conversion}

可空引用使用 `Option.ofObj` 与 `Option.toObj`，而不是 `Nullable<T>` 转换：

```fsharp:line-numbers [NullBoundaries.fs]
let nullableTextToOption (value: string | null) : string option = Option.ofObj value

let optionToNullableText (value: string option) : string | null = Option.toObj value
```
适配器代码应该让方向清晰可见：

- 传入可空引用：用 `Option.ofObj` 从 `T | null` 得到 `T option`；
- 传出可选领域值：当 .NET API 要求 null 时，用 `Option.toObj` 从 `T option` 得到 `T | null`；
- 可空值类型：用 `ofNullable` 与 `toNullable` 在 `Nullable<T>` 和 `T option` 之间转换。

不要把 `defaultArg optionValue null` 当作含糊的替代品。启用空值检查后，它通常会削弱类型，意图也不如专门的互操作转换明确。

### `Some null` 是真实反例 {#some-null}

option 只说明值是 `None` 还是 `Some`；它不会验证内部值。如果内部类型允许 null，下面的值合法：

```fsharp:line-numbers [NullBoundaries.fs]
let someNullText: (string | null) option = Some null
```
该值是 `Some`，内部值却为 null。旧版或未检查的 .NET 代码同样可能违反假设。因此，准确规则是：

> 用 `None` 表示领域缺失，让普通 option 的内部类型保持非空，并在适配器中规范化外部 null。

不要宣称 `option` 的运行时表示让 null 不可能出现。启用空值检查后，`string option` 要求内部字符串非空；`(string | null) option` 则有意允许这个反例。类型会区分两种意图，适配器测试则检查外部输入。

## 把转换集中在核心之外 {#boundary-placement}

一种实用流程是：

```text
.NET 构造函数/成员/重载/接口
                    ↓ 检查其声明契约
       适配器中的 T | null 或 Nullable<T>
                    ↓ 只转换和验证一次
       option / Result / 受保护领域类型
                    ↓
              纯 F# 核心与工作流
```

这不是要求建立大型抽象层。像 `tryResolveType` 这样的两行函数就可以是完整适配器。目的在于防止外部缺失值规则泄漏到每个函数签名中。

### 紧凑的转换决策表 {#decision-table}

| 情况 | 保留或转换为 | 原因 |
|---|---|---|
| .NET 引用形参确实接受 null | 在该调用边界使用 `T | null` | 符合外部契约 |
| .NET 引用返回值可能为 null，且缺失很正常 | 转成 `T option` | 让下游显式处理缺失 |
| .NET 值成员使用 `Nullable<T>` | 在那里保留；进入核心时用 `Option.ofNullable` 转换 | 在互操作处保留真实运行时表示 |
| F# 领域字段可能缺失 | `T option` | 在模型中命名缺失 |
| 输入缺失本身就是验证失败 | `Result<T, Error>` | 保留构造失败原因 |
| API 用 null 表示失败，但也可能抛出异常 | 用 `option` 表示缺失；单独保留或翻译有文档的异常 | 缺失与失败是不同事实 |
| API 要求在输出时传 null | 在最终调用处用 `Option.toObj` 转换 | 让中间领域代码不含 null |

应先依据生产者文档中的真实行为，再考虑消费者的领域含义。习惯不是类型设计规则。

### 保留原因，不要合并成同一种结果 {#failure-causes}

空值检查会在编译期阻止一部分意外解引用。空白验证、文本解析、URI 接受规则和服务可用性是不同问题，应各自设计检查与表示。

普通缺失无需解释时使用 `option`；调用方需要原因时使用 `Result`。让意外异常保留诊断信息，直到某一层有足够上下文进行翻译。第 20 与 21 章会加入副作用及异常、资源策略，但不会改变这里的空值模型。

## 构建已检查的示例 {#run-tests}

在仓库根目录运行：

```console
dotnet build examples/chapters/ch19/Ch19.fsproj --configuration Release
```

该项目启用空值检查。源码覆盖普通构造函数、成员、重载和接口调用，null 输入收窄，`Type.GetType` 的可空返回，`Nullable<int>` 与可空引用的双向转换，以及 `Some null` 反例。

这个夹具只覆盖此处展示的 API，并不代表每个 .NET 库。实际调用 API 时，始终要检查目标框架当前的标注与文档。

## 练习 {#exercises}

### 练习 1：区分缺失表示 {#exercise-01}

对于下面每个值，选择 F# 核心消费它时应使用的 `T | null`、`Nullable<T>`、`T option` 或 `Result<T, Error>`。同时解释生产者契约与领域含义：

1. C# 属性 `DateTimeOffset? LastSeen`；
2. 可空引用返回值 `Customer? Find(string id)`，其中“未找到”很正常；
3. 新 F# 领域记录中的可选中间名；
4. 可能以 null 或空白到达的必填参与者文本；
5. 用 null 表示“未找到”、却会对格式错误类型名抛出异常的 API。

说明每项转换在哪里发生，以及哪些失败必须保持区分。

### 练习 2：包装一个真实可空 API {#exercise-02}

围绕 `Type.GetType(typeName, throwOnError = false)` 编写 `tryResolveType`。其公开返回类型必须为 `Type option`。测试一个已知核心类型和一个不存在的类型。

然后编写有意不同的 `resolveType`，让它返回 `Result<Type, ResolveTypeError>`，并让缺失类型携带所请求的名称。解释为什么捕获所有可能异常并返回同一个错误会丢失信息。

### 练习 3：检查 option 不变量 {#exercise-03}

给定下面的值：

```fsharp
let suspicious : (string | null) option = Some null
```

证明 `Option.isSome suspicious` 为 true，而内部值为 null。编写一个把 `string | null` 转成 `string option` 的适配函数，再编写另一个把 null 与空白拒绝成不同 `Result` 错误的函数。

解释哪个函数适合普通缺失，哪个适合必填且需要验证的输入。不要使用 `Unchecked`，也不要用笼统的异常捕获。

[阅读本章答案](../solutions/ch-19-dotnet-null-boundaries)。

下一章仍把转换留在核心外，并把时间、随机数和环境访问变成可见依赖，而不是隐藏输入。

## 资料来源 {#sources}

- [Microsoft Learn：F# 构造函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn：F# 方法与重载](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/methods)
- [Microsoft Learn：F# 接口](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn：F# 空值与空值检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn：F# 可空值类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [FSharp.Core：`Option` 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [Microsoft Learn：`Type.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.type.gettype?view=net-10.0)
