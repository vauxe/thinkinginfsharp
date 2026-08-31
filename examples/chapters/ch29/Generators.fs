namespace ThinkingInFSharp.Ch29

open FsCheck
open FsCheck.FSharp

type AllocationCaseError =
    | NegativeCapacity of capacity: int
    | NonPositiveRequest of seats: int

type AllocationCase =
    private
        { Capacity: int
          Requests: int list }

module AllocationCase =
    let create capacity requests =
        if capacity < 0 then
            Error(NegativeCapacity capacity)
        else
            match requests |> List.tryFind (fun seats -> seats <= 0) with
            | Some seats -> Error(NonPositiveRequest seats)
            | None ->
                Ok
                    { Capacity = capacity
                      Requests = requests }

    let capacity sample = sample.Capacity
    let requests sample = sample.Requests

    let internal assumeValid capacity requests =
        match create capacity requests with
        | Ok sample -> sample
        | Error error -> invalidArg (nameof requests) $"invalid allocation case: {error}"

type Decision =
    | Accepted of seats: int
    | Rejected of seats: int

type Allocation =
    { Decisions: Decision list
      Remaining: int }

module SeatAllocation =
    let allocate sample =
        let folder (remaining, decisions) request =
            if request <= remaining then
                remaining - request, Accepted request :: decisions
            else
                remaining, Rejected request :: decisions

        let remaining, reversedDecisions =
            ((sample.Capacity, []), sample.Requests) ||> List.fold folder

        { Decisions = List.rev reversedDecisions
          Remaining = remaining }

module AllocationProperties =
    let private requestedSeats decision =
        match decision with
        | Accepted seats
        | Rejected seats -> seats

    let conservesCapacity sample =
        let allocation = SeatAllocation.allocate sample

        let acceptedSeats =
            allocation.Decisions
            |> List.sumBy (function
                | Accepted seats -> int64 seats
                | Rejected _ -> 0L)

        acceptedSeats + int64 allocation.Remaining = int64 sample.Capacity

    let preservesRequests sample =
        let actual =
            sample |> SeatAllocation.allocate |> _.Decisions |> List.map requestedSeats

        actual = sample.Requests

    let remainingIsBounded sample =
        let remaining = (SeatAllocation.allocate sample).Remaining
        0 <= remaining && remaining <= sample.Capacity

    let isOversubscribed sample =
        (sample.Requests |> List.sumBy int64) > int64 sample.Capacity

    // Plausible, but false: a rejected large request can be followed by a smaller accepted one.
    let acceptedRequestsFormPrefix sample =
        sample
        |> SeatAllocation.allocate
        |> _.Decisions
        |> List.fold
            (fun (stillValid, hasRejected) decision ->
                match decision with
                | Accepted _ -> stillValid && not hasRejected, hasRejected
                | Rejected _ -> stillValid, true)
            (true, false)
        |> fst

module private AllocationCaseGen =
    let private general size =
        let largest = max 1 (min 40 (size + 1))
        let longest = min 12 size

        gen {
            let! capacity = Gen.choose (0, largest)
            let! length = Gen.choose (0, longest)
            let! requests = Gen.choose (1, largest + 1) |> Gen.listOfLength length
            return AllocationCase.assumeValid capacity requests
        }

    let private rejectionThenFit size =
        let largest = max 1 (min 40 (size + 1))

        gen {
            let! capacity = Gen.choose (1, largest)
            let! tooLarge = Gen.choose (capacity + 1, capacity + largest)
            let! fits = Gen.choose (1, capacity)
            return AllocationCase.assumeValid capacity [ tooLarge; fits ]
        }

    let generator =
        Gen.sized (fun size -> Gen.frequency [ 4, general size; 1, rejectionThenFit size ])

module private AllocationCaseShrink =
    let private removeEach requests =
        requests
        |> List.indexed
        |> Seq.map (fun (index, _) -> List.removeAt index requests)

    let private shrinkOneRequest requests =
        seq {
            for index, request in List.indexed requests do
                for smaller in 1 .. request - 1 do
                    yield List.updateAt index smaller requests
        }

    let shrink sample =
        seq {
            for requests in removeEach sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests

            for capacity in 0 .. sample.Capacity - 1 do
                yield AllocationCase.assumeValid capacity sample.Requests

            for requests in shrinkOneRequest sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests
        }
        |> Seq.distinct

type AllocationCaseArbitrary =
    static member AllocationCase() : Arbitrary<AllocationCase> =
        Arb.fromGenShrink(
            AllocationCaseGen.generator,
            AllocationCaseShrink.shrink
        )
