---
title: "Chapter 32: From Functions to Applications"
description: "Derive a small executable F# application from a pure workflow by making configuration, ports, composition, cancellation, ownership, and minimum observability explicit."
translationKey: part-05/ch-32-functions-to-applications
kind: chapter
part: 5
chapter: 32
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch32-functions-to-applications
  - foundation-example-tests
exerciseIds:
  - ch32-exercise-01
  - ch32-exercise-02
  - ch32-exercise-03
termIds: []
sources:
  - id: microsoft-dotnet-generic-host
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
    checked: "2026-08-24"
  - id: microsoft-dotnet-configuration
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
    checked: "2026-08-24"
  - id: microsoft-dotnet-logging
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging
    checked: "2026-08-24"
  - id: microsoft-dotnet-metrics
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation
    checked: "2026-08-24"
  - id: microsoft-dotnet-metric-collection
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection
    checked: "2026-08-24"
  - id: microsoft-dotnet-tracing
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
    checked: "2026-08-24"
  - id: microsoft-dotnet-di-guidelines
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
    checked: "2026-08-24"
---

# Chapter 32: From Functions to Applications {#overview}

A pure function can decide what should happen, but a running application must also obtain configuration, call storage or networks, propagate cancellation, report what happened, and release what it owns. Those responsibilities do not invalidate the functional core. They define a boundary around it.

This chapter builds the smallest useful boundary around the booking workflow developed earlier. The result is an executable console application with no dependency-injection container and no telemetry vendor. Its architecture is visible in ordinary F# values and one composition root. The point is not that every application should remain this small; it is that a stronger host should solve a demonstrated hosting problem rather than hide an undefined design.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish a pure domain workflow, application orchestration, adapters, a composition root, and a process host;
- derive narrow effect ports from what the workflow needs rather than from a framework;
- turn untrusted configuration text into validated domain values before starting work;
- propagate one `CancellationToken` across every cancellable port call;
- state who owns each `IDisposable` resource and when that ownership ends;
- emit a structured event, one low-cardinality metric, and one trace activity;
- distinguish instrumentation from collection, export, storage, dashboards, and alerts;
- test wiring and observability without making domain decisions impure;
- recognize what this sample proves and which production guarantees it deliberately lacks;
- decide when explicit construction is enough and when the .NET Generic Host earns its cost.

## See one application as several boundaries {#application-boundaries}

“The application” is too coarse a unit for reasoning. Separate responsibilities by the kinds of facts they know:

| Layer | Knows | Does not decide |
|---|---|---|
| Domain workflow | Valid commands, booking state, domain rules, domain events | Environment variables, databases, logging providers |
| Application orchestration | Order of effects, cancellation, expected versus unexpected outcomes | How a database or telemetry backend works |
| Adapter | How one external capability is performed | Booking policy |
| Composition root | Which concrete adapters and settings form this process | Per-request business rules |
| Process host | Arguments, exit code, process lifetime and shutdown signals | Domain transitions |

The dependency direction points inward. The application layer calls the domain and refers to small port types. Concrete adapters are supplied from the outside. The domain does not call back into `Program`, read global configuration, or choose an exporter.

A useful execution path is:

```text
process input
  -> validate configuration
  -> construct adapters and application
  -> receive command and cancellation
  -> validate command
  -> load state
  -> pure decision
  -> append event
  -> record observable outcome
  -> dispose owned resources
```

This is a sequence, not a claim that every step belongs in one function. It exposes the seams at which tests, failures, and ownership can be discussed.

## Derive ports from required effects {#derive-ports}

Start with the pure workflow's inputs and output. `decidePlaceBooking` needs an `Event`, current `BookingState`, and `PlaceBookingCommand`; it returns `Result<BookingEvent, PlaceBookingError>`. A running application must therefore obtain the current state and persist an accepted event. Those are the two effect capabilities in the sample:

<<< @/../examples/chapters/ch32/Ports.fs#ports{fsharp:line-numbers} [Ports.fs]

The record contains functions, not implementation classes. Each signature says something useful:

