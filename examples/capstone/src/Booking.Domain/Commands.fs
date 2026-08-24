namespace Booking.Domain

// #region commands
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
// #endregion commands
