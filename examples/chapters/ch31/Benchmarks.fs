namespace ThinkingInFSharp.Ch31

open System
open BenchmarkDotNet.Attributes

// #region aggregation-implementations
module RequestAggregation =
    let arrayPipeline maxSeats (requests: int array) =
        requests
        |> Array.filter (fun seats -> seats > 0 && seats <= maxSeats)
        |> Array.sumBy int64

    let singlePass maxSeats (requests: int array) =
        let mutable total = 0L

        for seats in requests do
            if seats > 0 && seats <= maxSeats then
                total <- total + int64 seats

        total
// #endregion aggregation-implementations

// #region equivalence
module Equivalence =
    let private fixedCases =
        [| 0, [||]
           4, [| 1; 4; 5; 0; -1; 2 |]
           1, [| 1; 1; 2; -3 |]
           6, [| 6; 5; 4; 3; 2; 1 |] |]

    let verify () =
        let random = Random 31

        let generatedCases =
            Array.init 256 (fun length ->
                let maxSeats = random.Next(0, 8)
                let requests = Array.init length (fun _ -> random.Next(-2, 12))
                maxSeats, requests)

        Array.append fixedCases generatedCases
        |> Array.iteri (fun index (maxSeats, requests) ->
            let expected = RequestAggregation.arrayPipeline maxSeats requests
            let actual = RequestAggregation.singlePass maxSeats requests

            if actual <> expected then
                failwithf
                    "equivalence case %d failed: maxSeats=%d expected=%d actual=%d"
                    index
                    maxSeats
                    expected
                    actual)

        fixedCases.Length + generatedCases.Length
// #endregion equivalence

// #region benchmark
[<MemoryDiagnoser>]
type RequestAggregationBenchmarks() =
    let mutable requests = Array.empty<int>

    [<Params(256, 4096)>]
    member val Count = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        let random = Random 31
        requests <- Array.init this.Count (fun _ -> random.Next(-2, 12))

    [<Benchmark(Baseline = true)>]
    member _.ArrayPipeline() =
        RequestAggregation.arrayPipeline 6 requests

    [<Benchmark>]
    member _.SinglePass() =
        RequestAggregation.singlePass 6 requests
// #endregion benchmark
