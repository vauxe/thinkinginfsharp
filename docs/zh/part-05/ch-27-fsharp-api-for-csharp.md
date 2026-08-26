---
title: "第 27 章：为 C# 设计 F# API"
description: "从 C# 调用方式设计稳定的 .NET 公共 API，同时让联合、option、纯函数与领域不变量留在 F# 内部。"
translationKey: part-05/ch-27-fsharp-api-for-csharp
---

# 第 27 章：为 C# 设计 F# API {#overview}

F# 和 C# 共享 CLR、程序集与大部分基础类型。两种语言的惯用表达经过程序集边界后，会呈现不同形状：

| F# 源码形式 | C# 看到的 API 形式 |
|---|---|
| `Result<_,_>` | `FSharpResult` |
| 可辨识联合 | 联合案例类型与辅助成员 |
| `option` | `FSharpOption` |
| 柯里化函数 | `FSharpFunc` |
| `Async<_>` | `FSharpAsync` |

这些都是有效的 CLR 形状。一旦公开，它们的表示就会进入调用方代码与版本兼容契约。

稳定的库因此维护两套含义准确的词汇：内部使用最能表达领域的 F# 类型，程序集边界提供调用方熟悉的 .NET 契约。两者之间的适配代码保持小巧、明确且可测试。

## 学完本章你将能够 {#outcomes}

学完本章后，你应该能够：

- 从真实 C# 调用点评审 F# 编译后的公共 API；
- 决定记录、联合、`option`、函数、异步、集合与元组是否需要投影；
- 按 .NET 约定设计命名空间、类型、属性、方法、参数和重载；
- 同时发布可空元数据并执行运行时参数检查；
- 区分调用者错误、预期业务拒绝与系统故障；
- 生成并验证公开 API 的 XML 文档；
- 把领域模型、C# 公共模型和 JSON/数据库模型分开演进；
- 识别源码、二进制、行为与传输格式兼容性风险。

## 先写调用代码，再设计公共 API {#consumer-first}

先写最小 C# 契约客户端。它能暴露 API 是否要求调用者理解 F#，也能把命名参数、可空性、构造方式和返回形状变成编译证据：

```csharp:line-numbers [Program.cs]
var accepted = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-27", attendee: "Lin", seats: 2));

Require(accepted.Outcome == BookingOutcome.Accepted, "accepted outcome");
Require(default(BookingOutcome) == BookingOutcome.None, "valid enum zero value");
Require(accepted.IsAccepted, "accepted flag");
Require(accepted.ConfirmationCode == "CONF-REQ-27", "confirmation code");
Require(accepted.RemainingSeats == 3, "remaining seats");
Require(accepted.ErrorMessage is null, "accepted error must be null");
Require(accepted.SuggestedSeats is null, "accepted suggestion must be null");

Console.WriteLine(
    $"Accepted: outcome={accepted.Outcome} code={accepted.ConfirmationCode} remaining={accepted.RemainingSeats}");
```
这个调用只出现普通命名空间、枚举、密封类、构造函数、静态方法、属性、`string?` 与 `int?`。C# 调用者无需知道内部存在联合和 `option`。命名参数也说明 `capacity`、`request`、`requestId` 等参数名是可被源码依赖的契约，不只是实现注释。

“C# 能调用”只是最低门槛。还应询问：IDE 是否给出自然补全？可空分析是否准确？错误是否能按预期分支？API 升级后，旧二进制能否继续运行？

## 一个含义，三个使用边界 {#three-surfaces}

同一个预约请求会经过三个面向不同用途的边界，但业务规则不能因此复制三份：

| 边界 | 为谁优化 | 合适的形式 | 不应负责 |
|---|---|---|---|
| F# 领域核心 | 领域推理与穷尽匹配 | 私有联合、记录、`option`、`Result`、纯函数 | C# 便利性、序列化构造规则 |
| .NET 公共 API | C#、VB 与反射工具 | 命名空间、类、枚举、成员、可空标注、Task、委托 | JSON 字段名、ORM 布局 |
| 传输格式/存储 DTO | JSON、消息或数据库适配器 | 显式字段、版本与序列化属性 | 领域不变量的唯一实现 |

