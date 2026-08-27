---
title: "Chapter 30: Diagnostics, Debugging, Formatting, and Builds"
description: "Read the first relevant compiler diagnostic, choose FSI or a debugger by evidence needed, enforce formatting without mutation, and reproduce locked Release builds."
translationKey: part-05/ch-30-diagnostics-tooling-builds
---

# Chapter 30: Diagnostics, Debugging, Formatting, and Builds {#overview}

Tooling is useful when it shortens the path from symptom to cause. A compiler diagnostic answers a static question, FSI tests a small expression, a debugger exposes one runtime execution, a formatter removes stylistic variation, and a locked build reconstructs an agreed dependency graph. Confusing these jobs creates rituals instead of diagnosis.

The examples use project commands rather than one editor or CI vendor. An IDE may put buttons around them, but project files, lock files, tool manifests, and repeatable commands remain the common source of truth.

## Read the first relevant diagnostic {#diagnostic-anatomy}

A typical F# compiler line has this shape:

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

The path identifies the source seen by the build, while line and column mark where the compiler detected a problem. `error` is the severity, `FS0039` is a searchable diagnostic code, and the remaining text supplies context. With warnings-as-errors, a warning may become a failure while retaining its code.

The reported position is not a promise about root cause. A missing closing delimiter may be noticed several lines later. One unresolved type can make later member lookups fail. F# compilation order can make every name from an earlier-needed file appear absent. Compilers recover after an error so they can report more, but recovered interpretation may generate secondary noise.

Begin with the earliest relevant diagnostic in source you own, fix or explain it, then rebuild. Do not mechanically edit every red line from bottom to top. If the first line belongs to generated code or a dependency, find the first preceding restore/build failure that caused it.

### Classify the failure before choosing a tool {#failure-classes}

| Observed symptom | Likely class | First tool |
|---|---|---|
| FS0010-style unexpected token or indentation report | Parsing or indentation rule | Editor plus compiler |
| FS0001 expected one type but received another | Type inference or wrong model | Full message, inferred signatures, small FSI probe |
| FS0039 name or namespace not defined | Spelling, scope, reference, or file order | Project file and first missing symbol |
| NU-prefixed restore failure | Dependency graph, source, or lock mismatch | `dotnet restore --locked-mode` output |
| Build passes; value or effect is wrong | Runtime logic | Focused test, then debugger if needed |
| Test fails with expected/actual values | Behavioral regression or bad expectation | Smallest failing test and domain requirement |

A code tells you a category, not the repair. Searching FS0039 may list many causes; only the surrounding source, project order, and references choose among them.

## Two deliberate compiler failures {#expected-errors}

Expected-error examples make documentation statements executable. The checker requires the command to fail and every declared diagnostic code to appear. For this kind of example, a successful compile means the test failed.

### FS0030: one value cannot remain ambiguously generic {#fs0030}

The complete Chapter 11 example is one binding:

```fsharp:line-numbers [ch11-value-restriction.fsx]
let ambiguousBuckets = Array.create 2 []
```
Run it directly:

```console
dotnet fsi --exec value-restriction.fsx
```

F# 10 reports FS0030 and the weak type `'_a list array`. `Array.create` constructs one mutable array whose element type remains unresolved; that storage location cannot safely be generalized for unrelated element types. The diagnostic suggests three valid repairs: add a concrete annotation, make the data an argument to a generic function, or add `()` when each call should construct a fresh value.

The minimal example removes unrelated uses, so its first diagnostic teaches the intended lesson. It is not production code and should never be made green by suppressing FS0030.

### FS0039: file order is part of an F# project {#fs0039}

The deliberately invalid Chapter 16 project compiles `Workflow.fs` before `Domain.fs`:

```xml:line-numbers [Ch16WrongOrder.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../chapters/ch16/Workflow.fs" Link="Workflow.fs" />
    <Compile Include="../../chapters/ch16/Domain.fs" Link="Domain.fs" />
  </ItemGroup>
</Project>
```
F# project files have a fixed order. A file may use definitions from earlier files, but not later ones. `Workflow.fs` opens `ThinkingInFSharp.Ch16.Domain`, so this order produces FS0039 even though both files exist and each is syntactically valid.

The repair is to place `Domain.fs` before `Workflow.fs` in the valid project. Duplicating domain types in `Workflow.fs`, adding arbitrary `open` declarations, or cleaning caches does not fix the dependency direction. The first missing namespace is more informative than the later missing `Capacity`, `BookingRequest`, and union cases.

Run only the invalid project while investigating:

```console
dotnet build Ch16WrongOrder.fsproj \
  --configuration Release
```

Then run the valid Chapter 16 project and finally the complete example check. A narrow command gives fast feedback; the broad command detects related project-wiring changes.

## Isolate the failure without changing it {#reduction-loop}

A focused diagnostic loop is short:

