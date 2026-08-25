---
title: "Chapter 36 Solutions"
description: "Preserve an HTTP contract under automatic binding, reason about ambiguous effects, and assign security controls across deployment topologies."
translationKey: solutions/ch-36-web-api-boundaries
---

# Chapter 36 Solutions {#overview}

These solutions preserve observable contracts while changing mechanisms. Automatic binding is acceptable only if its failure behavior is controlled; a retry is safe only if durable state explains the last visible effect; deployment middleware is useful only when ownership matches the topology.

[Return to Chapter 36](../part-06/ch-36-web-api-boundaries).

## Exercise 1: change binding without changing the contract {#exercise-01}

### Freeze observable behavior first {#exercise-01-contract}

The refactor must preserve these invariants:

- accepted JSON media types and exact case-sensitive property names;
- rejection of unknown members and excessive nesting;
- an effective 16 KiB byte limit with and without `Content-Length`;
- `invalid_json` for malformed or shape-incompatible JSON;
- `invalid_request` for a null body or missing DTO field;
- accumulated domain field errors before any port call;
- the caller's request-aborted token on every port;
- every success status, error status, stable code, and response DTO shape.

Do not begin by deleting the contract tests. They are the specification that lets the mechanism change safely.

### Assign policy to the narrowest reusable layer {#exercise-01-layers}

One viable design divides responsibility as follows:

| Layer | Responsibility |
|---|---|
| HTTP JSON configuration | call `BookingJson.configure` on the options used by Minimal API binding |
| Kestrel configuration | reject bodies above 16 KiB on the real server |
| early middleware or endpoint filter | enforce the same streamed byte count when transport features do not provide it |
| binding-failure boundary | convert malformed JSON and binding failures to the stable `ApiErrorDto` contract |
| route handler | receive `PlaceBookingDto`, map it, validate it, and invoke the application workflow |
| outer exception boundary | preserve request cancellation and hide operational or unexpected details |

The handler can then have a compact conceptual signature:

```fsharp
PlaceBookingDto -> CancellationToken -> Task<IResult>
```

That signature does not prove the public contract. Framework binding runs before the handler. Depending on configuration and host, a binding failure may return a framework-generated body or surface as an exception to `TestServer`. The binding-failure boundary must normalize both paths before they become observable.

Do not read the body once in middleware merely to measure it and then ask the binder to read the consumed stream. Either install a genuinely limiting stream wrapper before binding, or buffer only within the same declared small limit and replace the body with a rewound stream. The former avoids a duplicate buffer; the latter is simpler but must dispose its owned buffer after the request.

### Keep tests black-box {#exercise-01-tests}

Run the existing HTTP cases unchanged. Add two requests without `Content-Length`: one exactly at the limit and one byte over it. Add `application/problem+json` or another valid `+json` media type to prove the content-type policy is intentional.

Assert that invalid input never enters `LoadBooking`, not merely that the response is `400`. Assert that cancellation reaches a blocked port, not merely that the client task finishes. Finally, repeat a real Kestrel smoke because `TestServer` does not reproduce all transport limits and headers.

If any status, code, field error, or side-effect count changes, the refactor changed the API. Decide that migration explicitly instead of calling it a binding implementation detail.

## Exercise 2: reason from the last visible effect {#exercise-02}

### Record ambiguity instead of guessing {#exercise-02-table}

The three interruptions produce different facts:

| Interruption | Provider sees | Snapshot contains | Caller sees | Blind retry risk |
|---|---|---|---|---|
| payment authorized, append fails | an authorization may exist | old state | `503` or lost response | a second authorization |
| append succeeds, notification fails | authorization exists | new booking | `503` | duplicate command while notification remains missing |
| notification succeeds, response is lost | authorization and notification exist | new booking | cancellation/no response | duplicate payment or notification despite complete work |

The caller cannot infer durable truth from the presence or absence of an HTTP response. The server also cannot infer whether a provider acted merely because its connection failed after sending a request. Both need identifiers that survive a process and network failure.

### Persist the minimum replay evidence {#exercise-02-evidence}

Chapter 37 needs a durable record keyed by the normalized request ID. At minimum it must retain:

