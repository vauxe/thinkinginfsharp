---
title: "Chapter 41: Fable, Elmish, and Browser Applications"
description: "Choose a browser architecture from runtime constraints, state complexity, rendering needs, interoperability, and deployment verification."
translationKey: part-07/ch-41-fable-elmish
---

# Chapter 41: Fable, Elmish, and Browser Applications {#overview}

F# remains F# in a Fable project: records, discriminated unions, pattern matching, modules, functions, and type inference still shape the program. The runtime does not remain .NET. For the JavaScript target, Fable translates supported F# and library constructs into JavaScript, then the browser executes that JavaScript with browser security, scheduling, numeric, packaging, and API rules.

That distinction prevents two opposite mistakes. A .NET team must not assume every BCL or NuGet API works in a browser. A JavaScript team must not treat Fable as an isolated platform that replaces npm, bundlers, DOM knowledge, accessibility, or browser diagnostics. The useful question is: “Which logic benefits from F# modeling, which boundary belongs to JavaScript or the browser, and how much state architecture does this interface actually need?”

## Fable changes the target, not the source language {#target-runtime}

A browser Fable pipeline has four distinct products:

```text
F# source + .fsproj + compatible NuGet packages
  -> Fable-generated JavaScript modules
  -> JavaScript bundler production assets
  -> browser execution under Web APIs and browser policy
```

The F# compiler still type-checks the project. Fable then translates the supported program. Vite or another JavaScript tool resolves modules, tree-shakes, minifies, hashes, and emits assets. The browser loads those assets; it does not load the project's `.dll` or start the CLR.

This is transpilation to a target ecosystem, not remote control of a .NET process and not the same mechanism as .NET WebAssembly. Fable can also target other languages, but this chapter and the browser sample verify only JavaScript in a browser.

### Keep three compatibility questions separate {#three-compatibility-questions}

For every dependency or API, ask:

1. Does ordinary F# type-check this source?
2. Does Fable support or translate the used language/library surface for JavaScript?
3. Does the emitted JavaScript work in the target browsers and bundler configuration?

A `netstandard2.0` asset answers neither the second nor the third question by itself. Conversely, a JavaScript library can work well through a typed Fable binding even though it has no .NET implementation.

## The browser sample: one minimal browser boundary {#verified-slice}

The browser sample intentionally avoids React and Elmish packages. It isolates the smallest useful path: F# source becomes a production JavaScript bundle, attaches accessible DOM events, and updates visible state. A real application must still test that bundle in its supported browsers.

### The locked project surface {#locked-project}

```xml:line-numbers [FableSample.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="App.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Fable.Browser.Dom" Version="2.20.0" />
    <PackageReference Include="Fable.Core" Version="5.2.0" />
  </ItemGroup>
</Project>
```
To reproduce this sample in an application, record these independently moving inputs:

- the .NET SDK and F# language versions;
- the local Fable tool version;
- Fable.Core and Fable.Browser.Dom in `packages.lock.json`;
- the JavaScript package manager and Vite versions in the JavaScript lock file;
- the browser versions used by automated and manual acceptance tests.

The compiler, Fable.Core, and browser bindings have separate release cadences. Matching their major or minor numbers by appearance is not a compatibility strategy; restore their declared graph, compile it, and run the target.

### Read the F# before the generated JavaScript {#sample-code}

```fsharp:line-numbers [App.fs]
module FableSample.App

open Browser.Dom

type Model = { Count: int }

type Message =
    | Increment
    | Reset

let initialModel = { Count = 0 }

let update message model =
    match message with
    | Increment -> { model with Count = model.Count + 1 }
    | Reset -> initialModel

let private elementById id =
    match document.getElementById id with
    | null -> failwith $"Required element #{id} was not found."
    | element -> element

let private countOutput = elementById "count"
let private incrementButton = elementById "increment"
let private resetButton = elementById "reset"
let mutable private model = initialModel

let private render () =
    countOutput.textContent <- $"Count: {model.Count}"

    if model.Count = 0 then
        resetButton.setAttribute ("disabled", "")
    else
        resetButton.removeAttribute "disabled"

let private dispatch message =
    model <- update message model
    render ()

incrementButton.addEventListener ("click", fun _ -> dispatch Increment)
resetButton.addEventListener ("click", fun _ -> dispatch Reset)

render ()
document.documentElement.setAttribute ("data-fable-ready", "true")
```
`Model`, `Message`, `initialModel`, and `update` are ordinary F#. The `update` function is deterministic: the same message and model produce the same next model. It does not know about elements, clicks, or rendering.

