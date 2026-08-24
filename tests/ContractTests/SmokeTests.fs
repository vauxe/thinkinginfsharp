namespace ThinkingInFSharp.ContractTests

open System.Text.Json
open Xunit

module SmokeTests =
    [<Fact>]
    let ``JSON contract tests execute on the pinned toolchain`` () =
        use document = JsonDocument.Parse("""{"name":"Ada","tickets":2}""")
        let root = document.RootElement

        Assert.Equal(JsonValueKind.Object, root.ValueKind)
        Assert.Equal("Ada", root.GetProperty("name").GetString())
        Assert.Equal(2, root.GetProperty("tickets").GetInt32())
