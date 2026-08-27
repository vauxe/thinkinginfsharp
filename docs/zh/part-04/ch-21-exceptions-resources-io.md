---
title: "第 21 章：异常、资源与 I/O"
description: "用 try/with 制定特定异常策略，用 use 及时释放资源，并在不抹掉原因、不混淆领域缺失的前提下翻译文件系统失败。"
translationKey: part-04/ch-21-exceptions-resources-io
---

# 第 21 章：异常、资源与 I/O {#overview}

文件 I/O 会带来纯代码没有的三个问题：操作可能通过异常失败，取得的句柄必须释放，而且部分工作可能已经产生可观察结果。把三者全部视作“返回 `Result`”会隐藏重要区别；把一切都视作异常，又会迫使调用方用异常处理普通的可恢复结果。

应把这三类问题分开处理。`use` 负责在词法作用域结束时释放资源；`try/with` 只转换调用方能够处理的异常；取得字节或文本后，领域解析仍是常规的强类型函数。这样既能明确各项责任，也无须发明万能的错误包装器。

## 异常会中断正常表达式求值 {#exception-flow}

F# 的 `try/with` 会产生值：

```fsharp
let outcome =
    try
        operation ()
        |> Ok
    with
    | :? KnownException as cause -> Error(KnownFailure cause)
```

若 `operation` 正常完成，`try` 分支提供值。若它抛出异常，处理器会从上到下检查，第一个匹配模式提供值。所有分支必须统一为同一个结果类型。如果没有模式匹配，堆栈展开会继续寻找外层处理器。

`:? IOException as cause` 这样的类型测试模式可用于普通 .NET 异常。异常模式也遵循继承：`FileNotFoundException` 属于 `IOException`。因此，放在前面的宽泛 `IOException` 处理器会吞掉更具体的用例。

异常对象包含失败信息与堆栈上下文，并不只是消息字符串。当异常进入应用逻辑时，应判断它是否代表预期结果。不要先捕获并丢掉类型、原因与堆栈，之后才猜测它的含义。

### 只有 API 约定异常流程时才抛出 {#raise-reraise}

`raise cause` 会开始异常传播。`invalidArg`、`nullArg` 与 `failwith` 等帮助函数会创建特定异常，但对于预期拒绝，具名领域联合通常更清楚。

在 `with` 处理器中，`reraise()` 会传播当前正在处理的异常，并保留原有堆栈。在那里写 `raise cause` 会从当前位置再次抛出该对象，从而改变所报告的抛出位置。如果一次捕获只为了再次抛出，那么不捕获通常更好。

用返回分支表达“预约已关闭”等常规领域状态。内存耗尽、不变量损坏和意外的库故障则继续按异常传播。调用方由此可以看出自己需要处理哪些失败。

## 资源生命周期需要单独处理 {#resource-lifetime}

`StreamReader` 实现 `IDisposable`，并负责底层流的生命周期。及时释放会归还该资源。F# 用 `use` 表明释放责任：

```fsharp
let read path =
    use reader = File.OpenText path
    reader.ReadToEnd()
```

包含代码块运行期间，该绑定表现得像 `let`；控制流离开作用域时则调用 `Dispose`。只要取得成功，这包括正常返回和异常展开。一个作用域中的多个 `use` 绑定会按声明的相反顺序释放。

`use` 回答“谁在何时释放这个值？”它不会捕获打开、读取、用户代码或释放本身产生的异常，错误处理仍需另行决定。如果获取过程已经抛出异常，就没有成功绑定的值可供 `use` 释放。

`using resource operation` 函数会围绕一次函数调用表达类似的生命周期。当词法作用域足以说明释放责任时，优先使用 `use`。若清理对象不是单个 `IDisposable`，例如任务创建的临时目录，则使用 `try/finally`。

### 保证尝试释放，不代表释放永不失败 {#disposal-failure}

