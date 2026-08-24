namespace ThinkingInFSharp.ExampleTests

open Booking.Domain
open Xunit

module BookingDomainTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private expectError result =
        match result with
        | Ok value -> failwithf "Expected Error, received Ok %A" value
        | Error error -> error

    [<Fact>]
    let ``identifiers reject blank input and normalize surrounding whitespace`` () =
        Assert.Equal(BlankEventId, EventId.create "   " |> expectError)
        Assert.Equal(BlankRequestId, RequestId.create "\t" |> expectError)

        let eventId = EventId.create "  EVT-42  " |> expectOk
        let requestId = RequestId.create "  REQ-9  " |> expectOk

        Assert.Equal("EVT-42", EventId.value eventId)
        Assert.Equal("REQ-9", RequestId.value requestId)

    [<Fact>]
    let ``capacity and seat count reject non-positive values`` () =
        Assert.Equal(NonPositiveCapacity 0, Capacity.create 0 |> expectError)
        Assert.Equal(NonPositiveCapacity -2, Capacity.create -2 |> expectError)
        Assert.Equal(NonPositiveSeatCount 0, SeatCount.create 0 |> expectError)

        let capacity = Capacity.create 40 |> expectOk
        let seats = SeatCount.create 3 |> expectOk

        Assert.Equal(40<seat>, Capacity.value capacity)
        Assert.Equal(3<seat>, SeatCount.value seats)

    [<Fact>]
    let ``event is assembled only from protected identifiers and capacity`` () =
        let eventId = EventId.create "EVT-42" |> expectOk
        let capacity = Capacity.create 40 |> expectOk
        let event = Event.create eventId capacity

        Assert.Equal(eventId, Event.id event)
        Assert.Equal(capacity, Event.capacity event)

    [<Fact>]
    let ``booking creation rejects a request larger than event capacity`` () =
        let eventId = EventId.create "EVT-42" |> expectOk
        let event = Event.create eventId (Capacity.create 4 |> expectOk)
        let requestId = RequestId.create "REQ-9" |> expectOk
        let requested = SeatCount.create 5 |> expectOk

        let error = Booking.create event requestId requested |> expectError

        Assert.Equal(RequestedSeatsExceedCapacity(5<seat>, 4<seat>), error)

    [<Fact>]
    let ``new booking starts pending and exposes protected fields`` () =
        let eventId = EventId.create "EVT-42" |> expectOk
        let event = Event.create eventId (Capacity.create 4 |> expectOk)
        let requestId = RequestId.create "REQ-9" |> expectOk
        let requested = SeatCount.create 3 |> expectOk
        let booking = Booking.create event requestId requested |> expectOk

        Assert.Equal(requestId, Booking.requestId booking)
        Assert.Equal(eventId, Booking.eventId booking)
        Assert.Equal(requested, Booking.seats booking)
        Assert.Equal(Pending, Booking.status booking)

    [<Fact>]
    let ``status details reject blank input`` () =
        Assert.Equal(BlankConfirmationCode, ConfirmationCode.create " " |> expectError)
        Assert.Equal(BlankCancellationReason, CancellationReason.create "\t" |> expectError)

    [<Fact>]
    let ``pending booking confirms immutably and cannot confirm twice`` () =
        let event =
            Event.create (EventId.create "EVT-42" |> expectOk) (Capacity.create 4 |> expectOk)

        let booking =
            Booking.create event (RequestId.create "REQ-9" |> expectOk) (SeatCount.create 3 |> expectOk)
            |> expectOk

        let code = ConfirmationCode.create "  CONF-1  " |> expectOk
        let confirmed = Booking.confirm code booking |> expectOk

        Assert.Equal(Pending, Booking.status booking)
        Assert.Equal(Confirmed code, Booking.status confirmed)
        Assert.Equal("CONF-1", ConfirmationCode.value code)

        let error = Booking.confirm code confirmed |> expectError
        Assert.Equal(CannotConfirmFrom(Confirmed code), error)

    [<Fact>]
    let ``booking cancellation is final`` () =
        let event =
            Event.create (EventId.create "EVT-42" |> expectOk) (Capacity.create 4 |> expectOk)

        let booking =
            Booking.create event (RequestId.create "REQ-9" |> expectOk) (SeatCount.create 3 |> expectOk)
            |> expectOk

        let code = ConfirmationCode.create "CONF-1" |> expectOk
        let confirmed = Booking.confirm code booking |> expectOk
        let reason = CancellationReason.create "  duplicate request  " |> expectOk
        let cancelled = Booking.cancel reason confirmed |> expectOk

        Assert.Equal(Cancelled reason, Booking.status cancelled)
        Assert.Equal("duplicate request", CancellationReason.value reason)

        let confirmError = Booking.confirm code cancelled |> expectError
        let cancelError = Booking.cancel reason cancelled |> expectError

        Assert.Equal(CannotConfirmFrom(Cancelled reason), confirmError)
        Assert.Equal(CannotCancelFrom(Cancelled reason), cancelError)
