# F# Minimal API ecosystem sample

This is a deliberately small ASP.NET Core Minimal API written in F#. It demonstrates one JSON input/output boundary, domain-independent validation, stable safe errors, request cancellation, and an in-process HTTP contract test. It uses only the .NET 10 shared web framework; no third-party web package or external service is required.

It is not a second booking application. The Part VI capstone remains the example for domain modeling, persistence, consistency, idempotency, diagnostics, and a C# client. This sample isolates the platform-native Web choice so Chapter 39 can compare it fairly with F# web libraries.

## Run

From the repository root:

```console
dotnet run \
  --project examples/ecosystem/web/WebSample.fsproj \
  --configuration Release \
  -- \
  --urls http://127.0.0.1:5090
```

In another terminal:

```console
curl --include \
  --request POST \
  --header 'Content-Type: application/json' \
  --data '{"name":"Ada"}' \
  http://127.0.0.1:5090/api/greetings
```

The response is `200` with:

```json
{"message":"Hello, Ada!"}
```

Blank or missing `name` returns `400 name_required`. Malformed JSON, an incorrectly cased `Name`, or an unknown member returns `400 invalid_json`. A non-JSON content type returns `415 unsupported_media_type`. Unexpected exception details are not returned.

## Verify

```console
dotnet test tests/ContractTests/ContractTests.fsproj \
  --configuration Release \
  --filter FullyQualifiedName~WebSampleTests
```

The test uses ASP.NET Core `TestServer` and covers the success contract plus the transport and validation failures listed above. `TestServer` does not prove socket, proxy, TLS, authentication, rate-limit, body-size, deployment, or production dependency behavior. Those concerns are intentionally not duplicated from the capstone.