`elementById`, event registration, the mutable current model, and `render` form the effectful browser shell. Mutation here is deliberate local runtime state, not an invitation to make domain transitions implicit. Missing required markup fails during startup instead of producing a partly wired page.

The final attribute is a readiness contract for the browser smoke. It is set only after elements are found, listeners are attached, and the initial model is rendered.

### What the checks cover—and do not {#sample-evidence}

The production command performs locked .NET tool/package restore, Fable compilation without cache reuse, and a Vite production build. The current output transforms 15 modules into an HTML entry and a hashed JavaScript asset. The exact file name and size are build outputs, not public application contracts.

The automated smoke serves only `dist` and launches installed Chrome through a locked Playwright client. It waits for Fable readiness, verifies `0 -> 3 -> 0` and reset state, rejects browser or network errors, and checks for page overflow at 360 pixels. An independent DevTools run also produced 100 for accessibility, best practices, SEO, and agentic browsing.

These checks cover one DOM interaction, the current tool graph, a production build, and the observed Chrome environment. They do not cover React or Elmish compatibility, every browser, routing, HTTP, offline behavior, authentication, localization, hydration, server-side rendering, long-session memory behavior, or production hosting headers.

## Know the supported F# and .NET surface {#compatibility}

Fable's official JavaScript compatibility reference documents support for most of FSharp.Core and selected BCL types. It often maps familiar types to native JavaScript forms or a small Fable runtime. That gives pleasant source code, but target semantics remain observable.

Examples that deserve explicit tests include:

- most numeric types use JavaScript `number`, while `int64`, `uint64`, `bigint`, and `decimal` have different representations;
- small integer arithmetic and array bounds do not automatically reproduce every CLR overflow or bounds behavior;
- dates, regular expressions, reflection, generic information, and object members have documented limits or target-specific behavior;
- `Async.RunSynchronously` is unavailable in the browser target;
- `MailboxProcessor` support is limited and browser JavaScript remains single-threaded unless an explicit worker boundary is introduced;
- options and other F# values may use JavaScript-friendly runtime representations that matter at raw interop boundaries.

Do not memorize this list as permanent. Link the compatibility page in the upgrade record and test the precise operations your domain relies on.

### Share behavior, not wishful assemblies {#shared-code}

A good cross-target candidate is a dependency-light module of records, unions, validation, calculations, and pure state transitions. A poor candidate directly reads files, opens database connections, starts threads, loads reflection-heavy plugins, reads process environment, or depends on an arbitrary server NuGet graph.

Use a boundary like this:

```text
shared pure contracts and decisions
  <- server adapter: ASP.NET Core, database, secrets, clock
  -> browser adapter: DOM, fetch, storage, URL, browser clock
```

Compile the shared project for .NET and through Fable, then run the same reference inputs on both targets when semantic equivalence matters. A clean .NET compile says nothing about the JavaScript target.

Conditional compilation can isolate a genuinely tiny target difference, but repeated `#if FABLE_COMPILER` branches through business logic usually signal a missing port or a falsely shared module.

## Treat JavaScript interoperability as an adapter {#javascript-interop}

Browser applications eventually call Web APIs or JavaScript packages. Fable.Core provides typed imports, globals, dynamic helpers, and emit features. Prefer them in this order:

1. a maintained Fable-compatible package with an explicit native dependency contract;
2. a small local typed binding for the JavaScript surface actually used;
3. a narrow import wrapper around a module you own;
4. `Emit` or `emitJsExpr` only for a small, reviewed escape hatch.

