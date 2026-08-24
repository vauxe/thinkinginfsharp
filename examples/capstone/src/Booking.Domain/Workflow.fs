namespace Booking.Domain

open Booking.Domain.Validation

module Workflow =
    // #region booking-state
    type BookingState =
        | NotBooked
        | Booked of Booking
    // #endregion booking-state

    // Compatibility name for callers of the earlier workflow module.
    type BookingEvent = Booking.Domain.BookingEvent

    // #region place-decision
    type PlaceBookingError =
        | InvalidCommand of CommandValidationError list
        | BookingAlreadyExists of existingRequestId: RequestId
        | BookingCreationFailed of BookingCreationError

    let decidePlaceBooking (event: Event) (state: BookingState) (command: PlaceBookingCommand) =
        match validatePlaceBooking command with
        | Error errors -> Error(InvalidCommand errors)
        | Ok validCommand ->
            match state with
            | Booked existing -> Error(BookingAlreadyExists(Booking.requestId existing))
            | NotBooked ->
                Booking.create event (ValidPlaceBooking.requestId validCommand) (ValidPlaceBooking.seats validCommand)
                |> Result.map BookingPlaced
                |> Result.mapError BookingCreationFailed
    // #endregion place-decision

    // #region evolve
    let evolve (_: BookingState) (event: BookingEvent) =
        match event with
        | BookingPlaced booking
        | BookingConfirmed booking
        | BookingCancelled booking -> Booked booking
// #endregion evolve
