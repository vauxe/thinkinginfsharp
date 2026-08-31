---
title: "Chapter 41: Fable, Elmish, and Browser Applications"
description: "Choose a browser architecture from runtime constraints, state complexity, rendering needs, interoperability, and deployment verification."
translationKey: part-07/ch-41-fable-elmish
---

# Chapter 41: Fable, Elmish, and Browser Applications {#overview}

F# remains F# in a Fable project: records, discriminated unions, pattern matching, modules, functions, and type inference still shape the program. The runtime does not remain .NET. For the JavaScript target, Fable translates supported F# and library constructs into JavaScript, then the browser executes that JavaScript with browser security, scheduling, numeric, packaging, and API rules.

That distinction prevents two opposite mistakes. A .NET team must not assume every BCL or NuGet API works in a browser. A JavaScript team must not treat Fable as an isolated platform that replaces npm, bundlers, DOM knowledge, accessibility, or browser diagnostics. The useful question is: “Which logic benefits from F# modeling, which boundary belongs to JavaScript or the browser, and how much state architecture does this interface actually need?”

Keep the vocabularies separate: records, discriminated unions, pattern matching, modules, and type inference are F# language concepts; Fable is a transpiler and toolchain; Elmish, Feliz, and React are libraries or UI architectures; DOM, Web APIs, Vite, and npm belong to the browser and JavaScript ecosystem. The latter terms are not “standard F# syntax.”

## Fable changes the target, not the source language {#target-runtime}

A browser Fable pipeline has four distinct products:

```text
F# source + .fsproj + compatible NuGet packages
  -> Fable-generated JavaScript modules
  -> JavaScript bundler production assets
  -> browser execution under Web APIs and browser policy
```

The F# compiler still type-checks the project. Fable then translates the supported program. Vite or another JavaScript tool resolves modules, tree-shakes, minifies, hashes, and emits assets. The browser loads those assets; it does not load the project's `.dll` or start the CLR.

This is transpilation to a target ecosystem, not remote control of a .NET process and not the same mechanism as .NET WebAssembly. Fable can also target other languages, but this chapter uses only the JavaScript browser target to explain the boundary.

### Keep three compatibility questions separate {#three-compatibility-questions}

For every dependency or API, ask:

1. Does ordinary F# type-check this source?
2. Does Fable support or translate the used language/library surface for JavaScript?
3. Does the emitted JavaScript work in the target browsers and bundler configuration?

A `netstandard2.0` asset answers neither the second nor the third question by itself. Conversely, a JavaScript library can work well through a typed Fable binding even though it has no .NET implementation.

## In-page project template: one minimal browser boundary {#verified-slice}

The current repository no longer contains the former `examples/ecosystem/fable` project. This section preserves a reconstructable template, not a currently executable repository sample. It intentionally avoids React and Elmish and shows only the smallest path: F# becomes JavaScript, the page loads the generated module, and DOM events update visible state.

To reconstruct it, place `FableSample.fsproj`, `App.fs`, `index.html`, `package.json`, and `vite.config.mjs` in one directory; pin the Fable tool in `.config/dotnet-tools.json`, and pin the two dependency graphs with NuGet and JavaScript lock files. `App.fs` is the project's only F# compile item, `index.html` is the Vite entry, and `generated/` and `dist/` are generated directories.

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
To reproduce this template in an application, record these independently moving inputs:

- the .NET SDK and F# language versions;
- the local Fable tool version;
- Fable.Core and Fable.Browser.Dom in `packages.lock.json`;
- the JavaScript package manager and Vite versions in the JavaScript lock file;
- the browser versions used by automated and manual acceptance tests.

The compiler, Fable.Core, and browser bindings have separate release cadences. Matching their major or minor numbers by appearance is not a compatibility strategy; restore their declared graph, compile it, and run the target.

### Connect the HTML prerequisite to the F# entry first {#sample-code}

`App.fs` looks up three fixed IDs, so the HTML must provide those elements before the module runs. This excerpt omits only page styling and explanatory copy; its script path matches Fable's output directory:

```html:line-numbers [index.html]
<main>
  <output id="count" aria-live="polite">Count: 0</output>
  <button id="increment" type="button">Increment count</button>
  <button id="reset" type="button">Reset count</button>
</main>
<script type="module" src="/generated/App.js"></script>
```