运行时会尝试调用 `Dispose`，但其实现本身仍可能抛出。如果函数体执行和释放都失败，要保留两项故障就需要专门设计，不能假定 `use` 会报告两个异常。良好的资源实现通常允许重复释放，但使用方仍应明确每项资源由谁负责，并只释放一次。

对于异步资源，计算表达式构建器决定 `use!` 和异步释放的行为。第 22、23 章会说明这些规则。本章使用的是实现同步 `IDisposable` 的 `StreamReader`。

## 集中管理资源，同时明确释放责任 {#with-reader}

这个共享帮助函数接收资源获取函数和后续操作：

```fsharp:line-numbers
let withReader (openReader: string -> StreamReader) path operation =
    use reader = openReader path
    operation reader
```
其行为近似为：

```text
(string -> StreamReader) -> string -> (StreamReader -> 'T) -> 'T
```

`withReader` 负责释放 `openReader` 成功返回的每个 reader。调用方只能在 `operation` 内使用它；若返回 reader 本身，外部拿到的将是已释放对象。API 接收或返回可释放值时，应在文档中说明由谁负责释放。

注入 `openReader` 后，测试可以控制并观察资源获取过程，但打开和读取仍然不是纯函数。这个高阶函数只负责集中管理生命周期，并不会改变 I/O 的性质。

不要接收由别处管理的 reader 后悄悄释放它。一项实用约定是：谁取得资源，谁负责释放；除非 API 明确转移这项责任。

## 只翻译可操作的 I/O 失败 {#translate-errors}

错误联合会区分已知结果，并为诊断细节重要的故障保留异常对象：

```fsharp:line-numbers
type ReadTextError =
    | PathNotFound of path: string
    | AccessDenied of path: string * cause: UnauthorizedAccessException
    | IoFailure of path: string * cause: IOException
```
适配器执行翻译：

```fsharp:line-numbers
let readText path =
    try
        withReader File.OpenText path (fun reader -> reader.ReadToEnd()) |> Ok
    with
    | :? FileNotFoundException
    | :? DirectoryNotFoundException -> Error(PathNotFound path)
    | :? UnauthorizedAccessException as cause -> Error(AccessDenied(path, cause))
    | :? IOException as cause -> Error(IoFailure(path, cause))
```
其中几项选择是有意的：

- 文件缺失与目录缺失都变成 `PathNotFound path`，因为该调用方用同一种方式处理它们；
- 访问被拒绝会把具体异常保留为原因；
- 其余 `IOException` 值会保留原始异常，而不是只保留 `Message`；
- 更具体的处理器放在 `IOException` 前面；
- 不存在 `ex -> Error ...` 形式的全捕获分支。

该结果把这些 I/O 情况标记为适配器已知的失败，并不会让文件读取变成纯函数。操作仍需读取外部状态，可能与其他进程竞争，也可能遇到策略未覆盖的异常。

在内部错误值中保留异常，可以留下有用的原因与堆栈数据。错误跨进程或经过序列化时，应对外提供稳定的传输错误，而不是任意异常对象；原始原因则记录或保留在服务端。

### 在能够增加含义的层捕获 {#catch-layer}

底层帮助函数通常没有足够上下文判断“未找到”是正常结果、配置错误还是安全信号。让异常传播到以具体操作命名的适配器，由它附加路径，并且只转换调用方能够处理的情况。

不要在每一层记录同一个异常。要么处理它并记录结果，要么让它传播到负责日志的层。反复记录再重抛只会产生重复事件，不会增加信息。

## 用真实资源测试两条完成路径 {#resource-tests}

示例会在 `Path.GetTempPath()` 下创建唯一目录，写入一个文件，并打开真实 `StreamReader` 实例：

