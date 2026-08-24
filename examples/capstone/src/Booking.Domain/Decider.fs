namespace Booking.Domain

open Booking.Domain.Validation
open Booking.Domain.Workflow

// #region decision-contract
[<RequireQualifiedAccess>]
type BookingDecisionError =
    | InvalidCommand of CommandValidationError list
    | BookingAlreadyExists of existingRequestId: RequestId
    | BookingDoesNotExist
    | BookingCreationFailed of BookingCreationError
    | BookingTransitionFailed of BookingTransitionError
// #endregion decision-contract

module Decider =
    let private mapPlaceError error =
        match error with
        | PlaceBookingError.InvalidCommand errors -> BookingDecisionError.InvalidCommand errors
        | PlaceBookingError.BookingAlreadyExists requestId -> BookingDecisionError.BookingAlreadyExists requestId
        | PlaceBookingError.BookingCreationFailed creationError ->
            BookingDecisionError.BookingCreationFailed creationError

    let private requireBooking requestId state =
        match state with
        | NotBooked -> Error BookingDecisionError.BookingDoesNotExist
        | Booked booking when Booking.requestId booking = requestId -> Ok booking
        | Booked _ -> Error BookingDecisionError.BookingDoesNotExist

    let private decideConfirm state command =
        requireBooking (ValidConfirmBooking.requestId command) state
        |> Result.bind (fun booking ->
            Booking.confirm (ValidConfirmBooking.confirmationCode command) booking
            |> Result.map BookingConfirmed
            |> Result.mapError BookingDecisionError.BookingTransitionFailed)

    let private decideCancel state command =
        requireBooking (ValidCancelBooking.requestId command) state
        |> Result.bind (fun booking ->
            Booking.cancel (ValidCancelBooking.reason command) booking
            |> Result.map BookingCancelled
            |> Result.mapError BookingDecisionError.BookingTransitionFailed)

    // #region decide
    let decide
        (activity: Event)
        (state: BookingState)
        (command: BookingCommand)
        : Result<BookingEvent, BookingDecisionError> =
        match command with
        | BookingCommand.Place placeCommand ->
            decidePlaceBooking activity state placeCommand |> Result.mapError mapPlaceError
        | BookingCommand.Confirm confirmCommand ->
            validateConfirmBooking confirmCommand
            |> Result.mapError BookingDecisionError.InvalidCommand
            |> Result.bind (decideConfirm state)
        | BookingCommand.Cancel cancelCommand ->
            validateCancelBooking cancelCommand
            |> Result.mapError BookingDecisionError.InvalidCommand
            |> Result.bind (decideCancel state)
// #endregion decide
