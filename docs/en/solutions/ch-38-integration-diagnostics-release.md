---
title: "Chapter 38 Solutions"
description: "Audit inflated guarantees, design bounded telemetry collection, and turn the local booking check into a concrete release plan."
translationKey: solutions/ch-38-integration-diagnostics-release
---

# Chapter 38 Solutions {#overview}

These solutions choose one defensible design rather than presenting a universal production recipe. Storage, provider, collector, runtime, and deployment contracts must be checked against the actual products and environment.

[Return to Chapter 38](../part-06/ch-38-integration-diagnostics-release).

## Exercise 1: audit three inflated claims {#exercise-01}

### Separate the evidence from the missing boundary {#exercise-01-ledger}

Start with a four-column ledger:

| Inflated claim | Strongest current evidence | Missing mechanism or boundary | Next decisive test |
|---|---|---|---|
| safe across three replicas | concurrent commands through several service objects sharing one process and normalized file path do not oversell | one transactional/conditional store shared by independent processes; aggregate version by event | start three hosts against the real store, force them to read one version, release competing conditional writes, then verify committed occupancy |
| payment and notification exactly once | exact completed retries do not repeat controlled stub calls; ambiguous payment is not blindly retried; pending notification intent survives orderly restart | provider idempotency and lookup; transactional outbox; at-least-once relay; consumer atomic deduplication; reconciliation | kill at every provider/outbox acknowledgment boundary and compare provider records, outbox rows, publishes, and consumer state |
| production ready because tests pass | locked Release build, focused tests, TestServer HTTP integration, real local Kestrel and C# smoke, safe sample logs | security, real dependencies, publish artifact, migration, topology, load envelope, telemetry backend, SLO/RPO/RTO, rollout and recovery | deploy the immutable candidate to a production-like environment and exercise security, migration, load, dependency failure, restore, rollout, and rollback gates |

The first current claim is still useful: **within one process and one normalized snapshot path, cooperating service instances serialize the aggregate capacity decision**. The missing evidence begins exactly at the OS-process boundary.

The second current claim should split in two: **an exact completed operation replays its local result without repeating the modeled stub calls**, and **an ambiguous payment stops for reconciliation rather than charging again**. Neither sentence says what a real provider or notification consumer did.

The third defensible claim, once an application-specific gate has actually passed, is: **the application has a reproducible local acceptance check for its documented topology**. “Production ready” is not a single testable property until a production contract names environment, traffic, security, dependencies, availability, durability, and ownership.

### Rewrite the release note {#exercise-01-rewrite}

A defensible note, after the described acceptance path has run successfully, could read:

> The booking capstone now passes its locked local acceptance gate. The verified topology is one API process using one local snapshot path and controlled payment/notification adapters. Exact completed retries do not repeat those modeled effects, changed payloads conflict, ambiguous payment stops for reconciliation, and a C# HTTP consumer completes the public workflow. Multi-process storage, real provider delivery, security controls, telemetry export, artifact deployment, and production recovery remain outside this release.

That statement preserves accomplishments while making the next engineering work obvious. It is more useful than either the inflated claim or a vague “nothing is guaranteed.”

## Exercise 2: design collection without cardinality debt {#exercise-02}

### Define the signal contract before the collector {#exercise-02-contract}

Subscribe to the supported ASP.NET Core server instrumentation plus these application sources:

| Signal | Source | Keep | Avoid |
|---|---|---|---|
| traces | built-in ASP.NET Core server source | method, matched route template, status; normal trace context | raw URL query, request body, authorization headers |
| traces | `ThinkingInFSharp.Booking.Api` | `booking.http.request` only if its outcome adds investigation value | request ID as a searchable global attribute unless policy explicitly permits it |
| metrics | `ThinkingInFSharp.Booking.Api` | request counter and duration histogram with bounded `outcome` | correlation ID, concrete path, exception message, user or provider identifier |
| logs | application completion event 1000 | trace/correlation, method, route template, status, outcome, duration | bodies, confirmation code, transaction text, exception message, snapshot path |

Keep the existing custom child activity for the first deployment because `booking.outcome` provides a stable application classification independent of raw status. Set an explicit review date. If queries never use the child and the built-in server span plus structured log answer the same questions, remove it to reduce span volume.

Do not sample metrics. Aggregate every request measurement in process and let the metrics pipeline export temporally according to policy. For traces, start with parent-based probabilistic sampling, but retain or separately sample errors according to the collector's documented behavior. A head sampler cannot know a later outcome, so “keep every error” may require tail sampling or another error signal. State the latency, memory, and failure tradeoff rather than promising it for free.

Use a collector endpoint and credentials from deployment configuration. Enforce TLS and least privilege. Apply attribute allowlists or redaction in both the application and collector, because a collector rule is not a reason to emit known secrets. Bound queue memory, define export timeouts, and decide whether telemetry loss may ever affect request success; in most services it should not.

Microsoft's [.NET tracing guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) distinguishes instrument creation from collection. Its [metrics guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) recommends `IMeterFactory` in dependency-injection hosts, matching the tested design here.

### Test value joining and series growth {#exercise-02-tests}

Retain the current in-process listener test because it proves the producer independently of a vendor. Add a collector integration test that sends one successful and one invalid request with a controlled valid `traceparent`, then asserts:

