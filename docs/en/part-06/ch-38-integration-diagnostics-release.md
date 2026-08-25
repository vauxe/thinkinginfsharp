---
title: "Chapter 38: Integration, Diagnostics, C# Client, and Release Evidence"
description: "Close the booking-system loop with a real composition root, HTTP integration tests, a C# contract client, bounded diagnostics, and reproducible release evidence."
translationKey: part-06/ch-38-integration-diagnostics-release
---

# Chapter 38: Integration, Diagnostics, C# Client, and Release Evidence {#overview}

The preceding chapters built the booking system from the inside out. A precise domain model became a pure decider, ports and adapters, an HTTP boundary, and finally a consistency protocol. None of those layers alone proves that the executable uses them in the intended order. This chapter closes that gap.

The goal is not to add another architectural pattern. It is to connect one composition root, cross the public contract from another .NET language, observe the result without exposing sensitive data, and turn all of that into a command another person can reproduce. The final artifact is still a teaching system. Its value comes partly from saying exactly what it does **not** prove.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish component tests, in-process HTTP tests, and separate-process smoke tests;
- verify that the production entry point selects the intended consistency service;
- keep transport policy in the endpoint layer and business policy behind a typed port;
- use a C# client to test the public CLR and JSON contract rather than F# internals;
- correlate a response, structured log, metric, and trace without using unbounded metric dimensions;
- explain why an instrumentation point is not the same as a telemetry backend;
- design logs that help investigation without recording command bodies or secret-bearing values;
- make a release check deterministic, bounded, and self-cleaning;
- distinguish `build`, `publish`, deployment, and production readiness;
- maintain a ledger of proved guarantees, explicit limits, and required next evidence.

## Read the executable as a composition proof {#composition-proof}

A composition root answers a concrete question: which implementations will the running process actually use? Beautiful domain functions and strong adapter tests are irrelevant if the executable wires an older workflow around them.

Chapter 37 deliberately left that gap visible. The earlier `BookingEndpoints.map` path accepted `AsyncPorts`; it could not provide aggregate idempotency and capacity guarantees. The final entry point instead constructs `AtomicBookingStore`, the controlled payment and notification adapters, and `IdempotentBookingService`, then exposes only two operations to the HTTP layer.

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

## Build an evidence ladder {#evidence-ladder}

“The tests pass” is incomplete unless you can say which boundary each test crosses. This project uses several deliberately overlapping levels:

| Evidence level | Real components crossed | Useful claim | Claim it cannot make |
|---|---|---|---|
| pure example/property tests | domain values, decider, mappings | rules hold over examples and generated inputs | files, HTTP, and process startup work |
| adapter contract tests | strict JSON, snapshot files, configuration | local persistence and mapping obey their contracts | concurrent replicas are safe |
| consistency tests | aggregate store, service, controlled effects | modeled races, retries, and restart phases behave as specified | public HTTP maps every outcome correctly |
| in-process HTTP tests | ASP.NET Core pipeline, DTOs, final service, file adapter | status, body, headers, persistence, and effects compose | sockets, command-line startup, and another process work |
| separate-process smoke | real Kestrel socket and independent C# process | packaged source builds and the public workflow starts locally | a production topology, real provider, or failover works |

Microsoft's [ASP.NET Core integration-testing guidance](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) describes integration tests as broader tests that include the request pipeline and supporting infrastructure, while recommending that routine logic remain in faster unit tests. That is the reason for the ladder, not a reason to move every permutation through HTTP.

### Make HTTP effects observable in tests {#http-effects}

The end-to-end fixture builds a real `WebApplication`, selects `TestServer`, registers the same diagnostic middleware, maps the same consistent endpoints, and uses a temporary snapshot. Controlled payment and notification functions increment thread-safe counters.

Focused integration tests establish these facts:

- normalized exact placement replays the same `201` body and does not repeat effects;
- changed seats under the same operation identity return `409 idempotency_conflict`;
- invalid JSON returns `400 invalid_json` before creating a snapshot or invoking an effect;
- an ambiguous payment returns `503` first, then `409 payment_outcome_unknown`, with one payment call;
- the diagnostic test aligns the response correlation ID with bounded metrics and one stopped child activity.

