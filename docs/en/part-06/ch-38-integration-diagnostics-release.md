---
title: "Chapter 38: Integration, Diagnostics, C# Client, and Release Verification"
description: "Close the booking-system loop with a real composition root, HTTP integration tests, a C# contract client, bounded diagnostics, and reproducible release evidence."
translationKey: part-06/ch-38-integration-diagnostics-release
---

# Chapter 38: Integration, Diagnostics, C# Client, and Release Verification {#overview}

The preceding chapters built the booking system from the inside out: a precise domain model, a pure decider, ports and adapters, an HTTP API, and finally a consistency protocol. No layer alone verifies that the executable connects them in the intended order. This chapter closes that gap.

The goal is to design one composition root, exercise the public contract from another .NET language, and observe results without exposing sensitive data. The chapter then plans the complete acceptance path as one reproducible command and states exactly what that command **cannot** prove.

This remains an in-page reference implementation: the current repository has no complete Booking solution, runnable API, C# client, or acceptance script described below. If implemented as projects, `Booking.Api` should reference the prior three layers and compile `Diagnostics.fs` → `Endpoints.fs` → `Program.fs`.

`Program.fs` depends on Chapter 36's startup configuration and endpoints plus Chapter 37's store and service. `Diagnostics.fs` additionally requires `System.Diagnostics`, `System.Diagnostics.Metrics`, and ASP.NET Core logging/dependency-injection types.

“Composition root,” “correlation ID,” “metric cardinality,” and “instrumentation” are architecture and observability terms, not F# syntax terms. F# supplies function records for boundary interfaces, pattern matching for outcome mapping, and `task` for composing the request pipeline.

## Verify composition at the executable {#composition-proof}

A composition root answers a concrete question: which implementations will the running process actually use? Beautiful domain functions and strong adapter tests are irrelevant if the executable wires an older workflow around them.

Chapter 37 deliberately left this gap visible. The earlier `BookingEndpoints.map` path accepted `AsyncPorts`, so it could not provide aggregate idempotency and capacity guarantees. The proposed final entry point constructs `AtomicBookingStore`, controlled payment and notification adapters, and `IdempotentBookingService`. It exposes only two operations to the HTTP layer.

```fsharp:line-numbers [Program.fs]
[<EntryPoint>]
let main arguments =
    match StartupConfiguration.load () with
    | Error error ->
        eprintfn "Booking API startup configuration is invalid (%s)." (errorCode error)
        2
    | Ok configuration ->
        let builder = WebApplication.CreateBuilder arguments

        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning) |> ignore
        BookingDiagnostics.add builder.Services

        builder.WebHost.ConfigureKestrel(
            Action<KestrelServerOptions>(fun options ->
                options.AddServerHeader <- false
                options.Limits.MaxRequestBodySize <- int64 BookingEndpoints.MaxRequestBodyBytes)
        )
        |> ignore

        let store = AtomicBookingStore configuration.Store
        use payment = new PaymentStub(PaymentStubBehavior.Authorize "TX-LOCAL-STUB")
        use notification = new NotificationStub(NotificationStubBehavior.Deliver)

        let service =
            IdempotentBookingService(configuration.Activity, store, payment.Invoke, notification.Invoke)

        use application = builder.Build()

        BookingDiagnostics.useMiddleware application

        BookingEndpoints.mapConsistent
            application
            { Execute = fun command token -> service.Execute(command, token)
              Load = fun requestId token -> service.Load(requestId, token) }

        application.Run()
        0
```
Read this code from the outside inward:

1. Startup configuration is parsed before a listener starts.
2. Kestrel receives a request-body limit and suppresses its identifying server header.
3. One store and one service own consistency and external-effect sequencing.
4. Diagnostics wraps the mapped endpoints.
5. `mapConsistent` receives functions, not permission to reach through the service into storage.
6. `application.Run()` is the final long-lived effect.