`Emit` inserts JavaScript that the F# compiler cannot validate. Scattering it through view and domain code turns refactoring into string editing. Keep it private, test the generated behavior, and expose an ordinary F# function or interface.

### Track both package graphs {#two-package-graphs}

NuGet resolves F# source packages and bindings; npm or pnpm resolves bundlers and native JavaScript packages. Some Fable packages require matching npm dependencies, such as React and `react-dom`. A successful NuGet restore does not install that JavaScript runtime, and a successful npm install does not verify that the F# binding matches it.

Lock both graphs and record:

- the Fable tool and SDK used to translate;
- direct NuGet packages plus their lock file;
- direct npm packages plus the workspace lock;
- any required pairing between a binding and native JavaScript package;
- allowed lifecycle scripts and generated code policy;
- licenses, advisories, target browsers, and upgrade checks.

Generated JavaScript and bundle output are normally build artifacts. Commit them only when a publishing or audit contract requires it, and then define who regenerates and reviews them.

## Browser APIs are not server APIs {#browser-boundaries}

The browser offers DOM, events, URL/history, fetch, storage, workers, media, and other Web APIs under an origin and permission model. It does not offer your server's filesystem layout, process environment, database connection, ASP.NET Core dependency injection container, or trusted secret store.

### HTTP remains an untrusted contract {#http-contract}

Even if the server is also F#, browser and server values cross bytes. Define DTOs and wire formats explicitly. Validate both sides. Version incompatible changes. Test missing, extra, malformed, null, Unicode, date/time, number, and error payloads rather than assuming a shared record makes JSON identical.

The browser should map transport states into domain-facing cases such as `NotAsked`, `Loading`, `Loaded`, and `Failed`; a raw rejected promise or exception is not a complete user state. Keep correlation and cancellation metadata when responses can race.

Cookies, bearer tokens, CORS, CSRF, redirects, caches, and status codes are transport/security decisions. Elmish does not solve them, and using one language on both ends does not collapse the trust boundary.

### Client code and storage are visible {#client-security}

Never place a secret in F# source, build-time browser configuration, generated JavaScript, source maps, HTML, or browser storage. If the browser can use a credential, the user and injected script can observe or exercise it under the browser's rules.

Prefer `textContent` or renderer text nodes for untrusted content. Raw HTML requires an explicit sanitization and policy boundary. Review dependencies, content security policy, framing, subresource loading, source-map publication, and sensitive diagnostic data as part of deployment.

`localStorage`, `sessionStorage`, IndexedDB, and caches are persistence mechanisms, not confidential vaults or authoritative databases. Define data lifetime, schema migration, quota failure, multi-tab coordination, logout cleanup, and offline conflict behavior before relying on them.

### Accessibility belongs to the rendering boundary {#accessibility}

F# types can make states explicit, but they do not automatically produce semantic HTML, accessible names, focus movement, keyboard operation, announcements, contrast, reduced motion, or responsive layout. The browser sample uses a real heading hierarchy, buttons, a live `output`, a disabled reset state, visible focus, and narrow-screen tests because those are browser contracts.

Test with the accessibility tree and keyboard, not only CSS selectors. A virtual DOM, Feliz DSL, or Elmish loop changes construction mechanics; none exempts the resulting DOM from accessibility requirements.

## Choose state architecture from state complexity {#state-architecture}

Use the smallest explicit state model that keeps behavior understandable.

| Problem | First candidate | Why it may fit | Signal to reconsider |
|---|---|---|---|
| one bounded enhancement with a few elements | plain Fable + Browser.Dom | minimal graph and direct platform semantics | duplicated rendering, tangled transitions, lifecycle leaks |
| multiple coordinated states, effects, and routes | Elmish core + chosen renderer | one message flow and testable transitions | trivial local state becomes ceremony or update becomes a monolith |
| React component ecosystem is a hard requirement | Feliz and/or Fable.Elmish.React plus locked React | typed F# view surface over the required ecosystem | binding/native mismatch, bundle or upgrade cost exceeds benefit |
| mostly server-rendered content with small interactions | server HTML plus isolated Fable islands | preserves simple navigation and payload | islands need shared global state or duplicate large dependencies |

