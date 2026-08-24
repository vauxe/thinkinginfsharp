open System

module BookingDomain =
    [<Measure>]
    type seat

    // #region private-capacity
    type CapacityError =
        | NonPositiveCapacity of actual: int

    type Capacity = private Capacity of int<seat>

    module Capacity =
        let create raw =
            if raw > 0 then
                raw
                |> LanguagePrimitives.Int32WithMeasure<seat>
                |> Capacity
                |> Ok
            else
                Error(NonPositiveCapacity raw)

        let value (Capacity capacity) = capacity
    // #endregion private-capacity

    // #region protected-components
    type EventIdError =
        | BlankEventId

    type EventId = private EventId of string

    module EventId =
        let create raw =
            if String.IsNullOrWhiteSpace raw then
                Error BlankEventId
            else
                raw.Trim() |> EventId |> Ok

        let value (EventId eventId) = eventId

    type SeatCountError =
        | NonPositiveSeatCount of actual: int

    type SeatCount = private SeatCount of int<seat>

    module SeatCount =
        let create raw =
            if raw > 0 then
                raw
                |> LanguagePrimitives.Int32WithMeasure<seat>
                |> SeatCount
                |> Ok
            else
                Error(NonPositiveSeatCount raw)

        let value (SeatCount seats) = seats
    // #endregion protected-components

    // #region private-request
    type BookingRequestError =
        | InvalidEventId of EventIdError
        | InvalidSeatCount of SeatCountError

    type BookingRequest =
        private
            { EventId: EventId
              Seats: SeatCount }

    module BookingRequest =
        let create rawEventId rawSeats =
            rawEventId
            |> EventId.create
            |> Result.mapError InvalidEventId
            |> Result.bind (fun eventId ->
                rawSeats
                |> SeatCount.create
                |> Result.mapError InvalidSeatCount
                |> Result.map (fun seats ->
                    { EventId = eventId
                      Seats = seats }))

        let eventId request =
            request.EventId |> EventId.value

        let seats request =
            request.Seats |> SeatCount.value
    // #endregion private-request

open BookingDomain

// #region smart-constructor-results
let describeCapacityError error =
    match error with
    | NonPositiveCapacity actual ->
        $"capacity must be positive: {actual}"

match Capacity.create 40 with
| Ok capacity -> printfn "Capacity: accepted=%d" (Capacity.value capacity)
| Error error -> printfn "Capacity: %s" (describeCapacityError error)

match Capacity.create 0 with
| Ok _ -> printfn "Capacity rejection: unexpected success"
| Error error -> printfn "Capacity rejection: %s" (describeCapacityError error)

let describeRequestError error =
    match error with
    | InvalidEventId BlankEventId -> "event id is blank"
    | InvalidSeatCount(NonPositiveSeatCount actual) ->
        $"seat count must be positive: {actual}"

match BookingRequest.create "  EVT-42  " 3 with
| Ok request ->
    printfn
        "Request: event=%s seats=%d"
        (BookingRequest.eventId request)
        (BookingRequest.seats request)
| Error error -> printfn "Request: %s" (describeRequestError error)

match BookingRequest.create "   " 3 with
| Ok _ -> printfn "Request rejection: unexpected event success"
| Error error -> printfn "Request rejection: %s" (describeRequestError error)

match BookingRequest.create "EVT-42" 0 with
| Ok _ -> printfn "Request rejection: unexpected seat success"
| Error error -> printfn "Request rejection: %s" (describeRequestError error)
// #endregion smart-constructor-results
