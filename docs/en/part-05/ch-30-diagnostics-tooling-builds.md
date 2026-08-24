---
title: "Chapter 30: Diagnostics, Debugging, Formatting, and Builds"
description: "Read the first relevant compiler diagnostic, choose FSI or a debugger by evidence needed, enforce formatting without mutation, and reproduce locked Release builds."
translationKey: part-05/ch-30-diagnostics-tooling-builds
kind: chapter
part: 5
chapter: 30
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch11-value-restriction
  - ch16-wrong-file-order
exerciseIds:
  - ch30-exercise-01
  - ch30-exercise-02
  - ch30-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-compiler-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options
    checked: "2026-08-24"
  - id: microsoft-fsharp-interactive
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-24"
  - id: microsoft-managed-debuggers
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers
    checked: "2026-08-24"
  - id: microsoft-dotnet-build
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
    checked: "2026-08-24"
  - id: microsoft-nuget-lock-files
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies
    checked: "2026-08-24"
  - id: microsoft-local-tools
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use
    checked: "2026-08-24"
  - id: fantomas-getting-started
    url: https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html
    checked: "2026-08-24"
  - id: fantomas-format-check
    url: https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html
    checked: "2026-08-24"
---

# Chapter 30: Diagnostics, Debugging, Formatting, and Builds {#overview}

Tooling is useful when it shortens the path from symptom to evidence. A compiler diagnostic answers a static question, FSI tests a small expression, a debugger exposes one runtime execution, a formatter removes stylistic variation, and a locked build reconstructs an agreed dependency graph. Confusing these jobs creates rituals without diagnosis.

This chapter uses repository commands rather than one editor or CI vendor. An IDE may put buttons around them, but the project, lock files, tool manifest, and repeatable command remain the shared contract.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read a diagnostic's path, position, severity, code, and message without treating its position as guaranteed root cause;
- start with the first relevant diagnostic and recognize cascaded errors;
- distinguish syntax, type, name-resolution, project-order, restore, runtime, and assertion failures;
- reduce a failure while preserving the command and environment that reproduce it;
- use FSI for inference and pure experiments without mistaking session state for a project build;
- use breakpoints, locals, watches, call stacks, and exception breaks to test a runtime hypothesis;
- restore a pinned local tool and run Fantomas in non-mutating check mode;
- treat compiler warnings and formatting as different quality signals;
- separate locked restore, Release build, and test stages;
- explain what SDK, package, tool, and deterministic-build settings do and do not reproduce.

## Read the first relevant diagnostic {#diagnostic-anatomy}

A typical F# compiler line has this shape:

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

The path identifies the source as seen by the build; line and column identify where the compiler detected a problem; `error` is severity; `FS0039` is a searchable, testable diagnostic code; the remaining text carries context. With warnings-as-errors, a warning may be promoted to failure while retaining its diagnostic identity.

The reported position is not a promise about root cause. A missing closing delimiter may be noticed several lines later. One unresolved type can make later member lookups fail. F# compilation order can make every name from an earlier-needed file appear absent. Compilers recover after an error so they can report more, but recovered interpretation may generate secondary noise.

Begin with the earliest relevant diagnostic in source you own, fix or explain it, then rebuild. Do not mechanically edit every red line from bottom to top. If the first line belongs to generated code or a dependency, find the first preceding restore/build failure that caused it.

### Classify the failure before choosing a tool {#failure-classes}

| Evidence | Likely class | First tool |
|---|---|---|
| FS0010-style unexpected token or indentation report | Parse/offside rule | Editor plus compiler |
| FS0001 expected one type but received another | Type inference or wrong model | Full message, inferred signatures, small FSI probe |
| FS0039 name or namespace not defined | Spelling, scope, reference, or file order | Project file and first missing symbol |
| NU-prefixed restore failure | Dependency graph, source, or lock mismatch | `dotnet restore --locked-mode` output |
| Build passes; value or effect is wrong | Runtime logic | Focused test, then debugger if needed |
| Test fails with expected/actual values | Behavioral regression or bad expectation | Smallest failing test and domain requirement |

A code tells you a category, not the repair. Searching FS0039 may list many causes; only the surrounding source, project order, and references choose among them.

## Two intentional compiler failures {#expected-errors}

Expected-error examples turn documentation claims into executable evidence. The example checker requires the command to fail and requires every declared diagnostic code to appear. A successful compile is a failed test for this kind of example.

### FS0030: one value cannot remain ambiguously generic {#fs0030}

The complete Chapter 11 fixture is one binding:

<<< @/../examples/expected-errors/ch11-value-restriction.fsx{fsharp:line-numbers} [ch11-value-restriction.fsx]

Run it directly:

```console
dotnet fsi --exec examples/expected-errors/ch11-value-restriction.fsx
```

F# 10 reports FS0030 and the weak type `'_a list array`. `Array.create` constructs one mutable array value whose element type remains unresolved; that single storage location cannot safely be generalized for unrelated element types. The diagnostic itself suggests the three intentional repairs: choose a concrete annotation, expose a data argument for a generic function, or add `()` when each call should construct a fresh value.

