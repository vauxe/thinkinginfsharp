namespace ThinkingInFSharp.Ecosystem.Data

open System
open System.Globalization
open FSharp.Data

// #region data-sample-results
type RegionSummary =
    { Region: string
      OrderCount: int
      Units: int
      Revenue: decimal }

type HighValueOrder =
    { OrderId: string
      Region: string
      OrderedAt: DateOnly
      Revenue: decimal }
// #endregion data-sample-results

[<RequireQualifiedAccess>]
module DataSample =
    [<Literal>]
    let private ResolutionFolder = __SOURCE_DIRECTORY__

    // #region data-sample-provider
    type private Orders =
        CsvProvider<
            "../../../tests/ContentFixtures/data/sample.csv",
            ResolutionFolder=ResolutionFolder,
            Culture="en-US",
            PreferDateOnly=true
         >
    // #endregion data-sample-provider

    let private revenue (row: Orders.Row) = decimal row.Units * row.UnitPrice

    // #region data-sample-sequence-query
    let summarizeByRegion (path: string) : RegionSummary list =
        Orders.Load(path).Rows
        |> Seq.groupBy _.Region
        |> Seq.map (fun (region, rows) ->
            let rows = Seq.toArray rows

            ({ Region = region
               OrderCount = rows.Length
               Units = rows |> Array.sumBy _.Units
               Revenue = rows |> Array.sumBy revenue }
            : RegionSummary))
        |> Seq.sortByDescending _.Revenue
        |> Seq.toList
    // #endregion data-sample-sequence-query

    // #region data-sample-query-expression
    let highValueOrders (minimumRevenue: decimal) (path: string) : HighValueOrder list =
        query {
            for row in Orders.Load(path).Rows do
                let rowRevenue = revenue row
                where (rowRevenue >= minimumRevenue)
                sortByDescending rowRevenue

                select (
                    { OrderId = row.OrderId
                      Region = row.Region
                      OrderedAt = row.OrderedAt
                      Revenue = rowRevenue }
                    : HighValueOrder
                )
        }
        |> Seq.toList
// #endregion data-sample-query-expression

module Program =
    [<EntryPoint>]
    let main arguments =
        match arguments with
        | [| path |] ->
            try
                let absolutePath = IO.Path.GetFullPath path

                for summary in DataSample.summarizeByRegion absolutePath do
                    let revenue = summary.Revenue.ToString("0.00", CultureInfo.InvariantCulture)

                    printfn "%s: orders=%d units=%d revenue=%s" summary.Region summary.OrderCount summary.Units revenue

                0
            with error ->
                eprintfn "Could not analyze the CSV file: %s" error.Message
                1
        | _ ->
            eprintfn "Usage: DataSample <orders.csv>"
            2