规则只在领域核心中决定。公共 API 和 DTO 解码输入、调用核心，再投影结果。它们可以拥有不同形状与发布节奏，因为程序集签名、JSON 模式和数据库模式是不同契约。

### 让联合留在核心 {#internal-union}

样例用封闭联合精确表示两种领域结果；建议席位只在拒绝时存在：

```fsharp:line-numbers [Library.fs]
type internal Decision =
    | Accepted of confirmationCode: string * remainingSeats: int
    | Rejected of message: string * suggestedSeats: int option

module internal Decision =
    let evaluate capacity (request: BookingRequest) =
        if String.IsNullOrWhiteSpace request.RequestId then
            Rejected("request id must not be blank", None)
        elif String.IsNullOrWhiteSpace request.Attendee then
            Rejected("attendee must not be blank", None)
        elif request.Seats <= 0 then
            Rejected("seat count must be positive", None)
        elif request.Seats > capacity then
            let suggestion = if capacity > 0 then Some capacity else None

            Rejected($"requested {request.Seats} exceeds available {capacity}", suggestion)
        else
            let normalizedRequestId = request.RequestId.Trim().ToUpperInvariant()
            Accepted($"CONF-{normalizedRequestId}", capacity - request.Seats)
```
这里的模式匹配仍然穷尽，非法组合不会进入核心。`internal` 防止 C# 或另一个程序集依赖案例的编译表示，也给库作者留下增加内部案例或改变载荷的空间。

### 在边界投影一次 {#boundary-projection}

下面是常见的跨语言投影，而不是机械的一一替换：

| 内部 F# 表示 | 常见 .NET 公共表示 | 选择依据 |
|---|---|---|
| 私有 DU / `Result<'T,'E>` | 封闭响应类加状态枚举，或预期成功值加异常 | 调用者需要怎样分支，失败是否预期 |
| `'T option` 返回值 | 可空引用、`Nullable<T>`，或 `TryX(..., out T)` | 缺失含义与值/引用类别 |
| `'T option` 参数 | 清楚的重载，偶尔为有明确 null 语义的可空参数 | 避免要求 C# 构造 `FSharpOption<T>` |
| `'T -> 'U` | `Func<T,U>`、`Action<T>` 或有名称的委托 | C# lambda 与工具支持 |
| `Async<'T>` | `Task<T>`，通常接收 `CancellationToken` | .NET 异步约定 |
| F# `list`/`Map`/`Set` | 与语义匹配的 .NET 集合接口 | 枚举、索引、查找与可变性契约 |
| 有领域含义的元组 | 有名称的结果类型 | 让字段含义和演进位置稳定 |

公开请求使用普通构造函数和只读属性：

```fsharp:line-numbers [Library.fs]
/// <summary>Identifies whether a booking request was accepted or rejected.</summary>
type BookingOutcome =
    /// <summary>No booking outcome has been assigned.</summary>
    | None = 0
    /// <summary>The booking was accepted and has a confirmation code.</summary>
    | Accepted = 1
    /// <summary>The booking was rejected and has an error message.</summary>
    | Rejected = 2

/// <summary>Input supplied by a .NET caller when evaluating a booking.</summary>
/// <param name="requestId">A non-null request identifier. Blank identifiers are rejected by <c>Evaluate</c>.</param>
/// <param name="attendee">A non-null attendee name. Blank names are rejected by <c>Evaluate</c>.</param>
/// <param name="seats">The number of seats requested.</param>
/// <exception cref="System.ArgumentNullException"><paramref name="requestId"/> or <paramref name="attendee"/> is <see langword="null"/>.</exception>
[<Sealed>]
type BookingRequest(requestId: string, attendee: string, seats: int) =
    do
        ArgumentNullException.ThrowIfNull(requestId, nameof requestId)
        ArgumentNullException.ThrowIfNull(attendee, nameof attendee)

    /// <summary>Gets the request identifier exactly as supplied.</summary>
    member _.RequestId = requestId

    /// <summary>Gets the attendee name exactly as supplied.</summary>
    member _.Attendee = attendee

    /// <summary>Gets the requested seat count.</summary>
    member _.Seats = seats
```
公开响应把引用缺失投影为可空 `string`，把值缺失投影为 `Nullable<int>`。构造函数是程序集内部的，因此调用者不能制造“接受但没有确认码”的响应：

