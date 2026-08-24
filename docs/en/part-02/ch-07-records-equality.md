---
title: "Chapter 7: Records, Updates, Equality, and Comparison"
description: "Move from positional tuples to named records, use anonymous records and immutable updates, and distinguish structural equality, comparison, reference identity, and hash codes."
translationKey: part-02/ch-07-records-equality
kind: chapter
part: 2
chapter: 7
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch07-records-equality
exerciseIds:
  - ch07-exercise-01
  - ch07-exercise-02
  - ch07-exercise-03
termIds:
  - anonymous-record
  - hash-code
  - immutability
  - record
  - reference-identity
  - structural-comparison
  - structural-equality
  - tuple
sources:
  - id: microsoft-records
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records
    checked: "2026-08-24"
  - id: microsoft-copy-update
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/copy-and-update-record-expressions
    checked: "2026-08-24"
  - id: microsoft-anonymous-records
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/anonymous-records
    checked: "2026-08-24"
  - id: fsharp-core-language-primitives
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html
    checked: "2026-08-24"
  - id: microsoft-gethashcode
    url: https://learn.microsoft.com/en-us/dotnet/api/system.object.gethashcode?view=net-10.0
    checked: "2026-08-24"
---

# Chapter 7: Records, Updates, Equality, and Comparison {#overview}

A tuple combines values, but field meaning exists only in position and surrounding context. To the compiler, `("A-1", "Lin", 2)` is merely `string * string * int`; it does not know that the three positions mean event, attendee, and seat count. As data crosses more functions, position quickly becomes a fragile protocol.

A record promotes that protocol into a type. Fields gain names, construction must be complete, and the same data shape can be reused across functions. F# records are immutable by default and automatically receive structural equality, structural comparison, and matching hash behavior when their components support those operations. Yet “two values have equal contents,” “two references point to the same object,” and “two values have the same hash code” are three different claims. This chapter makes code prove each distinction.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- choose among a local tuple, named record, and anonymous record by purpose;
- define, construct, access, and deconstruct records;
- express immutable change with copy-and-update and explain why it is not a deep copy;
- distinguish an anonymous record's exact shape from a named record's nominal type;
- separate structural equality, structural comparison, and reference identity;
- explain why a hash code must agree with equality but cannot replace equality or a business identifier;
- state business ordering with an explicit key instead of accidentally depending on structural order.

## All three shapes combine values {#product-types}

Tuples, records, and anonymous records all have product shape: one value contains several components at once. The distinction is not how “functional” they are, but their names, reuse boundary, and type identity.

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

<<< @/../examples/scripts/ch07-records-equality.fsx#record-definition{fsharp:line-numbers} [ch07-records-equality.fsx]

`BookingDraft` is a named type. Field labels participate in construction and access, so field order is no longer the caller's only clue to meaning. An ordinary record is a .NET reference type by default, while its fields are not assignable by default. “Reference type” and “mutable object” are not synonyms.

Two separately declared record types remain distinct nominal types even when every field name and type matches. Naming brings domain meaning into the compilation boundary: `BookingDraft` does not automatically become another record merely because its shape looks similar.

### Make type ownership explicit at construction {#construction}

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

The annotation does not repeat every field type; it resolves which type owns the labels. Depending on the most recently declared type with matching labels makes unrelated declaration order affect inference and should be avoided.

A record pattern deconstructs by name:

```fsharp
let label { EventId = eventId; Attendee = attendee; Seats = seats } =
    $"{eventId}:{attendee}:{seats}"
```

Construction and patterns use the same field vocabulary: one builds a value, and one extracts its values.

## Copy-and-update expresses immutable change {#update}

The shared example changes seat count from `2` to `3`:

<<< @/../examples/scripts/ch07-records-equality.fsx#copy-update{fsharp:line-numbers} [ch07-records-equality.fsx]

`{ original with Seats = 3 }` produces a new `BookingDraft`. `original.Seats` remains `2`, while `updated.Seats` is `3`. It states that new state derives from old state with only these fields changed, without repeating the others.

Copy-and-update is not a recursive deep copy. Unchanged field values are retained. If a field contains another reference object, old and new records may continue to refer to that same object. Immutable domain models commonly keep nested values immutable too, removing the need to defend shared state with deep copying.

Starting with F# 7, a field path can update nested records, but shorter syntax does not change that semantic boundary. First ask whether the nested model is clear, then decide whether to compress several `with` expressions.

## Anonymous records are exact temporary named shapes {#anonymous-records}

An anonymous record uses `{| ... |}` and needs no prior type declaration. The shared script projects from a named record and adds a computed field:

<<< @/../examples/scripts/ch07-records-equality.fsx#anonymous-record{fsharp:line-numbers} [ch07-records-equality.fsx]

The shape of `summary` is determined by every field label, every field type, and whether it is a reference or `struct` anonymous record. Another anonymous record has the same anonymous record type only when its shape matches exactly. There is no structural subtype meaning “contains at least these fields.”

Anonymous records support field access, structural equality and comparison, and copy-and-update, including adding fields during an update. They currently do not support record pattern matching, so dot access normally reads their fields.