The next block is the complete `App.fs`, not a set of unrelated snippets:

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

### What to check after reconstruction {#sample-evidence}

After reconstruction, the production sequence should perform locked .NET tool/package restore, Fable compilation without cache reuse, and a Vite production build. Check that `dist` contains an HTML entry and hashed JavaScript asset, but do not turn module count, file name, or size into a permanent contract.

A browser smoke should serve only `dist`, wait for `data-fable-ready`, verify `0 -> 3 -> 0` and reset-button state, and fail on browser errors, failed network requests, or overflow at 360 pixels. Then use the keyboard and accessibility tree to inspect button names, focus, and the live region. This repository no longer has that project or test, so the chapter does not claim these checks have run.

Even if those checks pass, they cover only one DOM interaction, the locked tool graph, a production build, and the tested browser environment. They do not cover React or Elmish compatibility, every browser, routing, HTTP, offline behavior, authentication, localization, hydration, server-side rendering, long-session memory behavior, or production hosting headers.

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

F# types can make states explicit, but they do not automatically produce semantic HTML, accessible names, focus movement, keyboard operation, announcements, contrast, reduced motion, or responsive layout. When reconstructing the template, add a real heading hierarchy, buttons, a live `output`, a disabled reset state, visible focus, and narrow-screen tests because those are browser contracts.

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

The in-page template already writes the core of this pattern without the Elmish library: `Message`, `Model`, and pure `update`. Its hand-written mutable shell performs dispatch and rendering. Elmish earns its dependency when standard commands, subscriptions, composition, instrumentation, or renderer integration replace enough custom lifecycle code.

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

Adding all four is not automatically more functional. For a small isolated component, Feliz hooks may be enough. For application-wide workflows and coordinated effects, Elmish may help. For one counter, the in-page template's direct DOM shell is easier to audit.

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

Client-side routing requires hosting fallback configuration. The in-page template is designed as an MPA with one entry and does not define SPA rewrites. A bundle that works on `/` may still return 404 when a user directly requests `/bookings/42` from static hosting.

## Test the browser application at several levels {#testing}

Use several layers because each catches a different class of mistake.

### Pure transition tests {#pure-tests}

Test `update`, validation, routing, reducers, encoding, and derived view data without a browser. Assert next state and effects for invalid, repeated, stale, retried, and cancelled work. If source runs on .NET and JavaScript, test semantic hotspots on both targets; CLR results do not prove generated JavaScript behavior.

### Binding and component tests {#binding-tests}

Test bindings against the exact JavaScript package, including optional members, callback `this`, promise rejection, cleanup, null/undefined, module format, and minification; type declarations can lag runtime. Query components by user-visible roles, names, labels, text, and state rather than CSS details.

### Contract and browser tests {#browser-tests}

Keep server fixtures for success and declared errors, and run a real HTTP slice for credentials, CORS, serialization, and status mapping. Production smoke must use built assets and cover console/page failures, readiness, one meaningful interaction, keyboard semantics, and narrow-screen overflow. Add browser matrices only when support policy requires them.

### Diagnose generated and bundled code {#diagnostics}

Use source maps and browser diagnostics for interop, bundle, network, accessibility, event, storage, and performance failures that compilation cannot reveal. Keep sensitive source maps private; separate diagnostic upload from public artifacts.

## Keep development and production pipelines distinct {#build-deploy}

The official Fable/Vite workflow can run Fable in watch mode and Vite for fast development. Production must still start from locked clean inputs and create an immutable artifact.

The in-page template's production sequence is conceptually:

```sh
dotnet tool restore
dotnet restore path/to/FableSample.fsproj --locked-mode
dotnet fable --outDir generated --noRestore --noCache
vite build
vite preview
```

After starting the preview server, inspect the production bundle in a real browser and automate the check when risk justifies it. A development-server run does not verify the production bundle.

### Deploy the artifact, not the development topology {#static-deployment}

Deploy `dist` to static hosting with correct MIME types, cache rules, compression, security headers, and base path. Hash immutable assets for long caching; keep the HTML entry revalidatable so it can point to the new hashes. Test under the real subpath if the site is not hosted at `/`.

