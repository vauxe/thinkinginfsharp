namespace ThinkingInFSharp.Ch16

open ThinkingInFSharp.Ch16.Domain
open ThinkingInFSharp.Ch16.Workflow

module Program =
    let summary () =
        match Capacity.create 4, BookingRequest.create "REQ-16" 3 with
        | Ok capacity, Ok request -> request |> decide capacity |> describe
        | Error _, _ -> "invalid-capacity"
        | _, Error _ -> "invalid-request"

    [<EntryPoint>]
    let main _ =
        printfn "%s" (summary ())
        0
