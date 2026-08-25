---
title: "Chapter 45 Solutions"
description: "Extend deterministic artifact automation, evaluate current command-line packages without overstating evidence, and turn the book into a twelve-week F# delivery loop."
translationKey: solutions/ch-45-scripting-packages-next
---

# Chapter 45 Solutions {#overview}

These are engineering solutions, not the only valid code. A strong answer makes semantics explicit before choosing syntax or a package, preserves the manifest script's existing evidence, and marks research separately from executed proof.

## Exercise 1: add exclusion without losing determinism {#exercise-01}

The word “glob” is not a complete contract. Shells and libraries disagree about separators, case, hidden files, character classes, recursive matching, malformed patterns, and whether a directory match prunes traversal. Define those decisions before adding `--exclude`.

### Contract {#exclusion-contract}

One acceptable version-2 contract is:

- `--exclude PATTERN` may repeat after the mode and before the two positional paths;
- patterns and candidate paths use `/` regardless of host OS;
- matching is ordinal and case-sensitive on every platform;
- `*` matches zero or more characters inside one path segment;
- `?` matches exactly one character other than `/`;
- `**` is valid only as a complete segment and matches zero or more complete segments;
- `[abc]`, brace expansion, escaping, absolute paths, empty segments, `.` segments, and `..` segments are unsupported and rejected;
- a pattern names files; `logs/**` is the explicit way to exclude a directory tree;
- the output file is excluded by identity before user rules are evaluated;
- symbolic links remain skipped by the existing traversal policy, independent of exclusions;
- duplicate patterns are removed, then normalized patterns are sorted ordinally;
- an invalid pattern fails before hashing or writing and returns exit `2` as a usage error.

Always-case-sensitive matching may surprise Windows users, but it produces one repository contract. A different answer may intentionally follow filesystem case rules if it records and tests that platform-dependent behavior.

### Schema and planning {#exclusion-schema}

Version the manifest because exclusions change what “complete” means:

```json
{
  "schemaVersion": 2,
  "exclusions": ["**/*.pdb", "logs/**"],
  "files": []
}
```

Including normalized rules makes two manifests with the same current file rows but different future coverage distinguishable. It also lets a reviewer see why an artifact is absent.

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

`write` and `check` must call the same planner. A separate check implementation is likely to drift. Keep output-file exclusion outside user patterns so no caller can accidentally make the manifest hash itself.

Directory pruning is an optimization with semantic risk. A `logs/**` rule may safely prune `logs`, but a general matcher with negation or future include rules might need to descend. This contract has no negation, so a tested prefix analysis may prune; the first implementation can remain simpler and filter file paths after traversal.

### Matcher choice {#matcher-choice}

Do not translate arbitrary user patterns directly into unbounded regular expressions. Either implement only the grammar above with a linear segment matcher and bounded pattern length, or evaluate a maintained glob package against the exact semantics.

For this answer, choose a package only if a spike proves all of the following:

- explicit ordinal case-sensitive comparison is supported;
- `**` and separator normalization match the written contract;
- malformed patterns can be rejected before filesystem effects;
- traversal can remain under the manifest script's link and root policy rather than letting the library walk independently;
- the package target and transitive graph fit the promoted console project;
- a lock file and locked restore reproduce the graph.

If no candidate meets those constraints, the intentionally tiny grammar is safer than advertising full glob compatibility. Name it “artifact pattern syntax,” document it, and reject unsupported syntax instead of approximating a shell.

### Test matrix {#exclusion-tests}

Use pure path/pattern tests plus real temporary directories:

| Case | Expected evidence |
|---|---|
| no rules | version 2 contains the same two files as the manifest script |
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

Simulate Windows-style strings in pure tests on every OS, then add filesystem tests on actual Windows and Unix-like runners. Do not call a Linux-only path test cross-platform evidence.

## Exercise 2: write a package adoption record {#exercise-02}

Start with the requirement, not the candidates. The promoted tool needs `write` and `check` subcommands, two required path arguments, repeatable `--exclude`, generated help, predictable usage failures, testable parsing without process termination, and ordinary .NET 10 publishing. Shell completion and native AOT are desirable but not release requirements.

### Candidate record as of 2026-08-25 {#candidate-record}

The official NuGet pages reviewed for this answer show:

| Choice | Reviewed version | Relevant shape | Friction or unanswered evidence |
|---|---:|---|---|
| hand-written parser | repository code | zero package graph; exact control over three tokens today | help, repeated options, aliases, diagnostics, and future subcommands become our maintenance |
| Argu | 6.2.5 | F#-oriented declarative parser using discriminated unions; targets .NET Standard 2.0 | package last updated in December 2024; brings FSharp.Core and `System.Configuration.ConfigurationManager`; trimming/AOT behavior needs a real spike |
| System.CommandLine | 2.0.11 | commands, options, arguments, validation, help, completions, and async actions; targets .NET 8 and .NET Standard 2.0 | object/builder API is C#-shaped; F# overload/null adaptation and exact help/error stability need a spike |

Both package versions are research facts checked on that date, not executable dependencies of the book site. The shown manifest script covers only its BCL parser until you run package-specific spikes.

Do not compare download counts as if they were correctness. Inspect owners, MIT licenses, source repositories, dependency tabs, release history, advisories, and the exact `.nupkg`; then run restore audit under the adopting project's effective sources.

### Focused spike {#parser-spike}