- `RequestId` is already a validated domain value at the storage boundary;
- `CancellationToken` is an explicit input to every potentially blocking operation;
- `Task<'T>` admits asynchronous completion and faults at the .NET boundary;
- `AppendEvent` returns `Task<unit>` because the application needs completion, not a storage-shaped response;
- `OwnedResource` makes this sample's ownership transfer visible.

Do not create one port per method merely to imitate an interface-heavy architecture. Group capabilities that share one coherent adapter and lifecycle; split them when callers, failure policies, security boundaries, or lifetimes actually differ. A record of functions is convenient for small F#-facing boundaries and test doubles. An interface may be more appropriate for C# consumers, framework activation, or a stateful protocol.

The domain still owns the rule. A port named `CanBook` would push policy into storage; a port named `LoadBooking` supplies a fact that the pure function can interpret. Likewise, the adapter stores a `BookingEvent` rather than deciding whether a request is acceptable.

## Treat configuration as untrusted input {#configuration}

Environment variables, JSON, command-line arguments, and secret stores all begin as external data. Their presence does not make them valid domain configuration. Parse and validate once near startup, then pass a typed value inward:

<<< @/../examples/chapters/ch32/Ports.fs#configuration{fsharp:line-numbers} [Ports.fs]

`AppConfig.load` accepts a lookup function instead of reading `Environment` directly. Production passes an environment lookup; the fixed demo and tests pass deterministic functions. This tiny seam avoids global mutation and does not require a configuration framework.

The loader accumulates independent errors for `BOOKING_EVENT_ID` and `BOOKING_CAPACITY`. If both are wrong, an operator can repair both before the next start. Parsing an integer is only the representation step; `Capacity.create` enforces the domain rule that capacity must be positive. The private `AppConfig` record prevents later code from constructing an unvalidated configuration record directly.

Configuration policy still needs deliberate choices:

- define source precedence instead of relying on accidental call order;
- fail startup for missing settings required by every request;
- validate ranges, formats, and cross-field rules, not only parsability;
- never print secret values in an error, log, metric tag, or trace tag;
- decide explicitly whether a setting is a startup snapshot or can reload;
- test the effective configuration of the published process.

The broader .NET configuration system unifies providers such as JSON, environment variables, command-line arguments, in-memory values, and secret stores behind `IConfiguration`. Use it when provider layering and framework integration are requirements. It does not replace conversion into domain-specific validated types.

## Keep construction in one composition root {#composition-root}

A composition root is the outermost place that selects concrete dependencies and establishes ownership. In the sample, the reusable construction function remains deliberately unsurprising:

<<< @/../examples/chapters/ch32/Composition.fs#composition-root{fsharp:line-numbers} [Composition.fs]

`Program` performs the remaining process-specific work: choose the lookup, install demo listeners, construct the in-memory store, create the application, run one command, and translate the result to output and an exit code. Domain modules contain none of those choices.

Manual construction is dependency injection in the literal sense: dependencies arrive as arguments. A DI container automates registration, resolution, scopes, and disposal; it is not the source of inversion of control. Keeping a composition root remains valuable even when a container later performs the construction.

Avoid resolving dependencies from a global service locator inside domain or application functions. That hides requirements from signatures, makes lifetime ambiguous, and forces tests to recreate ambient state. Explicit arguments make the dependency graph reviewable.

## Orchestrate effects around the pure decision {#orchestration}

The application method owns sequencing while reusing the existing domain workflow:

<<< @/../examples/chapters/ch32/Composition.fs#place{fsharp:line-numbers} [Composition.fs]

Read the method in order:

1. Reject use after disposal and start an optional activity.
2. Observe cancellation before invoking a port.
3. Validate the raw command so storage receives a typed `RequestId`.
4. Load the current state with the caller's token.
5. Call `decidePlaceBooking` for the domain decision.
6. Append only an accepted domain event, with the same token.
7. Record one terminal outcome and return the domain result.
8. Observe cancellation or an unexpected fault, then rethrow it.
9. Dispose the activity in `finally`, including every exit path.

The standalone domain function validates the command again. That repeats a cheap pure operation but not the validation rule: both calls use `validatePlaceBooking`. The first obtains a typed key before an effect; the public workflow remains safe when called independently. A later public API could expose a workflow that accepts `ValidPlaceBooking`, but only if that boundary improves the whole model.

