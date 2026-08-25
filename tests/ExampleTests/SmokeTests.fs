namespace ThinkingInFSharp.ExampleTests

open System
open System.Reflection
open System.Reflection.Emit
open Xunit
open ThinkingInFSharp.AvaloniaSample
open ThinkingInFSharp.UnitySample

module SmokeTests =
    let private opCodes =
        typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static)
        |> Array.map (fun field ->
            let opCode = field.GetValue null :?> OpCode
            int opCode.Value &&& 0xFFFF, opCode)
        |> Map.ofArray

    let private readOpCodes (methodInfo: MethodInfo) =
        let body =
            match methodInfo.GetMethodBody() with
            | null -> failwithf "Expected %s to have a managed method body." methodInfo.Name
            | value -> value

        let il =
            match body.GetILAsByteArray() with
            | null -> failwithf "Expected %s to contain IL." methodInfo.Name
            | bytes -> bytes

        let operandSize offset (opCode: OpCode) =
            match opCode.OperandType with
            | OperandType.InlineNone -> 0
            | OperandType.ShortInlineBrTarget
            | OperandType.ShortInlineI
            | OperandType.ShortInlineVar -> 1
            | OperandType.InlineVar -> 2
            | OperandType.InlineBrTarget
            | OperandType.InlineField
            | OperandType.InlineI
            | OperandType.InlineMethod
            | OperandType.ShortInlineR
            | OperandType.InlineSig
            | OperandType.InlineString
            | OperandType.InlineTok
            | OperandType.InlineType -> 4
            | OperandType.InlineI8
            | OperandType.InlineR -> 8
            | OperandType.InlineSwitch -> 4 + BitConverter.ToInt32(il, offset) * 4
            | unsupported -> failwithf "Unsupported IL operand type %A." unsupported

        let rec decode offset decoded =
            if offset >= il.Length then
                List.rev decoded
            else
                let key, operandOffset =
                    if il[offset] = 0xFEuy then
                        0xFE00 ||| int il[offset + 1], offset + 2
                    else
                        int il[offset], offset + 1

                let opCode = opCodes[key]
                decode (operandOffset + operandSize operandOffset opCode) (opCode :: decoded)

        decode 0 []

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
        Assert.True(typeof<MotionState>.IsValueType, "The fixed-step state must not allocate a class per tick.")

        let step =
            match
                typeof<Gameplay>
                    .GetMethod(
                        "Step",
                        BindingFlags.Public ||| BindingFlags.Static,
                        null,
                        [| typeof<MotionState>; typeof<single>; typeof<single>; typeof<single> |],
                        null
                    )
            with
            | null -> failwith "Expected the CLR-facing Gameplay.Step method."
            | methodInfo -> methodInfo

        Assert.DoesNotContain(OpCodes.Box, readOpCodes step)

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
