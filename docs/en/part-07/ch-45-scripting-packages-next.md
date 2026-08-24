---
title: "Chapter 45: Scripting, Automation, Packages, and What Comes Next"
description: "Turn F# scripts into deterministic local automation, choose and lock packages deliberately, and build a practical path for continued F# mastery."
translationKey: part-07/ch-45-scripting-packages-next
kind: chapter
part: 7
chapter: 45
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch45-scripting-packages-next
exerciseIds:
  - ch45-exercise-01
  - ch45-exercise-02
  - ch45-exercise-03
termIds: []
sources:
  - id: microsoft-fsi
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-25"
  - id: microsoft-fsi-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options
    checked: "2026-08-25"
  - id: microsoft-package-reference
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
    checked: "2026-08-25"
  - id: microsoft-package-evaluation
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages
    checked: "2026-08-25"
  - id: microsoft-nuget-audit
    url: https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages
    checked: "2026-08-25"
  - id: microsoft-package-source-mapping
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping
    checked: "2026-08-25"
  - id: microsoft-dotnet-tools
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools
    checked: "2026-08-25"
  - id: fake-build
    url: https://fake.build/
    checked: "2026-08-25"
  - id: paket-docs
    url: https://fsprojects.github.io/Paket/
    checked: "2026-08-25"
  - id: microsoft-quotations
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations
    checked: "2026-08-25"
  - id: microsoft-srtp
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters
    checked: "2026-08-25"
  - id: microsoft-flexible-types
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types
    checked: "2026-08-25"
  - id: microsoft-byrefs
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs
    checked: "2026-08-25"
  - id: microsoft-fsharp-tour
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tour
    checked: "2026-08-25"
  - id: fsharp-core-api
    url: https://fsharp.github.io/fsharp-core-docs/
    checked: "2026-08-25"
---

# Chapter 45: Scripting, Automation, Packages, and What Comes Next {#overview}

An F# script is not a lesser kind of F# program. It uses the same language, FSharp.Core, and .NET runtime as a compiled project, but chooses a shorter assembly and execution path. That makes `.fsx` an excellent surface for exploration, repository maintenance, data repair, release checks, and small local tools.

The shorter path does not remove engineering obligations. A script can depend on hidden session state, the caller's working directory, mutable package feeds, unstable traversal order, ambiguous exit codes, or writes that occur on every run. Once other people or CI depend on it, those details are its interface.

This final chapter turns one real script into a reliable local automation boundary. It then explains how to decide whether a package deserves entry into the dependency graph, how to lock the graph that actually ships, when a script should graduate into a project or tool, and how to keep learning F# without chasing every advanced feature at once.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- choose among a REPL submission, `.fsx` script, console project, local .NET tool, and build DSL;
- run a script in a fresh process and pass explicit command-line arguments;
- distinguish the process working directory from the script's source directory;
- use `#load`, `#r`, `#I`, and `#r "nuget: ..."` without hiding their ordering or trust implications;
- design automation around explicit inputs, deterministic planning, bounded effects, and meaningful exit codes;
- make a generated file idempotent by comparing desired and existing content before replacement;
- explain what a SHA-256 manifest detects and what it does not authenticate;
- evaluate a NuGet package by fit, compatibility, provenance, maintenance, license, vulnerabilities, and exit cost;
- distinguish an exact direct package version from a locked transitive dependency closure;
- choose between PackageReference, local tools, FAKE, and Paket from the problem rather than F# identity;
- recognize quotations, SRTP, flexible types, and byref/Span code without treating them as prerequisites;
- turn the preceding 44 chapters into a project-based learning loop with evidence and feedback.

## Choose the smallest execution surface that preserves the contract {#execution-surface}

The right surface is the smallest one that makes the required behavior repeatable. “Smallest” refers to operational contract, not merely file count.

