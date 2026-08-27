namespace ThinkingInFSharp.Ch32

open System
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Workflow

// #region configuration
type ConfigError =
    | MissingSetting of name: string
    | InvalidSetting of name: string * value: string

type AppConfig = private { Event: Event }

module AppConfig =
    [<Literal>]
    let EventIdSetting = "BOOKING_EVENT_ID"

    [<Literal>]
    let CapacitySetting = "BOOKING_CAPACITY"

    let private readEventId (lookup: string -> string option) =
        match lookup EventIdSetting with
        | None -> Error [ MissingSetting EventIdSetting ]
        | Some raw ->
            EventId.create raw
            |> Result.mapError (fun _ -> [ InvalidSetting(EventIdSetting, raw) ])

    let private readCapacity (lookup: string -> string option) =
        match lookup CapacitySetting with
        | None -> Error [ MissingSetting CapacitySetting ]
        | Some raw ->
            match Int32.TryParse raw with
            | true, value ->
                Capacity.create value
                |> Result.mapError (fun _ -> [ InvalidSetting(CapacitySetting, raw) ])
            | false, _ -> Error [ InvalidSetting(CapacitySetting, raw) ]

    let load lookup =
        match readEventId lookup, readCapacity lookup with
        | Ok eventId, Ok capacity -> Ok { Event = Event.create eventId capacity }
        | Error eventErrors, Error capacityErrors -> Error(eventErrors @ capacityErrors)
        | Error errors, Ok _
        | Ok _, Error errors -> Error errors

    let event config = config.Event
// #endregion configuration

// #region ports
type BookingPorts =
    { LoadBooking: RequestId -> CancellationToken -> Task<BookingState>
      AppendEvent: RequestId -> BookingEvent -> CancellationToken -> Task<unit>
      OwnedResource: IDisposable }

type BookingLog =
    { EventName: string
      Outcome: string
      RequestId: string
      Seats: int
      Detail: string }
// #endregion ports

// #region in-memory-adapter
type InMemoryBookingStore() =
    let syncRoot = obj ()
    let mutable state = NotBooked
    let mutable disposed = false

    let ensureActive () =
        if disposed then
            raise (ObjectDisposedException(nameof InMemoryBookingStore))

    member this.AsPorts() =
        { LoadBooking =
            fun _ cancellationToken ->
                cancellationToken.ThrowIfCancellationRequested()

                lock syncRoot (fun () ->
                    ensureActive ()
                    Task.FromResult state)
          AppendEvent =
            fun _ bookingEvent cancellationToken ->
                cancellationToken.ThrowIfCancellationRequested()

                lock syncRoot (fun () ->
                    ensureActive ()
                    state <- evolve state bookingEvent
                    Task.FromResult())
          OwnedResource = this :> IDisposable }

    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
// #endregion in-memory-adapter
