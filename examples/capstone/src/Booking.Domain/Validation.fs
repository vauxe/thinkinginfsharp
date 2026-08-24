namespace Booking.Domain

module Validation =
    type CommandValidationError =
        | InvalidRequestId of RequestIdError
        | InvalidSeatCount of SeatCountError

    type PlaceBookingCommand = { RequestId: string; Seats: int }

    type ValidPlaceBooking =
        private
            { RequestId: RequestId
              Seats: SeatCount }

    module ValidPlaceBooking =
        let requestId (command: ValidPlaceBooking) = command.RequestId
        let seats (command: ValidPlaceBooking) = command.Seats

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

    let private createValidCommand (requestId: RequestId) (seats: SeatCount) : ValidPlaceBooking =
        { RequestId = requestId; Seats = seats }

    let validatePlaceBooking (command: PlaceBookingCommand) =
        Ok createValidCommand
        |> applyValidation (validateRequestId command.RequestId)
        |> applyValidation (validateSeatCount command.Seats)
