---
title: "第 21 章答案"
description: "把资源安全读取与纯解析组合，用结构化策略替换全捕获字符串，并验证双 reader 在成功与失败时都会释放。"
translationKey: solutions/ch-21-exceptions-resources-io
kind: solution
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
termIds: []
sources:
  - id: microsoft-try-with
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression
    checked: "2026-08-24"
  - id: microsoft-raise-reraise
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function
    checked: "2026-08-24"
  - id: microsoft-use
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword
    checked: "2026-08-24"
---

# 第 21 章答案 {#overview}

让资源取得过程保持短暂，再把普通数据交给纯解析。只翻译对当前调用方具有诚实类型含义的异常；其他失败应保留其原有运行身份。

[返回第 21 章](../part-04/ch-21-exceptions-resources-io)。

## 练习 1：组合读取与解析 {#exercise-01}

### 定义不含 I/O 的解析 {#exercise-01-parser}

```fsharp
open System

type SeatParseError =
    | SeatsNotInteger of raw: string
    | SeatsNotPositive of value: int

let parsePositiveSeats (raw: string) =
    match Int32.TryParse(raw.Trim()) with
    | true, value when value > 0 -> Ok value
    | true, value -> Error(SeatsNotPositive value)
    | false, _ -> Error(SeatsNotInteger raw)

assert (parsePositiveSeats " 3 " = Ok 3)
assert (parsePositiveSeats "oops" = Error(SeatsNotInteger "oops"))
assert (parsePositiveSeats "0" = Error(SeatsNotPositive 0))
```

解析器只需要文本。路径会增加无关身份，reader 则会延长资源生命周期，却不能帮助解析。

### 保留失败所属阶段 {#exercise-01-workflow}

使用本章的 `ReadTextError` 与 `readText`：

```fsharp
type LoadSeatsError =
    | ReadFailure of ReadTextError
    | ParseFailure of SeatParseError

let loadSeats path =
    readText path
    |> Result.mapError ReadFailure
    |> Result.bind (fun text ->
        parsePositiveSeats text
        |> Result.mapError ParseFailure)
```

四项必需测试应断言这些形状：

| 夹具 | 预期结果 |
|---|---|
| 包含 `"3"` 的文件 | `Ok 3` |
| 缺失文件 | `Error(ReadFailure(PathNotFound path))` |
| 包含 `"oops"` 的文件 | `Error(ParseFailure(SeatsNotInteger "oops"))` |
| 包含 `"0"` 的文件 | `Error(ParseFailure(SeatsNotPositive 0))` |

在一个唯一临时目录下创建所有文件，并在 `finally` 中移除该确切目录。`readText` 会在 `parsePositiveSeats` 运行前释放 reader，所以解析成功或失败都不能延长文件句柄生命周期。

## 练习 2：审计全捕获适配器 {#exercise-02}

### 找出字符串抹掉的内容 {#exercise-02-audit}

全捕获版本会丢失：

- 异常的运行时类型与继承类别；
- 堆栈跟踪与内部异常；
- 路径与操作等结构化上下文；
- 缺失、拒绝、格式错误、取消与意外故障之间的区别；
- 哪些条件可恢复的显式决定；
- 稳定处理能力，因为本地化或随版本变化的消息属于呈现文本。

它还可能捕获以后加入 `try` 块的程序错误，并把它们错误报告成文件读取失败。

### 让翻译策略保持窄小 {#exercise-02-rewrite}

```fsharp
open System.IO

type ReadFailure =
    | MissingPath of path: string
    | Denied of path: string * cause: UnauthorizedAccessException
    | OtherIo of path: string * cause: IOException

let read path =
    try
        File.ReadAllText path
        |> Ok
    with
    | :? FileNotFoundException
    | :? DirectoryNotFoundException -> Error(MissingPath path)
    | :? UnauthorizedAccessException as cause -> Error(Denied(path, cause))
    | :? IOException as cause -> Error(OtherIo(path, cause))
```

具体的缺失路径用例位于 `IOException` 基类型处理器之前。没有最终 `ex` 模式，所以实参错误和已声明 I/O 策略之外的故障会保留诊断身份继续传播。

日志应放在操作最终得到处理或放弃的位置，而不是自动放进 `read`。如果 `OtherIo` 返回到服务边界，该边界可以带请求上下文记录一次 `cause`，再把它映射成稳定外部响应。

## 练习 3：证明嵌套释放顺序 {#exercise-03}

### 让两个 reader 都留在作用域内 {#exercise-03-scope}

```fsharp
open System
open System.IO

let withTwoReaders openFirst firstPath openSecond secondPath operation =
    use first = openFirst firstPath
    use second = openSecond secondPath
    operation first second

let readerIsDisposed (reader: StreamReader option) =
    match reader with
    | None -> false
    | Some value ->
        try
            value.Peek() |> ignore
            false
        with :? ObjectDisposedException ->
            true
```

对于任务临时目录中已有的 `firstPath` 与 `secondPath`，在带仪表的打开函数中保留引用：

```fsharp
let mutable firstSeen = None
let mutable secondSeen = None

let openFirst path =
    let reader = File.OpenText path
    firstSeen <- Some reader
    reader

let openSecond path =
    let reader = File.OpenText path
    secondSeen <- Some reader
    reader

withTwoReaders openFirst firstPath openSecond secondPath (fun first second ->
    first.Peek() + second.Peek())
|> ignore

assert (readerIsDisposed firstSeen)
assert (readerIsDisposed secondSeen)
```

重置保留的引用，使用一个会抛出 `InvalidDataException` 的操作调用同一个帮助函数，在外部捕获异常，然后重复两项释放断言。临时目录清理仍属于外层 `finally`。

F# 规定采用与声明相反的顺序：先释放 `second`，再释放 `first`。应先声明基础资源，再声明依赖它的资源，使依赖资源首先释放。操作不能返回任何 reader，因为 `withTwoReaders` 返回之后，两者都已经离开有效生命周期。

## 应注意什么 {#what-to-notice}

- 读取错误与解析错误通过 `Result.bind` 组合时仍保持区分。
- 特定异常翻译是策略；全捕获字符串会丢失信息。
- 即使操作抛出，`use` 仍会限定两个 reader 的作用域。
- 资源依赖应遵循声明顺序，使反向释放保持安全。
- 临时文件清理包围测试；它不属于纯解析器。
