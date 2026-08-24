namespace Booking.Api

open System
open System.Globalization
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Infrastructure
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core

type private StartupConfiguration =
    { Store: BookingStoreConfiguration
      Activity: Event }

[<RequireQualifiedAccess>]
type private StartupConfigurationError =
    | InvalidStore
    | InvalidEventId
    | InvalidCapacity

module private StartupConfiguration =
    [<Literal>]
    let EventIdEnvironmentVariable = "BOOKING_EVENT_ID"

    [<Literal>]
    let CapacityEnvironmentVariable = "BOOKING_CAPACITY"

    let private environmentOrDefault name fallback =
        match Environment.GetEnvironmentVariable name with
        | null -> fallback
        | value -> value

    let load () =
        let eventId =
            environmentOrDefault EventIdEnvironmentVariable "EVT-LOCAL"
            |> EventId.create
            |> Result.mapError (fun _ -> StartupConfigurationError.InvalidEventId)

        let capacity =
            let raw = environmentOrDefault CapacityEnvironmentVariable "8"

            match Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed ->
                Capacity.create parsed
                |> Result.mapError (fun _ -> StartupConfigurationError.InvalidCapacity)
            | false, _ -> Error StartupConfigurationError.InvalidCapacity

        match BookingStoreConfiguration.fromEnvironment (), eventId, capacity with
        | Error _, _, _ -> Error StartupConfigurationError.InvalidStore
        | _, Error error, _ -> Error error
        | _, _, Error error -> Error error
        | Ok store, Ok validEventId, Ok validCapacity ->
            Ok
                { Store = store
                  Activity = Event.create validEventId validCapacity }

module Program =
    let private errorCode error =
        match error with
        | StartupConfigurationError.InvalidStore -> "invalid_booking_store"
        | StartupConfigurationError.InvalidEventId -> "invalid_event_id"
        | StartupConfigurationError.InvalidCapacity -> "invalid_capacity"

    // #region api-host
    [<EntryPoint>]
    let main arguments =
        match StartupConfiguration.load () with
        | Error error ->
            eprintfn "Booking API startup configuration is invalid (%s)." (errorCode error)
            2
        | Ok configuration ->
            let builder = WebApplication.CreateBuilder arguments

            builder.WebHost.ConfigureKestrel(
                Action<KestrelServerOptions>(fun options ->
                    options.AddServerHeader <- false
                    options.Limits.MaxRequestBodySize <- int64 BookingEndpoints.MaxRequestBodyBytes)
            )
            |> ignore

            let clock (cancellationToken: CancellationToken) =
                cancellationToken.ThrowIfCancellationRequested()
                Task.FromResult DateTimeOffset.UtcNow

            use infrastructure =
                Composition.start
                    configuration.Store
                    (PaymentStubBehavior.Authorize "TX-LOCAL-STUB")
                    NotificationStubBehavior.Deliver
                    clock

            use application = builder.Build()

            BookingEndpoints.map
                application
                { Activity = configuration.Activity
                  Ports = infrastructure.Ports }

            application.Run()
            0
// #endregion api-host
