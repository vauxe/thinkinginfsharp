---
title: "Chapter 21: Exceptions, Resources, and I/O"
description: "Use try/with for specific exception policy, use for prompt disposal, and translate file-system failures without erasing causes or confusing them with domain absence."
translationKey: part-04/ch-21-exceptions-resources-io
---

# Chapter 21: Exceptions, Resources, and I/O {#overview}

File I/O combines three concerns that pure code does not have: an operation can fail outside its return path, an acquired handle must be released, and partial work may already be observable. Treating all three as “return `Result`” hides important differences. Treating everything as an exception pushes ordinary recoverable outcomes onto every caller.

This chapter keeps the boundaries separate. `use` owns a disposable resource for a lexical scope. `try/with` translates only exceptions the caller can act on. Domain parsing remains an ordinary typed function after bytes or text have been acquired. The result is explicit without inventing a universal error wrapper.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read `try/with` as an expression whose first matching handler returns the result;
- catch specific .NET exception types in inheritance-safe order;
- let unhandled failures propagate without destroying their stack information;
- use `use` to dispose an `IDisposable` value on success and exception paths;
- distinguish resource lifetime from exception translation;
- inject resource acquisition for testing while keeping ownership unambiguous;
- translate selected file-system failures into an actionable error union;
- preserve an unexpected I/O exception as a cause instead of reducing it to a string;
- clean a task-owned temporary directory with `try/finally`;
- choose consistently among `option`, `Result`, validation accumulation, and exceptions.

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

An exception object contains failure information and stack context. It is not merely a message string. Decide at a boundary whether a known exception means an expected application outcome. Do not catch first and decide what it meant later after its type, cause, and stack have been discarded.

### Raise only when exceptional flow is the contract {#raise-reraise}

`raise cause` starts exception propagation. Helpers such as `invalidArg`, `nullArg`, and `failwith` create particular exceptions, but a named domain union is usually clearer for an expected rejection.

Inside a `with` handler, `reraise()` propagates the currently handled exception while preserving its existing stack. Writing `raise cause` there throws that object from the current point and changes the reported throw site. Often no catch is better than a catch whose only purpose is to rethrow.

Do not use exceptions as an invisible branch for ordinary domain states such as “booking is closed.” Conversely, do not force an out-of-memory condition, a broken invariant, or an unexpected library fault into `Error "failed"` merely to keep a return type uniform.

## Resource lifetime is a separate contract {#resource-lifetime}

`StreamReader` implements `IDisposable` and owns an underlying stream. Prompt disposal releases that resource. F# expresses ownership with `use`:

```fsharp
let read path =
    use reader = File.OpenText path
    reader.ReadToEnd()
```

The binding behaves like `let` while the containing block runs, then calls `Dispose` when control leaves the scope. This includes normal return and exception unwinding after acquisition succeeds. Multiple `use` bindings in one scope are disposed in reverse declaration order.

`use` answers “who releases this value, and when?” It does not catch an exception from opening, reading, user code, or disposal. Exception policy remains a separate layer. If acquiring the value itself throws, no value was bound for `use` to dispose.

The `using resource operation` function expresses a similar lifetime around one function call. Prefer `use` when lexical scope already communicates ownership. Use `try/finally` for cleanup that is not represented by one `IDisposable`, such as removing a task-owned temporary directory.

### Disposal is guaranteed, not infallible {#disposal-failure}

The runtime attempts `Dispose`; the implementation itself can throw. If body execution and disposal both fail, preserving both failures requires an explicit policy rather than assuming `use` can report two exceptions. Good resource implementations make repeated disposal safe, but consumers should still own each resource clearly and dispose it once.

For asynchronous resources, the applicable computation-expression builder controls `use!`/asynchronous disposal behavior. Chapters 22 and 23 cover that contract. This chapter's `StreamReader` is synchronous `IDisposable`.

## Make ownership reusable without hiding it {#with-reader}

The shared helper accepts acquisition and an operation:

```fsharp:line-numbers [ch21-exceptions-resources-io.fsx]
let withReader (openReader: string -> StreamReader) path operation =
    use reader = openReader path
    operation reader
```
The contract is approximately:

```text
(string -> StreamReader) -> string -> (StreamReader -> 'T) -> 'T
```

`withReader` owns every reader returned successfully by `openReader`. Callers may use the reader only inside `operation`; returning the reader itself would hand out an already disposed object. Name ownership in documentation when an API accepts or returns disposable values.

Injecting `openReader` makes the acquisition point controllable and observable in tests. It does not make opening or reading pure. The higher-order function exists to centralize lifetime, not to rename I/O as a functional abstraction.

Avoid accepting a reader that somebody else owns and then silently disposing it. A useful convention is: the code that acquires a resource owns it unless the API explicitly transfers ownership.

## Translate only actionable I/O failures {#translate-errors}

The error union separates known outcomes and retains exception objects for failures whose diagnostic details matter:

```fsharp:line-numbers [ch21-exceptions-resources-io.fsx]
type ReadTextError =
    | PathNotFound of path: string
    | AccessDenied of path: string * cause: UnauthorizedAccessException
    | IoFailure of path: string * cause: IOException
```
The adapter performs the translation:

