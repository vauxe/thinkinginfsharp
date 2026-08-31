---
title: "Chapter 39: ASP.NET Core and the F# Web Ecosystem"
description: "Choose among platform-native Minimal APIs, controllers, and functional F# web libraries by system needs, team fit, and verified maintenance—not fashion."
translationKey: part-07/ch-39-web-ecosystem
---

# Chapter 39: ASP.NET Core and the F# Web Ecosystem {#overview}

F# does not need a separate web server to participate in modern .NET. An F# project can use Kestrel, endpoint routing, dependency injection, configuration, authentication, authorization, logging, metrics, and `TestServer` directly from ASP.NET Core. Several community libraries then add F#-shaped handlers, composition operators, views, or conventions on that same platform.

The practical question is not “Which F# framework wins?” Ask which API style removes real friction without hiding platform behavior the team must still understand. Begin with a verified platform-native slice, then compare current packages from primary sources.

This chapter's web code is an in-page complete-project template. The current repository does not contain the former `examples/ecosystem/web` project or its tests. The text uses the template to explain API shape. Adoption requires reconstructing the stated project first, then running build, contract, and real-process smoke verification.

Three vocabularies meet here. `Minimal API`, `RequestDelegate`, and `HttpContext` belong to ASP.NET Core. `HttpHandler`, `EndpointHandler`, and similar names belong to particular community libraries. Records, discriminated unions, pattern matching, and computation expressions are F# language constructs. Do not treat one web library's type names as universal F# terminology.

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

The web template is intentionally much smaller than Part VI's booking design. It answers one question: what does a direct F# endpoint look like when input, output, and errors remain explicit?

When reconstructed, the project should use `Microsoft.NET.Sdk.Web`, target `net10.0`, and need no third-party package reference for this template. Retain the newly generated lock file after restore; do not copy a resolved version from the deleted project. The public JSON types are ordinary CLR-friendly records rather than domain discriminated unions:

Read the excerpts inside this `Program.fs` file boundary:

- start with `namespace ThinkingInFSharp.Ecosystem.Web` and open `System`, the JSON and task namespaces, and the ASP.NET Core Builder/HTTP namespaces;
- place the three DTOs at namespace scope;
- place `jsonOptions`, `writeJson`, `writeError`, `greet`, and `map` inside `[<RequireQualifiedAccess>] module WebSample`;
- end with the `Program` entry module.

The code blocks omit that shell for focus and cannot run independently.

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

The final mapping contains no hidden framework:

```fsharp:line-numbers [Program.fs]
let map (application: WebApplication) =
    ArgumentNullException.ThrowIfNull(application, nameof application)

    application.MapPost("/api/greetings", RequestDelegate greet) |> ignore
```
That function still lives inside `WebSample`. The host at the end of the file makes it executable:

```fsharp:line-numbers [Program.fs]
module Program =
    [<EntryPoint>]
    let main arguments =
        let builder = WebApplication.CreateBuilder arguments
        use application = builder.Build()
        WebSample.map application
        application.Run()
        0
```
The mapping style is lower-level than automatic Minimal API parameter binding. That is deliberate for a stable teaching contract, not a general recommendation to deserialize every request by hand. The [.NET 10 Minimal API reference](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0) documents built-in binding, validation, responses, filters, authorization, and other platform features. Choose automatic binding when its contract matches yours; take control when compatibility or error-response requirements justify it.

### Plan the contract coverage precisely {#sample-evidence}

After reconstructing the project, the minimum `TestServer` contract cases should run the actual route and handler and verify that:

- one valid body is trimmed and returns the exact expected success JSON;
- malformed JSON, absent name, blank name, incorrect property case, and an unknown member fail safely;
- a non-JSON media type returns `415` rather than entering the handler contract.

Even when those cases pass, they do not test a real socket, proxy, TLS, authentication, rate limiting, body-size policy, or deployment. Those are not mysteriously supplied by using Minimal APIs.

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

| Choice | Stable surface checked on 2026-08-31 | Status in this chapter | Key adoption question |
|---|---|---:|---|
| ASP.NET Core Minimal API | locally checked: .NET SDK 10.0.302; ASP.NET Core runtime 10.0.10 | in-page project template | can the team contain C#-shaped API friction? |
| controller API | ASP.NET Core 10 platform docs | research only | do required controller extension points justify the ceremony? |
| Giraffe | NuGet 8.3.0 | research only | does continuation-style handler composition fit the team? |
| Falco | NuGet 5.2.0 stable | research only | do its focused endpoints and related packages cover required integrations? |
| Oxpecker | NuGet 2.1.1, `net10.0` asset | research only | is its newer endpoint/full-stack surface acceptable to operate and upgrade? |
| Saturn | NuGet 0.17.0, `net6.0` asset | research only | do its conventions outweigh the required .NET 10 compatibility proof? |