- the response header and completion log use the same trace ID;
- the custom activity is a child of the server activity when sampled;
- the counter and histogram include `success` and `client_error` measurements;
- exported records contain no forbidden fields or fixture secrets;
- disabling the activity sampler does not change HTTP behavior.

For cardinality, send at least 10,000 requests with distinct booking IDs and concrete URL values. Query or inspect the test backend and assert a fixed upper bound on custom metric series—at most four `outcome` series per instrument with the current contract. Also cap distinct route values to the number of registered templates and verify that no correlation or booking ID appears as a metric attribute.

Run this as a load test, not just a source scan. A harmless-looking enrichment processor can add an identifier after application tests have passed. Observe collector memory, dropped data, export queue length, and backend ingestion while varying IDs.

## Exercise 3: turn the check into a real release plan {#exercise-03}

### Choose and name one artifact {#exercise-03-artifact}

Assume the target is a framework-dependent Linux container on `linux-x64`. The immutable release identity is an OCI image digest, not a mutable tag. A multi-stage build restores from the lock file, runs the complete acceptance suite, then runs `dotnet publish` for `Booking.Api` in Release and copies only publish output into a pinned, supported ASP.NET Core runtime image.

Record alongside the digest:

- source commit and clean-tree status;
- SDK, target framework, runtime-image digest, and lock-file hash;
- build provenance and software bill of materials;
- vulnerability and license scan results under an explicit policy;
- configuration schema version and database migration range;
- public DTO/API version and rollback compatibility window.

Framework-dependent means the runtime comes from the runtime image. Pinning that image improves reproducibility, but security patches require rebuilding and promoting a new image. Microsoft's [.NET publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) makes the runtime model a deliberate publish choice.

### Promote the same bytes through gates {#exercise-03-pipeline}

A concrete pipeline is:

1. **Source gate:** frozen JavaScript install, locked .NET restore, formatting/content checks, full build and tests.
2. **Publish gate:** publish once, build the image once, generate provenance/SBOM, scan, sign, and store by digest.
3. **Ephemeral gate:** start the exact digest with a temporary real database and controlled provider sandbox; run migration, HTTP/C# smoke, malformed input, diagnostics export, and shutdown tests.
4. **Staging gate:** restore anonymized representative data, run backward/forward migration checks, concurrency/load tests, provider reconciliation, outbox recovery, and authorization tests.
5. **Promotion:** attach approval to the digest; do not rebuild for production.
6. **Canary deployment:** route a small, bounded share of eligible traffic while watching error rate, latency, saturation, payment ambiguity, outbox age, and capacity conflicts.
7. **Expansion:** increase traffic in timed stages only while gates remain healthy.
8. **Completion:** retain the prior compatible digest and migration recovery material for the declared rollback window.

The service needs separate liveness and readiness semantics. Liveness should answer whether the process can make progress without depending on every remote system. Readiness should remove an instance when a dependency essential to serving requests is unavailable, while avoiding synchronized flapping. Neither endpoint should reveal credentials, paths, SQL, or provider messages.

Run the process as a non-root user on a read-only base filesystem where practical, inject secrets through the platform rather than the image, restrict outbound destinations, terminate TLS under a documented trust model, and enforce authentication, authorization, request limits, and rate limits. Scan results need an owner and exception expiry; a green scanner alone is not a security design.

### Make storage evolution and rollback compatible {#exercise-03-rollback}

Replace the file snapshot before claiming replica safety. Use an event-keyed transactional or version-conditional store, then design schema changes with an expand/migrate/contract sequence:

1. deploy code that reads old and new forms but writes a backward-compatible form;
2. apply an additive migration and verify it under concurrent load;
3. backfill with bounded, observable batches;
4. switch writes only after old readers are gone;
5. remove old fields in a later release after the rollback window.

An application rollback is safe only while the older binary can read the current schema and understand messages already emitted. Otherwise recovery is a forward fix or database restore, each with an explicit data-loss window. Test restore time and data correctness; do not infer RPO or RTO from the existence of backups.

Trigger automated rollout stop on breached error/latency/saturation limits, failed readiness, unexpected payment ambiguity, or outbox backlog growth. Roll back only when the compatibility contract allows it. Preserve correlated evidence and open an incident rather than deleting the failed environment before diagnosis.

Local gates can prove deterministic builds, unit and contract behavior, static security policy, and the local smoke. Publish-image execution, real database migrations, provider sandbox behavior, collector export, load, canary routing, backup restore, and rollback require production-like or production control planes.

## Solution review {#solution-review}

- Rewrite each broad claim with its topology, dependency, failure model, and observation.
- Current local consistency evidence stops at the process boundary.
- No-blind-retry is valuable but is not cross-system exactly once.
- Production readiness becomes testable only after the production contract is named.
- Subscribe to stable sources and allowlist attributes before choosing dashboards.
- Correlation IDs belong in traces and controlled logs, not metric dimensions.
- Trace sampling and metric aggregation solve different cost problems.
- Test cardinality with many distinct IDs and inspect the exported backend shape.
- Publish one immutable artifact and promote the same digest through environments.
- Framework-dependent and self-contained deployment have different runtime patch contracts.
- Schema compatibility determines whether binary rollback is safe.
- Security, telemetry, migration, load, canary, restore, and rollback gates require owners and production-like evidence.

## Sources {#sources}

- [Microsoft Learn: Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [Microsoft Learn: Add distributed tracing instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
- [Microsoft Learn: Creating metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [Microsoft Learn: .NET application publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
