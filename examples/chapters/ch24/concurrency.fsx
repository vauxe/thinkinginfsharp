open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

let newGate<'T> () =
    TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

let startLongRunning (action: unit -> unit) =
    Task.Factory.StartNew(
        Action action,
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default
    )

let runTwoWithBarrier action =
    use barrier = new Barrier(2)
    let first = startLongRunning (fun () -> action barrier)
    let second = startLongRunning (fun () -> action barrier)
    Task.WaitAll [| first; second |]

let releaseWaits = newGate<unit> ()
let entered = [| 0 |]

let waitingWork label =
    task {
        Interlocked.Increment(&entered[0]) |> ignore
        do! releaseWaits.Task
        return label
    }

let firstWait = waitingWork "first"
let secondWait = waitingWork "second"
let bothPending = not firstWait.IsCompleted && not secondWait.IsCompleted

assert (entered[0] = 2)
assert bothPending
printfn "Concurrent waits: entered=%d pending=%b" entered[0] bothPending

releaseWaits.SetResult()

let waitResults =
    let running = Task.WhenAll [| firstWait; secondWait |]
    running.GetAwaiter().GetResult()

assert (waitResults = [| "first"; "second" |])
printfn "Concurrent results: %A" waitResults

let values = [| 1..8 |]
let sequentialSquares = values |> Array.map (fun value -> value * value)
let parallelSquares = values |> Array.Parallel.map (fun value -> value * value)
let parallelAgrees = parallelSquares = sequentialSquares

assert parallelAgrees
printfn "Parallel map agrees: %b" parallelAgrees

let racyCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    let snapshot = racyCounter[0]
    barrier.SignalAndWait() |> ignore
    racyCounter[0] <- snapshot + 1)

let lockedCounter = [| 0 |]
let counterLock = obj ()

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore
    lock counterLock (fun () -> lockedCounter[0] <- lockedCounter[0] + 1))

let atomicCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore
    Interlocked.Increment(&atomicCounter[0]) |> ignore)

assert (racyCounter[0] = 1)
assert (lockedCounter[0] = 2)
assert (atomicCounter[0] = 2)

printfn
    "Shared counter: race=%d lock=%d interlocked=%d"
    racyCounter[0]
    lockedCounter[0]
    atomicCounter[0]

type CapacityState =
    { mutable Remaining: int
      mutable Accepted: int }

let capacity = { Remaining = 3; Accepted = 0 }
let capacityLock = obj ()

let tryReserve seats =
    lock capacityLock (fun () ->
        if seats > 0 && seats <= capacity.Remaining then
            capacity.Remaining <- capacity.Remaining - seats
            capacity.Accepted <- capacity.Accepted + 1
            true
        else
            false)

let reservationResults = Array.zeroCreate<bool> 2
let reservationIndex = [| -1 |]

runTwoWithBarrier (fun barrier ->
    let index = Interlocked.Increment(&reservationIndex[0])
    barrier.SignalAndWait() |> ignore
    reservationResults[index] <- tryReserve 2)

let acceptedReservations = reservationResults |> Array.filter id |> Array.length
let capacityInvariant = capacity.Remaining = 1 && capacity.Accepted = 1

assert (acceptedReservations = 1)
assert capacityInvariant

printfn
    "Locked capacity: accepted=%d remaining=%d invariant=%b"
    acceptedReservations
    capacity.Remaining
    capacityInvariant

type ReservationReply =
    | Accepted of remaining: int
    | Rejected of remaining: int

type ReservationMessage =
    | Reserve of seats: int * reply: AsyncReplyChannel<ReservationReply>
    | Stop of reply: AsyncReplyChannel<int * int>

let reservationAgent =
    MailboxProcessor.Start(fun inbox ->
        let rec loop remaining accepted =
            async {
                let! message = inbox.Receive()

                match message with
                | Reserve(seats, reply) when seats > 0 && seats <= remaining ->
                    let nextRemaining = remaining - seats
                    reply.Reply(Accepted nextRemaining)
                    return! loop nextRemaining (accepted + 1)
                | Reserve(_, reply) ->
                    reply.Reply(Rejected remaining)
                    return! loop remaining accepted
                | Stop reply -> reply.Reply(remaining, accepted)
            }

        loop 3 0)

let agentReplies =
    [| reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply))
       reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply)) |]
    |> Async.Parallel
    |> Async.RunSynchronously

let agentAccepted =
    agentReplies
    |> Array.filter (function
        | Accepted _ -> true
        | Rejected _ -> false)
    |> Array.length

let agentRemaining, agentAcceptedState = reservationAgent.PostAndReply Stop

assert (agentAccepted = 1)
assert (agentRemaining = 1)
assert (agentAcceptedState = 1)

printfn
    "Agent capacity: accepted=%d remaining=%d invariant=%b"
    agentAccepted
    agentRemaining
    (agentAcceptedState = agentAccepted)

reservationAgent.Dispose()

let cache = ConcurrentDictionary<string, Lazy<int>>()
let computations = [| 0 |]

let getCached key =
    cache.GetOrAdd(
        key,
        fun _ ->
            lazy
                (Interlocked.Increment(&computations[0]) |> ignore
                 23)
    )
    |> fun delayed -> delayed.Value

let cacheBarrier = new Barrier(2)

let cachedTasks =
    [| startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore)
       startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore) |]

Task.WaitAll cachedTasks
let cachedValues = [| getCached "quote"; getCached "quote" |]

assert (cachedValues = [| 23; 23 |])
assert (computations[0] = 1)
assert (cache.Count = 1)
printfn "Cache: values=%A computations=%d entries=%d" cachedValues computations[0] cache.Count
cacheBarrier.Dispose()
