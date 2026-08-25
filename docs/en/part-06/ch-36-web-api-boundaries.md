---
title: "Chapter 36: Web API, JSON, and Input Boundaries"
description: "Expose the booking workflow through a small F# Minimal API while keeping JSON, validation, cancellation, failures, and secrets at explicit boundaries."
translationKey: part-06/ch-36-web-api-boundaries
kind: chapter
part: 6
chapter: 36
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
  - capstone-booking-contracts
  - capstone-booking-infrastructure
  - capstone-booking-api
exerciseIds:
  - ch36-exercise-01
  - ch36-exercise-02
  - ch36-exercise-03
termIds: []
sources:
  - id: microsoft-minimal-api
    url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-http-json
    url: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httprequestjsonextensions?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-json-unmapped
    url: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members
    checked: "2026-08-25"
  - id: microsoft-request-aborted
    url: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.defaulthttpcontext.requestaborted?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-testserver
    url: https://learn.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: ietf-rfc3986-unreserved
    url: https://www.rfc-editor.org/rfc/rfc3986.html#section-2.3
    checked: "2026-08-25"
  - id: microsoft-kestrel-security
    url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-app-secrets
    url: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-http-logging
    url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0
    checked: "2026-08-25"
---

# Chapter 36: Web API, JSON, and Input Boundaries {#overview}

Chapter 35 assembled capabilities but had no network boundary. This chapter adds one small ASP.NET Core Minimal API. “Minimal” describes the hosting model, not the amount of boundary judgment: bytes are still untrusted, DTOs are still not domain values, cancellation is still observable, and an exception message is still not a response contract.

The implementation keeps one question visible at every step: which layer is allowed to decide this? HTTP decides media type and status. The JSON contract decides wire shape. DTO mapping decides whether required transport data exists. The domain decides business validity and transitions. Adapters decide effects. The API coordinates those decisions and translates only their declared outcomes.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- map a small set of command-oriented routes without exposing domain representation;
- separate media-type, byte-size, JSON-shape, DTO-presence, and domain validation failures;
- reuse one strict `JsonSerializerOptions` policy for request and response boundaries;
- bound a request even when `Content-Length` is absent or a test server bypasses Kestrel limits;
- turn domain refusals into stable status/code pairs without leaking protected values;
- pass `HttpContext.RequestAborted` through every asynchronous port;
- distinguish client cancellation from an internally cancelled dependency;
- explain exactly when payment, persistence, notification, and response become visible;
- return safe operational errors without returning exception messages;
- load configuration without printing rejected values or committing secrets;
- use `TestServer` for the application pipeline and a real Kestrel smoke test for transport behavior;
- state which production concerns this teaching host intentionally omits.

## Treat HTTP as an outer interpreter {#outer-interpreter}

The request crosses several representations before it may cause an effect:

```text
HTTP bytes
  -> bounded strict JSON
  -> command DTO
  -> raw domain command
  -> validated protected values
  -> load + pure decision
  -> payment? + append + notification
  -> response DTO or safe error DTO
```

The arrows matter more than the boxes. No arrow is an unchecked cast. Each either produces the next representation or stops with an outcome owned by that boundary.

ASP.NET Core calls functions mapped with `MapGet`, `MapPost`, and related methods route handlers. A handler can return framework results, strings, or values that the framework serializes. This sample instead uses explicit `RequestDelegate` handlers so the byte cap, JSON error body, and cancellation behavior remain visible in one teaching-sized file.

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#endpoint-map{fsharp:line-numbers} [Endpoints.fs]

Using a direct delegate is not a claim that automatic Minimal API binding is wrong. It is a local choice to make this chapter's boundary policy executable and contract-testable. Exercise 1 asks you to preserve the same contract with automatic binding.

## Publish four narrow routes {#route-contract}

The API exposes commands rather than a generic endpoint that accepts a serialized discriminated union:

| Method and route | Request representation | Success | Meaning |
|---|---|---|---|
| `POST /api/bookings/place` | `PlaceBookingDto` | `201` + `BookingDto` | validate, charge, append, notify |
| `POST /api/bookings/confirm` | `ConfirmBookingDto` | `200` + `BookingDto` | validate transition, append, notify |
| `POST /api/bookings/cancel` | `CancelBookingDto` | `200` + `BookingDto` | validate transition, append, notify |
| `GET /api/bookings/{requestId}` | route text | `200` + `BookingDto` | load the matching snapshot |

