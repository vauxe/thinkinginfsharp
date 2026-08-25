---
title: "Chapter 7 Solutions"
description: "Reasoning about tuple migration, immutable updates, structural equality, reference identity, hash contracts, and business ordering."
translationKey: solutions/ch-07-records-equality
---

# Chapter 7 Solutions {#overview}

These answers discuss type, contents, object, and domain identity separately. Saying only that two things are “the same” does not identify the rule a program should use.

[Return to Chapter 7](../part-02/ch-07-records-equality).

## Exercise 1: migrate a tuple to a record {#exercise-01}

One complete rewrite is:

```fsharp
type BookingDraft =
    { EventId: string
      Attendee: string
      Seats: int }

let draft =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let format { EventId = eventId; Attendee = attendee; Seats = seats } =
    $"{eventId}:{attendee}:{seats}"

let attendee = draft.Attendee
```

The record removes the risk of swapping two `string` positions while still type-checking, and callers need not remember a third position. It does not guarantee nonempty strings, positive seats, an existing event, or sufficient capacity. Those are invariants and workflow rules addressed in Chapter 12 and the capstone.

## Exercise 2: trace copies and identity {#exercise-02}

The shared definitions provide the predictions directly:

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
Let `updated = { original with Seats = 3 }`, let `equalCopy` repeat all original fields, and let `alias = original`:

| Comparison | Result | Reason |
| --- | --- | --- |
| `original = equalCopy` | `true` | All three fields are structurally equal |
| `PhysicalEquality original equalCopy` | `false` | The reference objects were constructed separately |
| `PhysicalEquality original alias` | `true` | Both names point to one object |
| `original = updated` | `false` | `Seats` differs |

Copy-and-update retains unchanged field values. If one field were an array, old and new records could point to that same array. Mutating an array element would then be visible through both paths. The mutable nested object is the issue; record fields were not reassigned.

## Exercise 3: design equality, hashing, and order {#exercise-03}

- Remove content-equal drafts with structural equality because their fields define equivalence for this requirement.
- Check whether two variables denote one cached object with reference identity. If the cache contract is key-based, comparing an explicit cache key is often clearer.
- Decide whether real bookings are one business entity with an explicit booking or request ID, not by guessing from contents or object identity.
- State display order as `bookings |> List.sortByDescending (fun booking -> booking.Seats)`. If equal seat counts need attendee-name ascending order, use `List.sortWith` to state both levels instead of relying on record declaration order.

`hash x = hash y` is necessary for structural equality, not sufficient. Unequal values may collide, so matching hashes cannot replace `x = y`; a hash also says nothing about whether two references denote one object.

## What to notice {#what-to-notice}

- **Names improve readability but do not establish invariants:** moving from a tuple to a record is only the first modeling step.
- **Immutable update is not deep copying:** the record itself is not changed, while nested references may still be shared.
- **Requirements choose equality:** content, object identity, and business identity can each be valid but cannot be mixed carelessly.
- **Hashing is an indexing mechanism:** do not persist it, use it as an ID, or assert equality from it alone.
- **Business order should be explicit:** default structural comparison is a language-provided order, not necessarily a product rule.
