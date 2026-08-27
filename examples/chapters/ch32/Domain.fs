namespace Booking.Domain

open System

type EventId = private EventId of string

module EventId =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error "event id is blank"
        else
            Ok(EventId raw)

type Capacity = private Capacity of int

module Capacity =
    let create value =
        if value > 0 then Ok(Capacity value) else Error "capacity must be positive"

    let value (Capacity value) = value

type Event =
    private
        { Id: EventId
          Capacity: Capacity }

module Event =
    let create eventId capacity =
        { Id = eventId
          Capacity = capacity }

    let capacity event = event.Capacity

type RequestId = string

type BookingState =
    | NotBooked
    | Booked

type BookingEvent = BookingPlaced of requestId: RequestId * seats: int

module Validation =
    type PlaceBookingCommand =
        { RequestId: string
          Seats: int }

    type ValidPlaceBooking = private ValidPlaceBooking of PlaceBookingCommand

    module ValidPlaceBooking =
        let requestId (ValidPlaceBooking command) = command.RequestId

    let validatePlaceBooking command =
        [ if String.IsNullOrWhiteSpace command.RequestId then
              "request id is blank"
          if command.Seats <= 0 then
              "seat count must be positive" ]
        |> function
            | [] -> Ok(ValidPlaceBooking command)
            | errors -> Error errors

module Workflow =
    open Validation

    type BookingFailure =
        | InvalidCommand of string list
        | AlreadyBooked
        | ExceedsCapacity of requested: int * available: int

    let decidePlaceBooking event state command =
        match state with
        | Booked -> Error AlreadyBooked
        | NotBooked ->
            let available = event |> Event.capacity |> Capacity.value

            if command.Seats > available then
                Error(ExceedsCapacity(command.Seats, available))
            else
                Ok(BookingPlaced(command.RequestId, command.Seats))

    let evolve _ _ = Booked
