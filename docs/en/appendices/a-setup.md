---
title: "Appendix A: Set Up F#"
description: "Install the .NET SDK, start F# Interactive, run a script, and create a first project."
translationKey: appendices/a-setup
---

# Appendix A: Set Up F# {#overview}

You can read the whole site without installing anything. To run the examples, install the .NET SDK; it includes the F# compiler, F# Interactive, and the `dotnet` command.

## 1. Install a supported .NET SDK {#install-sdk}

Download a supported SDK from the [.NET download page](https://dotnet.microsoft.com/en-us/download). An SDK is required, not only a runtime.

Open a new terminal after installation and run:

```console
dotnet --info
dotnet fsi --help
```

If both commands work, the toolchain is ready. Examples in this book were reviewed with F# 10 and .NET 10; durable language fundamentals also work on later compatible versions, while exact diagnostics may change.

## 2. Try F# Interactive {#fsi}

Start the interactive prompt:

```console
dotnet fsi
```

Enter this expression and finish the submission with `;;`:

```fsharp
20 + 22;;
```

FSI should report the value `42` and its type `int`. Exit with `#quit;;`.

## 3. Run a script {#script}

Create `lesson.fsx` with this content:

```fsharp
let greet name = $"Hello, {name}!"
printfn "%s" (greet "F#")
```

Run it from the directory containing the file:

```console
dotnet fsi --exec lesson.fsx
```

A script is the easiest way to save and rerun a small experiment. Most early chapter examples can be copied into an `.fsx` file this way.

## 4. Create a project {#project}

Use a project when code needs multiple files, packages, tests, or publishing:

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

The generated `.fsproj` records the target framework and source-file order. F# compiles files in project order, so definitions must appear before files that use them.

## Choose an editor only when useful {#editor}

Any text editor works. An editor with F# language support adds type information, completion, navigation, formatting, and diagnostics, but it does not replace the command-line build. Keep the terminal commands working so the project remains reproducible outside one editor.

## Common setup problems {#troubleshooting}

- **`dotnet` is not found:** open a new terminal and verify that the SDK installation directory is on `PATH`.
- **Only runtimes are listed:** install an SDK from the download page.
- **FSI keeps waiting:** close the current expression, string, or bracket; interactive submissions end with `;;`.
- **A project cannot restore packages:** check the network and configured NuGet sources, then retry `dotnet restore`.
- **A diagnostic differs from the book:** compare `dotnet --version`; compiler wording and diagnostic details can change between SDK versions.

Continue with [Chapter 1](../part-01/ch-01-first-session) once `dotnet fsi` works.

## Sources {#sources}

- [.NET downloads](https://dotnet.microsoft.com/en-us/download)
- [Microsoft Learn: Get started with F# on the command line](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn: F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# project files](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
