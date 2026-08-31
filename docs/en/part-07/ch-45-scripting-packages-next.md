---
title: "Chapter 45: Scripting, Automation, Packages, and What Comes Next"
description: "Turn F# scripts into deterministic local automation, choose and lock packages deliberately, and build a practical path for continued F# mastery."
translationKey: part-07/ch-45-scripting-packages-next
---

# Chapter 45: Scripting, Automation, Packages, and What Comes Next {#overview}

An F# script is not a lesser kind of F# program. It uses the same language, FSharp.Core, and .NET runtime as a compiled project, but has less setup and a shorter execution path. That makes `.fsx` an excellent fit for exploration, repository maintenance, data repair, release checks, and small local tools.

The shorter path does not remove engineering obligations. A script can depend on hidden session state, the caller's working directory, mutable package feeds, unstable traversal order, ambiguous exit codes, or writes that occur on every run. Once other people or CI depend on it, those details are its interface.

This final chapter turns a real script into reliable local automation. It then covers package selection, locking the graph that ships, and deciding when a script should become a project or tool. The final sections offer a focused route for continuing to learn F#.

## Choose the smallest execution surface that preserves the contract {#execution-surface}

The right surface is the smallest one that makes the required behavior repeatable. “Smallest” refers to operational contract, not merely file count.

Keep the vocabulary layers distinct. FSI, `.fsx`, and F# Interactive belong to the F# toolchain. NuGet, PackageReference, target frameworks, and .NET tools belong to the .NET platform. CLI contracts, lock files, idempotence, and supply chains are general engineering concepts. They meet in one script, but they are not all F# language features.

| Surface | Best first use | Reproducibility boundary | Promotion signal |
|---|---|---|---|
| REPL submission | inspect one expression, type, or API | current FSI process and its hidden state | the result must be repeated or reviewed |
| `.fsx` script | local automation, experiment, migration, report | script, SDK, arguments, files, environment, and package sources | multiple modules, tests, publishing, or a stable public CLI appear |
| console project | maintained command, scheduled job, richer tests | project graph, target framework, lock file, build, and published artifact | installation and cross-repository reuse matter |
| local .NET tool | repository-wide executable with a stable command | tool manifest plus package restore and runtime compatibility | organization-wide distribution or API versioning grows |
| build DSL such as FAKE | named build targets and dependency graph | DSL/tool version, script dependencies, target graph, and invoked tools | the graph or custom integration justifies another abstraction |

Do not promote a 70-line script merely because projects look more serious. Promote it when callers rely on its command syntax or several files form an internal architecture. A locked restore, normal test discovery, or self-contained deployment is also a clear signal.

Likewise, do not compress a real application into one script to avoid an `.fsproj`. File order, public API boundaries, build properties, analyzers, test projects, and publishing are useful constraints once the program needs them.

### Use the REPL to answer one question {#repl-question}

`dotnet fsi` starts F# Interactive. A REPL submission is ideal for asking what signature the compiler infers, how a BCL method behaves, or whether a small transformation is plausible. The `;;` terminator belongs to interactive submission; a normal `.fsx` file does not need it.

The session remembers earlier bindings, opened namespaces, loaded files, referenced assemblies, and package resolutions. That is convenient while exploring and dangerous as evidence. Before preserving a result, put the necessary code in a script and execute it in a fresh process.

Do not choose Polyglot Notebooks or .NET Interactive as the default surface for new work: their maintainers deprecated them in March and April 2026 respectively and archived the repository. Plan a migration for existing notebooks. This chapter uses the still-supported FSI script path when execution must be repeatable.

### Use a script for a complete, reviewable operation {#script-operation}

A script should state its inputs, outputs, failure behavior, and possible side effects as clearly as a small application. It may still be concise. The repository's complete `examples/scripts/ch45-scripting-packages-next.fsx` manifest script has one file, uses only libraries included with .NET, creates no global installation, and runs from the repository root.

The useful distinction is not “throwaway versus production.” It is “bounded operation versus growing product.” A one-off data repair may need stricter validation, backups, and an audit trail than a long-lived developer convenience.

### Use a project when the build graph becomes part of the answer {#project-promotion}

Move to an F# console project when the build itself needs structure: several compiled files, analyzers, project references, controlled target frameworks, or `packages.lock.json`. Normal test discovery, generated documentation, publishing, trimming/AOT checks, and a supported command contract are also project-level needs. Pure functions can move almost unchanged; the important addition is a defined build and distribution boundary.

If contributors should invoke one versioned command from the repository, a local .NET tool can be appropriate. Commit its `.config/dotnet-tools.json`, restore it with `dotnet tool restore`, and remember that tools run with the user's authority. A versioned tool manifest controls which tool package is requested; it does not make untrusted tool code safe.

## Understand what FSI executes {#fsi-model}

Microsoft documents the command form as `dotnet fsi [options] [script-file [arguments]]`. When a script runs, `fsi.CommandLineArgs[0]` is the script path and later elements are its arguments. `--` tells FSI to treat the remaining tokens as script arguments when an argument might otherwise look like an FSI option.

