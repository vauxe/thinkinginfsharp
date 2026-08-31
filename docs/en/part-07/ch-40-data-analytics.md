---
title: "Chapter 40: Data, Type Providers, Analytics, and Machine Learning"
description: "Choose data access, query, analytical, visualization, and machine-learning tools from schema ownership, execution semantics, scale, and evidence."
translationKey: part-07/ch-40-data-analytics
---

# Chapter 40: Data, Type Providers, Analytics, and Machine Learning {#overview}

F# makes data transformation pleasant because records, units of measure, pattern matching, sequences, and higher-order functions describe structure and flow directly. Library choice, schema trust, query placement, memory scale, and model usefulness still require separate engineering decisions and validation.

There is no single “F# data stack.” Start by asking who controls the data and schema, where operations must run, how large and sensitive the workload is, and what must remain reproducible. A small local CSV project template leads into relational access, analysis, visualization, and machine learning.

The current repository no longer contains the former `examples/ecosystem/data` project or its CSV test fixture. The code in this chapter supplies a reconstructable file layout; it is not a currently executable repository path.

Keep the vocabularies separate: “type provider” and “query expression” name F# language/tooling mechanisms; `CsvProvider`, Deedle, Dapper, and ML.NET are library or platform APIs; “data frame,” “feature,” and “model drift” are data/ML terms, not names for F# syntax.

## Start with the data contract {#data-contract}

Before choosing a library, write down these facts:

| Question | Why it changes the design |
|---|---|
| who owns the schema? | your migration, a partner API, and a user-uploaded file have different change contracts |
| is the shape closed or open? | a fixed table can map to records; sparse or evolving features may need a dynamic representation |
| how large can the data become? | six rows, six million rows, and an unbounded stream require different evaluation models |
| where must filtering and aggregation run? | transferring a whole table before filtering may be incorrect operationally even if results match |
| are writes involved? | transactions, concurrency, retries, and idempotency matter more than query syntax |
| what is missing, invalid, or late? | absence, corruption, and eventual arrival are different domain states |
| what is sensitive? | samples, logs, plots, caches, model files, and notebook outputs can all leak data |
| what must be reproduced? | a one-off exploration and a regulatory training run need different provenance |

Choose representation after answering those questions:

| Workload | Useful first representation | Reconsider when |
|---|---|---|
| small, bounded transformation | records plus `Array`, `List`, or `Seq` | labels, alignment, or missing-data operations dominate |
| relational application state | explicit repository/port over a database adapter | analysis queries fight the transactional model |
| labeled heterogeneous analysis | a data frame or series | memory, typing, or deployment constraints dominate |
| large analytical scan | storage engine, columnar format, or warehouse query | local iteration becomes the bottleneck |
| events arriving over time | `IAsyncEnumerable`, channel, or streaming system | a batch snapshot is the actual business unit |
| model training | versioned tabular/tensor pipeline | production inference needs only a small exported model |

No row library removes the need to model domain meaning. A database `NULL`, an empty CSV cell, an absent JSON property, `NaN`, and “not yet measured” should not collapse into one accidental value.

## Inspect the reconstructable local CSV template {#representative-sample}