Separate routes make allowed commands discoverable and give each request one stable JSON shape. They also avoid treating the compiler-oriented encoding of `BookingCommand` as a public protocol.

`201 Created` includes a relative `Location` header built from the normalized request ID. After trimming, the domain accepts 1–64 ASCII URI-unreserved characters: letters, digits, `-`, `.`, `_`, and `~`; the complete values `.` and `..` are excluded because URI resolution treats them as dot-segments. The stored value is therefore exactly one stable path segment; `Uri.EscapeDataString` remains a defensive encoding step, and an HTTP test follows the returned location back to `200`. Confirmation and cancellation modify the representation already addressed by that ID, so they return `200`.

This is not a claim that command routes are the only REST design. It is a small, consistent contract for this workflow. Changing route semantics later would be a public API migration, not an internal refactor.

## Keep response types at the boundary {#boundary-dtos}

Successful handlers project protected `Booking` values through `BookingMapping.ofDomain`; they never hand a domain record or union to the serializer. Failed handlers return one API-owned shape:

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#api-error-contract{fsharp:line-numbers} [Endpoints.fs]

`code` is the stable machine-readable decision. `message` is safe explanatory text, not a place for an exception or provider response. `errors` contains stable field/code pairs and is empty for non-field failures.

The wire messages are English, while the book explains them in both languages. Clients should branch on codes, not translated prose. Localizing human-facing text later can therefore leave protocol behavior unchanged.

## Reject input in the layer that understands it {#validation-layers}

“Bad request” is not one failure. The implementation keeps three authority levels distinct.

### Transport and syntax {#transport-syntax}

Before a DTO exists, the API checks that the content type is a recognized JSON media type, that at most 16 KiB are read, and that bytes deserialize under the strict options. The outcomes are `415 unsupported_media_type`, `413 request_too_large`, or `400 invalid_json`.

`HasJsonContentType` recognizes JSON media types, including the structured `+json` suffix. A malformed document, a wrongly cased property, an unknown property, or a value of the wrong JSON kind fails before any port is called.

### DTO presence {#dto-presence}

JSON `null` can deserialize to a null DTO. A missing `seats` property becomes `Nullable<int>()`. The command mappers therefore report `MissingBody`, `MissingRequestId`, `MissingSeats`, and corresponding command-specific failures as `400 invalid_request`.

This layer answers whether the wire representation supplied the data required to form a raw command. It deliberately does not decide whether `0` seats or a blank identifier is legal business input.

### Domain validity {#domain-validity}

The existing validation module still owns request identifiers, non-positive seats, blank confirmation codes, and blank cancellation reasons. A request ID must be nonblank, at most 64 characters, URI-unreserved ASCII, and not the complete dot-segment `.` or `..`; values containing `/`, `%`, `?`, or Unicode likewise cannot become ambiguous route identities. The API first validates to obtain a protected storage key and to reject all field problems before I/O. The pure decider validates again when it accepts the raw command; that repeated pure check preserves one domain authority rather than cloning the rule in HTTP code.

Multiple domain errors become one `validation_failed` response with ordered field errors. Request ID failures use the stable field codes `blank`, `too_long`, or `invalid_format`. No storage, payment, or notification call occurs for transport, DTO, or domain-validation failure.

## Bound bytes before interpreting JSON {#bounded-body}

Kestrel's default request-body limit is far larger than these tiny command documents. The host lowers it to 16 KiB. The endpoint also enforces the same limit while reading:

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#bounded-json-body{fsharp:line-numbers} [Endpoints.fs]

Checking `Content-Length` gives an early rejection when the sender provides it, but that header alone is not a bound. Chunked requests and custom test streams may have no declared length. The loop therefore reads at most one byte beyond the limit, stops, and never allocates in proportion to attacker-controlled input.

The body is buffered because the maximum is deliberately small and strict deserialization needs a complete command. A file-upload endpoint would need a different streaming design and its own limit; copying this 16 KiB policy to every endpoint would be cargo culting.

The same `BookingJson.configure` call fixes case sensitivity, unknown-member rejection, null omission, and depth. Reusing it prevents HTTP and persistence from giving the same DTO two subtly different meanings.

## Coordinate the workflow without moving rules outward {#workflow}

After mapping and validation, the endpoint has a raw command, a protected request ID, an optional protected payment request, and the success status. It can now coordinate effects:

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#endpoint-workflow{fsharp:line-numbers} [Endpoints.fs]

The order is intentional:

1. load the current booking state;
2. call the pure decider;
3. for placement only, request payment authorization;
4. append the accepted event;
5. send a notification;
6. serialize the resulting booking DTO.

