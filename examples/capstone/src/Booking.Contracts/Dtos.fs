namespace Booking.Contracts

open System
open System.Text.Json
open System.Text.Json.Serialization

[<RequireQualifiedAccess>]
module BookingContract =
    [<Literal>]
    let CurrentSchemaVersion = 1

// CLI record shape: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html
// #region command-dtos
[<CLIMutable>]
type PlaceBookingDto =
    { [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("seats")>]
      Seats: Nullable<int> }

[<CLIMutable>]
type ConfirmBookingDto =
    { [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("confirmationCode")>]
      ConfirmationCode: string | null }

[<CLIMutable>]
type CancelBookingDto =
    { [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("reason")>]
      Reason: string | null }
// #endregion command-dtos

// #region booking-dto
[<CLIMutable>]
type BookingDto =
    { [<JsonPropertyName("schemaVersion")>]
      SchemaVersion: int
      [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("eventId")>]
      EventId: string | null
      [<JsonPropertyName("seats")>]
      Seats: Nullable<int>
      [<JsonPropertyName("status")>]
      Status: string | null
      [<JsonPropertyName("confirmationCode")>]
      ConfirmationCode: string | null
      [<JsonPropertyName("cancellationReason")>]
      CancellationReason: string | null }
// #endregion booking-dto

// #region json-options
module BookingJson =
    // Wire names: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties
    // Unmapped data: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members
    let configure (options: JsonSerializerOptions) =
        ArgumentNullException.ThrowIfNull(options, nameof options)
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.PropertyNameCaseInsensitive <- false
        options.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.MaxDepth <- 8

    let private options =
        let settings = JsonSerializerOptions()
        configure settings
        settings

    let serializeBooking (dto: BookingDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserializeBooking (json: string) : BookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<BookingDto>(json, options)

    let deserializePlaceBooking (json: string) : PlaceBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceBookingDto>(json, options)

    let deserializeConfirmBooking (json: string) : ConfirmBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<ConfirmBookingDto>(json, options)

    let deserializeCancelBooking (json: string) : CancelBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<CancelBookingDto>(json, options)
// #endregion json-options
