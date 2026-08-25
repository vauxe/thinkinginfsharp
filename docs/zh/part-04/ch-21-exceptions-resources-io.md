---
title: "第 21 章：异常、资源与 I/O"
description: "用 try/with 制定特定异常策略，用 use 及时释放资源，并在不抹掉原因、不混淆领域缺失的前提下翻译文件系统失败。"
translationKey: part-04/ch-21-exceptions-resources-io
kind: chapter
part: 4
chapter: 21
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch21-exceptions-resources-io
exerciseIds:
  - ch21-exercise-01
  - ch21-exercise-02
  - ch21-exercise-03
termIds:
  - effect
  - option
  - result
  - validation-accumulation
sources:
  - id: microsoft-try-with
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression
    checked: "2026-08-24"
  - id: microsoft-raise-reraise
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function
    checked: "2026-08-24"
  - id: microsoft-try-finally
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-finally-expression
    checked: "2026-08-24"
  - id: microsoft-use
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword
    checked: "2026-08-24"
  - id: dotnet-stream-reader
    url: https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader?view=net-10.0
    checked: "2026-08-24"
---

# 第 21 章：异常、资源与 I/O {#overview}

文件 I/O 会组合纯代码没有的三项关注点：操作可以在返回路径之外失败，取得的句柄必须释放，而且部分工作可能已经可观察。把三者全部视作“返回 `Result`”会隐藏重要区别。把一切都视作异常，则会把普通可恢复结果推给每一位调用方。

本章会保持这些边界相互独立。`use` 在词法作用域内拥有一个可释放资源。`try/with` 只翻译调用方能够处理的异常。取得字节或文本后，领域解析仍是普通有类型函数。这样可以保持显式，而不必发明普遍适用的错误包装器。

## 学完后你能够做什么 {#outcomes}

学完本章，你应该能够：

- 把 `try/with` 读成由第一个匹配处理器返回结果的表达式；
- 按不会破坏继承语义的顺序捕获特定 .NET 异常类型；
- 让未处理故障继续传播，而不破坏其堆栈信息；
- 用 `use` 在成功与异常路径上释放 `IDisposable` 值；
- 区分资源生命周期与异常翻译；
- 注入资源取得过程以便测试，同时让所有权毫不含糊；
- 把选定的文件系统失败翻译成可操作错误联合；
- 把意外 I/O 异常保留为原因，而不是缩减成字符串；
- 用 `try/finally` 清理任务自己拥有的临时目录；
- 在 `option`、`Result`、验证累积与异常之间作出一致选择。

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

异常对象包含失败信息与堆栈上下文，并不只是消息字符串。应在边界判断某项已知异常是否代表预期应用结果。不要先捕获，等丢掉类型、原因与堆栈后才判断它意味着什么。

### 只有异常流就是契约时才抛出 {#raise-reraise}

`raise cause` 会开始异常传播。`invalidArg`、`nullArg` 与 `failwith` 等帮助函数会创建特定异常，但对于预期拒绝，具名领域联合通常更清楚。

在 `with` 处理器中，`reraise()` 会传播当前正在处理的异常，并保留原有堆栈。在那里写 `raise cause` 会从当前位置再次抛出该对象，从而改变所报告的抛出位置。如果一次捕获只为了再次抛出，那么不捕获通常更好。

不要把异常当作“预约已关闭”等普通领域状态的不可见分支。反过来，也不要仅为了保持返回类型统一，就把内存耗尽、损坏不变量或意外库故障强行变成 `Error "failed"`。

## 资源生命周期是独立契约 {#resource-lifetime}

`StreamReader` 实现 `IDisposable` 并拥有底层流。及时释放会归还该资源。F# 用 `use` 表达所有权：

```fsharp
let read path =
    use reader = File.OpenText path
    reader.ReadToEnd()
```

包含代码块运行期间，该绑定表现得像 `let`；控制流离开作用域时则调用 `Dispose`。只要取得成功，这包括正常返回和异常展开。一个作用域中的多个 `use` 绑定会按声明的相反顺序释放。

`use` 回答“谁在何时释放这个值？”它不会捕获打开、读取、用户代码或释放本身产生的异常。异常策略仍是独立层。如果取得值本身就抛出，则没有成功绑定可供 `use` 释放的值。

`using resource operation` 函数会围绕一次函数调用表达类似生命周期。当词法作用域已经能传达所有权时，应优先使用 `use`。对于不由单个 `IDisposable` 表示的清理，例如移除任务拥有的临时目录，应使用 `try/finally`。

### 保证尝试释放，不代表释放永不失败 {#disposal-failure}