Choose MPA, SPA fallback, or server routing deliberately. Define how old HTML behaves with new assets during rollout, how a service worker updates if one exists, and how to roll back both assets and API compatibility.

No application server is required for this template's static artifact. A local preview server is a development tool, not a production dependency or a hosting choice.

### Measure bundle and runtime cost {#browser-performance}

Measure compressed transfer, parsed/executed JavaScript, main-thread work, rendering, memory, network waterfalls, and interaction latency on representative devices. A small source file can import a large native package; a large generated directory can still tree-shake to a small bundle. Inspect the production result rather than judging either input by line count.

Use code splitting for proven route or feature boundaries, not as automatic fragmentation. Loading indicators, chunk failure, cache invalidation, preload, and offline behavior become part of the state model.

## Keep the version table honest {#version-table}

These are dated observations, not a preapproved stack:

| Choice | Stable surface checked on 2026-08-31 | Status in this chapter | Adoption question |
|---|---|---|---|
| Fable tool | 5.13.0; tool targets .NET 10 | in-page project template | does the generated JavaScript preserve the semantics this app needs? |
| Fable.Core | 5.2.0; `netstandard2.0` asset | in-page project template | is every used helper supported on the JavaScript target? |
| Fable.Browser.Dom | 2.20.0; browser binding graph | in-page project template | are the required Web APIs and target browsers covered? |
| Vite | template pins 6.4.3 | in-page project template | are base path, assets, production mode, and hosting behavior verified? |
| Fable.Elmish | 5.0.2 | research only | does coordinated state/effect complexity justify the loop? |
| Fable.Elmish.React | 5.6.0 stable; 6.0 beta exists | research only | is the F# binding compatible with the chosen React/npm matrix? |
| Feliz | 3.3.3 | research only | does its typed React surface fit component and upgrade needs? |
| Fable.React | 9.4.0 stable; package recommends Feliz for new work | research only | is this an existing-stack maintenance case rather than a new default? |

“In-page project template” means the text supplies the key files and context needed to reconstruct the example; it does not mean this book repository contains an executable browser project. “Research only” means the option must be evaluated in the adopting application before use.

## Run a reversible browser-stack spike {#adoption-spike}

Use one representative vertical slice covering:

- real navigation and a form with raw, valid, invalid, and rejected states;
- an out-of-order asynchronous request and a typed JavaScript/Web API adapter;
- authenticated HTTP with declared errors, keyboard access, and narrow layout;
- locked restore, production bundle, static serve, and browser smoke;
- bundle/runtime diagnostics, one dependency upgrade, and a rollback/deletion path.

Compare plain Fable, Elmish, and the required renderer on the same slice. Count concepts, dependencies, lifecycle code, tests, build steps, and operational responsibility—not just view syntax.

Adopt the larger stack only when it removes more risk than it adds. Keep the losing spike small enough to delete, and keep domain code free of renderer types so reversal remains credible.

## Exercises {#exercises}

### Exercise 1: choose three browser architectures {#exercise-01}

Evaluate these browser surfaces separately:

1. A server-rendered documentation page needs one accessible preference toggle and has no shared application state.
2. A booking client has a multi-step draft, URL navigation, overlapping availability and payment requests, retries, and recoverable failures.
3. A product must integrate five maintained React components owned by the frontend team; most state remains local to each component.

For each surface, choose a first candidate and a reversal condition. Compare plain Fable DOM, Elmish, and Feliz/React; each surface may lead to a different architecture.


::: details Answer

#### Case A: one preference toggle on server-rendered documentation {#exercise-01-case-a}

Start with a tiny isolated enhancement. If native HTML and CSS can represent the preference, use them. If it needs stored state and event handling, and the application already uses a Fable build pipeline, plain Fable plus Browser.Dom is the first F# candidate.

Use one model and one typed boundary:

```fsharp
type Preference =
    | FollowSystem
    | Light
    | Dark

type Message =
    | PreferenceSelected of Preference
    | SystemPreferenceChanged of prefersDark: bool
```

The pure transition decides the effective preference. One adapter reads and validates storage, another applies a document attribute, and a third manages the media-query listener and its cleanup. Storage failure falls back without making the control unusable. The toggle is a real button or radio group with an accessible name and visible focus.

Do not add Elmish or React yet. They add a loop, renderer, native npm packages, upgrade pairings, and lifecycle concepts without removing meaningful risk from one island.

