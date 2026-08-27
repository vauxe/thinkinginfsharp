using System.Reflection;
using ThinkingInFSharp.Ch27;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static bool ContainsFSharpSpecificType(Type type)
{
    var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    return definition.Namespace?.StartsWith("Microsoft.FSharp", StringComparison.Ordinal) == true
        || type.GetGenericArguments().Any(ContainsFSharpSpecificType)
        || (type.HasElementType
            && type.GetElementType() is { } elementType
            && ContainsFSharpSpecificType(elementType));
}

static IEnumerable<Type> GetPublicSignatureTypes(Type type)
{
    const BindingFlags flags =
        BindingFlags.Public
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    foreach (var constructor in type.GetConstructors(flags))
    {
        foreach (var parameter in constructor.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    foreach (var method in type.GetMethods(flags))
    {
        yield return method.ReturnType;

        foreach (var parameter in method.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    foreach (var property in type.GetProperties(flags))
    {
        yield return property.PropertyType;
    }
}

// #region accepted-call
var accepted = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-27", attendee: "Lin", seats: 2));

Require(accepted.Outcome == BookingOutcome.Accepted, "accepted outcome");
Require(default(BookingOutcome) == BookingOutcome.None, "valid enum zero value");
Require(accepted.IsAccepted, "accepted flag");
Require(accepted.ConfirmationCode == "CONF-REQ-27", "confirmation code");
Require(accepted.RemainingSeats == 3, "remaining seats");
Require(accepted.ErrorMessage is null, "accepted error must be null");
Require(accepted.SuggestedSeats is null, "accepted suggestion must be null");

Console.WriteLine(
    $"Accepted: outcome={accepted.Outcome} code={accepted.ConfirmationCode} remaining={accepted.RemainingSeats}");
// #endregion accepted-call

// #region rejected-call
var rejected = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-28", attendee: "Ada", seats: 8));

Require(rejected.Outcome == BookingOutcome.Rejected, "rejected outcome");
Require(!rejected.IsAccepted, "rejected flag");
Require(rejected.ConfirmationCode is null, "rejected code must be null");
Require(rejected.RemainingSeats is null, "rejected remaining must be null");
Require(rejected.ErrorMessage == "requested 8 exceeds available 5", "capacity error");
Require(rejected.SuggestedSeats == 5, "capacity suggestion");

Console.WriteLine(
    $"Rejected: outcome={rejected.Outcome} message={rejected.ErrorMessage} suggested={rejected.SuggestedSeats}");
// #endregion rejected-call

var invalid = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-29", attendee: "Mira", seats: 0));

Require(invalid.Outcome == BookingOutcome.Rejected, "invalid outcome");
Require(invalid.ErrorMessage == "seat count must be positive", "seat validation");
Require(invalid.SuggestedSeats is null, "invalid suggestion must be null");

Console.WriteLine(
    $"Invalid: outcome={invalid.Outcome} message={invalid.ErrorMessage} suggested=none");

var nullGuarded = false;

try
{
    _ = new BookingRequest(requestId: null!, attendee: "Lin", seats: 1);
}
catch (ArgumentNullException error) when (error.ParamName == "requestId")
{
    nullGuarded = true;
}

Require(nullGuarded, "requestId null guard");

var requestGuarded = false;

try
{
    _ = BookingApi.Evaluate(capacity: 5, request: null!);
}
catch (ArgumentNullException error) when (error.ParamName == "request")
{
    requestGuarded = true;
}

var capacityGuarded = false;

try
{
    _ = BookingApi.Evaluate(
        capacity: -1,
        request: new BookingRequest(requestId: "REQ-30", attendee: "Lin", seats: 1));
}
catch (ArgumentOutOfRangeException error) when (error.ParamName == "capacity")
{
    capacityGuarded = true;
}

Require(requestGuarded, "request null guard");
Require(capacityGuarded, "capacity range guard");
Console.WriteLine("Guards: request-id=true request=true capacity=true");

// #region public-surface-contract
var publicTypes = typeof(BookingApi).Assembly.GetExportedTypes();

var publicTypeNames = publicTypes
    .Select(type => type.Name)
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();

var expectedPublicTypes = new[]
{
    nameof(BookingApi),
    nameof(BookingOutcome),
    nameof(BookingRequest),
    nameof(BookingResponse)
};

Require(publicTypeNames.SequenceEqual(expectedPublicTypes), "minimal public type surface");
Require(typeof(BookingResponse).GetConstructors().Length == 0, "response construction is controlled");
Require(
    !publicTypes.SelectMany(GetPublicSignatureTypes).Any(ContainsFSharpSpecificType),
    "no F#-specific type leaks through public signatures");
Console.WriteLine($"Public types: {string.Join(",", publicTypeNames)}");

var nullability = new NullabilityInfoContext();
var requestIdParameter = typeof(BookingRequest).GetConstructors().Single().GetParameters()[0];
var confirmationProperty = typeof(BookingResponse).GetProperty(nameof(BookingResponse.ConfirmationCode))!;
var requestIdState = nullability.Create(requestIdParameter).ReadState;
var confirmationState = nullability.Create(confirmationProperty).ReadState;

Require(requestIdState == NullabilityState.NotNull, "requestId nullable metadata");
Require(confirmationState == NullabilityState.Nullable, "confirmation nullable metadata");
Console.WriteLine(
    $"Nullability: request-id={requestIdState} confirmation={confirmationState}");

var documentationPath = Path.ChangeExtension(typeof(BookingApi).Assembly.Location, ".xml");
Require(File.Exists(documentationPath), "XML documentation sidecar");
var documentation = File.ReadAllText(documentationPath);
Require(documentation.Contains("BookingApi.Evaluate", StringComparison.Ordinal), "Evaluate XML documentation");
Console.WriteLine("XML docs: evaluate=true");
// #endregion public-surface-contract