运行时会尝试调用 `Dispose`；其实现本身仍可能抛出。如果函数体执行和释放都失败，要保留两项故障就需要显式策略，不能假定 `use` 能报告两个异常。良好的资源实现会让重复释放安全，但消费者仍应明确拥有每项资源并只释放一次。

对于异步资源，适用的计算表达式构建器会控制 `use!`/异步释放行为。第 22 与 23 章会处理该契约。本章的 `StreamReader` 是同步 `IDisposable`。

## 复用所有权，同时不隐藏它 {#with-reader}

共享帮助函数接收获取函数与操作：

<<< @/../examples/scripts/ch21-exceptions-resources-io.fsx#resource-scope{fsharp:line-numbers} [ch21-exceptions-resources-io.fsx]

其契约近似为：

```text
(string -> StreamReader) -> string -> (StreamReader -> 'T) -> 'T
```

`withReader` 拥有 `openReader` 成功返回的每个 reader。调用方只能在 `operation` 内使用它；返回 reader 本身会把已经释放的对象交给外部。当 API 接收或返回可释放值时，应在文档中说明所有权。

注入 `openReader` 会让获取点可以由测试控制与观察，却不会让打开或读取变纯。这个高阶函数用于集中生命周期，而不是把 I/O 改名成函数式抽象。

不要接收别人拥有的 reader 后悄悄释放它。一项实用约定是：取得资源的代码拥有它，除非 API 显式转移所有权。

## 只翻译可操作的 I/O 失败 {#translate-errors}

错误联合会区分已知结果，并为诊断细节重要的故障保留异常对象：

<<< @/../examples/scripts/ch21-exceptions-resources-io.fsx#error-model{fsharp:line-numbers} [ch21-exceptions-resources-io.fsx]

适配器执行翻译：

<<< @/../examples/scripts/ch21-exceptions-resources-io.fsx#translate-errors{fsharp:line-numbers} [ch21-exceptions-resources-io.fsx]

其中几项选择是有意的：

- 文件缺失与目录缺失都变成 `PathNotFound path`，因为该调用方用同一种方式处理它们；
- 访问被拒绝会把具体异常保留为原因；
- 其余 `IOException` 值会保留原始异常，而不是只保留 `Message`；
- 更具体的处理器放在 `IOException` 前面；
- 不存在 `ex -> Error ...` 形式的全捕获分支。

该结果表示这些 I/O 结果在此适配器中属于预期。它没有宣称文件读取是纯函数。操作仍会查询外部状态，可能与其他进程竞争，也可能遇到这项策略未覆盖的异常。

在内部错误值中持有异常会保留有用的原因与堆栈数据。如果错误要跨进程或序列化边界，应公开稳定的传输错误，并在服务端记录或用其他方式保留原因；不要把任意异常对象暴露成公开线上契约。

### 在能够增加含义的层捕获 {#catch-layer}

低层帮助函数通常没有足够上下文判断“未找到”是正常结果、配置错误还是安全信号。让异常到达以具体操作命名的适配器。该适配器可以附加路径，并只翻译其调用方理解的用例。

不要在每一层记录同一个异常。一种常见策略是：要么处理它并记录所得结果，要么让它传播到拥有日志责任的边界。反复记录再重抛会产生重复事件，却没有增加信息。

## 用真实资源测试两条完成路径 {#resource-tests}

共享脚本会在 `Path.GetTempPath()` 下创建唯一目录，写入一个文件，并打开真实 `StreamReader` 实例：

<<< @/../examples/scripts/ch21-exceptions-resources-io.fsx#temp-tests{fsharp:line-numbers} [ch21-exceptions-resources-io.fsx]

两个打开函数只为测试观察而保留 reader 引用。成功操作返回后，对保留 reader 调用 `Peek` 会抛出 `ObjectDisposedException`。第二项操作读取文件后抛出 `InvalidDataException`；在 `withReader` 外部捕获该异常后，这个 reader 同样已经释放。

这是两条控制路径的直接证据。它比断言源码中出现 `use` 关键字更强，也比尝试删除已打开文件更可移植——类 Unix 系统与 Windows 对打开文件删除有不同表现。

外层 `try/finally` 拥有目录清理。目录名包含新的 GUID，目标是平台临时目录下的具体子项，并且只会删除该任务解析出的自有路径。最终断言确认它不再存在。

真实临时文件会证明 .NET 边界。纯解析测试仍应使用内存字符串，不需要文件系统夹具。

## I/O 不只拥有成功值 {#io-contract}

对于读取操作，至少应评审：

- 路径来源与平台规则；
- 取得与共享模式；
- 文本编码和格式错误字节策略；
- 文件大小，以及完整缓冲是否可接受；
- 异步或长时间工作中的取消；
- 资源所有权与释放；
- 异常翻译与诊断保留；
- 检查路径与使用路径之间的竞争。