Create a disposable `net10.0` F# console project for each package. Lock its exact direct version and generated closure. Drive the parser as a pure-ish adapter from `string array` to:

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

Capture restore, lock diff, build warnings, package audit, published size, startup, and the amount of adapter code. Do not infer AOT support from target-framework compatibility.

### Reversible decision {#package-decision}

Keep the hand-written parser in the manifest script because the current contract has only a mode plus two paths. For the promoted tool with repeatable exclusions and generated help, provisionally choose System.CommandLine `2.0.11` if the spike passes the vectors. Its command model matches the planned CLI and the recent stable release has a direct .NET ownership path.

This is not a universal preference over Argu. Choose Argu if the team values a DU-declared F# surface and its spike produces clearer code with acceptable maintenance and deployment evidence. The deciding artifact is the adapter and test matrix, not language branding.

The adoption change should include:

- exact PackageReference in the console project;
- committed `packages.lock.json` and CI `dotnet restore --locked-mode`;
- effective source and Package Source Mapping review;
- restore audit with warnings handled by policy;
- one module owning every package-specific type;
- golden CLI contract tests and a published-process smoke test;
- an update owner and quarterly review trigger;
- a removal note: replace only the parsing adapter, retain `Command` and all core functions.

If the package fails a required vector, retain hand parsing or spike the other candidate. Do not expand the public CLI merely to match a library's defaults.

## Exercise 3: plan the next twelve weeks {#exercise-03}

This example chooses the tooling-and-libraries track. Each four-week increment ships a usable boundary and ends with a review that can reduce scope.

### Weeks 1–4: promote the manifest script without changing its semantics {#weeks-01-04}

**Outcome:** a `net10.0` console project with `Manifest.Core`, a thin CLI, and a published executable that preserves the manifest script's schema version 1.

Work includes:

- move manifest entries, rendering, planning outcomes, and path normalization into ordered `.fs` modules;
- keep filesystem and console effects behind narrow functions;
- add example-based tests for rendering and process-level tests for exits `0`, `1`, and `2`;
- run Windows and Linux/macOS temporary-directory tests for separators, links where supported, and output exclusion;
- publish a framework-dependent artifact and run it outside the source tree;
- retain the script as a small compatibility launcher only if users need it; otherwise document the new command.

Revisit Chapters 9, 16–18, 21, 26, 28, and 30. Use no new runtime package. The real boundary is filesystem plus process CLI. Diagnostics record command, exit, stderr category, SDK, OS, and output hash.

Review question: “Can any invalid argument or filesystem failure produce a partial destination or success exit?” Reverse if the project adds ceremony without improving tests, distribution, or locked behavior.

### Weeks 5–8: add versioned exclusions and dependency evidence {#weeks-05-08}

**Outcome:** schema version 2, explicit exclusion semantics from Exercise 1, backward-readable version 1 manifests, and a locked parser decision.

Work includes:

- model `ManifestV1` and `ManifestV2` separately and define one directional upgrade;
- add normalized exclusion rules to desired state and keep `write`/`check` on one planner;
- execute the parser spike from Exercise 2 before adding any package;
- if adopted, commit the exact package and closure lock, plus restore audit evidence;
- fuzz or property-test ordering, duplicate rules, and render/parse stability within bounded generators;
- add a migration fixture and reject unknown future schema versions without overwriting them;
- measure traversal and hashing on a representative tree before parallelizing.

Revisit Chapters 10–15, 27–31, and 37. Package budget is one CLI parser and zero glob packages unless the matcher spike proves otherwise. The real boundary is schema compatibility plus cross-platform matching.

Review question: “Can version 2 explain every omitted file and preserve a version 1 artifact without silent reinterpretation?” Remove the parser package if its adapter and graph exceed the behavior it replaces.

### Weeks 9–12: distribute and operate the tool {#weeks-09-12}

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

Review question: “Can a new contributor restore, verify, upgrade, diagnose, and remove this tool without private machine knowledge?” If not, reduce distribution ambition or make the missing ownership explicit.

### Advanced-feature budget {#advanced-budget}

None of the three increments requires quotations, SRTP, flexible types, or custom byrefs. Ordinary records, unions, functions, interfaces at effects, tasks for cancellation, and arrays/streams are sufficient.

Consider Span only if a profile shows boundary copying dominates and a synchronous lifetime can be proved. Consider SRTP only if several concrete algorithms genuinely need one member-constrained abstraction. Quotations would make sense only if the product begins consuming or producing expression trees. Flexible types may appear in a library signature; recognize them before deciding whether to expose them.

That restraint is part of the plan, not missing ambition. The learning goal is to ship increasingly trustworthy F#, then deepen a language feature when the system supplies a reason.

## Review checklist {#review-checklist}

- Are pattern and CLI semantics written before implementation?
- Do `write` and `check` share one desired-state planner?
- Are schema, path, ordering, encoding, and case rules stable and tested?
- Are link, output-file, untrusted-input, and destructive-target policies explicit?
- Are researched package facts dated and separated from executed repository evidence?
- Does the lock cover the application closure, and does CI enforce locked restore?
- Can package-specific types be removed at one adapter?
- Does every four-week increment end in a runnable artifact and a falsifiable review question?
- Are advanced features justified by an observed constraint rather than a curriculum checklist?

[Return to Chapter 45](../part-07/ch-45-scripting-packages-next).