The local stubs are conspicuous. `PaymentStubBehavior.Authorize` does not become a real payment merely because it sits behind a function type. Composition makes the selected capability reviewable; it does not upgrade that capability.

### Preserve one HTTP policy surface {#http-policy-surface}

The final integration does not duplicate four endpoints. `map` and `mapConsistent` share body limits, strict deserialization, DTO mapping, validation, success serialization, route extraction, and the safe error boundary. Only command execution and loading differ.

```fsharp:line-numbers [Endpoints.fs]
let private mapHandlers (application: WebApplication) place confirm cancel load =
    ArgumentNullException.ThrowIfNull(application, nameof application)

    let protectedHandler handler =
        RequestDelegate(fun context -> safely handler context)

    application.MapPost("/api/bookings/place", protectedHandler place) |> ignore

    application.MapPost("/api/bookings/confirm", protectedHandler confirm) |> ignore

    application.MapPost("/api/bookings/cancel", protectedHandler cancel) |> ignore

    application.MapGet("/api/bookings/{requestId}", protectedHandler load) |> ignore

let map (application: WebApplication) (dependencies: BookingApiDependencies) =
    let execute = executeCommand dependencies

    mapHandlers
        application
        (handlePlaceWith execute)
        (handleConfirmWith execute)
        (handleCancelWith execute)
        (handleGet dependencies)

let mapConsistent (application: WebApplication) (dependencies: ConsistentBookingApiDependencies) =
    let execute = executeConsistent dependencies

    mapHandlers
        application
        (handlePlaceWith execute)
        (handleConfirmWith execute)
        (handleCancelWith execute)
        (handleConsistentGet dependencies)
```
`ConsistentBookingApiDependencies` is a narrow adapter-facing interface expressed as a record of functions. The endpoint layer knows that execution returns `Result<Booking, BookingConsistencyError>`; it does not know how the snapshot is locked or replaced. Exhaustive matching translates each declared error to a stable status and `ApiErrorDto` code.

That boundary also explains a useful testing seam. An HTTP contract test can provide controlled functions. The executable can provide the real local service. Neither path requires a service locator or mutable global dependency.

## Build a verification ladder {#evidence-ladder}

“The tests pass” is incomplete unless you can say which boundary each test crosses. An implemented project should use several deliberately overlapping levels:

| Test level | Real components crossed | Supported conclusion | Unsupported conclusion |
|---|---|---|---|
| pure example/property tests | domain values, decider, mappings | rules hold over examples and generated inputs | files, HTTP, and process startup work |
| adapter contract tests | strict JSON, snapshot files, configuration | local persistence and mapping obey their contracts | concurrent replicas are safe |
| consistency tests | aggregate store, service, controlled effects | modeled races, retries, and restart phases behave as specified | public HTTP maps every outcome correctly |
| in-process HTTP tests | ASP.NET Core pipeline, DTOs, final service, file adapter | status, body, headers, persistence, and effects compose | sockets, command-line startup, and another process work |
| separate-process smoke | real Kestrel socket and independent C# process | packaged source builds and the public workflow starts locally | a production topology, real provider, or failover works |

Microsoft's [ASP.NET Core integration-testing guidance](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) describes integration tests as broader tests that include the request pipeline and supporting infrastructure, while recommending that routine logic remain in faster unit tests. That is the reason for the ladder, not a reason to move every permutation through HTTP.

### Make HTTP effects observable in tests {#http-effects}

The end-to-end fixture should build a real `WebApplication`, select `TestServer`, register the same diagnostic middleware, map the same consistent endpoints, and use a temporary snapshot. Controlled payment and notification functions increment thread-safe counters.

The minimum focused integration tests should show that:

- normalized exact placement replays the same `201` body and does not repeat effects;
- changed seats under the same operation identity return `409 idempotency_conflict`;
- invalid JSON returns `400 invalid_json` before creating a snapshot or invoking an effect;
- an ambiguous payment returns `503` first, then `409 payment_outcome_unknown`, with one payment call;
- the diagnostic test aligns the response correlation ID with bounded metrics and one stopped child activity.

