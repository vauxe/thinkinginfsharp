---
title: "Chapter 39: ASP.NET Core and the F# Web Ecosystem"
description: "Choose among platform-native Minimal APIs, controllers, and functional F# web libraries by system needs, team fit, and verified maintenance—not fashion."
translationKey: part-07/ch-39-web-ecosystem
---

# Chapter 39: ASP.NET Core and the F# Web Ecosystem {#overview}

F# does not need a separate web server to participate in modern .NET. An F# project can use Kestrel, endpoint routing, dependency injection, configuration, authentication, authorization, logging, metrics, and `TestServer` directly from ASP.NET Core. Several community libraries then add F#-shaped handlers, composition operators, views, or conventions on that same platform.

The practical question is not “Which F# framework wins?” Ask which API style removes real friction without hiding platform behavior the team must still understand. Begin with a verified platform-native slice, then compare current packages from primary sources.

## Start with the shared platform {#shared-platform}

ASP.NET Core owns the server and most cross-cutting runtime behavior. Whether an endpoint is expressed as a Minimal API delegate, a controller action, a Giraffe `HttpHandler`, a Falco endpoint, or an Oxpecker handler, production work still includes:

- host startup, configuration, dependency injection, and lifetime;
- Kestrel or another supported server integration;
- middleware ordering and endpoint routing;
- authentication schemes and authorization policies;
- request limits, cancellation, timeouts, streaming, and response start semantics;
- logging, metrics, distributed tracing, health behavior, and deployment;
- `HttpContext`, HTTP semantics, proxies, TLS, and untrusted input.

A library can provide safer defaults or a more composable API for some of these concerns. It cannot make their operational semantics disappear. Knowing the platform is portable knowledge across every choice below.

Microsoft's [.NET 10 API guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) presents Minimal APIs and controller-based APIs as the two platform approaches. It recommends starting with Minimal APIs for new HTTP APIs, while pointing to controllers for requirements such as advanced model-binding extensibility, application-model features, or OData. That is a platform default, not a command that every F# team must avoid community libraries.

## Inspect the representative Minimal API {#representative-sample}

The web sample is intentionally much smaller than the booking capstone. It answers one question: what does a direct F# endpoint look like when input, output, and errors remain explicit?

The project uses `Microsoft.NET.Sdk.Web`, targets `net10.0`, and has no third-party package reference. Its lock file records `FSharp.Core` 10.1.301. The public JSON types are ordinary CLR-friendly records rather than domain discriminated unions:

```fsharp:line-numbers [Program.fs]
[<CLIMutable>]
type GreetingRequestDto =
    { [<JsonPropertyName("name")>]
      Name: string | null }

[<CLIMutable>]
type GreetingResponseDto =
    { [<JsonPropertyName("message")>]
      Message: string }

[<CLIMutable>]
type WebSampleErrorDto =
    { [<JsonPropertyName("code")>]
      Code: string
      [<JsonPropertyName("message")>]
      Message: string }
```
`GreetingRequestDto.Name` admits `null` because JSON is an untrusted boundary. Validation converts that representation into a nonblank local `name` before success. This repeats a central lesson of Part VI: permissive boundary representation does not require a permissive domain.

### Make framework adaptation explicit {#explicit-adaptation}

The handler has type `HttpContext -> Task` and is then wrapped in ASP.NET Core's `RequestDelegate`. It checks the media type, uses strict case-sensitive JSON that rejects unknown members, validates the name, and produces only stable error codes and messages.

```fsharp:line-numbers [Program.fs]
let private greet (context: HttpContext) : Task =
    task {
        if not (context.Request.HasJsonContentType()) then
            return!
                writeError
                    context
                    StatusCodes.Status415UnsupportedMediaType
                    "unsupported_media_type"
                    "Content-Type must be a JSON media type."
        else
            try
                let! request =
                    JsonSerializer.DeserializeAsync<GreetingRequestDto>(
                        context.Request.Body,
                        jsonOptions,
                        context.RequestAborted
                    )

                match request with
                | null ->
                    return! writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                | value ->
                    match value.Name with
                    | null ->
                        return!
                            writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                    | name when String.IsNullOrWhiteSpace name ->
                        return!
                            writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                    | name ->
                        return! writeJson context StatusCodes.Status200OK { Message = $"Hello, {name.Trim()}!" }
            with
            | :? JsonException ->
                return!
                    writeError
                        context
                        StatusCodes.Status400BadRequest
                        "invalid_json"
                        "The request body is not valid for this endpoint."
            | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
                return raise error
            | _ when context.Response.HasStarted -> context.Abort()
            | _ ->
                return!
                    writeError
                        context
                        StatusCodes.Status500InternalServerError
                        "internal_error"
                        "The request could not be completed."
    }
```
Several details matter more than the number of lines:

- `RequestAborted` reaches deserialization and is rethrown when the client cancels;
- malformed representation and missing business input are different errors;
- the invalid body and unexpected exception are never returned;
- a response already started cannot safely be replaced with JSON, so the context is aborted;
- the handler returns the non-generic `Task` expected by `RequestDelegate`.

F# 10 null checking also forced the handler to match `value.Name` before calling `Trim`. That friction is useful at this boundary: the compiler refused to pretend a deserialized string was non-null.

The final mapping and host contain no hidden framework:

```fsharp:line-numbers [Program.fs]
let map (application: WebApplication) =
    ArgumentNullException.ThrowIfNull(application, nameof application)

    application.MapPost("/api/greetings", RequestDelegate greet) |> ignore
```
The mapping style is lower-level than automatic Minimal API parameter binding. That is deliberate for a stable teaching contract, not a general recommendation to deserialize every request by hand. The [.NET 10 Minimal API reference](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0) documents built-in binding, validation, responses, filters, authorization, and other platform features. Choose automatic binding when its contract matches yours; take control when compatibility or error-response requirements justify it.

### State exactly what the tests cover {#sample-evidence}

Focused `TestServer` cases run the real route and handler:

- one valid body is trimmed and returns the exact expected success JSON;
- malformed JSON, absent name, blank name, incorrect property case, and an unknown member fail safely;
- a non-JSON media type returns `415` rather than entering the handler contract.

The example is intentionally small. It does not test a real socket, proxy, TLS, authentication, rate limiting, body-size policy, or deployment. Those are not mysteriously supplied by using Minimal APIs.

## Choose the level of abstraction first {#abstraction-level}

Before comparing names, decide what kind of endpoint vocabulary the team wants.

| Level | Typical unit | Composition style | Primary tradeoff |
|---|---|---|---|
| platform Minimal API | delegate or `RequestDelegate` mapped to a route | endpoint routing and middleware | direct platform access; some APIs and overloads are designed around C# |
| platform controllers | attributed class/action | filters, model binding, application model | mature extension surface; more object/attribute ceremony in F# |
| functional micro-framework | F# handler function and combinators | pipelines, lists, or endpoint DSL | idiomatic F# flow; another API and package lifecycle |
| opinionated application framework | controllers/routers/application builders or generators | conventions and structured modules | faster convention when it fits; larger upgrade and escape-hatch surface |

Do not decide by counting source lines in “Hello world.” Authentication failure, JSON evolution, streaming cancellation, OpenAPI customization, test replacement, and deployment diagnostics reveal the real abstraction boundary.

## Platform-native Minimal APIs {#minimal-apis}

Choose direct Minimal APIs when the team already understands ASP.NET Core, wants the smallest dependency surface, or shares conventions and infrastructure with C# services. Every platform document, middleware package, host feature, and diagnostic integration applies without a wrapper translation step.

Strengths include:

- no community framework dependency beyond the .NET shared framework;
- direct endpoint routing, filters, results, DI, authentication, and OpenAPI integration;
- easy interoperability with C# infrastructure and examples;
- a small composition root that can call an F# functional core;
- `TestServer` or `WebApplicationFactory` integration using standard ASP.NET tools.

Common F# friction includes delegate overload inference, nullability at model-binding boundaries, attribute-oriented examples, and C#-first documentation. Explicit type annotations, `RequestDelegate`, small adapter functions, and boundary DTOs usually contain that friction. If the endpoint layer grows into a private collection of handler combinators, reconsider whether a maintained F# library already supplies the desired vocabulary.

## Controller-based APIs {#controllers}

Controllers remain a platform-native alternative. Choose them for controller-specific extension points or an organization-wide controller standard. They may also suit mixed-language teams that value a uniform class/action API more than an F# endpoint DSL.

F# can define controller classes, attributes, methods, tasks, and CLR DTOs. The friction is architectural rather than impossible interoperability: inheritance, mutable binding models, attributes, action overloads, and framework conventions may dominate code that would otherwise be functions and explicit data.

Keep controllers thin. Convert boundary DTOs to valid domain inputs, call a workflow, then exhaustively translate its result. Do not move domain states into nullable controller properties merely to resemble a C# tutorial.

