namespace Booking.Domain

module Validation =
    type CommandValidationError =
        | InvalidRequestId of RequestIdError
        | InvalidSeatCount of SeatCountError
        | InvalidConfirmationCode of ConfirmationCodeError
        | InvalidCancellationReason of CancellationReasonError

    // Compatibility name for the earlier teaching slice; no second runtime type is created.
    type PlaceBookingCommand = PlaceBooking

    type ValidPlaceBooking =
        private
            { RequestId: RequestId
              Seats: SeatCount }

    module ValidPlaceBooking =
        let requestId (command: ValidPlaceBooking) = command.RequestId
        let seats (command: ValidPlaceBooking) = command.Seats

    // #region validated-lifecycle-commands
    type ValidConfirmBooking =
        private
            { RequestId: RequestId
              ConfirmationCode: ConfirmationCode }

    module ValidConfirmBooking =
        let requestId (command: ValidConfirmBooking) = command.RequestId
        let confirmationCode (command: ValidConfirmBooking) = command.ConfirmationCode

    type ValidCancelBooking =
        private
            { RequestId: RequestId
              Reason: CancellationReason }

    module ValidCancelBooking =
        let requestId (command: ValidCancelBooking) = command.RequestId
        let reason (command: ValidCancelBooking) = command.Reason
    // #endregion validated-lifecycle-commands

    // #region validation-accumulation
    let private applyValidation valueResult functionResult =
        match functionResult, valueResult with
        | Ok mapping, Ok value -> Ok(mapping value)
        | Error earlier, Error later -> Error(earlier @ later)
        | Error errors, Ok _
        | Ok _, Error errors -> Error errors

    let private validateRequestId (raw: string) =
        RequestId.create raw
        |> Result.mapError (fun error -> [ InvalidRequestId error ])

    let private validateSeatCount (raw: int) =
        SeatCount.create raw
        |> Result.mapError (fun error -> [ InvalidSeatCount error ])

    let private validateConfirmationCode raw =
        ConfirmationCode.create raw
        |> Result.mapError (fun error -> [ InvalidConfirmationCode error ])

    let private validateCancellationReason raw =
        CancellationReason.create raw
        |> Result.mapError (fun error -> [ InvalidCancellationReason error ])

    let private createValidCommand (requestId: RequestId) (seats: SeatCount) : ValidPlaceBooking =
        { RequestId = requestId; Seats = seats }

    let validatePlaceBooking (command: PlaceBookingCommand) =
        Ok createValidCommand
        |> applyValidation (validateRequestId command.RequestId)
        |> applyValidation (validateSeatCount command.Seats)
    // #endregion validation-accumulation

    // #region lifecycle-validation
    let private createValidConfirmCommand requestId confirmationCode : ValidConfirmBooking =
        { RequestId = requestId
          ConfirmationCode = confirmationCode }

    let validateConfirmBooking (command: ConfirmBooking) =
        Ok createValidConfirmCommand
        |> applyValidation (validateRequestId command.RequestId)
        |> applyValidation (validateConfirmationCode command.ConfirmationCode)

    let private createValidCancelCommand requestId reason : ValidCancelBooking =
        { RequestId = requestId
          Reason = reason }

    let validateCancelBooking (command: CancelBooking) =
        Ok createValidCancelCommand
        |> applyValidation (validateRequestId command.RequestId)
        |> applyValidation (validateCancellationReason command.Reason)
// #endregion lifecycle-validation