Expected business refusal remains `Error PlaceBookingError`. Cancellation remains `OperationCanceledException`, so .NET callers and hosts recognize it as cancellation. An unexpected adapter fault remains a faulted task. Converting all three into one undifferentiated `Result` would erase operational meaning.

The sample does not retry. A retry policy must know whether the operation is transient and whether an append is idempotent. Retrying an ambiguous write without an idempotency key can duplicate an event. Add policy at the boundary only after defining those semantics.

## Make lifecycle ownership explicit {#lifecycle}

Every disposable object needs one owner. “It will be collected” is not a lifetime rule: `Dispose` often releases handles, sockets, buffers, subscriptions, or telemetry state that garbage collection does not release promptly.

The sample establishes this contract: `Composition.start` receives `BookingPorts`, and the resulting `BookingApplication` owns `ports.OwnedResource`. Disposing the application disposes that resource, its `ActivitySource`, and its `Meter` once. `Program` uses `use app = ...`, so normal completion and exceptions leave the scope through deterministic cleanup.

That contract is intentionally visible, but it is not universal:

| Construction rule | Resource owner | Receiver action |
|---|---|---|
| Application explicitly creates an adapter for its own lifetime | Application or enclosing composition root | Dispose after work drains |
| Caller passes a shared adapter without transferring ownership | Caller | Receiver must not dispose it |
| DI container creates a registered disposable service | Container/scope | Consumer must not dispose it |
| Factory creates a short-lived resource | The scope using the factory result | `use`/`use!` at that scope |

If cleanup performs asynchronous I/O, model `IAsyncDisposable` and use `use!` in an appropriate task expression. If requests can still be running, shutdown must first stop accepting new work, signal cancellation, allow a bounded drain, and only then dispose dependencies. This tiny one-command process has no concurrent drain protocol.

Disposal is not crash recovery. `SIGKILL`, power loss, or process termination can bypass cleanup. Durable correctness must come from storage transactions, idempotency, and recovery rules rather than a `finally` block.

## Add the minimum useful observability {#observability}

Observability begins with questions, not products. The sample asks three different questions and uses three different signals:

| Signal | Question | Sample evidence | Cardinality guidance |
|---|---|---|---|
| Structured log event | What happened in one named attempt? | outcome, request ID, seats, detail | Identifiers may be searchable fields subject to privacy and retention policy |
| Counter measurement | How many attempts ended by outcome? | `booking.requests{outcome=accepted}` +1 | Keep tag values bounded; never add request ID |
| Trace activity | Where did this operation spend time and how did it end? | `booking.place` with outcome and request ID | Per-operation context is appropriate, but sampling and sensitive-data policy still apply |

These signals complement rather than duplicate one another. Metrics make trends and alerts inexpensive, traces correlate work across a path, and logs retain discrete diagnostic events. None repairs an incorrect domain rule.

### Structured logs preserve fields {#structured-logs}

`BookingLog` is a record whose fields are serialized to one JSON object in the demo. That is stronger than building an unparseable prose sentence because a collector can preserve `eventName`, `outcome`, `requestId`, `seats`, and `detail` as separate fields.

It is still only a teaching adapter. A production application commonly maps this event to `ILogger` with stable message templates, event IDs, levels, scopes, redaction, and configured providers. Providers determine where logs go; console output alone is not durable storage. Avoid interpolating secrets or uncontrolled payloads before the logging system can filter them.

The sample classifies an accepted or rejected domain decision as a completed operation and a thrown adapter exception as `faulted`. Rejection is not automatically a warning or trace error: “capacity exceeded” can be an ordinary business outcome. Define severity from operational action, not from the spelling of a union case.

### Metrics publish measurements; collectors aggregate them {#metrics}

The diagnostic names are stable constants, and the application creates one counter:

<<< @/../examples/chapters/ch32/Composition.fs#diagnostics-names{fsharp:line-numbers} [Composition.fs]

`Counter<int64>.Add` publishes an increment. It does not by itself create historical storage, a rate chart, retention, or an alert. A collection tool aggregates measurements and may export them to a backend. The demo's `MeterListener` merely observes one in-process measurement so the example and test can prove the instrumentation fired.