```fsharp:line-numbers
let tempName = Guid.NewGuid().ToString("N")

let tempDirectory =
    Path.Combine(Path.GetTempPath(), $"thinkinginfsharp-ch21-{tempName}")

let filePath = Path.Combine(tempDirectory, "seats.txt")
let missingPath = Path.Combine(tempDirectory, "missing.txt")
let mutable cleanupRemoved = false

Directory.CreateDirectory tempDirectory |> ignore

try
    File.WriteAllText(filePath, "42")

    let mutable successReader = None

    let openSuccess path =
        let reader = File.OpenText path
        successReader <- Some reader
        reader

    let text = withReader openSuccess filePath (fun reader -> reader.ReadToEnd())

    let successDisposed = readerIsDisposed successReader

    let mutable failureReader = None

    let openFailure path =
        let reader = File.OpenText path
        failureReader <- Some reader
        reader

    let failureCaught =
        try
            withReader openFailure filePath (fun reader ->
                reader.ReadToEnd() |> ignore
                raise (InvalidDataException "invalid-data"))

            false
        with :? InvalidDataException as cause ->
            assert (cause.Message = "invalid-data")
            true

    let failureDisposed = readerIsDisposed failureReader
    let readResult = readText filePath
    let missingResult = readText missingPath

    assert (text = "42")
    assert successDisposed
    assert failureCaught
    assert failureDisposed
    assert (readResult = Ok "42")

    match missingResult with
    | Error(PathNotFound path) -> assert (path = missingPath)
    | other -> failwithf "Expected PathNotFound, received %A" other

    printfn "Success: text=%s disposed=%b" text successDisposed
    printfn "Failure: caught=%b disposed=%b" failureCaught failureDisposed
    printfn "Read result: %s" (renderReadResult readResult)
    printfn "Missing result: %s" (renderReadResult missingResult)
finally
    if Directory.Exists tempDirectory then
        Directory.Delete(tempDirectory, recursive = true)

    cleanupRemoved <- not (Directory.Exists tempDirectory)
```
两个打开函数只为测试观察而保留 reader 引用。成功操作返回后，对保留 reader 调用 `Peek` 会抛出 `ObjectDisposedException`。第二项操作读取文件后抛出 `InvalidDataException`；在 `withReader` 外部捕获该异常后，这个 reader 同样已经释放。

这项测试直接覆盖两条控制路径。它比检查源码中是否出现 `use` 更可靠，也比删除已打开文件更具可移植性，因为类 Unix 系统与 Windows 的相关行为不同。

外层 `try/finally` 负责清理目录。目录名包含新的 GUID，删除目标仅限平台临时目录下由本次任务创建并解析出的子目录。最终断言确认该目录已不存在。

真实临时文件可以验证与 .NET 文件系统的交互。纯解析测试仍应使用内存字符串，无须准备文件系统测试环境。

## 评审 I/O 不能只看成功值 {#io-contract}

对于读取操作，至少应评审：

- 路径来源与平台规则；
- 文件打开方式与共享方式；
- 文本编码和格式错误字节策略；
- 文件大小，以及完整缓冲是否可接受；
- 异步或长时间工作中的取消；
- 由谁负责资源及其释放；
- 异常翻译与诊断保留；
- 检查路径与使用路径之间的竞争。

先调用 `File.Exists` 再调用 `File.OpenText` 无法保证文件仍然存在；其他参与者可以在两次调用间改变它。应尝试操作并处理其有文档的结果。同样，先前的访问检查不等于随后使用时的授权。

本章测试使用 `File.WriteAllText` 与 `ReadToEnd`，因为文件只有两个字节。这不是建议缓冲无界输入，也不是宣称一次写入具有原子性和持久性。应根据真实需求选择流式处理、限额、原子替换与刷新策略。

## 一致选择缺失与失败语义 {#failure-decision-table}