| Surface | Best first use | Reproducibility boundary | Promotion signal |
|---|---|---|---|
| REPL submission | inspect one expression, type, or API | current FSI process and its hidden state | the result must be repeated or reviewed |
| `.fsx` script | local automation, experiment, migration, report | script, SDK, arguments, files, environment, and package sources | multiple modules, tests, publishing, or a stable public CLI appear |
| console project | maintained command, scheduled job, richer tests | project graph, target framework, lock file, build, and published artifact | installation and cross-repository reuse matter |
| local .NET tool | repository-wide executable with a stable command | tool manifest plus package restore and runtime compatibility | organization-wide distribution or API versioning grows |
| build DSL such as FAKE | named build targets and dependency graph | DSL/tool version, script dependencies, target graph, and invoked tools | the graph or custom integration justifies another abstraction |

Do not promote a 70-line script merely because projects look more serious. Do promote it when the script has become a product: callers rely on its command syntax, several files form an internal architecture, restore must be locked, unit tests need normal discovery, or deployment needs a self-contained executable.

Likewise, do not compress a real application into one script to avoid an `.fsproj`. File order, public API boundaries, build properties, analyzers, test projects, and publishing are useful constraints once the program needs them.

### Use the REPL to answer one question {#repl-question}

`dotnet fsi` starts F# Interactive. A REPL submission is ideal for asking what signature the compiler infers, how a BCL method behaves, or whether a small transformation is plausible. The `;;` terminator belongs to interactive submission; a normal `.fsx` file does not need it.

The session remembers earlier bindings, opened namespaces, loaded files, referenced assemblies, and package resolutions. That is convenient while exploring and dangerous as evidence. Before preserving a result, put the necessary code in a script and execute it in a fresh process.

### Use a script for a complete, reviewable operation {#script-operation}

A script should state its inputs, outputs, failure behavior, and owned effects as clearly as a small application. It may still be concise. X45 has one file, uses only libraries included with .NET, creates no global installation, and can be invoked from the repository root.

The useful distinction is not “throwaway versus production.” It is “bounded operation versus growing product.” A one-off data repair may deserve stricter validation, backups, and audit evidence than a long-lived developer convenience.

### Use a project when the build graph becomes part of the answer {#project-promotion}

Move to an F# console project when you need several compiled files, ordinary unit-test discovery, analyzers, generated documentation, project references, controlled target frameworks, `packages.lock.json`, publishing, trimming/AOT checks, or a supported command contract. The pure functions from a script can move almost unchanged; the important change is an explicit build and distribution boundary.

If contributors should invoke one versioned command from the repository, a local .NET tool can be appropriate. Commit its `.config/dotnet-tools.json`, restore it with `dotnet tool restore`, and remember that tools run with the user's authority. A versioned tool manifest controls which tool package is requested; it does not make untrusted tool code safe.

## Understand what FSI executes {#fsi-model}

Microsoft documents the command shape as `dotnet fsi [options] [script-file [arguments]]`. When a script runs, `fsi.CommandLineArgs[0]` is the script path and later elements are its arguments. `--` tells FSI to treat the remaining tokens as script arguments when an argument might otherwise look like an FSI option.

X45 accepts these forms:

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx write ./artifacts ./artifacts.manifest.json
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx check ./artifacts ./artifacts.manifest.json
```

`--exec` runs the script and exits rather than remaining interactive. `write` converges the output toward desired content. `check` performs no write and returns exit code `2` when the output is absent or stale. Unexpected failures return `1`; success returns `0`.

### Working directory and source directory answer different questions {#script-paths}

Relative process paths are resolved from the caller's current working directory. That is useful for command arguments such as `./artifacts`, because the caller owns their meaning. It also means a script invoked from another directory must not assume those paths are beside the script.

When a resource belongs to the script itself, anchor it with `__SOURCE_DIRECTORY__`. `__SOURCE_FILE__` identifies the current source file. Use caller-relative paths for caller-owned input, source-relative paths for script-owned assets, and absolute paths at the boundary where work begins. Do not silently mix the two models.

Environment variables, current culture, time zone, current time, random seeds, network state, and the installed SDK are inputs too. Read them once at the edge, validate them, and pass ordinary values inward when reproducibility matters.

### Directives are ordered compile inputs {#directives}

FSI processes script declarations in order. Its main directives are:

- `#load "helpers.fsx"` compiles and executes another script before later code uses its definitions;
- `#r "library.dll"` references an assembly file;
- `#I "directory"` adds an assembly search path for later references;
- `#r "nuget: PackageId, Version"` restores and references a NuGet package;
- conditional symbols such as `INTERACTIVE` can separate FSI-only declarations when a file is also compiled elsewhere.