- a fingerprint of the original command, so reusing one ID for different input is a conflict;
- the accepted booking or decision result;
- a stable payment idempotency key and whether authorization is pending, known accepted, known declined, or ambiguous;
- whether notification is pending or delivered;
- enough response data to replay the same completed result without rerunning effects.

“Pending” and “ambiguous” are different. Pending means no attempt is known to have begun. Ambiguous means an attempt began but its outcome is unknown; a provider status query or provider-supported idempotency key is required before another charge.

Notification after the local commit suggests a durable outbox-shaped record: commit the booking and “notification pending” together, then deliver and mark completion separately. A deterministic stub can prove the state machine, but it cannot prove a real message broker or email provider's delivery semantics.

This is not a distributed transaction. It is an explicit protocol for replay, reconciliation, and at-least-once attempts with deduplication where supported. Compensation, authorization expiry, and provider callbacks require additional business rules not present in this sample.

## Exercise 3: review two deployment topologies {#exercise-03}

### Put each control where it has trustworthy information {#exercise-03-table}

Use this responsibility table as a starting point, not universal infrastructure policy:

| Concern | Kestrel at the edge | Trusted reverse proxy in front | Booking requirement |
|---|---|---|---|
| TLS and HSTS | configure in the app/server | usually terminate at proxy; preserve secure scheme correctly | required on untrusted networks |
| forwarded headers | leave disabled | enable only for explicit known proxies/networks | topology-dependent |
| host filtering | configure allowed hosts | proxy validates; app may add defense in depth | required when host is security-relevant |
| 16 KiB body limit | Kestrel plus application limit | proxy, Kestrel, and application limits should agree | required for these command routes |
| rate limiting/timeouts | app/server policy | coordinate proxy and app policies | required before public exposure; values are workload-dependent |
| authentication/authorization | application validates identity and permission | proxy may authenticate, but app must trust and authorize the resulting identity explicitly | required before exposing booking data |
| secret retrieval | controlled store available to the process | controlled store or workload identity, never proxy headers carrying raw secrets | required for real providers |
| HTTP logging | classify and redact in the app | classify and redact at both layers; avoid duplicate body capture | required diagnostic policy; body logging is optional |

Forwarded headers are dangerous when every sender is trusted: a client could forge its scheme or address. Conversely, leaving them disabled behind a terminating proxy can make HTTPS redirects, secure links, and audit data wrong. Configuration follows the real trust boundary.

CORS is necessary only for browser origins that must call this API directly. It does not authenticate callers and does not protect the API from scripts, servers, or command-line clients. If no cross-origin browser client exists, leaving CORS disabled is the smaller correct policy.

Disabling `Server: Kestrel` reduces passive disclosure but does not repair missing authentication, TLS, or rate limiting. Likewise, moving a credential from source code to a plain environment variable prevents an accidental commit but does not encrypt it.

The release review should name the owner and verification evidence for every required row: configuration test, deployment probe, log sample, or security test. A checked box without a topology or observable result is not a control.

## Solution review {#solution-review}

- A binding refactor must preserve failures that occur before the handler runs.
- Kestrel and application-level limits cover different execution environments.
- Contract tests assert side-effect absence and cancellation, not only status codes.
- An HTTP response is evidence of observation, not a transaction receipt.
- Payment ambiguity needs a durable key and reconciliation before another charge.
- Post-commit notification needs durable pending/completed state and replay policy.
- An outbox is a protocol component, not a claim of exactly-once delivery.
- Edge and proxy deployments assign TLS and forwarded-header authority differently.
- Authentication and authorization remain required even when a proxy participates.
- CORS is a browser policy, not caller authentication.
- Environment variables and a suppressed server header are limited hardening measures.
- Logging controls require classification, redaction, and evidence at every logging layer.

## Sources {#sources}

- [Microsoft Learn: Minimal APIs quick reference](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Microsoft Learn: `HttpContext.RequestAborted`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.defaulthttpcontext.requestaborted?view=aspnetcore-10.0)
- [Microsoft Learn: test ASP.NET Core middleware with `TestServer`](https://learn.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-10.0)
- [Microsoft Learn: Kestrel security considerations and configurable limits](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0)
- [Microsoft Learn: safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [Microsoft Learn: HTTP logging and redaction](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