The `outcome` tag has four bounded values in this application: `accepted`, `rejected`, `canceled`, and `faulted`. A request ID is deliberately absent. Metric systems usually allocate a time series for each tag combination; unbounded IDs can create excessive memory, storage, and cost.

Use a counter for occurrences that only increase. Use a histogram when a duration or size distribution and its tail matter. Do not encode latency as one average counter pair unless its limitations are acceptable. Define units and descriptions so a collector does not have to guess.

### Traces surround meaningful work {#traces}

`ActivitySource.StartActivity` may return `null` when no interested listener exists. The sample treats that as normal and avoids dereferencing it. When an activity exists, disposing it stops it; `finally` ensures a completed activity for success, rejection, cancellation, and faults.

The activity records low-ambiguity tags and a status. Accepted and rejected decisions completed as designed, so they receive `Ok`; an unexpected fault receives `Error`; cancellation remains distinct. A production convention may refine those choices, but it should be consistent across services.

Creating an `ActivitySource` is instrumentation, not distributed-trace collection. A collector such as OpenTelemetry must subscribe, sample, enrich, batch, and export activities. The local `ActivityListener` proves one activity stopped; it does not prove cross-process propagation, backend delivery, retention, or a useful trace query.

## Read the fixed evidence narrowly {#fixed-evidence}

Run the deterministic demonstration after the Release build:

```console
dotnet examples/chapters/ch32/bin/Release/net10.0/Ch32.App.dll --demo
```

It emits exactly:

```text
{"eventName":"booking.place","outcome":"accepted","requestId":"REQ-32","seats":2,"detail":"event-appended"}
result: accepted=true
metric: name=booking.requests value=1 outcome=accepted
trace: name=booking.place outcome=accepted
lifecycle: store-disposed=true
```

This proves that one fixed command passed through configuration, composition, the pure decision, the in-memory append, all three local observable signals, and deterministic disposal. Focused tests additionally prove independent configuration errors accumulate, the same cancellation token reaches both ports, one accepted event is appended, and a pre-canceled token calls no port.

It does not prove that logs or telemetry reach an external backend, that the adapter is durable, or that concurrent requests preserve capacity. `LoadBooking` followed by `AppendEvent` is not an atomic transaction. Two callers can read the same state before either append. The in-memory adapter is a wiring demonstration, not a production booking store.

## Test the boundary without retesting the domain {#boundary-tests}

Application tests should observe responsibilities unique to the application layer:

- malformed independent settings produce all relevant startup errors;
- invalid commands perform no storage effect;
- accepted decisions append exactly one event;
- rejected decisions append none;
- the caller's cancellation token reaches every port;
- pre-cancellation avoids calling a port and remains cancellation;
- adapter faults remain faults while emitting the chosen terminal signal;
- each attempt emits exactly one terminal metric/log outcome;
- an activity is stopped on every path when a listener samples it;
- the declared owner disposes each owned resource once.

Keep domain rule examples and properties in domain tests. Boundary tests can use deterministic function records and tracking disposables rather than a real database or telemetry vendor. Separate integration tests should then prove each production adapter's protocol, serialization, and failure behavior.

Do not make every test assert every signal. One focused contract test can fix telemetry shape; most orchestration tests should emphasize effects and results. Otherwise an innocent wording change creates noise across the suite.

## Know when a stronger host is justified {#stronger-host}

An explicit composition root is often enough for a command-line tool, a small single-purpose process, a library-owned worker invoked by another host, or an early application whose dependencies and lifetime fit on one screen.

The .NET Generic Host becomes useful when the process needs several standard facilities together:

- layered configuration providers and environment conventions;
- logging providers, filtering, and scopes;
- DI registration, scopes, and container-owned disposal;
- multiple `IHostedService` or `BackgroundService` workers;
- coordinated startup, shutdown signals, and graceful stopping;
- framework integrations that already expect host services.

For new non-web hosted applications, current .NET guidance recommends `Host.CreateApplicationBuilder`. Web applications normally use `WebApplicationBuilder`, which builds on related hosting facilities. Choosing either does not move domain rules into services or controllers. Preserve the pure workflow, narrow ports, typed configuration, and composition boundary.

