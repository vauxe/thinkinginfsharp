---
title: "Chapter 32: From Functions to Applications"
description: "Derive a small executable F# application from a pure workflow by making configuration, ports, composition, cancellation, ownership, and minimum observability explicit."
translationKey: part-05/ch-32-functions-to-applications
---

# Chapter 32: From Functions to Applications {#overview}

A pure function decides what should happen. A running application must also load configuration, call storage or networks, propagate cancellation, report outcomes, and release resources. These responsibilities form the application shell around the functional core.

The example builds the smallest useful shell around the earlier booking workflow. Its console application uses plain F# values, one composition root, direct construction, and local instrumentation. A stronger host becomes worthwhile when the process truly needs layered configuration, lifecycle scopes, background workers, or framework integration.

## See one application as several layers {#application-boundaries}

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

This sequence does not imply that every step belongs in one function. It shows where to discuss tests, failures, and resource responsibility separately.

## Derive dependencies from required side effects {#derive-ports}

Start with the pure workflow's inputs and output. `decidePlaceBooking` needs an `Event`, current `BookingState`, and `PlaceBookingCommand`; it returns `Result<BookingEvent, PlaceBookingError>`. A running application must therefore obtain the current state and persist an accepted event. Those are the two effect capabilities in the sample:

```fsharp:line-numbers [Ports.fs]
type BookingPorts =
    { LoadBooking: RequestId -> CancellationToken -> Task<BookingState>
      AppendEvent: RequestId -> BookingEvent -> CancellationToken -> Task<unit>
      OwnedResource: IDisposable }

type BookingLog =
    { EventName: string
      Outcome: string
      RequestId: string
      Seats: int
      Detail: string }
```
The record contains functions, not implementation classes. Each signature says something useful:

- `RequestId` reaches storage only after domain validation;
- every potentially blocking operation receives a `CancellationToken`;
- `Task<'T>` represents asynchronous completion and .NET faults;
- `AppendEvent` returns `Task<unit>` because the application needs completion, not storage-specific response data;
- `OwnedResource` identifies the resource that the application must dispose.

Do not create one port per method merely to imitate an interface-heavy architecture. Group operations that share one adapter and lifecycle; split them when callers, failure policies, security requirements, or lifetimes differ. A record of functions is convenient for a small F# API and its test doubles. An interface may suit C# callers, framework activation, or a stateful protocol better.

The domain still defines the rule. A port named `CanBook` would push policy into storage; `LoadBooking` instead supplies a fact for the pure function to interpret. Likewise, the adapter stores a `BookingEvent` rather than deciding whether a request is acceptable.

## Treat configuration as untrusted input {#configuration}

Environment variables, JSON, command-line arguments, and secret stores all begin as external data. Their presence does not make them valid domain configuration. Parse and validate once near startup, then pass a typed value inward:

```fsharp:line-numbers [Ports.fs]
type ConfigError =
    | MissingSetting of name: string
    | InvalidSetting of name: string * value: string

type AppConfig = private { Event: Event }

module AppConfig =
    [<Literal>]
    let EventIdSetting = "BOOKING_EVENT_ID"

    [<Literal>]
    let CapacitySetting = "BOOKING_CAPACITY"

    let private readEventId (lookup: string -> string option) =
        match lookup EventIdSetting with
        | None -> Error [ MissingSetting EventIdSetting ]
        | Some raw ->
            EventId.create raw
            |> Result.mapError (fun _ -> [ InvalidSetting(EventIdSetting, raw) ])

    let private readCapacity (lookup: string -> string option) =
        match lookup CapacitySetting with
        | None -> Error [ MissingSetting CapacitySetting ]
        | Some raw ->
            match Int32.TryParse raw with
            | true, value ->
                Capacity.create value
                |> Result.mapError (fun _ -> [ InvalidSetting(CapacitySetting, raw) ])
            | false, _ -> Error [ InvalidSetting(CapacitySetting, raw) ]

    let load lookup =
        match readEventId lookup, readCapacity lookup with
        | Ok eventId, Ok capacity -> Ok { Event = Event.create eventId capacity }
        | Error eventErrors, Error capacityErrors -> Error(eventErrors @ capacityErrors)
        | Error errors, Ok _
        | Ok _, Error errors -> Error errors

    let event config = config.Event
```
`AppConfig.load` accepts a lookup function instead of reading `Environment` directly. Production passes an environment lookup; the fixed demo and tests pass deterministic functions. This injected lookup avoids global mutation without requiring a configuration framework.