The manifest script accepts these forms:

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx write ./artifacts ./artifacts.manifest.json
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx check ./artifacts ./artifacts.manifest.json
```

`--exec` runs the script and exits rather than remaining interactive. `write` converges the output toward desired content. `check` performs no write and returns exit code `2` when the output is absent or stale. Unexpected failures return `1`; success returns `0`.

### Working directory and source directory answer different questions {#script-paths}

Relative process paths are resolved from the caller's current working directory. That is useful for command arguments such as `./artifacts`, because the caller chooses their meaning. It also means a script invoked from another directory must not assume those paths are beside the script.

When a resource belongs to the script itself, anchor it with `__SOURCE_DIRECTORY__`. `__SOURCE_FILE__` identifies the current source file. Use caller-relative paths for inputs supplied by the caller and source-relative paths for assets shipped with the script. Convert both to absolute paths before work begins; do not silently mix the two models.

Environment variables, current culture, time zone, current time, random seeds, network state, and the installed SDK are inputs too. Read them once at the edge, validate them, and pass ordinary values inward when reproducibility matters.

### Directives are ordered compile inputs {#directives}

FSI processes script declarations in order. Its main directives are:

- `#load "helpers.fsx"` compiles and executes another script before later code uses its definitions;
- `#r "library.dll"` references an assembly file;
- `#I "directory"` adds an assembly search path for later references;
- `#r "nuget: PackageId, Version"` restores and references a NuGet package;
- conditional symbols such as `INTERACTIVE` can separate FSI-only declarations when a file is also compiled elsewhere.

These are not ordinary runtime function calls. A missing or incompatible reference prevents later script code from compiling. `#load` also executes the loaded script's top-level effects, so a “helper” that writes files during loading has hidden startup behavior.

Keep reusable loaded scripts free of top-level side effects. Put behavior in named functions and let one entry script start execution. If a growing set of `#load` directives starts recreating project file order, use a project.

## The manifest script: generate a stable artifact manifest {#x45}

The manifest script solves a practical local problem. It enumerates files beneath an artifact directory and writes their normalized relative paths, byte lengths, and SHA-256 digests to deterministic JSON. It can update the manifest or check that the current file matches.

The next four named code blocks appear in source order and come from that one complete file. Together they form the main implementation; they are not four independently executable snippets. The file first opens `System`, `System.IO`, `System.Security.Cryptography`, `System.Text`, and `System.Text.Json`, and each later block uses types or functions introduced earlier. Read them in sections, but run the complete `.fsx` file.

Its contract is deliberately narrow:

- input is one existing local directory and one output file path;
- directory traversal skips symbolic links and rejects a symbolic-link root;
- the output file itself is excluded if it sits under the source directory;
- paths use `/` and entries use ordinal path ordering on every platform;
- JSON has schema version `1`, UTF-8 without a BOM, and one final newline;
- unchanged desired content leaves the existing output untouched;
- replacement uses a uniquely named file in the output directory, then moves it over the destination;
- no-argument execution creates and removes a unique temporary fixture for its self-test.

### Model observable outcomes, not incidental steps {#manifest-model}

The script distinguishes planning data from write and check outcomes:

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
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
```
`ManifestPlan` contains structured entries and the exact text to write. `Updated` and `Unchanged` replace a Boolean whose meaning would need documentation. `Current` and `Stale` keep read-only CI checks distinct from mutation.

The model remains small because this is local automation. A public tool might add schema compatibility, structured diagnostics, cancellation, logging, and a stable serialized result. Those needs would be promotion signals.

### Make traversal and hashing policy explicit {#artifact-scan}

The filesystem adapter resolves full paths, uses the operating system's path equality for excluding the output, recursively skips reparse points, normalizes reported separators, and hashes each open stream:

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
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
```
Skipping links avoids accidentally walking outside the selected tree or entering a cycle. It is a policy, not a universal rule: a deployment format that intentionally includes links would need to record link targets safely instead.

Opening with `FileShare.Read` prevents cooperating Windows writers from modifying the file while it is hashed. This is not a transactional filesystem snapshot, especially across platforms. If producers may mutate the tree concurrently, first publish an immutable staging directory or use a storage mechanism with snapshot semantics.

SHA-256 lets a later consumer detect whether bytes differ from the recorded bytes. It does not identify the producer or detect coordinated replacement of both artifact and manifest. Authenticity requires a signature or another trusted channel; release provenance requires additional records.

### Separate deterministic planning from applying effects {#manifest-plan}

The planner renders JSON with `Utf8JsonWriter` instead of relying on unspecified reflection ordering. It sorts entries before rendering and fixes property order, casing, indentation, encoding, and newline policy:

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
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
```
The boundary still reads files, so `planManifest` is not pure. The important separation is that it computes one complete desired result before deciding whether to mutate the output. `renderManifest` itself is deterministic for the same entry array.

Stable output prevents noisy diffs and makes equality meaningful. Sorting after enumeration avoids inheriting filesystem order. Relative paths avoid embedding a developer's absolute directory. No timestamps, machine names, or random identifiers enter the final JSON.

### Write only when desired content differs {#idempotent-write}

The application layer compares existing and desired text. Only a difference creates a temporary file and replaces the destination:

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
let readExisting outputPath =
    if File.Exists outputPath then
        Some(File.ReadAllText(outputPath, Encoding.UTF8))
    else
        None

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
```
This gives the useful idempotence property: after one successful `write`, another `write` over unchanged inputs reports `Unchanged` and does not alter the output timestamp. The same-directory temporary file keeps the final move on one filesystem and narrows the window in which a partial destination is visible.

