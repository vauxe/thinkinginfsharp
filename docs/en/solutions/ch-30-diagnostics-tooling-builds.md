---
title: "Chapter 30 Solutions"
description: "Repair an F# file-order cascade, assign distinct questions to FSI, tests, and a debugger, and audit an intentionally changed locked dependency graph."
translationKey: solutions/ch-30-diagnostics-tooling-builds
---

# Chapter 30 Solutions {#overview}

Each solution preserves the original failure before changing code. The goal is not to memorize commands. It is to collect the observation that answers each diagnostic question and leave a durable repository check behind.

[Return to Chapter 30](../part-05/ch-30-diagnostics-tooling-builds).

## Exercise 1: diagnose a cascade from file order {#exercise-01}

### Repair the first missing dependency {#exercise-01-order}

Address the first FS0039 saying that `ThinkingInFSharp.Ch16.Domain` is absent. `Workflow.fs` opens that namespace before the project has compiled `Domain.fs`; later errors about `Capacity`, `BookingRequest`, and `Accepted` are consequences of the same missing dependency.

The invalid order is:

```xml
<Compile Include="../../chapters/ch16/Workflow.fs" Link="Workflow.fs" />
<Compile Include="../../chapters/ch16/Domain.fs" Link="Domain.fs" />
```

The valid project must compile the dependency first:

```xml
<Compile Include="Domain.fs" />
<Compile Include="Workflow.fs" />
<Compile Include="Program.fs" />
```

First rerun the narrow valid-project build, then the complete example gate. The expected-error project remains wrong on purpose and continues to prove FS0039.

Two tempting non-repairs are copying `Capacity` and `BookingRequest` into `Workflow.fs`, which creates competing domain models, and adding more `open` declarations, which cannot expose a file not yet compiled. Repeatedly deleting `obj` is another distraction: a clean build reproduces the same wrong order.

Moving every workflow definition into `Domain.fs` could make compilation succeed, but it changes the module boundary to evade a one-line project repair. Such a redesign needs an architectural reason beyond clearing diagnostics.

## Exercise 2: choose FSI, a test, and a debugger {#exercise-02}

### Give each tool a distinct question {#exercise-02-tools}

The value `Rejected(3, 2)` is correct under the current rule: three requested seats do not fit capacity two. Before changing `decide`, determine why the caller expected acceptance.

Use FSI to isolate the pure rule with controlled values:

```fsharp
let expectOk = function
    | Ok value -> value
    | Error error -> failwithf "invalid probe: %A" error

let capacity = Capacity.create 2 |> expectOk
let request = BookingRequest.create "B-30" 3 |> expectOk

Workflow.decide capacity request
// Rejected (3, 2)
```

This confirms smart construction and the pure function's result for controlled inputs. It does not show what values the application supplied or preserve a regression test after the session closes.

Add a focused example test when this policy is intentional:

```fsharp
[<Fact>]
let ``three seats do not fit capacity two`` () =
    Assert.Equal(
        Rejected(3, 2),
        Workflow.decide capacity request
    )
```

The test is the durable artifact. If the real requirement says capacity should have been four, instead preserve a test at the conversion or caller boundary that currently produces two; do not freeze an incorrect expectation around the pure core.

Set a breakpoint immediately before `Workflow.decide` in the compiled caller. Inspect the validated `SeatCount`, `Capacity`, and caller stack frame. If the values are 3 and 2, trace the capacity to its source. If earlier values differ but `decide` receives 3 and 2, inspect the boundary conversion. Step inside only after confirming the inputs.

The debugger traces one real execution back to its inputs. FSI answers a small model question. The automated test preserves the agreed behavior. Using all three for the same question adds work without increasing confidence.

## Exercise 3: audit a reproducible build {#exercise-03}

### Make each mismatch fail in its owning stage {#exercise-03-audit}

Start from the directory containing the example and record the selected SDK:

```console
dotnet --info
dotnet tool restore
dotnet fantomas . --check
dotnet clean Sample.slnx --configuration Release
dotnet restore Sample.slnx --locked-mode
```

The local tool manifest makes `dotnet fantomas` use the declared 7.0.5 command; the teammate's global installation is not the repository contract. If formatting differs, run the pinned formatter deliberately and review its source-only diff.

Locked restore should fail because the project dependency changed without the corresponding lock graph. That failure confirms the lock check works. Confirm that the package change is intentional, review its compatibility and sources, then regenerate deliberately:

```console
dotnet restore Sample.slnx --force-evaluate
git diff -- "*.fsproj" "*.csproj" "packages.lock.json"
dotnet restore Sample.slnx --locked-mode
```

The shell's wildcard behavior varies, so use your version-control client or explicit project paths if that review command does not expand recursively. The required review is the project reference together with every affected `packages.lock.json`, not a particular shell spelling.

After the locked graph agrees, prove Release compilation and tests without implicit stages:

```console
dotnet build Sample.slnx --configuration Release --no-restore
dotnet test Sample.slnx --configuration Release --no-build
dotnet test Sample.slnx --configuration Release --no-build
```

Update the PackageReference and affected lock files in one deliberate dependency change. Update `.config/dotnet-tools.json` only if the formatter upgrade is also intentional; preferably keep its baseline diff separate. `.editorconfig` changes only for a style-policy decision, and `global.json` changes only for an SDK-policy decision.

A cached Debug success verifies none of these stages. It may reuse assets, perform an implicit restore, omit Release-only compilation, and bypass the pinned formatter. Cleaning is useful here because stale state is part of the stated hypothesis, not because deletion is a universal cure.

## Solution review {#solution-review}

- Repair the earliest missing file dependency; later missing names are a cascade.
- Copying domain types or adding `open` cannot correct project compilation order.
- FSI isolates a pure question, a debugger traces one execution, and a test preserves policy.
- Inspect inputs before stepping into a correct decision function.
- Local tool restore makes a global formatter irrelevant to the repository contract.
- Locked restore should fail when a project dependency changes without its lock graph.
- Regenerate lock files only after accepting the dependency change, then rerun locked mode.
- Separate Release build and test from restore with `--no-restore` and `--no-build`.
- Update package, tool, style, and SDK contracts only for their corresponding decisions.
- A clean build is valuable when stale state is an explicit hypothesis.