The first two facts should share one test because the effect counters are the causal observation. A response assertion alone would miss a duplicate payment hidden behind a replayed body.

`TestServer` sends HTTP abstractions in memory. That makes the pipeline fast and deterministic, but it intentionally avoids port allocation, TLS, and kernel networking. The release smoke should therefore add a second, smaller test across a real loopback socket.

### Prefer signals over delays {#causal-tests}

After implementation, concurrency tests elsewhere in the project should use barriers and task-completion signals to force both operations into the dangerous interval. Restart tests should launch a genuinely separate process against the persisted snapshot. That evidence is stronger than “run it many times and hope the scheduler is unlucky.”

Repetition still has a role: it can detect leaked shared state and nondeterministic cleanup. It is not a substitute for controlling the causal interleaving that defines the bug.

## Verify the public contract from C# {#csharp-contract}

F# and C# share the CLR, but they do not share identical ergonomics. A public F# API can compile while exposing curried functions, F#-specific unions, options, or generic shapes that are awkward to ordinary C# callers. Chapter 27 designed separate CLR-friendly DTOs; after implementation, an independent C# executable should consume them for real.

The proposed client references only `Booking.Contracts`, never `Booking.Domain` or `Booking.Infrastructure`. It communicates with the service exclusively through `HttpClient` and JSON.

The block below is the middle of a top-level C# console program, not a complete file. It assumes earlier imports for `System.Net`, `System.Net.Http.Json`, and the Contracts namespace. It also assumes existing `requestId`, `HttpClient client`, and `JsonSerializerOptions json` values plus `ReadBooking` and `Require` helpers.

```csharp:line-numbers [Program.cs]
var place = new PlaceBookingDto
{
    RequestId = requestId,
    Seats = 2
};

using var placedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var placed = await ReadBooking(placedResponse, json);
Require(placed.Status == HttpStatusCode.Created, "Place must return 201 Created.");
Require(placed.Booking.RequestId == requestId, "Place request ID round-trip.");
Require(placed.Booking.Seats == 2, "Place seat count round-trip.");
Require(placed.Booking.Status == "pending", "Placed booking must be pending.");

using var replayedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var replayed = await ReadBooking(replayedResponse, json);
Require(replayed.Status == HttpStatusCode.Created, "Exact replay must return the acknowledged status.");
Require(replayed.Body == placed.Body, "Exact replay must return the acknowledged booking.");

var confirm = new ConfirmBookingDto
{
    RequestId = requestId,
    ConfirmationCode = "CONF-CSHARP"
};

using var confirmedResponse = await client.PostAsJsonAsync("api/bookings/confirm", confirm, json);
var confirmed = await ReadBooking(confirmedResponse, json);
Require(confirmed.Status == HttpStatusCode.OK, "Confirm must return 200 OK.");
Require(confirmed.Booking.Status == "confirmed", "Confirmed booking status.");
Require(confirmed.Booking.ConfirmationCode == "CONF-CSHARP", "Confirmation code round-trip.");

var escapedRequestId = Uri.EscapeDataString(requestId);
using var loadedResponse = await client.GetAsync($"api/bookings/{escapedRequestId}");
var loaded = await ReadBooking(loadedResponse, json);
Require(loaded.Body == confirmed.Body, "GET must return the current confirmed booking.");
```
That flow should check four contract properties:

| Step | Contract check |
|---|---|
| place | object initializers can construct the DTO; JSON produces `201` and a pending booking |
| exact replay | application idempotency returns the same acknowledged status and body |
| confirm | another DTO crosses the same boundary and produces a representable confirmed response |
| GET | URL escaping and response DTO deserialization work without F# domain knowledge |

