namespace Booking.Domain.Testing

open System
open System.Threading
open System.Threading.Tasks

type RecordedCall<'Input> =
    { Input: 'Input
      CancellationToken: CancellationToken }

type ControlledOperation<'Input, 'Output>() =
    let syncRoot = obj ()
    let calls = ResizeArray<RecordedCall<'Input>>()

    let entered =
        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    let completion =
        TaskCompletionSource<'Output>(TaskCreationOptions.RunContinuationsAsynchronously)

    member _.Calls =
        lock syncRoot (fun () -> calls |> Seq.toList)

    member _.Entered = entered.Task

    member _.Invoke input (cancellationToken: CancellationToken) =
        task {
            lock syncRoot (fun () ->
                calls.Add
                    { Input = input
                      CancellationToken = cancellationToken })

            entered.TrySetResult() |> ignore

            use _registration =
                cancellationToken.Register(fun () ->
                    completion.TrySetCanceled(cancellationToken) |> ignore)

            return! completion.Task
        }

    member _.Succeed output =
        completion.TrySetResult output

    member _.Fail(error: exn) =
        completion.TrySetException error
