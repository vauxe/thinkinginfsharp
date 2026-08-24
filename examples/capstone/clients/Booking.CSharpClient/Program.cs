using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Booking.Contracts;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static Uri ReadBaseAddress(string[] arguments)
{
    var raw = arguments.Length > 0
        ? arguments[0]
        : Environment.GetEnvironmentVariable("BOOKING_API_URL") ?? "http://127.0.0.1:5088/";

    if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
        || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        || !string.IsNullOrEmpty(parsed.Query)
        || !string.IsNullOrEmpty(parsed.Fragment))
    {
        throw new ArgumentException("The API base address must be an absolute HTTP(S) URI without a query or fragment.");
    }

    var builder = new UriBuilder(parsed)
    {
        Path = parsed.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? parsed.AbsolutePath
            : $"{parsed.AbsolutePath}/"
    };

    return builder.Uri;
}

static async Task<(HttpStatusCode Status, string Body, BookingDto Booking)> ReadBooking(
    HttpResponseMessage response,
    JsonSerializerOptions json)
{
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Booking API returned {(int)response.StatusCode} ({response.StatusCode}): {body}",
            inner: null,
            response.StatusCode);
    }

    var booking = JsonSerializer.Deserialize<BookingDto>(body, json)
        ?? throw new InvalidOperationException("The Booking API returned an empty booking body.");

    return (response.StatusCode, body, booking);
}

var baseAddress = ReadBaseAddress(args);
var requestId = args.Length > 1 ? args[1].Trim() : "REQ-CSHARP";
Require(requestId.Length > 0, "The request ID must not be blank.");

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    MaxDepth = 8
};

using var client = new HttpClient
{
    BaseAddress = baseAddress,
    Timeout = TimeSpan.FromSeconds(10)
};

// #region csharp-http-contract-client
var place = new PlaceBookingDto
{
    RequestId = requestId,
    Seats = 2
};

using var placedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var placed = await ReadBooking(placedResponse, json);
Require(placed.Status == HttpStatusCode.Created, "Place must return 201 Created.");
Require(placed.Booking.RequestId == requestId, "Place request ID round-trip.");
Require(placed.Booking.Seats == 2, "Place seat count round-trip.");
Require(placed.Booking.Status == "pending", "Placed booking must be pending.");

using var replayedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var replayed = await ReadBooking(replayedResponse, json);
Require(replayed.Status == HttpStatusCode.Created, "Exact replay must return the acknowledged status.");
Require(replayed.Body == placed.Body, "Exact replay must return the acknowledged booking.");

var confirm = new ConfirmBookingDto
{
    RequestId = requestId,
    ConfirmationCode = "CONF-CSHARP"
};

using var confirmedResponse = await client.PostAsJsonAsync("api/bookings/confirm", confirm, json);
var confirmed = await ReadBooking(confirmedResponse, json);
Require(confirmed.Status == HttpStatusCode.OK, "Confirm must return 200 OK.");
Require(confirmed.Booking.Status == "confirmed", "Confirmed booking status.");
Require(confirmed.Booking.ConfirmationCode == "CONF-CSHARP", "Confirmation code round-trip.");

var escapedRequestId = Uri.EscapeDataString(requestId);
using var loadedResponse = await client.GetAsync($"api/bookings/{escapedRequestId}");
var loaded = await ReadBooking(loadedResponse, json);
Require(loaded.Body == confirmed.Body, "GET must return the current confirmed booking.");
// #endregion csharp-http-contract-client

Console.WriteLine($"Placed: id={placed.Booking.RequestId} seats={placed.Booking.Seats} status={placed.Booking.Status}");
Console.WriteLine($"Replay: status={(int)replayed.Status} same-body={replayed.Body == placed.Body}");
Console.WriteLine(
    $"Confirmed: id={confirmed.Booking.RequestId} code={confirmed.Booking.ConfirmationCode} status={confirmed.Booking.Status}");
Console.WriteLine($"Loaded: status={(int)loaded.Status} same-body={loaded.Body == confirmed.Body}");