Do not select Elmish because the language is F#, React because the application is “modern,” or direct DOM because the first demo is short. Base the choice on state lifetime, concurrent side effects, component interoperability, rendering frequency, team skills, accessibility, and upgrade responsibility.

## Elmish makes the event loop explicit {#elmish-loop}

Elmish formalizes model-view-update:

```text
init -> Model + Cmd<Msg>
event/effect -> Msg
update Msg Model -> Model + Cmd<Msg>
view Model dispatch -> rendered UI
```

The model is an immutable snapshot. A message names what happened. `update` decides the next state and describes commands. The runtime executes commands, dispatches later messages, and asks the renderer to update the view.

The browser sample already implements the core of this pattern without the library: `Message`, `Model`, and pure `update`. Its hand-written mutable shell performs dispatch and rendering. Elmish earns its dependency when standard commands, subscriptions, composition, instrumentation, or renderer integration replace enough custom lifecycle code.

### Commands describe effects; they do not purify them {#commands}

An HTTP command still performs I/O and can fail, time out, complete late, or be cancelled. The gain is that `update` returns a description and receives the outcome as another message instead of hiding the effect inside a state mutation.

Give every effect explicit outcome cases. For example, `SearchStarted`, `SearchSucceeded`, `SearchFailed`, and `SearchCancelled` let the view distinguish an empty successful result from a connection failure, while one `SetResults` message collapses those meanings.

### Subscriptions manage external event lifecycles {#subscriptions}

Timers, WebSockets, browser observers, and global event sources can emit independently of a command. Elmish subscriptions associate those sources with model-dependent identities and start or stop them as the program changes.

Cleanup is the hard requirement. Each active source should have one current registration; socket replacement disposes the previous connection; obsolete subscriptions stop dispatching. Test start, replacement, disposal, reconnection, and page teardown.

### Reject stale asynchronous outcomes {#stale-results}

Suppose the user searches for `fa`, then quickly for `fable`. The first request may finish last. A useful model carries a request ID or generation:

```fsharp
type RemoteData<'value> =
    | NotAsked
    | Loading of requestId: int * query: string
    | Loaded of requestId: int * query: string * value: 'value
    | Failed of requestId: int * query: string * message: string
```

When a completion message arrives, `update` accepts it only if its ID still matches the active request. Cancellation through `AbortController` can save work, but the identity check remains necessary because cancellation can race, be unsupported, or arrive after completion.

Model debouncing separately from network state. Timer generation, requested query, active request, visible result, and validation message are different facts. Compressing them into `bool isLoading` loses the information needed to resolve races.

## Elmish is not a renderer {#renderers}

Elmish core is UI-independent. A renderer turns a model into DOM or renderer-specific elements and routes user events back through `dispatch`.

### React, Feliz, and Elmish.React {#react-feliz}

Feliz is a typed F# API for React. It can be used with React component-local state and hooks, with an Elmish program, or with `Feliz.UseElmish` at a component boundary. Fable.Elmish.React connects an Elmish program to React/React Native and its package metadata requires separate native React packages for applications.

These tools solve different problems:

- React supplies the JavaScript rendering/component runtime;
- Feliz supplies an F#-friendly typed construction API;
- Elmish supplies model-message-update-command organization;
- Fable.Elmish.React connects an Elmish program to the React renderer.

Adding all four is not automatically more functional. For a small isolated component, Feliz hooks may be enough. For application-wide workflows and coordinated effects, Elmish may help. For one counter, the browser sample's direct DOM shell is easier to audit.

The Fable.React package page recommends Feliz for new React projects because Fable.React is less actively maintained. Treat that as current maintainer guidance, not a reason to rewrite a stable application without migration checks.

### Other renderers and direct bindings {#other-renderers}

Fable can target the DOM directly or use bindings for other JavaScript UI libraries. Evaluate each option from maintained package metadata, native peer dependencies, hydration or routing needs, accessibility output, debugging, bundle cost, and upgrade history. Popularity in either .NET or JavaScript alone does not prove the binding seam is healthy.

