let bookingCapacity = 8

// The first slice starts from fixed token rows so it needs no files, network,
// exceptions, records, or domain unions. Later slices will replace this boundary.
let rawRows =
    [ [ "B-101"; "Lin"; "3" ]
      [ "B-102"; "Ada"; "2" ]
      [ "malformed" ]
      [ "B-999"; "Noa"; "many" ]
      [ "B-103"; "Sam"; "4" ]
      [ "B-104"; "Mira"; "2" ] ]

// This fixed grammar is deliberately not a general integer parser.
let tryParseSeatCount seatText =
    match seatText with
    | "1" -> Some 1
    | "2" -> Some 2
    | "3" -> Some 3
    | "4" -> Some 4
    | _ -> None

let tryParseRow tokens =
    match tokens with
    | [ bookingId; attendee; seatText ] when bookingId <> "" && attendee <> "" ->
        match tryParseSeatCount seatText with
        | Some requestedSeats -> Some(bookingId, attendee, requestedSeats)
        | None -> None
    | _ -> None

let parsedBookings = rawRows |> List.choose tryParseRow
let invalidRowCount = List.length rawRows - List.length parsedBookings

let formatBooking (bookingId, attendee, requestedSeats) =
    $"{bookingId}:{attendee}:{requestedSeats}"

let bookingLabels = parsedBookings |> List.map formatBooking

let summarizeCapacity capacity bookings =
    let folder (acceptedRev, rejectedRev, bookedSeats) ((_, _, requestedSeats) as booking) =
        if requestedSeats <= capacity - bookedSeats then
            booking :: acceptedRev, rejectedRev, bookedSeats + requestedSeats
        else
            acceptedRev, booking :: rejectedRev, bookedSeats

    let acceptedRev, rejectedRev, bookedSeats =
        bookings |> List.fold folder ([], [], 0)

    List.rev acceptedRev,
    List.rev rejectedRev,
    bookedSeats,
    capacity - bookedSeats

let acceptedBookings, rejectedBookings, bookedSeats, remainingSeats =
    summarizeCapacity bookingCapacity parsedBookings

let bookingIds bookings =
    bookings |> List.map (fun (bookingId, _, _) -> bookingId)

#if INTERACTIVE
printfn "Rows: valid=%d invalid=%d" (List.length parsedBookings) invalidRowCount
printfn "Labels: %A" bookingLabels
printfn "Accepted IDs: %A" (bookingIds acceptedBookings)
printfn "Rejected IDs: %A" (bookingIds rejectedBookings)
printfn "Capacity: booked=%d remaining=%d" bookedSeats remainingSeats
#endif