先调用 `File.Exists` 再调用 `File.OpenText` 无法保证文件仍然存在；其他参与者可以在两次调用间改变它。应尝试操作并处理其有文档的结果。同样，先前的访问检查不等于随后使用时的授权。

本章夹具使用 `File.WriteAllText` 与 `ReadToEnd`，因为文件只有两个字节。这不是建议缓冲无界输入，也不是宣称一次写入具有原子性和持久性。应根据真实需求选择流式处理、限额、原子替换与刷新策略。

## 一致选择缺失与失败语义 {#failure-decision-table}

| 情况 | 表示 | 原因 |
|---|---|---|
| 查找没有值，且不需要解释 | `option` | `None` 已经是完整的普通结果 |
| 一项预期操作可能失败，且调用方需要原因 | `Result<'T, 'Error>` | 错误属于有类型契约 |
| 若干独立纯输入检查都应报告 | 累积式验证 | 组合策略保留多项失败 |
| 依赖工作流步骤失败 | 首错 `Result.bind` 或显式匹配 | 后续工作缺少有效前提 |
| .NET API 用异常报告可恢复条件 | 在适配器捕获该特定异常并翻译 | 让外部约定符合调用方策略 |
| 程序员契约或不变量损坏 | 根据所有权使用异常、断言或进程失败 | 普通调用方通常无法把它当领域分支恢复 |
| 出现意外基础设施故障 | 带着原因传播，直到运行边界能够处理 | 避免虚假、信息贫乏的领域错误 |
| 取得了可释放资源 | `use`/`using` 加独立成功/失败契约 | 生命周期与结果语义是不同维度 |

这些选择可以组合。函数可以在内部使用 `use` 并返回 `Result`；无论产生哪一分支，仍会执行释放。验证器可以返回 `Result<_, Error list>` 而不含任何 I/O。如果异常适配器只翻译普通缺失条件，它可以返回 `option`。

避免默认使用 `Result<'T, string>` 之类的 API。字符串适合作为呈现值，但通常不是良好的内部错误模型：它会丢失用例、结构化上下文与编译器辅助处理。

## 在获取之后解析 {#parse-after-read}

一种实用的边界顺序是：

```text
打开 + 读取 + 释放
          ↓ Result<string, ReadTextError>
用纯函数解析文本
          ↓ Result<DomainValue, ParseError>
把两者映射成工作流错误联合
```

除非必须流式处理，否则不要为了执行解析而故意保持文件打开。较短生命周期会减少资源压力，也让纯解析器测试变得简单。必须流式处理时，消费者必须留在资源作用域内，其异常/取消行为也会成为该作用域契约的一部分。

应在工作流边界映射错误，而不是把两者压成“文件无效”。文件缺失、访问拒绝、语法错误与领域规则违规可能需要不同的用户消息、重试选择与遥测。

## 运行共享示例 {#run-example}

在仓库根目录运行：

```console
dotnet fsi --exec examples/scripts/ch21-exceptions-resources-io.fsx
```

五行确定性输出会证明成功路径释放、异常路径释放、成功读取、缺失路径翻译和最终临时目录清理。manifest 会检查确切输出。

## 练习 {#exercises}

### 练习 1：组合读取与解析 {#exercise-01}

定义纯 `parsePositiveSeats: string -> Result<int, SeatParseError>`。把它组合在 `readText` 之后，让工作流返回一个联合，以区分 `ReadFailure of ReadTextError` 与 `ParseFailure of SeatParseError`。

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

## 模型回顾 {#model-review}

- `try/with` 从正常分支或第一个匹配异常处理器返回值。
- 在异常基类型前捕获其子类型，并且只翻译调用方理解的结果。
- `reraise()` 会保留当前异常堆栈；无意义的捕获再抛出只会增加风险而没有策略。
- `use` 会在正常与异常退出时，为一个词法作用域及时执行释放。
- 释放、异常翻译、领域验证与日志是不同决定。
- 保留结构化错误上下文与原始原因，而不是把每项失败都缩减成消息。
- 真实临时资源验证适配器；纯值验证解析与领域逻辑。
- `option`、`Result`、累积、异常与 `use` 回答不同问题，并且可以组合。

下一章会把同样的分离应用到稍后才完成的计算，比较 F# `Async<'T>` 与 .NET `Task<'T>`。

## 资料来源 {#sources}

- [Microsoft Learn：F# `try/with`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression)
- [Microsoft Learn：F# `raise` 与 `reraise`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function)
- [Microsoft Learn：F# `try/finally`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-finally-expression)
- [Microsoft Learn：用 `use` 管理 F# 资源](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword)
- [Microsoft Learn：`StreamReader`](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader?view=net-10.0)
