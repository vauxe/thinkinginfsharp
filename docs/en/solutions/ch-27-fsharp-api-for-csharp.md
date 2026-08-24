---
title: "Chapter 27 Solutions"
description: "Project a leaking F# result into a controlled .NET response, evolve a query with overloads, and isolate serializer requirements in a dedicated DTO."
translationKey: solutions/ch-27-fsharp-api-for-csharp
kind: solution
part: 5
chapter: 27
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch27-fsharp-api
  - ch27-csharp-client
exerciseIds:
  - ch27-exercise-01
  - ch27-exercise-02
  - ch27-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-component-guidelines
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-24"
  - id: dotnet-breaking-changes
    url: https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes
    checked: "2026-08-24"
  - id: fsharp-climutable
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html
    checked: "2026-08-24"
---

# Chapter 27 Solutions {#overview}

All three solutions preserve one principle: the domain representation does not compromise for calling technology, and the boundary representation does not require callers to learn the implementation language. The adapter explicitly owns conversion and compatibility costs between them.

[Return to Chapter 27](../part-05/ch-27-fsharp-api-for-csharp).

## Exercise 1: contain a leaking F# representation {#exercise-01}

### Write the visible surface and state laws first {#exercise-01-surface}

The internal function's three outcomes can be projected as follows:

| Internal outcome | Public response | Required law |
|---|---|---|
| `Ok(code, remaining)` | `Accepted`, non-null confirmation code, present `RemainingSeats` | Error and suggestion are absent |
| `Error(message, Some seats)` | `Rejected`, non-null error, present `SuggestedSeats` | Confirmation and remaining seats are absent |
| `Error(message, None)` | `Rejected`, non-null error, absent suggestion | Confirmation and remaining seats are absent |

The public types can be `BookingRequest`, `BookingResponse`, `BookingOutcome`, and `BookingApi`. The response constructor must be non-public; otherwise C# can combine any enum, null, and number, recreating states the internal union excluded. The request can have a public constructor, but the constructor and entry point must each guard their own contracts.

The core still returns a union; the boundary only projects it:

<<< @/../examples/chapters/ch27/FSharpApi/Library.fs#boundary-adapter{fsharp:line-numbers} [Library.fs]

The public entry point validates cross-boundary arguments, then calls that same core:

<<< @/../examples/chapters/ch27/FSharpApi/Library.fs#public-api{fsharp:line-numbers} [Library.fs]

You may separately expose an idiomatic surface for F# callers, perhaps returning an abstract domain result. Do not make that convenience layer the source of the C# contract. Both surfaces should forward to the same internal function.

## Exercise 2: add optional filtering without breaking callers {#exercise-02}

### Delegate two overloads to one implementation {#exercise-02-overloads}

The internal implementation can still use `option`; public members need not:

```fsharp
open System
open System.Collections.Generic

[<AbstractClass; Sealed>]
type BookingSearch private () =
    static let validate name (value: string) =
        ArgumentNullException.ThrowIfNull(value, name)

        if String.IsNullOrWhiteSpace value then
            raise (ArgumentException("Value must not be blank.", name))

        value

    static let find requestId attendee =
        match attendee with
        | None -> [| requestId |] :> IReadOnlyList<string>
        | Some name -> [| $"{requestId}:{name}" |] :> IReadOnlyList<string>

    static member Find(requestId: string) : IReadOnlyList<string> =
        find (validate (nameof requestId) requestId) None

    static member Find(requestId: string, attendee: string) : IReadOnlyList<string> =
        let validRequestId = validate (nameof requestId) requestId
        let validAttendee = validate (nameof attendee) attendee
        find validRequestId (Some validAttendee)

assert (BookingSearch.Find("REQ-27") |> Seq.toList = [ "REQ-27" ])
assert (BookingSearch.Find("REQ-27", "Ada") |> Seq.toList = [ "REQ-27:Ada" ])
```

C# calls remain ordinary:

```csharp
var all = BookingSearch.Find(requestId: "REQ-27");
var filtered = BookingSearch.Find(requestId: "REQ-27", attendee: "Ada");
```