Keep renderer values out of domain and application modules. That makes a renderer migration, server-side reuse, and pure testing materially smaller.

## Scale an Elmish model without creating one giant update {#elmish-composition}

Split by cohesive features and clear message responsibility, not by arbitrary “models,” “views,” and “updates” folders. A feature can expose its `Model`, `Msg`, `init`, `update`, and view boundary. A parent wraps child messages and maps child commands back to the parent message type.

The parent coordinates cross-feature decisions. A child should not reach sideways into another feature's mutable store. If two features must coordinate, model a parent message or a shared domain transition rather than constructing a hidden event bus.

Do not put every cached response, text field, modal, route, and transient hover in one global model. Keep state at the narrowest lifetime that needs coordination. Conversely, duplicating authoritative booking state across components creates reconciliation work; choose one source of truth.

### Make impossible UI states difficult to express {#ui-state-modeling}

Prefer a union over unrelated flags:

```fsharp
type BookingPage =
    | Editing of Draft
    | Submitting of Draft * requestId: int
    | Accepted of Confirmation
    | Rejected of Draft * ValidationError list
    | Unavailable of Draft * safeMessage: string
```

This model cannot be both submitting and accepted. It also preserves the draft on a recoverable failure. Real products may need more orthogonal state, but each added field should represent an independent fact rather than repair a contradictory flag set.

Form typing needs restraint. Raw text belongs in editing state because partially typed numbers and dates are not yet domain values. Parse and validate at a deliberate transition; do not force every keystroke into a domain type or postpone all validation until the server responds.

### Let the URL carry navigable state {#routing}

If a state should survive reload, deep links, history navigation, or sharing, decide whether it belongs in the URL. Parse the route into a validated application case and render unknown routes explicitly. Do not maintain unrelated copies in router state, the global model, and component state without a synchronization rule.

Client-side routing requires hosting fallback configuration. The browser sample is an MPA with one entry and deliberately does not verify SPA rewrites. A bundle that works on `/` may still return 404 when a user directly requests `/bookings/42` from static hosting.

## Test the browser application at several levels {#testing}

Use several layers because each catches a different class of mistake.

### Pure transition tests {#pure-tests}

Test `update`, validation, routing parsers, reducers, encoders, and derived view data without a browser where possible. Assert next model and emitted effect descriptions for success, invalid input, repeated input, stale messages, retries, and cancellation messages.

If the same source must run on .NET and JavaScript, test semantic hotspots on both targets. A .NET-only test covers CLR behavior, not the generated JavaScript.

### Binding and component tests {#binding-tests}

Test local bindings against the exact JavaScript package version. Exercise optional members, callback `this`, promise rejection, event cleanup, null/undefined, module format, and production minification. Type declarations can be wrong or lag the runtime.

Component tests should query roles, names, labels, text, and state as users perceive them. CSS class selectors couple tests to rendering details and can miss an inaccessible UI.

### Contract and browser tests {#browser-tests}

Mock transport only for narrow deterministic cases. Keep server contract fixtures for success and every declared error, then run at least one real HTTP slice when a browser client and service must agree on credentials, CORS, serialization, and status mapping.

Production browser smoke must use built assets, not only a development server. Capture console errors, page exceptions, failed and error responses, initial readiness, one meaningful interaction, keyboard/accessibility semantics, and narrow-screen overflow. Add browser/version matrices only when product support requires them.

### Diagnose generated and bundled code {#diagnostics}

Read generated JavaScript when an interop or size question demands it, but debug from F# through source maps when policy permits. Inspect the browser network panel, accessibility tree, event listeners, performance trace, storage, and bundle graph. A successful F# compilation cannot show a missing asset, CSP rejection, stale service worker, hydration mismatch, or inaccessible name.

Do not publish source maps containing sensitive source or paths without an explicit access policy. If production diagnostics upload maps to a service, separate upload from public artifact exposure.

## Keep development and production pipelines distinct {#build-deploy}

