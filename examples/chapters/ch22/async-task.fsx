open System.Threading.Tasks

let newGate<'T> () =
    TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

let asyncEntered = newGate<bool> ()
let asyncRelease = newGate<unit> ()

let deferredAsync =
    async {
        asyncEntered.SetResult true
        do! Async.AwaitTask asyncRelease.Task
        return "async-done"
    }

assert (not asyncEntered.Task.IsCompleted)
printfn "Async before start: entered=false"

let runningAsync = Async.StartAsTask deferredAsync
asyncEntered.Task.GetAwaiter().GetResult() |> ignore

assert asyncEntered.Task.IsCompleted
assert (not runningAsync.IsCompleted)
printfn "Async after StartAsTask: entered=true completed=false"

asyncRelease.SetResult()
let asyncResult = runningAsync.GetAwaiter().GetResult()
assert (asyncResult = "async-done")
printfn "Async result: %s" asyncResult

let taskEntered = newGate<bool> ()
let taskRelease = newGate<unit> ()

let immediateTask () =
    task {
        taskEntered.SetResult true
        do! taskRelease.Task
        return "task-done"
    }

let runningTask = immediateTask ()

assert taskEntered.Task.IsCompleted
assert (not runningTask.IsCompleted)
printfn "Task after call: entered=true completed=false"

taskRelease.SetResult()
let taskResult = runningTask.GetAwaiter().GetResult()
assert (taskResult = "task-done")
printfn "Task result: %s" taskResult

let taskFromAsync = async { return 21 } |> Async.StartAsTask

let asyncFromTask = task { return 42 } |> Async.AwaitTask

let fromAsync = taskFromAsync.GetAwaiter().GetResult()
let fromTask = Async.RunSynchronously asyncFromTask

assert (fromAsync = 21)
assert (fromTask = 42)
printfn "Interop: async-to-task=%d task-to-async=%d" fromAsync fromTask