The smallest fixture removes unrelated uses, so the first diagnostic is the lesson. It is not production code and should never be made green by suppressing FS0030.

### FS0039: file order is part of an F# project {#fs0039}

The invalid Chapter 16 project compiles `Workflow.fs` before `Domain.fs`:

<<< @/../examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj{xml:line-numbers} [Ch16WrongOrder.fsproj]

F# files in a project have explicit order. A file may use definitions from earlier files, not later ones. `Workflow.fs` opens `ThinkingInFSharp.Ch16.Domain`, so this order produces FS0039 even though both files exist and each is syntactically valid.

The repair is to place `Domain.fs` before `Workflow.fs` in the valid project. Duplicating domain types in `Workflow.fs`, adding arbitrary `open` declarations, or cleaning caches does not repair the dependency direction. The first missing namespace is stronger evidence than the later missing `Capacity`, `BookingRequest`, and union cases.

Run only the invalid project while investigating:

```console
dotnet build examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj \
  --configuration Release
```

Then run the valid Chapter 16 project and finally the complete example gate. A narrow command accelerates feedback; the broad command detects collateral wiring changes.

## Reduce without changing the failure {#reduction-loop}

A disciplined diagnostic loop is short:

1. Record the exact command, configuration, SDK, and first relevant output.
2. Reproduce before editing; a non-reproducible report is a different investigation.
3. Remove unrelated code or filter to one test while preserving the same diagnostic or wrong behavior.
4. State one hypothesis that predicts an observation.
5. Use the cheapest tool that can observe it.
6. Make one minimal change, rerun the narrow command, then run the full gate.

Reduction is not random deletion. If removing a project reference changes FS0039 into a restore error, the reproduction changed. If switching from Release to an FSI paste removes conditional compilation, the environment changed. Keep a short note of each deliberate difference.

Compiler output often includes the useful inferred type. Read the complete message before adding an annotation: the annotation may expose the wrong assumption, but it can also silence useful generality. Prefer explaining why inference chose a type over forcing the type you hoped for.

## Use FSI for small static and dynamic questions {#fsi}

`dotnet fsi` is a read-evaluate-print loop included with the .NET SDK; `dotnet fsi --exec file.fsx` executes a script and exits. It is well suited to questions such as:

- What type did this expression infer?
- Which pattern branch handles this value?
- Does this pure transformation preserve the expected invariant?
- What does a small .NET API call return for one controlled input?

Use `#r` to reference an assembly or package and `#load` to load a script when the experiment needs them. Keep experiments deterministic and small. Once the idea matters to the product, move it into a compiled source file and an automated test.

### FSI is not the project compiler {#fsi-boundary}

An FSI session retains earlier bindings and loaded assemblies. Restart the session when stale state could explain success. A pasted expression does not automatically inherit project file order, all MSBuild properties, target framework assets, conditional symbols, or the exact assembly boundary.

FSI defines `INTERACTIVE`; compiled code defines `COMPILED`. That distinction can be deliberate, but it is another reason that “works in FSI” is only local evidence. The project must still build with its real `.fsproj` and warnings policy.

Avoid pasting a large workflow into FSI and manually rebuilding its dependency graph. A focused unit test is repeatable and preserves project context; FSI is best for a question small enough to see at once.

## Use a debugger for one runtime execution {#debugger}

When compilation succeeds but behavior contradicts a hypothesis, attach a managed .NET debugger in an IDE that supports the project. UI labels differ, but the evidence is common:

- a breakpoint pauses at an executable boundary;
- locals and watches show values in the selected stack frame;
- step over executes a call, while step into follows its implementation;
- the call stack shows how execution reached the current function;
- exception settings can pause when an exception is thrown rather than only when unhandled.

Place a breakpoint where information changes: immediately before a domain decision, after a boundary conversion, or before an external effect. In pipeline-heavy code, give an important intermediate result a name when that makes the hypothesis observable. Do not scatter breakpoints until one happens to look suspicious.

For an unexpected `Rejected(requested, capacity)`, inspect the validated request and capacity before `decide`, then the caller frame that supplied them. If both inputs are correct, step through the decision. If one is wrong, move outward to its producer. This follows data provenance rather than control-flow tourism.

Debug builds usually provide the clearest stepping and locals. Release optimization can reorder, inline, or omit observable locals even though program behavior remains correct. Reproduce the actual Release-only defect when necessary, but recognize the debugger's source view may be less literal.

### Do not mutate the evidence accidentally {#debugger-cautions}

Evaluating a watch expression can call a property or function with side effects. Changing a value in the debugger proves behavior under a modified state, not the original run. Record which actions were observational and which altered execution.

A debugger session is not a regression test. After finding the cause, write the smallest automated test that fails without the repair and passes with it. The test preserves evidence after the breakpoint disappears.

## Format with a pinned, non-mutating check {#formatting}

Fantomas is a source formatter, not a type checker or linter. This repository declares it as a local .NET tool:

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

Restore the exact declared tool and check all F# sources:

```console
dotnet tool restore
dotnet fantomas . --check
```

