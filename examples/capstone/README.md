# Booking capstone

This directory contains the runnable system assembled in Part VI: an F# domain and application core, strict JSON contracts, a local aggregate snapshot, an ASP.NET Core API, deterministic payment and notification adapters, and a C# HTTP client.

It requires no external account or service. The default adapters are controlled local stubs; they do not charge money or send a real notification.

## Automated release check

From the repository root, with .NET SDK 10.0.301, Node.js 22 or later, and pnpm 11.7.0 installed:

```console
pnpm install --frozen-lockfile
pnpm check:capstone
```

The second command:

1. restores the solution in locked mode;
2. builds the Release solution with warnings as errors;
3. runs every test whose fully qualified name contains `Booking`;
4. creates a fresh temporary snapshot directory;
5. starts the real API on a dynamically assigned loopback port;
6. runs the C# client through place, exact replay, confirm, and GET;
7. sends invalid JSON and verifies its stable error contract;
8. checks correlated success/failure diagnostics and forbidden-text absence;
9. stops the child process and removes the temporary directory, including on failure.

A successful run ends with output shaped like this:

```text
Capstone check passed.
Placed: id=REQ-CAPSTONE-CHECK seats=2 status=pending
Replay: status=201 same-body=True
Confirmed: id=REQ-CAPSTONE-CHECK code=CONF-CSHARP status=confirmed
Loaded: status=200 same-body=True
Diagnostics: success=true client-error=true correlation=<32 lowercase hex characters> secrets=false
```

The locked restore can download public NuGet packages when they are not already cached. The workflow needs no private feed or cloud credential.

## Run it manually on Bash or zsh

In terminal 1, create a new disposable snapshot and start Kestrel:

```bash
CAPSTONE_TEMP="$(mktemp -d "${TMPDIR:-/tmp}/thinking-in-fsharp-capstone.XXXXXX")"
export BOOKING_STORE_PATH="$CAPSTONE_TEMP/bookings.json"
export BOOKING_EVENT_ID="EVT-MANUAL"
export BOOKING_CAPACITY="4"

dotnet run \
  --project examples/capstone/src/Booking.Api/Booking.Api.fsproj \
  --configuration Release \
  -- \
  --urls http://127.0.0.1:5088
```

In terminal 2, run the C# contract client:

```bash
dotnet run \
  --project examples/capstone/clients/Booking.CSharpClient/Booking.CSharpClient.csproj \
  --configuration Release \
  -- \
  http://127.0.0.1:5088/ \
  REQ-MANUAL
```

The client directly references only `Booking.Contracts`. It sends and reads public DTOs over HTTP; it does not call F# domain types.

Inspect a safe transport failure and its response correlation ID:

```bash
curl --include \
  --request POST \
  --header 'Content-Type: application/json' \
  --data '{not-json' \
  http://127.0.0.1:5088/api/bookings/place
```

After stopping the API with <kbd>Ctrl</kbd>+<kbd>C</kbd>, remove only the disposable paths created above:

```bash
rm "$BOOKING_STORE_PATH"
rmdir "$CAPSTONE_TEMP"
```

If port 5088 is occupied, choose another loopback port in both commands.

## Run it manually on PowerShell

In terminal 1:

```powershell
$CapstoneTemp = Join-Path ([IO.Path]::GetTempPath()) ("thinking-in-fsharp-capstone-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $CapstoneTemp | Out-Null
$env:BOOKING_STORE_PATH = Join-Path $CapstoneTemp "bookings.json"
$env:BOOKING_EVENT_ID = "EVT-MANUAL"
$env:BOOKING_CAPACITY = "4"

dotnet run `
  --project examples/capstone/src/Booking.Api/Booking.Api.fsproj `
  --configuration Release `
  -- `
  --urls http://127.0.0.1:5088
```

In terminal 2:

```powershell
dotnet run `
  --project examples/capstone/clients/Booking.CSharpClient/Booking.CSharpClient.csproj `
  --configuration Release `
  -- `
  http://127.0.0.1:5088/ `
  REQ-MANUAL