They suit local projections, query results, and short-range adaptation. If a shape has a domain name, crosses a public boundary, centralizes invariants, or appears in many places, a named record is usually clearer. Anonymous does not mean unimportant, and should not be a default escape from naming a real type.

## Structural equality compares contents {#equality}

Two separately constructed `BookingDraft` values make F# `=` return `true` when corresponding fields are structurally equal:

<<< @/../examples/scripts/ch07-records-equality.fsx#equality-identity-hash{fsharp:line-numbers} [ch07-records-equality.fsx]

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

The reverse is false. Matching hash codes do not prove equality, because unequal values may collide. A hash collection uses a hash code to narrow candidates and then confirms them with consistent equality. If equality and hashing disagree, lookup loses correctness; ordinary records keep that contract automatically.

A hash code is not an object address, database key, request ID, or password digest. Default hash values are not guaranteed to remain stable across runtimes, processes, or platforms. Do not persist, transmit, or display one as a permanent identifier. Cryptographic digests also require a dedicated cryptographic algorithm, not `hash`.

## Structural comparison supplies a default order {#comparison}

When every field supports comparison, a record automatically supports structural comparison and can be passed directly to `List.sort`:

<<< @/../examples/scripts/ch07-records-equality.fsx#structural-comparison{fsharp:line-numbers} [ch07-records-equality.fsx]

This example compares in record declaration order: first `EventId`, then `Attendee` when event IDs tie, and finally `Seats`. Therefore `A-1:Ada:2` appears before `A-1:Lin:1`.

Default order fits values that need determinism and whose structural order matches intent. Business order often differs—for example, seat count descending and then attendee name. Use `List.sortBy` or `List.sortWith` so that rule appears in code. Adding or reordering a record field should not silently alter an important business rule.

Equality and comparison constraints are not identical either. A type may support equality without having a meaningful total order. Chapter 11 states both constraints precisely, and Chapter 14 explains what ordered `Map` and `Set` versus hash collections require.

## Choose a data shape {#choosing-shape}

| Situation | Usually consider first | Reason |
| --- | --- | --- |
| Temporary paired result inside one function | Tuple | Positional meaning is local and obvious; deconstruction is concise |
| Domain data passed repeatedly | Named record | Fields have names, the type has identity, and it can evolve centrally |
| Local projection or short-range adaptation | Anonymous record | Avoids an extra declaration while retaining field labels |
| Several mutually exclusive shapes or states | A discriminated union in the next chapter | One fixed field set cannot say “exactly one of these” |

Do not optimize only for line count. Record and field names are model vocabulary. Anonymous records and tuples avoid inventing useless public types for shapes that truly remain local.

## Run the shared example {#run-example}

From the repository root, run:

```console
dotnet fsi --exec examples/scripts/ch07-records-equality.fsx
```

You should see:

```text
Record update: original=2 updated=3
Anonymous summary: Lin -> 3 seats, group=true
Equality: structural=true physical=false alias=true
Hashes agree for equal records: true
Structural sort: ["A-1:Ada:2"; "A-1:Lin:1"; "B-2:Lin:2"]
```

The five lines fix immutable update, anonymous projection, a content-versus-identity counterexample, the required equality-to-hash contract, and structural sorting. The manifest checks them in order. The script does not print a concrete hash integer because that number is not a stable contract.

## Debugging: state what you are comparing {#debugging}

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

## Summary {#summary}

- Tuples combine local values by position; named records bring field vocabulary and type identity into the model.
- An ordinary record is a reference type with immutable fields by default; it also gains structural equality and comparison when its components support them.
- Copy-and-update creates a new record but does not recursively clone referenced objects in unchanged fields.
- An anonymous record's exact field shape determines its type; it suits local projection and does not support record patterns.
- Structural equality compares contents; reference identity asks whether two references denote the same runtime object; domain identity may be separate data again.
- Equal values must have equal hash codes, but equal hash codes prove neither equality nor a permanent identity.
- Default structural comparison provides deterministic order; important business order should state an explicit key.

The next chapter handles what a record alone cannot express: a booking status is not an arbitrary combination of independent Boolean flags, but should have exactly one of a small number of mutually exclusive shapes.

## Vocabulary {#vocabulary}

- **record:** a product type made of named fields; an ordinary F# record is immutable by default and has structural equality and comparison when its components support them.
- **anonymous record:** a record value with no separate type name, whose exact field labels and types determine its shape.
- **structural equality:** equality determined by recursively comparing corresponding components.
- **structural comparison:** ordering obtained by recursively comparing components in a defined order.
- **reference identity:** whether two references point to one runtime object.
- **hash code:** an equality-consistent integer summary used to locate candidates in hash structures, not a unique identity.

## Sources {#sources}

- [Microsoft Learn: F# records](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn: Copy and update record expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/copy-and-update-record-expressions)
- [Microsoft Learn: Anonymous records](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/anonymous-records)
- [FSharp.Core: `LanguagePrimitives`, structural hashing, and physical equality](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
- [Microsoft Learn: `Object.GetHashCode`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gethashcode?view=net-10.0)
