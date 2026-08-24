namespace ThinkingInFSharp.ContractTests

open System
open System.Collections.Generic
open ThinkingInFSharp.Ch19.NullBoundaries
open Xunit

module Ch19NullTests =
    [<Fact>]
    let ``typed calls select constructors members overloads and interfaces`` () =
        let uri = createAbsoluteUri "https://example.com/books/fsharp"
        let items: IReadOnlyCollection<int> = [| 1; 2; 3 |]

        Assert.Equal("example.com", uriHost uri)
        Assert.Equal("zh / en", joinLabels [| "zh"; "en" |])
        Assert.Equal(3, countItems items)

    [<Fact>]
    let ``nullable reference input is rejected or normalized at the boundary`` () =
        Assert.Equal(Error MissingText, requireText null)
        Assert.Equal(Error BlankText, requireText "   ")
        Assert.Equal(Ok "F#", requireText " F# ")

    [<Fact>]
    let ``real dotnet nullable return becomes an option`` () =
        Assert.Equal(Some typeof<string>, tryResolveType "System.String")
        Assert.Equal(None, tryResolveType "ThinkingInFSharp.TypeThatDoesNotExist")

    [<Fact>]
    let ``nullable value types convert in both directions`` () =
        Assert.Equal(None, nullableIntToOption (Nullable<int>()))
        Assert.Equal(Some 4, nullableIntToOption (Nullable 4))

        let absent = optionToNullableInt None
        let present = optionToNullableInt (Some 4)

        Assert.False(absent.HasValue)
        Assert.True(present.HasValue)
        Assert.Equal(4, present.Value)

    [<Fact>]
    let ``nullable references convert in both directions`` () =
        Assert.Equal(None, nullableTextToOption null)
        Assert.Equal(Some "F#", nullableTextToOption "F#")
        Assert.Null(optionToNullableText None)
        Assert.Equal("F#", optionToNullableText (Some "F#"))

    [<Fact>]
    let ``option does not make a nullable payload non-null`` () =
        match someNullText with
        | Some payload -> Assert.Null(payload)
        | None -> failwith "Expected Some null, received None"
