---
title: "Chapter 40 Solutions"
description: "Choose bounded data tools, absorb CSV schema drift explicitly, and turn an exploratory classifier into reproducible training and inference systems."
translationKey: solutions/ch-40-data-analytics
kind: solution
part: 7
chapter: 40
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-data-csv-provider
  - foundation-example-tests
exerciseIds:
  - ch40-exercise-01
  - ch40-exercise-02
  - ch40-exercise-03
termIds: []
sources:
  - id: fsharp-data-csv-provider
    url: https://fsprojects.github.io/FSharp.Data/library/CsvProvider.html
    checked: "2026-08-25"
  - id: ef-core-10
    url: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew
    checked: "2026-08-25"
  - id: dapper-nuget
    url: https://www.nuget.org/packages/Dapper/2.1.79
    checked: "2026-08-25"
  - id: deedle-nuget
    url: https://www.nuget.org/packages/Deedle/8.0.0
    checked: "2026-08-25"
  - id: plotly-net-nuget
    url: https://www.nuget.org/packages/Plotly.NET/5.1.0
    checked: "2026-08-25"
  - id: mlnet-overview
    url: https://learn.microsoft.com/en-us/dotnet/machine-learning/mldotnet-api
    checked: "2026-08-25"
  - id: onnxruntime-csharp
    url: https://onnxruntime.ai/docs/get-started/with-csharp.html
    checked: "2026-08-25"
  - id: dotnet-interactive-deprecation
    url: https://github.com/dotnet/interactive/issues/4163
    checked: "2026-08-25"
---

# Chapter 40 Solutions {#overview}

These answers choose provisional boundaries, then name the evidence that can reverse them. Package syntax alone cannot settle transaction ownership, schema compatibility, analytical correctness, or model usefulness.

[Return to Chapter 40](../part-07/ch-40-data-analytics).

## Exercise 1: choose three data boundaries {#exercise-01}

### Case A: transactional PostgreSQL booking data {#exercise-01-case-a}

Start with the vendor ADO.NET provider plus [Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79) inside one infrastructure adapter. Keep the five tuned SQL statements explicit and map persistence DTOs to the existing booking domain.

The application port should describe effects, not query mechanics:

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

The spike must prove:

- every value is parameterized and any dynamic identifier comes from an allowlist;
- transaction ownership is visible and connections/readers are disposed;
- cancellation reaches open, execute, and read operations;
- the five queries return bounded projections and use expected indexes;
- two controlled writers with one expected version yield one success and one conflict;
- a connection loss around commit produces a defined reconciliation or idempotency stop;
- SQL diagnostics expose duration and row count without logging sensitive parameters;
- locked restore, migration, publish, startup, and rollback work in the target environment.

Choose direct ADO.NET if Dapper complicates record/null mapping or adds no recurring value. Choose EF Core if a representative write aggregate and read query show that its unit of work and migration graph reduce total ownership without leaking entities into the domain.

### Case B: weekly CSV analysis and accessible chart {#exercise-01-case-b}

Start with FSharp.Data for bounded ingestion, [Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0) for date-keyed alignment and missing-data work, and [Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0) only at the rendering boundary.

First compare Deedle with ordinary records plus `Map<DateOnly, _>`. If three small files have one unique date key and two joins, typed maps may be clearer and give stronger row-shape visibility. Deedle wins provisionally when repeated outer joins, alignment, resampling, and missing-value policies dominate the analysis.

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

Plotly.NET loses if the delivery target requires a static format its export path cannot reproduce reliably, if browser policy rejects its assets, or if a simpler reporting tool already owns accessibility and distribution. The analytical calculation survives because it does not return chart objects.

### Case C: Python-trained model with local 30 ms inference {#exercise-01-case-c}