These are not ordinary runtime function calls. A missing or incompatible reference prevents later script code from compiling. `#load` also executes the loaded script's top-level effects, so a “helper” that writes files during loading has hidden startup behavior.

Keep reusable loaded scripts effect-free at the top level. Put behavior in named functions and let one entry script own execution. If a growing set of `#load` directives starts recreating project file order, use a project.

## X45: generate a stable artifact manifest {#x45}

X45 solves a practical local problem: enumerate files beneath an artifact directory, record normalized relative paths, byte lengths, and SHA-256 digests in deterministic JSON, then either update the manifest or verify that it is current.

Its contract is deliberately narrow:

- input is one existing local directory and one output file path;
- directory traversal skips symbolic links and rejects a symbolic-link root;
- the output file itself is excluded if it sits under the source directory;
- paths use `/` and entries use ordinal path ordering on every platform;
- JSON has schema version `1`, UTF-8 without a BOM, and one final newline;
- unchanged desired content leaves the existing output untouched;
- replacement uses a uniquely named file in the output directory, then moves it over the destination;
- no-argument execution owns and removes a unique temporary fixture for repository verification.

### Model observable outcomes, not incidental steps {#manifest-model}

The script distinguishes planning data from write and check outcomes:

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#manifest-model{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

`ManifestPlan` contains both structured entries and the exact bytes-as-text desired at the boundary. `Updated` and `Unchanged` are not Booleans with undocumented meaning. `Current` and `Stale` make read-only CI behavior a separate contract from mutation.

The model remains small because this is local automation. A public tool might add schema compatibility, structured diagnostics, cancellation, logging, and a stable serialized result. Those needs would be promotion signals.

### Make traversal and hashing policy explicit {#artifact-scan}

The filesystem adapter resolves full paths, uses the operating system's path equality for excluding the output, recursively skips reparse points, normalizes reported separators, and hashes each open stream:

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#artifact-scan{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

Skipping links avoids accidentally walking outside the selected tree or entering a cycle. It is a policy, not a universal rule: a deployment format that intentionally includes links would need to record link targets safely instead.

Opening with `FileShare.Read` prevents cooperating Windows writers from modifying the file while it is hashed. This is not a transactional filesystem snapshot, especially across platforms. If producers may mutate the tree concurrently, first publish an immutable staging directory or use a storage mechanism with snapshot semantics.

SHA-256 lets a later consumer detect whether bytes differ from the recorded bytes. It does not establish who produced the manifest or whether both artifact and manifest were maliciously replaced. Authenticity requires a signature or another trusted channel; release provenance requires still more evidence.

### Separate deterministic planning from applying effects {#manifest-plan}

The planner renders JSON with `Utf8JsonWriter` instead of relying on unspecified reflection ordering. It sorts entries before rendering and fixes property order, casing, indentation, encoding, and newline policy:

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#manifest-plan{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

The boundary still reads files, so `planManifest` is not pure. The important separation is that it computes one complete desired result before deciding whether to mutate the output. `renderManifest` itself is deterministic for the same entry array.

Stable output prevents noisy diffs and makes equality meaningful. Sorting after enumeration avoids inheriting filesystem order. Relative paths avoid embedding a developer's absolute directory. No timestamps, machine names, or random identifiers enter the final JSON.

### Write only when desired content differs {#idempotent-write}

The application layer compares existing and desired text. Only a difference creates a temporary file and replaces the destination:

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#idempotent-write{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

This gives the useful idempotence property: after one successful `write`, another `write` over unchanged inputs reports `Unchanged` and does not alter the output timestamp. The same-directory temporary file keeps the final move on one filesystem and narrows the window in which a partial destination is visible.

Do not overstate that guarantee. The code does not request durable flushes, coordinate concurrent writers, preserve every prior permission or metadata bit, or recover an interrupted network filesystem. “Replace after complete local write” is accurate; “transactionally durable under every crash” is not.

`check` compares the same plan without writing. That makes CI failure actionable: exit `2` means regenerate or commit the manifest, while exit `1` means the operation itself failed. Scripts that always print an error but exit `0` break automation composition.

