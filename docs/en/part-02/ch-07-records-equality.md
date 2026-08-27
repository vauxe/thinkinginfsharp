---
title: "Chapter 7: Records, Updates, Equality, and Comparison"
description: "Move from positional tuples to named records, use anonymous records and immutable updates, and distinguish structural equality, comparison, reference identity, and hash codes."
translationKey: part-02/ch-07-records-equality
---

# Chapter 7: Records, Updates, Equality, and Comparison {#overview}

A tuple combines values, but field meaning exists only in position and surrounding context. To the compiler, `("A-1", "Lin", 2)` is merely `string * string * int`; it does not know that the three positions mean event, attendee, and seat count. As data crosses more functions, position quickly becomes a fragile protocol.

A record turns that convention into a type. Fields gain names, construction must be complete, and functions can reuse the same definition. F# records are immutable by default. They also receive structural equality, comparison, and matching hash behavior when their fields support those operations. Equal contents, reference identity, and equal hash codes are nevertheless three different claims; the examples below distinguish them.

## All three forms are product types {#product-types}

Tuples, records, and anonymous records are all product types: one value contains several components at once. They differ in naming, reuse scope, and type identity—not in how “functional” they are.

### Tuples suit short-range positional protocols {#tuples}

A tuple type puts positions into the type:

```fsharp
let request = "Lin", 2
let attendee, seats = request
```

The type is `string * int`, and the second line deconstructs by position. This is useful for temporary pairs inside a function, mathematical coordinates, or a local transformation visible at a glance. Swapping positions produces a different type. Two pairs with different meanings but the same component types still share one static type.

When call sites accumulate `fst` and `snd`, or readers must remember what a fourth position means, a tuple no longer communicates the domain clearly. Do not maintain a cross-module positional protocol with comments.

### Named records establish reusable types {#records}

The shared script defines a booking draft:

```fsharp:line-numbers [ch07-records-equality.fsx]
type BookingDraft =
    { EventId: string
      Attendee: string
      Seats: int }

let original =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }
```
`BookingDraft` is a named type. Field labels participate in construction and access, so field order is no longer the caller's only clue to meaning. An ordinary record is a .NET reference type by default, while its fields are not assignable by default. “Reference type” and “mutable object” are not synonyms.

Two separately declared record types remain distinct nominal types even when every field name and type matches. The name creates a compile-time distinction: `BookingDraft` does not become another record type merely because its fields look similar.

### Disambiguate the record type at construction {#construction}

A record expression must supply every field. The compiler can infer the record type when labels are distinctive enough. When several types reuse the same labels, add a type annotation or qualify the first label:

```fsharp
let draft: BookingDraft =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let another =
    { BookingDraft.EventId = "A-2"
      Attendee = "Ada"
      Seats = 1 }
```

The annotation does not repeat every field type; it tells the compiler which record the labels belong to. Do not rely on the most recently declared type with matching labels, because unrelated declaration order would then affect inference.

A record pattern deconstructs by name:

```fsharp
let label { EventId = eventId; Attendee = attendee; Seats = seats } =
    $"{eventId}:{attendee}:{seats}"
```

Construction and patterns use the same field vocabulary: one builds a value, and one extracts its values.

## Copy-and-update expresses immutable change {#update}

The shared example changes seat count from `2` to `3`:

```fsharp:line-numbers [ch07-records-equality.fsx]
let updated = { original with Seats = 3 }

printfn "Record update: original=%d updated=%d" original.Seats updated.Seats
```
`{ original with Seats = 3 }` produces a new `BookingDraft`. `original.Seats` remains `2`, while `updated.Seats` is `3`. It states that new state derives from old state with only these fields changed, without repeating the others.

Copy-and-update performs a shallow structural update: it creates a new outer record and retains every unchanged field value. A reference-valued field can therefore point to the same object from both records. Immutable domain models commonly use immutable nested values as well, making that sharing safe by construction.

Starting with F# 7, a field path can update nested records. The shorter syntax does not change the shallow-update semantics. First make the nested model clear; then decide whether several `with` expressions are worth compressing.

## Anonymous records name fields without declaring a type {#anonymous-records}

An anonymous record uses `{| ... |}` and needs no prior type declaration. The shared script projects from a named record and adds a computed field:

