---
title: "Chapter 32 Solutions"
description: "Derive narrow dispatch ports and resource responsibilities, design bounded telemetry, and choose an application host from concrete lifecycle requirements."
translationKey: solutions/ch-32-functions-to-applications
---

# Chapter 32 Solutions {#overview}

These solutions keep domain policy in a pure function and expose process responsibilities at its edge. Names may vary, but four requirements remain: narrow capabilities, defined cancellation and failure semantics, one party responsible for each resource, and telemetry fields with bounded cardinality.

[Return to Chapter 32](../part-05/ch-32-functions-to-applications).

## Exercise 1: derive ports and resource responsibilities {#exercise-01}

### Start from required data and operations {#exercise-01-ports}

Assume the worker receives a validated `Order` and inventory is identified by a validated `Sku`. A minimal first boundary can be:

```fsharp
open System.Threading
open System.Threading.Tasks

type DispatchPorts =
    { LoadInventory: Sku -> CancellationToken -> Task<VersionedInventory>
      CommitDispatch:
        InventoryVersion ->
            Dispatch -> CancellationToken -> Task<Result<unit, CommitError>> }
```

`VersionedInventory` adds a storage version to the domain `Inventory`. That version is not a dispatch rule; it lets the commit reject a stale read. Without it, separate load and commit calls provide no protection against two workers consuming the same stock.

The workflow is:

```fsharp
task {
    cancellationToken.ThrowIfCancellationRequested()
    let sku = Order.sku order
    let! current = ports.LoadInventory sku cancellationToken

    match decideDispatch current.Inventory order with
    | Error domainError -> return Error(DomainRejected domainError)
    | Ok dispatch ->
        let! committed =
            ports.CommitDispatch current.Version dispatch cancellationToken

        return committed |> Result.map (fun () -> dispatch) |> Result.mapError CommitRejected
}
```

The same caller token reaches both I/O calls. Pre-cancellation should prevent the first call. Cancellation from either adapter remains cancellation, while an unexpected database exception leaves the task faulted. `DispatchError` is an expected business refusal. `CommitError`, such as `VersionConflict`, comes from persistence or concurrency and should not be disguised as a domain rule.

This model does not automatically retry a version conflict. The caller may reload and decide again only if the operation has a defined retry limit, the order identity is stable, and commit is idempotent. Reusing the earlier domain result after inventory changes would be incorrect.

Assign cleanup separately for the long-lived client and each per-operation session:

- the composition root creates the database client or connection pool, and the process becomes responsible for it;
- at shutdown, the process stops new work, drains outstanding calls, and then disposes the client;
- the adapter creates a session or transaction for one operation and disposes it with `use` or `use!` inside that operation;
- the pure function never sees either resource;
- if a caller supplies a shared client and retains disposal responsibility, the application must not dispose it.

If load and commit must share one database transaction, the shown two-call port is insufficient. Redesign the adapter to create one transaction, run the load and pure decision, conditionally commit, and dispose the transaction. Alternatively, use the store's compare-and-swap operation. Two adjacent calls are not automatically atomic.

A container is unnecessary to express these rules. Constructor or function arguments expose dependencies, and `use` marks a local cleanup scope. A container may later automate long-lived registrations and scopes without changing the domain workflow.

## Exercise 2: design three telemetry signals {#exercise-02}

### Give each signal one job {#exercise-02-signals}

One coherent design is:

| Signal | Name | Fields/tags | Terminal outcomes |
|---|---|---|---|
| Structured log | `dispatch.attempt` | `outcome`, `orderId`, `sku`, `quantity`, `detail` | accepted, rejected, conflicted, canceled, faulted |
| Counter | `dispatch.attempts` | `outcome` and optionally bounded `channel` | the same bounded outcome vocabulary |
| Activity | `dispatch.place` | `dispatch.outcome`, `order.id`, `inventory.sku`; status and exception metadata by policy | stopped on every sampled path |