Reconsider this choice when multiple interactive islands must coordinate, listener cleanup is duplicated, the URL becomes the source of state, or asynchronous work makes local dispatch inconsistent. Fable itself can also lose. If the page has no Fable toolchain and a few reviewed JavaScript lines cost much less to maintain, F# need not become a browser dependency.

The acceptance slice covers system/light/dark initialization, invalid stored input, storage denial, keyboard operation, system preference changes, cleanup, a 320-pixel layout, production bundling, and no console or HTTP failures.

#### Case B: a multi-step booking client with coordinated effects {#exercise-01-case-b}

Start with Fable.Elmish core and keep the renderer provisional. The flow has persistent coordinated state, URL state, several fallible external operations, retries, and races. Under those conditions, a message loop can replace custom lifecycle machinery.

Model meaningful states instead of a bag of flags:

```fsharp
type BookingFlow =
    | Editing of Draft
    | CheckingAvailability of Draft * requestId: int
    | AwaitingConfirmation of PricedDraft
    | Paying of PricedDraft * attemptId: int
    | Completed of Confirmation
    | RecoverableFailure of Draft * SafeError
    | OutcomeUnknown of PricedDraft * reconciliationId: string
```

An availability result must match the active request ID. A payment timeout is not automatically safe to retry: the server contract from Chapters 36–38 must distinguish a known failure from an unknown outcome and support idempotent reconciliation. The server, not the browser, determines the final price, capacity, and booking result.

Commands cover availability, submission, payment or reconciliation, and route updates. Every command returns a success, declared failure, cancellation, or transport-unavailable message. Reserve subscriptions for external streams such as network state or a server event channel; give each one an identity and cleanup path.

Start with the smallest renderer that meets the actual UI. Choose Feliz and React only when required components, rendering ergonomics, or existing React code justify the added NuGet and npm dependencies. Elmish and direct DOM can coexist behind a narrow rendering adapter; the state model should not expose renderer nodes.

Reject Elmish if a representative flow shows that most state is isolated and the loop adds mappings without simplifying external operations. Change renderers if manual DOM updates are inaccessible or error-prone, maintained React components are required, or measurement shows rendering pressure. Binding lag, upgrade conflicts, or bundle cost can instead rule out React.

Acceptance covers navigation through deep links, reload, and Back. It also covers invalid drafts, out-of-order availability results, duplicate submission, known payment failure versus unknown outcome, cancellation, retry, focus after errors, and screen-reader announcements. The locked production output must pass a real service contract test.

#### Case C: five required React components with mostly local state {#exercise-01-case-c}

Start with Feliz plus locked `react` and `react-dom`, and wrap each required component through a small typed binding. Use component-local hooks for genuinely local state. Do not introduce one application-wide Elmish model merely because the client is written in F#.

For each component, inventory:

- exact native package and peer versions;
- the F# binding version or the locally maintained binding API;
- required props, events, refs, promises, null/undefined, and cleanup;
- generated module format and production bundler behavior;
- semantic DOM, focus, keyboard, localization, and error behavior;
- licenses, advisories, source maps, bundle cost, and who handles upgrades.

Introduce `Feliz.UseElmish` or a child Elmish program only where a component contains a real workflow. Add a root Elmish program only when cross-component transitions, external operations, or routing need one coordinator. Fable.Elmish.React connects an Elmish program to React; it does not replace React itself.

Reject this choice if the binding and native-package matrix fails, accessibility output is unacceptable, errors appear only in production, bundle or runtime cost is too high, or platform HTML can replace the components. A successful storybook-like demo is insufficient without the production Fable/Vite pipeline and target browsers.

:::

### Exercise 2: model stale search results {#exercise-02}

Design `Model`, `Msg`, `update`, and command/subscription responsibilities for a search box with 250 ms debounce, cancellation, results, empty state, safe failure, and out-of-order completion. The user types `fa`, then `fable`; the `fa` request finishes last. Specify which identities are compared, what remains visible during refresh, what is announced accessibly, and which transitions are pure-test cases.


::: details Answer

#### Separate input, pending work, and visible results {#exercise-02-model}

Use monotonically increasing generations for debounce timers and request IDs for network work:

```fsharp
type Pending =
    | NoPending
    | Debouncing of generation: int * query: string
    | Requesting of requestId: int * query: string

type VisibleResults =
    | NeverSearched
    | Showing of query: string * SearchItem list
    | Empty of query: string
    | Failed of query: string * safeMessage: string

type Model =
    { Input: string
      Pending: Pending
      Visible: VisibleResults
      NextGeneration: int
      NextRequestId: int
      Announcement: string option }
```

Keeping `Visible` separate lets the previous successful result remain on screen during a refresh. The view can show a non-blocking “Updating results” status instead of replacing useful content with a spinner.

Messages carry the identities needed to reject stale work:

```fsharp
type Msg =
    | InputChanged of string
    | DebounceElapsed of generation: int * query: string
    | SearchSucceeded of requestId: int * query: string * SearchItem list
    | SearchFailed of requestId: int * query: string * safeMessage: string
    | SearchCancelled of requestId: int
```

#### Keep update deterministic {#exercise-02-update}

`InputChanged text` trims only according to the declared search contract, allocates a new debounce generation, clears any old announcement, and returns commands to cancel the active request/timer and start a 250 ms timer. Blank input moves to `NoPending` and `NeverSearched` without starting HTTP.

`DebounceElapsed(generation, query)` starts a request only if `Pending` is exactly `Debouncing(generation, query)` and the current input still denotes that query. It allocates `requestId`, moves to `Requesting(requestId, query)`, preserves `Visible`, and returns a start-search command.

Success, failure, and cancellation are accepted only when both request ID and query match the active `Requesting` case. Any completion for `fa` after `fable` has become active returns the unchanged model and no command. Logging an aggregate stale-completion counter is an adapter concern, not a model transition that exposes user input.

Successful nonempty results become `Showing`; zero results become `Empty`; declared failures become `Failed` with a safe message. Cancellation caused by newer input does not overwrite visible content or announce an error. A real user-requested stop may use a distinct message and notice.

#### Manage timers and AbortController outside the model {#exercise-02-effects}

A command adapter manages timer handles and `AbortController` instances keyed by generation or request ID. Starting newer work aborts and removes older handles. Completion and disposal also remove them. The model stores serializable IDs and state, not browser controller objects.

Abort is an optimization and lifecycle action, not the correctness rule. The identity guard still rejects a response that won the race with abort or came from a transport that cannot cancel.

A subscription can manage the debounce timer when the project consistently uses model-keyed Elmish subscriptions. Its ID includes the active generation, so changing or clearing it stops the previous subscription. Do not run both command-timer and subscription-timer designs simultaneously.

#### Test the transitions and accessible output {#exercise-02-tests}

Pure tests cover:

- blank input starts nothing;
- `fa` allocates generation 1, then `fable` allocates generation 2 and cancels generation 1;
- a generation-1 tick is ignored, while generation 2 starts request 1;
- a later edit starts request 2 and request-1 success/failure/cancellation is ignored;
- request-2 nonempty, empty, and safe failure results create distinct visible states;
- retry allocates a new request ID rather than reusing an ambiguous completion identity;
- accepted completion clears pending work and produces one bounded announcement;
- ignored stale work does not change content, focus, or announcement.

The browser test types both queries under controlled response gates, completes `fable` first and `fa` last, and verifies that only `fable` remains. It also checks keyboard input, focus retention, a polite live region that does not announce every keystroke, safe errors, cancellation cleanup, unmounting, console or network failures, and the production bundle.

:::

### Exercise 3: audit a shared library and release {#exercise-03}

A team wants to share a server pricing project with a Fable checkout. It currently uses records and decimal arithmetic, but also reads `DateTime.UtcNow`, environment variables, a JSON file, and a reflection-based serializer. Design the target-neutral core, server and browser adapters, DTO/wire boundary, cross-target tests, package locks, browser security review, production build, static-host checks, rollout, and rollback. State which behavior cannot be declared equivalent until measured.


::: details Answer

#### Extract a target-neutral pricing core {#exercise-03-core}

Create a small project containing only pricing contracts and deterministic decisions:

```fsharp
type Money =
    private
    | Money of currency: Currency * amount: decimal

type PricingInput =
    { At: DateTimeOffset
      Basket: Basket
      Rules: PricingRules }

val price: PricingInput -> Result<PricedBasket, PricingError list>
```

