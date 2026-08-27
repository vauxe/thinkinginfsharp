---
title: "Chapter 33: Business Language, Commands, Events, and Model"
description: "Consolidate the booking system's domain language, distinguish intent from fact, state, and boundary data, and use events without assuming event sourcing."
translationKey: part-06/ch-33-domain-language-model
---

# Chapter 33: Business Language, Commands, Events, and Model {#overview}

The booking system began with values and functions, not an architecture diagram. Protected domain types, a workflow, side-effect dependencies, and a stable public module followed. Part VI assembles those slices into one application. This chapter first defines the vocabulary of each layer.

Vocabulary is part of the design. `PlaceBooking` requests work; `BookingPlaced` records a fact; `BookingState` represents the current domain view; a future JSON request represents external data. Giving all four the same record type would save a few declarations but obscure when each value is valid and who may use it.

## Begin with one glossary {#glossary}

The following words have precise local meanings in this project:

| Word | Meaning here | Typical name | Can it fail merely by existing? |
|---|---|---|---|
| Command | A request that the system attempt one business action | `PlaceBooking` | Yes; raw boundary fields may be invalid, and valid intent may still be refused |
| Validated command | Intent whose independent field validation has succeeded | `ValidPlaceBooking` | Its fields are valid, but current state may still refuse it |
| Domain event | An immutable description of a business fact that the domain accepted | `BookingPlaced` | No; it describes an outcome, not a request |
| State | The domain's present view used to judge the next command | `NotBooked`, `Booked booking` | No; publicly obtainable state should satisfy its invariants |
| Boundary DTO | Data organized for JSON, storage, another language, or another process | `PlaceBookingRequestDto` | Yes; it is untrusted until mapped and validated |
| Port | A capability the application requires from its environment | `LoadBooking`, `AppendEvent` | Calls may reject, cancel, or fault according to their contract |

“Event” has two meanings in the sample. The type `Event` is the scheduled activity whose seats can be booked. The type `BookingEvent` is a fact about a booking. In prose, this chapter calls the former an **activity** when ambiguity is possible. Neither meaning is the same as a .NET delegate event.

The table describes roles, not mandatory suffixes. A small internal type can be clear without `Command` in its name. The important test is whether readers can tell if the value asks, states, remembers, or crosses a boundary.

## See how the model grew {#model-evolution}

The earlier teaching slices were successive refinements, not six competing architectures:

1. Part I used tuples, lists, expressions, and folds to discover the seat-allocation behavior.
2. Part II replaced loose primitives and Boolean combinations with records, single-case unions, and discriminated unions.
3. Part III separated validation, decision, and evolution into modules with an explicit compilation order.
4. Part IV surrounded the pure workflow with asynchronous ports, cancellation, and owned resources.
5. Part V tested invariants and projected a stable F#-facing public module without leaking workflow types.
6. This part consolidates that language, then connects it to contracts, storage, adapters, and HTTP.

Preserving every historical type would create two sources of truth. The capstone instead migrates callers toward one model and retains only small compatibility aliases where an earlier chapter still compiles against an old name.

## Model the business before the transport {#domain-model}

The core model names the activity, booking lifecycle, and failures in domain terms:

```fsharp:line-numbers [Domain.fs]
type Event =
    private
        { Id: EventId
          Capacity: Capacity }

module Event =
    let create eventId capacity = { Id = eventId; Capacity = capacity }

    let id event = event.Id
    let capacity event = event.Capacity

type BookingStatus =
    | Pending
    | Confirmed of ConfirmationCode
    | Cancelled of CancellationReason

type BookingCreationError = RequestedSeatsExceedCapacity of requested: int<seat> * capacity: int<seat>

type BookingTransitionError =
    | CannotConfirmFrom of current: BookingStatus
    | CannotCancelFrom of current: BookingStatus

type Booking =
    private
        { RequestId: RequestId
          EventId: EventId
          Seats: SeatCount
          Status: BookingStatus }

module Booking =
    let create event requestId seats =
        let requested = SeatCount.value seats
        let capacity = event |> Event.capacity |> Capacity.value

        if requested > capacity then
            Error(RequestedSeatsExceedCapacity(requested, capacity))
        else
            Ok
                { RequestId = requestId
                  EventId = Event.id event
                  Seats = seats
                  Status = Pending }

    let requestId booking = booking.RequestId
    let eventId booking = booking.EventId
    let seats booking = booking.Seats
    let status booking = booking.Status

    let restore requestId eventId seats status =
        { RequestId = requestId
          EventId = eventId
          Seats = seats
          Status = status }

    let confirm confirmationCode booking =
        match booking.Status with
        | Pending ->
            Ok
                { booking with
                    Status = Confirmed confirmationCode }
        | current -> Error(CannotConfirmFrom current)

    let cancel reason booking =
        match booking.Status with
        | Pending
        | Confirmed _ ->
            Ok
                { booking with
                    Status = Cancelled reason }
        | Cancelled _ as current -> Error(CannotCancelFrom current)
```
Several F# choices work together here:

- records group named values such as request, activity, seats, and status;
- discriminated unions make lifecycle alternatives and error alternatives explicit;
- values are immutable by default, so a transition returns a new `Booking`;
- single-case unions distinguish identifiers, counts, codes, and reasons that share primitive representations;
- units of measure prevent accidental arithmetic between seats and unrelated integers inside the model;
- private record representations prevent callers from constructing a `Booking` that skipped its rules;
- module functions form the supported construction, observation, and transition surface.

A type alone does not enforce every invariant. `BookingStatus` can express three legal states, but only `Booking.confirm` and `Booking.cancel` define which transitions are allowed. `Booking.create` compares requested seats with the activity capacity. Protection comes from the combination of representation, access control, and the small functions that may create new values.

The model deliberately contains no JSON property names, database paths, HTTP status codes, logging levels, or dependency-injection services. Those concepts can change without changing what a booking means.

## Commands describe intent {#commands}

The canonical command vocabulary is small:

```fsharp:line-numbers [Commands.fs]
type PlaceBooking = { RequestId: string; Seats: int }

type ConfirmBooking =
    { RequestId: string
      ConfirmationCode: string }

type CancelBooking = { RequestId: string; Reason: string }

[<RequireQualifiedAccess>]
type BookingCommand =
    | Place of PlaceBooking
    | Confirm of ConfirmBooking
    | Cancel of CancelBooking

module Commands =
    let place requestId seats : PlaceBooking =
        { RequestId = requestId; Seats = seats }

    let confirm requestId confirmationCode : ConfirmBooking =
        { RequestId = requestId
          ConfirmationCode = confirmationCode }

    let cancel requestId reason : CancelBooking =
        { RequestId = requestId
          Reason = reason }
```
`Place`, `Confirm`, and `Cancel` are imperative names because a command asks the system to attempt something. The caller cannot truthfully name the input `BookingPlaced`: capacity, duplicate request identity, current status, or malformed text may prevent that fact.

These command records intentionally contain boundary-friendly primitives. Constructing `({ RequestId = " "; Seats = 0 } : PlaceBooking)` is possible, so construction does **not** mean acceptance. Validation converts independently valid fields into protected values such as `RequestId` and `SeatCount`; the decision then applies rules that depend on current state.

This yields three distinct questions:

| Stage | Question | Example failure |
|---|---|---|
| Parse/map | Can external representation become the command's primitive fields? | JSON value has the wrong numeric form |
| Validate | Are the fields meaningful by themselves? | Blank request ID or non-positive seats |
| Decide | Is this valid intent allowed in the present state? | Booking already exists or capacity is too small |

Separating these questions lets independent validation errors accumulate while state-dependent rules stop at the first refusal. Chapter 34 connects every command to one pure decider; this chapter defines its input language.

`[<RequireQualifiedAccess>]` makes call sites write `BookingCommand.Place`, `BookingCommand.Confirm`, or `BookingCommand.Cancel`. Qualification prevents generic case names from becoming ambiguous when the domain grows.

## Events describe accepted facts {#events}

The corresponding event vocabulary uses the past tense:

```fsharp:line-numbers [Events.fs]
type BookingEvent =
    | BookingPlaced of Booking
    | BookingConfirmed of Booking
    | BookingCancelled of Booking

module BookingEvent =
    let booking event =
        match event with
        | BookingPlaced booking
        | BookingConfirmed booking
        | BookingCancelled booking -> booking

    let requestId event = event |> booking |> Booking.requestId
```
`BookingPlaced`, `BookingConfirmed`, and `BookingCancelled` state what the domain accepted. In this model each event carries the resulting protected `Booking`, so evolution can project the new state without repeating transition rules. This is a simple in-process fact representation, not yet a promise about a durable wire schema.

An event should not contain an operation that still needs approval. A handler may fail to perform a later side effect—sending a notification, for example—but that failure does not make the already accepted booking fact grammatically become a command again. Application policy decides how to retry or compensate for the side effect.

The union cases are public, while the `Booking` representation is private. Code holding a valid booking can therefore wrap it in an event case. Do not overstate the guarantee: the current API protects booking construction and transitions, but it does not cryptographically prove event provenance. Chapter 34 narrows normal event production through the decider.

Domain events also do not imply .NET events or a message broker. A pure function can return a `BookingEvent` as ordinary data. The application may fold it into state, persist it, publish a mapped integration message, or do more than one of those according to explicit consistency rules.

## State is the present decision context {#state}

The workflow needs only two top-level state cases:

```fsharp:line-numbers [Workflow.fs]
type BookingState =
    | NotBooked
    | Booked of Booking
```
`NotBooked` means no booking exists for the request being considered. `Booked booking` carries the protected booking whose own status is pending, confirmed, or cancelled. This nesting avoids invalid combinations such as “not booked and confirmed.”

Evolution is intentionally mechanical:

```fsharp:line-numbers [Workflow.fs]
let evolve (_: BookingState) (event: BookingEvent) =
    match event with
    | BookingPlaced booking
    | BookingConfirmed booking
    | BookingCancelled booking -> Booked booking
```
`evolve` answers “what state follows this accepted fact?” It does not answer “may this fact happen?” The decider and domain transition functions own that policy. If `evolve` rechecked capacity or status, the rule could drift into two implementations.

The current event carries the complete resulting `Booking`, so `evolve` does not need its previous-state argument. Keeping the conventional `state -> event -> state` signature makes folding explicit and leaves room for a later event that represents a delta. Do not infer that an unused argument proves history is irrelevant to deciding the event.

The pure conceptual path is:

```text
raw DTO or caller input
  -> command
  -> field validation
  -> validated command + current state
  -> decide
  -> accepted event or explicit error
  -> evolve
  -> next state
  -> boundary projection or committed effect
```

Only the last step needs an external effect. Everything from validation through evolution can be deterministic and tested without a database, clock, or network.

## Do not confuse state with a DTO {#dto-boundary}

A domain value and a DTO can momentarily contain the same information and still have different contracts:

| Concern | Domain value | Boundary DTO |
|---|---|---|
| Primary audience | Domain functions and F# callers | Serializer, database adapter, C# caller, or remote client |
| Validity | Constructed through protected rules | May contain missing, blank, default, unknown, or obsolete fields |
| Representation changes when | Business meaning changes | Wire/storage compatibility changes |
| F# features | Private records, DUs, options, units of measure | Explicit primitive fields and a deliberately versioned representation |
| Failure | Domain error or impossible construction | Parse, schema, mapping, and compatibility errors |

Serializing `Booking`, `BookingStatus`, or `BookingEvent` directly would make their compiler-oriented representation a public storage or network contract. Renaming a union case, changing its payload, or reorganizing private fields could then become a migration. Chapter 35 will introduce explicit DTOs and total-or-explicitly-failing mappings instead.

A DTO is not “bad domain modeling.” It is an anti-corruption boundary whose job is to admit external representation rules without weakening the domain. Keep it simple, document its schema, and validate before passing protected values inward.

## Offer a stable public path {#public-surface}

The capstone's intended F#-facing entry point begins with raw boundary values but returns an opaque model:

```fsharp:line-numbers [PublicApi.fs]
let start rawEventId rawCapacity =
    let eventIdResult =
        EventId.create rawEventId
        |> Result.mapError (fun _ -> [ BookingError.BlankEventId ])

    let capacityResult =
        Capacity.create rawCapacity
        |> Result.mapError (fun (NonPositiveCapacity actual) -> [ BookingError.NonPositiveCapacity actual ])

    match eventIdResult, capacityResult with
    | Ok validEventId, Ok validCapacity -> BookingModel(Event.create validEventId validCapacity, NotBooked) |> Ok
    | Error eventErrors, Error capacityErrors -> Error(eventErrors @ capacityErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```