`outcome` is bounded because the application defines five legal values. A small enumerated channel such as `web`, `batch`, or `manual` can also be bounded if its meaning is stable. `orderId` and usually `sku` are high-cardinality; they must never become metric tags. They may appear in logs or traces when access, retention, sampling, and privacy policy permit.

Customer name, address, free-form notes, authentication tokens, connection strings, and raw payloads should not be emitted. Even an order ID can be sensitive when another system can resolve it to a person. Use redaction or a non-reversible correlation value when policy requires it.

The structured event should preserve typed fields instead of interpolating one sentence. With `ILogger`, use a stable message template and event ID so providers can retain those properties. Choose a level based on operational action: a normal stock rejection may be informational, while an unexpected adapter exception is an error.

Increment the counter exactly once after each terminal attempt. A counter reports occurrences; the collector derives totals or rates. If duration matters, add a histogram with a declared time unit rather than encoding an average into the counter. Alerts belong to collection/backend configuration, not the domain function.

Start the activity around application orchestration and dispose it in `finally`. Treat a `null` activity as normal. Put the bounded outcome on the activity and use status consistently: an expected refusal can complete successfully at the protocol level, while an unexpected exception is an error. Record cancellation separately rather than rewriting it as a fault.

A local `MeterListener` verifies that the process published the expected named measurement, value, and tags. A local `ActivityListener` verifies that a sampled activity started, received tags, and stopped. Capturing the logging callback verifies that the structured record was produced.

Those listeners do not verify aggregation, sampling policy, propagation headers, batching, export, authentication, backend ingestion, retention, dashboards, or alerts. Test the real OpenTelemetry or provider pipeline in integration or staging. Add a backend query or health signal when its operational importance warrants one.

## Exercise 3: choose a hosting level {#exercise-03}

### Let lifecycle requirements choose the tool {#exercise-03-hosts}

For the one-file import command, use explicit construction. It has one bounded operation, a natural `use` scope, simple argument/configuration parsing, and an exit code. Adding a service container and hosted-service lifecycle would not remove meaningful complexity. Cancellation can come from a console signal token if interruption is required.

For the process with three background consumers, use the Generic Host. It already coordinates hosted services, logging providers, layered configuration, DI scopes, shutdown signals, and graceful stopping. Current guidance favors `Host.CreateApplicationBuilder` for a new non-web host. Each consumer should honor the provided stopping token, stop accepting work, and obey a bounded drain policy.

For the ASP.NET Core API, use `WebApplicationBuilder` and the ASP.NET Core host. HTTP server lifecycle, request scopes, configuration, logging, middleware, endpoint activation, and graceful shutdown are framework concerns. Pass `HttpContext.RequestAborted` or the endpoint cancellation token through application ports.

The following boundaries remain unchanged in all three cases:

- `decideDispatch` remains pure and unaware of the host;
- external input is converted to validated commands and configuration at the edge;
- storage, clock, messaging, and telemetry dependencies remain explicit adapters or application services;
- expected business rejection remains distinguishable from cancellation and fault;
- one composition root selects implementations and lifetimes;
- resource ownership and shutdown order are documented;
- metric dimensions remain bounded and sensitive fields follow policy;
- adapter integration and concurrency guarantees receive separate tests.

The host changes how outer resources are constructed and governed. It should not change the meaning of a dispatch decision. If moving to a framework requires domain modules to resolve services or read ambient configuration, the boundary has moved in the wrong direction.

## Solution review {#solution-review}

- Ports follow the facts a pure decision needs and the commitments its result requires.
- A versioned or transactional commit is needed when concurrent writers must not oversell.
- Domain refusal, commit conflict, cancellation, and unexpected fault remain distinct.
- The same cancellation token reaches each operation unless a documented cleanup policy requires otherwise.
- Long-lived clients and per-operation sessions can have different owners.
- A local listener proves instrumentation, not delivery to an observability backend.
- Metric labels use a small bounded vocabulary; request identifiers do not belong there.
- Logs and traces may carry identifiers only under explicit privacy and retention policy.
- Explicit construction fits bounded processes; Generic Host fits multi-service lifecycle coordination.
- Web hosting supplies HTTP concerns while the functional core stays host-independent.