Fantomas 7 reads formatting settings from `.editorconfig`; an unused `fantomas.json` would only imply control that does not exist. In version 7.0.5, a clean check exits 0 and a file needing formatting exits 99. `--check` reports differences without writing them. Run `dotnet fantomas .` deliberately to apply formatting, then review and test the mechanical change.

Pinning matters because formatter output can change across releases. Upgrade the tool in a dedicated change, establish one new baseline, and keep behavioral edits out of that formatting diff when possible.

### Formatting and static analysis answer different questions {#static-analysis}

Fantomas normalizes layout. The F# compiler checks parsing, name resolution, types, constraints, and enabled warnings. `TreatWarningsAsErrors` makes emitted warnings fail the build; optional warnings such as unused bindings must still be enabled deliberately. Nullable checking and analyzers likewise need explicit project configuration.

Do not treat a formatted file as correct, or a warning-free build as fully tested. Formatting, static compilation, property tests, boundary tests, and runtime observation cover different risks.

When suppressing a warning, scope the suppression narrowly and record why the flagged condition is safe. A global suppression that merely makes a gate green discards future evidence.

## Reproduce the toolchain and dependency graph {#reproducible-builds}

“Reproducible” has layers:

| Layer | Repository evidence | What remains outside it |
|---|---|---|
| SDK selection | `global.json` selects 10.0.301 with `latestPatch` | Exact host runtime and installed patch may differ |
| Direct and transitive packages | `PackageReference` plus committed `packages.lock.json` | Feed availability and external credentials |
| Local tools | `.config/dotnet-tools.json` pins Fantomas 7.0.5 | Host runtime capable of running the tool |
| Compiler outputs | `Deterministic=true` with the same inputs | OS-specific native assets, paths, timestamps outside compiler control |
| Behavior | tests and example-output assertions | Unmodelled external services and machine state |

`latestPatch` deliberately permits a later servicing patch in the same SDK feature band; it is a security/maintenance tradeoff, not byte-for-byte SDK identity. Record `dotnet --info` when investigating an environment-specific failure.

A PackageReference such as `Version="3.4.0"` alone can permit resolution behavior beyond one transitive graph. The lock file records resolved versions and content hashes. `dotnet restore --locked-mode` uses that graph or fails when project dependencies and lock file disagree; it does not silently rewrite the contract.

### Separate restore, build, and test {#build-stages}

Use explicit stages when reproducibility matters:

```console
dotnet tool restore
dotnet fantomas . --check
dotnet restore ThinkingInFSharp.slnx --locked-mode
dotnet build ThinkingInFSharp.slnx --configuration Release --no-restore
dotnet test ThinkingInFSharp.slnx --configuration Release --no-build
```

`dotnet build` normally performs an implicit restore. `--no-restore` proves the build consumes the graph from the preceding locked restore. `--no-build` similarly prevents tests from hiding a build step. These flags clarify stage ownership; they are not performance decorations.

When stale artifacts are a plausible cause, run `dotnet clean` before the locked restore and Release build. Do not begin every diagnosis by deleting caches: first preserve the failing output, then use a clean build as a controlled experiment.

For hard MSBuild investigations, a binary log from `dotnet build -bl:<path>` records evaluation and execution details. It may contain absolute paths, properties, and environment-derived data, so inspect and handle it as diagnostic data rather than publishing it automatically.

## A compact evidence checklist {#checklist}

Before calling a tooling problem fixed, ask:

1. What exact command and environment reproduced it?
2. What was the first relevant diagnostic or wrong value?
3. Which category of failure was it, and which observation confirmed the hypothesis?
4. Did the repair address the cause rather than suppress evidence?
5. Does the narrow reproduction now pass or produce the intended diagnostic?
6. Did formatting check run without mutation?
7. Did locked restore, Release build, and all tests pass from a clean state?
8. Is the discovered regression preserved as an automated test or expected-error fixture?

Tools become engineering practice when another reader can repeat the evidence, not when one workstation happens to turn green.

## Exercises {#exercises}

### Exercise 1: diagnose a cascade from file order {#exercise-01}

The invalid Chapter 16 build reports an absent `Domain` namespace followed by absent domain types. Explain which message to address first, identify the project-file repair, and list two tempting edits that would hide or duplicate the model rather than fix order.

### Exercise 2: choose FSI, a test, and a debugger {#exercise-02}

A compiled booking workflow returns `Rejected(3, 2)` when a caller expected acceptance. Describe one small FSI experiment, one focused automated test, and one breakpoint plan. State what evidence each provides and which artifact remains after diagnosis.

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
- Reproducibility is layered and never substitutes for recording the actual environment.

## Sources {#sources}

- [Microsoft Learn: F# compiler options and warnings](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn: F# Interactive and scripting](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: managed .NET debuggers](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers)
- [Microsoft Learn: `dotnet build`, implicit restore, and build logs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- [Microsoft Learn: NuGet dependency lock files and locked mode](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
- [Microsoft Learn: local .NET tool manifests and restore](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)
- [Fantomas: install and use the local formatter](https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html)
- [Fantomas: non-mutating formatting checks](https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html)