```fsharp:line-numbers [ch07-records-equality.fsx]
let summary =
    {| updated with
        IsGroup = updated.Seats > 1 |}

printfn "Anonymous summary: %s -> %d seats, group=%b" summary.Attendee summary.Seats summary.IsGroup
```
Every field label and type helps determine the type of `summary`, as does the choice between a reference and `struct` anonymous record. Two anonymous records have the same type only when all of those details match. “Contains at least these fields” is not a structural subtype relation here.

Anonymous records support field access, structural equality and comparison, and copy-and-update, including adding fields during an update. They currently do not support record pattern matching, so dot access normally reads their fields.

They suit local projections, query results, and short-range adaptation. Prefer a named record when the data has a domain name, appears in a public API, centralizes invariants, or is reused widely. An anonymous record should not be an escape from naming a real domain type.

## Structural equality compares contents {#equality}

Two separately constructed `BookingDraft` values make F# `=` return `true` when corresponding fields are structurally equal:

```fsharp:line-numbers [ch07-records-equality.fsx]
let equalCopy =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let alias = original
let structurallyEqual = original = equalCopy
let physicallyEqual = LanguagePrimitives.PhysicalEquality original equalCopy
let aliasIsSameReference = LanguagePrimitives.PhysicalEquality original alias
let equalHashesAgree = hash original = hash equalCopy

printfn "Equality: structural=%b physical=%b alias=%b" structurallyEqual physicallyEqual aliasIsSameReference
printfn "Hashes agree for equal records: %b" equalHashesAgree
```
Generated record equality recursively uses each field type's equality semantics. The capability composes: if a component type does not satisfy F# equality, the containing record cannot unconditionally provide normal structural equality either. Chapter 11 will express this rule as a generic constraint.

Structural equality answers whether contents are equal under the type's rules. It does not answer whether two variables represent the same memory object or whether two business entities have the same identity. Two equal booking drafts can have matching content while real bookings still require distinct request identifiers.

## Reference identity checks the same object {#identity}

For reference types, `LanguagePrimitives.PhysicalEquality` checks physical or reference equality. In the example:

- `original = equalCopy` is `true` because fields match;
- `PhysicalEquality original equalCopy` is `false` because they were constructed separately;
- `PhysicalEquality original alias` is `true` because `alias` points to the original object.

Although an ordinary record is a reference type by default, domain logic normally uses structural equality. Identity matters more often in caches, object graphs, interoperation, or low-level code that explicitly depends on shared instances. Do not substitute physical equality for a business identifier, and do not infer object sharing from structural equality.

`PhysicalEquality` requires a reference type. Reference identity does not have the same meaning for a `struct` record or another value type.

## Hash codes locate candidates {#hash}

A record's generated structural equality comes with structural hashing. The example asserts only one required direction: two structurally equal records produce the same `hash` result.

Hashing provides a one-way guarantee: equal values share a hash code, but different values may collide. A hash collection uses the code to narrow candidates and equality to confirm a match. Ordinary records generate both operations consistently.

Treat a hash code as a temporary lookup hint within the current runtime. Durable identity needs an explicit database key or request ID, and persisted or transmitted values need a documented stable format. Security-sensitive digests require a dedicated cryptographic algorithm.

## Structural comparison supplies a default order {#comparison}

When every field supports comparison, a record automatically supports structural comparison and can be passed directly to `List.sort`:

```fsharp:line-numbers [ch07-records-equality.fsx]
let drafts =
    [ { EventId = "B-2"
        Attendee = "Lin"
        Seats = 2 }
      { EventId = "A-1"
        Attendee = "Lin"
        Seats = 1 }
      { EventId = "A-1"
        Attendee = "Ada"
        Seats = 2 } ]

let sortedLabels =
    drafts
    |> List.sort
    |> List.map (fun draft -> $"{draft.EventId}:{draft.Attendee}:{draft.Seats}")

printfn "Structural sort: %A" sortedLabels
```
This example compares in record declaration order: first `EventId`, then `Attendee` when event IDs tie, and finally `Seats`. Therefore `A-1:Ada:2` appears before `A-1:Lin:1`.

Default order fits values that need determinism and whose structural order matches intent. Business order often differs—for example, seat count descending and then attendee name. Use `List.sortBy` or `List.sortWith` so that rule appears in code. Adding or reordering a record field should not silently alter an important business rule.