The official Fable/Vite workflow can run Fable in watch mode and Vite for fast development. Production must still start from locked clean inputs and create an immutable artifact.

The browser sample's production sequence is conceptually:

```sh
dotnet tool restore
dotnet restore FableSample.fsproj --locked-mode
dotnet fable --outDir generated --noRestore --noCache
vite build
vite preview
```

After starting the preview server, inspect the production bundle in a real browser and automate the check when risk justifies it. A development-server run does not verify the production bundle.

### Deploy the artifact, not the development topology {#static-deployment}

Deploy `dist` to static hosting with correct MIME types, cache rules, compression, security headers, and base path. Hash immutable assets for long caching; keep the HTML entry revalidatable so it can point to the new hashes. Test under the real subpath if the site is not hosted at `/`.

Choose MPA, SPA fallback, or server routing deliberately. Define how old HTML behaves with new assets during rollout, how a service worker updates if one exists, and how to roll back both assets and API compatibility.

No application server is required for the browser sample's artifact. A local preview server is a development tool, not a production dependency or a hosting choice.

### Measure bundle and runtime cost {#browser-performance}

Measure compressed transfer, parsed/executed JavaScript, main-thread work, rendering, memory, network waterfalls, and interaction latency on representative devices. A small source file can import a large native package; a large generated directory can still tree-shake to a small bundle. Inspect the production result rather than judging either input by line count.

Use code splitting for proven route or feature boundaries, not as automatic fragmentation. Loading indicators, chunk failure, cache invalidation, preload, and offline behavior become part of the state model.

## Keep the version table honest {#version-table}

These are dated observations, not a preapproved stack:

| Choice | Stable surface checked on 2026-08-25 | Status in this chapter | Adoption question |
|---|---|---|---|
| Fable tool | 5.13.0; tool targets .NET 10 | illustrated | does the generated JavaScript preserve the semantics this app needs? |
| Fable.Core | 5.2.0; `netstandard2.0` asset | illustrated | is every used helper supported on the JavaScript target? |
| Fable.Browser.Dom | 2.20.0; browser binding graph | illustrated | are the required Web APIs and target browsers covered? |
| Vite | 6.4.3 | illustrated | are base path, assets, production mode, and hosting behavior verified? |
| Fable.Elmish | 5.0.2 | research only | does coordinated state/effect complexity justify the loop? |
| Fable.Elmish.React | 5.6.0 stable; 6.0 beta exists | research only | is the F# binding compatible with the chosen React/npm matrix? |
| Feliz | 3.3.3 | research only | does its typed React surface fit component and upgrade needs? |
| Fable.React | 9.4.0 stable; package recommends Feliz for new work | research only | is this an existing-stack maintenance case rather than a new default? |

“Illustrated” means the chapter includes a minimal configuration or use; it does not mean this book repository contains an executable browser project. “Research only” means the option must be evaluated in the adopting application before use.

## Run a reversible browser-stack spike {#adoption-spike}

Use one representative vertical slice:

- one route or embedded island with real navigation constraints;
- one form containing raw, valid, invalid, and server-rejected states;
- one overlapping asynchronous request that completes out of order;
- one JavaScript package or Web API behind a typed adapter;
- one authenticated HTTP call with declared error mapping;
- one accessible keyboard flow and one narrow-screen layout;
- locked NuGet/npm restore, production bundle, static serve, and Chrome smoke;
- bundle, interaction, memory, diagnostics, CSP, and source-map checks;
- one dependency upgrade and a documented rollback/deletion path.

Compare plain Fable, Elmish, and the required renderer on the same slice. Count concepts, dependencies, lifecycle code, tests, build steps, and operational responsibility—not just view syntax.

Adopt the larger stack only when it removes more risk than it adds. Keep the losing spike small enough to delete, and keep domain code free of renderer types so reversal remains credible.

## Avoid common browser mistakes {#common-mistakes}

