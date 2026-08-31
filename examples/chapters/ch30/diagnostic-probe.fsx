#load "../ch16/Domain.fs"
#load "../ch16/Workflow.fs"

open ThinkingInFSharp.Ch16
open ThinkingInFSharp.Ch16.Domain

let expectOk = function
    | Ok value -> value
    | Error error -> failwithf "invalid probe: %A" error

let capacity = Capacity.create 2 |> expectOk
let request = BookingRequest.create "B-30" 3 |> expectOk

Workflow.decide capacity request |> printfn "%A"