The first two facts share one test because the effect counters are the causal observation. A response assertion alone would miss a duplicate payment hidden behind a replayed body.

`TestServer` sends HTTP abstractions in memory. That makes the pipeline fast and deterministic, but it intentionally avoids port allocation, TLS, and kernel networking. The release smoke therefore adds a second, smaller test across a real loopback socket.

### Prefer signals over delays {#causal-tests}

Concurrency tests elsewhere in the capstone use barriers and task-completion signals to force both operations into the dangerous interval. Restart tests launch a genuinely separate process against the persisted snapshot. Those facts are stronger than “run it many times and hope the scheduler is unlucky.”

Repetition still has a role: it can detect leaked shared state and nondeterministic cleanup. It is not a substitute for controlling the causal interleaving that defines the bug.

## Prove the public contract from C# {#csharp-contract}

F# and C# share the CLR, but they do not share identical ergonomics. A public F# API can compile while exposing curried functions, F#-specific unions, options, or generic shapes that are awkward to ordinary C# callers. Chapter 27 designed separate CLR-friendly DTOs; this chapter consumes them from an actual C# executable.

The client directly references only `Booking.Contracts`. It never references `Booking.Domain` or `Booking.Infrastructure`, and it communicates with the service only through `HttpClient` and JSON.

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
This one flow checks four contract properties:

| Step | Contract evidence |
|---|---|
| place | object initializers can construct the DTO; JSON produces `201` and a pending booking |
| exact replay | application idempotency returns the same acknowledged status and body |
| confirm | another DTO crosses the same boundary and produces a representable confirmed response |
| GET | URL escaping and response DTO deserialization work without F# domain knowledge |

The client deliberately configures strict, case-sensitive deserialization and rejects unmapped properties. That is a compatibility test for the chosen contract, not a rule every consumer must copy. The comparison of raw successful bodies is also narrow: it proves deterministic output in this contract version, not that arbitrary JSON texts with different property order are semantically unequal.

A successful C# client does not prove binary compatibility with every previous assembly version. That requires retained consumer fixtures or an API-compatibility tool against a declared baseline. It does prove that the current published surface is usable in the most important cross-language path.

## Instrument the boundary, not the secret {#diagnostics}

When a request fails, an operator first needs a small set of answers: which operation boundary ran, when, how long, which outcome class occurred, and which trace connects the evidence? Logging an entire command is a tempting shortcut that can turn diagnostics into a data leak.

The booking middleware records a completion event with stable field names:

```text
Booking request completed correlationId=<trace-id> method=<method> endpoint=<route-template> statusCode=<status> outcome=<outcome> elapsedMs=<duration>
```

It does not record request or response bodies, booking request IDs, confirmation codes, provider transaction text, exception messages, or the snapshot path. The HTTP response receives `X-Correlation-ID`. When an active `Activity` exists, the value is its 32-character W3C trace ID; otherwise the middleware creates a random trace ID of the same bounded form.

### Correlation is a join key, not proof of identity {#correlation}

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

Most importantly, `Meter`, `ActivitySource`, and log calls are producers. They do not create a collector, durable store, dashboard, alert, retention policy, or access policy. The sample tests production of signals with `MeterListener` and `ActivityListener`; deployment must separately configure and test collection.

## Turn the proof into one command {#release-check}

A real application should expose its acceptance path as one documented command. For a .NET solution, the baseline can be:

```console
dotnet test Sample.slnx --configuration Release
```

If acceptance also needs a separate API process and client, an application-specific script should create a uniquely named temporary directory, listen on `127.0.0.1` with an available port, and clean up the exact child process and directory in `finally`. That orchestration belongs to the application, not to this book site.

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

This output is a compact witness, not the complete test report. A failure includes bounded tail output rather than allowing a runaway child to consume unlimited memory. Process startup and HTTP calls also have timeouts.

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

Before this service handles real bookings, a concrete system would need decisions and evidence for at least:

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

## Keep a guarantee ledger {#guarantee-ledger}

