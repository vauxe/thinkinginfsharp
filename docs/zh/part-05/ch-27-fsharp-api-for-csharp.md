---
title: "第 27 章：为 C# 设计 F# API"
description: "从 C# 调用方式设计稳定的 .NET 公共 API，同时让联合、option、纯函数与领域不变量留在 F# 内部。"
translationKey: part-05/ch-27-fsharp-api-for-csharp
---

# 第 27 章：为 C# 设计 F# API {#overview}

F# 和 C# 共享 CLR、程序集与大部分基础类型。但惯用的 F# 类型经过程序集公开后，在 C# 中会呈现不同形式：

| F# 源码形式 | C# 看到的内容 |
|---|---|
| `Result<_,_>` | `FSharpResult` |
| 可区分联合 | 联合案例类型与辅助成员 |
| `option` | `FSharpOption` |
| 柯里化函数 | `FSharpFunc` |
| `Async<_>` | `FSharpAsync` |

这些都是有效的 CLR 表示。但一旦公开，调用方就会依赖它们，它们也随之成为版本兼容的一部分。

稳定的库因此维护两套清楚的表达：领域内部使用表达力强的 F# 类型，对外提供符合 .NET 习惯的 API。两者之间的适配代码应保持小巧并且可以测试。

## 先写调用代码，再设计公共 API {#consumer-first}

先写一个最小的 C# 调用程序。它能暴露 API 是否要求调用方理解 F#，还可以让编译器检查命名参数、可空性、构造方式和返回类型：

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
这个调用只使用命名空间、枚举、密封类、构造函数、静态方法、属性、`string?` 与 `int?`。C# 调用方无须知道内部存在联合和 `option`。命名实参也说明，`capacity`、`request`、`requestId` 等参数名会成为源码依赖，而不只是实现注释。

“C# 能调用”只是最低门槛。还要检查：IntelliSense 是否自然？可空分析是否准确？调用方能否按预期错误分支处理？API 升级后旧二进制能否继续运行？

## 一个含义，三层表示 {#three-surfaces}

同一个预约请求可以有三层表示，但业务规则只能保留一份：

| 层 | 主要面向 | 合适的表示 | 不应定义 |
|---|---|---|---|
| F# 领域核心 | 领域推理与穷尽匹配 | 私有联合、记录、`option`、`Result`、纯函数 | C# 便利性、序列化构造规则 |
| .NET 公共 API | C#、VB 与反射工具 | 命名空间、类、枚举、成员、可空标注、Task、委托 | JSON 字段名、ORM 布局 |
| 传输格式/存储 DTO | JSON、消息或数据库适配器 | 明确的字段、版本与序列化属性 | 领域不变量的唯一实现 |

业务规则只由领域核心决定。公共 API 和 DTO 负责解码输入、调用核心，再转换结果。三层可以采用不同表示和版本节奏，因为程序集签名、JSON 模式和数据库模式的兼容规则各不相同。

### 让联合留在核心 {#internal-union}

示例用封闭联合只表示两种领域结果；建议席位仅在拒绝时存在：

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

### 在公共 API 中转换一次 {#boundary-projection}

下面是常见的跨语言映射，并非机械地一一替换：

| 内部 F# 表示 | 常见 .NET 公共表示 | 选择依据 |
|---|---|---|
| 私有 DU / `Result<'T,'E>` | 封闭响应类加状态枚举，或预期成功值加异常 | 调用者需要怎样分支，失败是否预期 |
| `'T option` 返回值 | 可空引用、`Nullable<T>`，或 `TryX(..., out T)` | 缺失含义与值/引用类别 |
| `'T option` 参数 | 清楚的重载，偶尔为有明确 null 语义的可空参数 | 避免要求 C# 构造 `FSharpOption<T>` |
| `'T -> 'U` | `Func<T,U>`、`Action<T>` 或有名称的委托 | C# lambda 与工具支持 |
| `Async<'T>` | `Task<T>`，通常接收 `CancellationToken` | .NET 异步约定 |
| F# `list`/`Map`/`Set` | 与行为匹配的 .NET 集合接口 | 枚举、索引、查找与可变性语义 |
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
公开响应把缺失的引用映射为可空 `string`，把缺失的值映射为 `Nullable<int>`。构造函数只在程序集内部可见，因此调用方无法构造“已接受但没有确认码”的响应：

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
即使公开签名不含 `Microsoft.FSharp.*`，由 F# 编译的程序集通常仍在运行时依赖 `FSharp.Core`。目标是让调用方无须理解 F# 表示，而不是隐藏实现语言。正常的项目或 NuGet 依赖解析会自动传递这项依赖。

