namespace ThinkingInFSharp.ExampleTests

open System
open Xunit
open ThinkingInFSharp.AvaloniaSample
open ThinkingInFSharp.UnitySample

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

    [<Fact>]
    let ``Unity gameplay step is pure and exposes a CLR-friendly boundary`` () =
        let initial = Gameplay.Create 10.0f
        let moved = Gameplay.Step(initial, 2.0f, 6.0f, 0.5f)
        let stopped = Gameplay.Step(moved, 0.0f, 6.0f, 0.5f)

        Assert.Equal(13.0f, moved.PositionX)
        Assert.Equal(6.0f, moved.VelocityX)
        Assert.Equal(13.0f, stopped.PositionX)
        Assert.Equal(0.0f, stopped.VelocityX)

        let assembly = typeof<Gameplay>.Assembly
        let references = assembly.GetReferencedAssemblies()

        Assert.True(
            references |> Array.exists (fun reference -> reference.Name = "FSharp.Core"),
            "The plug-in assembly must declare its FSharp.Core dependency."
        )

        let rec isFSharpType (candidate: Type) =
            let belongsToFSharp =
                match candidate.Namespace with
                | null -> false
                | namespaceName -> namespaceName.StartsWith("Microsoft.FSharp", StringComparison.Ordinal)

            belongsToFSharp
            || (candidate.IsGenericType
                && (candidate.GetGenericArguments() |> Array.exists isFSharpType))

        let publicSignatureTypes =
            assembly.GetExportedTypes()
            |> Array.collect (fun exportedType ->
                [| for property in exportedType.GetProperties() do
                       property.PropertyType

                   for methodInfo in exportedType.GetMethods() do
                       methodInfo.ReturnType

                       for parameter in methodInfo.GetParameters() do
                           parameter.ParameterType |])

        Assert.False(
            publicSignatureTypes |> Array.exists isFSharpType,
            "The C# bridge must not expose F#-specific types in public signatures."
        )