The reconstructed `DataSample.fsproj` should target `net10.0`, compile only `Program.fs`, and reference exactly [FSharp.Data 8.2.0](https://www.nuget.org/packages/FSharp.Data/8.2.0). Keep the generated package lock after the first restore. Its only compile-time data source is `sample.csv` beside `Program.fs`; compilation should not depend on a URL, database, account, or secret.

`Program.fs` starts with `namespace ThinkingInFSharp.Ecosystem.Data` and opens `System`, `System.Globalization`, and `FSharp.Data`. The two output records are namespace-level types; the provider type, `revenue`, and the two query functions belong to module `DataSample`; command-line parsing and printing belong to the final `Program` entry-point module. Every code block below is an excerpt from that one file and does not run independently.

Use these six synthetic rows for `sample.csv` so that the inference contract is visible:

```csv [sample.csv]
OrderId,Region,Product,Units,UnitPrice,OrderedAt
ORD-1001,North,Keyboard,2,75.50,2026-01-02
ORD-1002,South,Monitor,1,240.00,2026-01-03
ORD-1003,North,Mouse,4,25.00,2026-01-04
ORD-1004,West,Dock,3,110.00,2026-01-05
ORD-1005,South,Keyboard,2,80.00,2026-01-06
ORD-1006,West,Cable,5,12.00,2026-01-07
```

The official [CSV provider guide](https://fsprojects.github.io/FSharp.Data/library/CsvProvider.html) explains the two distinct moments: a sample supplies column names and inferred types when code is checked, while `Load` or `Parse` supplies runtime data. The data sample makes the design-time location independent of the build working directory:

```fsharp:line-numbers [Program.fs]
[<Literal>]
let private ResolutionFolder = __SOURCE_DIRECTORY__

type private Orders =
    CsvProvider<
        "sample.csv",
        ResolutionFolder=ResolutionFolder,
        Culture="en-US",
        PreferDateOnly=true
     >
```
The fixed sample makes `Units` an `int`, `UnitPrice` a `decimal`, and `OrderedAt` a `DateOnly`. `Culture="en-US"` and `PreferDateOnly=true` are part of the inference contract. Change those static parameters or the sample and the generated API may change at compile time.

This compile-time feedback establishes the shape witnessed by the fixed sample. Runtime validation must still cover file presence and access, size, encoding, syntax, and domain meaning for each later input.

### Keep generated rows inside the adapter {#generated-row-boundary}

The data sample does not return `Orders.Row`. It converts generated rows into ordinary records:

```fsharp:line-numbers [Program.fs]
type RegionSummary =
    { Region: string
      OrderCount: int
      Units: int
      Revenue: decimal }

type HighValueOrder =
    { OrderId: string
      Region: string
      OrderedAt: DateOnly
      Revenue: decimal }
```
This boundary has three benefits:

- callers do not inherit the provider package or a sample-derived public API;
- domain names and required fields can differ from source column names;
- a later move to a database, another parser, or an explicit schema changes the adapter rather than every consumer.

The records here are analytical outputs, not booking-domain entities. A real ingestion boundary should additionally validate identifiers, ranges, currencies, time zones, duplicates, and business meaning before constructing trusted domain values.

### Use ordinary sequence composition when it is enough {#sequence-aggregation}

The regional summary groups the generated rows, materializes each group once, calculates totals, sorts, and returns a list:

```fsharp:line-numbers [Program.fs]
let private revenue (row: Orders.Row) = decimal row.Units * row.UnitPrice

let summarizeByRegion (path: string) : RegionSummary list =
    Orders.Load(path).Rows
    |> Seq.groupBy _.Region
    |> Seq.map (fun (region, rows) ->
        let rows = Seq.toArray rows

        ({ Region = region
           OrderCount = rows.Length
           Units = rows |> Array.sumBy _.Units
           Revenue = rows |> Array.sumBy revenue }
        : RegionSummary))
    |> Seq.sortByDescending _.Revenue
    |> Seq.toList
```
The data set has six rows, so an in-memory sequence is the honest choice. The pipeline is readable because the evaluation budget is known. Applying the same code blindly to a 60 GB file would change the operational contract: `CsvProvider` caches rows by default, and even with caching disabled, `groupBy`, sorting, and final materialization can retain substantial data.

### Read query expressions by their source {#query-expression-sample}

The second function uses F# query syntax:

```fsharp:line-numbers [Program.fs]
let highValueOrders (minimumRevenue: decimal) (path: string) : HighValueOrder list =
    query {
        for row in Orders.Load(path).Rows do
            let rowRevenue = revenue row
            where (rowRevenue >= minimumRevenue)
            sortByDescending rowRevenue

            select (
                { OrderId = row.OrderId
                  Region = row.Region
                  OrderedAt = row.OrderedAt
                  Revenue = rowRevenue }
                : HighValueOrder
            )
    }
    |> Seq.toList
```
Here `Orders.Load(path).Rows` is an in-process sequence, so filtering, sorting, and projection execute locally. The syntax resembles a database query, but no SQL exists.

After reconstruction, focused tests should assert the six-row aggregation, `DateOnly` values, threshold, and descending order; also run a locked restore followed by Release `--no-restore` build and test. The console entry point should resolve the input path to an absolute path and print three deterministic summaries for this sample. Even when these checks pass, they cover only the fixed local schema and calculations, not arbitrary uploads, huge files, encoding attacks, or a database provider.

## Treat schema as a versioned dependency {#schema-dependency}

A type provider moves some feedback earlier by generating an API from information available during checking. Decide where that information comes from.

### Stable local samples {#stable-samples}

A committed sample is appropriate when it is:

- small, synthetic or safely de-identified, and legally reviewable;
- representative of headers, separators, culture, optionality, and edge values;
- versioned with the code and available to every clean build;
- paired with runtime tests for compatible and incompatible inputs;
- changed deliberately when the accepted schema changes.

Do not commit production exports merely to make IntelliSense work. Samples, generated outputs, build caches, and test failure messages all become part of the information boundary.

### Live design-time sources {#live-design-time-sources}

A URL or database inspected during compilation couples a build to availability, credentials, network policy, server version, and mutable external state. A successful build in one developer session may then fail in CI or silently expose a different generated surface tomorrow.

If a database type provider is valuable, prefer its supported offline schema mechanism when practical. Otherwise use a schema-only account with least privilege, keep secrets outside static parameters and source, pin the driver/provider graph, and make the required design-time service an explicit build dependency. Cache invalidation is not schema governance.

### Dynamic or partner-controlled data {#dynamic-data}

For genuinely open data, a representative sample can still improve ergonomics, but place an explicit decoding layer after parsing:

```text
bytes -> syntax parser -> source-format values -> validation -> trusted model
```

Decide whether a new field is ignored or rejected, whether a missing field is optional or an error, how numeric widening works, and how versions coexist. Keep raw input or a safe digest for diagnosis where policy permits. Type inference cannot decide those compatibility rules for you.

## Know where a query executes {#query-execution}

The [F# query expression reference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/query-expressions) supports LINQ-style queries, but the source controls meaning.

| Source | Typical execution | Important consequence |
|---|---|---|
| `list`, array, `seq`, CSV rows | local .NET enumeration | F# functions work, but CPU and data movement are yours |
| `IQueryable<'T>` from EF or another provider | expression tree translated by the provider | only supported expressions translate; SQL/provider semantics apply |
| materialized result followed by `Seq` | remote first, then local | the exact materialization point sets the data-transfer boundary |
| asynchronous stream | incremental local effects | cancellation, disposal, backpressure, and partial failure matter |

The same-looking `where` can therefore mean a local predicate or a provider expression. Unsupported translation can throw; provider versions may translate differently; database null, collation, decimal, date, and ordering semantics may differ from .NET.

Make the execution boundary visible in names and tests. Inspect generated SQL or provider diagnostics for important queries. Project only required columns, limit result size, pass cancellation, and test against the real database engine when dialect or transaction behavior matters. An in-memory provider does not validate production SQL.

Avoid hiding a remote query behind an innocent `seq<'T>` if enumeration performs I/O. A port such as `search : Query -> CancellationToken -> Task<SearchResult list>` states the effect, materialization, and cancellation boundary more honestly.

## Choose relational access by control and schema responsibility {#relational-access}

F# can consume the full .NET database ecosystem. The useful distinction is not “F# library versus C# library,” but how much SQL, mapping, tracking, schema discovery, and lifecycle work each choice handles.

### Direct ADO.NET provider {#ado-net}

Use a vendor `DbConnection`, parameterized `DbCommand`, and reader when exact SQL, streaming, batching, provider features, or dependency minimization matter. You own mapping, null handling, transactions, disposal, retries, and diagnostics. This is often a good narrow adapter, not a virtue contest about writing plumbing.

Never construct SQL by interpolating untrusted values. Parameters protect values; dynamic identifiers and query structure require allowlists or a query builder. Open connections as late as practical, dispose readers and commands deterministically, and pass cancellation to asynchronous operations.

### Dapper {#dapper}

[Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79) is a SQL-first object mapper over ADO.NET. It can reduce repetitive parameter and row mapping while keeping query text visible and transaction control explicit.

It fits read models and services whose team wants explicit SQL. F# friction appears around constructors, anonymous parameters, options, records, and nulls; contain it with CLR-friendly persistence DTOs and small mapping functions. Dapper does not design aggregates, migrations, concurrency rules, or safe SQL composition for you.

### Entity Framework Core {#ef-core}

EF Core provides a broader model for units of work, change tracking, relationships, LINQ translation, migrations, and providers. [EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) is the .NET 10 LTS line and requires the .NET 10 runtime.

Choose it when that application model and its provider ecosystem repay the conventions. F# can call it, but mutable entity types, navigation properties, expression translation, attributes, proxy assumptions, and C#-first examples may make persistent entities poor domain types. Keep EF entities in infrastructure and map them to immutable records or unions rather than weakening the domain for tracking convenience.

### SQLProvider {#sqlprovider}

[SQLProvider 1.5.27](https://www.nuget.org/packages/SQLProvider/1.5.27) is an F# database type provider with LINQ queries, schema exploration, CRUD support, multiple database backends, and an offline schema option. It can give excellent feedback when the database schema is stable and provider support matches the target.

An adoption spike must exercise the actual driver, design-time schema path, generated SQL, nullability, transactions, async/cancellation, migrations, publish output, and CI restore. Generated entities remain adapter types. A 47 MB package download and its driver/native dependency graph are operational facts to inspect, not a quality judgment.

### Compare what each option controls {#relational-comparison}

| Need | First candidate | Key verification |
|---|---|---|
| exact SQL and provider features | direct ADO.NET | mapping, lifetime, cancellation, transaction, and injection tests |
| explicit SQL with less mapping ceremony | Dapper | F# DTO mapping, parameterization, multi-mapping, and transaction behavior |
| change tracking and rich application model | EF Core | real-provider translation, concurrency, migration, and entity/domain separation |
| compile-time, schema-driven F# access | SQLProvider | repeatable schema acquisition, generated SQL, driver, and deployment compatibility |

One service can use a write-oriented adapter and separate read projections. Do not let two libraries coordinate the same transaction or migration path without a precise boundary.

## Keep transactions and domain decisions distinct {#transactions-domain}

Database libraries expose mechanics; the application defines business meaning. Put uniqueness, capacity, version checks, or ledger balance behind the atomic operation that actually protects them. Map unique violations and optimistic-concurrency conflicts to declared application results rather than leaking vendor exceptions.

Retries require idempotency and transaction knowledge. Retrying a read-only transient failure differs from retrying a command after the connection drops during commit. Measure pool saturation, query duration, returned rows, retries, and conflicts with bounded labels; do not log full SQL parameters or row contents by default.

For migrations, separate “application can read both shapes,” “schema changed,” “old writers stopped,” and “old column removed.” Type-provider recompilation and an ORM migration file do not prove a rolling deployment is compatible.

## Choose analytical structures by operations and scale {#analytical-structures}

Start with ordinary F# collections when rows are bounded and the operations are type-stable. Arrays offer compact indexed storage; sequences support deferred pipelines; lists are excellent for recursive construction but are rarely the best dense numerical table.

[Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0) targets `net10.0` and provides labeled series and frames, alignment, joins, missing-data operations, grouping, statistics, and time-series functions. Choose it when those semantics reduce real work, not because every CSV deserves a data frame.

Data frames trade some static information about row types for heterogeneous columns and interactive manipulation. Specify keys, duplicate handling, alignment, missing values, ordering, and conversions explicitly. A frame that prints attractively can still contain an `object` column, silent coercion, or a misaligned time series.

When data no longer fits the intended memory budget, push projection and aggregation into the database or analytical engine, partition the workload, or use a suitable columnar format. Benchmark parsing, allocation, joins, and serialization with representative cardinality. “Lazy” does not mean constant memory after grouping, sorting, caching, or materialization.

## Make exploration reproducible {#reproducible-exploration}

The SDK includes [F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/): `dotnet fsi` runs a REPL or a versioned `.fsx` script. It is a good scratch surface because the language and runtime are the same, but an interactive session still carries hidden state.

For a durable result, record:

- exact SDK and package versions plus lock information;
- immutable input identity, schema version, and permitted acquisition steps;
- culture, time zone, text encoding, and missing-value policy;
- random seeds and deterministic limitations;
- cell or step execution order;
- expected tables, metrics, or artifact hashes;
- a headless command that reproduces the result from a clean process.

Move reusable parsing, features, and calculations into compiled modules with tests. Leave only orchestration and presentation in the script. If an analysis changes a product decision, review it like application code.

Older material commonly recommends Polyglot Notebooks or .NET Interactive. Microsoft [deprecated Polyglot Notebooks on 2026-03-27 and .NET Interactive on 2026-04-24](https://github.com/dotnet/interactive/issues/4163); the repository was archived. Existing installations may continue temporarily, but receive no new features or post-deprecation security fixes. Do not choose them as a new default. Evaluate a currently maintained Jupyter kernel separately if notebook presentation is essential.

## Treat visualization as an output boundary {#visualization}

[Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0) is the checked stable package; 6.0 packages are prereleases on the checked date. It builds plotly.js chart descriptions from .NET and has related interactive and image-export packages.

A chart choice includes more than its constructor:

- interactive HTML versus deterministic static image or vector export;
- browser/CDN policy, offline assets, content security policy, and bundle size;
- colors, labels, units, time zones, aggregation, and uncertainty;
- accessible title, text summary, table alternative, contrast, and keyboard behavior;
- whether tooltips, embedded data, filenames, or exported metadata reveal sensitive rows;
- server, desktop, notebook, or browser rendering and its native/runtime dependencies.

Test the table that feeds the chart before testing pixels. Keep the transformation that selects bins, denominators, smoothing, and missing values separate from rendering. A visually plausible graph can encode the wrong population with perfect typography.

## Choose a machine-learning boundary, not merely a trainer {#machine-learning}

First decide the task and product decision. Classification, regression, ranking, recommendation, anomaly detection, forecasting, embedding search, and generation have different labels, metrics, latency, and failure costs. A rules baseline may be cheaper, more explainable, and more accurate for the available data.

An honest training workflow includes:

```text
versioned observations
  -> leakage-safe split
  -> fitted preprocessing and features
  -> baseline and candidate training
  -> task-appropriate evaluation
  -> immutable model plus schema/metadata
  -> deployment validation
  -> monitored inference and retraining decision
```

Split before fitting transformations that learn from data. Preserve entity or time boundaries when random row splitting would leak the future or the same customer across sets. Report a metric tied to product cost, uncertainty, and a baseline; “accuracy improved” is incomplete without the data slice and threshold.

### ML.NET {#mlnet}

[ML.NET](https://learn.microsoft.com/en-us/dotnet/machine-learning/mldotnet-api) provides .NET data views, transforms, trainers, evaluation, model persistence, and import paths including ONNX and TensorFlow. [Microsoft.ML 5.0.0](https://www.nuget.org/packages/Microsoft.ML/5.0.0) is the checked stable package; 6.0 is prerelease on the checked date.

It is a strong candidate for classical ML that must train or infer inside a .NET estate. F# can consume the CLR API, though its attributes, mutable input/output classes, generic overloads, and examples are designed around C#. Keep those types in an ML adapter and map from validated F# feature records. Test the persisted model by loading it in a fresh process, not only by predicting with the in-memory object that trained it.

### ONNX Runtime {#onnx-runtime}

[ONNX Runtime's .NET binding](https://onnxruntime.ai/docs/get-started/with-csharp.html) is a candidate when training occurs elsewhere and the product needs portable inference. The boundary is the model's named tensors, dtypes, dimensions, preprocessing, output interpretation, and execution provider—not a single `Run` call.

Pin the model digest and runtime/native package, validate tensor shape before inference, reuse sessions and buffers according to documented ownership, bound concurrency, and test on every target architecture. An ONNX file is executable supply-chain input and should be reviewed and distributed like code.

### TorchSharp or an external training stack {#torchsharp-external}

[TorchSharp 0.107.0](https://www.nuget.org/packages/TorchSharp/0.107.0) exposes the LibTorch engine to .NET. It is useful when tensor and deep-learning work must stay in .NET, but native CPU/GPU packages, architecture support, tensor disposal, device placement, reproducibility, and a pre-1.0 API lifecycle enter the operational contract.

Choose an external Python or managed training platform when its algorithms, hardware tooling, experiment tracking, or team expertise clearly dominate. Keep the boundary explicit through versioned files, ONNX, a batch job, queue, or service contract. Crossing a process boundary is often simpler than maintaining a private F# wrapper over an entire fast-moving ecosystem.

### Separate training from serving {#training-serving}

| Concern | Training | Online inference |
|---|---|---|
| primary goal | learn and compare | stable decision under a latency/error budget |
| data | versioned historical set with labels | one request or bounded batch with validated features |
| lifecycle | iterative, resource-heavy, often offline | long-lived model/session with controlled concurrency |
| evidence | split, baseline, metrics, seeds, provenance | startup load, schema compatibility, load, failure, rollback |
| monitoring | experiment and data quality | latency, errors, feature/model drift, decision outcomes |

Do not train during web startup or construct a prediction engine per request unless measurements and ownership explicitly justify it. Warm the model, define thread-safety, make version visible in diagnostics, and keep the previous immutable artifact for rollback.

## Keep the version table honest {#version-table}

These are dated observations, not a universal stack recommendation:

| Choice | Stable surface checked on 2026-08-31 | Status in this chapter | Key adoption question |
|---|---|---|---|
| FSharp.Data | 8.2.0; `net8.0` and `netstandard2.0` assets | in-page project template | is a fixed sample an honest schema witness? |
| Dapper | 2.1.79 | research only | does explicit SQL plus DTO mapping fit ownership? |
| EF Core | 10 LTS on .NET 10 | research only | do tracking, migrations, and LINQ translation repay entity friction? |
| SQLProvider | 1.5.27 | research only | can design-time schema and target driver be reproduced safely? |
| Deedle | 8.0.0; `net10.0` asset | research only | do labeled alignment and missing-data operations justify a frame? |
| Plotly.NET | 5.1.0 stable; 6.0 preview exists | research only | what rendering, accessibility, and data-export contract is required? |
| Microsoft.ML | 5.0.0 stable; 6.0 preview exists | research only | is in-process classical .NET ML the right product boundary? |
| TorchSharp | 0.107.0 plus a native LibTorch package | research only | can the team own native deployment and pre-1.0 evolution? |

“In-page project template” means the chapter supplies the files and context needed to reconstruct a small example; it does not claim that the project currently exists in this repository. “Research only” means the adopting application must restore, compile, execute, benchmark, and security-review the relevant option. Computed target compatibility is not the same as an included target asset, and a stable package label is not proof of product suitability.

## Run a bounded data-stack spike {#adoption-spike}

Use the same acceptance data to verify one representative path:

- a realistic schema covering null, Unicode, time, decimal, and boundary values;
- a bounded projected, filtered, ordered success query plus malformed input, cancellation, dependency/transient failure, and any write conflict or transaction boundary;
- generated SQL, allocation, row count, and latency at representative scale;
- locked restore through target-platform execution, including review of secrets, logs, temporary files, visualization output, and model artifacts;
- one upgrade or schema evolution and a deletion condition.

Compare correctness and ownership before syntax. A ten-line demo that assumes a live developer database, downloads a schema, loads all rows, or hides a native runtime is not smaller; it has merely moved work outside the file.

## Exercises {#exercises}

### Exercise 1: choose three data boundaries {#exercise-01}

Evaluate these data workloads separately:

1. An F# booking service needs transactional writes, optimistic concurrency, and five carefully tuned PostgreSQL queries.
2. An analyst joins three bounded CSV extracts by date, explores missing values, and publishes an accessible chart each week.
3. A web API receives a versioned feature DTO and must run a model trained by a Python team within 30 ms in the same process.

For each workload, choose and justify a first candidate, compare at least two options, and list the evidence that would reverse the choice.


::: details Answer

#### Case A: transactional PostgreSQL booking data {#exercise-01-case-a}

Start with the vendor ADO.NET provider plus [Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79) inside one infrastructure adapter. Keep the five tuned SQL statements explicit and map persistence DTOs to the existing booking domain.

The application port should describe the persistence operation, not query mechanics:

```fsharp
type BookingStore =
    { TryAppend:
        expectedVersion: int64 ->
        events: BookingEvent list ->
        CancellationToken ->
        Task<Result<int64, AppendError>> }
```

One transaction loads or checks the current version and performs a conditional write such as `WHERE version = @expectedVersion`. Zero affected rows becomes `VersionConflict`; unique violations become a declared duplicate result; ambiguous commit failure is not blindly retried. The exact SQL differs by schema and driver, so test it on the same PostgreSQL major version used in production.

Direct ADO.NET is the countercandidate. It may win if streaming, batching, provider-specific types, or mapping control makes Dapper's helpers marginal. EF Core 10 is another candidate only if change tracking, migrations, or its application model provides demonstrated value; five tuned queries and explicit event/version semantics do not by themselves justify tracked entities.

The spike must verify:

- every value is parameterized and any dynamic identifier comes from an allowlist;
- transaction scope and disposal responsibility are visible, and connections and readers are disposed;
- cancellation reaches open, execute, and read operations;
- the five queries return bounded projections and use expected indexes;
- two controlled writers with one expected version yield one success and one conflict;
- a connection loss around commit produces a defined reconciliation or idempotency stop;
- SQL diagnostics expose duration and row count without logging sensitive parameters;
- locked restore, migration, publish, startup, and rollback work in the target environment.

Choose direct ADO.NET if Dapper complicates record or null mapping, or adds no recurring value. Choose EF Core if a representative write aggregate and read query show that its unit of work and migrations reduce total implementation and maintenance cost. Its entities must not leak into the domain.

#### Case B: weekly CSV analysis and accessible chart {#exercise-01-case-b}

Start with FSharp.Data for bounded ingestion, [Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0) for date-keyed alignment and missing-data work, and [Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0) only at the rendering boundary.

First compare Deedle with records plus `Map<DateOnly, _>`. If three small files have one unique date key and two joins, typed maps may be clearer and make every row field visible in the type. Deedle wins provisionally when repeated outer joins, alignment, resampling, and missing-value policies dominate the analysis.

The weekly job should:

- pin three synthetic schema samples and validate each runtime file separately;
- record input digests, acquisition time, culture, encoding, and expected date range;
- reject duplicate keys or resolve them by one documented rule;
- distinguish missing observations from zero and from invalid cells;
- compute the chart table in a compiled, tested function;
- emit the table as CSV plus a text summary alongside the chart;
- use explicit units, time zone, denominator, colors, and accessible labels;
- bundle or approve browser assets and scan HTML/tooltips for sensitive data;
- run headlessly from a clean process and compare invariant summary values.

Plotly.NET loses if its export path cannot reproduce the required static format reliably or browser policy rejects its assets. It also loses if a simpler reporting tool already handles accessibility and distribution. The analytical calculation remains because it does not return chart objects.

#### Case C: Python-trained model with local 30 ms inference {#exercise-01-case-c}

Start with an ONNX export and the [ONNX Runtime .NET binding](https://onnxruntime.ai/docs/get-started/with-csharp.html). It preserves the Python team's training ecosystem while keeping inference in process and avoiding a network hop.

Define a versioned `FeatureDto` at HTTP input, validate it, then map it to a neutral `FeatureVector`. One inference adapter contains tensor names, order, data types, dimensions, normalization, session management, execution-provider selection, and output-to-decision mapping. Do not let tensor objects or model-specific column names enter the domain.

Acceptance criteria include:

- the Python and .NET paths run a golden vector set and agree within a declared tolerance;
- model digest, opset, feature schema, preprocessing version, labels, threshold, and training-data identity travel together;
- a fresh process loads and warms the immutable model before readiness succeeds;
- invalid shapes, `NaN`, infinities, missing features, and unknown schema versions fail before inference;
- a bounded concurrent load test meets the 30 ms percentile budget on every target architecture;
- native runtime packages are locked, published, vulnerability-reviewed, and exercised in the deployment image;
- diagnostics report model version, latency, and bounded outcome—not raw features;
- the previous artifact remains deployable and rollback does not require a schema downgrade.

ML.NET is the alternative because it can import ONNX and add .NET transforms. It wins only if those transforms reduce the preprocessing the team must maintain while golden vectors remain identical. A local Python sidecar loses the initial comparison because it reintroduces process startup, packaging, serialization, and health checks without a demonstrated requirement.

:::

### Exercise 2: design for CSV schema drift {#exercise-02}

The supplier changes `UnitPrice` to `Price`, adds `Currency`, sometimes sends a blank `OrderedAt`, and may append unknown columns. Design a v1/v2 ingestion boundary around the data sample. Specify compile-time samples, syntax parsing, source DTOs, compatibility rules, domain validation, quarantine evidence, fixture cases, and the condition for deleting v1 support. Do not solve currency by treating all decimals as interchangeable.


::: details Answer

#### Make version recognition explicit {#exercise-02-recognition}

Do not replace the data sample and hope every producer changed atomically. Define two source contracts and select one from an envelope version, manifest, filename convention controlled by the receiver, or a bounded header inspection. Header guessing is a fallback only when the distinguishing sets cannot overlap.

| Source contract | Required columns | Optional/extension rule |
|---|---|---|
| v1 | `OrderId,Region,Product,Units,UnitPrice,OrderedAt` | unknown columns follow an explicit ignore-or-reject policy |
| v2 | `OrderId,Region,Product,Units,Price,Currency` plus `OrderedAt` | blank `OrderedAt` is syntactically allowed; unknown columns use the same stated policy |

Keep one small synthetic compile-time sample per accepted version. A v2 sample must include a blank date and enough representative values to infer the intended optional numeric and date types. Generated provider types remain private inside `V1Adapter` and `V2Adapter`.

If upstream drift is frequent or headers are genuinely open, replace sample-derived runtime decoding with an explicit CSV schema/parser while retaining samples as fixtures. A type provider is optional ergonomics, not the compatibility authority.

#### Normalize through source DTOs {#exercise-02-normalization}

Use this boundary:

```text
bounded UTF-8 CSV
  -> recognized v1 or v2 source rows
  -> syntax and column diagnostics
  -> version-specific normalization
  -> domain validation
  -> accepted rows or quarantine records
```

`Price` becomes a money candidate only after `Currency` is parsed against an allowed ISO currency set. For v1, use a versioned configured currency such as USD only if the supplier contract guarantees it; otherwise v1 cannot construct money safely and must be rejected or routed for enrichment. Never add decimals from different currencies.

A blank v2 `OrderedAt` maps to `None` in the source DTO. The domain then decides whether absence is permitted, substituted from trusted envelope time, or rejected. An invalid nonblank date is not `None`; it is malformed input.

Normalize `OrderId`, region, units, and product through the same constructors used by other adapters. Detect duplicate order IDs within the file before any side effects. Attach the row number, source version, safe error code, and input digest to the quarantine record. Exclude complete sensitive rows from application logs.

Unknown columns should be either rejected for a closed contract or ignored while recorded for an extension-tolerant contract. Apply the same policy in both versions. Silently switching based on provider behavior makes compatibility accidental.

#### Build compatibility fixtures {#exercise-02-evidence}

Fixtures should cover:

- the exact current v1 and v2 samples;
- v1 `UnitPrice` and v2 `Price` mapping to the same money value under an explicit currency guarantee;
- missing, blank, malformed, duplicate, and differently cased headers;
- blank versus invalid `OrderedAt`;
- supported, missing, unknown, and incorrectly cased currency codes;
- zero, negative, overflow, and high-bound units/prices;
- quoted commas, quotes, CRLF/LF, Unicode, BOM, invalid UTF-8, and size/row limits;
- allowed and forbidden unknown columns;
- a mixed or ambiguous file that must not partially commit;
- cancellation before commit and deterministic retry after quarantine.

Compile both adapters in clean locked builds without network access. Run their normalized accepted rows through the same downstream contract tests. Observe accepted/rejected counts by source version with bounded labels.

Delete v1 only after every condition below is true:

- the supplier contract and migration window have ended;
- production has received no v1 input during the agreed retention interval;
- retained replays no longer require v1;
- all callers and fixtures have moved; and
- rollback cannot restore a producer that emits v1.

Keep the migration decision and a safe schema example even after deleting executable support.

:::

### Exercise 3: productionize an exploratory model {#exercise-03}

An `.fsx` script loads a local export, engineers features, trains a classifier, prints accuracy, and draws a chart. Design the path to a repeatable training job and a separately deployable inference component. Include immutable data identity, leakage-safe splitting, baseline and slice metrics, package/model locks, feature schema, secret handling, headless tests, model registry metadata, startup/load evidence, monitoring, and rollback.


::: details Answer

#### Freeze the question and data lineage {#exercise-03-lineage}

Write the product decision, prediction horizon, label definition, unit of observation, excluded uses, and cost of false positives/negatives before selecting an algorithm. Create a read-only data manifest containing source versions, extraction query or job revision, time range, row count, schema digest, content/object version, and access policy.

Never copy a live production export into source control. Store it in an approved immutable location; keep a tiny synthetic fixture in the repository. The training job accepts a manifest identifier, not “latest.csv.”

Split by the causal unit. For repeated customers, devices, or events, keep an entity on one side. For forecasting, train on the past and validate on a later interval. Fit imputers, encoders, scaling, feature selection, and vocabulary only on training data, then apply the fitted transform to validation and test sets.

#### Extract a tested feature pipeline {#exercise-03-features}

Move parsing, validation, feature derivation, and metric calculations from the `.fsx` file into a compiled project. Define plain records for validated observations and a versioned feature schema. Unit-test boundary values and add golden-vector tests shared with inference.

Keep the script only as a thin caller while migrating; run it with `dotnet fsi` from a clean process if retained. Do not build the new workflow on .NET Interactive or Polyglot Notebooks: both are deprecated and the project is archived. If a maintained notebook frontend remains useful for presentation, it calls the same compiled pipeline and is not the sole executable record.

#### Train, evaluate, and package immutably {#exercise-03-training}

The headless training command locks SDK/packages and records code revision, data manifest, split definition, seed, deterministic limitations, feature version, trainer/hyperparameters, runtime/architecture, and elapsed resources. It trains a simple baseline and the candidate.

Report task-appropriate metrics with confidence or repeated-run variation, decision threshold, calibration where relevant, and slices for important cohorts/time ranges. Compare against the baseline and product cost. Refuse promotion when a required slice regresses even if the aggregate improves.

The promoted artifact contains:

- immutable model bytes and cryptographic digest;
- model format/opset and required runtime/native package versions;
- ordered feature/output schema, dtype, dimensions, labels, and threshold;
- preprocessing artifact or exact feature-code version;
- training data manifest, code revision, metrics, slices, and approval;
- license, security, intended-use, limitation, and expiry/review metadata.

Choose an ML.NET model when training and serving intentionally share its pipeline; choose ONNX when portability from the Python trainer is the stable seam. Conversion is tested with golden vectors and representative batches, not accepted because export succeeded.

#### Give the inference component clear responsibilities {#exercise-03-inference}

At startup, load the exact configured model, verify digest and schema, create the long-lived session/engine, warm it, and then report readiness. The request path validates the feature DTO, applies the matching preprocessing version, invokes a bounded concurrency pool, maps outputs to a declared decision, and never logs raw sensitive features.

Contract tests cover schema versions, golden predictions, thresholds, invalid floating-point values, timeout and cancellation, corrupt or missing models, native-library failure, and safe errors. Load tests run in the deployment image on every CPU or GPU architecture. A real-process smoke test verifies the published layout and startup lifecycle.

Monitor input validity, missingness, feature ranges, prediction distribution, latency, failures, model version, and delayed decision outcomes with privacy-preserving bounded dimensions. Drift is an investigation signal, not an automatic retrain command.

Deploy a new immutable artifact through shadow or canary comparison where policy allows. Roll back by selecting the previous model/service artifact; keep input compatibility across the rollback window. Retraining requires the same evaluation and approval gate, not merely a newer timestamp.

The original chart becomes a reproducible review output with its source table and text summary. It is never the sole basis for promotion.

:::


Chapter 41 moves the same discipline into the browser: Fable compiles F# to JavaScript, where the runtime, API surface, package graph, and state model differ from server-side .NET.