The client deliberately configures strict, case-sensitive deserialization and rejects unmapped properties. That tests compatibility with the chosen contract; other consumers need not copy the policy. Comparing raw successful bodies confirms deterministic output in this contract version. It does not imply that JSON texts with different property order are semantically unequal.

Passing this C# acceptance flow does not establish binary compatibility with every previous assembly version. That requires retained consumer fixtures or an API-compatibility tool against a declared baseline. This acceptance step proves only that the primary cross-language path works.

## Instrument the boundary, not the secret {#diagnostics}

When a request fails, an operator needs a few answers: which operation ran, when, for how long, with which outcome, and which trace links the signals? Logging the entire command is an easy way to turn diagnostics into a data leak.

The booking middleware records a completion event with stable field names:

```text
Booking request completed correlationId=<trace-id> method=<method> endpoint=<route-template> statusCode=<status> outcome=<outcome> elapsedMs=<duration>
```

It does not record request or response bodies, booking request IDs, confirmation codes, provider transaction text, exception messages, or the snapshot path. The HTTP response receives `X-Correlation-ID`. When an active `Activity` exists, the value is its 32-character W3C trace ID; otherwise the middleware creates a random trace ID of the same bounded form.

### Correlation joins signals; it does not prove identity {#correlation}

The same correlation value appears in the response header, structured completion event, logging scope, and custom activity tag. That lets a client report one value and lets an operator join several diagnostic signals.

An incoming valid trace context may influence the propagated trace ID. Therefore correlation is not authentication, authorization, request ownership, or a trusted business identifier. Its bounded hexadecimal form prevents arbitrary header text from entering logs, but access controls and retention still matter.

The log uses event ID `1000` and a precompiled `LoggerMessage` template. Stable names make queries durable and avoid converting structured fields into one opaque interpolated sentence. ASP.NET Core logging scopes can carry contextual values across nested log calls, and the platform can include active trace and span identifiers; see the official [logging documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0).

### Keep metric dimensions finite {#metric-cardinality}

The custom `Meter` exposes only:

| Instrument | Unit | Recorded dimension |
|---|---|---|
| `booking.http.requests` counter | `{request}` | `outcome` |
| `booking.http.duration` histogram | `ms` | `outcome` |

`outcome` has four controlled values: `success`, `client_error`, `server_error`, and `canceled`. Request IDs, paths containing IDs, correlation IDs, exception messages, and provider values are not metric dimensions. Otherwise each request could create a new time series and exhaust a monitoring backend's cardinality budget.

The middleware records the endpoint display name as a route template, such as `HTTP: GET /api/bookings/{requestId}`, rather than the concrete URL. It currently keeps that value on the trace and log, not on the custom metric.

`IMeterFactory` comes from dependency injection, which also isolates meters between test service providers. Microsoft's [.NET metrics guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) recommends this host-oriented pattern and describes counters for totals and histograms for distributions.

### Treat activities as optional instrumentation objects {#activity-lifecycle}

`ActivitySource.StartActivity` creates the internal `booking.http.request` child only when a listener is interested. It can return `null`; the request must still run. The middleware therefore null-checks tags and status, and disposes a created activity in `finally` so both success and failure stop it.

The child activity adds booking-specific outcome tags beneath ASP.NET Core's server activity. This is useful only if those tags answer a tracing question. A team satisfied with the built-in server span and enriched logs could omit the child rather than create a redundant span. Instrumentation should have an investigation purpose.

The official [.NET tracing guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) likewise notes the `null` behavior and that disposing an activity stops it.

Most importantly, `Meter`, `ActivitySource`, and log calls are producers. They do not create a collector, durable store, dashboard, alert, retention policy, or access policy. After implementation, `MeterListener` and `ActivityListener` can verify signal production; deployment must separately configure and test collection.

## Put verification behind one command {#release-check}

A real application should expose its acceptance path as one documented command. For a .NET solution, the baseline can be:

```console
dotnet test path/to/YourSolution.slnx --configuration Release
```

