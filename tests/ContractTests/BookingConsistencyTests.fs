namespace ThinkingInFSharp.ContractTests

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Ports
open Booking.Infrastructure
open Xunit

module BookingConsistencyTests =
    type private Counter() =
        let mutable value = 0

        member _.Increment() = Interlocked.Increment(&value) |> ignore
        member _.Next() = Interlocked.Increment(&value)
        member _.Value = Volatile.Read(&value)

    type private TemporaryDirectory() =
        let directoryPath =
            Path.Combine(Path.GetTempPath(), "thinking-in-fsharp", Guid.NewGuid().ToString("N"))

        do Directory.CreateDirectory(directoryPath) |> ignore

        member _.Path = directoryPath

        interface IDisposable with
            member _.Dispose() =
                if Directory.Exists directoryPath then
                    Directory.Delete(directoryPath, true)

    let private complete (operation: Task<'value>) = operation.GetAwaiter().GetResult()

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private activity capacityValue =
        let eventId = EventId.create "EVT-K11" |> expectOk
        let capacity = Capacity.create capacityValue |> expectOk
        Event.create eventId capacity

    let private configuration snapshotPath =
        BookingStoreConfiguration.create snapshotPath |> expectOk

    let private requestId raw = RequestId.create raw |> expectOk

    let private service snapshotPath event payment notification =
        let store = AtomicBookingStore(configuration snapshotPath)
        store, IdempotentBookingService(event, store, payment, notification)

    let private authorize (paymentCalls: Counter) (_: PaymentRequest) (cancellationToken: CancellationToken) =
        cancellationToken.ThrowIfCancellationRequested()
        paymentCalls.Increment()
        Task.FromResult(PaymentOutcome.Authorized "TX-K11")

    let private deliver (notificationCalls: Counter) (_: NotificationRequest) (cancellationToken: CancellationToken) =
        cancellationToken.ThrowIfCancellationRequested()
        notificationCalls.Increment()
        Task.FromResult()

    let private execute (application: IdempotentBookingService) command =
        application.Execute(command, CancellationToken.None) |> complete

    let private load (store: AtomicBookingStore) event rawRequestId =
        store.Load(event, requestId rawRequestId, CancellationToken.None) |> complete

    [<Fact>]
    let ``two controlled competitors cannot oversell aggregate capacity`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 3
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let store, application =
            service
                (Path.Combine(temporary.Path, "bookings.json"))
                event
                (authorize paymentCalls)
                (deliver notificationCalls)

        let release =
            TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

        use ready = new CountdownEvent(2)

        let compete command =
            task {
                ready.Signal() |> ignore
                do! release.Task
                return! application.Execute(command, CancellationToken.None)
            }

        let first = compete (BookingCommand.Place(Commands.place "REQ-RACE-A" 2))
        let second = compete (BookingCommand.Place(Commands.place "REQ-RACE-B" 2))
        ready.Wait()
        release.SetResult()

        let results = Task.WhenAll(first, second) |> complete

        let accepted =
            results
            |> Array.choose (function
                | Ok booking -> Some booking
                | Error _ -> None)

        let refused =
            results
            |> Array.choose (function
                | Error(BookingConsistencyError.AggregateCapacityExceeded(requested, remaining)) ->
                    Some(requested, remaining)
                | _ -> None)

        Assert.Single accepted |> ignore
        Assert.Equal<(int * int) array>([| 2, 1 |], refused)
        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(1, notificationCalls.Value)

        let occupied =
            [ "REQ-RACE-A"; "REQ-RACE-B" ]
            |> List.choose (fun id -> load store event id |> expectOk)
            |> List.sumBy (Booking.seats >> SeatCount.value >> int)

        Assert.Equal(2, occupied)

    [<Fact>]
    let ``concurrent equivalent requests replay without duplicate side effects`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 4
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let _, application =
            service
                (Path.Combine(temporary.Path, "bookings.json"))
                event
                (authorize paymentCalls)
                (deliver notificationCalls)

        let release =
            TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

        use ready = new CountdownEvent(2)

        let compete rawRequestId =
            task {
                ready.Signal() |> ignore
                do! release.Task

                return! application.Execute(BookingCommand.Place(Commands.place rawRequestId 2), CancellationToken.None)
            }

        let first = compete " REQ-DUPLICATE "
        let second = compete "REQ-DUPLICATE"
        ready.Wait()
        release.SetResult()

        let results = Task.WhenAll(first, second) |> complete
        Assert.All(results, fun result -> Assert.True(Result.isOk result, $"Expected success, received {result}"))
        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(1, notificationCalls.Value)

        Assert.Equal(
            Error BookingConsistencyError.IdempotencyConflict,
            execute application (BookingCommand.Place(Commands.place "REQ-DUPLICATE" 1))
        )

        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(1, notificationCalls.Value)

    [<Fact>]
    let ``notification failure retries only the pending notification`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 4
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let notification (_: NotificationRequest) (cancellationToken: CancellationToken) =
            cancellationToken.ThrowIfCancellationRequested()

            if notificationCalls.Next() = 1 then
                Task.FromException<unit>(
                    DependencyUnavailableException(
                        "Notification dependency is unavailable.",
                        InvalidOperationException "controlled notification failure"
                    )
                )
            else
                Task.FromResult()

        let store, application =
            service (Path.Combine(temporary.Path, "bookings.json")) event (authorize paymentCalls) notification

        let command = BookingCommand.Place(Commands.place "REQ-RETRY-NOTIFY" 2)

        Assert.Equal(Error BookingConsistencyError.DependencyUnavailable, execute application command)
        Assert.True(load store event "REQ-RETRY-NOTIFY" |> expectOk |> Option.isSome)

        execute application command |> expectOk |> ignore
        execute application command |> expectOk |> ignore

        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(2, notificationCalls.Value)

    [<Fact>]
    let ``uncertain payment is not charged blindly on retry`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 4
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let failPayment (_: PaymentRequest) (cancellationToken: CancellationToken) =
            cancellationToken.ThrowIfCancellationRequested()
            paymentCalls.Increment()

            Task.FromException<PaymentOutcome>(
                DependencyUnavailableException(
                    "Payment dependency is unavailable.",
                    InvalidOperationException "controlled payment failure"
                )
            )

        let store, application =
            service (Path.Combine(temporary.Path, "bookings.json")) event failPayment (deliver notificationCalls)

        let command = BookingCommand.Place(Commands.place "REQ-PAYMENT-UNKNOWN" 2)

        Assert.Equal(Error BookingConsistencyError.DependencyUnavailable, execute application command)
        Assert.Equal(Error BookingConsistencyError.PaymentOutcomeUnknown, execute application command)
        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(0, notificationCalls.Value)
        Assert.Equal(Ok None, load store event "REQ-PAYMENT-UNKNOWN")

    [<Fact>]
    let ``unexpected payment bugs are not classified as dependency outages`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 4
        let notificationCalls = Counter()

        let buggyPayment (_: PaymentRequest) (_: CancellationToken) =
            Task.FromException<PaymentOutcome>(InvalidOperationException "programming defect")

        let _, application =
            service (Path.Combine(temporary.Path, "bookings.json")) event buggyPayment (deliver notificationCalls)

        let command = BookingCommand.Place(Commands.place "REQ-PAYMENT-BUG" 2)

        let failure =
            Assert.Throws<InvalidOperationException>(fun () -> execute application command |> ignore)

        Assert.Equal("programming defect", failure.Message)
        Assert.Equal(0, notificationCalls.Value)

    [<Fact>]
    let ``capacity rejection may succeed after cancellation releases seats`` () =
        use temporary = new TemporaryDirectory()
        let event = activity 2
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let _, application =
            service
                (Path.Combine(temporary.Path, "bookings.json"))
                event
                (authorize paymentCalls)
                (deliver notificationCalls)

        execute application (BookingCommand.Place(Commands.place "REQ-HOLDER" 2))
        |> expectOk
        |> ignore

        let waiting = BookingCommand.Place(Commands.place "REQ-WAITING" 1)

        Assert.Equal(Error(BookingConsistencyError.AggregateCapacityExceeded(1, 0)), execute application waiting)

        execute application (BookingCommand.Cancel(Commands.cancel "REQ-HOLDER" "released"))
        |> expectOk
        |> ignore

        execute application waiting |> expectOk |> ignore

        Assert.Equal(2, paymentCalls.Value)
        Assert.Equal(3, notificationCalls.Value)

    [<Fact>]
    let ``a separate process restores state and replays without effects`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "bookings.json")
        let event = activity 4
        let paymentCalls = Counter()
        let notificationCalls = Counter()

        let _, application =
            service snapshotPath event (authorize paymentCalls) (deliver notificationCalls)

        execute application (BookingCommand.Place(Commands.place "REQ-RESTART" 2))
        |> expectOk
        |> ignore

        let persisted = File.ReadAllText snapshotPath
        Assert.Contains("\"schemaVersion\":1", persisted)
        Assert.DoesNotContain("TX-K11", persisted)

        let dotnetHost =
            match Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
            | null
            | "" -> "dotnet"
            | path -> path

        let scriptPath = Path.Combine(AppContext.BaseDirectory, "restart-probe.fsx")
        let startInfo = ProcessStartInfo(dotnetHost)
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true

        for argument in
            [ "fsi"
              "--exec"
              scriptPath
              snapshotPath
              "EVT-K11"
              "4"
              "REQ-RESTART"
              "2" ] do
            startInfo.ArgumentList.Add argument

        use probeProcess =
            match Process.Start startInfo with
            | null -> failwith "The restart probe process could not be started."
            | started -> started

        let output = probeProcess.StandardOutput.ReadToEndAsync()
        let error = probeProcess.StandardError.ReadToEndAsync()

        if not (probeProcess.WaitForExit 30000) then
            probeProcess.Kill(true)
            Assert.Fail "The restart probe did not exit within 30 seconds."

        let standardOutput = output |> complete
        let standardError = error |> complete

        Assert.True(probeProcess.ExitCode = 0, $"Restart probe failed: {standardError}")
        Assert.Equal("restored|REQ-RESTART|2|pending", standardOutput.Trim())
        Assert.Equal(1, paymentCalls.Value)
        Assert.Equal(1, notificationCalls.Value)