## 用 .NET 习惯呈现公共成员 {#dotnet-shape}

面向其他 .NET 语言的 API 优先使用命名空间、类型和成员；实现函数可以留在非公开模块。示例定义一个抽象且密封、构造函数私有的类型，用来组织一组静态操作：

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
这不是要求把每个 F# 模块都改成类。只有跨语言公共 API 需要采用调用方熟悉的形式；面向 F# 的 API 仍可自然地公开模块、函数和联合。

### 名称属于源码兼容性 {#names}

公共命名空间、类型、方法和属性使用 `PascalCase`，参数使用 `camelCase`；布尔属性采用肯定式 `IsAccepted` 或 `CanRetry`。避免只靠大小写区分名称，也不要把内部缩写传播给所有调用者。

F# 成员用括号内的元组式参数声明，编译后会成为常规的多参数 CLI 方法。因此 C# 看到的是 `Evaluate(int capacity, BookingRequest request)`。参数名要谨慎选择：C# 命名实参会把名称写进源码，重命名会破坏该源码，即使二进制签名没有变化。

`[<CompiledName>]` 可以给编译后的值或函数另一个名称，适合确实需要 F# 与 CLI 两套惯用名称的小型 API。它不是修补混乱命名的通用工具。应先统一公共词汇，再用 C# 编译器检查最终 API。

### 属性、方法与重载表达不同承诺 {#members-overloads}

属性适合读取成本低且稳定的状态值；需要参数、可能耗时或可能明显失败的操作应使用方法。不要把网络或磁盘 I/O 藏进看似字段读取的属性。

可选行为通常用重载表示，例如同时提供 `Find(requestId)` 与 `Find(requestId, attendee)`，两者都委托给同一实现。按参数数量重载通常比按相近类型重载更清楚。不要为假设中的未来选项预先建立所有组合；当选项成组增长时，改用具名 options 类型。

公开回调使用 `Func`、`Action` 或领域委托；公开异步方法返回 `Task` 或 `Task<T>`，并在调用方可以取消时接收 `CancellationToken`。内部仍可立即把委托转成 F# 函数，把任务工作流映射回领域操作。

### 集合与元组必须保留行为 {#collections-tuples}

不要只把 `list<'T>` 换成 `IEnumerable<T>` 就认为设计完成。顺序枚举适合 `IEnumerable<T>`；需要稳定索引和计数时可用 `IReadOnlyList<T>`；键查找则使用相应的字典接口。还要说明返回的是实时视图还是快照，因为只读接口并不能证明底层存储不可变。

两个短暂且没有领域名称的结果偶尔可以使用元组。如果 `Item1`、`Item2` 会让 C# 调用方猜测含义，或将来可能增加成员，就返回具名类型。少量适配代码可以换来清楚得多的 API。

## 正确表达 null、缺失与失败 {#absence-failure}

公共 API 设计首先要区分三件事：调用参数无效、业务上可预期的拒绝，以及基础设施故障。把三者全部编码为 `null`、全部抛成异常或全部塞进一个字符串，都会丢失信息。

### 可空标注不能代替运行时检查 {#null-contract}

F# 9 及以上启用 nullable 检查后，`string` 与 `string | null` 具有不同的静态含义。对于不可为 null 的公开输入，示例既生成 `NotNull` 元数据，也在入口调用 `ArgumentNullException.ThrowIfNull`。未启用可空分析的 C# 代码、反射和其他运行时调用仍然可能传入 null。

预期缺失的引用输出使用 `string | null`，预期缺失的值输出使用 `Nullable<int>`。`Nullable<T>` 只适用于值类型；F# 中没有值时构造 `Nullable<T>()`，C# 将其视为 `T?` 且 `is null` 为真。

C# 测试客户端通过反射检查这些约定，并确认公开签名没有泄露 F# 专用类型：

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
反射测试可以验证元数据，却不能替代真实调用。示例还会编译并运行接受、拒绝、无效值、null 输入和范围错误等路径。

