namespace ThinkingInFSharp.ExampleTests

open Xunit
open ThinkingInFSharp.AvaloniaSample

module SmokeTests =
    [<Fact>]
    let ``F# tests execute on the pinned toolchain`` () =
        let doubled = [ 1; 2; 3 ] |> List.map ((*) 2)

        Assert.Equal<int list>([ 2; 4; 6 ], doubled)

    [<Fact>]
    let ``Avalonia counter keeps state transitions outside the view`` () =
        let initial = Counter.initial

        let afterAdds =
            [ AddSeat; AddSeat; AddSeat ]
            |> List.fold (fun state message -> Counter.update message state) initial

        let afterReset = Counter.update Reset afterAdds
        let afterUnderflow = Counter.update RemoveSeat initial

        Assert.Equal(0, initial.Seats)
        Assert.Equal(3, afterAdds.Seats)
        Assert.Equal(0, afterReset.Seats)
        Assert.Equal(0, afterUnderflow.Seats)
