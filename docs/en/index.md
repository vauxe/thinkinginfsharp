---
title: Thinking in F#
description: Learn functional modeling and production .NET engineering from F# itself.
translationKey: index
---

# Thinking in F# {#overview}

This site teaches F# from the beginning. It starts with expressions, values, types, and functions, then grows toward effects, testing, .NET interoperability, and complete applications.

[Open the complete contents](./contents) or [start with Chapter 1](./part-01/ch-01-first-session). Read the first three parts in order; later parts are there when you need their topics.

## What you will learn to do {#capabilities}

- express business rules with types and make illegal states difficult to create;
- separate pure logic from effects while handling real I/O honestly;
- build testable, diagnosable F# programs that work naturally with C#;
- judge where F# fits in web, data, cloud, desktop, automation, and Unity work.

## The quickest start {#quick-start}

Install a currently supported [.NET SDK](https://dotnet.microsoft.com/en-us/download), then verify it in a terminal:

```console
dotnet --version
```

Start F# Interactive:

```console
dotnet fsi
```

At the prompt, enter:

```fsharp
20 + 22;;
```

FSI should report the value `42` and the type `int`. Chapter 1 explains exactly what that output means and how to save larger examples as `.fsx` scripts.