The public module gives consumers four focused concepts:

| Role | Public names | Consumer action |
|---|---|---|
| Opaque state | `PublicApi.BookingModel` | obtain it from `start` and pass it to transitions |
| Commands | `place`, `confirm`, `cancel` | request a domain transition |
| Observation | `BookingView` and observation functions | read a projected view |
| Failure | `BookingError` | match on the smaller public error vocabulary |

The internal workflow continues to use `Event`, `Booking`, `BookingState`, `BookingEvent`, `RequestId`, and `SeatCount`. Those names stay out of consumer signatures.

The module establishes a stable consumer path while leaving other assembly types accessible. A library that later needs assembly-wide restriction can add a signature file or move implementation types into a separate internal assembly.

The stable API and the boundary DTO solve different problems. The former protects F# source dependencies inside one library ecosystem; the latter fixes a serialization or cross-language contract. As Chapter 27 showed, a C#-first API often expresses its public surface with classes, enums, members, nullable annotations, and exceptions.

## Migrate names without duplicating models {#compatibility-aliases}

Earlier slices used `Validation.PlaceBookingCommand` and `Workflow.BookingEvent`. The consolidated code retains these declarations only as aliases:

```fsharp
// Compatibility names; no second runtime representation is created.
type PlaceBookingCommand = PlaceBooking
type BookingEvent = Booking.Domain.BookingEvent
```

A type alias gives old source code another name for the same type. It has no distinct constructor, serialized representation, equality semantics, or runtime identity. That makes it suitable for a staged teaching migration.

An alias is not a permanent excuse for two vocabularies. New code uses `PlaceBooking` and the namespace-level `BookingEvent`; old examples move when their chapter is revised. If two names begin to acquire different rules, define two honest concepts and an explicit mapping rather than letting aliases conceal disagreement.

## Events do not require event sourcing {#events-not-event-sourcing}

Returning a domain event commits the code to a vocabulary of facts. It does **not** commit the system to storing every fact forever or reconstructing state from them.

| Design | Source of truth | How current state is obtained | Additional obligations |
|---|---|---|---|
| Current-state persistence | Latest booking DTO or database row | Read the saved current representation | Atomic updates, concurrency checks, schema migration, recovery |
| Domain events with current-state persistence | Latest state; events may trigger in-process work or integration | Read current state; handle selected facts | Dispatch timing, side-effect consistency, duplicate handling where delivery can repeat |
| Event sourcing | Append-only ordered event stream | Replay events, often aided by snapshots or projections | Stream concurrency, event schema evolution, replay determinism, projection rebuilds, idempotency, retention, privacy, and operational tooling |

Event sourcing is a storage architecture: each entity's ordered event stream is the authoritative history, and current state is derived by replay. CQRS is another independent choice that separates write commands from read queries. They are often combined, but neither follows automatically from defining an F# union named `BookingEvent`.

The capstone currently proves only a pure fact vocabulary and an evolution function. Its earlier in-memory adapter demonstrates wiring, not a durable event store. Later chapters may persist a current DTO, append selected facts, or map facts to integration messages without turning the event list into the sole source of truth.

Choose event sourcing only when access to history, temporal decisions, audit needs, or projection flexibility justify its migration and operational costs. “We already have events” is not sufficient evidence.

## Name types from time and authority {#naming}

Use tense and responsibility as a quick review tool:

- command names are imperative business actions: `PlaceBooking`, not `SetStatus`;
- event names are past-tense facts: `BookingConfirmed`, not `ConfirmBookingEvent` if that sounds pending;
- state names describe what is presently true: `NotBooked`, `Booked`;
- error names state why an attempted transition failed: `CannotConfirmFrom`;
- DTO names identify the boundary and direction when ambiguity matters: `PlaceBookingRequestDto`;
- port names describe capabilities, not chosen products: `LoadBooking`, not `ReadPostgresRow`.

Names should use the language of the people defining the rule. Technical precision still matters: if business participants use “event” for the scheduled activity, qualify the fact as `BookingEvent` rather than silently changing their word.