### 枚举需要合法的零值与未知值规则 {#enum-contract}

CLR 枚举的默认值是零，任意底层整数也能被转换成枚举。`BookingOutcome.None = 0` 因而让默认值有名称；库本身只由受控构造器产生 `Accepted` 或 `Rejected`。若枚举来自不可信输入，仍应验证已定义值或在 `switch` 中保留默认分支，不能把类型声明误当成运行时封闭集合。

枚举适合稳定、无载荷的粗粒度标签，不等同于可区分联合。案例各有不同数据时，让枚举选择响应解释，而不要建立多个互相矛盾的公开布尔标志。

### 预期拒绝是数据，API 使用错误是异常 {#error-policy}

示例把座位不足和业务字段无效返回为 `BookingResponse`，因为调用方预计会展示或处理它们。null 请求与负容量都属于无效的 API 或配置输入。前者抛出 `ArgumentNullException`，后者抛出 `ArgumentOutOfRangeException`。意外 I/O、取消和程序错误继续遵循相应的 .NET 异常与 Task 规则。

错误类别并非在所有应用中都固定不变，应根据调用方可以采取的动作来分类。F# 核心使用 `Result` 或联合表达预期分支，公共适配器再把它们转换成清楚、稳定的 .NET 结果。

## XML 文档会随公共 API 发布 {#xml-documentation}

所有公开类型、构造函数、属性与方法都应有简短 XML 文档；参数防卫还应记录异常条件。`<summary>`、`<remarks>`、`<param>`、`<returns>` 与 `<exception>` 会进入 IDE 和文档工具。

样例打开 `GenerateDocumentationFile`，并把 F# 警告 3390 加入构建，用来发现 XML 格式和参数名错误：

```xml:line-numbers [FSharpApi.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>ThinkingInFSharp.Ch27.FSharpApi</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarnOn>$(WarnOn);3390</WarnOn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Library.fs" />
  </ItemGroup>
</Project>
```
客户端还会断言程序集同目录下存在 XML 文档文件，并且包含 `BookingApi.Evaluate`。这无法判断文字质量，却能发现没有随程序集生成的注释。API 稳定后，可以用 `.fsi` 文件把公共签名与文档集中到一份便于评审的清单中。

## 不要让 JSON 或数据库反向设计领域 {#wire-boundary}

序列化器可能要求公开无参构造函数、可写属性、特定字段名或属性标注。`[<CLIMutable>]` 会为 F# 记录生成默认构造函数和属性 getter/setter。它适合确有这类要求的集成 DTO，但也允许出现 null、零和部分初始化状态。

因此不要为了一个序列化器给领域记录随手添加 `[<CLIMutable>]`。建立专用 DTO，把可空和默认值视为未验证输入，再通过智能构造函数或解码器转成领域类型。反向投影同样集中在适配器。这样 JSON 字段重命名、版本兼容或 ORM 要求不会迫使领域联合和不变量一起改变。

C# 公共类型也不应自动成为 JSON 模式。进程内调用方、网络消费者和持久数据按不同节奏演进。只有确认它们确实具有相同的兼容要求时，才复用同一表示。

## 兼容性不只有“还能编译” {#compatibility}

发布过的公共 API 至少有四类兼容性：

| 类别 | 失败时间 | 例子 |
|---|---|---|
| 源码兼容性 | 调用者重新编译时 | 重命名参数破坏命名实参；新增重载造成解析歧义 |
| 二进制兼容性 | 旧二进制加载或调用时 | 给现有方法增加参数、删除成员或改变签名导致 `MissingMethodException` 等 |
| 行为兼容性 | 程序运行时 | 从返回拒绝改为抛异常；改变比较、顺序或默认值 |
| 传输格式兼容性 | 读取消息/存储数据时 | 改 JSON 字段名、枚举编码或必需字段 |

“只是增加”也未必安全。新重载可能让现有方法组或 `null` 调用产生歧义；给接口增加成员会破坏已有实现；给公开联合增加用例，会使原本穷尽的 F# 匹配不再完整。改变可空标注也可能在调用方重新编译时新增警告或错误。

优先新增成员或重载，并保留旧成员转发到新实现。迁移期间可用 `[<Obsolete("Use Evaluate(...)")>]` 指出替代项与期限。不要为了“简化”API 就直接修改原签名。跨主要版本时还应记录行为与传输格式迁移，不能只依赖语义版本号。