The capstone can now make these narrow, tested claims:

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

The final composition still reflects the language's strengths. Domain types prevent arbitrary invalid states; `Result` makes expected failures part of the endpoint match; records of functions create small ports; `task` carries cancellation through HTTP and I/O; pattern matching makes the error-to-status table reviewable; deterministic serialization gives another language a plain contract.

F# also makes it comfortable to keep the policy core smaller than the host. The executable is mostly wiring. The C# client demonstrates that this internal style does not require every external consumer to adopt F# representations.

The language does not select a production database, make a provider idempotent, export telemetry, secure a network, or operate a deployment. The mature use of types is to expose those remaining boundaries, not to hide them behind a generic “effect” abstraction.

## Avoid common closure mistakes {#common-mistakes}

- Testing the service but forgetting to wire it into the executable leaves a false green path.
- Reimplementing endpoint validation for the final service lets two public paths drift.
- Calling `TestServer` a real network test ignores sockets, startup arguments, and process lifetime.
- Using only a real-process smoke test makes failure cases slow and hard to control.
- Letting the C# client reference domain internals defeats the contract test.
- Putting request IDs or correlation IDs on metrics creates unbounded cardinality.
- Logging bodies “temporarily” often creates a permanent sensitive-data store.
- Assuming `StartActivity` is non-null makes behavior depend on whether a listener is installed.
- Creating custom spans without an investigation question adds noise and cost.
- Checking logs for one forbidden literal is evidence for that fixture, not a universal secret scanner.
- Calling `dotnet build -c Release` a published artifact skips target and runtime decisions.
- Adding production infrastructure to make the sample look complete obscures rather than fixes its boundary.

## Exercises {#exercises}

### Exercise 1: audit three inflated claims {#exercise-01}

A release note says: “The booking API is safe across three replicas, performs payment and notifications exactly once, and is production ready because all tests pass.” Rewrite it as a guarantee ledger. For each claim, identify the strongest current evidence, the missing topology or dependency, the next mechanism, and a test that would produce the missing evidence. Do not merely replace every sentence with “not guaranteed.”

### Exercise 2: design collection without cardinality debt {#exercise-02}

The service will use an OpenTelemetry-compatible collector. Design the configuration and validation work without changing the domain model. Choose which built-in and custom sources to subscribe to, which attributes may become metric dimensions, how sampling works, what is redacted, and how logs join traces. Specify one automated test and one load test that catch a cardinality mistake. Decide whether the custom child activity earns its cost.

### Exercise 3: turn the check into a real release plan {#exercise-03}

Choose one explicit target, such as a framework-dependent Linux container or a self-contained `linux-x64` service. Extend the acceptance gate into a publish, promotion, deployment, and rollback plan. Name the immutable artifact, runtime/configuration contract, storage migration strategy, health gates, security checks, smoke tests, telemetry checks, rollout policy, and rollback trigger. State which steps can remain local and which require a production-like environment.

[Read the chapter solutions](../solutions/ch-38-integration-diagnostics-release).

## Chapter review {#chapter-review}

- The composition root is evidence that the executable selects the intended implementations.
- A shared endpoint surface prevents transport policy from drifting between orchestration versions.
- Pure, adapter, consistency, in-process HTTP, and separate-process tests support different claims.
- Effect counters make “no duplicate side effect” observable rather than inferred from a response.
- A C# HTTP client proves the current public DTO path without exposing F# domain internals.
- Correlation IDs join evidence; they are not caller identity or authorization.
- Metrics need bounded dimensions, while high-cardinality detail belongs in controlled traces or logs.
- Instrumentation sources do nothing operational until collection, storage, policy, and ownership exist.
- One documented acceptance command should own cleanup and fail when any required stage breaks.
- Build, publish, deploy, and operate are distinct stages with distinct evidence.
- A guarantee ledger must preserve both proved behavior and explicit limitations.
- F# makes the policy and boundaries precise; production guarantees still come from real infrastructure and operations.

Part VI is complete. Part VII maps this foundation onto the wider F# and .NET ecosystem without pretending that every useful library belongs in one application.