Start with an ONNX export and the [ONNX Runtime .NET binding](https://onnxruntime.ai/docs/get-started/with-csharp.html). It preserves the Python team's training ecosystem while keeping inference in process and avoiding a network hop.

Define a versioned `FeatureDto` at HTTP input, validate it, then map to a neutral `FeatureVector`. One inference adapter owns tensor names, order, dtype, dimensions, normalization, session lifetime, execution provider, and output-to-decision mapping. Do not let tensor objects or model-specific column names enter the domain.

Acceptance evidence includes:

- the Python and .NET paths run a golden vector set and agree within a declared tolerance;
- model digest, opset, feature schema, preprocessing version, labels, threshold, and training-data identity travel together;
- a fresh process loads and warms the immutable model before readiness succeeds;
- invalid shapes, `NaN`, infinities, missing features, and unknown schema versions fail before inference;
- a bounded concurrent load test meets the 30 ms percentile budget on every target architecture;
- native runtime packages are locked, published, vulnerability-reviewed, and exercised in the deployment image;
- diagnostics report model version, latency, and bounded outcome—not raw features;
- the previous artifact remains deployable and rollback does not require a schema downgrade.

ML.NET is the countercandidate because it can import ONNX and add .NET transforms. It wins only if those transforms reduce owned preprocessing while golden vectors remain identical. A local Python sidecar loses the initial comparison because it reintroduces process startup, packaging, serialization, and health boundaries without a demonstrated requirement.

## Exercise 2: design for CSV schema drift {#exercise-02}

### Make version recognition explicit {#exercise-02-recognition}

Do not replace the data sample and hope every producer changed atomically. Define two source contracts and select one from an envelope version, manifest, filename convention controlled by the receiver, or a bounded header inspection. Header guessing is a fallback only when the distinguishing sets cannot overlap.

| Source contract | Required columns | Optional/extension rule |
|---|---|---|
| v1 | `OrderId,Region,Product,Units,UnitPrice,OrderedAt` | unknown columns follow an explicit ignore-or-reject policy |
| v2 | `OrderId,Region,Product,Units,Price,Currency` plus `OrderedAt` | blank `OrderedAt` is syntactically allowed; unknown columns use the same stated policy |

Keep one small synthetic compile-time sample per accepted version. A v2 sample must include a blank date and enough representative values to infer the intended optional numeric/date shapes. Generated provider types remain private inside `V1Adapter` and `V2Adapter`.

If upstream drift is frequent or headers are genuinely open, replace sample-derived runtime decoding with an explicit CSV schema/parser while retaining samples as fixtures. A type provider is optional ergonomics, not the compatibility authority.

### Normalize through source DTOs {#exercise-02-normalization}

Use this boundary:

```text
bounded UTF-8 CSV
  -> recognized v1 or v2 source rows
  -> syntax and column diagnostics
  -> version-specific normalization
  -> domain validation
  -> accepted rows or quarantined evidence
```

`Price` becomes a money candidate only after `Currency` is parsed against an allowed ISO currency set. For v1, use a versioned configured currency such as USD only if the supplier contract guarantees it; otherwise v1 cannot construct money safely and must be rejected or routed for enrichment. Never add decimals from different currencies.

A blank v2 `OrderedAt` maps to `None` in the source DTO. The domain then decides whether absence is permitted, substituted from trusted envelope time, or rejected. An invalid nonblank date is not `None`; it is malformed input.

Normalize `OrderId`, region, units, and product through the same constructors used by other adapters. Detect duplicate order IDs within the file before effects. Attach row number, source version, safe error code, and input digest to quarantine evidence; exclude full sensitive rows from ordinary logs.

Unknown columns should be either rejected for a closed contract or ignored while recorded for an extension-tolerant contract. Apply the same policy in both versions. Silently switching based on provider behavior makes compatibility accidental.

### Build the evolution evidence {#exercise-02-evidence}

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

Delete v1 only after the supplier contract and migration window have ended, production observations show no v1 input for the agreed retention interval, retained replays no longer require v1, all callers and fixtures have moved, and rollback cannot restore a v1-emitting producer. Keep the migration decision and a safe schema example even after deleting executable support.

## Exercise 3: productionize an exploratory model {#exercise-03}

### Freeze the question and data lineage {#exercise-03-lineage}

Write the product decision, prediction horizon, label definition, unit of observation, excluded uses, and cost of false positives/negatives before selecting an algorithm. Create a read-only data manifest containing source versions, extraction query or job revision, time range, row count, schema digest, content/object version, and access policy.

Never copy a live production export into source control. Store it in an approved immutable location; keep a tiny synthetic fixture in the repository. The training job accepts a manifest identifier, not “latest.csv.”

Split by the causal unit. For repeated customers, devices, or events, keep an entity on one side. For forecasting, train on the past and validate on a later interval. Fit imputers, encoders, scaling, feature selection, and vocabulary only on training data, then apply the fitted transform to validation and test sets.

### Extract a tested feature pipeline {#exercise-03-features}

Move parsing, validation, feature derivation, and metric calculations from the `.fsx` file into a compiled project. Define plain records for validated observations and a versioned feature schema. Unit-test boundary values and add golden-vector tests shared with inference.

Keep the script only as a thin caller while migrating; run it with `dotnet fsi` from a clean process if retained. Do not build the new workflow on .NET Interactive or Polyglot Notebooks: both are deprecated and the project is archived. If a maintained notebook frontend remains useful for presentation, it calls the same compiled pipeline and is not the sole executable record.

### Train, evaluate, and package immutably {#exercise-03-training}

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

### Deploy inference as a separate owned component {#exercise-03-inference}

At startup, load the exact configured model, verify digest and schema, create the long-lived session/engine, warm it, and then report readiness. The request path validates the feature DTO, applies the matching preprocessing version, invokes a bounded concurrency pool, maps outputs to a declared decision, and never logs raw sensitive features.

Contract tests cover schema versions, golden predictions, thresholds, invalid floating-point values, timeout/cancellation, corrupt/missing models, native-library failure, and safe errors. Load tests run in the deployment image on every CPU/GPU architecture. A real-process smoke proves publish layout and startup ownership.

Monitor input validity, missingness, feature ranges, prediction distribution, latency, failures, model version, and delayed decision outcomes with privacy-preserving bounded dimensions. Drift is an investigation signal, not an automatic retrain command.

Deploy a new immutable artifact through shadow or canary comparison where policy allows. Roll back by selecting the previous model/service artifact; keep input compatibility across the rollback window. Retraining requires the same evaluation and approval gate, not merely a newer timestamp.

The original chart becomes a generated review artifact with its source table and textual summary. It is never the only evidence for promotion.

## Solution review {#solution-review}

- Explicit SQL and version checks fit the small transactional PostgreSQL surface provisionally.
- Deedle earns its place only when labeled alignment and missing-data work beat typed maps.
- Plotly.NET stays outside the analytical calculation and must satisfy accessibility and data policy.
- ONNX is a versioned tensor/preprocessing contract between Python training and .NET inference.
- Golden vectors test cross-runtime semantics; latency tests run on each real target architecture.
- CSV versions are recognized explicitly and normalize through private source adapters.
- Blank, invalid, unknown, and absent remain different states throughout ingestion.
- Currency needs a code and contract; a decimal alone is not money.
- V1 removal depends on producer, observation, replay, fixture, and rollback evidence.
- Training data, split, preprocessing, model, metrics, and approval form one lineage.
- Exploration moves into compiled, tested functions before it becomes production evidence.
- Serving owns model loading, concurrency, diagnostics, native dependencies, and rollback.
- Drift prompts investigation; it does not silently replace the deployed model.
- Every recommendation remains reversible when its stated evidence fails.

## Sources {#sources}

- [FSharp.Data: CSV Type Provider](https://fsprojects.github.io/FSharp.Data/library/CsvProvider.html)
- [Microsoft Learn: What's New in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [NuGet: Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79)
- [NuGet: Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0)
- [NuGet: Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0)
- [Microsoft Learn: How ML.NET Works](https://learn.microsoft.com/en-us/dotnet/machine-learning/mldotnet-api)
- [ONNX Runtime: C# API](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [Microsoft: Polyglot Notebooks and .NET Interactive deprecation](https://github.com/dotnet/interactive/issues/4163)