The API does not inspect private booking fields to reimplement transitions. It uses `Decider.decide`, `BookingEvent.booking`, protected accessors, and port functions. HTTP owns sequencing and translation; domain modules still own legal facts.

## Map outcomes, not strings {#error-map}

The response table is part of the API contract:

| Status | Stable code | Source |
|---|---|---|
| `400` | `invalid_json` | malformed or contract-incompatible JSON |
| `400` | `invalid_request` | required DTO data missing |
| `400` | `validation_failed` | domain command fields invalid |
| `404` | `booking_not_found` | confirmation, cancellation, or lookup has no matching booking |
| `409` | `booking_already_exists` | placement reuses an existing request ID |
| `409` | `capacity_exceeded` | requested seats exceed this activity's capacity |
| `409` | `invalid_transition` | current status rejects the requested transition |
| `413` | `request_too_large` | command body exceeds 16 KiB |
| `415` | `unsupported_media_type` | request is not a JSON media type |
| `422` | `payment_declined` | provider produced an expected refusal |
| `503` | `storage_unavailable` / `dependency_unavailable` | an operational dependency cannot complete |
| `500` | `internal_error` | an unexpected application fault occurred |

A payment refusal is neither malformed JSON nor an exception. A capacity refusal is neither “not found” nor an infrastructure outage. Keeping those distinctions makes client behavior and diagnostics possible without returning private union payloads.

Do not derive `code` from `sprintf "%A" error` or use `exception.Message`. Compiler names, file paths, provider details, and future refactors would become accidental public data.

## Propagate request cancellation {#request-cancellation}

`HttpContext.RequestAborted` is signaled when the connection underlying the request is aborted. The endpoint passes that same token to body reads, load, charge, append, notify, and response serialization.

When the client-owned token is cancelled in the in-process test, the blocked `LoadBooking` observes cancellation and the HTTP task remains cancelled. The error boundary rethrows an `OperationCanceledException` when `RequestAborted` is cancelled; it does not manufacture `500` JSON for a client that has gone away.

An operation can also cancel itself while the client remains connected—for example, a dependency-specific deadline. This sample maps that different condition to `503 dependency_unavailable`. A production system may distinguish dependency timeout from outage, but it must not confuse either with client disconnect.

Cancellation is a request to stop, not rollback. Once an external effect or file replacement is visible, later cancellation cannot make it un-happen. The next section makes that limitation concrete.

## Tell the truth about partial failure {#partial-failure}

The current sequence has observable interruption windows:

| Last completed step | What is true | Current response or observation | Safe conclusion |
|---|---|---|---|
| pure decision | no external effect or snapshot change | domain error, if any | retrying valid input has not duplicated an effect yet |
| payment authorization | provider may have acted; snapshot is old | `503` if append later fails | a blind retry may charge again |
| event append | booking snapshot is new | `503` if notification fails | a retry may see “already exists” while notification is missing |
| notification | all modeled effects completed | response may still be lost to cancellation | absence of a response does not prove failure |

This HTTP boundary exposes these facts rather than hiding them behind a generic `try/with`. Chapter 37 introduces atomic capacity and idempotency policy, then defines retry and restart behavior. Until then, this API is a runnable boundary demonstration, not a consistency-safe commercial booking service.

The test named “dependency failures are safe and reveal the post-commit notification window” proves that notification failure returns a safe `503` while the recorded state is already `Booked`. That is evidence of the problem, not evidence that the problem is solved.

## Keep exception details inside the process {#safe-errors}

The outer handler separates client cancellation, Kestrel's oversized-body exception, the typed storage adapter exception, and an unexpected fault:

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#safe-error-boundary{fsharp:line-numbers} [Endpoints.fs]

Adapters wrap known provider transport or availability failures in `DependencyUnavailableException` and retain the original exception as `InnerException` for internal diagnostics. The exact `Charge` or `Notify` call converts only that typed signal to `503 dependency_unavailable`; an arbitrary programming exception continues to the outer boundary and becomes a safe `500 internal_error`. `BookingStoreAdapterException` likewise retains its typed category for internal code but exposes only `storage_unavailable` over HTTP.

If an error occurs after response headers have started, writing a second JSON document would corrupt the response. The handler aborts that connection instead. Known DTO serialization is intentionally simple, but the boundary still avoids pretending an already-started response can be replaced.

