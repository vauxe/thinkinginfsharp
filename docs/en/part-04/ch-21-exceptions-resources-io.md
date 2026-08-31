---
title: "Chapter 21: Exceptions, Resources, and I/O"
description: "Use try/with for specific exception policy, use for prompt disposal, and translate file-system failures without erasing causes or confusing them with domain absence."
translationKey: part-04/ch-21-exceptions-resources-io
---

# Chapter 21: Exceptions, Resources, and I/O {#overview}

File I/O adds three concerns that pure code does not have: an operation can throw instead of returning, an acquired handle must be released, and partial work may already be visible. Treating all three as “return `Result`” hides important differences. Treating everything as an exception forces callers to catch ordinary, recoverable outcomes.

Keep these concerns separate. `use` disposes a resource when its lexical scope ends. `try/with` translates only exceptions the caller can act on. Once bytes or text have been read, domain parsing remains an ordinary typed function. This design states each responsibility clearly without inventing a universal error wrapper.

## Exceptions interrupt normal expression evaluation {#exception-flow}

An F# `try/with` has a value:

```fsharp
let outcome =
    try
        operation ()
        |> Ok
    with
    | :? KnownException as cause -> Error(KnownFailure cause)
```

If `operation` completes, the `try` branch supplies the value. If it raises, handlers are checked from top to bottom and the first matching pattern supplies the value. All branches must agree on one result type. If no pattern matches, stack unwinding continues to an outer handler.

Type-test patterns such as `:? IOException as cause` work with ordinary .NET exceptions. An exception pattern also follows inheritance: `FileNotFoundException` is an `IOException`. Therefore a broad `IOException` handler placed first would consume the more specific case.

An exception object contains failure information and stack context; it is not merely a message string. At the point where an exception enters application logic, decide whether it represents an expected outcome. Do not catch it first and interpret it later, after discarding its type, cause, and stack.

### Raise only when the API calls for exceptional flow {#raise-reraise}

`raise cause` starts exception propagation. Helpers such as `invalidArg`, `nullArg`, and `failwith` create particular exceptions, but a named domain union is usually clearer for an expected rejection.

Inside a `with` handler, `reraise()` propagates the currently handled exception while preserving its existing stack. Writing `raise cause` there throws that object from the current point and changes the reported throw site. Often no catch is better than a catch whose only purpose is to rethrow.

Represent ordinary domain states such as “booking is closed” with a return branch. Let out-of-memory conditions, broken invariants, and unexpected library faults propagate as exceptions. Callers can then see which failures they are expected to handle.

## Resource lifetime is a separate concern {#resource-lifetime}

`StreamReader` implements `IDisposable` and owns an underlying stream. Prompt disposal releases that resource. F# expresses ownership with `use`:

```fsharp
let read path =
    use reader = File.OpenText path
    reader.ReadToEnd()
```

The binding behaves like `let` while the containing block runs, then calls `Dispose` when control leaves the scope. This includes normal return and exception unwinding after acquisition succeeds. Multiple `use` bindings in one scope are disposed in reverse declaration order.

`use` answers “who releases this value, and when?” It does not catch exceptions from opening, reading, user code, or disposal. Error handling remains separate. If acquisition itself throws, no value was bound for `use` to dispose.

The `using resource operation` function expresses a similar lifetime around one function call. Prefer `use` when lexical scope already communicates ownership. Use `try/finally` for cleanup that is not represented by one `IDisposable`, such as removing a task-owned temporary directory.

### Disposal is guaranteed, not infallible {#disposal-failure}

The runtime attempts `Dispose`; the implementation itself can throw. If body execution and disposal both fail, preserving both failures requires an explicit policy rather than assuming `use` can report two exceptions. Good resource implementations make repeated disposal safe, but consumers should still own each resource clearly and dispose it once.

For asynchronous resources, the computation-expression builder determines how `use!` and asynchronous disposal behave. Chapters 22 and 23 cover those rules. This chapter uses the synchronous `IDisposable` implementation of `StreamReader`.

## Make ownership reusable without hiding it {#with-reader}

The shared helper accepts acquisition and an operation:

```fsharp:line-numbers
let withReader (openReader: string -> StreamReader) path operation =
    use reader = openReader path
    operation reader
```
Its behavior is approximately:

```text
(string -> StreamReader) -> string -> (StreamReader -> 'T) -> 'T
```

`withReader` owns every reader returned successfully by `openReader`. Callers may use the reader only inside `operation`; returning the reader itself would hand out an already disposed object. Name ownership in documentation when an API accepts or returns disposable values.

Injecting `openReader` makes the acquisition point controllable and observable in tests. It does not make opening or reading pure. The higher-order function exists to centralize lifetime, not to rename I/O as a functional abstraction.

