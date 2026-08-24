namespace ThinkingInFSharp.Tests

open FsCheck
open FsCheck.FSharp
open global.FsCheck.Xunit
open ThinkingInFSharp.Ch29
open global.Xunit

type private CapturingRunner() =
    let mutable result = None

    member _.Result = result

    interface IRunner with
        member _.OnStartFixture _ = ()
        member _.OnArguments(_, _, _) = ()
        member _.OnShrink(_, _) = ()
        member _.OnFinished(_, testResult) = result <- Some testResult

[<Properties(Arbitrary = [| typeof<AllocationCaseArbitrary> |], QuietOnSuccess = true)>]
module Ch29Properties =
    // #region passing-properties
    [<Property(MaxTest = 300)>]
    let ``allocation conserves capacity`` (sample: AllocationCase) =
        AllocationProperties.conservesCapacity sample

    [<Property(MaxTest = 300)>]
    let ``allocation preserves every request in order`` (sample: AllocationCase) =
        AllocationProperties.preservesRequests sample

    [<Property(MaxTest = 300)>]
    let ``remaining capacity stays within bounds`` (sample: AllocationCase) =
        AllocationProperties.remainingIsBounded sample
        |> Prop.classify (AllocationCase.requests sample |> List.isEmpty) "empty"
        |> Prop.classify (AllocationProperties.isOversubscribed sample) "oversubscribed"
    // #endregion passing-properties

    // #region fixed-replay
    [<Fact>]
    let ``fixed replay disproves the plausible prefix property`` () =
        let runner = CapturingRunner()

        let config =
            Config.Quick
                .WithMaxTest(300)
                .WithArbitrary([ typeof<AllocationCaseArbitrary> ])
                .WithReplay(13285693176119930639UL, 18364232908344279255UL, 4)
                .WithRunner(runner)

        Check.One("accepted requests form a prefix", config, AllocationProperties.acceptedRequestsFormPrefix)

        match runner.Result with
        | Some(TestResult.Failed(data, _, shrunkArguments, _, _, _, _)) ->
            let shrunk = shrunkArguments |> List.exactlyOne |> unbox<AllocationCase>

            Assert.True(data.NumberOfShrinks > 0, "the counterexample should be shrunk")
            Assert.Equal(1, AllocationCase.capacity shrunk)
            Assert.Equal<int list>([ 2; 1 ], AllocationCase.requests shrunk)
        | Some result ->
            let report = Runner.onFinishedToString "accepted requests form a prefix" result
            Assert.Fail($"expected a falsified property, got: {report}")
        | None -> Assert.Fail("FsCheck did not report a result")
    // #endregion fixed-replay