This chapter does not add detailed fault logging. Chapter 38 will add structured diagnostics with an explicit data classification. Silence is safer than logging an unknown exception message before that policy exists, but production silence is not observability.

## Load configuration without disclosing it {#configuration-secrets}

The host reads `BOOKING_STORE_PATH`, with optional `BOOKING_EVENT_ID` and `BOOKING_CAPACITY`, then builds protected configuration and domain values. A rejected setting produces only `invalid_booking_store`, `invalid_event_id`, or `invalid_capacity`; the raw value is not printed.

<<< @/../examples/capstone/src/Booking.Api/Program.fs#api-host{fsharp:line-numbers} [Program.fs]

The sample's path, event ID, and capacity are ordinary configuration, not credentials. The rule is still useful: do not echo an untrusted configured path merely because this particular value is not secret. A later real payment key must stay outside source control and responses.

Environment variables keep values out of committed code, but Microsoft explicitly warns that they are commonly stored as plain text and remain visible if the process or machine is compromised. Use development Secret Manager only for development; choose a controlled production secret store for deployment.

The default host logging observed in the smoke test records method, route, status, content type, length, and duration—not bodies or configuration values. Do not enable request/response body logging casually: it buffers data and can capture personal or credential material. Classify and redact first.

Kestrel's `Server` header is disabled to reduce needless implementation disclosure. That is hardening, not authentication or authorization.

## Test the pipeline and the transport {#testing}

The contract tests use the official `Microsoft.AspNetCore.TestHost` package. Each test builds the real `WebApplication`, maps the real endpoints, injects controlled ports, starts the in-memory pipeline, and sends requests through `HttpClient`.

The focused cases cover:

- exact success JSON, `Location`, lookup, confirmation, and cancellation;
- malformed JSON, wrong property case, unknown property, missing field, null body, and wrong media type;
- accumulated domain validation before any effect;
- the 16 KiB limit before JSON parsing;
- duplicate, missing, capacity, and payment outcomes;
- pre-commit payment failure and post-commit notification failure;
- typed storage failure, unexpected failure, and response redaction;
- cancellation reaching a controlled blocked port without a timing sleep.

`TestServer` sends requests in memory and intentionally does not reproduce every transport behavior or header. That is why the application-level byte limit is tested there and a separate loopback Kestrel smoke test verifies real startup, headers, routing, and file persistence.

Neither test style replaces the other. Starting a random real port for every contract assertion would add noise; relying only on TestServer would leave Kestrel configuration unobserved.

## Run the API locally {#local-run}

The commands below use a temporary snapshot and bind only to loopback. Run them from the repository root.

### Start the host {#local-start}

On macOS or Linux:

```bash
BOOKING_STORE_PATH="${TMPDIR:-/tmp}/thinking-in-fsharp-booking.json" \
BOOKING_EVENT_ID="EVT-LOCAL" \
BOOKING_CAPACITY="4" \
ASPNETCORE_URLS="http://127.0.0.1:5086" \
dotnet run --project examples/capstone/src/Booking.Api/Booking.Api.fsproj -c Release
```

In PowerShell:

```powershell
$env:BOOKING_STORE_PATH = Join-Path ([IO.Path]::GetTempPath()) "thinking-in-fsharp-booking.json"
$env:BOOKING_EVENT_ID = "EVT-LOCAL"
$env:BOOKING_CAPACITY = "4"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5086"
dotnet run --project examples/capstone/src/Booking.Api/Booking.Api.fsproj -c Release
```

### Send successful requests {#local-success}

In another terminal, place and read a booking:

```bash
curl --fail-with-body -i \
  -H 'Content-Type: application/json' \
  -d '{"requestId":"REQ-36","seats":2}' \
  http://127.0.0.1:5086/api/bookings/place

curl --fail-with-body -i \
  http://127.0.0.1:5086/api/bookings/REQ-36
```

The first response is `201`, includes `Location: /api/bookings/REQ-36`, and returns a pending `BookingDto`. The second is `200` with the persisted representation.

### Observe a strict failure {#local-failure}

Property names are case-sensitive:

```bash
curl -i \
  -H 'Content-Type: application/json' \
  -d '{"requestId":"REQ-BAD","Seats":2}' \
  http://127.0.0.1:5086/api/bookings/place
```

The response is stable and contains no parser exception:

```json
{"code":"invalid_json","message":"The request body is not valid JSON for this endpoint.","errors":[]}
```

Stop the host before deleting its explicit temporary snapshot. A later run with the same path restores the saved state; that fact is useful for the restart tests in Chapter 37.

