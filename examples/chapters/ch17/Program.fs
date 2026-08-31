namespace ThinkingInFSharp.Ch17

open ThinkingInFSharp.Ch17.SeatAllocation

module Program =
    let private getOk label result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "%s failed: %A" label error

    [<EntryPoint>]
    let main _ =
        let capacity = Capacity.create 6 |> getOk "capacity"
        let requested = SeatCount.create 4 |> getOk "requested seats"
        let allocation = allocate capacity requested |> getOk "allocation"

        printfn
            "allocated requested=%d remaining=%d"
            (allocation |> Allocation.requested |> SeatCount.value)
            (allocation |> Allocation.remaining)

        let tooMany = SeatCount.create 7 |> getOk "large request"

        match allocate capacity tooMany with
        | Error(InsufficientCapacity(requested, available)) ->
            printfn "rejected requested=%d available=%d" requested available
        | other ->
            failwithf "expected insufficient capacity, got %A" other

        0