The loader accumulates independent errors for `BOOKING_EVENT_ID` and `BOOKING_CAPACITY`. If both are wrong, an operator can repair both before the next start. Parsing an integer is only the representation step; `Capacity.create` enforces the domain rule that capacity must be positive. The private `AppConfig` record prevents later code from constructing an unvalidated configuration record directly.

Configuration still requires several decisions:

- define source precedence instead of relying on accidental call order;
- fail startup for missing settings required by every request;
- validate ranges, formats, and cross-field rules, not only parsability;
- never print secret values in an error, log, metric tag, or trace tag;
- decide whether a setting is a startup snapshot or can reload;
- test the effective configuration of the published process.

The broader .NET configuration system unifies providers such as JSON, environment variables, command-line arguments, in-memory values, and secret stores behind `IConfiguration`. Use it when provider layering and framework integration are requirements. It does not replace conversion into domain-specific validated types.

## Keep construction in one composition root {#composition-root}

A composition root is the outermost place that selects concrete dependencies and assigns cleanup responsibility. In the sample, the reusable construction function remains intentionally simple:

```fsharp:line-numbers [Composition.fs]
module Composition =
    let start config ports writeLog =
        new BookingApplication(AppConfig.event config, ports, writeLog)
```
`Program` performs the remaining process-specific work: choose the lookup, install demo listeners, construct the in-memory store, create the application, run one command, and translate the result to output and an exit code. Domain modules contain none of those choices.

Manual construction is dependency injection in its literal sense: dependencies arrive as arguments. A DI container automates registration, resolution, scopes, and disposal; it does not create inversion of control. A visible composition root remains valuable even when a container later performs the construction.

Avoid resolving dependencies from a global service locator inside domain or application functions. That hides requirements from signatures, makes lifetimes ambiguous, and forces tests to recreate ambient state. Function arguments make the dependency graph visible.

## Orchestrate effects around the pure decision {#orchestration}

The application method controls sequencing while reusing the existing domain workflow:

```fsharp:line-numbers [Composition.fs]
member _.Place(command: PlaceBookingCommand, cancellationToken: CancellationToken) =
    task {
        ensureActive ()

        let activity =
            activities.StartActivity(DiagnosticNames.PlaceActivityName, ActivityKind.Internal)

        try
            try
                cancellationToken.ThrowIfCancellationRequested()

                match validatePlaceBooking command with
                | Error errors ->
                    let failure = InvalidCommand errors
                    observe activity command "rejected" (sprintf "%A" failure)
                    return Error failure
                | Ok validCommand ->
                    let requestId = ValidPlaceBooking.requestId validCommand
                    let! state = ports.LoadBooking requestId cancellationToken

                    match decidePlaceBooking event state command with
                    | Error failure ->
                        observe activity command "rejected" (sprintf "%A" failure)
                        return Error failure
                    | Ok bookingEvent ->
                        do! ports.AppendEvent requestId bookingEvent cancellationToken
                        observe activity command "accepted" "event-appended"
                        return Ok bookingEvent
            with
            | :? OperationCanceledException as error ->
                observe activity command "canceled" "operation-canceled"
                return raise error
            | error ->
                observe activity command "faulted" (error.GetType().Name)
                return raise error
        finally
            match activity with
            | null -> ()
            | current -> current.Dispose()
    }
```
Read the method in order:

1. Reject use after disposal and start an optional activity.
2. Observe cancellation before invoking a dependency.
3. Validate the raw command so storage receives a typed `RequestId`.
4. Load the current state with the caller's token.
5. Call `decidePlaceBooking` for the domain decision.
6. Append only an accepted domain event, with the same token.
7. Record one terminal outcome and return the domain result.
8. Observe cancellation or an unexpected fault, then rethrow it.
9. Dispose the activity in `finally`, including every exit path.

The standalone domain function validates the command again. This repeats a cheap pure operation, not the rule itself: both calls use `validatePlaceBooking`. The first obtains a typed key before a side effect; the public workflow remains safe when called independently. A later API could accept `ValidPlaceBooking`, but only if that change improves the whole model.

Expected business refusal remains `Error PlaceBookingError`. Cancellation remains `OperationCanceledException`, so .NET callers and hosts recognize it as cancellation. An unexpected adapter fault remains a faulted task. Converting all three into one undifferentiated `Result` would erase operational meaning.

The sample does not retry. A retry policy must know whether the failure is transient and whether append is idempotent. Retrying an ambiguous write without an idempotency key can duplicate an event. Add retries only after defining those semantics.

## Assign each resource a clear lifetime {#lifecycle}

Every disposable object needs one component responsible for it. “It will be collected” is not a lifetime rule: `Dispose` often releases handles, sockets, buffers, subscriptions, or telemetry state that garbage collection does not release promptly.