## Know the production boundary {#production-boundary}

This local host deliberately has no authentication, authorization, TLS certificate, CORS policy, rate limiter, proxy trust configuration, distributed store, or real payment provider. It must not be exposed to untrusted networks as-is.

For an edge deployment, decide TLS, HSTS, allowed hosts, rate limits, request timeouts, authentication, authorization, and secret storage. Behind a reverse proxy, additionally configure forwarded headers with explicit trusted proxies and decide which layer terminates TLS and enforces each limit.

Do not add every middleware “for security” without a threat model. For example, CORS governs browsers, not arbitrary HTTP clients; enabling a permissive policy would weaken rather than complete the boundary. Chapter 42 revisits deployment choices, while Chapter 38 adds only the diagnostics and release checks needed by this sample.

## Avoid common API boundary mistakes {#boundary-mistakes}

- Serializing `BookingCommand` or `Booking` directly turns compiler representation into a public protocol.
- Treating a deserialized DTO as validated domain data skips smart constructors and accumulated rules.
- Trusting only `Content-Length` leaves unknown-length bodies unbounded.
- Letting persistence and HTTP use different JSON options creates two meanings for one DTO.
- Mapping every refusal to `400` erases what clients can safely do next.
- Returning `exception.Message` can disclose paths, provider details, or implementation names.
- Catching client cancellation as `500` lies about both the request and the server.
- Retrying after an ambiguous payment or notification failure can duplicate effects.
- Assuming an in-memory server reproduces Kestrel transport behavior leaves a verification gap.
- Putting production credentials in environment variables is not encryption or access control.
- Enabling body logging before classification and redaction can turn diagnostics into a data leak.
- Calling this unauthenticated loopback sample production-ready overstates its boundary.

## Exercises {#exercises}

### Exercise 1: change binding without changing the contract {#exercise-01}

Redesign one command route to use automatic Minimal API parameter binding. Preserve the exact strict JSON policy, 16 KiB effective limit, `ApiErrorDto` shapes, cancellation propagation, and all current status/code pairs. Identify which behavior belongs in configuration, an endpoint filter or middleware, and the handler. Specify contract tests that prevent framework defaults from changing the public response.

### Exercise 2: reason from the last visible effect {#exercise-02}

For each interruption—payment authorized then append fails, append succeeds then notification fails, and notification succeeds then the client disconnects—state what the provider, snapshot, caller, and a retry can observe. Propose the minimum idempotency information Chapter 37 must persist. Do not claim a distributed transaction.

### Exercise 3: review two deployment topologies {#exercise-03}

Compare exposing Kestrel directly with running it behind a reverse proxy. Produce a short responsibility table for TLS, forwarded headers, host filtering, request limits, rate limiting, authentication, secret retrieval, and log redaction. Mark which controls are required by this booking API and which depend on deployment requirements.

[Read the chapter solutions](../solutions/ch-36-web-api-boundaries).

## Model review {#model-review}

- HTTP is an outer interpreter, not the owner of domain rules.
- Four explicit routes accept and return boundary representations only.
- Media type, size, syntax, DTO presence, and domain validity are distinct checks.
- A real bound counts bytes even without `Content-Length`.
- One strict JSON policy prevents transport and persistence drift.
- Stable error codes are public; exception and provider messages are not.
- `RequestAborted` flows through every asynchronous effect and response write.
- Cancellation does not roll back an already-visible effect.
- Payment-before-append and notification-after-append create different retry hazards.
- `TestServer` proves the pipeline; loopback Kestrel proves selected transport behavior.
- Configuration rejection never requires printing the rejected value.
- Environment variables avoid commits but are not encrypted secret storage.
- Body logging requires explicit classification and redaction.
- Disabling a server header is hardening, not an authorization system.
- The current host is runnable and testable, not yet consistency-safe or production-complete.

## Sources {#sources}

- [Microsoft Learn: Minimal APIs quick reference](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Microsoft Learn: `HttpRequestJsonExtensions` and JSON content types](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httprequestjsonextensions?view=aspnetcore-10.0)
- [Microsoft Learn: reject unmapped JSON members](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn: `HttpContext.RequestAborted`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.defaulthttpcontext.requestaborted?view=aspnetcore-10.0)
- [Microsoft Learn: test ASP.NET Core middleware with `TestServer`](https://learn.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-10.0)
- [Microsoft Learn: Kestrel security considerations and configurable limits](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0)
- [Microsoft Learn: safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [Microsoft Learn: HTTP logging and redaction](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