- Calling Fable “.NET in the browser” and assuming arbitrary assemblies work.
- Treating `netstandard` compatibility as Fable and browser compatibility.
- Forgetting that npm peer/native packages are separate from NuGet bindings.
- Exposing secrets in generated JavaScript, browser configuration, storage, or source maps.
- Spreading `Emit`, dynamic values, raw DOM casts, or renderer nodes through domain code.
- Putting effects inside `update` and then calling the function pure.
- Modeling load, empty, error, cancelled, and stale states with one Boolean.
- Cancelling a request without also rejecting late completion by identity.
- Registering timers, sockets, or listeners repeatedly without a clear cleanup owner.
- Choosing Elmish for trivial local state or refusing it after custom dispatch code becomes a framework.
- Treating Elmish, React, Feliz, and Fable.Elmish.React as synonyms.
- Sharing a server project that reads files, environment, threads, or databases with the browser target.
- Testing only under the development server or only under .NET.
- Querying DOM tests by CSS implementation details while missing roles, names, focus, and keyboard behavior.
- Publishing a static bundle without checking base paths, direct routes, caching, CSP, MIME types, and rollback.
- Reading generated directory size as bundle size instead of measuring production transfer and execution.
- Claiming a package option is supported because its current version page was reviewed but never built.

## Exercises {#exercises}

### Exercise 1: choose three browser architectures {#exercise-01}

Evaluate these browser surfaces separately:

1. A server-rendered documentation page needs one accessible preference toggle and has no shared application state.
2. A booking client has a multi-step draft, URL navigation, overlapping availability and payment requests, retries, and recoverable failures.
3. A product must integrate five maintained React components owned by the frontend team; most state remains local to each component.

For each surface, choose a first candidate and a reversal condition. Compare plain Fable DOM, Elmish, and Feliz/React; each surface may lead to a different architecture.

### Exercise 2: model stale search results {#exercise-02}

Design `Model`, `Msg`, `update`, and command/subscription responsibilities for a search box with 250 ms debounce, cancellation, results, empty state, safe failure, and out-of-order completion. The user types `fa`, then `fable`; the `fa` request finishes last. Specify which identities are compared, what remains visible during refresh, what is announced accessibly, and which transitions are pure-test cases.

### Exercise 3: audit a shared library and release {#exercise-03}

A team wants to share a server pricing project with a Fable checkout. It currently uses records and decimal arithmetic, but also reads `DateTime.UtcNow`, environment variables, a JSON file, and a reflection-based serializer. Design the target-neutral core, server and browser adapters, DTO/wire boundary, cross-target tests, package locks, browser security review, production build, static-host checks, rollout, and rollback. State which behavior cannot be declared equivalent until measured.

[Read the chapter solutions](../solutions/ch-41-fable-elmish).

## Chapter review {#chapter-review}

- Fable preserves the F# source language while changing the target runtime to JavaScript.
- Separate ordinary F# type checking, Fable compatibility, and target-browser behavior.
- The browser runs bundled JavaScript, not the project DLL or CLR.
- Share dependency-light contracts and decisions; isolate server and browser effects behind adapters.
- Lock and review NuGet and npm graphs independently, including binding/native pairings.
- Keep browser HTTP untrusted and model every meaningful remote state.
- Client code, storage, configuration, and source maps cannot protect secrets from the user or injected script.
- Direct DOM is a valid small choice; Elmish earns its loop through coordinated state, effects, subscriptions, and composition.
- Elmish organizes state transitions; React renders; Feliz supplies a typed React API; Elmish.React connects the loop and renderer.
- Commands expose side-effect responsibility but do not remove failure, cancellation, or race semantics.
- Reject stale completions by identity even when cancellation exists.
- Renderer choice does not automate semantic HTML, focus, keyboard behavior, or responsive layout.
- Test pure logic, bindings, wire contracts, production assets, and real browser behavior at separate layers.
- Static hosting must still handle base paths, route fallback, MIME, caching, security headers, updates, and rollback.
- Version metadata is a dated snapshot; only the browser sample's plain-DOM slice is executed here.

Chapter 42 moves from a static browser artifact to deployed service topology: containers, cloud boundaries, serverless constraints, and .NET Aspire orchestration.