### Verify idempotence with a real temporary fixture {#script-evidence}

With no arguments, X45 creates two files in a unique directory under `Path.GetTempPath()`. It writes once, sets the output timestamp to a sentinel, writes again, checks without mutation, verifies ordinal normalized paths, and removes only that owned directory in `finally`.

Run the verified slice from the repository root:

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx
```

The registered example requires these ordered observations:

```text
First write: updated files=2
Second write: unchanged files=2
Check mode: current files=2
Stable timestamp: true
Manifest paths: nested/beta.bin, notes.txt
Cleanup: removed=true
```

The repository executes that script in a fresh FSI process under .NET SDK `10.0.301` and F# 10. The evidence covers the temporary fixture, exact output order, idempotent second write, read-only current check, path normalization, and cleanup. It does not cover hostile directories, concurrent producers, millions of files, remote filesystems, signatures, or every Windows/Linux filesystem.

## Treat automation as a public interface as soon as another caller depends on it {#automation-interface}

A script may have no assembly API, but it still exposes contracts:

- command name, argument order, defaults, and help text;
- accepted path forms and whether paths are caller-relative;
- standard output for data, standard error for diagnostics, and exit codes;
- files created, replaced, or deleted and the ownership rule for each;
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

Do not wrap every `dotnet build` in F# merely to say the build uses F#. A short task runner provided by the repository may be clearer. Introduce F# where it owns meaningful parsing, planning, validation, concurrency, or reusable policy.

When invoking a process, pass an argument list rather than constructing an unescaped shell string, capture exit status and bounded output, propagate cancellation, and decide which environment variables are inherited. Secrets must not appear in command lines or normal logs.

### Bound untrusted and expensive inputs {#automation-safety}

X45 targets a trusted local artifact tree. A tool exposed to untrusted paths would also need file-count and byte limits, permission policy, special-file handling, race analysis, timeout/cancellation, output-size bounds, and perhaps a sandbox. A checksum manifest is not a reason to read an attacker-controlled device file forever.

Resolve destructive targets before acting. Never derive a recursive delete root from an absent environment variable, broad wildcard, repository root, or home directory. Prefer task-owned temporary directories and recoverable moves. Validate that the resolved target remains inside the intended root.

Credentials belong in the environment's secret mechanism, not source, fixture files, manifests, exception messages, or generated reports. A local script runs with the invoking user's authority; “only a script” is not a security boundary.

## Add a package only after naming the missing capability {#package-choice}

The first X45 design question was whether it needed a package. `System.IO`, `SHA256`, and `Utf8JsonWriter` already satisfied the bounded contract, so the correct dependency count was zero beyond the SDK and FSharp.Core.

That choice is not anti-package minimalism. A maintained parser, protocol client, database driver, testing library, or framework can remove far more risk than it adds. The point is to compare a package against a written need rather than using package discovery as architecture.

### Evaluate fit before popularity {#package-scorecard}

For a candidate package, record at least:

| Question | Evidence to inspect | Reject or spike when |
|---|---|---|
| Does its API solve the exact problem? | smallest representative call, error/cancellation model, data ownership | the demo works only after large adapters or hidden global state |
| Does it support the target? | package target frameworks, runtime/native assets, AOT/browser/platform notes | the shipping target is absent or only assumed compatible |
| Who owns it? | package owners, source repository, license, release history, issue/review activity | provenance or license cannot be established |
| What enters transitively? | full dependency graph, build/analyzer/content assets, native binaries | the closure is disproportionate or conflicts with the host |
| Is its operational model acceptable? | threads, network, files, reflection, generated code, logging, configuration | critical behavior cannot be observed or controlled |
| Can the team update and leave? | migration notes, API surface used, replacement seam, data formats | removal would require rewriting the domain or stored data |
| What evidence exists? | focused test on the actual target plus restore/build/runtime checks | only a README snippet or download count supports the decision |

NuGet's official package-evaluation guidance points to version history, project/source links, owners, license, dependencies, usage, and vulnerability information. These are signals, not proof of future maintenance. A popular package can be wrong for the target; a small package can be excellent when its contract and ownership are clear.

Create a bounded adoption spike. Test the hardest representative behavior, one failure, target compatibility, and removal seam. Record the version and date examined because package state changes.

### `#r "nuget:"` is convenient, not a lock file {#script-packages}