A container is valuable when it manages a real object graph and scopes. Adding it to resolve three obvious values usually increases indirection without solving a problem. Conversely, hand-building dozens of scoped services and shutdown callbacks can recreate a weaker container badly. Let dependency count, lifetime diversity, framework integration, and operational needs decide.

## Review an application boundary {#review-checklist}

Before calling a host complete, ask:

- Can the domain run without environment, network, filesystem, clock, or telemetry globals?
- Does each effect appear in a port with domain-relevant inputs and explicit cancellation?
- Are external strings parsed into validated values before long-lived resources start?
- Is there one visible composition root?
- Does every disposable have exactly one documented owner?
- Are business rejection, cancellation, and unexpected fault still distinguishable?
- Do logs preserve fields and exclude secrets?
- Are metric tag combinations bounded and operationally meaningful?
- Can activities be absent without changing behavior?
- Is instrumentation connected to a real collection/export path in production?
- Are concurrency, idempotency, retry, and recovery guarantees stated rather than implied?
- Does a stronger framework remove demonstrated lifecycle work rather than merely relocate it?

## Exercises {#exercises}

### Exercise 1: derive ports and ownership {#exercise-01}

A pure function `decideDispatch : Inventory -> Order -> Result<Dispatch, DispatchError>` is ready to run in a worker. Derive the minimum ports for loading inventory and committing a dispatch. State the types, cancellation behavior, expected error boundary, and owner of a disposable database session. Do not introduce a container.

### Exercise 2: design three observable signals {#exercise-02}

For the dispatch attempt, define one structured log event, one metric, and one activity. Choose names, fields or tags, and terminal outcomes. Identify which values are bounded, which are high-cardinality, and which may be sensitive. Explain what a local listener proves and what still needs a collector/exporter test.

### Exercise 3: choose a hosting level {#exercise-03}

Choose between explicit construction and the Generic Host for: (a) a command that imports one file and exits, (b) a process running three background consumers with graceful shutdown, configuration layering, and logging providers, and (c) an ASP.NET Core API. Justify each choice and name the architectural boundaries that should remain unchanged.

[Read the chapter solutions](../solutions/ch-32-functions-to-applications).

## Model review {#model-review}

- A functional core decides; an application boundary obtains facts and performs effects.
- Ports describe required capabilities and domain-relevant data, not framework objects.
- Configuration remains untrusted until parsing and domain validation succeed.
- Manual construction is dependency injection; a container is optional automation.
- One composition root makes implementations and ownership visible.
- Cancellation passes unchanged through every cancellable effect.
- Business rejection, cancellation, and fault carry different operational meanings.
- Every disposable has one owner; shutdown must drain work before disposal.
- Logs, metrics, and traces answer different questions.
- Instrumentation produces signals; listeners, collectors, exporters, storage, and alerts are separate concerns.
- Metric dimensions must be bounded; per-request identifiers belong in controlled logs or traces, not metric tags.
- The fixed demo proves wiring, not durability, atomicity, recovery, or backend delivery.
- A stronger host is justified by real configuration, scope, worker, and shutdown needs.

## Part V checkpoint {#part-checkpoint}

Run the focused composition tests from the repository root:

```console
dotnet test tests/ExampleTests/ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~Ch32CompositionTests
```

Passing tests show that configuration errors accumulate, cancellation is observed before a port call, owned resources are disposed, and the sample emits its structured log, metric, and completed activity. They still prove only in-process wiring, not production export or durable delivery.

[Continue to Chapter 33](../part-06/ch-33-domain-language-model), where the capstone is rebuilt as one coherent application path.

## Sources {#sources}

- [Microsoft Learn: .NET Generic Host responsibilities and lifecycle](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Microsoft Learn: configuration providers and `IConfiguration`](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Microsoft Learn: structured logging and message templates](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Microsoft Learn: `Meter`, instruments, tags, and cardinality guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [Microsoft Learn: metric collection, aggregation, and export](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection)
- [Microsoft Learn: `ActivitySource`, nullable activities, tags, and collection](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
- [Microsoft Learn: DI ownership and disposal guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)