1. Record the full command, configuration, SDK, and first relevant output.
2. Reproduce before editing; a non-reproducible report is a different investigation.
3. Remove unrelated code or filter to one test while preserving the same diagnostic or wrong behavior.
4. State one hypothesis that predicts an observation.
5. Use the cheapest tool that can observe it.
6. Make one minimal change, rerun the narrow command, then run the full verification set.

Isolation is not random deletion. If removing a project reference changes FS0039 into a restore error, the reproduction has changed. If switching from Release to an FSI paste removes conditional compilation, the environment has changed. Briefly record each deliberate difference.

Compiler output often includes the useful inferred type. Read the complete message before adding an annotation: the annotation may expose the wrong assumption, but it can also silence useful generality. Prefer explaining why inference chose a type over forcing the type you hoped for.

## Use FSI for small static and dynamic questions {#fsi}

`dotnet fsi` is a read-evaluate-print loop included with the .NET SDK; `dotnet fsi --exec file.fsx` executes a script and exits. It is well suited to questions such as:

- What type did this expression infer?
- Which pattern branch handles this value?
- Does this pure transformation preserve the expected invariant?
- What does a small .NET API call return for one controlled input?

Use `#r` to reference an assembly or package and `#load` to load a script when the experiment needs them. Keep experiments deterministic and small. Once the idea matters to the product, move it into a compiled source file and an automated test.

### FSI is not equivalent to a project build {#fsi-boundary}

An FSI session retains earlier bindings and loaded assemblies. Restart it when stale state could explain success. A pasted expression does not automatically inherit project file order, all MSBuild properties, target-framework assets, conditional symbols, or the actual assembly context.

FSI defines `INTERACTIVE`; compiled code defines `COMPILED`. That distinction can be deliberate, but “works in FSI” still answers only a local question. The project must build with its real `.fsproj` and warnings policy.

Avoid pasting a large workflow into FSI and manually rebuilding its dependency graph. A focused unit test is repeatable and preserves project context; FSI is best for a question small enough to see at once.

## Use a debugger for one runtime execution {#debugger}

When compilation succeeds but behavior contradicts a hypothesis, attach a managed .NET debugger in an IDE that supports the project. UI labels differ, but the available information is the same:

- a breakpoint pauses at an executable location;
- locals and watches show values in the selected stack frame;
- step over executes a call, while step into follows its implementation;
- the call stack shows how execution reached the current function;
- exception settings can pause when an exception is thrown rather than only when unhandled.

Place a breakpoint where information changes: immediately before a domain decision, after converting external input, or before an external side effect. In pipeline-heavy code, name an important intermediate result when that makes the hypothesis observable. Do not scatter breakpoints until one happens to look suspicious.

For an unexpected `Rejected(requested, capacity)`, inspect the validated request and capacity before `decide`, then the caller frame that supplied them. If both inputs are correct, step through the decision. If one is wrong, move outward to its producer. This follows data provenance rather than control-flow tourism.

Debug builds usually provide the clearest stepping and locals. Release optimization can reorder, inline, or omit observable locals even though program behavior remains correct. Reproduce the actual Release-only defect when necessary, but recognize the debugger's source view may be less literal.

### Do not alter the execution accidentally {#debugger-cautions}

Evaluating a watch expression can call a property or function with side effects. Changing a value in the debugger shows behavior under modified state, not the original run. Record which actions only observed execution and which altered it.

A debugger session is not a regression test. After finding the cause, write the smallest automated test that fails without the repair and passes with it. The test preserves the behavior after the breakpoint disappears.

## Check formatting with a pinned tool {#formatting}