Avoid accepting a reader that somebody else owns and then silently disposing it. A useful convention is: the code that acquires a resource owns it unless the API explicitly transfers ownership.

## Translate only actionable I/O failures {#translate-errors}

The error union separates known outcomes and retains exception objects for failures whose diagnostic details matter:

```fsharp:line-numbers
type ReadTextError =
    | PathNotFound of path: string
    | AccessDenied of path: string * cause: UnauthorizedAccessException
    | IoFailure of path: string * cause: IOException
```
The adapter performs the translation:

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
Several choices are deliberate:

- missing file and missing directory become one `PathNotFound path` because this caller handles them the same way;
- access denial retains its concrete exception as the cause;
- remaining `IOException` values retain the original exception rather than only `Message`;
- more specific handlers appear before `IOException`;
- there is no catch-all `ex -> Error ...` branch.

The result marks these I/O outcomes as expected cases for this adapter; it does not make file reading pure. The operation still reads external state, can race with another process, and may encounter exceptions outside this policy.

Holding an exception inside an internal error value preserves useful cause and stack data. Across a process or serialization boundary, expose a stable transport error instead of an arbitrary exception object. Log or otherwise retain the cause on the service side.

### Catch at the layer that can add meaning {#catch-layer}

A low-level helper often lacks enough context to decide whether “not found” is normal, a configuration error, or a security signal. Let the exception reach an adapter named for the operation. That adapter can attach the path and translate only the cases its caller understands.

Do not log the same exception at every layer. Either handle it and record the outcome, or propagate it to the layer responsible for logging. Repeated log-and-rethrow creates duplicate incidents without adding information.

## Test both completion paths with real resources {#resource-tests}

The example creates a unique directory beneath `Path.GetTempPath()`, writes one file, and opens actual `StreamReader` instances:

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
Two opener functions retain reader references solely for test observation. After the success operation returns, calling `Peek` on the retained reader raises `ObjectDisposedException`. A second operation reads the file and then raises `InvalidDataException`; after the exception is caught outside `withReader`, that reader is disposed too.

This directly tests both control paths. It proves more than checking for a `use` keyword in source and is more portable than deleting an open file, which behaves differently on Unix-like systems and Windows.

The outer `try/finally` handles directory cleanup. The directory name contains a fresh GUID, and deletion targets only that resolved child of the platform temporary directory. The final assertion confirms that the directory no longer exists.

Testing with real temporary files proves the .NET boundary. Pure parsing tests should still use in-memory strings; they do not need a filesystem fixture.

## Review more than the success value {#io-contract}

For a read operation, review at least:

- path provenance and platform rules;
- acquisition and sharing mode;
- text encoding and malformed byte policy;
- file size and whether whole-file buffering is acceptable;
- cancellation for asynchronous or long-running work;
- resource ownership and disposal;
- exception translation and diagnostic retention;
- races between checking and using a path.

`File.Exists` followed by `File.OpenText` cannot guarantee that the file remains present; another actor can change it between calls. Attempt the operation and handle its documented outcomes. Likewise, a prior access check is not authorization for later use.

The chapter fixture uses `File.WriteAllText` and `ReadToEnd` because its file contains two bytes. That is not a recommendation to buffer unbounded input or to treat a write as atomic and durable. Choose streaming, limits, atomic replacement, and flush policy from the real requirement.

## Choose absence and failure semantics consistently {#failure-decision-table}

| Situation | Representation | Why |
|---|---|---|
| Lookup has no value and no explanation is needed | `option` | `None` is the complete ordinary outcome |
| One expected operation can fail and caller needs a reason | `Result<'T, 'Error>` | The error is part of the function's type |
| Several independent pure input checks should all report | Accumulating validation | Combination policy preserves multiple failures |
| A dependent workflow step fails | First-error `Result.bind` or explicit match | Later work lacks a valid prerequisite |
| A .NET API reports a recoverable condition by exception | Catch that specific exception at an adapter and translate | Aligns a foreign convention with caller policy |
| A programmer error or invariant violation occurs | Exception, assertion, or process failure according to responsibility | Ordinary callers usually cannot recover through a domain branch |
| An unexpected infrastructure failure occurs | Propagate with its cause until the responsible operational layer can handle it | Avoid false, information-poor domain errors |
| A disposable resource is acquired | `use`/`using` plus separate success/failure handling | Lifetime and result semantics are different concerns |

These choices compose. A function may use `use` internally and return `Result`; disposal still happens when either branch is produced. A validator may return `Result<_, Error list>` without any I/O. An exception adapter may return `option` when the only translated condition is ordinary absence.