让 C# 测试客户端进入 CI，并保存已发布的程序集或包作为 API 基线。NuGet 包可以启用 package validation 和 baseline version。也可以用 `Microsoft.DotNet.ApiCompat.Tool` 比较程序集。这些工具能发现许多签名差异，但行为与序列化兼容仍需专项测试。

真实 C# 调用方位于 `examples/chapters/ch27/CSharpClient/Program.cs`。从仓库根目录构建并运行：

```console
dotnet build examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release
dotnet run --project examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release --no-build
```

客户端会断言业务结果、参数检查、四个导出类型、公开签名、可空元数据和 XML 文档，而不只是打印演示输出。其固定输出为：

```text
Accepted: outcome=Accepted code=CONF-REQ-27 remaining=3
Rejected: outcome=Rejected message=requested 8 exceeds available 5 suggested=5
Invalid: outcome=Rejected message=seat count must be positive suggested=none
Guards: request-id=true request=true capacity=true
Public types: BookingApi,BookingOutcome,BookingRequest,BookingResponse
Nullability: request-id=NotNull confirmation=Nullable
XML docs: evaluate=true
```

修改公共 API 后，先重新编译这个调用程序，再运行旧二进制兼容性测试与行为测试。

## 练习 {#exercises}

### 练习 1：隐藏泄露的 F# 表示类型 {#exercise-01}

一个库公开 `decide : int -> BookingRequest -> Result<(string * int), string * int option>`。为 C# 调用者设计公共类型和方法，但保留这个函数作为内部核心。写出成功、拒绝与缺失建议的对应关系，并说明哪些构造必须受控。


::: details 参考答案

#### 先定义公开 API 及其合法状态 {#exercise-01-surface}

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

:::

### 练习 2：增加可选筛选而不破坏调用方 {#exercise-02}

已有 `BookingSearch.Find(string requestId)`。增加按 attendee 筛选的能力，不公开 `string option`。给出 F# 成员声明和两种 C# 调用；说明参数命名、重载歧义以及选项继续增长时的迁移策略。


::: details 参考答案

#### 用两个重载委托给一个实现 {#exercise-02-overloads}

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

#### 让选项增长触发一次有意迁移 {#exercise-02-evolution}

第三个独立筛选不必立刻导致四个重载。若多个筛选项共同构成查询条件，可新增 `BookingSearchOptions` 和对应的 `Find` 方法。保留旧重载并转发到新实现。文档注明默认值与组合规则，再用 `Obsolete` 指明迁移目标，不要突然删除桥接成员。

即使只是新增重载，也要重新编译已有 C# 消费方；方法组、泛型推断和 null 实参都可能产生歧义。API 基线工具检查二进制兼容性，消费方编译检查源码兼容性。

:::

### 练习 3：把 JSON DTO 与领域请求分开 {#exercise-03}

假设序列化器要求无参构造和可写属性。设计一个允许未验证输入的 DTO、一个返回结构化错误的领域转换，以及反向投影位置。分类一次 JSON 字段改名会影响哪种兼容性，并说明为何 DTO 规则不应进入领域类型。


::: details 参考答案

#### 允许 DTO 不完整，再显式解码 {#exercise-03-dto}

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

#### 字段改名是传输格式迁移 {#exercise-03-compatibility}

把 JSON 的 `requestId` 改成 `id` 首先破坏传输格式兼容性：已存文档和旧客户端仍发送旧名称。若 DTO 同时是公开程序集类型，重命名属性还会影响源码和二进制兼容性，这正说明不应无意复用契约。

安全迁移可以在一段时间内读取两个名称、只写新名称，并用模式版本或明确弃用期限移除旧名称。适配器把两种输入都映射到同一个领域字段；领域 `RequestId` 不需要跟着序列化拼写变化。

:::


## 来源 {#sources}

- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn：F# XML 文档](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation)
- [Microsoft Learn：F# null 值与 nullable 检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn：F# 可空值类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [Microsoft Learn：.NET 库的破坏性变更](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
- [Microsoft Learn：NuGet 包兼容性规则](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget-package-compatibility-rules)
- [Microsoft Learn：CA1008 枚举应有零值](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1008)
- [FSharp.Core 参考：`CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
