type BookingDraft =
    { Attendee: string
      RequestedSeats: int
      Channel: string option }

let trimAttendee draft =
    { draft with
        Attendee = draft.Attendee.Trim() }

let capSeats maximum draft =
    { draft with
        RequestedSeats = min maximum draft.RequestedSeats }

let addChannel channel draft =
    { draft with Channel = Some channel }

let toLabel draft =
    let channel = draft.Channel |> Option.defaultValue "unknown"
    $"{draft.Attendee}:{draft.RequestedSeats}:{channel}"

let rawDraft =
    { Attendee = "  Lin  "
      RequestedSeats = 6
      Channel = None }

// #region repeated-nesting
let nestedLabel =
    toLabel (addChannel "web" (capSeats 4 (trimAttendee rawDraft)))

printfn "Nested: %s" nestedLabel
// #endregion repeated-nesting

// #region pipeline
let pipedLabel =
    rawDraft
    |> trimAttendee
    |> capSeats 4
    |> addChannel "web"
    |> toLabel

printfn "Pipeline matches nested: %b" (pipedLabel = nestedLabel)
// #endregion pipeline

// #region composition
let prepareLabel =
    trimAttendee
    >> capSeats 4
    >> addChannel "web"
    >> toLabel

let prepareLabelBackward =
    toLabel
    << addChannel "web"
    << capSeats 4
    << trimAttendee

printfn "Forward composition: %s" (prepareLabel rawDraft)
printfn "Backward composition: %s" (prepareLabelBackward rawDraft)
// #endregion composition

// #region parameter-order
let deskLabel =
    { Attendee = "  Mira "
      RequestedSeats = 2
      Channel = None }
    |> trimAttendee
    |> capSeats 4
    |> addChannel "desk"
    |> toLabel

printfn "Configured pipeline: %s" deskLabel
// #endregion parameter-order

// #region direct-call
let fitsWithin capacity requested = requested <= capacity

let requested = 3
let capacity = 4
let fits = fitsWithin capacity requested

printfn "Direct predicate: requested=%d capacity=%d fits=%b" requested capacity fits
// #endregion direct-call