State the guarantee precisely: the script replaces the destination after a complete local write on the same filesystem. Durable flushes, concurrent-writer coordination, full permission and metadata preservation, crash durability, and interrupted network-filesystem recovery all require additional mechanisms.

`check` compares the same plan without writing. That makes CI failure actionable: exit `2` means regenerate or commit the manifest, while exit `1` means the operation itself failed. Scripts that always print an error but exit `0` break automation composition.

### Verify idempotence with a real temporary fixture {#script-evidence}

With no arguments, the manifest script creates two files in a unique directory under `Path.GetTempPath()`. It writes once, sets the output timestamp to a sentinel, writes again, checks without mutation, and verifies ordinal normalized paths. In `finally`, it removes only the directory it created.

This revision reran the complete script from the repository root on 2026-08-31 with .NET SDK 10.0.302:

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx
```

That run produced these ordered lines:

```text
First write: updated files=2
Second write: unchanged files=2
Check mode: current files=2
Stable timestamp: true
Manifest paths: nested/beta.bin, notes.txt
Cleanup: removed=true
```

Run the script in a fresh FSI process. Its output lets you check the temporary fixture, exact output order, idempotent second write, read-only current check, path normalization, and cleanup. It does not cover hostile directories, concurrent producers, millions of files, remote filesystems, signatures, or every Windows/Linux filesystem.

## Treat automation as a public interface as soon as another caller depends on it {#automation-interface}

A script may have no assembly API, but it still exposes contracts:

- command name, argument order, defaults, and help text;
- accepted path forms and whether paths are caller-relative;
- standard output for data, standard error for diagnostics, and exit codes;
- files created, replaced, or deleted and who is responsible for each;
- ordering, encoding, culture, time, and schema stability;
- package, SDK, tool, operating-system, and external-command assumptions;
- behavior under partial failure, cancellation, repeated execution, and concurrent invocation.

Document the pieces callers may automate against. Human-friendly wording can evolve; machine-consumed output needs a schema or an explicit unstable status. Do not ask CI to parse decorative logs when a JSON result or exit code is the real contract.

### Prefer convergence over a sequence of edits {#convergence}

Idempotent automation computes desired state from current inputs and converges toward it. This is stronger than “run these append operations in order.” Appending a generated line on every run, adding duplicate configuration entries, or renaming whatever file happens to match first accumulates history-dependent state.

Planning first also enables `check` and dry-run modes. The plan can be tested as data before application. When actions are destructive or externally visible, render the exact target set and require the caller's explicit mode rather than inferring permission from environment names.

Idempotence is not immunity to wrong inputs. A deterministic script can reliably generate the wrong file. Validate the source contract, test representative and failure cases, and keep reviewable diffs.

### Use the shell for composition and F# for typed decisions {#shell-boundary}

Shell scripts are excellent at invoking commands and connecting streams. They become less portable when data parsing, branching, escaping, collections, error models, or filesystem rules dominate. F# gives those decisions types and normal .NET APIs while still invoking external processes when appropriate.

Do not wrap every `dotnet build` in F# merely to say the build uses F#. A short task file may be clearer. Introduce F# when the task needs meaningful parsing, planning, validation, concurrency, or reusable policy.

When invoking a process, pass an argument list rather than constructing an unescaped shell string, capture exit status and bounded output, propagate cancellation, and decide which environment variables are inherited. Secrets must not appear in command lines or normal logs.

### Bound untrusted and expensive inputs {#automation-safety}

The manifest script targets a trusted local artifact tree. A tool exposed to untrusted paths would also need file-count and byte limits, permission policy, special-file handling, race analysis, timeout/cancellation, output-size bounds, and perhaps a sandbox. A checksum manifest is not a reason to read an attacker-controlled device file forever.

Resolve destructive targets before acting. Never derive a recursive delete root from an absent environment variable, broad wildcard, repository root, or home directory. Prefer task-owned temporary directories and recoverable moves. Validate that the resolved target remains inside the intended root.

Credentials belong in the environment's secret mechanism, not source, fixture files, manifests, exception messages, or generated reports. A local script runs with the invoking user's authority; “only a script” is not a security boundary.

## Add a package only after naming the missing capability {#package-choice}

The manifest script's first design question was whether it needed a package. `System.IO`, `SHA256`, and `Utf8JsonWriter` already satisfied the bounded contract, so the correct dependency count was zero beyond the SDK and FSharp.Core.

That choice is not anti-package minimalism. A maintained parser, protocol client, database driver, testing library, or framework can remove far more risk than it adds. The point is to compare a package against a written need rather than using package discovery as architecture.

### Evaluate fit before popularity {#package-scorecard}

For a candidate package, record at least:

| Question | What to inspect | Reject or run a spike when |
|---|---|---|
| Does its API solve the exact problem? | smallest representative call, error/cancellation model, data lifetime | the demo works only after large adapters or hidden global state |
| Does it support the target? | package target frameworks, runtime/native assets, AOT/browser/platform notes | the shipping target is absent or only assumed compatible |
| Who maintains it? | package owners, source repository, license, release history, issue/review activity | provenance or license cannot be established |
| What enters transitively? | full dependency graph, build/analyzer/content assets, native binaries | the closure is disproportionate or conflicts with the host |
| Is its operational model acceptable? | threads, network, files, reflection, generated code, logging, configuration | critical behavior cannot be observed or controlled |
| Can the team update and leave? | migration notes, API surface used, replacement seam, data formats | removal would require rewriting the domain or stored data |
| What has been verified? | focused test on the actual target plus restore/build/runtime checks | only a README snippet or download count supports the decision |

NuGet's official package-evaluation guidance points to version history, project/source links, owners, license, dependencies, usage, and vulnerability information. These signals cannot predict future maintenance. A popular package can be wrong for the target; a small package can be excellent when its contract and maintainers are clear.

Create a bounded adoption spike. Test the hardest representative behavior, one failure, target compatibility, and removal seam. Record the version and date examined because package state changes.

### `#r "nuget:"` is convenient, not a lock file {#script-packages}