`requestId` and `attendee` now appear in C# named arguments, so renaming them later breaks recompiling caller source. Adding the second-parameter overload preserves the first method's binary signature; directly changing the original method to two parameters would break old binaries.

### Let option growth trigger one deliberate migration {#exercise-02-evolution}

A third independent filter need not immediately produce four overloads. If the filters form one concept, add `BookingSearchOptions` and `Find(BookingSearchOptions options)`, keep the old overloads, and forward from them. Document defaults and combination rules, then use `Obsolete` to supply a migration target rather than suddenly removing bridge members.

Even an additive overload warrants compiling existing C# source: method groups, generic inference, and null arguments can become newly ambiguous. An API baseline tool checks the binary surface; real consumer compilation checks the source surface.

## Exercise 3: separate a JSON DTO from the domain request {#exercise-03}

### Permit an incomplete DTO, then decode explicitly {#exercise-03-dto}

The `CLIMutable` DTO honestly admits that a value produced by default construction is not validated yet. A private domain record can be obtained only through the conversion function:

```fsharp
open System

[<CLIMutable>]
type BookingRequestDto =
    { RequestId: string | null
      Attendee: string | null
      Seats: int }

type DtoError =
    | MissingBody
    | MissingRequestId
    | MissingAttendee
    | InvalidSeats of int

type DomainRequest =
    private
        { RequestId: string
          Attendee: string
          Seats: int }

module DomainRequest =
    let ofDto (dto: BookingRequestDto | null) =
        match dto with
        | null -> Error MissingBody
        | value ->
            match value.RequestId with
            | null -> Error MissingRequestId
            | requestId when String.IsNullOrWhiteSpace requestId ->
                Error MissingRequestId
            | requestId ->
                match value.Attendee with
                | null -> Error MissingAttendee
                | attendee when String.IsNullOrWhiteSpace attendee ->
                    Error MissingAttendee
                | _ when value.Seats <= 0 -> Error(InvalidSeats value.Seats)
                | attendee ->
                    Ok
                        { RequestId = requestId
                          Attendee = attendee
                          Seats = value.Seats }

    let toDto (request: DomainRequest) : BookingRequestDto =
        { RequestId = request.RequestId
          Attendee = request.Attendee
          Seats = request.Seats }

let empty = Activator.CreateInstance<BookingRequestDto>()
assert (DomainRequest.ofDto empty = Error MissingRequestId)

let valid: BookingRequestDto =
    { RequestId = "REQ-27"
      Attendee = "Lin"
      Seats = 2 }

match DomainRequest.ofDto valid with
| Ok request -> assert (DomainRequest.toDto request = valid)
| Error error -> failwithf "unexpected DTO error: %A" error
```

A production decoder can also accumulate multiple field errors, normalize text, and add JSON paths to error context. What matters is that only `ofDto` understands default null/zero states; the workflow accepts `DomainRequest` and does not revalidate a DTO at every step.

### A field rename is a wire-format migration {#exercise-03-compatibility}

Renaming JSON `requestId` to `id` first breaks wire-format compatibility: stored documents and old clients still send the old name. If the DTO is also a public assembly type, renaming its property affects source and binary compatibility too—precisely why contracts should not be reused accidentally.

A safe migration can read both names for a period, write only the new name, and remove the old name according to a schema version or explicit deprecation schedule. The adapter maps both inputs to one domain field; domain `RequestId` need not follow serialization spelling.

## Solution review {#solution-review}

- Write public state laws first, then choose classes, enums, nullable values, and controlled construction.
- Let public members and an idiomatic F# surface share an internal implementation instead of implementing business rules through each other.
- Overloads can preserve an old signature, but real source must still test resolution ambiguity.
- When options become a concept, contain overload growth with a named options type and forwarding bridges.
- `CLIMutable` belongs on a DTO with a genuine construction requirement, not on the domain invariant itself.
- A DTO decoder accepts incomplete states; the domain workflow accepts only validated types.
- Assembly, behavioral, and wire-format contracts are different surfaces to test and migrate separately.
