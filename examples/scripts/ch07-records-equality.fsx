// #region record-definition
type BookingDraft =
    { EventId: string
      Attendee: string
      Seats: int }

let original =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }
// #endregion record-definition

// #region copy-update
let updated = { original with Seats = 3 }

printfn "Record update: original=%d updated=%d" original.Seats updated.Seats
// #endregion copy-update

// #region anonymous-record
let summary =
    {| updated with
        IsGroup = updated.Seats > 1 |}

printfn "Anonymous summary: %s -> %d seats, group=%b" summary.Attendee summary.Seats summary.IsGroup
// #endregion anonymous-record

// #region equality-identity-hash
let equalCopy =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let alias = original
let structurallyEqual = original = equalCopy
let physicallyEqual = LanguagePrimitives.PhysicalEquality original equalCopy
let aliasIsSameReference = LanguagePrimitives.PhysicalEquality original alias
let equalHashesAgree = hash original = hash equalCopy

printfn "Equality: structural=%b physical=%b alias=%b" structurallyEqual physicallyEqual aliasIsSameReference
printfn "Hashes agree for equal records: %b" equalHashesAgree
// #endregion equality-identity-hash

// #region structural-comparison
let drafts =
    [ { EventId = "B-2"
        Attendee = "Lin"
        Seats = 2 }
      { EventId = "A-1"
        Attendee = "Lin"
        Seats = 1 }
      { EventId = "A-1"
        Attendee = "Ada"
        Seats = 2 } ]

let sortedLabels =
    drafts
    |> List.sort
    |> List.map (fun draft -> $"{draft.EventId}:{draft.Attendee}:{draft.Seats}")

printfn "Structural sort: %A" sortedLabels
// #endregion structural-comparison
