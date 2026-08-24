namespace Booking.Domain

open Booking.Domain.Validation
open Booking.Domain.Workflow

// #region public-types
module PublicApi =
    [<RequireQualifiedAccess>]
    type BookingPhase =
        | Pending
        | Confirmed of confirmationCode: string
        | Cancelled of reason: string

    [<RequireQualifiedAccess>]
    type BookingError =
        | BlankEventId
        | NonPositiveCapacity of actual: int
        | BlankRequestId
        | NonPositiveSeatCount of actual: int
        | RequestedSeatsExceedCapacity of requested: int * capacity: int
        | BookingAlreadyExists of existingRequestId: string
        | BookingDoesNotExist
        | BlankConfirmationCode
        | BlankCancellationReason
        | CannotConfirmFrom of current: BookingPhase
        | CannotCancelFrom of current: BookingPhase

    type BookingView = private BookingView of requestId: string * eventId: string * seats: int * phase: BookingPhase

    module BookingView =
        let requestId (BookingView(requestId, _, _, _)) = requestId
        let eventId (BookingView(_, eventId, _, _)) = eventId
        let seats (BookingView(_, _, seats, _)) = seats
        let phase (BookingView(_, _, _, phase)) = phase

    type BookingModel = private BookingModel of event: Event * state: BookingState
    // #endregion public-types

    let private toPhase status =
        match status with
        | BookingStatus.Pending -> BookingPhase.Pending
        | BookingStatus.Confirmed code -> BookingPhase.Confirmed(ConfirmationCode.value code)
        | BookingStatus.Cancelled reason -> BookingPhase.Cancelled(CancellationReason.value reason)

    let private toView booking =
        BookingView(
            booking |> Booking.requestId |> RequestId.value,
            booking |> Booking.eventId |> EventId.value,
            booking |> Booking.seats |> SeatCount.value |> int,
            booking |> Booking.status |> toPhase
        )

    let eventId (BookingModel(event, _)) = event |> Event.id |> EventId.value

    let capacity (BookingModel(event, _)) =
        event |> Event.capacity |> Capacity.value |> int

    let tryBooking (BookingModel(_, state)) =
        match state with
        | NotBooked -> None
        | Booked booking -> Some(toView booking)

    // #region start
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
    // #endregion start

    let private mapCommandError error =
        match error with
        | InvalidRequestId BlankRequestId -> BookingError.BlankRequestId
        | InvalidSeatCount(NonPositiveSeatCount actual) -> BookingError.NonPositiveSeatCount actual
        | InvalidConfirmationCode BlankConfirmationCode -> BookingError.BlankConfirmationCode
        | InvalidCancellationReason BlankCancellationReason -> BookingError.BlankCancellationReason

    let private mapPlaceError error =
        match error with
        | InvalidCommand errors -> errors |> List.map mapCommandError
        | BookingAlreadyExists existingRequestId ->
            [ BookingError.BookingAlreadyExists(RequestId.value existingRequestId) ]
        | BookingCreationFailed(RequestedSeatsExceedCapacity(requested, capacity)) ->
            [ BookingError.RequestedSeatsExceedCapacity(int requested, int capacity) ]

    let private mapTransitionError error =
        match error with
        | CannotConfirmFrom current -> BookingError.CannotConfirmFrom(toPhase current)
        | CannotCancelFrom current -> BookingError.CannotCancelFrom(toPhase current)

    // #region transitions
    let place rawRequestId rawSeats (BookingModel(event, state)) =
        let command = Commands.place rawRequestId rawSeats

        match decidePlaceBooking event state command with
        | Error error -> error |> mapPlaceError |> Error
        | Ok bookingEvent -> BookingModel(event, evolve state bookingEvent) |> Ok

    let confirm rawConfirmationCode (BookingModel(event, state)) =
        match state with
        | NotBooked -> Error [ BookingError.BookingDoesNotExist ]
        | Booked booking ->
            let command =
                Commands.confirm (booking |> Booking.requestId |> RequestId.value) rawConfirmationCode

            match ConfirmationCode.create command.ConfirmationCode with
            | Error BlankConfirmationCode -> Error [ BookingError.BlankConfirmationCode ]
            | Ok confirmationCode ->
                match Booking.confirm confirmationCode booking with
                | Ok confirmed -> BookingModel(event, evolve state (BookingConfirmed confirmed)) |> Ok
                | Error error -> Error [ mapTransitionError error ]

    let cancel rawReason (BookingModel(event, state)) =
        match state with
        | NotBooked -> Error [ BookingError.BookingDoesNotExist ]
        | Booked booking ->
            let command =
                Commands.cancel (booking |> Booking.requestId |> RequestId.value) rawReason

            match CancellationReason.create command.Reason with
            | Error BlankCancellationReason -> Error [ BookingError.BlankCancellationReason ]
            | Ok reason ->
                match Booking.cancel reason booking with
                | Ok cancelled -> BookingModel(event, evolve state (BookingCancelled cancelled)) |> Ok
                | Error error -> Error [ mapTransitionError error ]
// #endregion transitions
