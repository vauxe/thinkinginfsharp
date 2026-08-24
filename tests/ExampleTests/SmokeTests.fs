namespace ThinkingInFSharp.ExampleTests

open Xunit

module SmokeTests =
    [<Fact>]
    let ``F# tests execute on the pinned toolchain`` () =
        let doubled = [ 1; 2; 3 ] |> List.map ((*) 2)

        Assert.Equal<int list>([ 2; 4; 6 ], doubled)
