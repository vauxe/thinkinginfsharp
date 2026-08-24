open System
open System.IO

// #region error-model
type ReadTextError =
    | PathNotFound of path: string
    | AccessDenied of path: string * cause: UnauthorizedAccessException
    | IoFailure of path: string * cause: IOException
// #endregion error-model

// #region resource-scope
let withReader
    (openReader: string -> StreamReader)
    path
    operation
    =
    use reader = openReader path
    operation reader
// #endregion resource-scope

// #region translate-errors
let readText path =
    try
        withReader File.OpenText path (fun reader -> reader.ReadToEnd())
        |> Ok
    with
    | :? FileNotFoundException
    | :? DirectoryNotFoundException -> Error(PathNotFound path)
    | :? UnauthorizedAccessException as cause -> Error(AccessDenied(path, cause))
    | :? IOException as cause -> Error(IoFailure(path, cause))
// #endregion translate-errors

let renderReadResult result =
    match result with
    | Ok text -> $"ok:{text}"
    | Error(PathNotFound _) -> "path-not-found"
    | Error(AccessDenied _) -> "access-denied"
    | Error(IoFailure(_, cause)) -> $"io-failure:{cause.GetType().Name}"

let readerIsDisposed (reader: StreamReader option) =
    match reader with
    | None -> false
    | Some value ->
        try
            value.Peek() |> ignore
            false
        with :? ObjectDisposedException ->
            true

// #region temp-tests
let tempName = Guid.NewGuid().ToString("N")

let tempDirectory =
    Path.Combine(
        Path.GetTempPath(),
        $"thinkinginfsharp-ch21-{tempName}"
    )

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

    let text =
        withReader openSuccess filePath (fun reader -> reader.ReadToEnd())

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
// #endregion temp-tests

printfn "Cleanup: removed=%b" cleanupRemoved
