namespace ThinkingInFSharp.ExampleTests

open System
open System.IO
open ThinkingInFSharp.Ecosystem.Data
open Xunit

module DataSampleTests =
    let private fixturePath =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../ContentFixtures/data/sample.csv"))

    [<Fact>]
    let ``typed rows aggregate into deterministic regional summaries`` () =
        let actual = DataSample.summarizeByRegion fixturePath

        let expected: RegionSummary list =
            [ { Region = "South"
                OrderCount = 2
                Units = 3
                Revenue = 400.00M }
              { Region = "West"
                OrderCount = 2
                Units = 8
                Revenue = 390.00M }
              { Region = "North"
                OrderCount = 2
                Units = 6
                Revenue = 251.00M } ]

        Assert.Equal<RegionSummary list>(expected, actual)

    [<Fact>]
    let ``query expression filters and orders high value rows`` () =
        let actual = DataSample.highValueOrders 200M fixturePath

        let expected: HighValueOrder list =
            [ { OrderId = "ORD-1004"
                Region = "West"
                OrderedAt = DateOnly(2026, 1, 5)
                Revenue = 330.00M }
              { OrderId = "ORD-1002"
                Region = "South"
                OrderedAt = DateOnly(2026, 1, 3)
                Revenue = 240.00M } ]

        Assert.Equal<HighValueOrder list>(expected, actual)
