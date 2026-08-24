namespace ThinkingInFSharp.Ch27

open System

// #region public-request
/// <summary>Identifies whether a booking request was accepted or rejected.</summary>
type BookingOutcome =
    /// <summary>No booking outcome has been assigned.</summary>
    | None = 0
    /// <summary>The booking was accepted and has a confirmation code.</summary>
    | Accepted = 1
    /// <summary>The booking was rejected and has an error message.</summary>
    | Rejected = 2

/// <summary>Input supplied by a .NET caller when evaluating a booking.</summary>
/// <param name="requestId">A non-null request identifier. Blank identifiers are rejected by <c>Evaluate</c>.</param>
/// <param name="attendee">A non-null attendee name. Blank names are rejected by <c>Evaluate</c>.</param>
/// <param name="seats">The number of seats requested.</param>
/// <exception cref="System.ArgumentNullException"><paramref name="requestId"/> or <paramref name="attendee"/> is <see langword="null"/>.</exception>
[<Sealed>]
type BookingRequest(requestId: string, attendee: string, seats: int) =
    do
        ArgumentNullException.ThrowIfNull(requestId, nameof requestId)
        ArgumentNullException.ThrowIfNull(attendee, nameof attendee)

    /// <summary>Gets the request identifier exactly as supplied.</summary>
    member _.RequestId = requestId

    /// <summary>Gets the attendee name exactly as supplied.</summary>
    member _.Attendee = attendee

    /// <summary>Gets the requested seat count.</summary>
    member _.Seats = seats
// #endregion public-request

// #region internal-model
type internal Decision =
    | Accepted of confirmationCode: string * remainingSeats: int
    | Rejected of message: string * suggestedSeats: int option

module internal Decision =
    let evaluate capacity (request: BookingRequest) =
        if String.IsNullOrWhiteSpace request.RequestId then
            Rejected("request id must not be blank", None)
        elif String.IsNullOrWhiteSpace request.Attendee then
            Rejected("attendee must not be blank", None)
        elif request.Seats <= 0 then
            Rejected("seat count must be positive", None)
        elif request.Seats > capacity then
            let suggestion = if capacity > 0 then Some capacity else None

            Rejected($"requested {request.Seats} exceeds available {capacity}", suggestion)
        else
            let normalizedRequestId = request.RequestId.Trim().ToUpperInvariant()
            Accepted($"CONF-{normalizedRequestId}", capacity - request.Seats)
// #endregion internal-model

// #region public-response
/// <summary>A C#-friendly projection of the internal F# booking decision.</summary>
/// <remarks>
/// Accepted responses have a confirmation code and remaining-seat count.
/// Rejected responses have an error message and may have a suggested seat count.
/// </remarks>
[<Sealed>]
type BookingResponse
    internal
    (
        outcome: BookingOutcome,
        confirmationCode: string | null,
        remainingSeats: Nullable<int>,
        errorMessage: string | null,
        suggestedSeats: Nullable<int>
    ) =
    /// <summary>Gets the accepted or rejected outcome.</summary>
    member _.Outcome = outcome

    /// <summary>Gets whether this response represents an accepted booking.</summary>
    member _.IsAccepted = outcome = BookingOutcome.Accepted

    /// <summary>Gets the confirmation code, or <see langword="null"/> when rejected.</summary>
    member _.ConfirmationCode = confirmationCode

    /// <summary>Gets remaining capacity, or <see langword="null"/> when rejected.</summary>
    member _.RemainingSeats = remainingSeats

    /// <summary>Gets the rejection message, or <see langword="null"/> when accepted.</summary>
    member _.ErrorMessage = errorMessage

    /// <summary>Gets a capacity-based suggestion when available; otherwise <see langword="null"/>.</summary>
    member _.SuggestedSeats = suggestedSeats
// #endregion public-response

// #region boundary-adapter
module internal ResponseAdapter =
    let fromDecision decision =
        match decision with
        | Accepted(confirmationCode, remainingSeats) ->
            BookingResponse(BookingOutcome.Accepted, confirmationCode, Nullable remainingSeats, null, Nullable<int>())
        | Rejected(message, suggestedSeats) ->
            let suggestion =
                match suggestedSeats with
                | Some seats -> Nullable seats
                | None -> Nullable<int>()

            BookingResponse(BookingOutcome.Rejected, null, Nullable<int>(), message, suggestion)
// #endregion boundary-adapter

// #region public-api
/// <summary>Provides the stable .NET entry point for booking decisions.</summary>
[<AbstractClass; Sealed>]
type BookingApi private () =
    /// <summary>Evaluates one request against the supplied available capacity.</summary>
    /// <param name="capacity">Available seats. Negative capacity is invalid configuration.</param>
    /// <param name="request">A non-null request to evaluate.</param>
    /// <returns>A response projected into ordinary .NET enum, class, string, and nullable-value members.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    static member Evaluate(capacity: int, request: BookingRequest) =
        ArgumentNullException.ThrowIfNull(request, nameof request)

        if capacity < 0 then
            raise (ArgumentOutOfRangeException(nameof capacity, capacity, "Capacity cannot be negative."))

        request |> Decision.evaluate capacity |> ResponseAdapter.fromDecision
// #endregion public-api