Avoid generic containers such as `Request`, `Response`, `Data`, or `StatusChanged` at a domain-wide scope. They force readers to recover context from folders or comments. Also avoid encoding implementation promises into domain names: `BookingSavedToJson` is an adapter outcome, not a booking fact.

## Interpret the domain-model checks narrowly {#evidence}

The consolidated implementation and focused tests show that:

- all three commands express intent and all three events express accepted facts;
- booking construction and transitions still pass through the existing protected domain functions;
- `PublicApi` does not expose internal domain or workflow types in its function signatures;
- the old command and event names are aliases rather than second runtime models;
- adding new event cases made an older pattern match fail exhaustiveness checking until it was updated;
- domain, workflow, and property tests still pass under F# 10 with null checking and warnings as errors.

These checks do not cover one decider for all commands, durable JSON compatibility, atomic persistence, idempotency, HTTP behavior, or restart recovery. Later chapters implement those capabilities explicitly; clean type names do not imply them.

## Review a domain language {#review-checklist}

Before extending the model, ask:

- Can a domain expert recognize the actions, facts, states, and refusals?
- Does each command describe one attempted business action rather than a field update?
- Can raw command fields be distinguished from validated values?
- Does each event state something that has already been accepted?
- Does `decide` own permission while `evolve` owns projection?
- Are publicly constructible values guaranteed valid, or clearly marked as boundary input?
- Are domain representations protected without hiding useful observation functions?
- Does a DTO change for compatibility reasons independently of the domain type?
- Are compatibility aliases temporary and directionally migrating callers?
- Has any mention of events accidentally implied a broker, CQRS, or event sourcing?
- Are unproved concurrency, durability, retry, and recovery guarantees stated as missing?

## Exercises {#exercises}

### Exercise 1: classify values by role {#exercise-01}

Classify these values:

- `PlaceBooking`
- `ValidPlaceBooking`
- `BookingPlaced`
- `Booked booking`
- `PlaceBookingRequestDto`
- `AppendEvent`
- `RequestedSeatsExceedCapacity`

For each value, record its role—command, validated command, domain event, state, boundary DTO, port, or domain error—who may create it, and whether construction means the requested booking happened.

### Exercise 2: extend the language before the code {#exercise-02}

The business asks to change the seat count of a pending booking. Propose a command, an accepted event, state-dependent errors, and the protected domain transition. State what must be validated independently, what requires the activity capacity and current booking status, and what `evolve` should do. Do not add JSON or storage fields yet.

### Exercise 3: decide whether history is the truth {#exercise-03}

Compare current-state persistence with event sourcing for this booking system. It must prevent overselling, recover after restart, return current booking status quickly, and retain a 90-day audit trail. Identify what both designs need, what event sourcing adds, and which one these facts support. List the new evidence that would make you reconsider.

[Read the chapter solutions](../solutions/ch-33-domain-language-model).

## Model review {#model-review}

- The domain language precedes controllers, tables, serializers, and hosts.
- A command asks; a validated command has sound fields; a decision may still refuse it.
- An event states an accepted fact and uses past-tense business language.
- State is the present context for the next decision, not automatically its storage format.
- A DTO belongs to a compatibility boundary and remains untrusted until mapped.
- Records express named products; discriminated unions express closed alternatives.
- Private representations plus smart constructors and transition functions protect invariants.
- `decide` owns whether a fact may occur; `evolve` projects an accepted fact into state.
- A stable public module can hide workflow representation without becoming a wire contract.
- Type aliases help migration but must not preserve two permanent vocabularies.
- Domain events can be ordinary returned data; brokers and handlers are optional application choices.
- Event sourcing and CQRS are separate architectural commitments, not consequences of an event union.
- Current checks cover vocabulary and encapsulation, not persistence or concurrency guarantees.

## Sources {#sources}

- [Microsoft Learn: F# records, immutability, construction, and access modifiers](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn: F# discriminated unions and named cases](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: F# access control](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn: domain events as facts and explicit domain side effects](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Azure Architecture Center: CQRS commands, reads, and independent complexity](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Azure Architecture Center: event sourcing, replay, projections, and trade-offs](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