```fsharp:line-numbers [Library.fs]
/// <summary>A C#-friendly projection of the internal F# booking decision.</summary>
/// <remarks>
/// Accepted responses have a confirmation code and remaining-seat count.
/// Rejected responses have an error message and may have a suggested seat count.
/// </remarks>
[<Sealed>]
type BookingResponse
    internal
    (
        outcome: BookingOutcome,
        confirmationCode: string | null,
        remainingSeats: Nullable<int>,
        errorMessage: string | null,
        suggestedSeats: Nullable<int>
    ) =
    /// <summary>Gets the accepted or rejected outcome.</summary>
    member _.Outcome = outcome

    /// <summary>Gets whether this response represents an accepted booking.</summary>
    member _.IsAccepted = outcome = BookingOutcome.Accepted

    /// <summary>Gets the confirmation code, or <see langword="null"/> when rejected.</summary>
    member _.ConfirmationCode = confirmationCode

    /// <summary>Gets remaining capacity, or <see langword="null"/> when rejected.</summary>
    member _.RemainingSeats = remainingSeats

    /// <summary>Gets the rejection message, or <see langword="null"/> when accepted.</summary>
    member _.ErrorMessage = errorMessage

    /// <summary>Gets a capacity-based suggestion when available; otherwise <see langword="null"/>.</summary>
    member _.SuggestedSeats = suggestedSeats
```
适配器是唯一了解两种表示的地方：

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
即使公开签名不含 `Microsoft.FSharp.*`，F# 编译的程序集通常仍在运行时依赖 `FSharp.Core`。目标是消除调用方的 F# 表示知识，不是假装实现不由 F# 编写；正常的项目或 NuGet 依赖解析会传递运行时依赖。

## 把公共成员设计成 .NET API {#dotnet-shape}

面向普通 .NET 语言的 API 优先使用命名空间、类型和成员；实现函数可以留在非公开模块。样例用抽象且密封、构造函数私有的类型承载一组静态操作：

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
这不是要求每个 F# 模块都改成类。只有跨语言公共边界需要改成调用方熟悉的形式；面向 F# 的 API 仍可自然地公开模块、函数和联合。

### 名称也是兼容性契约 {#names}

公共命名空间、类型、方法和属性使用 `PascalCase`，参数使用 `camelCase`；布尔属性采用肯定式 `IsAccepted` 或 `CanRetry`。避免只靠大小写区分名称，也不要把内部缩写传播给所有调用者。

F# 成员用括号内的元组式参数声明，编译成普通多参数 CLI 方法，所以 C# 得到 `Evaluate(int capacity, BookingRequest request)`。参数名值得认真选择：C# 命名实参会把它写进源码，重命名会破坏该源码，即使二进制签名仍相同。

`[<CompiledName>]` 能给编译后的值或函数另一个名称，适合确实需要 F# 与 CLI 两套惯用名称的小型 API。它不是修补混乱命名的默认工具；先让公共词汇本身一致，并用 C# 编译器查看最终形式。

### 属性、方法与重载表达不同承诺 {#members-overloads}

属性适合便宜、稳定、像状态观察的值；会执行工作、接收参数、可能耗时或有明显失败的操作应是方法。不要把网络或磁盘 I/O 藏进看似字段读取的属性。