“In-page project template” means the text provides a reconstruction structure; it does not mean the book site ships an executable service. “Research only” is not a negative quality judgment; evaluate the option in the adopting application.

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

## Exercises {#exercises}

### Exercise 1: choose for three teams {#exercise-01}

Evaluate these teams separately:

1. A mixed C#/F# team is building a small internal JSON API under an organization-wide ASP.NET platform.
2. An F# team is building server-rendered HTML with reusable functional handlers and HTMX.
3. An existing Saturn service is moving to .NET 10 while adding no product features.

For each team, choose a starting web surface, compare at least two candidates, and describe their package and operational boundaries. Finish with the evidence that would change the choice.


::: details Answer

#### Team A: mixed languages on one platform {#exercise-01-team-a}

Start with platform Minimal APIs.

The organization already operates ASP.NET Core, the service is a small internal JSON API, and both C# and F# engineers need to understand the HTTP boundary. Direct endpoint routing minimizes extra framework indirection, reuses standard authorization and diagnostic conventions, and keeps F#-specific design in the domain and workflow.

Compare it with Falco 5.2.0 in a two-day spike. Falco could win if repeated request/response adapters dominate the direct implementation and its endpoint vocabulary materially improves review. Run the same DTO, strict JSON, policy authorization, cancellation, OpenAPI, `TestServer`, and publish checks. Reject Falco if required organization middleware or endpoint metadata needs custom bridges with no clear owner.

Evidence that would change the initial choice includes:

- five representative endpoints repeatedly fight delegate overloads or binding behavior;
- Falco reduces boundary code while preserving the exact organization contract;
- both-language maintainers can debug through the Falco and ASP.NET layers;
- its locked package graph passes the organization's support and vulnerability policy;
- upgrade and incident exercises remain understandable.

Do not select controllers merely because C# engineers know them. Select them if a controller-specific extension—such as the required application model or OData path—is actually present. Microsoft's [API overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) makes that distinction explicit.

#### Team B: F#, server HTML, and HTMX {#exercise-01-team-b}

Start a comparison between Giraffe 8.3.0 and Oxpecker 2.1.1; provisionally choose Oxpecker only after its `net10.0` target and newer lifecycle pass the team's support gate.

Oxpecker aligns with endpoint routing, exposes an `HttpContext -> Task` terminal handler, and offers related view and HTMX packages. Those features match the requirements. Giraffe remains the lower-adoption-risk alternative because it has a longer-lived handler ecosystem and can use separate view or HTMX integrations.

The spike must include more than a counter button:

- escaped dynamic HTML and a deliberate raw-HTML review;
- an antiforgery-protected form using the actual cookie/auth topology;
- validation errors rendered for both full-page and HTMX requests;
- authentication challenge and authorization forbid behavior;
- route generation, layouts, localization, static assets, and CSP policy;
- `TestServer` plus one browser interaction and accessibility check;
- package upgrade and deployment diagnostics.

Choose Giraffe if maturity, existing knowledge, or an extension outweighs Oxpecker's endpoint-routing fit. Choose Falco 5.2.0 as a third candidate only if its markup/HTMX surface better matches the templates. Do not combine all three.

#### Team C: existing Saturn service {#exercise-01-team-c}

Do not begin with a rewrite. The product asks only for a move to .NET 10, with no feature changes. First inventory Saturn 0.17.0, its `net6.0` asset, the Giraffe dependency, authentication packages, generators, and all direct or transitive warnings.

Create a branch that:

1. targets `net10.0` under warnings as errors;
2. restores from a newly reviewed lock file;
3. runs every current contract and browser test;
4. publishes and starts in the production-like runtime image;
5. exercises authentication, error handling, static assets, diagnostics, shutdown, and load;
6. records unsupported or obsolete API use and the maintainers' published support status.

If that passes the organization's support policy, ship the runtime move separately and keep Saturn for now. A successful runtime upgrade reduces uncertainty without mixing platform risk with a framework rewrite.

If it fails materially, compare a move to Giraffe—closest to Saturn's underlying handler model—with a move to Minimal APIs—smallest long-term dependency surface. Estimate from three representative routes, not all 40. Preserve public contracts and authentication behavior before improving internal style.

:::

### Exercise 2: preserve the greeting contract in a spike {#exercise-02}

Select Giraffe, Falco, or Oxpecker and sketch a bounded spike that replaces only `WebSample.map` and its handler. Preserve the exact success/error JSON, strict member policy, cancellation behavior, and HTTP contract cases. List the package version, new transitive surface, framework concepts introduced, and a deletion criterion if the spike loses. Do not move DTO validation into a domain project reference to the framework.


::: details Answer

#### Bound a Falco 5.2.0 experiment {#exercise-02-scope}

Create a temporary project or branch that pins Falco 5.2.0. Its official package metadata also brings Falco.Markup and an FSharp.Core minimum into the reviewed graph. Record the resolved lock file rather than installing an unconstrained version.