If acceptance also needs a separate API process and client, use an application-specific script. It should create a unique temporary directory, listen on an available loopback port, and clean up the exact child process and directory in `finally`. That orchestration belongs to the application, not to this book site.

A robust acceptance command orders its stages deliberately:

1. restore the solution in locked mode;
2. build the whole solution in `Release` without another restore;
3. run every test whose fully qualified name contains `Booking`;
4. start the real API with a fresh local snapshot and deterministic stubs;
5. run the independent C# client through place, replay, confirm, and GET;
6. send malformed JSON with a separate HTTP client;
7. match its 32-character response correlation ID to the client-error log;
8. require at least one success log and reject known secret-bearing text;
9. stop the server and remove the temporary snapshot even on failure.

A concise final report might look like:

```text
Capstone check passed.
Placed: id=REQ-CAPSTONE-CHECK seats=2 status=pending
Replay: status=201 same-body=True
Confirmed: id=REQ-CAPSTONE-CHECK code=CONF-CSHARP status=confirmed
Loaded: status=200 same-body=True
Diagnostics: success=true client-error=true correlation=<32 lowercase hex characters> secrets=false
```

This output is a compact summary, not the complete test report. A failure includes bounded tail output so a runaway child cannot consume unlimited memory. Process startup and HTTP calls also have timeouts.

### Reproduce it from a clean state {#clean-state}

The application's README should name exact prerequisites, the one-command check, and any manual debugging flow. The manual path is valuable when inspecting logs or stepping through a request; the automated path is valuable because it controls names, ports, timeouts, assertions, and cleanup.

“No external service” means the workflow needs no cloud account, private feed, payment provider, broker, or telemetry backend. A locked restore may still download public NuGet packages when the local cache is empty. Reproducible inputs do not imply an offline cache exists.

Do not make the manual command appear simpler by telling readers to delete a broad directory. Create one unique disposable directory and remove exactly that path after the API stops. Production data is never a cleanup target.

## Do not call a build a deployment {#build-publish-deploy}

The name “release check” describes an acceptance gate. It does not currently run `dotnet publish`, create a signed artifact, produce an SBOM, scan a container, deploy an environment, migrate storage, or verify a rollback.

The stages have different meanings:

| Stage | Question answered |
|---|---|
| restore | can declared locked dependencies be resolved? |
| build | does this source compile under the selected configuration? |
| test | do the checked behaviors hold in the test environments? |
| publish | what deployable files are produced for a chosen runtime model and target? |
| deploy | can an environment run that immutable artifact with its real configuration and dependencies? |
| operate | can owners detect, mitigate, recover, and learn from failures? |

Microsoft's [.NET publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) distinguishes framework-dependent and self-contained publishing, along with runtime-specific and single-file choices. Select among them from the deployment environment, patching model, target OS/architecture, startup, and size requirements. Do not silently treat the developer machine's `dotnet run` output as the artifact contract.

### Define the missing production gate {#production-gate}

Before this service handles real bookings, a concrete system must decide and verify at least:

- authenticated callers, authorization policy, TLS termination, rate limits, and abuse handling;
- real secret injection, rotation, redaction, and least-privilege access;
- multi-process transactional or conditional storage, schema migration, backup, restore, RPO, and RTO;
- payment-provider idempotency and reconciliation for ambiguous outcomes;
- a transactional outbox, consumer deduplication, dead-letter handling, and replay policy;
- health/readiness behavior that reflects required dependencies without leaking internals;
- telemetry export, sampling, cardinality budgets, retention, dashboards, alerts, and ownership;
- a versioned publish artifact, provenance, vulnerability review, promotion, and rollback rehearsal;
- load, failure-injection, restart, and deployment-topology tests against production-like dependencies.

That list is not a request to add every mechanism to the teaching repository. It is a boundary checklist. Architecture should grow when a named requirement and test environment exist.

## Plan the post-acceptance guarantee ledger {#guarantee-ledger}