Here, `Composition.start` receives `BookingPorts`, and the resulting `BookingApplication` becomes responsible for `ports.OwnedResource`. Disposing the application disposes that resource, its `ActivitySource`, and its `Meter` exactly once. `Program` uses `use app = ...`, so both normal completion and exceptions trigger deterministic cleanup.

This lifetime rule is visible, but it is not universal:

| Construction rule | Responsible component | Receiver action |
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

```fsharp:line-numbers [Composition.fs]
module DiagnosticNames =
    [<Literal>]
    let MeterName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let ActivitySourceName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let RequestCounterName = "booking.requests"

    [<Literal>]
    let PlaceActivityName = "booking.place"
```
`Counter<int64>.Add` publishes an increment. It does not by itself create historical storage, a rate chart, retention, or an alert. A collection tool aggregates measurements and may export them to a backend. The demo's `MeterListener` merely observes one in-process measurement so the example and test can prove the instrumentation fired.

The `outcome` tag has four bounded values in this application: `accepted`, `rejected`, `canceled`, and `faulted`. A request ID is deliberately absent. Metric systems usually allocate a time series for each tag combination; unbounded IDs can create excessive memory, storage, and cost.

Use a counter for occurrences that only increase. Use a histogram when a duration or size distribution and its tail matter. Do not encode latency as one average counter pair unless its limitations are acceptable. Define units and descriptions so a collector does not have to guess.

### Traces surround meaningful work {#traces}

`ActivitySource.StartActivity` may return `null` when no interested listener exists. The sample treats that as normal and avoids dereferencing it. When an activity exists, disposing it stops it; `finally` ensures a completed activity for success, rejection, cancellation, and faults.

The activity records low-ambiguity tags and a status. Accepted and rejected decisions completed as designed, so they receive `Ok`; an unexpected fault receives `Error`; cancellation remains distinct. A production convention may refine those choices, but it should be consistent across services.

Creating an `ActivitySource` supplies instrumentation. A collector such as OpenTelemetry then subscribes, samples, enriches, batches, and exports activities. The local `ActivityListener` proves that one activity stopped; production evidence must separately cover cross-process propagation, backend delivery, retention, and useful trace queries.

## Interpret the fixed output narrowly {#fixed-evidence}

Run the deterministic demonstration after the Release build:

```console
dotnet bin/Release/net10.0/Ch32.App.dll --demo
```

It emits exactly:

```text
{"eventName":"booking.place","outcome":"accepted","requestId":"REQ-32","seats":2,"detail":"event-appended"}
result: accepted=true
metric: name=booking.requests value=1 outcome=accepted
trace: name=booking.place outcome=accepted
lifecycle: store-disposed=true
```

The output confirms that one fixed command passed through configuration, composition, the pure decision, the in-memory append, all three local signals, and deterministic disposal. Focused tests also show that independent configuration errors accumulate, the same cancellation token reaches both dependencies, one accepted event is appended, and a pre-canceled token calls neither dependency.

These results cover only in-process wiring. External delivery, durable storage, and concurrent capacity each require integration tests. Because `LoadBooking` and `AppendEvent` are separate operations, two callers can read the same state before either appends. The in-memory adapter therefore demonstrates wiring; a production store must enforce consistency atomically.

## Test application responsibilities without retesting the domain {#boundary-tests}

Application tests should observe responsibilities unique to the application layer:

- malformed independent settings produce all relevant startup errors;
- invalid commands perform no storage effect;
- accepted decisions append exactly one event;
- rejected decisions append none;
- the caller's cancellation token reaches every dependency;
- pre-cancellation avoids calling a dependency and remains cancellation;
- adapter faults remain faults while emitting the chosen terminal signal;
- each attempt emits exactly one terminal metric/log outcome;
- an activity is stopped on every path when a listener samples it;
- the responsible component disposes each resource exactly once.

Keep domain examples and properties in domain tests. Application tests can use deterministic function records and tracking disposables instead of a real database or telemetry vendor. Separate integration tests should verify each production adapter's protocol, serialization, and failure behavior.

Do not make every test assert every signal. One focused contract test can fix the telemetry schema; most orchestration tests should emphasize side effects and results. Otherwise an innocent wording change creates noise across the suite.

## Know when a stronger host is justified {#stronger-host}

An explicit composition root is often enough for a command-line tool, a small single-purpose process, a library-owned worker invoked by another host, or an early application whose dependencies and lifetime fit on one screen.

The .NET Generic Host becomes useful when the process needs several standard facilities together:

- layered configuration providers and environment conventions;
- logging providers, filtering, and scopes;
- DI registration, scopes, and container-owned disposal;
- multiple `IHostedService` or `BackgroundService` workers;
- coordinated startup, shutdown signals, and graceful stopping;
- framework integrations that already expect host services.

For new non-web hosted applications, current .NET guidance recommends `Host.CreateApplicationBuilder`. Web applications normally use `WebApplicationBuilder`, which builds on related hosting facilities. Choosing either does not move domain rules into services or controllers. Preserve the pure workflow, narrow dependencies, typed configuration, and one composition root.

A container is valuable when it manages a real object graph and scopes. Adding it to resolve three obvious values usually increases indirection without solving a problem. Conversely, hand-building dozens of scoped services and shutdown callbacks can recreate a weaker container badly. Let dependency count, lifetime diversity, framework integration, and operational needs decide.

## Review application wiring {#review-checklist}

Before calling a host complete, ask:

- Can the domain run without environment, network, filesystem, clock, or telemetry globals?
- Does each side effect appear as a dependency with domain-relevant inputs and cancellation support?
- Are external strings parsed into validated values before long-lived resources start?
- Is there one visible composition root?
- Is one component responsible for disposing each resource?
- Are business rejection, cancellation, and unexpected fault still distinguishable?
- Do logs preserve fields and exclude secrets?
- Are metric tag combinations bounded and operationally meaningful?
- Can activities be absent without changing behavior?
- Is instrumentation connected to a real collection/export path in production?
- Are concurrency, idempotency, retry, and recovery guarantees stated rather than implied?
- Does a stronger framework remove real lifecycle work rather than merely relocate it?

## Exercises {#exercises}

### Exercise 1: derive dependencies and lifetimes {#exercise-01}

A pure function `decideDispatch : Inventory -> Order -> Result<Dispatch, DispatchError>` is ready to run in a worker. Derive the minimum dependencies for loading inventory and committing a dispatch. State their types, cancellation behavior, expected errors, and who disposes a database session. Do not introduce a container.

### Exercise 2: design three observable signals {#exercise-02}

For the dispatch attempt, define one structured log event, one metric, and one activity. Choose names, fields or tags, and terminal outcomes. Identify which values are bounded, high-cardinality, or sensitive. Explain what a local listener verifies and what still requires a collector/exporter test.

### Exercise 3: choose a hosting level {#exercise-03}

Choose between direct construction and the Generic Host for each case:

- a command that imports one file and exits;
- a process that runs three background consumers and needs graceful shutdown, layered configuration, and logging providers;
- an ASP.NET Core API.

Justify each choice and identify the architectural boundaries that should remain unchanged.

[Read the chapter solutions](../solutions/ch-32-functions-to-applications).

## Model review {#model-review}

- A functional core decides; the application shell obtains facts and performs side effects.
- Ports describe required capabilities and domain-relevant data, not framework objects.
- Configuration remains untrusted until parsing and domain validation succeed.
- Manual construction is dependency injection; a container is optional automation.
- One composition root makes implementations and resource responsibilities visible.
- Cancellation passes unchanged through every cancellable effect.
- Business rejection, cancellation, and fault carry different operational meanings.
- Every disposable has one owner; shutdown must drain work before disposal.
- Logs, metrics, and traces answer different questions.
- Instrumentation produces signals; listeners, collectors, exporters, storage, and alerts are separate concerns.
- Metric dimensions must be bounded; per-request identifiers belong in controlled logs or traces, not metric tags.
- The fixed demo demonstrates wiring, not durability, atomicity, recovery, or backend delivery.
- Real configuration, scope, worker, and shutdown needs justify a stronger host.

## Part V checkpoint {#part-checkpoint}

Run the focused composition tests from the directory containing the example:

```console
dotnet test ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~Ch32CompositionTests
```

Passing tests show that configuration errors accumulate, cancellation is observed before a dependency call, resources are disposed, and the sample emits its structured log, metric, and completed activity. They cover only in-process wiring, not production export or durable delivery.

[Continue to Chapter 33](../part-06/ch-33-domain-language-model), where the capstone is rebuilt as one coherent application path.

## Sources {#sources}

- [Microsoft Learn: .NET Generic Host responsibilities and lifecycle](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Microsoft Learn: configuration providers and `IConfiguration`](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Microsoft Learn: structured logging and message templates](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Microsoft Learn: `Meter`, instruments, tags, and cardinality guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [Microsoft Learn: metric collection, aggregation, and export](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection)
- [Microsoft Learn: `ActivitySource`, nullable activities, tags, and collection](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
- [Microsoft Learn: DI ownership and disposal guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)
