namespace Booking.Domain

// #region events
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
// #endregion events