Only after the in-page design has been implemented and the acceptance path above has passed may the project make these narrow claims:

- protected F# constructors and deciders enforce the modeled booking states and transitions;
- strict DTO mapping rejects malformed and unknown transport data before domain work;
- cooperating services in one process and one normalized snapshot path do not oversell aggregate capacity;
- exact completed commands replay without repeating the modeled payment or notification calls;
- a changed payload under the same operation identity conflicts;
- cancellation releases committed occupancy, and canceled waits release synchronization resources;
- an ambiguous payment is not blindly repeated;
- orderly restart can load durable progress and replay a completed result;
- the final HTTP route maps the consistent service rather than the earlier port-only workflow;
- a current C# consumer can complete the public JSON workflow without domain references;
- controlled logs, measurements, and activities correlate a successful or rejected request;
- after declared public packages are available, the complete local check needs no external runtime account or service.

The same ledger must retain the limits:

- the file adapter is not safe for writers in multiple processes or machines;
- replacement is not a general ACID or power-loss durability guarantee;
- the stubs do not authorize money or deliver a real message;
- notification and cross-system effects are not exactly once;
- payment reconciliation and reservation expiry are not implemented;
- authentication, TLS policy, secret management, and abuse controls are absent;
- instrumentation has no configured exporter or operational backend;
- the release check neither publishes nor deploys an artifact;
- no production SLO, RPO, RTO, scale envelope, or supported upgrade path is claimed.

Keeping both halves together prevents a test list from becoming marketing language. A claim should name its topology, dependency, failure model, and observation.

## Notice what F# contributes to the closure {#fsharp-role}

The final composition still reflects the language's strengths. Domain types prevent arbitrary invalid states, and `Result` makes expected failures part of endpoint matching. Function records create narrow ports; `task` carries cancellation through HTTP and I/O. Pattern matching exposes the error-to-status mapping, while deterministic serialization gives another language a straightforward contract.

F# also makes it comfortable to keep the policy core smaller than the host. The executable is mostly wiring. The C# client demonstrates that this internal style does not require every external consumer to adopt F# representations.

The language does not select a production database, make a provider idempotent, export telemetry, secure a network, or operate a deployment. The mature use of types is to expose those remaining boundaries, not to hide them behind a generic “effect” abstraction.

## Exercises {#exercises}

### Exercise 1: audit three inflated claims {#exercise-01}

Assume a team has implemented this chapter's project and run the described acceptance path. Its release note says: “The booking API is safe across three replicas, performs payment and notifications exactly once, and is production ready because all tests pass.” Rewrite it as a guarantee ledger. For each claim, identify that hypothetical project's strongest evidence, the missing topology or dependency, the next mechanism, and a test that would produce the missing evidence. Do not merely replace every sentence with “not guaranteed.”


::: details Answer

#### Separate the evidence from the missing boundary {#exercise-01-ledger}

Start with a four-column ledger:

| Inflated claim | Strongest current evidence | Missing mechanism or boundary | Next decisive test |
|---|---|---|---|
| safe across three replicas | concurrent commands through several service objects sharing one process and normalized file path do not oversell | one transactional/conditional store shared by independent processes; aggregate version by event | start three hosts against the real store, force them to read one version, release competing conditional writes, then verify committed occupancy |
| payment and notification exactly once | exact completed retries do not repeat controlled stub calls; ambiguous payment is not blindly retried; pending notification intent survives orderly restart | provider idempotency and lookup; transactional outbox; at-least-once relay; consumer atomic deduplication; reconciliation | kill at every provider/outbox acknowledgment boundary and compare provider records, outbox rows, publishes, and consumer state |
| production ready because tests pass | locked Release build, focused tests, TestServer HTTP integration, real local Kestrel and C# smoke, safe sample logs | security, real dependencies, publish artifact, migration, topology, load envelope, telemetry backend, SLO/RPO/RTO, rollout and recovery | deploy the immutable candidate to a production-like environment and exercise security, migration, load, dependency failure, restore, rollout, and rollback gates |