Avoid APIs such as `Result<'T, string>` by default. Strings are appropriate presentation values, but usually poor internal error models: they lose cases, structured context, and compiler-assisted handling.

## Keep parsing after acquisition {#parse-after-read}

A useful I/O sequence is:

```text
open + read + dispose
          ↓ Result<string, ReadTextError>
parse text with pure functions
          ↓ Result<DomainValue, ParseError>
map both into a workflow error union
```

Do not keep the file open during parsing unless streaming requires it. A short lifetime reduces resource pressure and keeps pure parser tests simple. With streaming, the consumer must stay inside the resource scope, so its exception and cancellation behavior also applies within that scope.

Map errors where the workflow combines reading and parsing; do not flatten both into “invalid file.” Missing files and denied access may need different messages or retry choices. Malformed syntax and violated domain rules may need different telemetry.

## Exercises {#exercises}

### Exercise 1: compose reading and parsing {#exercise-01}

Define a pure `parsePositiveSeats: string -> Result<int, SeatParseError>`. Compose it after `readText` so the workflow returns a union distinguishing `ReadFailure of ReadTextError` from `ParseFailure of SeatParseError`.

Test a valid file, a missing file, non-integer text, and zero. Explain why parsing does not need access to the reader or path.


::: details Answer

#### Define parsing without I/O {#exercise-01-parser}

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

The parser needs only text. A path would add irrelevant identity, and a reader would extend resource lifetime without helping the parse.

#### Preserve which phase failed {#exercise-01-workflow}

Using `ReadTextError` and `readText` from the chapter:

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

The four required tests should assert these results:

| Fixture | Expected result |
|---|---|
| file containing `"3"` | `Ok 3` |
| missing file | `Error(ReadFailure(PathNotFound path))` |
| file containing `"oops"` | `Error(ParseFailure(SeatsNotInteger "oops"))` |
| file containing `"0"` | `Error(ParseFailure(SeatsNotPositive 0))` |

Create all files beneath one unique temporary directory and remove that exact directory in `finally`. `readText` disposes its reader before `parsePositiveSeats` runs, so parser success or failure cannot extend the file handle's lifetime.

:::

### Exercise 2: audit a catch-all adapter {#exercise-02}

Review this code:

```fsharp
let read path =
    try Ok(File.ReadAllText path)
    with error -> Error error.Message
```

List the information and policy it loses. Rewrite it with a structured error union, specific handlers in correct inheritance order, and a decision about unrecognized exceptions. State where logging belongs.


::: details Answer

#### Identify what the string erases {#exercise-02-audit}

The catch-all version loses:

- the exception's runtime type and inheritance category;
- its stack trace and inner exception;
- structured context such as path and operation;
- the distinction between missing, denied, malformed, canceled, and unexpected failure;
- an explicit decision about which conditions are recoverable;
- stable handling, because localized or version-dependent messages are presentation text.

It may also catch programming errors from code added later to the `try` block and misreport them as a file read failure.

#### Make the translation policy narrow {#exercise-02-rewrite}

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

Specific missing-path cases come before the `IOException` base handler. There is no final `ex` pattern, so argument bugs and failures outside the declared I/O policy propagate with their diagnostic identity intact.

Log where the operation is finally handled or abandoned, not automatically inside `read`. If `OtherIo` is returned to a service boundary, that boundary can log `cause` once with request context and map it to a stable external response.

:::

### Exercise 3: prove nested disposal order {#exercise-03}

Write `withTwoReaders` using two `use` bindings. Inject opener functions that retain both actual `StreamReader` references. Prove both readers are disposed when the operation succeeds and when it raises.

Explain why reverse declaration order matters for resources where the second depends on the first, and why the operation must not return either reader.


::: details Answer

#### Keep both readers inside the scope {#exercise-03-scope}

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

For existing `firstPath` and `secondPath` inside the task's temporary directory, retain references in instrumented openers:

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

Reset the retained references, call the same helper with an operation that raises `InvalidDataException`, catch it outside, and repeat both disposal assertions. Cleanup of the temporary directory still belongs in an outer `finally`.

F# specifies reverse declaration order: `second` is disposed before `first`. Declare a base resource first and a resource depending on it second, so the dependent resource is released first. The operation must not return either reader because both are outside their valid lifetime after `withTwoReaders` returns.

:::


The next chapter applies the same separation to computations that complete later, comparing F# `Async<'T>` with .NET `Task<'T>`.

## Sources {#sources}

- [Microsoft Learn: F# `try/with`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression)
- [Microsoft Learn: F# `raise` and `reraise`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function)
- [Microsoft Learn: F# `try/finally`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-finally-expression)
- [Microsoft Learn: F# resource management with `use`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword)
- [Microsoft Learn: `StreamReader`](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader?view=net-10.0)