```fsharp:line-numbers [ch21-exceptions-resources-io.fsx]
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

The result says that these I/O outcomes are expected at this adapter. It does not claim file reading is pure. The operation still consults external state, can race with another process, and can encounter an exception not covered by this policy.

Holding an exception inside an internal error value preserves useful cause and stack data. If the error crosses a process or serialization boundary, expose a stable transport error and log or otherwise retain the cause on the service side; do not expose arbitrary exception objects as a public wire contract.

### Catch at the layer that can add meaning {#catch-layer}

A low-level helper often lacks enough context to decide whether “not found” is normal, a configuration error, or a security signal. Let the exception reach an adapter named for the operation. That adapter can attach the path and translate only the cases its caller understands.

Do not log the same exception at every layer. A common policy is either handle it and record the resulting outcome, or propagate it to a boundary that owns logging. Repeated log-and-rethrow creates duplicate incidents without adding information.

## Test both completion paths with real resources {#resource-tests}

The shared script creates a unique directory beneath `Path.GetTempPath()`, writes one file, and opens actual `StreamReader` instances:

```fsharp:line-numbers [ch21-exceptions-resources-io.fsx]
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

This is direct evidence for both control paths. It is stronger than asserting that a `use` keyword appears in source, and more portable than attempting to delete an open file—Unix-like systems and Windows have different open-file deletion behavior.

The outer `try/finally` owns directory cleanup. The directory name contains a fresh GUID, the target is a specific child of the platform temporary directory, and deletion occurs only for that resolved task-owned path. The final assertion confirms it no longer exists.

Testing with real temporary files proves the .NET boundary. Pure parsing tests should still use in-memory strings; they do not need a filesystem fixture.

## I/O has more than a success value {#io-contract}

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
| One expected operation can fail and caller needs a reason | `Result<'T, 'Error>` | The error is part of the typed contract |
| Several independent pure input checks should all report | Accumulating validation | Combination policy preserves multiple failures |
| A dependent workflow step fails | First-error `Result.bind` or explicit match | Later work lacks a valid prerequisite |
| A .NET API reports a recoverable condition by exception | Catch that specific exception at an adapter and translate | Aligns a foreign convention with caller policy |
| A programmer contract or invariant is broken | Exception, assertion, or process failure according to ownership | Ordinary callers usually cannot recover as a domain branch |
| An unexpected infrastructure failure occurs | Propagate with cause until an operational boundary can handle it | Avoid false, information-poor domain errors |
| A disposable resource is acquired | `use`/`using` plus a separate success/failure contract | Lifetime and result semantics are different axes |

These choices compose. A function may use `use` internally and return `Result`; disposal still happens when either branch is produced. A validator may return `Result<_, Error list>` without any I/O. An exception adapter may return `option` when the only translated condition is ordinary absence.

Avoid APIs such as `Result<'T, string>` by default. Strings are appropriate presentation values, but usually poor internal error models: they lose cases, structured context, and compiler-assisted handling.

## Keep parsing after acquisition {#parse-after-read}

A useful boundary sequence is:

```text
open + read + dispose
          ↓ Result<string, ReadTextError>
parse text with pure functions
          ↓ Result<DomainValue, ParseError>
map both into a workflow error union
```

Do not parse while deliberately keeping the file open unless streaming is required. A short lifetime reduces resource pressure and makes pure parser tests trivial. When streaming is required, the consumer must remain inside the resource scope, and its exception/cancellation behavior becomes part of that scope's contract.

Map errors at the workflow boundary rather than flattening both into “invalid file.” A missing file, denied access, malformed syntax, and violated domain rule may require different user messages, retry choices, and telemetry.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch21-exceptions-resources-io.fsx
```

Five deterministic lines prove success-path disposal, exception-path disposal, successful reading, missing-path translation, and final temporary-directory cleanup. Compare the exact output.

## Exercises {#exercises}

### Exercise 1: compose reading and parsing {#exercise-01}

Define a pure `parsePositiveSeats: string -> Result<int, SeatParseError>`. Compose it after `readText` so the workflow returns a union distinguishing `ReadFailure of ReadTextError` from `ParseFailure of SeatParseError`.

Test a valid file, a missing file, non-integer text, and zero. Explain why parsing does not need access to the reader or path.

### Exercise 2: audit a catch-all adapter {#exercise-02}

Review this code:

```fsharp
let read path =
    try Ok(File.ReadAllText path)
    with error -> Error error.Message
```

List the information and policy it loses. Rewrite it with a structured error union, specific handlers in correct inheritance order, and a decision about unrecognized exceptions. State where logging belongs.

### Exercise 3: prove nested disposal order {#exercise-03}

Write `withTwoReaders` using two `use` bindings. Inject opener functions that retain both actual `StreamReader` references. Prove both readers are disposed when the operation succeeds and when it raises.

Explain why reverse declaration order matters for resources where the second depends on the first, and why the operation must not return either reader.

[Read the chapter solutions](../solutions/ch-21-exceptions-resources-io).

## Model review {#model-review}

- `try/with` returns a value from the normal branch or the first matching exception handler.
- Catch exception subtypes before their base types, and translate only outcomes the caller understands.
- `reraise()` preserves the current exception stack; a pointless catch-and-raise adds risk without policy.
- `use` owns prompt disposal for one lexical scope on normal and exceptional exit.
- Disposal, exception translation, domain validation, and logging are separate decisions.
- Preserve structured error context and original causes instead of reducing every failure to a message.
- Real temporary resources verify the adapter; pure values verify parsing and domain logic.
- `option`, `Result`, accumulation, exceptions, and `use` answer different questions and can compose.

The next chapter applies the same separation to computations that complete later, comparing F# `Async<'T>` with .NET `Task<'T>`.

## Sources {#sources}

- [Microsoft Learn: F# `try/with`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-with-expression)
- [Microsoft Learn: F# `raise` and `reraise`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-raise-function)
- [Microsoft Learn: F# `try/finally`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/exception-handling/the-try-finally-expression)
- [Microsoft Learn: F# resource management with `use`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/resource-management-the-use-keyword)
- [Microsoft Learn: `StreamReader`](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader?view=net-10.0)
