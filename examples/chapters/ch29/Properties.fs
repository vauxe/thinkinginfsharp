namespace ThinkingInFSharp.Ch29

open FsCheck
open FsCheck.FSharp
open global.FsCheck.Xunit
open global.Xunit

[<Properties(
    Arbitrary = [| typeof<AllocationCaseArbitrary> |],
    QuietOnSuccess = true
)>]
module Ch29Properties =
    [<Property(MaxTest = 300)>]
    let ``allocation conserves capacity`` (sample: AllocationCase) =
        AllocationProperties.conservesCapacity sample

    [<Property(MaxTest = 300)>]
    let ``allocation preserves requests and order`` (sample: AllocationCase) =
        AllocationProperties.preservesRequests sample

    [<Property(MaxTest = 300)>]
    let ``remaining capacity stays within bounds`` (sample: AllocationCase) =
        AllocationProperties.remainingIsBounded sample
        |> Prop.classify
            (AllocationCase.requests sample |> List.isEmpty)
            "empty"
        |> Prop.classify
            (AllocationProperties.isOversubscribed sample)
            "oversubscribed"

type private CollectingRunner() =
    let mutable result = None

    member _.Result = result

    interface IRunner with
        member _.OnStartFixture _ = ()
        member _.OnArguments(_, _, _) = ()
        member _.OnShrink(_, _) = ()
        member _.OnFinished(_, finishedResult) = result <- Some finishedResult

type CounterexampleTests() =
    [<Fact>]
    member _.``false prefix property shrinks to the policy counterexample``() =
        let runner = CollectingRunner()

        let config =
            Config.Quick
                .WithMaxTest(300)
                .WithArbitrary([ typeof<AllocationCaseArbitrary> ])
                .WithReplay(13285693176119930639UL, 18364232908344279255UL, 4)
                .WithRunner(runner)

        Check.One(
            "accepted requests form a prefix",
            config,
            AllocationProperties.acceptedRequestsFormPrefix
        )

        match runner.Result with
        | Some(TestResult.Failed(data, _, shrunkArguments, _, _, _, _)) ->
            let shrunk = shrunkArguments |> List.exactlyOne |> unbox<AllocationCase>
            Assert.True(data.NumberOfShrinks > 0)
            Assert.Equal(1, AllocationCase.capacity shrunk)
            Assert.Equal<int list>([ 2; 1 ], AllocationCase.requests shrunk)
        | _ -> Assert.Fail("expected a falsified property")
