namespace ThinkingInFSharp.Ch16

open System

module Domain =
    type BookingIdError = | MissingBookingId

    type BookingId = private BookingId of string

    module BookingId =
        let create (raw: string | null) =
            match raw with
            | null -> Error MissingBookingId
            | value when String.IsNullOrWhiteSpace value -> Error MissingBookingId
            | value -> Ok(BookingId(value.Trim()))

        let value (BookingId bookingId) = bookingId

    type SeatCountError = NonPositiveSeatCount of actual: int

    type SeatCount = private SeatCount of int

    module SeatCount =
        let create raw =
            if raw > 0 then
                Ok(SeatCount raw)
            else
                Error(NonPositiveSeatCount raw)

        let value (SeatCount seats) = seats

    type CapacityError = NonPositiveCapacity of actual: int

    type Capacity = private Capacity of int

    module Capacity =
        let create raw =
            if raw > 0 then
                Ok(Capacity raw)
            else
                Error(NonPositiveCapacity raw)

        let value (Capacity capacity) = capacity

    type BookingRequestError =
        | InvalidBookingId of BookingIdError
        | InvalidSeatCount of SeatCountError

    type BookingRequest =
        private
            { Id: BookingId
              Seats: SeatCount }

    module BookingRequest =
        let create (rawId: string | null) rawSeats =
            match BookingId.create rawId with
            | Error error -> Error(InvalidBookingId error)
            | Ok bookingId ->
                match SeatCount.create rawSeats with
                | Error error -> Error(InvalidSeatCount error)
                | Ok seats -> Ok { Id = bookingId; Seats = seats }

        let id request = request.Id
        let seats request = request.Seats