FSI supports a package reference such as:

```fsharp
#r "nuget: PackageId, 1.2.3"
open PackageNamespace
```

Omitting the version asks for the highest available non-preview version at resolution time. That is useful for disposable exploration and unsuitable for a versioned automation contract. Put an exact direct version in a committed script.

The exact version in one `#r` directive does not create a repository `packages.lock.json` for the full transitive graph. It also relies on effective NuGet configuration, package sources, credentials, caches, and network availability. Do not describe a pinned directive as a locked restore.

FSI normally does not consume package build targets. Its documented `usepackagetargets=true` option enables them for packages authored to require that behavior. Enable it only with a understood need: build targets are executable restore/build behavior and widen the trust and compatibility surface.

For a script whose dependency closure must be reviewed and reproduced in CI, move the automation into a project with PackageReference and a committed lock file, or adopt a dependency manager whose script workflow provides an explicit committed lock. The one-file aesthetic is not worth an unverifiable supply chain.

### Lock the graph that runs {#locking}

In an SDK-style application or tool project, specify direct PackageReference versions and enable NuGet lock-file generation. Commit `packages.lock.json`, then run `dotnet restore --locked-mode` in CI. Locked mode restores the recorded closure or fails when project dependencies would change it.

A lock file answers resolution, not trust or runtime correctness. It does not prove that a package is safe, licensed for the product, compatible with the target, or behaviorally correct. It also does not force a consuming application's graph to use a library project's private resolution; the top-level consumer resolves its own closure.

Keep SDK and tool versions explicit too. This repository pins the SDK in `global.json`, local tools in `.config/dotnet-tools.json`, NuGet graphs in project lock files, and JavaScript tools in a workspace lock file. Each mechanism covers a different graph.

Update intentionally: change one bounded set, regenerate the lock, inspect direct and transitive differences, read relevant release notes, run focused and full tests, and retain rollback. “Latest” is a query result, not a review policy.

### Treat restore as a supply-chain operation {#package-security}

Packages and .NET tools execute with meaningful authority through runtime code, build targets, analyzers, generators, native assets, or tool entry points. Use trusted sources, protect credentials, and review source configuration. When public and private feeds coexist, Package Source Mapping can constrain which source may serve each direct and transitive package ID.

NuGet audit compares resolved dependencies with known vulnerability data during restore. Address findings according to policy and keep audit sources available. No finding means “no matching known advisory was reported under this configuration,” not “the package is secure.”

Prefer repository-scoped configuration and local tools over undocumented machine state. Do not commit restored package caches or credentials. Preserve restore logs and lock diffs when a dependency incident must be explained.

## Read the F# ecosystem as layers, not a shopping list {#ecosystem-map}

The ecosystem explored in Part VII sits on several ownership layers:

| Layer | Examples from this book | First compatibility question |
|---|---|---|
| F# language and FSharp.Core | unions, pattern matching, collections, async, quotations | which language/compiler and FSharp.Core contract is required? |
| .NET runtime and BCL | files, JSON, HTTP, tasks, diagnostics, cryptography | which TFM, runtime, OS, and API behavior is required? |
| Microsoft platform framework | ASP.NET Core, hosting, containers, Aspire integrations | which supported platform version and deployment model applies? |
| F# community library | FsCheck, Giraffe/Falco/Oxpecker, FSharp.Data, Elmish | which API value offsets package and maintenance cost? |
| cross-language UI/toolchain | Fable/npm/browser, Avalonia backends, Unity Editor/IL2CPP | which compiler, host, native tool, and release matrix must agree? |
| repository automation | scripts, local tools, FAKE, Paket, CI runner | which graph owns ordering, restore, credentials, and evidence? |

F# participates in the entire NuGet ecosystem, not only packages with “FSharp” in their names. Many ordinary .NET libraries work directly. The integration question is API shape: nulls, delegates, tasks, exceptions, mutation, reflection, overloads, serialization, and C#-oriented builders may require a narrow adapter.

