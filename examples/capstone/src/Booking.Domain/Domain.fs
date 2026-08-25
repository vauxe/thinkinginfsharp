namespace Booking.Domain

open System

[<Measure>]
type seat

module private NormalizedText =
    let create (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            None
        else
            Some(raw.Trim())

type EventIdError = | BlankEventId

type EventId = private EventId of string

module EventId =
    let create raw =
        match NormalizedText.create raw with
        | Some value -> Ok(EventId value)
        | None -> Error BlankEventId

    let value (EventId eventId) = eventId

type RequestIdError =
    | BlankRequestId
    | RequestIdTooLong of maximum: int * actual: int
    | InvalidRequestIdFormat
    | InvalidRequestIdCharacter of actual: char

type RequestId = private RequestId of string

module RequestId =
    [<Literal>]
    let MaxLength = 64

    let private isUriUnreserved character =
        (character >= 'A' && character <= 'Z')
        || (character >= 'a' && character <= 'z')
        || (character >= '0' && character <= '9')
        || character = '-'
        || character = '.'
        || character = '_'
        || character = '~'

    let create raw =
        match NormalizedText.create raw with
        | None -> Error BlankRequestId
        | Some "."
        | Some ".." -> Error InvalidRequestIdFormat
        | Some value when value.Length > MaxLength -> Error(RequestIdTooLong(MaxLength, value.Length))
        | Some value ->
            match value |> Seq.tryFind (isUriUnreserved >> not) with
            | Some invalid -> Error(InvalidRequestIdCharacter invalid)
            | None -> Ok(RequestId value)

    let value (RequestId requestId) = requestId

type CapacityError = NonPositiveCapacity of actual: int

type Capacity = private Capacity of int<seat>

module Capacity =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> Capacity |> Ok
        else
            Error(NonPositiveCapacity raw)

    let value (Capacity capacity) = capacity

type SeatCountError = NonPositiveSeatCount of actual: int

type SeatCount = private SeatCount of int<seat>

module SeatCount =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> SeatCount |> Ok
        else
            Error(NonPositiveSeatCount raw)

    let value (SeatCount seats) = seats

type ConfirmationCodeError = | BlankConfirmationCode

type ConfirmationCode = private ConfirmationCode of string

module ConfirmationCode =
    let create raw =
        match NormalizedText.create raw with
        | Some value -> Ok(ConfirmationCode value)
        | None -> Error BlankConfirmationCode

    let value (ConfirmationCode code) = code

type CancellationReasonError = | BlankCancellationReason

type CancellationReason = private CancellationReason of string

module CancellationReason =
    let create raw =
        match NormalizedText.create raw with
        | Some value -> Ok(CancellationReason value)
        | None -> Error BlankCancellationReason

    let value (CancellationReason reason) = reason

// #region booking-model
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

    // #region booking-rehydration
    let restore requestId eventId seats status =
        { RequestId = requestId
          EventId = eventId
          Seats = seats
          Status = status }
    // #endregion booking-rehydration

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
// #endregion booking-model