The first current claim is still useful: **within one process and one normalized snapshot path, cooperating service instances serialize the aggregate capacity decision**. The missing evidence begins exactly at the OS-process boundary.

The second current claim should split in two: **an exact completed operation replays its local result without repeating the modeled stub calls**, and **an ambiguous payment stops for reconciliation rather than charging again**. Neither sentence says what a real provider or notification consumer did.

The third defensible claim, once an application-specific gate has actually passed, is: **the application has a reproducible local acceptance check for its documented topology**. “Production ready” is not a single testable property until a production contract names environment, traffic, security, dependencies, availability, durability, and responsibilities.

#### Rewrite the release note {#exercise-01-rewrite}

A defensible note, after the described acceptance path has run successfully, could read:

> The booking capstone now passes its locked local acceptance gate. The verified topology is one API process using one local snapshot path and controlled payment/notification adapters. Exact completed retries do not repeat those adapter calls, changed payloads conflict, ambiguous payment stops for reconciliation, and a C# HTTP consumer completes the public workflow. Multi-process storage, real provider delivery, security controls, telemetry export, artifact deployment, and production recovery remain outside this release.

That statement preserves accomplishments while making the next engineering work obvious. It is more useful than either the inflated claim or a vague “nothing is guaranteed.”

:::

### Exercise 2: design collection without cardinality debt {#exercise-02}

The service will use an OpenTelemetry-compatible collector. Design the configuration and validation work without changing the domain model. Choose which built-in and custom sources to subscribe to, which attributes may become metric dimensions, how sampling works, what is redacted, and how logs join traces. Specify one automated test and one load test that catch a cardinality mistake. Decide whether the custom child activity earns its cost.


::: details Answer

#### Define the signal contract before the collector {#exercise-02-contract}

Subscribe to the supported ASP.NET Core server instrumentation plus these application sources:

| Signal | Source | Keep | Avoid |
|---|---|---|---|
| traces | built-in ASP.NET Core server source | method, matched route template, status; normal trace context | raw URL query, request body, authorization headers |
| traces | `ThinkingInFSharp.Booking.Api` | `booking.http.request` only if its outcome adds investigation value | request ID as a searchable global attribute unless policy explicitly permits it |
| metrics | `ThinkingInFSharp.Booking.Api` | request counter and duration histogram with bounded `outcome` | correlation ID, concrete path, exception message, user or provider identifier |
| logs | application completion event 1000 | trace/correlation, method, route template, status, outcome, duration | bodies, confirmation code, transaction text, exception message, snapshot path |

Keep the existing custom child activity for the first deployment because `booking.outcome` provides a stable application classification independent of raw status. Set an explicit review date. If queries never use the child and the built-in server span plus structured log answer the same questions, remove it to reduce span volume.

Do not sample metrics. Aggregate every request measurement in process and let the metrics pipeline export at the configured interval. For traces, start with parent-based probabilistic sampling. Retain or separately sample errors according to the collector's documented behavior. A head sampler cannot know a later outcome, so “keep every error” may require tail sampling or another error signal. State the latency, memory, and failure tradeoff rather than promising it for free.

Use a collector endpoint and credentials from deployment configuration. Enforce TLS and least privilege. Apply attribute allowlists or redaction in both the application and collector, because a collector rule is not a reason to emit known secrets. Bound queue memory, define export timeouts, and decide whether telemetry loss may ever affect request success; in most services it should not.

Microsoft's [.NET tracing guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) distinguishes instrument creation from collection. Its [metrics guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) recommends `IMeterFactory` in dependency-injection hosts, matching the design planned for verification here.

#### Test correlation and series growth {#exercise-02-tests}

First implement an in-process listener test that verifies local signal production without a vendor. Then add a collector integration test that sends one successful and one invalid request with a controlled valid `traceparent`, and assert:

