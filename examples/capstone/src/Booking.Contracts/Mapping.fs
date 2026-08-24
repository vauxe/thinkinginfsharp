namespace Booking.Contracts

open System
open Booking.Domain

// #region mapping-errors
[<RequireQualifiedAccess>]
type DtoMappingError =
    | MissingBody
    | UnsupportedSchemaVersion of actual: int
    | MissingRequestId
    | MissingEventId
    | MissingSeats
    | MissingStatus
    | MissingConfirmationCode
    | MissingCancellationReason
    | InvalidRequestId of RequestIdError
    | InvalidEventId of EventIdError
    | InvalidSeatCount of SeatCountError
    | InvalidConfirmationCode of ConfirmationCodeError
    | InvalidCancellationReason of CancellationReasonError
    | UnknownStatus of actual: string
    | UnexpectedConfirmationCode of status: string
    | UnexpectedCancellationReason of status: string
// #endregion mapping-errors

module private MappingInternals =
    let requestId (raw: string | null) =
        match raw with
        | null -> Error DtoMappingError.MissingRequestId
        | value -> RequestId.create value |> Result.mapError DtoMappingError.InvalidRequestId

    let eventId (raw: string | null) =
        match raw with
        | null -> Error DtoMappingError.MissingEventId
        | value -> EventId.create value |> Result.mapError DtoMappingError.InvalidEventId

    let rawSeats (raw: Nullable<int>) =
        if raw.HasValue then
            Ok raw.Value
        else
            Error DtoMappingError.MissingSeats

    let seats raw =
        rawSeats raw
        |> Result.bind (fun value -> SeatCount.create value |> Result.mapError DtoMappingError.InvalidSeatCount)

    let confirmationCode (raw: string | null) =
        match raw with
        | null -> Error DtoMappingError.MissingConfirmationCode
        | value ->
            ConfirmationCode.create value
            |> Result.mapError DtoMappingError.InvalidConfirmationCode

    let cancellationReason (raw: string | null) =
        match raw with
        | null -> Error DtoMappingError.MissingCancellationReason
        | value ->
            CancellationReason.create value
            |> Result.mapError DtoMappingError.InvalidCancellationReason

    let status (dto: BookingDto) =
        match dto.Status with
        | null -> Error DtoMappingError.MissingStatus
        | "pending" ->
            match dto.ConfirmationCode, dto.CancellationReason with
            | null, null -> Ok Pending
            | null, _ -> Error(DtoMappingError.UnexpectedCancellationReason "pending")
            | _, _ -> Error(DtoMappingError.UnexpectedConfirmationCode "pending")
        | "confirmed" ->
            confirmationCode dto.ConfirmationCode
            |> Result.bind (fun code ->
                match dto.CancellationReason with
                | null -> Ok(Confirmed code)
                | _ -> Error(DtoMappingError.UnexpectedCancellationReason "confirmed"))
        | "cancelled" ->
            cancellationReason dto.CancellationReason
            |> Result.bind (fun reason ->
                match dto.ConfirmationCode with
                | null -> Ok(Cancelled reason)
                | _ -> Error(DtoMappingError.UnexpectedConfirmationCode "cancelled"))
        | unknown -> Error(DtoMappingError.UnknownStatus unknown)

// #region command-mapping
module PlaceBookingMapping =
    let ofDomain (command: PlaceBooking) : PlaceBookingDto =
        { RequestId = command.RequestId
          Seats = Nullable command.Seats }

    let toDomain (dto: PlaceBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId with
            | null -> Error DtoMappingError.MissingRequestId
            | requestId -> MappingInternals.rawSeats value.Seats |> Result.map (Commands.place requestId)

module ConfirmBookingMapping =
    let ofDomain (command: ConfirmBooking) : ConfirmBookingDto =
        { RequestId = command.RequestId
          ConfirmationCode = command.ConfirmationCode }

    let toDomain (dto: ConfirmBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.ConfirmationCode with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingConfirmationCode
            | requestId, confirmationCode -> Ok(Commands.confirm requestId confirmationCode)

module CancelBookingMapping =
    let ofDomain (command: CancelBooking) : CancelBookingDto =
        { RequestId = command.RequestId
          Reason = command.Reason }

    let toDomain (dto: CancelBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.Reason with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingCancellationReason
            | requestId, reason -> Ok(Commands.cancel requestId reason)
// #endregion command-mapping

// #region snapshot-mapping
module BookingMapping =
    let ofDomain (booking: Booking) : BookingDto =
        let nullableText (value: string) : string | null = value
        let noText: string | null = null

        let status, confirmationCode, cancellationReason =
            match Booking.status booking with
            | Pending -> "pending", noText, noText
            | Confirmed code -> "confirmed", code |> ConfirmationCode.value |> nullableText, noText
            | Cancelled reason -> "cancelled", noText, reason |> CancellationReason.value |> nullableText

        { SchemaVersion = BookingContract.CurrentSchemaVersion
          RequestId = booking |> Booking.requestId |> RequestId.value
          EventId = booking |> Booking.eventId |> EventId.value
          Seats = booking |> Booking.seats |> SeatCount.value |> int |> Nullable
          Status = status
          ConfirmationCode = confirmationCode
          CancellationReason = cancellationReason }

    let toDomain (dto: BookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value when value.SchemaVersion <> BookingContract.CurrentSchemaVersion ->
            Error(DtoMappingError.UnsupportedSchemaVersion value.SchemaVersion)
        | value ->
            MappingInternals.requestId value.RequestId
            |> Result.bind (fun requestId ->
                MappingInternals.eventId value.EventId
                |> Result.map (fun eventId -> requestId, eventId))
            |> Result.bind (fun (requestId, eventId) ->
                MappingInternals.seats value.Seats
                |> Result.map (fun seats -> requestId, eventId, seats))
            |> Result.bind (fun (requestId, eventId, seats) ->
                MappingInternals.status value
                |> Result.map (Booking.restore requestId eventId seats))
// #endregion snapshot-mapping
