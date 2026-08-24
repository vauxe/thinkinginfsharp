namespace ThinkingInFSharp.ExampleTests

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Workflow
open Booking.Infrastructure
open Xunit

module BookingAdapterTests =
    type private TemporaryDirectory() =
        let directoryPath =
            Path.Combine(Path.GetTempPath(), "thinking-in-fsharp", Guid.NewGuid().ToString("N"))

        do Directory.CreateDirectory(directoryPath) |> ignore

        member _.Path = directoryPath

        interface IDisposable with
            member _.Dispose() =
                if Directory.Exists directoryPath then
                    Directory.Delete(directoryPath, true)

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private complete (operation: Task<'value>) = operation.GetAwaiter().GetResult()

    let private createBooking requestId =
        let eventId = EventId.create "EVT-K09" |> expectOk
        let capacity = Capacity.create 8 |> expectOk
        let seats = SeatCount.create 2 |> expectOk
        let validRequestId = RequestId.create requestId |> expectOk
        Booking.create (Event.create eventId capacity) validRequestId seats |> expectOk

    let private paymentRequest requestId =
        { RequestId = RequestId.create requestId |> expectOk
          Seats = SeatCount.create 2 |> expectOk }

    let private notificationRequest requestId =
        { RequestId = RequestId.create requestId |> expectOk
          Message = "booking confirmed" }

    let private configuration snapshotPath =
        BookingStoreConfiguration.create snapshotPath |> expectOk

    [<Fact>]
    let ``payment stub deterministically authorizes or declines and records the request`` () =
        let request = paymentRequest "REQ-PAYMENT"
        use authorized = new PaymentStub(PaymentStubBehavior.Authorize "TX-STUB")
        use declined = new PaymentStub(PaymentStubBehavior.Decline "card declined")

        Assert.Equal(Authorized "TX-STUB", authorized.Invoke request CancellationToken.None |> complete)
        Assert.Equal(Declined "card declined", declined.Invoke request CancellationToken.None |> complete)
        Assert.Equal<PaymentRequest>([| request |], authorized.Calls)
        Assert.Equal<PaymentRequest>([| request |], declined.Calls)

    [<Fact>]
    let ``payment stub faults exactly and cancellation prevents a recorded side effect`` () =
        let request = paymentRequest "REQ-PAYMENT-FAIL"
        use failing = new PaymentStub(PaymentStubBehavior.Fail "payment offline")

        let failure =
            Assert.Throws<InvalidOperationException>(fun () ->
                failing.Invoke request CancellationToken.None |> complete |> ignore)

        Assert.Equal("payment offline", failure.Message)
        Assert.Single(failing.Calls) |> ignore

        use cancelled = new PaymentStub(PaymentStubBehavior.Authorize "TX-UNUSED")
        use owner = new CancellationTokenSource()
        owner.Cancel()

        Assert.ThrowsAny<OperationCanceledException>(fun () ->
            cancelled.Invoke request owner.Token |> complete |> ignore)
        |> ignore

        Assert.Empty cancelled.Calls

    [<Fact>]
    let ``notification stub delivers faults or cancels without networking`` () =
        let request = notificationRequest "REQ-NOTIFY"
        use delivered = new NotificationStub(NotificationStubBehavior.Deliver)
        use failing = new NotificationStub(NotificationStubBehavior.Fail "mailbox offline")

        delivered.Invoke request CancellationToken.None |> complete

        let failure =
            Assert.Throws<InvalidOperationException>(fun () ->
                failing.Invoke request CancellationToken.None |> complete)

        Assert.Equal("mailbox offline", failure.Message)
        Assert.Equal<NotificationRequest>([| request |], delivered.Calls)
        Assert.Equal<NotificationRequest>([| request |], failing.Calls)

        use cancelled = new NotificationStub(NotificationStubBehavior.Deliver)
        use owner = new CancellationTokenSource()
        owner.Cancel()

        Assert.ThrowsAny<OperationCanceledException>(fun () -> cancelled.Invoke request owner.Token |> complete)
        |> ignore

        Assert.Empty cancelled.Calls

    [<Fact>]
    let ``composition connects persistence stubs clock and caller cancellation token`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        let expectedNow = DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)
        let clockTokens = ResizeArray<CancellationToken>()

        let clock token =
            clockTokens.Add token
            Task.FromResult expectedNow

        use composition =
            Composition.start
                (configuration snapshotPath)
                (PaymentStubBehavior.Authorize "TX-COMPOSED")
                NotificationStubBehavior.Deliver
                clock

        let ports = composition.Ports
        let booking = createBooking "REQ-COMPOSED"
        let requestId = Booking.requestId booking
        let payment = paymentRequest "REQ-COMPOSED"
        let notification = notificationRequest "REQ-COMPOSED"
        use owner = new CancellationTokenSource()

        ports.AppendEvent requestId (BookingPlaced booking) owner.Token |> complete
        let loaded = ports.LoadBooking requestId owner.Token |> complete
        let paymentOutcome = ports.Charge payment owner.Token |> complete
        ports.Notify notification owner.Token |> complete
        let now = ports.GetUtcNow owner.Token |> complete

        Assert.Equal(Booked booking, loaded)
        Assert.Equal(Authorized "TX-COMPOSED", paymentOutcome)
        Assert.Equal(expectedNow, now)
        Assert.Equal<PaymentRequest>([| payment |], composition.PaymentStub.Calls)
        Assert.Equal<NotificationRequest>([| notification |], composition.NotificationStub.Calls)
        Assert.Equal<CancellationToken>([| owner.Token |], clockTokens)
        Assert.True(File.Exists snapshotPath)

    [<Fact>]
    let ``composition owns stub lifetimes and rejects use after disposal`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")

        let composition =
            Composition.start
                (configuration snapshotPath)
                (PaymentStubBehavior.Authorize "TX-LIFETIME")
                NotificationStubBehavior.Deliver
                (fun _ -> Task.FromResult DateTimeOffset.UnixEpoch)

        let ports = composition.Ports
        let payment = composition.PaymentStub
        let notification = composition.NotificationStub
        (composition :> IDisposable).Dispose()
        (composition :> IDisposable).Dispose()

        Assert.True(composition.IsDisposed)
        Assert.True(payment.IsDisposed)
        Assert.True(notification.IsDisposed)

        Assert.Throws<ObjectDisposedException>(fun () -> ports.GetUtcNow(CancellationToken.None) |> complete |> ignore)
        |> ignore

    [<Fact>]
    let ``composition keeps corrupt store details inside a typed adapter exception`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "booking.json")
        File.WriteAllText(snapshotPath, "{broken", Encoding.UTF8)

        use composition =
            Composition.start
                (configuration snapshotPath)
                (PaymentStubBehavior.Authorize "TX-UNUSED")
                NotificationStubBehavior.Deliver
                (fun _ -> Task.FromResult DateTimeOffset.UnixEpoch)

        let requestId = RequestId.create "REQ-CORRUPT" |> expectOk

        let failure =
            Assert.Throws<BookingStoreAdapterException>(fun () ->
                composition.Ports.LoadBooking requestId CancellationToken.None
                |> complete
                |> ignore)

        Assert.Equal(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidJson, failure.StoreError)
