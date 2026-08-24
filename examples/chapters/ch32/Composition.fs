namespace ThinkingInFSharp.Ch32

open System
open System.Collections.Generic
open System.Diagnostics
open System.Diagnostics.Metrics
open System.Threading
open Booking.Domain.Validation
open Booking.Domain.Workflow

// #region diagnostics-names
module DiagnosticNames =
    [<Literal>]
    let MeterName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let ActivitySourceName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let RequestCounterName = "booking.requests"

    [<Literal>]
    let PlaceActivityName = "booking.place"
// #endregion diagnostics-names

// #region application
type BookingApplication internal (event, ports: BookingPorts, writeLog: BookingLog -> unit) =
    let meter = new Meter(DiagnosticNames.MeterName, "1.0.0")

    let activities = new ActivitySource(DiagnosticNames.ActivitySourceName, "1.0.0")

    let requestCounter =
        meter.CreateCounter<int64>(DiagnosticNames.RequestCounterName, "{request}", "Completed booking attempts")

    let mutable disposed = 0

    let ensureActive () =
        if Volatile.Read(&disposed) <> 0 then
            raise (ObjectDisposedException(nameof BookingApplication))

    let setTag (activity: Activity | null) name (value: string) =
        match activity with
        | null -> ()
        | current -> current.SetTag(name, value) |> ignore

    let observe (activity: Activity | null) (command: PlaceBookingCommand) outcome detail =
        setTag activity "booking.outcome" outcome
        setTag activity "booking.request_id" command.RequestId

        requestCounter.Add(1L, KeyValuePair<string, obj | null>("outcome", box outcome))

        match activity with
        | null -> ()
        | current ->
            match outcome with
            | "faulted" -> current.SetStatus(ActivityStatusCode.Error, detail) |> ignore
            | "accepted"
            | "rejected" -> current.SetStatus(ActivityStatusCode.Ok) |> ignore
            | _ -> ()

        writeLog
            { EventName = DiagnosticNames.PlaceActivityName
              Outcome = outcome
              RequestId = command.RequestId
              Seats = command.Seats
              Detail = detail }

    member _.Place(command: PlaceBookingCommand, cancellationToken: CancellationToken) =
        task {
            ensureActive ()

            let activity =
                activities.StartActivity(DiagnosticNames.PlaceActivityName, ActivityKind.Internal)

            try
                try
                    cancellationToken.ThrowIfCancellationRequested()

                    match validatePlaceBooking command with
                    | Error errors ->
                        let failure = InvalidCommand errors
                        observe activity command "rejected" (sprintf "%A" failure)
                        return Error failure
                    | Ok validCommand ->
                        let requestId = ValidPlaceBooking.requestId validCommand
                        let! state = ports.LoadBooking requestId cancellationToken

                        match decidePlaceBooking event state command with
                        | Error failure ->
                            observe activity command "rejected" (sprintf "%A" failure)
                            return Error failure
                        | Ok bookingEvent ->
                            do! ports.AppendEvent requestId bookingEvent cancellationToken
                            observe activity command "accepted" "event-appended"
                            return Ok bookingEvent
                with
                | :? OperationCanceledException as error ->
                    observe activity command "canceled" "operation-canceled"
                    return raise error
                | error ->
                    observe activity command "faulted" (error.GetType().Name)
                    return raise error
            finally
                match activity with
                | null -> ()
                | current -> current.Dispose()
        }

    interface IDisposable with
        member _.Dispose() =
            if Interlocked.Exchange(&disposed, 1) = 0 then
                ports.OwnedResource.Dispose()
                activities.Dispose()
                meter.Dispose()
// #endregion application

// #region composition-root
module Composition =
    let start config ports writeLog =
        new BookingApplication(AppConfig.event config, ports, writeLog)
// #endregion composition-root