```

After stopping the API, remove the exact temporary directory held in `$CapstoneTemp`:

```powershell
Remove-Item -LiteralPath $CapstoneTemp -Recurse
```

## Runtime configuration

| Setting | Required | Meaning |
|---|---:|---|
| `BOOKING_STORE_PATH` | yes | full or relative path of the aggregate JSON snapshot; it must name a file, not a directory |
| `BOOKING_EVENT_ID` | no | activity ID; defaults to `EVT-LOCAL` |
| `BOOKING_CAPACITY` | no | positive integer capacity; defaults to `8` |
| `--urls` / normal ASP.NET Core URL configuration | no | listener address; keep the sample on loopback unless deployment controls are added |

The event ID and capacity become part of the snapshot contract. Reopening an existing snapshot with different values fails safely. Use a separate path for a separate activity; do not delete or reinterpret real data as a migration strategy.

Invalid startup configuration writes only a stable reason code and exits with code 2. It does not print the configured snapshot path.

## HTTP surface

| Method and route | Success | Purpose |
|---|---:|---|
| `POST /api/bookings/place` | `201` | reserve aggregate capacity, authorize through the stub, commit, and notify |
| `POST /api/bookings/confirm` | `200` | confirm an existing pending booking |
| `POST /api/bookings/cancel` | `200` | cancel an active booking and release its seats |
| `GET /api/bookings/{requestId}` | `200` | read the current booking |

Command bodies are strict, case-sensitive JSON and are bounded at 16 KiB. Errors use the stable `ApiErrorDto` shape. An exact completed command replays its acknowledged result; reuse of the same operation identity with different command data returns `409 idempotency_conflict`.

If a payment call becomes ambiguous, an exact retry returns `409 payment_outcome_unknown` and does not blindly charge again. This sample has no provider reconciliation endpoint, so an operator cannot resolve that state here.

## Diagnostics contract

Each request completed by the middleware receives `X-Correlation-ID`. It uses the active 32-character W3C trace ID when one exists—normally the context generated or propagated by ASP.NET Core—and otherwise creates a random W3C trace ID. The completion log uses event ID 1000 and stable fields:

```text
Booking request completed correlationId=<id> method=<method> endpoint=<route-template> statusCode=<status> outcome=<outcome> elapsedMs=<duration>
```

The logger also opens a `CorrelationId` scope. It does not record request/response bodies, confirmation codes, provider transaction text, exception messages, or the snapshot path.

Custom instruments are deliberately small:

| Signal | Name | Dimensions or tags |
|---|---|---|
| counter | `booking.http.requests` | bounded `outcome` only |
| histogram | `booking.http.duration` | milliseconds; bounded `outcome` only |
| activity | source `ThinkingInFSharp.Booking.Api`, name `booking.http.request` | correlation ID, method, route template, status, outcome |

The custom activity is an internal child of ASP.NET Core's server activity. `ActivitySource.StartActivity` may return `null` when no listener samples it; request execution still works. A `Meter`, `ActivitySource`, and log calls are instrumentation points—not storage, dashboards, alerting, or export. A deployment must configure a collector/provider such as OpenTelemetry and verify sampling, redaction, retention, and access.

See the official [.NET tracing instrumentation guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs), [.NET metrics instrumentation guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation), and [ASP.NET Core logging and scope documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0).

## Guarantees and limits

The checked local workflow proves:

- aggregate capacity is not oversold by cooperating service instances in one process and one normalized snapshot path;
- exact completed retries do not repeat modeled payment or notification effects;
- changed payload under the same operation key conflicts;
- cancellation releases committed occupancy;
- orderly restart can replay persisted completion;
- invalid transport input does not enter storage or external effects;
- the public C# client and JSON DTOs remain compatible.

It does not provide:

- safe concurrent writers across processes, containers, or machines;
- an ACID, replicated, encrypted, or backed-up database;
- crash durability under every filesystem and power-loss scenario;
- a real payment provider, notification provider, transactional outbox, or exactly-once delivery;
- authentication, authorization, TLS policy, rate limiting, CORS policy, or production secret management;
- a payment reconciliation operation, reservation expiry, data migration, or administrative UI;
- a telemetry collector, durable log store, dashboard, alert, SLO, RPO, or RTO.

Those are deployment and product requirements, not hidden properties of F#, `SemaphoreSlim`, local JSON, or the test harness.
