open System

type BookingRequest = { Attendee: string; Seats: int }

type BookingError =
    | EmptyAttendee
    | NonPositiveSeats of actual: int
    | TooManySeats of requested: int * maximum: int

// #region option-lookup
let attendees = [ "B-101", "Lin"; "B-102", "Ada" ]

let tryFindAttendee bookingId =
    attendees |> List.tryFind (fun (id, _) -> id = bookingId) |> Option.map snd

let knownAttendee = tryFindAttendee "B-101" |> Option.defaultValue "none"

let missingAttendee = tryFindAttendee "B-999" |> Option.defaultValue "none"

printfn "Lookup: known=%s missing=%s" knownAttendee missingAttendee
// #endregion option-lookup

// #region option-composition
let requestedSeats = [ "B-101", 3; "B-102", 0 ]

let tryPositiveSeats seats = if seats > 0 then Some seats else None

let tryRequestedSeats bookingId =
    requestedSeats
    |> List.tryFind (fun (id, _) -> id = bookingId)
    |> Option.map snd
    |> Option.bind tryPositiveSeats

let positiveSeats =
    tryRequestedSeats "B-101" |> Option.map string |> Option.defaultValue "none"

let nonPositiveSeats =
    tryRequestedSeats "B-102" |> Option.map string |> Option.defaultValue "none"

printfn "Option bind: positive=%s nonPositive=%s" positiveSeats nonPositiveSeats
// #endregion option-composition

// #region result-validation
let validateAttendee request =
    if String.IsNullOrWhiteSpace request.Attendee then
        Error EmptyAttendee
    else
        Ok request

let validateSeats maximum request =
    if request.Seats <= 0 then
        Error(NonPositiveSeats request.Seats)
    elif request.Seats > maximum then
        Error(TooManySeats(request.Seats, maximum))
    else
        Ok request

let validate maximum request =
    request |> validateAttendee |> Result.bind (validateSeats maximum)

let describeError error =
    match error with
    | EmptyAttendee -> "attendee is empty"
    | NonPositiveSeats actual -> $"seat count {actual} is not positive"
    | TooManySeats(requested, maximum) -> $"requested {requested} exceeds maximum {maximum}"

let describeResult result =
    match result with
    | Ok request -> $"ok:{request.Attendee}:{request.Seats}"
    | Error error -> $"error:{describeError error}"

let validRequest = { Attendee = "Lin"; Seats = 2 }

let emptyAttendeeRequest = { Attendee = ""; Seats = 2 }

printfn
    "Validation: success=%s failure=%s"
    (validate 4 validRequest |> describeResult)
    (validate 4 emptyAttendeeRequest |> describeResult)
// #endregion result-validation

// #region error-context
type RequestFailure =
    { RequestId: string
      Cause: BookingError }

let addRequestContext requestId result =
    result
    |> Result.mapError (fun error -> { RequestId = requestId; Cause = error })

let oversizedRequest = { Attendee = "Ada"; Seats = 6 }

let contextualFailure = oversizedRequest |> validate 4 |> addRequestContext "R-9"

match contextualFailure with
| Ok _ -> printfn "Context: unexpected success"
| Error failure -> printfn "Context: %s -> %s" failure.RequestId (describeError failure.Cause)
// #endregion error-context

// #region result-short-circuit
let doublyInvalidRequest = { Attendee = ""; Seats = 0 }

printfn "Short circuit: %s" (validate 4 doublyInvalidRequest |> describeResult)
// #endregion result-short-circuit

// #region some-null
let riskyPayload: (string | null) option = Some null

let payloadIsNull =
    match riskyPayload with
    | Some value -> isNull value
    | None -> false

printfn "Some null: isSome=%b payloadIsNull=%b" riskyPayload.IsSome payloadIsNull
// #endregion some-null