Equality and comparison constraints are not identical either. A type may support equality without having a meaningful total order. Chapter 11 states both constraints precisely, and Chapter 14 explains what ordered `Map` and `Set` versus hash collections require.

## Choose a representation {#choosing-shape}

| Situation | Usually consider first | Reason |
| --- | --- | --- |
| Temporary paired result inside one function | Tuple | Positional meaning is local and obvious; deconstruction is concise |
| Domain data passed repeatedly | Named record | Fields have names, the type has identity, and it can evolve centrally |
| Local projection or short-range adaptation | Anonymous record | Avoids an extra declaration while retaining field labels |
| Several mutually exclusive states | A discriminated union in the next chapter | One fixed field set cannot say “exactly one of these” |

Do not optimize only for line count. Record and field names are model vocabulary. Anonymous records and tuples avoid inventing public types for data that remains local.

## Run the shared example {#run-example}

From the directory containing the example, run:

```console
dotnet fsi --exec ch07-records-equality.fsx
```

You should see:

```text
Record update: original=2 updated=3
Anonymous summary: Lin -> 3 seats, group=true
Equality: structural=true physical=false alias=true
Hashes agree for equal records: true
Structural sort: ["A-1:Ada:2"; "A-1:Lin:1"; "B-2:Lin:2"]
```

The five lines demonstrate immutable update, anonymous projection, content versus identity, the equality-to-hash guarantee, and structural sorting. Compare them in order. The script omits the actual hash integer because its value is not stable across environments.

## State what you are comparing {#debugging}

When “the same data” behaves differently, first identify the layer of the question:

1. are the types equal, or are these different named records with similar fields?
2. are you comparing field contents, reference identity, or a domain ID?
3. do all component fields support the equality or comparison you need?
4. did sorting accidentally adopt default record field order?
5. is a hash code being misused as proof of equality or as a permanent key?

If the old value appears to change after an update, the record probably contains a reference to some mutable object. Copy-and-update did not mutate the old record; draw the nested object shared by old and new records instead of vaguely concluding that immutability failed.

When construction infers the wrong record type, check whether several records reuse labels. Put one informative type annotation on the binding or parameter rather than gambling on declaration order.

## Exercises {#exercises}

### Exercise 1: migrate a tuple to a record {#exercise-01}

Replace `("A-1", "Lin", 2)` and a formatting function accepting `string * string * int` with `BookingDraft`. Write the type definition, construction, field access, and record-pattern versions. Explain which positional mistakes disappear, and which domain rules are still not guaranteed automatically.

### Exercise 2: trace copies and identity {#exercise-02}

Create a record from `original` that changes only `Seats`, then separately construct another record with exactly the original fields. Predict and verify three structural equality and `PhysicalEquality` results. If a record contained a mutable list or array field, explain what copy-and-update could share; do not add mutable record fields for this exercise.

### Exercise 3: design equality, hashing, and order {#exercise-03}

Choose structural equality, reference identity, a domain ID, or an explicit ordering key for each need: remove content-equal drafts, confirm whether two variables denote one cache object, and display bookings by descending seat count. Explain why `hash x = hash y` cannot decide equality for the first two, and write the `List.sortByDescending` key for the third.

[Read the chapter solutions](../solutions/ch-07-records-equality).

## Key takeaways {#summary}

- Tuples combine local values by position; named records bring field vocabulary and type identity into the model.
- An ordinary record is a reference type with immutable fields by default; it also gains structural equality and comparison when its components support them.
- Copy-and-update creates a new record but does not recursively clone referenced objects in unchanged fields.
- An anonymous record's complete field set determines its type; it suits local projection and does not support record patterns.
- Structural equality compares contents; reference identity asks whether two references denote the same runtime object; domain identity may be separate data again.
- Equal values must have equal hash codes, but equal hash codes prove neither equality nor a permanent identity.
- Default structural comparison provides deterministic order; important business order should state an explicit key.

A record alone cannot express the next requirement: a booking must have exactly one of a few mutually exclusive statuses, not an arbitrary combination of Boolean flags.

## Sources {#sources}

- [Microsoft Learn: F# records](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn: Copy and update record expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/copy-and-update-record-expressions)
- [Microsoft Learn: Anonymous records](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/anonymous-records)
- [FSharp.Core: `LanguagePrimitives`, structural hashing, and physical equality](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
- [Microsoft Learn: `Object.GetHashCode`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gethashcode?view=net-10.0)