Only the outer adapter may change:

```text
POST /api/greetings
  -> Falco endpoint/handler
  -> existing strict JsonSerializerOptions
  -> existing boundary DTO and name validation
  -> existing stable success/error writers
```

Using Falco's request and response helpers is optional during the first pass. The existing contract is the experimental control. If a helper cannot reject incorrectly cased or unknown JSON members with the required `invalid_json` format, retain the manual strict deserializer. Alternatively, record a proposed contract change explicitly; never weaken the test silently.

The handler must pass the request cancellation token into deserialization and rethrow client cancellation. It must not catch a canceled task as `internal_error`. It must preserve the “response already started” boundary or demonstrate the framework's equivalent behavior.

#### Run the same executable checks {#exercise-02-evidence}

First create `WebSampleTests` for the direct template from the expected behavior listed in this chapter. Then make the Falco spike run that same contract suite unchanged. Add only framework-specific assertions that matter, such as endpoint metadata or middleware ordering. Then run:

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
| platform access | direct | is direct ASP.NET Core access still clear? |
| diagnostics | standard ASP.NET context | are route names/status/correlation still visible? |
| maintenance | Microsoft .NET support | who is responsible for Falco upgrades? |

Delete the spike if any of these conditions holds:

- it changes the contract without a product reason;
- required middleware needs custom bridges;
- cancellation or diagnostics become harder to follow;
- it fails the publish policy; or
- the code saved does not justify maintaining a second API lifecycle.

If the spike wins, replace the old route once, document the conventions, and keep framework types out of validation and application modules.

This design intentionally does not claim the spike passes. The chapter illustrates the direct Minimal API approach; either implementation must be verified in the adopting application.

:::

### Exercise 3: design a reversible migration {#exercise-03}

A 40-endpoint service has handlers, authentication helpers, generated OpenAPI, and integration tests tied to one F# framework. Design an incremental move to platform Minimal APIs or another functional framework. Identify a route-by-route compatibility seam, shared error/DTO policy, authentication responsibility, collision prevention, contract comparison, rollout observation, and the condition for removing the old framework package.


::: details Answer

#### Inventory behavior before handlers {#exercise-03-inventory}

Build a route catalog containing method, normalized template, request/response schema, status codes, authentication scheme, authorization policy, filters/middleware, streaming/body limits, OpenAPI operation, diagnostics, and current contract tests. Search for framework types outside the web project; those are coupling work, not route rewrites.

Extract or confirm three neutral seams:

- boundary DTOs and strict serializer/error policy;
- application functions whose inputs and `Result` outputs do not mention the web framework;
- ASP.NET Core authentication and authorization policies managed by host configuration.

Do not create one giant “universal handler” abstraction that models every framework feature. Small application ports plus public transport contracts are the stable seam.

#### Move one route without route collisions {#exercise-03-routing}

Maintain a machine-checked route registry: every `(method, route pattern)` belongs to exactly one old or new mapper. The composition root installs both during migration. A build or startup test enumerates endpoint data sources and rejects duplicate method/template pairs.

Select a representative low-risk route that still exercises JSON, authorization, one application result, and an error. Map it in the new surface, remove it from the old map in the same change, and run the original contract tests unchanged.

For additional confidence, host old and new variants separately in tests under private comparison prefixes, feed identical requests, and compare normalized status, headers, and JSON semantics. Do not expose both prefixes in production or compare volatile headers byte for byte.

ASP.NET Core schemes and policies continue to manage authentication. Verify that new endpoints carry equivalent policy metadata and produce the same challenge and forbid behavior. A successful `200` test with an authenticated fixture does not cover unauthenticated or unauthorized paths.

OpenAPI comparison should normalize ordering and known generator metadata, then detect removed operations, schema changes, status changes, and security requirements. Review intentional changes; do not overwrite the baseline automatically.

#### Roll out and remove the old package {#exercise-03-rollout}

Move routes in small groups while observing traffic, status, latency, cancellation, and error classes for each template. Canary the same immutable artifact if the deployment platform supports it. Roll back at the artifact level. Do not switch between two handlers per request unless a demonstrated need justifies the added runtime path.

The old framework can be removed only when:

- no route, middleware, view, binding type, or test helper uses it;
- no application/domain project references it directly or transitively by design;
- endpoint enumeration shows the expected complete route set once;
- authentication, OpenAPI, diagnostics, real-process smoke, and publish tests pass;
- the lock file and deployment artifact no longer contain the package;
- the rollback window for the last old-framework artifact has closed.

If migration stalls, keep route responsibility explicit. A documented mixed boundary is safer than pretending the old package is gone while hidden helpers still control policy.

:::


Chapter 40 moves from HTTP boundaries to data access, type providers, analysis, visualization, and machine learning—another area where workload characteristics matter more than one universal stack.