## Giraffe: composable middleware-style handlers {#giraffe}

[Giraffe](https://github.com/giraffe-fsharp/Giraffe) describes itself as a functional ASP.NET Core micro-framework. Its core `HttpHandler` model composes handlers and can short-circuit or continue an ASP.NET Core pipeline. That vocabulary suits teams that want routing, binding, responses, and authorization expressed as reusable F# functions.

Why choose it:

- a mature functional handler model with a broad body of examples and extensions;
- explicit composition and reuse within ASP.NET Core;
- support for APIs and server-rendered HTML through related packages;
- a familiar choice for existing Giraffe and SAFE-stack codebases.

Friction to evaluate:

- the continuation-passing handler type and its operators are concepts the team must learn;
- handler-specific abstractions can spread into application code if boundaries are not enforced;
- JSON/view/authorization extension choices add their own compatibility matrix;
- platform features may require understanding both Giraffe ordering and ASP.NET middleware ordering.

The stable package checked for this book is [Giraffe 8.3.0](https://www.nuget.org/packages/Giraffe/8.3.0), published in July 2026. Pin the chosen package and read its release notes during upgrades; this chapter does not compile a Giraffe sample and therefore makes no stronger compatibility claim.

## Falco: endpoint-oriented functional toolkit {#falco}

[Falco](https://www.falcoframework.com/) is a functional-first toolkit on ASP.NET Core. Its documented style builds endpoint values from routing and response functions, then installs them into a `WebApplication`. It also offers a native F# markup engine and related OpenAPI/HTMX packages.

Why choose it:

- a compact endpoint vocabulary that stays close to ASP.NET Core;
- uniform request-reading and response functions;
- server-rendered F# markup when that is part of the product;
- standard middleware remains available.

Friction to evaluate:

- a smaller ecosystem means each required integration deserves a spike;
- related markup, OpenAPI, or HTMX packages are separate version decisions;
- examples still require platform knowledge for security, hosting, and operations;
- switching handler vocabulary later touches the outer web layer.

The stable package checked here is [Falco 5.2.0](https://www.nuget.org/packages/Falco/5.2.0), which includes `net8.0`, `net9.0`, and `net10.0` assets. NuGet also listed 6.0 prereleases on the review date; this book does not silently treat a prerelease as the stable recommendation.

## Oxpecker: F# handlers on endpoint routing {#oxpecker}

[Oxpecker](https://github.com/Lanayx/Oxpecker) builds on ASP.NET Core endpoint routing and inherits much of Giraffe's successful API vocabulary. Its documented `EndpointHandler` is `HttpContext -> Task`, while `EndpointMiddleware` composes around the next handler. Related packages cover views, HTMX, OpenAPI, and other full-stack concerns.

Why choose it:

- direct alignment with endpoint routing and a terminal handler type close to `RequestDelegate`;
- an F#-first composition API with typed routing and response helpers;
- integrated options for server-rendered and HTMX-oriented applications;
- a migration guide for teams familiar with Giraffe.

Friction to evaluate:

- it is newer, so its API lifecycle and production track record differ from older choices;
- its package target may bind the service to a newer .NET runtime sooner;
- the broad full-stack family can tempt a team to adopt features it does not need;
- migration similarity does not mean every Giraffe behavior is identical.

The checked stable package is [Oxpecker 2.1.1](https://www.nuget.org/packages/Oxpecker/2.1.1), whose package asset targets `net10.0`. That is a fact to include in runtime planning, not evidence that it is inherently better or worse.

## Saturn: convention-rich functional MVC {#saturn}

[Saturn](https://github.com/SaturnFramework/Saturn) provides an opinionated server-side functional MVC model over Giraffe, with application, router, and controller conventions. It can reduce assembly work when those conventions match the product, especially in an existing Saturn or SAFE application.

A new .NET 10 service should examine Saturn's maintenance status and target-framework fit carefully. The checked stable package is [Saturn 0.17.0](https://www.nuget.org/packages/Saturn/0.17.0), published in April 2024 with a `net6.0` asset and a dependency on Giraffe 6.4 or later. NuGet computes compatibility with later TFMs, but that does not confirm every generator, dependency, authentication path, or deployment behavior on .NET 10.

Therefore do not label Saturn “dead,” and do not select it from an old tutorial without a spike. For an existing system, upgrade evidence and convention value may justify it. For a new system, compare its current issue/release activity, template output, transitive graph, and required features against the simpler alternatives.

## Keep the version table honest {#version-table}

The following is a dated observation, not an evergreen ranking:

| Choice | Stable surface checked on 2026-08-25 | Status in this chapter | Key adoption question |
|---|---|---:|---|
| ASP.NET Core Minimal API | .NET SDK 10.0.301; ASP.NET Core runtime 10.0.9 | illustrated | can the team contain C#-shaped API friction? |
| controller API | ASP.NET Core 10 platform docs | research only | do required controller extension points justify the ceremony? |
| Giraffe | NuGet 8.3.0 | research only | does continuation-style handler composition fit the team? |
| Falco | NuGet 5.2.0 stable | research only | do its focused endpoints and related packages cover required integrations? |
| Oxpecker | NuGet 2.1.1, `net10.0` asset | research only | is its newer endpoint/full-stack surface acceptable to operate and upgrade? |
| Saturn | NuGet 0.17.0, `net6.0` asset | research only | do its conventions outweigh the required .NET 10 compatibility proof? |

“Illustrated” means this chapter shows the approach, not that the book site ships an executable service. “Research only” is not a negative quality judgment; evaluate the option in the adopting application.

## Separate the decisions that often get bundled {#separate-decisions}

A framework name should not silently decide every web concern.

### API contract and serialization {#contract-serialization}

Decide whether external JSON mirrors CLR DTOs, uses F# unions through a named converter, or follows a schema-first contract. Specify case sensitivity, unknown members, nulls, versioning, error shapes, and size limits. A framework's convenient binder is useful only when it produces the contract you intend.

### OpenAPI and clients {#openapi-clients}

A generated OpenAPI document is trustworthy only after tests compare it with real routes and error responses. Decide whether annotations, endpoint metadata, or a separate schema is authoritative. Keep at least one consumer test, like the capstone's C# client, because a valid document can still describe an unusable API.

### HTML, HTMX, or a separate frontend {#html-frontend}

For server-rendered HTML, compare escaping-by-default, typed markup, layouts, streaming, forms, antiforgery, localization, and tooling. Giraffe, Falco, and Oxpecker have different view ecosystems. For an independent browser SPA, the backend choice need not determine the frontend language; Chapter 41 covers Fable separately.

### Authentication and authorization {#auth}

Prefer ASP.NET Core authentication schemes and authorization policies unless a wrapper has a specific, verified benefit. Confirm middleware order, challenge/forbid behavior, endpoint metadata, test replacement, and proxy/TLS assumptions. A functional `requiresRole` helper does not configure identity validation by itself.

### Dependency injection {#dependency-injection}

The host container is practical at infrastructure boundaries. Retrieve dependencies at composition time or through explicit handler parameters, then pass small functions into the core. Reaching into `HttpContext.RequestServices` throughout business logic trades constructor ceremony for hidden dependencies.

## Choose by scenario, not identity {#scenario-guide}

Use these as starting hypotheses to test:

| Scenario | First candidate | Reason to reconsider |
|---|---|---|
| small JSON service in a mixed C#/F# platform team | Minimal APIs | repeated delegate/binding adapters are becoming a private framework |
| F# team wants reusable functional HTTP pipelines | Giraffe | continuation model or extension compatibility adds more friction than value |
| compact API or server-rendered app wants a focused F# endpoint toolkit | Falco | a required integration lacks convincing support evidence |
| team wants endpoint routing with Giraffe-like handlers and modern view/HTMX options | Oxpecker | runtime target or newer API lifecycle conflicts with deployment policy |
| existing convention-heavy Saturn/SAFE application | Saturn | upgrade graph or current platform evidence is insufficient |
| API requires controller-specific application model or OData | controllers | requirements can be met more simply with endpoint routing |

A product may use more than one API style during migration, but do not keep two permanent ways to express the same policy without assigning responsibility. Mounting a functional framework beside platform endpoints is technically possible; the cost is duplicated conventions, filters, errors, metadata, and tests.

## Protect the functional core from framework churn {#framework-boundary}

The Part VI architecture transfers directly:

```text
HTTP framework handler
  -> parse and validate boundary representation
  -> call a small application function
  -> exhaustively map declared result
  -> write stable transport representation
```

Keep `HttpContext`, framework handler types, binding attributes, and response helpers in the web project. Domain and application projects should not reference Giraffe, Falco, Oxpecker, Saturn, or ASP.NET Core merely because the executable uses one of them.

This containment makes a framework change finite. It does not make it free: routing metadata, authentication, streaming, multipart input, OpenAPI, filters, and integration tests still live at the boundary. But business invariants and effect protocols remain stable while the adapter changes.

## Test the boundary at four layers {#testing-strategy}

Use four layers:

1. pure tests for validation and workflow decisions;
2. focused handler tests only where framework-free invocation is meaningful;
3. `TestServer` integration tests for binding, routing, middleware, error bodies, authentication metadata, and cancellation;
4. a small real-process smoke for startup configuration, sockets, and deployment packaging.

Reserve broader tests for important infrastructure scenarios, as Microsoft's [integration-testing guidance](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) recommends. Library helpers complement rather than replace the ASP.NET pipeline when middleware matters. Run the same contract cases against every candidate; fewer assertions are not evidence of a simpler framework.

## Evaluate performance and security with the actual path {#performance-security}

Framework benchmarks can reveal overhead patterns, but they do not include your JSON payloads, authentication, logging, database, provider latency, response size, allocation profile, or failure path. Use Chapter 31's measurement discipline: define a workload and budget, profile the complete candidate, then optimize the measured bottleneck.

Security likewise belongs to the final pipeline. Test body and header limits, malformed encodings, authentication challenge/forbid behavior, and authorization metadata. Cover CSRF for cookie-based forms, output encoding, uploads, redirects, proxy headers, rate limits, timeouts, logging, and failures after a response starts. Do not infer safety from “functional,” “minimal,” or “typed.”

Third-party packages expand the supply-chain and upgrade surface. Pin direct dependencies, retain lock files, inspect transitive changes, read release notes, and rerun contract/security tests. Avoid copying an installation command with `*` into reproducible project setup.

## Run a bounded adoption spike {#adoption-spike}

Time-box one representative vertical slice and verify:

- the real DTO/serializer contract through success, validation, and unexpected failures;
- required authentication, authorization, cancellation, and OpenAPI/client generation;
- one diagnostic correlation through middleware;
- locked Release build, `TestServer`, real-process startup, and publish;
- one compatible upgrade and a deletion condition.

Compare correctness gaps, concepts, package graph, diagnostics, tests, documentation, and ownership before line count. Delete losing spikes instead of supporting several stacks.

## Avoid common ecosystem mistakes {#common-mistakes}

- Treating ASP.NET Core and an F# library as mutually exclusive, or assuming helpers configure identity, limits, TLS, and telemetry, misunderstands the stack.
- Selecting by syntax, downloads, or isolated benchmarks ignores product, middleware, operations, and upgrade requirements.
- Copying C# Minimal API overloads without annotations can produce confusing F# inference failures.
- Serializing domain unions directly or moving `HttpContext` into the core couples private design to the boundary.
- Computed target compatibility is not active support; age alone does not prove abandonment; a newer prerelease is not a stable default.
- Installing several web DSLs “for flexibility” creates several policy surfaces to secure and test.
- Rebuilding the booking capstone in every framework produces breadth without additional understanding.

## Exercises {#exercises}

### Exercise 1: choose for three teams {#exercise-01}

Evaluate these teams separately:

1. A mixed C#/F# team is building a small internal JSON API under an organization-wide ASP.NET platform.
2. An F# team is building server-rendered HTML with reusable functional handlers and HTMX.
3. An existing Saturn service is moving to .NET 10 while adding no product features.

For each team, choose a starting web surface, compare at least two candidates, and describe their package and operational boundaries. Finish with the evidence that would change the choice.

### Exercise 2: preserve the greeting contract in a spike {#exercise-02}

Select Giraffe, Falco, or Oxpecker and sketch a bounded spike that replaces only `WebSample.map` and its handler. Preserve the exact success/error JSON, strict member policy, cancellation behavior, and HTTP contract cases. List the package version, new transitive surface, framework concepts introduced, and a deletion criterion if the spike loses. Do not move DTO validation into a domain project reference to the framework.

### Exercise 3: design a reversible migration {#exercise-03}

A 40-endpoint service has handlers, authentication helpers, generated OpenAPI, and integration tests tied to one F# framework. Design an incremental move to platform Minimal APIs or another functional framework. Identify a route-by-route compatibility seam, shared error/DTO policy, authentication responsibility, collision prevention, contract comparison, rollout observation, and the condition for removing the old framework package.

[Read the chapter solutions](../solutions/ch-39-web-ecosystem).

Chapter 40 moves from HTTP boundaries to data access, type providers, analysis, visualization, and machine learning—another area where workload characteristics matter more than one universal stack.
