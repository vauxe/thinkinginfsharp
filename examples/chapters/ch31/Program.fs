namespace ThinkingInFSharp.Ch31

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running

module Program =
    let private benchmarkConfig job =
        ManualConfig.Create(DefaultConfig.Instance).AddJob([| job |])

    [<EntryPoint>]
    let main arguments =
        let verifiedCases = Equivalence.verify ()

        if Array.contains "--verify-only" arguments then
            printfn "Equivalence cases: %d" verifiedCases
        else
            let job =
                if Array.contains "--smoke" arguments then
                    Job.Dry.WithId("Dry")
                else
                    Job.ShortRun.WithId("ShortRun")

            BenchmarkRunner.Run<RequestAggregationBenchmarks>(benchmarkConfig job)
            |> ignore

        0
