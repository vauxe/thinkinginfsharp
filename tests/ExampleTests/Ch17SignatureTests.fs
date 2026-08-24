namespace ThinkingInFSharp.ExampleTests

open ThinkingInFSharp.Ch17.SeatAllocation
open Xunit

module Ch17SignatureTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    [<Fact>]
    let ``capacity is constructed and observed through its public module`` () =
        Assert.Equal(Error(NonPositiveCapacity 0), Capacity.create 0)
        Assert.Equal(Error(NonPositiveCapacity -2), Capacity.create -2)

        let capacity = Capacity.create 4 |> expectOk
        Assert.Equal(4, Capacity.value capacity)

    [<Fact>]
    let ``seat count is constructed and observed through its public module`` () =
        Assert.Equal(Error(NonPositiveSeatCount 0), SeatCount.create 0)

        let seats = SeatCount.create 3 |> expectOk
        Assert.Equal(3, SeatCount.value seats)

    [<Fact>]
    let ``allocation exposes observations without exposing its representation`` () =
        let capacity = Capacity.create 4 |> expectOk
        let requested = SeatCount.create 3 |> expectOk
        let allocation = allocate capacity requested |> expectOk

        Assert.Equal(4, allocation |> Allocation.capacity |> Capacity.value)
        Assert.Equal(3, allocation |> Allocation.requested |> SeatCount.value)
        Assert.Equal(1, Allocation.remaining allocation)

    [<Fact>]
    let ``allocation preserves the capacity invariant on rejection`` () =
        let capacity = Capacity.create 4 |> expectOk
        let requested = SeatCount.create 5 |> expectOk

        Assert.Equal(Error(InsufficientCapacity(requested = 5, available = 4)), allocate capacity requested)
