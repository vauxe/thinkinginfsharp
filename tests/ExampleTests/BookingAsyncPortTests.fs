namespace ThinkingInFSharp.ExampleTests

open System
open System.Threading
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Testing
open Booking.Domain.Workflow
open Xunit

module BookingAsyncPortTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private requestId = RequestId.create "REQ-K04" |> expectOk
    let private seats = SeatCount.create 2 |> expectOk
    let private paymentRequest = { RequestId = requestId; Seats = seats }

    let private notification =
        { RequestId = requestId
          Message = "confirmed" }

    [<Fact>]
    let ``all asynchronous ports receive the caller token`` () =
        let load = ControlledOperation<RequestId, BookingState>()
        let append = ControlledOperation<RequestId * BookingEvent, unit>()
        let charge = ControlledOperation<PaymentRequest, PaymentOutcome>()
        let notify = ControlledOperation<NotificationRequest, unit>()
        let clock = ControlledOperation<unit, DateTimeOffset>()

        let ports =
            {
                LoadBooking = load.Invoke
                AppendEvent = fun id event token -> append.Invoke(id, event) token
                Charge = charge.Invoke
                Notify = notify.Invoke
                GetUtcNow = fun token -> clock.Invoke() token
            }

        use owner = new CancellationTokenSource()
        let loadTask = ports.LoadBooking requestId owner.Token

        let eventId = EventId.create "EVT-K04" |> expectOk
        let capacity = Capacity.create 4 |> expectOk
        let bookingEvent =
            Booking.create (Event.create eventId capacity) requestId seats
            |> expectOk
            |> BookingPlaced

        let appendTask = ports.AppendEvent requestId bookingEvent owner.Token
        let chargeTask = ports.Charge paymentRequest owner.Token
        let notifyTask = ports.Notify notification owner.Token
        let clockTask = ports.GetUtcNow owner.Token

        Assert.True(load.Succeed NotBooked)
        Assert.True(append.Succeed())
        Assert.True(charge.Succeed(Authorized "TX-K04"))
        Assert.True(notify.Succeed())

        let now = DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero)
        Assert.True(clock.Succeed now)

        Assert.Equal(NotBooked, loadTask.GetAwaiter().GetResult())
        appendTask.GetAwaiter().GetResult()
        Assert.Equal(Authorized "TX-K04", chargeTask.GetAwaiter().GetResult())
        notifyTask.GetAwaiter().GetResult()
        Assert.Equal(now, clockTask.GetAwaiter().GetResult())

        let allTokens =
            [
                load.Calls.Head.CancellationToken
                append.Calls.Head.CancellationToken
                charge.Calls.Head.CancellationToken
                notify.Calls.Head.CancellationToken
                clock.Calls.Head.CancellationToken
            ]

        Assert.All(allTokens, fun token -> Assert.Equal(owner.Token, token))

    [<Fact>]
    let ``controlled operation stays pending until explicitly completed`` () =
        let controlled = ControlledOperation<string, int>()
        let running = controlled.Invoke "quote" CancellationToken.None

        controlled.Entered.GetAwaiter().GetResult()
        Assert.False(running.IsCompleted)
        Assert.Equal("quote", controlled.Calls.Head.Input)

        Assert.True(controlled.Succeed 23)
        Assert.Equal(23, running.GetAwaiter().GetResult())

    [<Fact>]
    let ``controlled operation preserves a supplied fault`` () =
        let controlled = ControlledOperation<unit, string>()
        let running = controlled.Invoke() CancellationToken.None
        let expected = InvalidOperationException "payment-offline"

        Assert.True(controlled.Fail expected)

        let observed =
            try
                running.GetAwaiter().GetResult() |> ignore
                None
            with :? InvalidOperationException as error ->
                Some error

        Assert.True(running.IsFaulted)
        Assert.Same(expected, observed.Value)

    [<Fact>]
    let ``controlled operation observes caller cancellation`` () =
        let controlled = ControlledOperation<unit, string>()
        use owner = new CancellationTokenSource()
        let running = controlled.Invoke() owner.Token

        controlled.Entered.GetAwaiter().GetResult()
        owner.Cancel()

        let observedToken =
            try
                running.GetAwaiter().GetResult() |> ignore
                None
            with :? OperationCanceledException as error ->
                Some error.CancellationToken

        Assert.True(running.IsCanceled)
        Assert.Equal(Some owner.Token, observedToken)
        Assert.Equal(owner.Token, controlled.Calls.Head.CancellationToken)
