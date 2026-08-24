namespace ThinkingInFSharp.ExampleTests

open Booking.Domain
open Booking.Domain.Validation
open Booking.Domain.Workflow
open Xunit

module BookingWorkflowTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private createEvent capacity =
        let eventId = EventId.create "EVT-K03" |> expectOk
        let validCapacity = Capacity.create capacity |> expectOk
        Event.create eventId validCapacity

    let private command requestId seats : PlaceBookingCommand =
        { RequestId = requestId
          Seats = seats }

    [<Fact>]
    let ``independent command errors accumulate in field order`` () =
        let result = validatePlaceBooking (command " " 0)

        Assert.Equal(
            Error
                [ InvalidRequestId BlankRequestId
                  InvalidSeatCount(NonPositiveSeatCount 0) ],
            result
        )

    [<Fact>]
    let ``valid command produces an event that evolves booking state`` () =
        let event = createEvent 4
        let result = decidePlaceBooking event NotBooked (command " REQ-K03 " 3)

        match result with
        | Error error -> failwithf "Expected BookingPlaced, received %A" error
        | Ok(BookingPlaced booking as bookingEvent) ->
            Assert.Equal("REQ-K03", booking |> Booking.requestId |> RequestId.value)
            Assert.Equal(3<seat>, booking |> Booking.seats |> SeatCount.value)
            Assert.Equal(Pending, Booking.status booking)
            Assert.Equal(Booked booking, evolve NotBooked bookingEvent)

    [<Fact>]
    let ``capacity failure follows successful command validation`` () =
        let event = createEvent 4
        let result = decidePlaceBooking event NotBooked (command "REQ-K03" 5)

        Assert.Equal(
            Error(BookingCreationFailed(RequestedSeatsExceedCapacity(5<seat>, 4<seat>))),
            result
        )

    [<Fact>]
    let ``existing state short circuits the later capacity decision`` () =
        let event = createEvent 4
        let existingId = RequestId.create "REQ-EXISTING" |> expectOk
        let existingSeats = SeatCount.create 2 |> expectOk
        let existing = Booking.create event existingId existingSeats |> expectOk

        let result =
            decidePlaceBooking event (Booked existing) (command "REQ-NEW" 5)

        Assert.Equal(Error(BookingAlreadyExists existingId), result)

    [<Fact>]
    let ``invalid input is reported before inspecting existing state`` () =
        let event = createEvent 4
        let existingId = RequestId.create "REQ-EXISTING" |> expectOk
        let existingSeats = SeatCount.create 2 |> expectOk
        let existing = Booking.create event existingId existingSeats |> expectOk

        let result =
            decidePlaceBooking event (Booked existing) (command "" 0)

        Assert.Equal(
            Error
                (InvalidCommand
                    [ InvalidRequestId BlankRequestId
                      InvalidSeatCount(NonPositiveSeatCount 0) ]),
            result
        )
