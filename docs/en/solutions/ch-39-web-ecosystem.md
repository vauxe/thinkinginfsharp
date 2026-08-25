---
title: "Chapter 39 Solutions"
description: "Choose web surfaces for concrete teams, design a contract-preserving Falco spike, and migrate framework-bound endpoints reversibly."
translationKey: solutions/ch-39-web-ecosystem
kind: solution
part: 7
chapter: 39
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-web-minimal-api
  - foundation-contract-tests
exerciseIds:
  - ch39-exercise-01
  - ch39-exercise-02
  - ch39-exercise-03
termIds: []
sources:
  - id: microsoft-aspnet-api-overview
    url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-aspnet-integration-tests
    url: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: giraffe-nuget
    url: https://www.nuget.org/packages/Giraffe/8.3.0
    checked: "2026-08-25"
  - id: falco-nuget
    url: https://www.nuget.org/packages/Falco/5.2.0
    checked: "2026-08-25"
  - id: oxpecker-nuget
    url: https://www.nuget.org/packages/Oxpecker/2.0.1
    checked: "2026-08-25"
  - id: saturn-nuget
    url: https://www.nuget.org/packages/Saturn/0.17.0
    checked: "2026-08-25"
---

# Chapter 39 Solutions {#overview}

These answers are provisional engineering decisions with explicit reversal evidence. A real team must run the spikes against its authentication, serializer, deployment, and workload rather than copying the conclusion.

[Return to Chapter 39](../part-07/ch-39-web-ecosystem).

## Exercise 1: choose for three teams {#exercise-01}

### Team A: mixed languages on one platform {#exercise-01-team-a}

Start with platform Minimal APIs.

The organization already operates ASP.NET Core, the service is a small internal JSON API, and both C# and F# engineers need to read the edge. Direct endpoint routing minimizes framework translation, reuses standard authorization/diagnostic conventions, and keeps the F# differentiation in the domain and workflow rather than the host.

Compare it with Falco 5.2.0 in a two-day spike. Falco could win if repeated request/response adapters dominate the direct implementation and its endpoint vocabulary materially improves review. Run the same DTO, strict JSON, policy authorization, cancellation, OpenAPI, `TestServer`, and publish checks. Reject Falco if required organization middleware or endpoint metadata needs custom bridges with no clear owner.

Evidence that would change the initial choice includes:

- five representative endpoints repeatedly fight delegate overloads or binding behavior;
- Falco reduces boundary code while preserving the exact organization contract;
- both-language maintainers can debug through the Falco and ASP.NET layers;
- its locked package graph passes the organization's support and vulnerability policy;
- upgrade and incident exercises remain understandable.

Do not select controllers merely because C# engineers know them. Select them if a controller-specific extension—such as the required application model or OData path—is actually present. Microsoft's [API overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) makes that distinction explicit.

### Team B: F#, server HTML, and HTMX {#exercise-01-team-b}

Start a comparison between Giraffe 8.3.0 and Oxpecker 2.0.1; provisionally choose Oxpecker only after its `net10.0` target and newer lifecycle pass the team's support gate.

Oxpecker aligns with endpoint routing, exposes an `HttpContext -> Task` terminal handler, and offers related view/HTMX packages. Those facts fit the desired shape. Giraffe remains the lower-adoption-risk countercandidate because of its longer-lived handler ecosystem and the team's ability to use separate view/HTMX integrations.

The spike must include more than a counter button:

- escaped dynamic HTML and a deliberate raw-HTML review;
- an antiforgery-protected form using the actual cookie/auth topology;
- validation errors rendered for both full-page and HTMX requests;
- authentication challenge and authorization forbid behavior;
- route generation, layouts, localization, static assets, and CSP policy;
- `TestServer` plus one browser interaction and accessibility check;
- package upgrade and deployment diagnostics.

Choose Giraffe if maturity, existing knowledge, or an extension outweighs Oxpecker's endpoint-routing fit. Choose Falco 5.2.0 as a third candidate only if its markup/HTMX surface better matches the templates. Do not combine all three.

### Team C: existing Saturn service {#exercise-01-team-c}

Do not begin with a rewrite. The product asks for a .NET 10 move and no feature change, so first build a compatibility inventory for Saturn 0.17.0, its `net6.0` asset, Giraffe dependency, authentication packages, generators, and every direct/transitive warning.

Create a branch that:

1. targets `net10.0` under warnings as errors;
2. restores from a newly reviewed lock file;
3. runs every current contract and browser test;
4. publishes and starts in the production-like runtime image;
5. exercises authentication, error handling, static assets, diagnostics, shutdown, and load;
6. records unsupported or obsolete API use and maintainer evidence.

If that passes within the organization's support policy, ship the runtime move separately and keep Saturn for now. A successful runtime upgrade is valuable evidence and avoids mixing platform risk with a framework rewrite.

If it fails materially, compare a move to Giraffe—closest to Saturn's underlying handler model—with a move to Minimal APIs—smallest long-term dependency surface. Estimate from three representative routes, not all 40. Preserve public contracts and authentication behavior before improving internal style.

## Exercise 2: preserve the greeting contract in a spike {#exercise-02}

### Bound a Falco 5.2.0 experiment {#exercise-02-scope}

Create a temporary project or branch that pins Falco 5.2.0. Its official package metadata also brings Falco.Markup and an FSharp.Core minimum into the reviewed graph. Record the resolved lock file rather than installing an unconstrained version.

Only the outer adapter may change:

```text
POST /api/greetings
  -> Falco endpoint/handler
  -> existing strict JsonSerializerOptions
  -> existing boundary DTO and name validation
  -> existing stable success/error writers
```

