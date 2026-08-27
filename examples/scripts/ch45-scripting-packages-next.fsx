open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

// #region manifest-model
type ManifestEntry =
    { Path: string
      Bytes: int64
      Sha256: string }

type ManifestPlan =
    { Entries: ManifestEntry array
      Json: string }

type WriteOutcome =
    | Updated of fileCount: int
    | Unchanged of fileCount: int

type CheckOutcome =
    | Current of fileCount: int
    | Stale of fileCount: int
// #endregion manifest-model

// #region artifact-scan
let pathComparer =
    if OperatingSystem.IsWindows() then
        StringComparer.OrdinalIgnoreCase
    else
        StringComparer.Ordinal

let samePath left right =
    pathComparer.Equals(Path.GetFullPath left, Path.GetFullPath right)

let isReparsePoint (attributes: FileAttributes) =
    attributes.HasFlag FileAttributes.ReparsePoint

let rec regularFilesUnder directory =
    seq {
        for path in Directory.EnumerateFileSystemEntries directory do
            let attributes = File.GetAttributes path

            if not (isReparsePoint attributes) then
                if attributes.HasFlag FileAttributes.Directory then
                    yield! regularFilesUnder path
                else
                    yield path
    }

let normalizedRelativePath root path =
    Path
        .GetRelativePath(root, path)
        .Replace(Path.DirectorySeparatorChar, '/')
        .Replace(Path.AltDirectorySeparatorChar, '/')

let hashFile path =
    use input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
    let length = input.Length

    let digest =
        SHA256.HashData input
        |> Convert.ToHexString
        |> fun text -> text.ToLowerInvariant()

    length, digest
// #endregion artifact-scan

// #region manifest-plan
let renderManifest (entries: ManifestEntry array) =
    use buffer = new MemoryStream()

    use writer = new Utf8JsonWriter(buffer, JsonWriterOptions(Indented = true))

    writer.WriteStartObject()
    writer.WriteNumber("schemaVersion", 1)
    writer.WriteStartArray("files")

    for entry in entries do
        writer.WriteStartObject()
        writer.WriteString("path", entry.Path)
        writer.WriteNumber("bytes", entry.Bytes)
        writer.WriteString("sha256", entry.Sha256)
        writer.WriteEndObject()

    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()

    Encoding.UTF8.GetString(buffer.ToArray()) + "\n"

let planManifest sourceDirectory outputFile =
    let sourceRoot = Path.GetFullPath sourceDirectory
    let outputPath = Path.GetFullPath outputFile

    if not (Directory.Exists sourceRoot) then
        invalidArg (nameof sourceDirectory) $"Source directory does not exist: {sourceRoot}"

    if isReparsePoint (File.GetAttributes sourceRoot) then
        invalidArg (nameof sourceDirectory) $"Source directory must not be a symbolic link: {sourceRoot}"

    let entries =
        regularFilesUnder sourceRoot
        |> Seq.filter (fun path -> not (samePath path outputPath))
        |> Seq.map (fun path ->
            let length, digest = hashFile path

            { Path = normalizedRelativePath sourceRoot path
              Bytes = length
              Sha256 = digest })
        |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Path, right.Path))
        |> Seq.toArray

    { Entries = entries
      Json = renderManifest entries }
// #endregion manifest-plan

let readExisting outputPath =
    if File.Exists outputPath then
        Some(File.ReadAllText(outputPath, Encoding.UTF8))
    else
        None

// #region idempotent-write
let replaceFromSameDirectory (outputPath: string) (content: string) =
    let outputDirectory =
        match Path.GetDirectoryName outputPath with
        | null -> invalidArg (nameof outputPath) "Output path must include a directory."
        | directory -> directory

    Directory.CreateDirectory outputDirectory |> ignore

    let temporaryPath =
        Path.Combine(outputDirectory, $".{Path.GetFileName outputPath}.{Guid.NewGuid():N}.tmp")

    try
        File.WriteAllText(temporaryPath, content, UTF8Encoding(false))
        File.Move(temporaryPath, outputPath, overwrite = true)
    finally
        if File.Exists temporaryPath then
            File.Delete temporaryPath