可选行为通常用重载表示，例如同时提供 `Find(requestId)` 与 `Find(requestId, attendee)`，两者委托给同一实现。按参数数量重载通常比按相近类型重载更清楚。不要仅为未来可能性预先制造组合爆炸；当选项开始成组增长时，改用有名称的 options 类型。

公开回调使用 `Func`、`Action` 或领域委托；公开异步方法返回 `Task`/`Task<T>`，按协议接收 `CancellationToken`。内部仍可立即把委托转成 F# 函数、把任务工作流映射回领域操作。

### 集合与元组必须保留语义 {#collections-tuples}

不要只把 `list<'T>` 换成 `IEnumerable<T>` 就宣称完成设计。若结果是单次流，`IEnumerable<T>` 合适；若承诺稳定索引和计数，可选 `IReadOnlyList<T>`；键查找则使用相应字典接口。仍需说明它是实时视图还是快照，因为只读接口不保证背后不可变。

两个短暂且无领域名称的结果偶尔可用元组；如果 `Item1`、`Item2` 会让 C# 调用者猜含义，或未来可能增加成员，就返回有名称的类型。边界处增加少量固定代码，可以换来更清楚的契约。

## 正确表达 null、缺失与失败 {#absence-failure}

边界设计首先区分三件事：调用者违反参数契约、业务上可预期的拒绝，以及基础设施故障。把它们全部编码为 `null`、全部抛异常或全部塞进一个字符串都会丢失信息。

### 可空标注不能代替运行时防卫 {#null-contract}

F# 9 及以上启用 nullable 检查后，`string` 与 `string | null` 表达不同静态契约。对非 null 的公开输入，样例既发出 `NotNull` 元数据，又在入口调用 `ArgumentNullException.ThrowIfNull`；因为未启用分析的 C#、反射和其他运行时调用仍能传入 null。

预期缺失的引用输出使用 `string | null`，预期缺失的值输出使用 `Nullable<int>`。`Nullable<T>` 只适用于值类型；F# 中没有值时构造 `Nullable<T>()`，C# 将其视为 `T?` 且 `is null` 为真。

C# 契约客户端通过反射检查这些承诺，并检查公开签名没有泄露 F# 专用类型：

```csharp:line-numbers [Program.cs]
var publicTypes = typeof(BookingApi).Assembly.GetExportedTypes();

var publicTypeNames = publicTypes
    .Select(type => type.Name)
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();

var expectedPublicTypes = new[]
{
    nameof(BookingApi),
    nameof(BookingOutcome),
    nameof(BookingRequest),
    nameof(BookingResponse)
};

Require(publicTypeNames.SequenceEqual(expectedPublicTypes), "minimal public type surface");
Require(typeof(BookingResponse).GetConstructors().Length == 0, "response construction is controlled");
Require(
    !publicTypes.SelectMany(GetPublicSignatureTypes).Any(ContainsFSharpSpecificType),
    "no F#-specific type leaks through public signatures");
Console.WriteLine($"Public types: {string.Join(",", publicTypeNames)}");

var nullability = new NullabilityInfoContext();
var requestIdParameter = typeof(BookingRequest).GetConstructors().Single().GetParameters()[0];
var confirmationProperty = typeof(BookingResponse).GetProperty(nameof(BookingResponse.ConfirmationCode))!;
var requestIdState = nullability.Create(requestIdParameter).ReadState;
var confirmationState = nullability.Create(confirmationProperty).ReadState;

Require(requestIdState == NullabilityState.NotNull, "requestId nullable metadata");
Require(confirmationState == NullabilityState.Nullable, "confirmation nullable metadata");
Console.WriteLine(
    $"Nullability: request-id={requestIdState} confirmation={confirmationState}");

var documentationPath = Path.ChangeExtension(typeof(BookingApi).Assembly.Location, ".xml");
Require(File.Exists(documentationPath), "XML documentation sidecar");
var documentation = File.ReadAllText(documentationPath);
Require(documentation.Contains("BookingApi.Evaluate", StringComparison.Ordinal), "Evaluate XML documentation");
Console.WriteLine("XML docs: evaluate=true");
```
反射测试是元数据证据，不替代真实调用。样例同时编译并运行接受、拒绝、无效值、null 输入和范围错误路径。

