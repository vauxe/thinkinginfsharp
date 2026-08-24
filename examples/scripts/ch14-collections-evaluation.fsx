open System
open System.Collections.Generic

let ensureEqual label expected actual =
    if actual <> expected then
        failwithf "%s: expected %A, got %A" label expected actual

// #region eager-collections
let source = [ 1; 2; 3 ]
let doubledList = source |> List.map ((*) 2)
let doubledArray = source |> List.toArray |> Array.map ((*) 2)
doubledArray[0] <- 20

ensureEqual "list stays immutable" [ 2; 4; 6 ] doubledList
ensureEqual "array element changes" [| 20; 4; 6 |] doubledArray
ensureEqual "source stays unchanged" [ 1; 2; 3 ] source
printfn "Eager: list=%A array=%A source=%A" doubledList doubledArray source
// #endregion eager-collections

// #region repeated-enumeration
let mutable pulls = 0

let delayedSquares =
    seq {
        for value in 1..3 do
            pulls <- pulls + 1
            yield value * value
    }

ensureEqual "deferred before enumeration" 0 pulls
printfn "Deferred before enumeration: pulls=%d" pulls

let firstPass = delayedSquares |> Seq.toList
ensureEqual "first values" [ 1; 4; 9 ] firstPass
ensureEqual "first pass count" 3 pulls
printfn "First enumeration: values=%A pulls=%d" firstPass pulls

let secondPass = delayedSquares |> Seq.toList
ensureEqual "second values" firstPass secondPass
ensureEqual "second pass repeats production" 6 pulls
printfn "Second enumeration: values=%A pulls=%d" secondPass pulls
// #endregion repeated-enumeration

// #region cached-sequence
let mutable cachedPulls = 0

let cachedSquares =
    seq {
        for value in 1..3 do
            cachedPulls <- cachedPulls + 1
            yield value * value
    }
    |> Seq.cache

let cachedFirst = cachedSquares |> Seq.toList
let cachedSecond = cachedSquares |> Seq.toList

ensureEqual "cached values" cachedFirst cachedSecond
ensureEqual "cached production count" 3 cachedPulls
printfn "Cached enumerations: first=%A second=%A pulls=%d" cachedFirst cachedSecond cachedPulls
// #endregion cached-sequence

// #region ordered-collections
let uniqueSeats = [ 3; 1; 3; 2 ] |> Set.ofList

let bookingByCode =
    [ "B2", "first"
      "A1", "only"
      "B2", "replacement" ]
    |> Map.ofList

ensureEqual "set removes duplicates and orders" [ 1; 2; 3 ] (Set.toList uniqueSeats)
ensureEqual "later map binding replaces earlier" "replacement" bookingByCode["B2"]

printfn
    "Ordered collections: set=%A map=%A"
    (Set.toList uniqueSeats)
    (Map.toList bookingByCode)
// #endregion ordered-collections

// #region equality-only-key
[<CustomEquality; NoComparison>]
type EmailAddress =
    { Value: string }

    override this.Equals(other: obj) =
        match other with
        | :? EmailAddress as candidate ->
            StringComparer.OrdinalIgnoreCase.Equals(this.Value, candidate.Value)
        | _ -> false

    override this.GetHashCode() =
        StringComparer.OrdinalIgnoreCase.GetHashCode(this.Value)

let recipients = Dictionary<EmailAddress, string>()
recipients[{ Value = "lin@example.com" }] <- "first"
recipients[{ Value = "LIN@example.com" }] <- "second"

ensureEqual "hash equality replaces value" 1 recipients.Count
ensureEqual "case-insensitive lookup" "second" recipients[{ Value = "Lin@Example.com" }]
printfn "Hash dictionary: count=%d lookup=%s" recipients.Count recipients[{ Value = "Lin@Example.com" }]
// #endregion equality-only-key

// #region conversion-snapshot
let mutableArray = [| 1; 2; 3 |]
let listSnapshot = mutableArray |> Array.toList
mutableArray[0] <- 99

ensureEqual "list is an independent snapshot" [ 1; 2; 3 ] listSnapshot
printfn "Conversion snapshot: array=%A list=%A" mutableArray listSnapshot
// #endregion conversion-snapshot