let writeManifest sourceDirectory outputFile =
    let outputPath = Path.GetFullPath outputFile
    let plan = planManifest sourceDirectory outputPath

    match readExisting outputPath with
    | Some existing when existing = plan.Json -> Unchanged plan.Entries.Length
    | _ ->
        replaceFromSameDirectory outputPath plan.Json
        Updated plan.Entries.Length

let checkManifest sourceDirectory outputFile =
    let outputPath = Path.GetFullPath outputFile
    let plan = planManifest sourceDirectory outputPath

    match readExisting outputPath with
    | Some existing when existing = plan.Json -> Current plan.Entries.Length
    | _ -> Stale plan.Entries.Length
// #endregion idempotent-write

// #region demo
let runDemo () =
    let fixtureRoot =
        Path.Combine(Path.GetTempPath(), $"thinkinginfsharp-ch45-{Guid.NewGuid():N}")

    let sourceDirectory = Path.Combine(fixtureRoot, "artifacts")
    let nestedDirectory = Path.Combine(sourceDirectory, "nested")
    let outputFile = Path.Combine(fixtureRoot, "artifacts.manifest.json")
    let mutable cleanupRemoved = false

    try
        Directory.CreateDirectory nestedDirectory |> ignore
        File.WriteAllText(Path.Combine(sourceDirectory, "notes.txt"), "alpha\n", UTF8Encoding(false))
        File.WriteAllBytes(Path.Combine(nestedDirectory, "beta.bin"), [| 0uy; 1uy; 2uy; 255uy |])

        let first = writeManifest sourceDirectory outputFile

        let sentinel = DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        File.SetLastWriteTimeUtc(outputFile, sentinel)
        let timestampBefore = File.GetLastWriteTimeUtc outputFile

        let second = writeManifest sourceDirectory outputFile
        let timestampAfter = File.GetLastWriteTimeUtc outputFile
        let checkResult = checkManifest sourceDirectory outputFile
        let plan = planManifest sourceDirectory outputFile

        assert (first = Updated 2)
        assert (second = Unchanged 2)
        assert (checkResult = Current 2)
        assert (timestampBefore = timestampAfter)
        assert (plan.Entries |> Array.map _.Path = [| "nested/beta.bin"; "notes.txt" |])

        printfn "First write: updated files=2"
        printfn "Second write: unchanged files=2"
        printfn "Check mode: current files=2"
        printfn "Stable timestamp: %b" (timestampBefore = timestampAfter)
        printfn "Manifest paths: %s" (plan.Entries |> Array.map _.Path |> String.concat ", ")
    finally
        if Directory.Exists fixtureRoot then
            Directory.Delete(fixtureRoot, recursive = true)

        cleanupRemoved <- not (Directory.Exists fixtureRoot)

    printfn "Cleanup: removed=%b" cleanupRemoved
// #endregion demo

// #region cli
let usage () =
    eprintfn "Usage: dotnet fsi --exec %s [write|check] SOURCE_DIRECTORY OUTPUT_FILE" fsi.CommandLineArgs[0]

let runCommand arguments =
    match arguments with
    | [||] -> runDemo ()
    | [| "write"; sourceDirectory; outputFile |] ->
        match writeManifest sourceDirectory outputFile with
        | Updated count -> printfn "Manifest updated: files=%d output=%s" count (Path.GetFullPath outputFile)
        | Unchanged count -> printfn "Manifest unchanged: files=%d output=%s" count (Path.GetFullPath outputFile)
    | [| "check"; sourceDirectory; outputFile |] ->
        match checkManifest sourceDirectory outputFile with
        | Current count -> printfn "Manifest current: files=%d output=%s" count (Path.GetFullPath outputFile)
        | Stale count ->
            eprintfn "Manifest stale: files=%d output=%s" count (Path.GetFullPath outputFile)
            Environment.ExitCode <- 2
    | _ ->
        usage ()
        Environment.ExitCode <- 2

try
    let suppliedArguments = fsi.CommandLineArgs |> Array.skip 1

    let arguments =
        if suppliedArguments.Length > 0 && suppliedArguments[0] = "--" then
            suppliedArguments[1..]
        else
            suppliedArguments

    runCommand arguments
with error ->
    eprintfn "Manifest failed: %s" error.Message
    Environment.ExitCode <- 1
// #endregion cli