FSI supports a package reference such as:

```fsharp
#r "nuget: PackageId, 1.2.3"
open PackageNamespace
```

Omitting the version asks for the highest available non-preview version at resolution time. That is useful for disposable exploration and unsuitable for a versioned automation contract. Put an exact direct version in a committed script.

The exact version in one `#r` directive does not create a repository `packages.lock.json` for the full transitive graph. It also relies on effective NuGet configuration, package sources, credentials, caches, and network availability. Do not describe a pinned directive as a locked restore.

FSI normally does not consume package build targets. Its documented `usepackagetargets=true` option enables them for packages authored to require that behavior. Enable it only for a clear, documented need: build targets execute during restore or build and widen the trust and compatibility surface.

If CI must review and reproduce the full dependency closure, move the automation into a project with PackageReference and a committed lock file. Another dependency manager is suitable only if its script workflow also provides a committed lock. A one-file script is not worth an unverifiable supply chain.

### Lock the graph that runs {#locking}

In an SDK-style application or tool project, specify direct PackageReference versions and enable NuGet lock-file generation. Commit `packages.lock.json`, then run `dotnet restore --locked-mode` in CI. Locked mode restores the recorded closure or fails when project dependencies would change it.

A lock file answers resolution, not trust or runtime correctness. It does not prove that a package is safe, licensed for the product, compatible with the target, or behaviorally correct. It also does not force a consuming application's graph to use a library project's private resolution; the top-level consumer resolves its own closure.

Keep SDK and tool versions explicit too. Pin the SDK in `global.json` and local tools in `.config/dotnet-tools.json`. Project lock files cover NuGet dependencies; a workspace lock file covers JavaScript tools. Use only the mechanisms the project needs.

Update intentionally: change one bounded set, regenerate the lock, inspect direct and transitive differences, read relevant release notes, run focused and full tests, and retain rollback. “Latest” is a query result, not a review policy.

### Treat restore as a supply-chain operation {#package-security}

Packages and .NET tools execute with meaningful authority through runtime code, build targets, analyzers, generators, native assets, or tool entry points. Use trusted sources, protect credentials, and review source configuration. When public and private feeds coexist, Package Source Mapping can constrain which source may serve each direct and transitive package ID.

NuGet audit compares resolved dependencies with known vulnerability data during restore. Address findings according to policy and keep audit sources available. No finding means “no matching known advisory was reported under this configuration,” not “the package is secure.”

Prefer repository-scoped configuration and local tools over undocumented machine state. Do not commit restored package caches or credentials. Preserve restore logs and lock diffs when a dependency incident must be explained.

## Read the F# ecosystem as layers, not a shopping list {#ecosystem-map}

The ecosystem explored in Part VII spans several responsibility layers:

| Layer | Examples from this book | First compatibility question |
|---|---|---|
| F# language and FSharp.Core | unions, pattern matching, collections, async, quotations | which language/compiler and FSharp.Core contract is required? |
| .NET runtime and BCL | files, JSON, HTTP, tasks, diagnostics, cryptography | which TFM, runtime, OS, and API behavior is required? |
| Microsoft platform framework | ASP.NET Core, hosting, containers, Aspire integrations | which supported platform version and deployment model applies? |
| F# community library | FsCheck, Giraffe/Falco/Oxpecker, FSharp.Data, Elmish | which API value offsets package and maintenance cost? |
| cross-language UI/toolchain | Fable/npm/browser, Avalonia backends, Unity Editor/IL2CPP | which compiler, host, native tool, and release matrix must agree? |
| repository automation | scripts, local tools, FAKE, Paket, CI runner | which tool controls ordering, restore, credentials, and verification? |

F# participates in the entire NuGet ecosystem, not only packages with “FSharp” in their names. Many ordinary .NET libraries work directly. Inspect how the API represents nulls, delegates, tasks, exceptions, mutation, reflection, overloads, serialization, and C#-oriented builders; a narrow adapter may help.

Conversely, an F#-native package is not automatically the best fit. Check target frameworks, release history and tests, transitive assets, and team comprehension exactly as for any other dependency.

### FAKE and Paket solve different problems {#fake-paket}

