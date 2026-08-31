---
title: "Chapter 16: Modules, Namespaces, Projects, and Compiler Settings"
description: "Turn a script into a dependency-ordered F# project, separate namespaces from modules, and make compiler contracts—including minimal nullable annotations—explicit."
translationKey: part-03/ch-16-modules-namespaces-projects
---

# Chapter 16: Modules, Namespaces, Projects, and Compiler Settings {#overview}

A script can hide a useful fact: a program has a dependency direction. Once domain definitions, workflows, and startup code live in separate files, F# asks you to state that direction in compilation order. A later file can use definitions the compiler has already seen; an earlier file cannot reach forward to a later one.

This rule is not ceremony added by an editor. It gives a project an inspectable architecture. In this chapter's executable project, `Domain.fs` knows nothing about the workflow, `Workflow.fs` depends on the domain, and `Program.fs` composes both. The project file records exactly that order.

## From a script to a project {#script-to-project}

Use `.fsx` scripts while discovering expressions and APIs. Move to a project when code needs several files, package or project references, repeatable compiler settings, tests, or a deployable output. The syntax inside functions barely changes; the project now states its compilation rules explicitly.

The chapter example has this physical layout:

```text
./
├── Ch16.fsproj
├── Domain.fs
├── Workflow.fs
└── Program.fs
```

The folder is useful to humans, but it does not declare an F# namespace. The first declarations inside the source files do that, while `Ch16.fsproj` decides which files participate and in which order.

## Tour the three source files {#project-tour}

Every source file begins with the same namespace:

```fsharp
namespace ThinkingInFSharp.Ch16
```

Each then places definitions in a focused module:

- `Domain` defines protected identifiers, seat counts, capacity, requests, and validation;
- `Workflow` defines the decision union and pure decision functions;
- `Program` contains composition and the process entry point.

The resulting qualified names expose both layers, such as `ThinkingInFSharp.Ch16.Domain.BookingId` and `ThinkingInFSharp.Ch16.Workflow.decide`. A filename helps navigation, but it is not automatically part of either qualified name.

The dependency direction is small enough to see:

```text
compiler input:  Domain.fs  ──▶  Workflow.fs  ──▶  Program.fs
available names: ───────────────────────────────────────────▶
```

Arrows mean “must be seen before,” not “calls at runtime.” `Domain.fs` remains usable without `Workflow.fs`; the reverse is not true.

## Compilation order is part of the program {#file-order}

The project file lists source inputs in dependency order:

```xml:line-numbers [Ch16.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Domain.fs" />
    <Compile Include="Workflow.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
`<Compile Include="Domain.fs" />` contributes a compiler input. The next item can use its definitions, and the item after that can use both preceding files. Reordering editor tabs or moving files in the folder does not change compilation order; changing the `<Compile>` sequence does.

The same ordering rule applies within a source file: definitions normally use earlier definitions. F# has explicit constructs for genuine recursion, but ordinary program layers should read from foundations toward composition.

`Program.fs` is last because it depends on both other modules and contains `[<EntryPoint>]`. Keeping startup composition in the final layer also prevents domain code from depending on console concerns.

### The wrong order is a real compiler error {#wrong-order}

This minimal expected-error project deliberately lists `Workflow.fs` first:

```xml:line-numbers [Ch16WrongOrder.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Workflow.fs" />
    <Compile Include="Domain.fs" />
  </ItemGroup>
</Project>
```
When the compiler reaches this line in `Workflow.fs`:

```fsharp
open ThinkingInFSharp.Ch16.Domain
```

it has not processed `Domain.fs`, so `Domain` is unknown and compilation reports `FS0039`. This is not a warning to suppress. Put the provider before the consumer.

If two files appear to require each other, first inspect the design. Often shared types belong in an earlier domain file, while operations that coordinate both sides belong in a later workflow file. A cycle is useful architectural feedback. Recursive modules or `namespace rec` exist for genuine mutual recursion, but they should not become a shortcut for erasing ordinary layers.

## Namespace and module solve different problems {#namespace-vs-module}

A namespace organizes types and modules under a stable qualified name. It can be continued across files and even across assemblies. It cannot directly contain F# values or functions:

```fsharp
namespace Booking

let normalize raw = raw.Trim() // invalid: a namespace cannot contain this value
```

Place the binding in a module instead:

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

A module groups F# types, values, functions, and nested modules in one named scope. A file can use a top-level `module Booking.Text` declaration for its whole contents, or it can use local `module Text =` declarations under a namespace as the example does. In a multi-file project, begin each file with an explicit namespace or module declaration; do not rely on the single-file application's implicit module behavior.

A practical division is:

| Construct | Owns | Can span files? | Typical role |
|---|---|---:|---|
| Namespace | Types and modules | Yes | Stable product or library naming |
| Module | Types, values, functions, nested modules | Not as one declaration | Cohesive behavior and F# API |
| File | Source text in one compiler position | No | One dependency step and review unit |

Do not create one module per type mechanically. Group definitions that change together and expose a coherent vocabulary. Chapter 17 will make that public vocabulary explicit with signature files.

## `open` shortens names; it does not create dependencies {#open}

`Workflow.fs` contains:

```fsharp
open ThinkingInFSharp.Ch16.Domain
```

After that declaration, accessible names from `Domain` can be written as `Capacity`, `BookingRequest`, and `BookingId` instead of with their full path. `open` changes name lookup within the following scope. Project membership or an assembly reference supplies the definitions; compile order supplies their visibility; access modifiers continue to control which names are available.

Qualification is often clearer where modules meet:

```fsharp
let requested = request |> Domain.BookingRequest.seats |> Domain.SeatCount.value
```

Opening a focused domain module is often clearer inside a workflow that uses it throughout. Avoid opening broad modules with common names merely to save characters; later `open` declarations can affect which unqualified name wins. You can also add `[<RequireQualifiedAccess>]` to an F# module or union when consumers should keep the owner visible.

## Project, solution, and assembly are different levels {#project-contract}

The project file is an MSBuild XML document. For a normal SDK-style F# build, it defines one compilation and produces an assembly—usually a `.dll`, plus an executable host when `OutputType` is `Exe`. A solution groups projects for restore, build, and test operations; it is not another namespace and does not merge their source files into one compilation.

Two dependency mechanisms therefore operate at different levels:

| Mechanism | Scope | Meaning |
|---|---|---|
| `<Compile Include="..." />` | Inside one F# project | Adds source files in compiler order |
| `<ProjectReference Include="..." />` | Between projects | Builds and references another project's output |

The test project uses `ProjectReference` to consume the chapter project. Its test files still have their own `<Compile>` order. A namespace with the same name may appear in both assemblies, but the reference—not the namespace spelling—makes external definitions available.

## Compiler settings determine the build {#settings}

Read settings by the question they answer:

| Setting | Question answered |
|---|---|
| `global.json` `sdk.version` and `rollForward` | Which installed .NET SDK may run CLI/build tooling? |
| `<TargetFramework>net10.0</TargetFramework>` | Which target framework APIs and runtime does this project target? |
| `<LangVersion>10.0</LangVersion>` | Which F# language version does the compiler accept? |
| `<Nullable>enable</Nullable>` | Should F# perform its opt-in nullness analysis? |
| `<OutputType>Exe</OutputType>` | Is the project packaged as an executable rather than the default library? |
| `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | Must compiler warnings fail the build? |

These settings govern separate decisions. The SDK selects the build toolset, `TargetFramework` selects the APIs and runtime, and `LangVersion` selects accepted F# features. A reproducible project records each choice.

Shared policy can live in `Directory.Build.props`; MSBuild imports it for descendant projects. A larger codebase can centralize `LangVersion`, nullable checking, warnings as errors, deterministic output, and locked package restore there. Keep a small standalone teaching project self-contained; centralize stable policy only when several real projects share it.

Prefer fixing a diagnostic to globally suppressing it. Warnings-as-errors makes that discipline reproducible in local builds and CI; it does not mean every possible optional warning has been enabled.

## The minimum nullable-reference model {#nullable-minimum}

With F# null checking enabled, `string` means a non-null reference and `string | null` admits null. The annotation guides compile-time analysis but still uses an ordinary .NET reference at runtime. Validate values that arrive from foreign or unchecked code.

The domain constructor deliberately accepts nullable text:

```fsharp
let create (raw: string | null) =
    match raw with
    | null -> Error MissingBookingId
    | value when System.String.IsNullOrWhiteSpace value -> Error MissingBookingId
    | value -> Ok(BookingId(value.Trim()))
```

After the `null` case, analysis knows the remaining `value` is non-null. The boundary converts absence into a domain error before a protected `BookingId` is created. Internal functions can then consume the protected value without repeating null checks.

Use `option` for absence in an F# domain model. Use `T | null` when an external .NET interface actually admits null. Chapter 19 combines nullable value types, annotations from other .NET languages, `Null`/`NonNull` patterns, `option`, and runtime validation into one model.

### Wrappers must preserve nullable annotations {#nullable-propagation}

`BookingRequest.create` forwards its text to `BookingId.create`, so its own public input states the same contract:

```fsharp
let create (rawId: string | null) rawSeats =
    match BookingId.create rawId with
    | Error error -> Error(InvalidBookingId error)
    | Ok bookingId ->
        // validate seats and assemble the request
        // ...
```

Leaving `rawId` unannotated makes inference choose a non-null `string` parameter. Passing `null` in the focused test then produces nullable-mismatch diagnostic `FS3261`. Fix the wrapper's parameter type; do not disable null checking or scatter unchecked conversions.

Do not annotate every reference as nullable “just in case.” That loses useful type information and moves checks inward. Admit null only where callers can supply it, validate there, and keep the core model non-null by construction.

## Let dependency shape guide the architecture {#dependency-shape}

The compiler only enforces “earlier before later,” not a good choice of layers. Use the order to make architectural intent reviewable:

```text
stable domain types and invariants
             ↓
pure policies and workflows
             ↓
I/O adapters and application composition
```

Earlier files should usually be more stable and know less about infrastructure. Later files may depend on them and perform composition. If `Domain.fs` needs `Program.fs`, moving `Program.fs` earlier may silence one error while reversing the intended architecture. Move the misplaced abstraction instead.

Keep each project small enough to have one reason to change, but do not split solely to create more assemblies. A new project introduces a real reference and deployment boundary; a new module or file may be sufficient for organization. Use projects when independent reuse, build policy, ownership, or dependency direction warrants the boundary.

## Build and run the project {#build-test}

From the repository root:

```console
dotnet build examples/chapters/ch16/Ch16.fsproj -c Release
dotnet run --project examples/chapters/ch16/Ch16.fsproj -c Release --no-build
```

The executable prints:

```text
accepted:REQ-16 remaining=1
```

The repository check also builds the reversed fixture at `examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj` and requires `FS0039`. Together, the successful executable and expected failure verify both sides of the compilation-order rule.

## Exercises {#exercises}

### Exercise 1: write the dependency order {#exercise-01}

A project has `Domain.fs`, `Pricing.fs`, and `Program.fs`. `Pricing` uses domain types; `Program` uses both. Write the three `<Compile>` items in a valid order. Then put `Pricing.fs` first and predict where and why `FS0039` appears.

Explain why moving the files between folders without changing declarations or project items does not fix the dependency.


::: details Answer

#### Valid project order {#exercise-01-order}

The dependencies are:

```text
Domain.fs  ──▶  Pricing.fs  ──▶  Program.fs
     └──────────────────────────▶
```

Therefore the project items are:

```xml
<ItemGroup>
  <Compile Include="Domain.fs" />
  <Compile Include="Pricing.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

`Domain.fs` provides the independent vocabulary. `Pricing.fs` may use it because the compiler has already processed it. `Program.fs` comes last because it consumes both providers.

More than one topological order can be valid when files are independent. Here the stated dependencies force all three positions. Do not alphabetize the items unless alphabetical order also happens to respect the graph.

#### Diagnose the reversed order {#exercise-01-diagnostic}

This order is invalid:

```xml
<ItemGroup>
  <Compile Include="Pricing.fs" />
  <Compile Include="Domain.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

Compilation reaches `Pricing.fs` before `Domain.fs`. `FS0039` appears at its `open` declaration or first qualified use of the missing `Domain` module or one of its types. The exact location depends on which unavailable name is encountered first; the cause is the same forward reference.

Folders do not participate in F# name resolution or compiler input order. Moving `Domain.fs` into `Core` changes only its path; the project item must change too. The folder neither moves the file earlier nor adds `Core` to its namespace. Source declarations establish names, and `<Compile>` items establish order.

:::

### Exercise 2: repair scope and choose qualification {#exercise-02}

Repair this invalid file while keeping `Booking.Text.normalize` as the public qualified name:

```fsharp
namespace Booking

let normalize (raw: string) = raw.Trim()
```

In a consumer module, show one call using the full name and one call after an `open` declaration. Explain exactly what `open` changes and what it does not change.


::: details Answer

#### Put the value in a module {#exercise-02-fix}

The requested public name is obtained by placing `Text` under the `Booking` namespace:

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

The namespace may contain the module, and the module may contain the `let`-bound function. Moving only the `let` one indentation level has no meaning without the `module Text =` declaration.

#### Qualified and opened calls {#exercise-02-open}

A consumer can retain the whole owner at the call site:

```fsharp
module Booking.Consumer

let normalizeQualified raw =
    Booking.Text.normalize raw
```

Or it can open the module before the following use:

```fsharp
module Booking.Consumer

open Booking.Text

let normalizeOpened raw =
    normalize raw
```

`open Booking.Text` adds accessible members to short-name lookup in the following scope. The original names, definitions, file order, assembly references, and access levels stay unchanged. Both forms therefore require either an earlier defining file in this project or a reference to the defining assembly.

Qualification is the better default when a short name is ambiguous or only used once. A focused `open` is reasonable when the consumer repeatedly speaks that module's vocabulary.

:::

### Exercise 3: propagate one nullable boundary {#exercise-03}

Assume `BookingId.create : (string | null) -> Result<BookingId, BookingIdError>`. Write `BookingRequest.create` so it accepts the same nullable text contract, forwards it, and maps the error into `InvalidBookingId`.

Test both `null` and a non-null identifier. Explain why the parameter annotation belongs on the wrapper and why this boundary type is not a replacement for `option` in the domain model.


::: details Answer

#### State the wrapper's real contract {#exercise-03-contract}

This compact model makes both the inner and outer parameter explicit:

```fsharp
open System

type BookingIdError =
    | MissingBookingId

type BookingId = private BookingId of string

module BookingId =
    let create (raw: string | null) =
        match raw with
        | null -> Error MissingBookingId
        | value when String.IsNullOrWhiteSpace value -> Error MissingBookingId
        | value -> Ok(BookingId(value.Trim()))

type BookingRequestError =
    | InvalidBookingId of BookingIdError

type BookingRequest =
    private
        { Id: BookingId
          Seats: int }

module BookingRequest =
    let create (rawId: string | null) seats =
        match BookingId.create rawId with
        | Error error -> Error(InvalidBookingId error)
        | Ok bookingId -> Ok { Id = bookingId; Seats = seats }
```

`BookingRequest.create` promises that callers may supply `null`, then immediately delegates validation and preserves the error context. The production chapter example additionally validates `SeatCount`; that separate invariant does not change the nullable-reference reasoning.

#### Test both sides of the boundary {#exercise-03-tests}

```fsharp
match BookingRequest.create null 2 with
| Error(InvalidBookingId MissingBookingId) -> ()
| other -> failwithf "Unexpected nullable result: %A" other

match BookingRequest.create "REQ-16" 2 with
| Ok _ -> ()
| other -> failwithf "Unexpected valid result: %A" other
```

Without `(rawId: string | null)`, inference makes the wrapper accept non-null `string`, even though the called function accepts a wider input. A test that passes `null` then conflicts with the wrapper's inferred contract. Annotating the wrapper records what its callers can actually provide.

`string | null` models a CLR reference boundary that can contain null. It should be checked and normalized at that boundary. `option<string>` is an explicit F# domain value with `Some` and `None`, pattern matching, and composition functions. One does not silently substitute for the other; convert deliberately when crossing the boundary.

:::


Chapter 17 uses signature files to restrict the public API to the types and operations that a component deliberately exposes.

## Sources {#sources}

- [Microsoft Learn: F# modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn: F# namespaces](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces)
- [Microsoft Learn: `open` declarations](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/import-declarations-the-open-keyword)
- [Microsoft Learn: dependency-ordered F# project example](https://learn.microsoft.com/en-us/odata/webapi-8/tutorials/basic-crud-in-fsharp)
- [Microsoft Learn: F# compiler options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn: F# null values and null checking](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: `global.json` overview](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Microsoft Learn: common MSBuild project items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
