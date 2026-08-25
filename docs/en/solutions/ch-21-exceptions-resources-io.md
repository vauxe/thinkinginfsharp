---
title: "Chapter 21 Solutions"
description: "Compose resource-safe reading with pure parsing, replace catch-all strings with structured policy, and verify two-reader disposal on success and failure."
translationKey: solutions/ch-21-exceptions-resources-io
---

# Chapter 21 Solutions {#overview}

Keep resource acquisition short, then hand plain data to pure parsing. Translate only exceptions that have a truthful typed meaning for this caller; every other failure should retain its original operational identity.

[Return to Chapter 21](../part-04/ch-21-exceptions-resources-io).

## Exercise 1: compose reading and parsing {#exercise-01}

### Define parsing without I/O {#exercise-01-parser}

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

### Preserve which phase failed {#exercise-01-workflow}

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

The four required tests should assert these shapes:

| Fixture | Expected result |
|---|---|
| file containing `"3"` | `Ok 3` |
| missing file | `Error(ReadFailure(PathNotFound path))` |
| file containing `"oops"` | `Error(ParseFailure(SeatsNotInteger "oops"))` |
| file containing `"0"` | `Error(ParseFailure(SeatsNotPositive 0))` |

Create all files beneath one unique temporary directory and remove that exact directory in `finally`. `readText` disposes its reader before `parsePositiveSeats` runs, so parser success or failure cannot extend the file handle's lifetime.

## Exercise 2: audit a catch-all adapter {#exercise-02}

### Identify what the string erases {#exercise-02-audit}

The catch-all version loses:

- the exception's runtime type and inheritance category;
- its stack trace and inner exception;
- structured context such as path and operation;
- the distinction between missing, denied, malformed, canceled, and unexpected failure;
- an explicit decision about which conditions are recoverable;
- stable handling, because localized or version-dependent messages are presentation text.

It may also catch programming errors from code added later to the `try` block and misreport them as a file read failure.

### Make the translation policy narrow {#exercise-02-rewrite}

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

## Exercise 3: prove nested disposal order {#exercise-03}

### Keep both readers inside the scope {#exercise-03-scope}

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

## What to notice {#what-to-notice}

- Read errors and parse errors remain distinct while composing through `Result.bind`.
- A specific exception translation is policy; a catch-all string is information loss.
- `use` scopes both readers even when the operation raises.
- Resource dependency should follow declaration order so reverse disposal is safe.
- Temporary-file cleanup surrounds the test; it does not belong to the pure parser.