- the response header and completion log use the same trace ID;
- the custom activity is a child of the server activity when sampled;
- the counter and histogram include `success` and `client_error` measurements;
- exported records contain no forbidden fields or fixture secrets;
- disabling the activity sampler does not change HTTP behavior.

For cardinality, send at least 10,000 requests with distinct booking IDs and concrete URL values. Query or inspect the test backend and assert a fixed upper bound on custom metric series—at most four `outcome` series per instrument with the current contract. Also cap distinct route values to the number of registered templates and verify that no correlation or booking ID appears as a metric attribute.

Run this as a load test, not just a source scan. A harmless-looking enrichment processor can add an identifier after application tests have passed. Observe collector memory, dropped data, export queue length, and backend ingestion while varying IDs.

:::

### Exercise 3: turn the check into a real release plan {#exercise-03}

Choose one explicit target, such as a framework-dependent Linux container or a self-contained `linux-x64` service. Extend the acceptance gate into a publish, promotion, deployment, and rollback plan. Name the immutable artifact, runtime/configuration contract, storage migration strategy, health gates, security checks, smoke tests, telemetry checks, rollout policy, and rollback trigger. State which steps can remain local and which require a production-like environment.


::: details Answer

#### Choose and name one artifact {#exercise-03-artifact}

Assume the target is a framework-dependent Linux container on `linux-x64`. The immutable release identity is an OCI image digest, not a mutable tag. A multi-stage build restores from the lock file and runs the complete acceptance suite. It then publishes `Booking.Api` in Release and copies only the publish output into a pinned, supported ASP.NET Core runtime image.

Record alongside the digest:

- source commit and clean-tree status;
- SDK, target framework, runtime-image digest, and lock-file hash;
- build provenance and software bill of materials;
- vulnerability and license scan results under an explicit policy;
- configuration schema version and database migration range;
- public DTO/API version and rollback compatibility window.

Framework-dependent means the runtime comes from the runtime image. Pinning that image improves reproducibility, but security patches require rebuilding and promoting a new image. Microsoft's [.NET publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) makes the runtime model a deliberate publish choice.

#### Promote the same bytes through gates {#exercise-03-pipeline}

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

Apply these runtime controls where the platform supports them:

- run as a non-root user on a read-only base filesystem;
- inject secrets through the platform, not the image;
- restrict outbound destinations;
- terminate TLS under a documented trust model; and
- enforce authentication, authorization, request-size limits, and rate limits.

Scan results need a responsible owner and an expiry for every exception. A green scanner alone is not a security design.

#### Make storage evolution and rollback compatible {#exercise-03-rollback}

Replace the file snapshot before claiming replica safety. Use an event-keyed transactional or version-conditional store, then design schema changes with an expand/migrate/contract sequence:

1. deploy code that reads old and new forms but writes a backward-compatible form;
2. apply an additive migration and verify it under concurrent load;
3. backfill with bounded, observable batches;
4. switch writes only after old readers are gone;
5. remove old fields in a later release after the rollback window.

An application rollback is safe only while the older binary can read the current schema and understand messages already emitted. Otherwise recovery is a forward fix or database restore, each with an explicit data-loss window. Test restore time and data correctness; do not infer RPO or RTO from the existence of backups.

Stop rollout automatically when error, latency, or saturation limits are breached. Also stop for failed readiness, unexpected payment ambiguity, or growing outbox backlog. Roll back only when the compatibility contract allows it. Preserve related logs, traces, and state, then open an incident; do not delete the failed environment before diagnosis.

Local gates can verify deterministic builds, unit and contract behavior, static security policy, and the local smoke test. Published-image execution, real database migrations, provider sandbox behavior, collector export, load, canary routing, backup restore, and rollback require production-like or production control planes.

:::


Part VI is complete. Part VII maps this foundation onto the wider F# and .NET ecosystem without pretending that every useful library belongs in one application.