| 情况 | 表示 | 原因 |
|---|---|---|
| 查找没有值，且不需要解释 | `option` | `None` 已经是完整的普通结果 |
| 一项预期操作可能失败，且调用方需要原因 | `Result<'T, 'Error>` | 错误属于函数类型的一部分 |
| 若干独立纯输入检查都应报告 | 累积式验证 | 组合策略保留多项失败 |
| 依赖工作流步骤失败 | 首错 `Result.bind` 或模式匹配 | 后续工作缺少有效前提 |
| .NET API 用异常报告可恢复条件 | 在适配器捕获该特定异常并翻译 | 让外部约定符合调用方策略 |
| 出现程序错误或违反不变量 | 根据责任范围使用异常、断言或终止进程 | 一般调用方通常无法把它当领域分支恢复 |
| 出现意外基础设施故障 | 带着原因传播，直到负责运行的层能够处理 | 避免虚假、信息贫乏的领域错误 |
| 取得了可释放资源 | `use`/`using` 加独立的成功/失败处理 | 生命周期与结果语义是两类问题 |

这些选择可以组合。函数可以在内部使用 `use` 并返回 `Result`；无论产生哪一分支，仍会执行释放。验证器可以返回 `Result<_, Error list>` 而不含任何 I/O。如果异常适配器只翻译普通缺失条件，它可以返回 `option`。

不要默认使用 `Result<'T, string>` 之类的 API。字符串适合用于展示，却通常不适合作为内部错误模型：它会丢失错误类别、结构化上下文，也无法借助编译器检查处理是否完整。

## 在获取之后解析 {#parse-after-read}

一种实用的 I/O 处理顺序是：

```text
打开 + 读取 + 释放
          ↓ Result<string, ReadTextError>
用纯函数解析文本
          ↓ Result<DomainValue, ParseError>
把两者映射成工作流错误联合
```

除非必须流式处理，否则不要在解析期间故意保持文件打开。缩短资源生命周期既能减少压力，也让纯解析器测试更简单。必须流式处理时，消费者要留在资源作用域内，其异常与取消行为也在该作用域内发生。

应在工作流组合读取与解析的位置映射错误，不要把两者都压成“文件无效”。文件缺失、访问拒绝、语法错误与违反领域规则，可能需要不同的用户提示、重试方式和遥测。

## 练习 {#exercises}

### 练习 1：组合读取与解析 {#exercise-01}

定义纯函数 `parsePositiveSeats: string -> Result<int, SeatParseError>`，并在 `readText` 之后调用它。工作流应返回一个联合类型。用 `ReadFailure of ReadTextError` 表示读取失败，用 `ParseFailure of SeatParseError` 表示解析失败。

测试有效文件、缺失文件、非整数文本与零。解释为什么解析无须访问 reader 或路径。

### 练习 2：审计全捕获适配器 {#exercise-02}

评审下面的代码：

```fsharp
let read path =
    try Ok(File.ReadAllText path)
    with error -> Error error.Message
```

列出它丢失的信息与策略。用结构化错误联合、继承顺序正确的特定处理器，以及对未识别异常的明确决定来重写。说明日志应属于哪里。

### 练习 3：证明嵌套释放顺序 {#exercise-03}

用两个 `use` 绑定编写 `withTwoReaders`。注入会保留两个真实 `StreamReader` 引用的打开函数。证明操作成功与抛出异常时，两个 reader 都会释放。

解释当第二项资源依赖第一项时，为何反向声明顺序很重要，以及操作为何不能返回任何一个 reader。

[阅读本章答案](../solutions/ch-21-exceptions-resources-io)。

下一章会把同样的分离应用到稍后才完成的计算，比较 F# `Async<'T>` 与 .NET `Task<'T>`。

## 资料来源 {#sources}

- [Microsoft Learn：F# `try/with`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression)
- [Microsoft Learn：F# `raise` 与 `reraise`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function)
- [Microsoft Learn：F# `try/finally`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-finally-expression)
- [Microsoft Learn：用 `use` 管理 F# 资源](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword)
- [Microsoft Learn：`StreamReader`](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader?view=net-10.0)
