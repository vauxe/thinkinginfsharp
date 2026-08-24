namespace ThinkingInFSharp.ExampleTests

open Booking.Domain
open Booking.Domain.Validation
open Booking.Domain.Workflow
open Xunit

module BookingDeciderTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private createActivity capacity =
        let eventId = EventId.create "EVT-K07" |> expectOk
        let validCapacity = Capacity.create capacity |> expectOk
        Event.create eventId validCapacity

    let private decidePlace activity state requestId seats =
        Commands.place requestId seats
        |> BookingCommand.Place
        |> Decider.decide activity state

    let private decideConfirm activity state requestId code =
        Commands.confirm requestId code
        |> BookingCommand.Confirm
        |> Decider.decide activity state

    let private decideCancel activity state requestId reason =
        Commands.cancel requestId reason
        |> BookingCommand.Cancel
        |> Decider.decide activity state

    let private place activity requestId seats =
        let bookingEvent = decidePlace activity NotBooked requestId seats |> expectOk
        evolve NotBooked bookingEvent

    [<Fact>]
    let ``placement validation accumulates independent errors before state rules`` () =
        let activity = createActivity 4
        let result = decidePlace activity NotBooked " " 0

        Assert.Equal(
            Error(
                BookingDecisionError.InvalidCommand
                    [ InvalidRequestId BlankRequestId; InvalidSeatCount(NonPositiveSeatCount 0) ]
            ),
            result
        )

    [<Fact>]
    let ``confirmation validation accumulates independent errors before missing state`` () =
        let activity = createActivity 4
        let result = decideConfirm activity NotBooked "" " "

        Assert.Equal(
            Error(
                BookingDecisionError.InvalidCommand
                    [ InvalidRequestId BlankRequestId
                      InvalidConfirmationCode BlankConfirmationCode ]
            ),
            result
        )

    [<Fact>]
    let ``cancellation validation accumulates independent errors before missing state`` () =
        let activity = createActivity 4
        let result = decideCancel activity NotBooked "\t" " "

        Assert.Equal(
            Error(
                BookingDecisionError.InvalidCommand
                    [ InvalidRequestId BlankRequestId
                      InvalidCancellationReason BlankCancellationReason ]
            ),
            result
        )

    [<Fact>]
    let ``valid placement emits a fact that evolves state`` () =
        let activity = createActivity 4
        let bookingEvent = decidePlace activity NotBooked " REQ-K07 " 3 |> expectOk

        match bookingEvent with
        | BookingPlaced booking ->
            Assert.Equal("REQ-K07", booking |> Booking.requestId |> RequestId.value)
            Assert.Equal(3<seat>, booking |> Booking.seats |> SeatCount.value)
            Assert.Equal(Pending, Booking.status booking)
            Assert.Equal(Booked booking, evolve NotBooked bookingEvent)
        | unexpected -> failwithf "Expected BookingPlaced, received %A" unexpected

    [<Fact>]
    let ``placement maps capacity refusal without inventing another rule`` () =
        let activity = createActivity 4
        let result = decidePlace activity NotBooked "REQ-K07" 5

        Assert.Equal(
            Error(BookingDecisionError.BookingCreationFailed(RequestedSeatsExceedCapacity(5<seat>, 4<seat>))),
            result
        )

    [<Fact>]
    let ``existing booking short circuits later placement capacity rule`` () =
        let activity = createActivity 4
        let state = place activity "REQ-EXISTING" 2

        let existing =
            match state with
            | Booked booking -> booking
            | NotBooked -> failwith "Expected existing booking"

        let result = decidePlace activity state "REQ-NEW" 5

        Assert.Equal(Error(BookingDecisionError.BookingAlreadyExists(Booking.requestId existing)), result)

    [<Fact>]
    let ``valid confirmation emits normalized confirmed booking`` () =
        let activity = createActivity 4
        let state = place activity "REQ-K07" 3
        let bookingEvent = decideConfirm activity state " REQ-K07 " " CONF-7 " |> expectOk

        match bookingEvent with
        | BookingConfirmed booking ->
            match Booking.status booking with
            | Confirmed code -> Assert.Equal("CONF-7", ConfirmationCode.value code)
            | unexpected -> failwithf "Expected Confirmed, received %A" unexpected

            Assert.Equal(Booked booking, evolve state bookingEvent)
        | unexpected -> failwithf "Expected BookingConfirmed, received %A" unexpected

    [<Fact>]
    let ``valid command against absent or mismatched booking reports not found`` () =
        let activity = createActivity 4
        let existingState = place activity "REQ-EXISTING" 2

        Assert.Equal(
            Error BookingDecisionError.BookingDoesNotExist,
            decideConfirm activity NotBooked "REQ-MISSING" "CONF-7"
        )

        Assert.Equal(
            Error BookingDecisionError.BookingDoesNotExist,
            decideCancel activity existingState "REQ-OTHER" "duplicate"
        )

    [<Fact>]
    let ``confirmed booking cannot be confirmed twice`` () =
        let activity = createActivity 4
        let placed = place activity "REQ-K07" 3
        let firstEvent = decideConfirm activity placed "REQ-K07" "CONF-7" |> expectOk
        let confirmed = evolve placed firstEvent

        let code =
            match confirmed with
            | Booked booking ->
                match Booking.status booking with
                | Confirmed code -> code
                | unexpected -> failwithf "Expected Confirmed, received %A" unexpected
            | NotBooked -> failwith "Expected confirmed booking"

        let result = decideConfirm activity confirmed "REQ-K07" "CONF-8"

        Assert.Equal(Error(BookingDecisionError.BookingTransitionFailed(CannotConfirmFrom(Confirmed code))), result)

    [<Fact>]
    let ``cancellation emits a final fact and repeated cancellation is refused`` () =
        let activity = createActivity 4
        let placed = place activity "REQ-K07" 3
        let confirmedEvent = decideConfirm activity placed "REQ-K07" "CONF-7" |> expectOk
        let confirmed = evolve placed confirmedEvent

        let cancelledEvent =
            decideCancel activity confirmed "REQ-K07" " duplicate " |> expectOk

        let cancelled = evolve confirmed cancelledEvent

        match cancelledEvent with
        | BookingCancelled booking ->
            match Booking.status booking with
            | Cancelled reason -> Assert.Equal("duplicate", CancellationReason.value reason)
            | unexpected -> failwithf "Expected Cancelled, received %A" unexpected
        | unexpected -> failwithf "Expected BookingCancelled, received %A" unexpected

        match cancelled with
        | NotBooked -> failwith "Expected cancelled booking"
        | Booked booking ->
            let reason =
                match Booking.status booking with
                | Cancelled reason -> reason
                | unexpected -> failwithf "Expected Cancelled, received %A" unexpected

            Assert.Equal(
                Error(BookingDecisionError.BookingTransitionFailed(CannotCancelFrom(Cancelled reason))),
                decideCancel activity cancelled "REQ-K07" "again"
            )