Fantomas is a source formatter, not a type checker or linter. A project can declare it as a local .NET tool:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "fantomas": {
      "version": "7.0.5",
      "commands": ["fantomas"]
    }
  }
}
```

Restore the declared tool version and check all F# sources:

```console
dotnet tool restore
dotnet fantomas . --check
```

Fantomas 7 reads formatting settings from `.editorconfig`; an unused `fantomas.json` creates a false impression of configuration. In version 7.0.5, a clean check exits 0 and a file needing formatting exits 99. `--check` reports differences without writing. Run `dotnet fantomas .` only when applying formatting, then review and test the mechanical change.

Pinning matters because formatter output can change across releases. Upgrade the tool in a dedicated change, establish one new baseline, and keep behavioral edits out of that formatting diff when possible.

### Formatting and static analysis answer different questions {#static-analysis}

Fantomas normalizes layout. The F# compiler checks parsing, name resolution, types, constraints, and enabled warnings. `TreatWarningsAsErrors` makes emitted warnings fail the build; optional warnings such as unused bindings must still be enabled separately. Nullable checking and analyzers likewise require project configuration.

Formatting, static compilation, property tests, contract tests, and runtime observation cover different risks. A release decision combines the relevant results instead of treating consistent layout or a warning-free build as a complete verdict.

When suppressing a warning, keep the scope narrow and record why the flagged condition is safe. A global suppression used only to make checks pass discards future diagnostic information.

## Reproduce the toolchain and dependency graph {#reproducible-builds}

Reproducibility has several layers:

| Layer | Repository input | What remains outside it |
|---|---|---|
| SDK selection | `global.json` selects 10.0.301 | Host runtime and operating system still differ |
| Direct and transitive packages | `PackageReference` plus committed `packages.lock.json` | Feed availability and external credentials |
| Local tools | `.config/dotnet-tools.json` pins Fantomas 7.0.5 | Host runtime capable of running the tool |
| Compiler outputs | `Deterministic=true` with the same inputs | OS-specific native assets, paths, timestamps outside compiler control |
| Behavior | tests and example-output assertions | Unmodelled external services and machine state |

Exact SDK selection keeps SDK-provided dependencies aligned with package locks. It does not freeze the operating system, runtime used by deployed software, package feeds, or local caches. Upgrade the SDK deliberately and record `dotnet --info` when investigating an environment-specific failure.

A PackageReference such as `Version="3.4.0"` alone may allow more than one transitive dependency graph. The lock file records resolved versions and content hashes. `dotnet restore --locked-mode` uses that graph or fails when project dependencies disagree with the lock file; it does not silently rewrite the lock.

### Separate restore, build, and test {#build-stages}

Use separate stages when reproducibility matters:

```console
dotnet tool restore
dotnet fantomas . --check
dotnet restore Sample.slnx --locked-mode
dotnet build Sample.slnx --configuration Release --no-restore
dotnet test Sample.slnx --configuration Release --no-build
```

`dotnet build` normally performs an implicit restore. `--no-restore` confirms that the build consumes the graph from the preceding locked restore. `--no-build` similarly prevents tests from hiding a build step. These flags make each stage's responsibility visible; they are not performance decorations.

When stale artifacts are a plausible cause, run `dotnet clean` before the locked restore and Release build. Do not begin every diagnosis by deleting caches: first preserve the failing output, then use a clean build as a controlled experiment.

For hard MSBuild investigations, a binary log from `dotnet build -bl:<path>` records evaluation and execution details. It may contain absolute paths, properties, and environment-derived data, so inspect and handle it as diagnostic data rather than publishing it automatically.

## A compact diagnosis checklist {#checklist}

Before calling a tooling problem fixed, ask:

1. What full command and environment reproduced it?
2. What was the first relevant diagnostic or wrong value?
3. Which category of failure was it, and which observation confirmed the hypothesis?
4. Did the repair address the cause rather than hide the symptom?
5. Does the narrow reproduction now pass or produce the intended diagnostic?
6. Did formatting check run without mutation?
7. Did locked restore, Release build, and all tests pass from a clean state?
8. Is the discovered regression preserved as an automated test or expected-error fixture?

Tools become engineering practice when another reader can reproduce the result, not when one workstation happens to turn green.

## Exercises {#exercises}

### Exercise 1: diagnose a cascade from file order {#exercise-01}

The invalid Chapter 16 build reports an absent `Domain` namespace followed by absent domain types. Explain which message to address first, identify the project-file repair, and list two tempting edits that would hide or duplicate the model rather than fix order.

### Exercise 2: choose FSI, a test, and a debugger {#exercise-02}

A compiled booking workflow returns `Rejected(3, 2)` when a caller expected acceptance. Describe one small FSI experiment, one focused automated test, and one breakpoint plan. State what each reveals and which artifact remains after diagnosis.

### Exercise 3: audit a reproducible build {#exercise-03}

A teammate changes one package version but forgets its lock file, has a global Fantomas version, and reports that Debug succeeds from a warm tree. Give an ordered, platform-neutral command sequence that should expose each mismatch and state which repository files must be updated deliberately.

[Read the chapter solutions](../solutions/ch-30-diagnostics-tooling-builds).

## Model review {#model-review}

- Start with the first relevant diagnostic; later errors may be recovery cascades.
- A diagnostic position is where the compiler noticed a problem, not guaranteed root cause.
- Expected-error fixtures must fail and contain the declared code.
- FSI answers small inference and execution questions but does not replace a project build.
- A debugger tests one runtime hypothesis through values, stack frames, and exceptions.
- Preserve debugger discoveries as automated regression tests.
- Fantomas formatting and compiler static analysis answer different questions.
- Pin local tools and use `--check` when a gate must not rewrite source.
- Lock files reproduce a resolved package graph; locked restore fails on drift.
- Separate restore, build, and test so each stage's inputs are visible.
- Clean builds are controlled experiments, not the first response to every failure.
- Reproducibility has layers and never substitutes for recording the actual environment.

## Sources {#sources}

- [Microsoft Learn: F# compiler options and warnings](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn: F# Interactive and scripting](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: managed .NET debuggers](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers)
- [Microsoft Learn: `dotnet build`, implicit restore, and build logs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- [Microsoft Learn: NuGet dependency lock files and locked mode](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
- [Microsoft Learn: local .NET tool manifests and restore](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)
- [Fantomas: install and use the local formatter](https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html)
- [Fantomas: non-mutating formatting checks](https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html)
