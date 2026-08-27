---
title: "Chapter 45 Solutions"
description: "Extend deterministic artifact automation, evaluate command-line packages without claiming more than the checks show, and turn the book into a twelve-week F# delivery loop."
translationKey: solutions/ch-45-scripting-packages-next
---

# Chapter 45 Solutions {#overview}

These are engineering solutions, not the only valid implementations. A strong answer defines behavior before choosing syntax or a package, preserves what the manifest script already verifies, and separates researched facts from results produced by running the code.

## Exercise 1: add exclusion without losing determinism {#exercise-01}

The word “glob” is not a complete contract. Shells and libraries use different rules for separators, case, hidden files, character classes, recursion, malformed patterns, and directory pruning. Define those rules before adding `--exclude`.

### Contract {#exclusion-contract}

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

### Schema and planning {#exclusion-schema}

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

### Matcher choice {#matcher-choice}

Do not translate arbitrary user patterns directly into unbounded regular expressions. Either implement only the grammar above with a linear segment matcher and a pattern-length limit, or test a maintained glob package against every stated rule.

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

## Exercise 2: write a package adoption record {#exercise-02}

Start with the requirements, not the candidates. The promoted tool needs:

- `write` and `check` subcommands;
- two required path arguments and repeatable `--exclude` options;
- generated help and predictable usage failures;
- a parser that tests can call without terminating the process;
- standard .NET 10 publishing.

Shell completion and native AOT are desirable, but they are not release requirements.

### Candidate record as of 2026-08-25 {#candidate-record}

The official NuGet pages reviewed for this answer show:

| Choice | Reviewed version | Relevant fit | Cost or open question |
|---|---:|---|---|
| hand-written parser | repository code | no dependency graph; exact control over today's three arguments | we must maintain help, repeated options, aliases, diagnostics, and future subcommands |
| Argu | 6.2.5 | F#-oriented declarative parser using discriminated unions; targets .NET Standard 2.0 | package last updated in December 2024; brings FSharp.Core and `System.Configuration.ConfigurationManager`; trimming/AOT behavior needs a real spike |
| System.CommandLine | 2.0.11 | commands, options, arguments, validation, help, completions, and async actions; targets .NET 8 and .NET Standard 2.0 | API uses object and builder patterns common in C#; F# overload/null adaptation and help/error stability need a spike |

Both package versions are facts checked on that date; neither is a dependency of the book site. The manifest script verifies only its BCL parser until the package-specific spikes are run.

Do not treat download counts as a measure of correctness. Inspect maintainers, MIT licenses, source repositories, dependency tabs, release history, advisories, and the exact `.nupkg`. Then run a restore audit with the adopting project's effective package sources.

### Focused spike {#parser-spike}

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

### Reversible decision {#package-decision}

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

## Exercise 3: plan the next twelve weeks {#exercise-03}

This example chooses the tooling-and-libraries track. Each four-week increment ships a usable boundary and ends with a review that can reduce scope.

### Weeks 1–4: promote the manifest script without changing its semantics {#weeks-01-04}

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

### Weeks 5–8: add versioned exclusions and dependency verification {#weeks-05-08}

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

Review question: “Can a new contributor restore, verify, upgrade, diagnose, and remove this tool without undocumented machine knowledge?” If not, reduce the distribution scope or assign responsibility for the missing work.

### Advanced-feature budget {#advanced-budget}

None of the three increments requires quotations, SRTP, flexible types, or custom byrefs. Ordinary records, unions, and functions are sufficient. Use interfaces to isolate side effects, tasks to support cancellation, and arrays or streams for data movement.

Consider `Span` only if profiling shows that boundary copying dominates and the lifetime is demonstrably synchronous. Consider SRTP only if several concrete algorithms need the same member-constrained abstraction. Quotations make sense only if the product consumes or produces expression trees. Flexible types may appear in a library signature; identify them before deciding whether to expose them.

That restraint is part of the plan, not missing ambition. The learning goal is to ship increasingly trustworthy F#, then deepen a language feature when the system supplies a reason.

## Review checklist {#review-checklist}

- Are pattern and CLI semantics written before implementation?
- Do `write` and `check` share one desired-state planner?
- Are schema, path, ordering, encoding, and case rules stable and tested?
- Are link, output-file, untrusted-input, and destructive-target policies explicit?
- Are researched package facts dated and separated from checks run in the repository?
- Does the lock cover the application closure, and does CI enforce locked restore?
- Can package-specific types be removed at one adapter?
- Does every four-week increment end in a runnable artifact and a falsifiable review question?
- Are advanced features justified by an observed constraint rather than a curriculum checklist?

[Return to Chapter 45](../part-07/ch-45-scripting-packages-next).
