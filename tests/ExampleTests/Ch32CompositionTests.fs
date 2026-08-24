namespace ThinkingInFSharp.ExampleTests

open System
open System.Diagnostics
open System.Diagnostics.Metrics
open System.Threading
open System.Threading.Tasks
open Booking.Domain.Validation
open Booking.Domain.Workflow
open ThinkingInFSharp.Ch32
open Xunit

module Ch32CompositionTests =
    type private TrackingResource() =
        member val IsDisposed = false with get, private set

        interface IDisposable with
            member this.Dispose() = this.IsDisposed <- true

    let private validConfig () =
        let lookup name =
            match name with
            | AppConfig.EventIdSetting -> Some "EVT-32"
            | AppConfig.CapacitySetting -> Some "4"
            | _ -> None

        match AppConfig.load lookup with
        | Ok config -> config
        | Error errors -> failwithf "invalid fixture configuration: %A" errors

    [<Fact>]
    let ``configuration accumulates independent errors`` () =
        let lookup name =
            if name = AppConfig.CapacitySetting then
                Some "zero"
            else
                None

        Assert.Equal(
            Error
                [ MissingSetting AppConfig.EventIdSetting
                  InvalidSetting(AppConfig.CapacitySetting, "zero") ],
            AppConfig.load lookup
        )

    [<Fact>]
    let ``composition emits structured log metric and completed activity`` () =
        let resource = new TrackingResource()
        let tokens = ResizeArray<CancellationToken>()
        let appended = ResizeArray<BookingEvent>()
        let logs = ResizeArray<BookingLog>()
        let measurements = ResizeArray<string * int64 * string>()
        let activities = ResizeArray<Activity>()

        let ports =
            { LoadBooking =
                fun _ token ->
                    tokens.Add token
                    Task.FromResult NotBooked
              AppendEvent =
                fun _ bookingEvent token ->
                    tokens.Add token
                    appended.Add bookingEvent
                    Task.FromResult()
              OwnedResource = resource }

        use meterListener = new MeterListener()

        meterListener.InstrumentPublished <-
            fun instrument listener ->
                if instrument.Meter.Name = DiagnosticNames.MeterName then
                    listener.EnableMeasurementEvents instrument

        meterListener.SetMeasurementEventCallback<int64>(fun instrument value tags _ ->
            let outcome =
                tags.ToArray()
                |> Array.tryPick (fun tag -> if tag.Key = "outcome" then Some(string tag.Value) else None)
                |> Option.defaultValue "missing"

            measurements.Add(instrument.Name, value, outcome))

        meterListener.Start()

        use activityListener = new ActivityListener()

        activityListener.ShouldListenTo <- fun source -> source.Name = DiagnosticNames.ActivitySourceName

        activityListener.Sample <- fun _ -> ActivitySamplingResult.AllDataAndRecorded

        activityListener.ActivityStopped <- Action<Activity>(activities.Add)
        ActivitySource.AddActivityListener activityListener

        use owner = new CancellationTokenSource()

        let result =
            use app = Composition.start (validConfig ()) ports logs.Add

            app.Place({ RequestId = "REQ-32"; Seats = 2 }, owner.Token).GetAwaiter().GetResult()

        Assert.True(Result.isOk result)
        Assert.Single appended |> ignore
        Assert.Equal<CancellationToken>([| owner.Token; owner.Token |], tokens)
        Assert.True(resource.IsDisposed)

        let log = Assert.Single logs
        Assert.Equal("booking.place", log.EventName)
        Assert.Equal("accepted", log.Outcome)
        Assert.Equal("REQ-32", log.RequestId)
        Assert.Equal(2, log.Seats)
        Assert.Equal("event-appended", log.Detail)

        Assert.Equal<(string * int64 * string)>([| DiagnosticNames.RequestCounterName, 1L, "accepted" |], measurements)

        let activity = Assert.Single activities
        Assert.Equal(DiagnosticNames.PlaceActivityName, activity.DisplayName)
        Assert.Equal("accepted", string (activity.GetTagItem "booking.outcome"))
        Assert.Equal("REQ-32", string (activity.GetTagItem "booking.request_id"))

    [<Fact>]
    let ``cancellation is observed before calling a port`` () =
        let resource = new TrackingResource()
        let mutable loadCalls = 0
        let logs = ResizeArray<BookingLog>()

        let ports =
            { LoadBooking =
                fun _ _ ->
                    loadCalls <- loadCalls + 1
                    Task.FromResult NotBooked
              AppendEvent = fun _ _ _ -> Task.FromResult()
              OwnedResource = resource }

        use owner = new CancellationTokenSource()
        owner.Cancel()

        use app = Composition.start (validConfig ()) ports logs.Add

        Assert.ThrowsAny<OperationCanceledException>(fun () ->
            app
                .Place(
                    { RequestId = "REQ-CANCELED"
                      Seats = 1 },
                    owner.Token
                )
                .GetAwaiter()
                .GetResult()
            |> ignore)
        |> ignore

        Assert.Equal(0, loadCalls)
        Assert.Equal("canceled", (Assert.Single logs).Outcome)