### 枚举需要合法的零值和未知值策略 {#enum-contract}

CLR 枚举的默认值是零，任意底层整数也能被转换成枚举。`BookingOutcome.None = 0` 因而让默认值有名称；库本身只由受控构造器产生 `Accepted` 或 `Rejected`。若枚举来自不可信输入，仍应验证已定义值或在 `switch` 中保留默认分支，不能把类型声明误当成运行时封闭集合。

枚举适合稳定、无载荷的粗粒度标签，不等同于可辨识联合。案例各有不同数据时，让枚举选择响应解释，而不要建立多个互相矛盾的公开布尔标志。

### 预期拒绝是数据，契约错误是异常 {#error-policy}

样例把座位不足和业务字段无效返回为 `BookingResponse`，因为调用者预计会展示或处理它们。null 请求和负容量违反 API/配置契约，因此抛 `ArgumentNullException` 或 `ArgumentOutOfRangeException`。意外 I/O、取消和程序错误继续遵循相应 .NET 异常/Task 规则。

这不是普遍规定某个错误永远属于哪类。关键是先按调用者能采取的动作分类，再让 F# 核心使用 `Result` 或联合表达预期分支，让公共适配器投影为清楚、稳定的 .NET 结果。

## XML 文档也是公共 API 的一部分 {#xml-documentation}

所有公开类型、构造函数、属性与方法都应有简短 XML 文档；参数防卫还应记录异常条件。`<summary>`、`<remarks>`、`<param>`、`<returns>` 与 `<exception>` 会进入 IDE 和文档工具。

样例打开 `GenerateDocumentationFile`，并把 F# 警告 3390 加入构建，用来发现 XML 格式和参数名错误：

```xml:line-numbers [FSharpApi.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>ThinkingInFSharp.Ch27.FSharpApi</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarnOn>$(WarnOn);3390</WarnOn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Library.fs" />
  </ItemGroup>
</Project>
```
客户端还断言程序集同目录下存在 XML 文档文件，并且包含 `BookingApi.Evaluate`。这不会判断文字是否清楚，但能确保注释确实随程序集生成。API 稳定后可再引入 `.fsi` 文件，把公共签名与文档集中成可审阅清单。

## 不要让 JSON 或数据库反向设计领域 {#wire-boundary}

序列化器可能偏好公开无参构造函数、可写属性、特定字段名或属性标注。`[<CLIMutable>]` 会为 F# 记录生成默认构造函数及属性 getter/setter；这正适合确有此要求的边界 DTO，却也允许先产生 null、零和部分初始化状态。

因此不要为了一个序列化器给领域记录随手添加 `[<CLIMutable>]`。建立专用 DTO，把可空和默认值视为未验证输入，再通过智能构造函数或解码器转成领域类型。反向投影同样集中在适配器。这样 JSON 字段重命名、版本兼容或 ORM 要求不会迫使领域联合和不变量一起改变。

C# 公共类型也不应自动成为 JSON 模式。进程内调用者、跨网络消费者和持久数据拥有不同兼容期限；只有确认它们确实是同一契约时才复用表示。

## 兼容性不只有“还能编译” {#compatibility}

发布过的公共 API 至少有四类兼容性：

| 类别 | 失败时间 | 例子 |
|---|---|---|
| 源码兼容性 | 调用者重新编译时 | 重命名参数破坏命名实参；新增重载造成解析歧义 |
| 二进制兼容性 | 旧二进制加载或调用时 | 给现有方法增加参数、删除成员或改变签名导致 `MissingMethodException` 等 |
| 行为兼容性 | 程序运行时 | 从返回拒绝改为抛异常；改变比较、顺序或默认值 |
| 传输格式兼容性 | 读取消息/存储数据时 | 改 JSON 字段名、枚举编码或必需字段 |

