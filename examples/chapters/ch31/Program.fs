namespace ThinkingInFSharp.Ch31

open BenchmarkDotNet.Running

module Program =
    let private run arguments =
        let summaries =
            BenchmarkSwitcher.FromTypes([| typeof<RequestAggregationBenchmarks> |]).Run(arguments)
            |> Seq.toArray

        if
            summaries.Length = 0
            || summaries
               |> Array.exists (fun summary ->
                   summary.HasCriticalValidationErrors
                   || summary.Reports |> Seq.exists (fun report -> not report.Success))
        then
            1
        else
            0

    [<EntryPoint>]
    let main arguments =
        try
            let checkedCases = Equivalence.verify ()
            printfn "Functional equivalence: %d cases passed" checkedCases

            match arguments with
            | [| "--verify-only" |] -> 0
            | [| "--smoke" |] ->
                printfn "Benchmark mode: Dry smoke (execution proof, not a baseline)"
                run [| "--job"; "dry"; "--filter"; "*" |]
            | [||] ->
                printfn "Benchmark mode: ShortRun baseline"
                run [| "--job"; "short"; "--filter"; "*" |]
            | forwarded -> run forwarded
        with error ->
            eprintfn "%s" error.Message
            1
