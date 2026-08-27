namespace ThinkingInFSharp.Ch32

open System
open System.Collections.Generic
open System.Diagnostics
open System.Diagnostics.Metrics
open System.Text.Json
open System.Threading
open Booking.Domain.Validation

module Program =
    let private fixedLookup name =
        match name with
        | AppConfig.EventIdSetting -> Some "EVT-32"
        | AppConfig.CapacitySetting -> Some "4"
        | _ -> None

    let private environmentLookup name =
        match Environment.GetEnvironmentVariable name with
        | null -> None
        | value -> Some value

    let private run lookup =
        match AppConfig.load lookup with
        | Error errors ->
            eprintfn "configuration: %A" errors
            2
        | Ok config ->
            let measurements = ResizeArray<string * int64 * string>()
            let completedActivities = ResizeArray<Activity>()

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

            activityListener.ActivityStopped <- Action<Activity>(completedActivities.Add)
            ActivitySource.AddActivityListener activityListener

            let jsonOptions =
                new JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

            let writeLog record =
                JsonSerializer.Serialize(record, jsonOptions) |> printfn "%s"

            let store = new InMemoryBookingStore()

            let result =
                use app = Composition.start config (store.AsPorts()) writeLog

                app.Place({ RequestId = "REQ-32"; Seats = 2 }, CancellationToken.None).GetAwaiter().GetResult()

            printfn "result: accepted=%b" (Result.isOk result)

            match measurements |> Seq.tryExactlyOne with
            | Some(name, value, outcome) -> printfn "metric: name=%s value=%d outcome=%s" name value outcome
            | None -> printfn "metric: unexpected-count=%d" measurements.Count

            match completedActivities |> Seq.tryExactlyOne with
            | Some activity ->
                printfn "trace: name=%s outcome=%O" activity.DisplayName (activity.GetTagItem "booking.outcome")
            | None -> printfn "trace: unexpected-count=%d" completedActivities.Count

            printfn "lifecycle: store-disposed=%b" store.IsDisposed
            0

    [<EntryPoint>]
    let main arguments =
        if arguments = [| "--demo" |] then
            run fixedLookup
        else
            run environmentLookup