Pass time and rules as data. Keep currency explicit and centralize scale/rounding rules. The core does not read a clock, environment, file, browser storage, or serializer. It does not decide how configuration arrived.

The server remains authoritative. The browser may calculate a preview for responsiveness, but checkout sends a versioned request and displays the server's priced result. A client bundle can be modified, run with stale rules, or have different numeric behavior.

#### Put runtime I/O in separate adapters {#exercise-03-adapters}

The server adapter reads approved configuration, obtains a clock instant, loads/validates the JSON rule document at startup, protects secrets, and maps HTTP DTOs through an explicit serializer. It exposes validated `PricingRules` and `DateTimeOffset` to the core.

The browser adapter receives nonsecret public configuration through versioned HTTP or a build artifact. It maps Web API time only when the product contract permits client time, and uses a defined JSON codec compatible with the server wire schema. It never assumes that the reflection-based server serializer will work in the browser.

If the browser must evaluate “now,” define whether the rule uses server time, client wall time, or a monotonic duration. For expiry or authoritative pricing, use a server-issued instant and version. Client clocks are user-controlled and can drift.

The old reflection serializer remains behind the server adapter until contract tests justify replacement. The browser codec must reject malformed, missing, extra, null, number, date, currency, and version cases according to one written compatibility policy.

#### Verify semantics on both targets {#exercise-03-cross-target}

Run the same golden vectors through .NET and Fable-generated JavaScript. Include every currency scale, midpoint rounding, negative/zero/high amounts, quantity boundaries, discounts, tax order, date cutoffs, Unicode identifiers, and invalid combinations. Compare declared money values, errors, and rule versions—not internal object representations.

Add generated boundary/property cases where the two test frameworks can share deterministic seeds or serialized fixtures. A mismatch blocks client preview release; it must not be rounded away without a domain decision.

Decimal use is not proof of identical runtime behavior. Fable documents target-specific numeric representations. Until cross-target tests and representative browser measurements pass, claim only that both targets compile. Even after they pass, the server recalculates authoritative price.

Compile-time success also cannot prove reflection, time-zone, serializer, or filesystem equivalence; those features were deliberately removed from the shared core rather than emulated.

#### Lock, build, secure, and release {#exercise-03-release}

The release pipeline performs:

1. locked SDK, tool, NuGet, and JavaScript dependency restore;
2. .NET unit/property tests and server serializer contracts;
3. Fable compilation plus JavaScript execution of shared golden vectors;
4. Vite production build and bundle/license/advisory review;
5. real server/browser HTTP contract and checkout smoke;
6. accessibility, CSP, storage, cache, source-map, and secret scans;
7. static-host tests for MIME, base path, compression, caching, direct routes, and error documents;
8. artifact digest, rule/API compatibility, rollout, monitoring, and rollback recording.

Deploy server support for the new DTO/rule version before publishing a client that needs it. Keep old server compatibility through the browser-cache and rollback window. Static assets use immutable hashes; the HTML entry revalidates and can be rolled back to a prior compatible asset set.

Observe bounded counts for rule versions, client/server price disagreement, rejected DTO versions, checkout errors, and rollback selection without logging baskets, tokens, or personal data. A disagreement prevents submission or shows the authoritative server price; it never silently charges the client preview.

Delete the original mixed project only after all of these conditions hold:

- every server caller uses the server adapter;
- the Fable project compiles only the neutral core;
- golden tests run on both targets;
- production no longer loads file or environment paths from core code;
- cached clients remain compatible; and
- rollback no longer requires the old layout.

:::


## Sources {#sources}

- [Fable: supported targets and stability levels](https://fable.io/docs/index.html)
- [Fable: create a project and install the tool](https://fable.io/docs/getting-started/your-first-fable-project.html)
- [Fable: development and production builds with Vite](https://fable.io/docs/javascript/build-and-run.html)
- [Fable CLI options and target behavior](https://fable.io/docs/getting-started/cli.html)
- [NuGet: Fable tool versions](https://www.nuget.org/packages/Fable)
- [Vite: production build guide](https://vite.dev/guide/build)

Chapter 42 moves from a static browser artifact to deployed service topology: containers, cloud boundaries, serverless constraints, and .NET Aspire orchestration.
