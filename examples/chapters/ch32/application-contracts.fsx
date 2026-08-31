#load "Domain.fs"
#load "Ports.fs"
#load "Composition.fs"

open System
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Validation
open ThinkingInFSharp.Ch32

let ensureEqual label expected actual =
    if actual <> expected then
        failwithf "%s: expected %A, got %A" label expected actual

let config =
    AppConfig.load (function
        | AppConfig.EventIdSetting -> Some "EVT-32-CONTRACT"
        | AppConfig.CapacitySetting -> Some "4"
        | _ -> None)
    |> function
        | Ok value -> value
        | Error errors -> failwithf "invalid contract setup: %A" errors

let missingErrors =
    AppConfig.load (fun _ -> None)
    |> function
        | Error errors -> errors
        | Ok _ -> failwith "expected missing settings"

let missingNames =
    missingErrors
    |> List.map (function
        | MissingSetting name -> name
        | InvalidSetting(name, _) -> name)
    |> Set.ofList

ensureEqual "configuration error count" 2 missingErrors.Length

ensureEqual
    "configuration error names"
    (Set [ AppConfig.EventIdSetting; AppConfig.CapacitySetting ])
    missingNames

type TrackingResource() =
    let mutable disposed = false
    member _.IsDisposed = disposed
    interface IDisposable with
        member _.Dispose() = disposed <- true

let acceptedResource = new TrackingResource()
let acceptedTokens = ResizeArray<CancellationToken>()
let mutable loadCalls = 0
let mutable appendCalls = 0

let acceptedPorts =
    { LoadBooking =
        fun _ token ->
            loadCalls <- loadCalls + 1
            acceptedTokens.Add token
            Task.FromResult NotBooked
      AppendEvent =
        fun _ _ token ->
            appendCalls <- appendCalls + 1
            acceptedTokens.Add token
            Task.FromResult()
      OwnedResource = acceptedResource }

let tokenSource = new CancellationTokenSource()

let acceptedResult =
    use app = Composition.start config acceptedPorts ignore

    app.Place(
        { RequestId = "REQ-32-CONTRACT"
          Seats = 2 },
        tokenSource.Token
    )
    |> fun task -> task.GetAwaiter().GetResult()

ensureEqual "accepted result" true (Result.isOk acceptedResult)
ensureEqual "load calls" 1 loadCalls
ensureEqual "append calls" 1 appendCalls
ensureEqual "token count" 2 acceptedTokens.Count
ensureEqual "tokens forwarded" true (acceptedTokens |> Seq.forall ((=) tokenSource.Token))

let canceledResource = new TrackingResource()
let mutable canceledLoadCalls = 0
let mutable canceledAppendCalls = 0

let canceledPorts =
    { LoadBooking =
        fun _ _ ->
            canceledLoadCalls <- canceledLoadCalls + 1
            Task.FromResult NotBooked
      AppendEvent =
        fun _ _ _ ->
            canceledAppendCalls <- canceledAppendCalls + 1
            Task.FromResult()
      OwnedResource = canceledResource }

let canceledSource = new CancellationTokenSource()
canceledSource.Cancel()

let cancellationObserved =
    try
        use app = Composition.start config canceledPorts ignore

        app.Place(
            { RequestId = "REQ-32-CANCELED"
              Seats = 1 },
            canceledSource.Token
        )
        |> fun task -> task.GetAwaiter().GetResult()
        |> ignore

        false
    with :? OperationCanceledException ->
        true

ensureEqual "cancellation observed" true cancellationObserved
ensureEqual "canceled load calls" 0 canceledLoadCalls
ensureEqual "canceled append calls" 0 canceledAppendCalls
ensureEqual "accepted resource disposed" true acceptedResource.IsDisposed
ensureEqual "canceled resource disposed" true canceledResource.IsDisposed

tokenSource.Dispose()
canceledSource.Dispose()

printfn "Config errors: count=%d names=true" missingErrors.Length

printfn
    "Accepted: ok=%b load=%d append=%d same-token=true"
    (Result.isOk acceptedResult)
    loadCalls
    appendCalls

printfn
    "Canceled: observed=%b load=%d append=%d"
    cancellationObserved
    canceledLoadCalls
    canceledAppendCalls

printfn
    "Lifecycle: accepted-store=%b canceled-store=%b"
    acceptedResource.IsDisposed
    canceledResource.IsDisposed