Using Falco's request and response helpers is optional during the first pass. Contract preservation is the experiment's control. If a helper cannot reject incorrectly cased or unknown JSON members with the required `invalid_json` shape, retain the manual strict deserializer or explicitly record a proposed contract change; do not silently weaken the test.

The handler must pass the request cancellation token into deserialization and rethrow client cancellation. It must not catch a canceled task as `internal_error`. It must preserve the “response already started” boundary or demonstrate the framework's equivalent behavior.

### Use the same executable evidence {#exercise-02-evidence}

Reference the spike from a copy of `WebSampleTests` and run the same contract cases unchanged. Add only framework-specific assertions that matter, such as endpoint metadata or middleware ordering. Then run:

- locked restore and Release build with warnings as errors;
- the unchanged `TestServer` contract;
- one real-process `curl` success and malformed request;
- authorization and diagnostic checks if they are target requirements;
- `dotnet publish` for the intended deployment target;
- a package vulnerability/license review and one compatible-version upgrade.

Compare:

| Dimension | Direct sample baseline | Falco spike question |
|---|---|---|
| public HTTP contract | passing contract cases | unchanged? |
| concepts | `RequestDelegate`, endpoint routing | endpoint list, handler, request/response helpers |
| direct dependencies | shared framework only | Falco plus resolved graph |
| platform access | direct | is an escape hatch clear? |
| diagnostics | standard ASP.NET context | are route names/status/correlation still visible? |
| maintenance | Microsoft .NET support | who owns Falco upgrades internally? |

Delete the spike if it changes the contract without a product reason, needs custom bridges for required middleware, obscures cancellation or diagnostics, fails publish policy, or saves too little recurring code to repay a second API lifecycle. If it wins, replace the old route once, document the conventions, and keep framework types out of the validation/application modules.

This design intentionally does not claim the spike passes: this repository verifies only the direct Minimal API implementation.

## Exercise 3: design a reversible migration {#exercise-03}

### Inventory behavior before handlers {#exercise-03-inventory}

Build a route catalog containing method, normalized template, request/response schema, status codes, authentication scheme, authorization policy, filters/middleware, streaming/body limits, OpenAPI operation, diagnostics, and current contract tests. Search for framework types outside the web project; those are coupling work, not route rewrites.

Extract or confirm three neutral seams:

- boundary DTOs and strict serializer/error policy;
- application functions whose inputs and `Result` outputs do not mention the web framework;
- ASP.NET Core authentication/authorization policies owned by host configuration.

Do not create one giant “universal handler” abstraction that models every framework feature. Small application ports plus public transport contracts are the stable seam.

### Move one route without route collisions {#exercise-03-routing}

Maintain a machine-reviewed ownership registry: every `(method, route pattern)` belongs to exactly one old or new mapper. The composition root installs both during migration, but a build/startup test enumerates endpoint data sources and rejects duplicate method/template pairs.

Select a representative low-risk route that still exercises JSON, authorization, one application result, and an error. Map it in the new surface, remove it from the old map in the same change, and run the original contract tests unchanged.

For additional confidence, host old and new variants separately in tests under private comparison prefixes, feed identical requests, and compare normalized status, headers, and JSON semantics. Do not expose both prefixes in production or compare volatile headers byte for byte.

Authentication stays owned by ASP.NET Core schemes and policies. Verify that new endpoints carry equivalent policy metadata and produce the same challenge versus forbid behavior. A successful `200` test with an authenticated fixture does not prove unauthenticated and unauthorized paths.

OpenAPI comparison should normalize ordering and known generator metadata, then detect removed operations, schema changes, status changes, and security requirements. Review intentional changes; do not overwrite the baseline automatically.

### Roll out and remove the old package {#exercise-03-rollout}

Move routes in small groups while observing per-template traffic, status, latency, cancellation, and error classifications. Canary the same immutable artifact if deployment supports it. Keep rollback at the artifact level; do not dynamically switch two handler implementations per request unless there is a demonstrated need.

The old framework can be removed only when:

- no route, middleware, view, binding type, or test helper uses it;
- no application/domain project references it directly or transitively by design;
- endpoint enumeration shows the expected complete route set once;
- authentication, OpenAPI, diagnostics, real-process smoke, and publish tests pass;
- the lock file and deployment artifact no longer contain the package;
- the rollback window for the last old-framework artifact has closed.

If migration stalls, keep ownership explicit. A documented mixed boundary is safer than pretending the old package is gone while hidden helpers still control policy.

## Solution review {#solution-review}

- Platform and team constraints give a better starting point than framework identity.
- Minimal APIs are a strong mixed-team default when direct ASP.NET Core integration matters.
- Server HTML and HTMX need security, browser, and view evidence—not a hello-world syntax comparison.
- An existing Saturn service should separate the .NET 10 compatibility move from any rewrite.
- A spike changes only the web adapter and runs the same contract cases as the baseline.
- Stable package versions and resolved transitive graphs are part of the experiment.
- Convenience binding must not silently weaken JSON or error contracts.
- A migration catalog records behavior, not merely source files.
- Each method/template pair has exactly one owner during migration.
- Authentication and authorization need challenge, forbid, and success comparisons.
- Remove the old package only after code, routes, lock file, artifact, tests, and rollback window agree.
- “Designed” and “verified” remain different statuses throughout these answers.

## Sources {#sources}

- [Microsoft Learn: APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0)
- [Microsoft Learn: Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [NuGet: Giraffe 8.3.0](https://www.nuget.org/packages/Giraffe/8.3.0)
- [NuGet: Falco 5.2.0](https://www.nuget.org/packages/Falco/5.2.0)
- [NuGet: Oxpecker 2.0.1](https://www.nuget.org/packages/Oxpecker/2.0.1)
- [NuGet: Saturn 0.17.0](https://www.nuget.org/packages/Saturn/0.17.0)