[FAKE](https://fake.build/) is an F# build-task DSL with target dependencies and modules for common tools. Choose it when a named target graph, reusable build integrations, or richer orchestration materially clarifies the build. A plain repository task file may remain clearer for four linear commands.

[Paket](https://fsprojects.github.io/Paket/) is an alternative .NET dependency manager with its own dependency and lock model, including script integration. Choose it because that model or an existing repository requires it, not because F# code must use an F#-associated package manager. Do not let NuGet and Paket manage the same dependency set without a clear division.

Both tools add concepts, bootstrapping, versions, and failure modes. Their value is real when those costs replace greater accidental complexity. Run a spike against the actual CI and developer environments before migration.

## Recognize advanced features without front-loading them {#advanced-recognition}

You can read most production F# by mastering the foundations already covered: types, functions, pattern matching, collections, modules, effects, async/task, .NET boundaries, and tests. Four features often look more mysterious than the problem that introduced them. For now, learn their recognition signals and stop conditions:

| Feature | Recognition signal | Why you may encounter it | What to do next |
|---|---|---|---|
| quotations | `<@ expression @>`, `<@@ expression @@>`, `Expr<'T>`, quotation patterns | a library represents F# code as data for a DSL, query, analysis, or generation | distinguish constructing/traversing an expression tree from executing it; read the library contract |
| SRTP | `inline` plus static/member constraints; current simplified syntax may use `'T`, while older/complex forms may show `^T` | operators or member-based compile-time abstraction | do not confuse it with ordinary generics; inspect inferred constraints and specialization cost |
| flexible types | `#SomeBase` inside a type annotation, equivalent to a subtype-constrained generic | a higher-order or nested input should accept any subtype/interface implementation | distinguish it from preprocessor directives and ordinary upcasts; keep public signatures readable |
| byref and Span | `&value`, `byref<'T>`, `inref<'T>`, `outref<'T>`, `Span<'T>`, `ReadOnlySpan<'T>` | interop or a measured synchronous buffer/copy hotspot | obey stack/lifetime rules; do not capture across async or heap boundaries; measure before adopting |

Quotations represent expressions; they do not execute themselves. SRTP specializes inline code at compile time and is not needed for everyday `'T` functions. Flexible `#Type` syntax expresses compatibility in an object hierarchy, not a comment or compiler command. Byref-like values trade ordinary composability for constrained lifetimes.

[Appendix H: Advanced Feature Recognition Index](../appendices/h-advanced-index) gives the focused entry points and cross-links. It deliberately does not turn these features into a second introductory course. Chapter 11 anchors generic constraints and SRTP; Chapter 31 anchors measured Span/byref decisions.

## Continue by building feedback loops, not a feature checklist {#learning-next}

Finishing a book gives you a map, not automatic fluency. Fluency comes from repeated cycles in which the compiler, tests, runtime results, and another reader can contradict your first design.

Use this loop:

1. choose one real, bounded problem whose failure matters enough to reveal tradeoffs;
2. model inputs, valid states, expected failure, and effects before choosing a framework;
3. build the smallest vertical slice through the real boundary;
4. inspect inferred signatures and assign ambiguous responsibilities;
5. test pure rules, adapters, failure paths, and the actual target proportionally;
6. profile or instrument before changing representation for performance;
7. review the dependency and deployment graph, not just source code;
8. record what was verified, what remains unknown, and what would reverse the choice;
9. simplify after learning, then repeat with a slightly harder boundary.

### Choose a project track from the risk you want to learn {#project-tracks}

| Track | First project | Harder second slice | Chapters to revisit |
|---|---|---|---|
| language and modeling | CLI that validates and transforms a versioned local format | migration across three schema versions with properties | 7–18, 28–30 |
| backend and distributed systems | authenticated API around a pure workflow | idempotent persistence, retries, tracing, container release | 20–24, 33–39, 42 |
| data and analytics | reproducible ingest/clean/report pipeline | schema drift, large data, notebook-to-project promotion | 14–15, 29–31, 40 |
| browser application | Fable state machine with one real API | URL state and navigation, cancellation, accessibility, bundle budget | 20, 22–24, 41 |
| desktop or mobile | Avalonia desktop slice with pure update logic | packaging, platform service, signed target artifact | 25–32, 43 |
| game and simulation | deterministic F# rules behind a thin host | replay, save migration, frame profile, real IL2CPP Player | 12, 20, 24, 27–31, 44 |
| tooling and libraries | promote the manifest script into a tested console tool | stable API/CLI, package publication, upgrade compatibility | 16–17, 26–31, this chapter |

Do not build seven starter projects. Pick the track whose unknowns resemble your work or curiosity, then deepen it until deployment and maintenance change your design.

### Learn to navigate sources at three levels {#source-reading}

Use the language reference for exact syntax and constraints, FSharp.Core API documentation for function signatures and behavior, and the relevant .NET/platform documentation for runtime boundaries. Then inspect the package's own source, release notes, tests, and issues when a community abstraction enters the decision.

Run small compiler experiments instead of arguing from memory. Record the SDK and package version. A blog post can teach a durable idea while its setup commands, syntax, or compatibility table has aged; separate the idea from the current contract.

Read unfamiliar F# from types outward: public signatures, domain cases, pure transformations, effect ports, composition root, then implementation detail. When a clever operator hides the data flow, ask for the inferred type and rewrite one call explicitly.

### Seek feedback that can change the design {#community-feedback}

Ask reviewers one falsifiable question, for example:

- Can this state be constructed illegally?
- Which token governs cancellation?
- What happens after the second run?
- Which Player test validates this package?

A generic request to “review my F#” produces generic approval.

When asking a community, provide a minimal reproduction, full diagnostics, SDK/package versions, target, expected behavior, actual behavior, and what you already ruled out. This respects other people's time and makes the answer useful to the next reader.

Contribute back at the smallest durable boundary: improve a reproduction, documentation example, test, issue, package metadata, or focused fix. You do not need compiler expertise to participate in the ecosystem.

## Exercises {#exercises}

### Exercise 1: add exclusion without losing determinism {#exercise-01}

Extend the manifest script design to accept repeatable `--exclude GLOB` rules for generated logs and symbol files. Define glob semantics, separator/case policy, whether rules match files or directories, behavior for invalid patterns, how excluded links are reported, and how the rule set appears in the manifest schema. Preserve `write`/`check` agreement, stable ordering, output exclusion, idempotence, and bounded tests across Windows and Unix-like paths. Decide whether to implement a tiny documented matcher or adopt a package.


::: details Answer

The word “glob” is not a complete contract. Shells and libraries use different rules for separators, case, hidden files, character classes, recursion, malformed patterns, and directory pruning. Define those rules before adding `--exclude`.

#### Contract {#exclusion-contract}

One acceptable version-2 contract is:

- `--exclude PATTERN` may appear more than once after the mode and before the two positional path arguments;
- patterns and candidate paths use `/` regardless of host OS;
- matching is ordinal and case-sensitive on every platform;
- `*` matches zero or more characters inside one path segment;
- `?` matches exactly one character other than `/`;
- `**` is valid only as a complete segment and matches zero or more complete segments;
- `[abc]`, brace expansion, escaping, absolute paths, empty segments, `.` segments, and `..` segments are unsupported and rejected;
- a pattern names files; `logs/**` is the explicit way to exclude a directory tree;
- the output file is excluded by its resolved identity before user rules are evaluated;
- symbolic links remain skipped by the existing traversal policy, independent of exclusions;
- duplicate patterns are removed, then normalized patterns are sorted ordinally;
- an invalid pattern fails before hashing or writing and returns exit `2` as a usage error.

Case-sensitive matching may surprise Windows users, but it gives the repository one rule on every platform. An alternative may follow each filesystem's case behavior, provided that the platform dependency is documented and tested.

#### Schema and planning {#exclusion-schema}

Version the manifest because exclusions change what “complete” means:

```json
{
  "schemaVersion": 2,
  "exclusions": ["**/*.pdb", "logs/**"],
  "files": []
}
```

Recording normalized rules distinguishes manifests that currently list the same files but apply different exclusion policies. It also explains why an artifact is absent.

Refactor the plan boundary conceptually into:

```text
parse arguments
  -> validate and normalize patterns
  -> enumerate without following links
  -> normalize each relative path
  -> apply exclusions
  -> hash and sort included files
  -> render one version-2 desired document
```

`write` and `check` must call the same planner. Separate implementations will drift. Keep output-file exclusion outside the user rules so callers cannot accidentally make the manifest hash itself.

Directory pruning is an optimization that can change behavior. A `logs/**` rule can safely prune `logs`, but a future matcher with negation or include rules might still need to enter it. This contract has no negation, so tested prefix analysis may prune. The first implementation can simply filter file paths after traversal.

#### Matcher choice {#matcher-choice}

Do not translate arbitrary user patterns directly into unbounded regular expressions. Either implement only the grammar above with a linear segment matcher and a pattern-length limit, or test a maintained glob package against every stated rule.

For this answer, choose a package only if a spike proves all of the following:

- explicit ordinal case-sensitive comparison is supported;
- `**` and separator normalization match the written contract;
- malformed patterns can be rejected before filesystem effects;
- traversal can remain under the manifest script's link and root policy rather than letting the library walk independently;
- the package target and transitive graph fit the promoted console project;
- a lock file and locked restore reproduce the graph.

If no candidate meets those constraints, the intentionally tiny grammar is safer than advertising full glob compatibility. Name it “artifact pattern syntax,” document it, and reject unsupported syntax instead of approximating a shell.

#### Test matrix {#exclusion-tests}

Use pure path/pattern tests plus real temporary directories:

| Case | Expected result |
|---|---|
| no rules | version 2 contains the same two files as the existing manifest script |
| `**/*.pdb` | nested and root `.pdb` files are absent; `.PDB` remains |
| `logs/**` | every file below normalized `logs/` is absent |
| `a/?eta.bin` | `a/beta.bin` matches; `a/longbeta.bin` does not |
| duplicate rules in different input order | normalized JSON and digest rows are byte-identical |
| backslash in a pattern | usage failure occurs before output mutation |
| `../secret` or absolute pattern | validation rejects traversal semantics |
| output inside source | output is still excluded even when no rule names it |
| second write | `Unchanged` and the sentinel timestamp remains |
| stale check | exit `2`, no write, previous output bytes unchanged |
| link to an outside tree | link is skipped; outside file is never hashed |

Simulate Windows-style strings in pure tests on every OS, then add filesystem tests on actual Windows and Unix-like runners. A Linux-only path test does not establish cross-platform behavior.

:::

### Exercise 2: write a package adoption record {#exercise-02}

Your team wants a command-line parser for the promoted manifest tool. Compare hand-written parsing with two current NuGet candidates. Record required syntax, help/error behavior, target frameworks, package/source identity, license, maintenance, transitive/build assets, vulnerabilities, trimming/AOT needs, test ergonomics, direct version, lock procedure, update owner, and removal seam. Build one focused spike for the hardest requirement and state a reversible decision.


::: details Answer

Start with the requirements, not the candidates. The promoted tool needs:

- `write` and `check` subcommands;
- two required path arguments and repeatable `--exclude` options;
- generated help and predictable usage failures;
- a parser that tests can call without terminating the process;
- standard .NET 10 publishing.

Shell completion and native AOT are desirable, but they are not release requirements.

#### Candidate record as of 2026-08-31 {#candidate-record}

The official NuGet pages reviewed for this answer show:

| Choice | Reviewed version | Relevant fit | Cost or open question |
|---|---:|---|---|
| hand-written parser | repository code | no dependency graph; exact control over today's three arguments | we must maintain help, repeated options, aliases, diagnostics, and future subcommands |
| [Argu](https://www.nuget.org/packages/Argu) | 6.2.5 | F#-oriented declarative parser using discriminated unions; targets .NET Standard 2.0 | package last updated in December 2024; brings FSharp.Core and `System.Configuration.ConfigurationManager`; trimming/AOT behavior needs a real spike |
| [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) | 2.0.11 | commands, options, arguments, validation, help, completions, and async actions; targets .NET 8 and .NET Standard 2.0 | API uses object and builder patterns common in C#; F# overload/null adaptation and help/error stability need a spike |

Both package versions are facts checked on that date; neither is a dependency of the book site. The manifest script verifies only its BCL parser until the package-specific spikes are run.

Do not treat download counts as a measure of correctness. Inspect maintainers, MIT licenses, source repositories, dependency tabs, release history, advisories, and the exact `.nupkg`. Then run a restore audit with the adopting project's effective package sources.

#### Focused spike {#parser-spike}

Create a disposable `net10.0` F# console project for each package. Lock its exact direct version and resolved dependency graph. Wrap the parser in a testable adapter from `string array` to:

```fsharp
type Command =
    | Write of source: string * output: string * exclusions: string list
    | Check of source: string * output: string * exclusions: string list
    | ShowHelp

type ParseFailure =
    { ExitCode: int
      StandardError: string }
```

The library-specific types stop at this adapter. Manifest planning receives only `Command`. This keeps removal from touching hashing, schema, or filesystem policy.

Run the same golden vectors against hand parsing, Argu, and System.CommandLine:

- valid `write` and `check` with zero, one, and three exclusions;
- options before and after positional arguments if the contract permits both;
- `--help`, unknown option, missing source, duplicate non-repeatable option, and an invalid pattern;
- a path beginning with `-` after the `--` terminator;
- Unicode and whitespace-bearing paths passed as already-separated argument tokens;
- exact exit category and destination stream, while allowing deliberately non-stable decoration to be normalized;
- published executable invocation on Windows and one Unix-like target;
- trimming and native AOT only if they become declared release requirements.

Record the restore, lock-file diff, build warnings, package audit, published size, startup time, and amount of adapter code. Do not infer AOT support from target-framework compatibility.

#### Reversible decision {#package-decision}

Keep the hand-written parser in the manifest script because the current contract has only a mode and two paths. For the promoted tool, provisionally choose System.CommandLine `2.0.11` if it passes every test vector. Its command model fits the planned subcommands, repeated options, and generated help, and its .NET project home makes the maintenance path clear.

This is not a universal preference over Argu. Choose Argu if the team values a DU-declared F# API and its spike produces clearer code with acceptable maintenance and deployment results. The adapter and test matrix should decide, not language branding.

The adoption change should include:

- an exact `PackageReference` version in the console project;
- committed `packages.lock.json` and CI `dotnet restore --locked-mode`;
- effective source and Package Source Mapping review;
- restore audit with warnings handled by policy;
- one module owning every package-specific type;
- golden CLI contract tests and a published-process smoke test;
- a named maintainer and a quarterly review trigger;
- a removal note: replace only the parsing adapter, retain `Command` and all core functions.

If the package fails a required vector, retain hand parsing or test the other candidate. Do not expand the public CLI merely to match a library's defaults.

:::

### Exercise 3: plan the next twelve weeks {#exercise-03}

Choose one project track from this chapter. Define three four-week increments that each end in executable evidence, not reading alone. Include the F# concepts to revisit, one real .NET or platform boundary, tests and diagnostics, package budget, deployment or distribution target, review question, and a criterion for simplifying or reversing the design. Place advanced features only where a measured problem demands them.


::: details Answer

This example chooses the tooling-and-libraries track. Each four-week increment ships a usable boundary and ends with a review that can reduce scope.

#### Weeks 1–4: promote the manifest script without changing its semantics {#weeks-01-04}

**Outcome:** a `net10.0` console project with `Manifest.Core`, a thin CLI, and a published executable that preserves the manifest script's schema version 1.

Work includes:

- move manifest entries, rendering, planning outcomes, and path normalization into ordered `.fs` modules;
- isolate filesystem and console I/O behind narrow functions;
- add example-based tests for rendering and process-level tests for exits `0`, `1`, and `2`;
- run Windows and Linux/macOS temporary-directory tests for separators, links where supported, and output exclusion;
- publish a framework-dependent artifact and run it outside the source tree;
- retain the script as a small compatibility launcher only if users need it; otherwise document the new command.

Revisit Chapters 9, 16–18, 21, 26, 28, and 30. Use no new runtime package. The real boundary is filesystem plus process CLI. Diagnostics record command, exit, stderr category, SDK, OS, and output hash.

Review question: “Can an invalid argument or filesystem failure produce a partial destination or a success exit?” Reverse the promotion if the project adds complexity without improving tests, distribution, or reproducibility.

#### Weeks 5–8: add versioned exclusions and dependency verification {#weeks-05-08}

**Outcome:** schema version 2, explicit exclusion semantics from Exercise 1, backward-readable version 1 manifests, and a locked parser decision.

Work includes:

- model `ManifestV1` and `ManifestV2` separately and define one directional upgrade;
- add normalized exclusion rules to desired state and keep `write`/`check` on one planner;
- execute the parser spike from Exercise 2 before adding any package;
- if adopted, commit the exact package version and complete lock file, plus the restore audit results;
- fuzz-test or property-test ordering, duplicate rules, and render/parse stability with bounded generators;
- add a migration fixture and reject unknown future schema versions without overwriting them;
- measure traversal and hashing on a representative tree before parallelizing.

Revisit Chapters 10–15, 27–31, and 37. Package budget is one CLI parser and zero glob packages unless the matcher spike proves otherwise. The real boundary is schema compatibility plus cross-platform matching.

Review question: “Can version 2 explain every omitted file and preserve a version 1 artifact without silent reinterpretation?” Remove the parser package if its adapter and graph exceed the behavior it replaces.

#### Weeks 9–12: distribute and operate the tool {#weeks-09-12}

**Outcome:** a repository-local tool or versioned executable consumed by a second fixture repository, with reproducible restore/build, release notes, and rollback.

Work includes:

- choose local tool versus ordinary published executable from installation needs;
- pin the tool manifest or artifact version and test clean-machine restoration from an approved source;
- add structured optional JSON diagnostics while keeping human stderr separate;
- define cancellation for large scans and a maximum supported file count/output size;
- build an unsigned local package/feed fixture so no proprietary account is required;
- produce an SBOM or dependency inventory if release policy requires it, without calling it provenance by itself;
- test upgrade from the previous command and rollback to it;
- ask a fresh reviewer to reproduce one success and one stale-manifest failure from documentation alone.

Revisit Chapters 20–24, 27, 30–32, 38, and 45. The package budget remains the approved parser; every new package needs a separate adoption record. The real boundary is installation, source trust, cancellation, and compatibility.

Review question: “Can a new contributor restore, verify, upgrade, diagnose, and remove this tool without undocumented machine knowledge?” If not, reduce the distribution scope or assign responsibility for the missing work.

#### Advanced-feature budget {#advanced-budget}

None of the three increments requires quotations, SRTP, flexible types, or custom byrefs. Ordinary records, unions, and functions are sufficient. Use interfaces to isolate side effects, tasks to support cancellation, and arrays or streams for data movement.

Consider `Span` only if profiling shows that boundary copying dominates and the lifetime is demonstrably synchronous. Consider SRTP only if several concrete algorithms need the same member-constrained abstraction. Quotations make sense only if the product consumes or produces expression trees. Flexible types may appear in a library signature; identify them before deciding whether to expose them.

That restraint is part of the plan, not missing ambition. The learning goal is to ship increasingly trustworthy F#, then deepen a language feature when the system supplies a reason.

:::


Part VII is now complete. The appendices turn the book into a working reference: environment setup, syntax, collections, C# migration, compiler diagnostics, terminology, solution review, and the advanced-feature recognition index.

## Sources {#sources}

- [Microsoft Learn: Interactive programming with F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [.NET Interactive: Polyglot Notebooks and .NET Interactive deprecation announcement](https://github.com/dotnet/interactive/issues/4163)
- [Microsoft Learn: PackageReference and lock-file behavior](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- [Microsoft Learn: Find and evaluate NuGet packages](https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages)
- [Microsoft Learn: Audit package dependencies](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [Microsoft Learn: Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [Microsoft Learn: .NET tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [FAKE documentation](https://fake.build/)
- [Paket documentation](https://fsprojects.github.io/Paket/)
- [Microsoft Learn: code quotations](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations)
- [Microsoft Learn: statically resolved type parameters](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters)
- [Microsoft Learn: flexible types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types)
- [Microsoft Learn: byrefs and byref-like structs](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn: Tour of F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
- [FSharp.Core API documentation](https://fsharp.github.io/fsharp-core-docs/)
