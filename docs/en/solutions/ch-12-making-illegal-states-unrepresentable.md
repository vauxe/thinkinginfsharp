---
title: "Chapter 12 Solutions"
description: "Protect a bounded value, choose an outer-record boundary, and correct a cross-file capacity API whose types blur capacity with availability."
translationKey: solutions/ch-12-making-illegal-states-unrepresentable
---

# Chapter 12 Solutions {#overview}

The protected representation is only as strong as its public producers. Check every function that returns the protected type, not just the function named `create`.

[Return to Chapter 12](../part-02/ch-12-making-illegal-states-unrepresentable).

## Exercise 1: protect a percentage {#exercise-01}

A private single-case union distinguishes validated rates from arbitrary decimals:

```fsharp
type FillRateError =
    | OutsideUnitInterval of actual: decimal

type FillRate = private FillRate of decimal

module FillRate =
    let create raw =
        if raw >= 0m && raw <= 1m then
            Ok(FillRate raw)
        else
            Error(OutsideUnitInterval raw)

    let value (FillRate rate) = rate
```

Both endpoints are accepted because the stated interval is closed. `create -0.1m` and `create 1.01m` retain their rejected values in `Error`; no exception is needed for expected input rejection.

With `type FillRate = decimal`, a caller can annotate `2m` as `FillRate` without invoking the validator. With the private union, ordinary outside code has no expression that constructs `FillRate` except the functions the module exposes. The accessor returns a decimal for calculation or serialization but does not weaken construction.

If arithmetic returns a new fill rate, it must call `create` or be implemented inside the trusted module with a proof that the range is preserved. Multiplying a valid rate by `2m` is not automatically valid.

## Exercise 2: choose a transparent or private outer record {#exercise-02}

Favor the public record when the complete rule is simply “contains one valid `EventId` and one valid `SeatCount`.” Both components already carry their proofs, all field combinations are legal, and consumers benefit from record construction, copy-and-update, and pattern matching.

Favor the private record when there is a cross-field rule, a derived field that must stay synchronized, normalization that must occur as a unit, or a likely representation change that should not break consumers. For example, a group-booking policy may require a contact address whenever `SeatCount` exceeds a threshold.

If no additional validation can fail, the minimal private API is:

```fsharp
BookingRequest.create : EventId -> SeatCount -> BookingRequest
BookingRequest.eventId : BookingRequest -> EventId
BookingRequest.seats : BookingRequest -> SeatCount
```

Returning plain `BookingRequest` from `create` is honest because both arguments are already protected and there is no new rejection rule. If construction checks a cross-field rule, change it to `Result<BookingRequest, BookingRequestError>`. If callers need one common transformation, expose that operation instead of leaking the record and making them reimplement policy.

Private is not automatically safer: an opaque type with missing observations forces awkward workarounds, while an opaque type whose constructor performs no checks merely adds ceremony.

## Exercise 3: expose a cross-file capacity API {#exercise-03}

The proposed signature reveals a modeling error:

```fsharp
tryReserve : SeatCount -> Capacity -> Result<Capacity, ReservationError>
```

The chapter's `Capacity` is a positive, fixed event capacity. Reservation does not change that fact. If the returned value really means remaining seats, an exact fill would produce zero and violate the type's positive invariant. Calling it `Capacity` has merged two concepts.

Model availability separately and permit zero:

```fsharp
namespace Booking.Domain

[<Measure>]
type seat

type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity

module Capacity =
    val create: raw: int -> Result<Capacity, CapacityError>
    val value: Capacity -> int<seat>

type SeatCountError =
    | NonPositiveSeatCount of actual: int

type SeatCount

module SeatCount =
    val create: raw: int -> Result<SeatCount, SeatCountError>
    val value: SeatCount -> int<seat>

type AvailableSeats

type ReservationError =
    | InsufficientSeats of requested: int<seat> * available: int<seat>

module AvailableSeats =
    val fromCapacity: Capacity -> AvailableSeats
    val value: AvailableSeats -> int<seat>
    val tryReserve:
        requested: SeatCount ->
        available: AvailableSeats ->
        Result<AvailableSeats, ReservationError>
```

`AvailableSeats` has the invariant “zero or positive,” while `Capacity` remains positive and unchanged. Exact fill returns a valid zero `AvailableSeats`; a request above availability returns `InsufficientSeats`. Another valid design uses a union such as `SoldOut | SeatsRemain of PositiveSeatCount` so zero is represented by a named case.

Place this public surface in `Capacity.fsi`, then place `Capacity.fs` immediately after it in project order, followed by consumer files. The `.fs` implementation may pattern-match and construct the hidden union cases. Later files see only the abstract types and listed values. Helpers omitted from the signature remain implementation-private.

If the original signature were retained, the implementation would have to reject exact fill, lie by returning zero as positive `Capacity`, or return unchanged capacity without representing reservation state. The type review has correctly exposed that all three choices are wrong.

## What to notice {#what-to-notice}

- **A closed interval needs both boundary tests:** the smart constructor states them once.
- **Protected components may make an outer record safe enough:** opacity must answer a remaining requirement.
- **A non-failing constructor should not return decorative `Result`:** its return type should match real alternatives.
- **Capacity and availability have different invariants:** similar numeric representation does not make them one domain type.
- **An abstract `.fsi` type hides every case from later files:** the implementation remains the single trusted construction scope.
