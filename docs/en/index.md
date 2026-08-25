---
layout: doc
title: Thinking in F#
description: Learn functional modeling and production .NET engineering from F# itself.
translationKey: index
kind: home
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-part-01-booking-basics
exerciseIds: []
termIds: []
sources:
  - id: dotnet-10-download
    url: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    checked: "2026-08-25"
---

# Thinking in F# {#overview}

This book is for developers who can already program but have not yet studied functional programming systematically. It begins with expressions, values, types, and functions, then moves toward testable workflows, asynchronous and concurrent code, .NET interoperability, and a complete event-booking system.

[Read the preface](./preface/) to choose one of three routes: a six-chapter quick start, systematic study, or a C#/.NET transition path. Each route uses the same complete English edition; Chinese is never required.

## What you will learn to do {#capabilities}

- express business rules with types and make illegal states difficult to create;
- separate pure logic from effects while handling real I/O honestly;
- build testable, diagnosable F# programs that work naturally with C#;
- judge where F# fits—and where it creates friction—in web, data, cloud, desktop, automation, and Unity work.

Every valid code sample comes from executable source shared by both editions.

## The quickest start {#quick-start}

The book assumes basic programming experience, but neither functional programming nor Chinese. With the repository and its [.NET SDK 10.0.301 reproduction baseline](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) ready, verify the toolchain from the repository root:

```console
dotnet --version
```

The repository's `global.json` selects the SDK feature band used to verify the book. Read [Chapter 1](./part-01/ch-01-first-session) through [Chapter 6](./part-01/ch-06-recursion-folds) in order. In each chapter, predict the shared script's behavior, run it, then complete the exercises independently before reading the solutions.

After the six chapters, run the first booking slice:

```console
dotnet fsi --exec examples/capstone/part-01/BookingBasics.fsx
```

You should see:

```text
Rows: valid=4 invalid=2
Labels: ["B-101:Lin:3"; "B-102:Ada:2"; "B-103:Sam:4"; "B-104:Mira:2"]
Accepted IDs: ["B-101"; "B-102"; "B-104"]
Rejected IDs: ["B-103"]
Capacity: booked=7 remaining=1
```

The script deliberately uses only values, functions, tuples, list patterns, `option`, pipelines, and folds introduced in Part I. Of six fixed input rows, one has the wrong shape and one has invalid seat text. The four remaining rows become labels and are then folded in order against capacity `8`. When `B-103` requests `4` seats, only `3` remain, so it is rejected; the final summary is `7` booked and `1` remaining. The small literal match here is not a general integer parser. Later parts progressively replace these temporary representations with records, unions, and real boundaries.