Conversely, an F#-native package is not automatically the best fit. Check target frameworks, release evidence, transitive assets, and team comprehension exactly as for any other dependency.

### FAKE and Paket solve different problems {#fake-paket}

[FAKE](https://fake.build/) is an F# build-task DSL with target dependencies and modules for common tools. Choose it when a named target graph, reusable build integrations, or richer orchestration materially clarifies the build. A plain repository task file may remain clearer for four linear commands.

[Paket](https://fsprojects.github.io/Paket/) is an alternative .NET dependency manager with its own dependency and lock model, including script integration. Choose it because that model or an existing repository requires it, not because F# code must use an F#-associated package manager. Do not run NuGet and Paket over the same ownership boundary without an explicit division.

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

Finishing a book gives you a map, not automatic fluency. Fluency comes from repeated cycles in which the compiler, tests, runtime evidence, and another reader can contradict your first design.

Use this loop:

1. choose one real, bounded problem whose failure matters enough to reveal tradeoffs;
2. model inputs, valid states, expected failure, and effects before choosing a framework;
3. build the smallest vertical slice through the real boundary;
4. inspect inferred signatures and make ambiguous ownership explicit;
5. test pure rules, adapters, failure paths, and the actual target proportionally;
6. profile or instrument before changing representation for performance;
7. review the dependency and deployment graph, not just source code;
8. write down what the evidence proves, what it does not, and what would reverse the choice;
9. simplify after learning, then repeat with a slightly harder boundary.

### Choose a project track from the risk you want to learn {#project-tracks}

| Track | First project | Harder second slice | Chapters to revisit |
|---|---|---|---|
| language and modeling | CLI that validates and transforms a versioned local format | migration across three schema versions with properties | 7–18, 28–30 |
| backend and distributed systems | authenticated API around a pure workflow | idempotent persistence, retries, tracing, container release | 20–24, 33–39, 42 |
| data and analytics | reproducible ingest/clean/report pipeline | schema drift, large data, notebook-to-project promotion | 14–15, 29–31, 40 |
| browser application | Fable state machine with one real API | URL ownership, cancellation, accessibility, bundle budget | 20, 22–24, 41 |
| desktop or mobile | Avalonia desktop slice with pure update logic | packaging, platform service, signed target artifact | 25–32, 43 |
| game and simulation | deterministic F# rules behind a thin host | replay, save migration, frame profile, real IL2CPP Player | 12, 20, 24, 27–31, 44 |
| tooling and libraries | promote X45 into a tested console tool | stable API/CLI, package publication, upgrade compatibility | 16–17, 26–31, this chapter |

Do not build seven starter projects. Pick the track whose unknowns resemble your work or curiosity, then deepen it until deployment and maintenance change your design.

### Learn to navigate sources at three levels {#source-reading}

Use the language reference for exact syntax and constraints, FSharp.Core API documentation for function signatures and behavior, and the relevant .NET/platform documentation for runtime boundaries. Then inspect the package's own source, release notes, tests, and issues when a community abstraction enters the decision.

Run small compiler experiments instead of arguing from memory. Record the SDK and package version. A blog post can teach a durable idea while its setup commands, syntax, or compatibility table has aged; separate the idea from the current contract.

Read unfamiliar F# from types outward: public signatures, domain cases, pure transformations, effect ports, composition root, then implementation detail. When a clever operator hides the data flow, ask for the inferred type and rewrite one call explicitly.

### Seek feedback that can change the design {#community-feedback}

Ask reviewers a falsifiable question: “Can this state be constructed illegally?”, “Which cancellation owns this task?”, “What happens after the second run?”, or “Which Player evidence supports this package?” A generic request to “review my F#” produces generic approval.

When asking a community, provide a minimal reproduction, full diagnostics, SDK/package versions, target, expected behavior, actual behavior, and what you already ruled out. This respects other people's time and makes the answer useful to the next reader.

Contribute back at the smallest durable boundary: improve a reproduction, documentation example, test, issue, package metadata, or focused fix. You do not need compiler expertise to participate in the ecosystem.

## Avoid common scripting and package mistakes {#common-mistakes}

- proving a result in a stateful REPL session but never running a fresh script process;
- putting `;;` throughout an `.fsx` file because interactive submissions used it;
- assuming relative paths are beside the script regardless of the caller's working directory;
- letting a loaded helper execute writes at top level;
- omitting a NuGet version and calling the result reproducible;
- calling one exact `#r` version a lock for the full dependency closure;
- using `usepackagetargets=true` without understanding the package's build behavior;
- selecting a package by download count or F# branding without a representative target test;
- treating a clean vulnerability audit as proof that dependencies are safe;
- using several feeds without controlling which feed may supply each package;
- rewriting a generated file on every run and creating timestamp or diff churn;
- depending on filesystem enumeration order, current culture, local time, or absolute developer paths;
- printing errors but returning exit code `0` to CI;
- deleting a broad path derived from an unchecked argument or environment variable;
- logging command-line secrets or embedding them in generated output;
- claiming a digest authenticates the producer rather than only identifying bytes;
- introducing a build DSL before a target graph exists;
- keeping a script after it has acquired a public CLI, several modules, packages, tests, and publishing needs;
- learning quotations, SRTP, flexible types, and byrefs before ordinary modeling and effects are comfortable;
- mistaking completion of a feature list for the ability to design, test, ship, and maintain a system.

## Exercises {#exercises}

### Exercise 1: add exclusion without losing determinism {#exercise-01}

Extend the X45 design to accept repeatable `--exclude GLOB` rules for generated logs and symbol files. Define glob semantics, separator/case policy, whether rules match files or directories, behavior for invalid patterns, how excluded links are reported, and how the rule set appears in the manifest schema. Preserve `write`/`check` agreement, stable ordering, output exclusion, idempotence, and bounded tests across Windows and Unix-like paths. Decide whether to implement a tiny documented matcher or adopt a package.

### Exercise 2: write a package adoption record {#exercise-02}

Your team wants a command-line parser for the promoted manifest tool. Compare hand-written parsing with two current NuGet candidates. Record required syntax, help/error behavior, target frameworks, package/source identity, license, maintenance, transitive/build assets, vulnerabilities, trimming/AOT needs, test ergonomics, direct version, lock procedure, update owner, and removal seam. Build one focused spike for the hardest requirement and state a reversible decision.

### Exercise 3: plan the next twelve weeks {#exercise-03}

Choose one project track from this chapter. Define three four-week increments that each end in executable evidence, not reading alone. Include the F# concepts to revisit, one real .NET or platform boundary, tests and diagnostics, package budget, deployment or distribution target, review question, and a criterion for simplifying or reversing the design. Place advanced features only where a measured problem demands them.

[Read the chapter solutions](../solutions/ch-45-scripting-packages-next).

## Model review {#model-review}

- A REPL answers one question; a script preserves one bounded operation; a project owns a growing build and distribution contract.
- FSI executes declarations in order, exposes explicit script arguments, and distinguishes caller working directory from source directory.
- Directives affect compilation and restore; loaded scripts should not hide top-level effects.
- Reliable automation has explicit inputs, deterministic desired output, bounded effects, meaningful exit codes, and a check mode.
- X45 creates a stable SHA-256 JSON manifest, skips links by policy, writes only on change, and proves idempotence in a real temporary fixture.
- A digest detects byte differences but does not authenticate provenance; same-directory replacement is not universal crash durability.
- Add a package for a named capability after testing API fit, target support, provenance, closure, operations, maintenance, and exit cost.
- An exact `#r "nuget:"` version pins one request but is not a committed transitive lock graph.
- PackageReference lock files, local tool manifests, FAKE, and Paket solve different ownership problems.
- Restore is a supply-chain operation; trusted sources, source mapping, audit, lock review, and rollback are separate controls.
- The F# ecosystem includes the full .NET ecosystem plus F#-native abstractions and cross-language toolchains.
- Quotations, SRTP, flexible types, and byref/Span are recognition topics until a concrete problem justifies deeper study.
- Continued mastery comes from vertical projects, compiler and runtime evidence, review questions, simplification, and repeated release loops.

Part VII is now complete. The appendices turn the book into a working reference: environment setup, syntax, collections, C# migration, compiler diagnostics, terminology, solution review, and the advanced-feature recognition index.

## Sources {#sources}

- [Microsoft Learn: Interactive programming with F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
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