“只是增加”也未必安全：新重载可能让旧源码的方法组或 `null` 调用变得歧义；给接口增加成员会破坏已有实现者；给公开联合增加案例会使 F# 调用者原先穷尽的匹配不再覆盖；改变可空标注可能给重新编译者新增警告或错误。

优先新增成员或重载，把旧成员保留为转发桥；需要迁移时用 `[<Obsolete("Use Evaluate(...)")>]` 提供明确替代和期限。不要在原位改签名来“简化”API。跨主要版本也应记录行为和传输格式迁移，而不只依赖语义版本号。

让 C# 契约客户端进入 CI，并保存已发布程序集或包作为 API 基线。NuGet 包可以启用 package validation 和 baseline version；也可用 `Microsoft.DotNet.ApiCompat.Tool` 比较程序集。工具能发现许多签名差异，行为与序列化兼容仍需针对性测试。

## 运行共享契约样例 {#run-example}

从示例所在目录构建并运行真实 C# 调用方：

```console
dotnet build CSharpClient.csproj --configuration Release --no-restore
dotnet run --project CSharpClient.csproj --configuration Release --no-build
```

客户端断言业务结果、参数防卫、四个导出类型、公开签名、可空元数据和 XML 文档，而不只打印演示输出。修改公开 API 后，先让这个消费者重新编译，再运行已有二进制兼容性与行为测试。

## 练习 {#exercises}

### 练习 1：封装泄露的 F# 表示 {#exercise-01}

一个库公开 `decide : int -> BookingRequest -> Result<(string * int), string * int option>`。为 C# 调用者设计公共类型和方法，但保留这个函数作为内部核心。写出成功、拒绝与缺失建议的对应关系，并说明哪些构造必须受控。

### 练习 2：增加可选筛选而不破坏调用方 {#exercise-02}

已有 `BookingSearch.Find(string requestId)`。增加按 attendee 筛选的能力，不公开 `string option`。给出 F# 成员声明和两种 C# 调用；说明参数命名、重载歧义以及选项继续增长时的迁移策略。

### 练习 3：把 JSON DTO 与领域请求分开 {#exercise-03}

假设序列化器要求无参构造和可写属性。设计一个允许未验证输入的 DTO、一个返回结构化错误的领域转换，以及反向投影位置。分类一次 JSON 字段改名会影响哪种兼容性，并说明为何 DTO 规则不应进入领域类型。

[阅读本章练习答案](../solutions/ch-27-fsharp-api-for-csharp)。

## 模型复盘 {#model-review}

- 共享 CLR 不等于共享惯用 API；从调用点和元数据评审最终 API 形式。
- F# 核心应保留联合、`option`、`Result`、函数与纯组合的表达力。
- 边界只投影一次，公共签名不泄露调用者不需要理解的 F# 表示。
- 类型、成员、参数名、可空性、异常和文档都是契约。
- 可空标注帮助静态分析；公开入口仍需运行时防卫。
- 业务拒绝、调用者错误和系统故障应支持不同的调用者动作。
- 领域模型、.NET 公共模型与传输格式 DTO 可以共享含义，但不必共享表示。
- 兼容性包含源码、二进制、行为与传输格式；基线工具只覆盖其中一部分。

## 来源 {#sources}

- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn：F# XML 文档](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation)
- [Microsoft Learn：F# null 值与 nullable 检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn：F# 可空值类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [Microsoft Learn：.NET 库的破坏性变更](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
- [Microsoft Learn：NuGet 包兼容性规则](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget-package-compatibility-rules)
- [Microsoft Learn：CA1008 枚举应有零值](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1008)
- [FSharp.Core 参考：`CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
