---
title: "Appendix B: Syntax and Operator Quick Reference"
description: "Read common F# types, expressions, patterns, declarations, computation expressions, and operators without turning a quick reference into a second language specification."
translationKey: appendices/b-syntax-reference
kind: appendix
appendix: B
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch02-values-bindings-expressions
  - ch03-functions-as-values
  - ch04-branching-patterns
  - ch08-discriminated-unions
exerciseIds: []
termIds: []
sources:
  - id: microsoft-fsharp-language-reference
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/
    checked: "2026-08-25"
  - id: microsoft-fsharp-types
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-types
    checked: "2026-08-25"
  - id: microsoft-fsharp-type-inference
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference
    checked: "2026-08-25"
  - id: microsoft-fsharp-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-25"
  - id: microsoft-fsharp-patterns
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching
    checked: "2026-08-25"
  - id: microsoft-fsharp-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-25"
  - id: microsoft-fsharp-computation-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions
    checked: "2026-08-25"
  - id: microsoft-fsharp-symbols
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/symbol-and-operator-reference/
    checked: "2026-08-25"
  - id: microsoft-fsharp-formatting
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting
    checked: "2026-08-25"
---

# Appendix B: Syntax and Operator Quick Reference {#overview}

This appendix is a lookup surface for syntax already explained in the chapters. It is not a substitute for the type checker, the F# Language Reference, or the problem-specific discussion. When unfamiliar code is dense, read its inferred or declared types first; punctuation becomes easier once the data flow is known.

All complete F# blocks below are pulled from examples already registered and executed by the repository gate. Tiny forms inside tables are syntax shapes, not new untested programs.

## Read a type from the outside inward {#read-types}

| Shape | Read it as | Important distinction |
|---|---|---|
| `int`, `decimal`, `bool`, `string` | one named type | numeric literals and conversions constrain inference |
| `'T` | an ordinary generic type parameter | the compiler may infer and generalize it |
| `unit` | a type with the single ordinary value `()` | it means completion, not null or missing data |
| `'T option` | `Some value` or `None` | models possible absence in trusted F# data |
| `Result<'T, 'Error>` | `Ok value` or `Error error` | models an expected success/failure outcome |
| `'T list` | immutable linked list | different from array and general sequence |
| `'T array` or `'T[]` | mutable indexed array | one runtime array representation |
| `seq<'T>` | enumerable source | does not promise eager, cached, or single evaluation |
| `'A * 'B` | tuple with two fields | `*` separates tuple parts in a type |
| `Name: string` | named field/member/parameter annotation | `:` is an annotation separator here |
| `Input -> Output` | function type | the arrow separates input from result |
| `Type<Arg>` | generic .NET/F# type application | for example `Task<int>` or `Map<string, int>` |

Function arrows associate to the right:

```text
decimal -> int -> decimal
= decimal -> (int -> decimal)
```

This is a curried function: give it a `decimal` and it returns a function awaiting an `int`. A tupled function has the distinct type `decimal * int -> decimal`.

Function application associates to the left:

```text
lineTotal 19.50m 3
= (lineTotal 19.50m) 3
```

Parentheses group a value; they do not define invocation syntax. `f (x, y)` passes one tuple. `f x y` performs two successive applications.

### Decode a higher-order signature {#higher-order-signature}

Read this common shape one arrow at a time:

```text
List.fold : ('State -> 'T -> 'State) -> 'State -> 'T list -> 'State
```

It accepts:

1. a folder from current state and one element to next state;
2. an initial state;
3. a list of elements;
4. and returns the final state.

The parentheses around the folder matter because that whole function is the first argument. Type names such as `'State` and `'T` describe relationships: the initial, intermediate, and final state must agree; every list element has one element type.

## Bind values and return expressions {#bindings-expressions}

`let` binds a name to the value of an expression. It is not a statement terminator or a promise of mutability.

<<< @/../examples/scripts/ch02-values-bindings-expressions.fsx#basic-values{fsharp:line-numbers} [ch02-values-bindings-expressions.fsx]

| Form | Meaning |
|---|---|
| `let name = expression` | immutable binding |
| `let name: Type = expression` | binding with an annotation |
| `let mutable name = expression` | mutable storage local to its scope |
| `name <- expression` | assignment to mutable storage or a settable property |
| `let rec f x = ...` | recursive binding; add `and` for mutual recursion |
| `let private name = ...` | declaration restricted by accessibility |
| `use resource = expression` | bind and dispose at the end of the lexical scope |

A multi-line construct returns its final expression. `printfn`, assignment, loops, and many effect calls return `unit`; they do not become a useful result merely because they are last.

Indentation is syntax. Aligned lines belong to the same block; dedenting closes it. Prefer formatter-stable layout over manual alignment tricks. `;` separates compact list/record elements or expressions in limited forms; it is not required at every line end. `;;` terminates an interactive submission and normally does not belong in `.fs` or `.fsx` files.

Shadowing creates a new binding with the same name. It does not mutate the earlier value. Use it sparingly when each step is a clear refinement in a small scope.

## Define and apply functions {#functions}

<<< @/../examples/scripts/ch03-functions-as-values.fsx#curried-function{fsharp:line-numbers} [ch03-functions-as-values.fsx]

| Form | Meaning |
|---|---|
| `let f x y = body` | named curried function |
| `let f (x, y) = body` | named function taking one tuple |
| `fun x -> body` | anonymous function |
| `let f (x: Type): Result = body` | parameter and return annotations |
| `let partiallyApplied = f first` | function awaiting remaining arguments |
| `value |> f` | `f value` |
| `(a, b) ||> f` | `f a b` |
| `f >> g` | function applying `f`, then `g` |
| `f << g` | function applying `g`, then `f` |

Choose parameter order so the stable configuration arrives first and the changing data can arrive last through `|>`. A pipeline changes grouping, not evaluation, failure, nullability, or performance semantics.

`ignore` consumes a value and returns `unit`. Use it when discarding is intentional; do not use it to hide a result whose success or failure matters.

## Branch with expressions and patterns {#branching-patterns}

`if/then/else` is an expression. Both branches must have a compatible type. Omitting `else` is allowed only when the `then` branch has type `unit`.

<<< @/../examples/scripts/ch04-branching-patterns.fsx#guarded-match{fsharp:line-numbers} [ch04-branching-patterns.fsx]

Match cases run top to bottom. Prefer structural cases before a final wildcard, and let compiler exhaustiveness feedback expose new domain states.

| Pattern | Meaning |
|---|---|
| `_` | accept and discard any value |
| `name` | accept any value and bind it to a new name |
| `42`, `"open"`, `true` | match a literal |
| `(left, right)` | decompose a tuple |
| `{ Name = name }` | select fields from a record |
| `Some value`, `None` | decompose an option |
| `Ok value`, `Error error` | decompose a result |
| `head :: tail`, `[]` | decompose a list |
| `case as whole` | retain both the decomposition and whole value |
| `p1 | p2` | OR pattern; both alternatives must bind compatible names |
| `:? Type as value` | runtime type test and binding at a .NET boundary |
| `pattern when condition` | guard after structural matching |

A lowercase identifier in a pattern usually **binds**; it does not compare against an earlier value with the same spelling. Use a literal, a union case, or an explicit guard for equality.

`function | pattern -> result | ...` is shorthand for `fun value -> match value with ...`. Use it when the missing parameter name improves rather than hides the code.

## Model data with records and unions {#records-unions}

| Form | Purpose |
|---|---|
| `type Person = { Name: string; Age: int }` | named product: all fields exist together |
| `{ old with Age = old.Age + 1 }` | record copy/update; creates a new record |
| `{| Name = "Ada"; Age = 36 |}` | anonymous record, often local or boundary-shaped |
| `type Status = Pending | Confirmed of string` | named alternatives with case-specific data |
| `type UserId = private UserId of string` | single-case union hiding unchecked construction |
| `type Alias = string` | abbreviation only; not a distinct domain type |

Union cases begin with uppercase identifiers. Case fields may be named to improve generated signatures and interoperation.

<<< @/../examples/scripts/ch08-discriminated-unions.fsx#union-definition{fsharp:line-numbers} [ch08-discriminated-unions.fsx]

<<< @/../examples/scripts/ch08-discriminated-unions.fsx#exhaustive-match{fsharp:line-numbers} [ch08-discriminated-unions.fsx]

Use records for simultaneous facts and unions for alternatives. Do not reproduce a union with independent Boolean flags unless contradictory combinations are genuinely valid.

## Recognize collection syntax {#collections}

| Syntax | Value/evaluation shape |
|---|---|
| `[ 1; 2; 3 ]` | list |
| `1 :: rest` | a new list with one head prepended |
| `left @ right` | list concatenation; copies the left spine |
| `[| 1; 2; 3 |]` | array |
| `array[index]` | index lookup |
| `source[start..finish]` | slice, subject to the source's rules |
| `seq { yield 1; yield 2 }` | sequence expression, normally deferred |
| `[ for x in source do yield f x ]` | list comprehension |
| `[ start..finish ]` | inclusive range under the element type's range rules |

`List`, `Array`, and `Seq` modules expose similarly named functions but preserve different storage and evaluation contracts. Appendix C compares those contracts; this table only identifies surface syntax.

## Recognize effects and computation expressions {#effects-computation-expressions}

| Form | Boundary |
|---|---|
| `try expression with | pattern -> handler` | translate selected exceptions |
| `try expression finally cleanup` | always run synchronous cleanup |
| `use x = acquire ()` | lexical `IDisposable` ownership |
| `raise exception` | raise an exception expression |
| `async { ... }` | F# async workflow built by `async` |
| `task { ... }` | .NET task workflow built by `task` |
| `let! x = operation` | builder-defined bind inside a computation expression |
| `do! operation` | bind and discard a unit-like result |
| `return value` / `return! work` | builder-defined return / delegated return |
| `yield value` / `yield! values` | builder-defined emission / delegated emission |
| `use! x = operation` | builder-defined asynchronous acquisition plus disposal scope |

The identifier before `{ ... }` selects a builder. The keywords with `!` therefore get meaning from that builder; braces alone do not guarantee concurrency, laziness, cancellation, exception translation, or rollback. Read the resulting type and the builder's contract.

Outside a computation expression, an F# function returns its final expression—there is no general `return` statement.

## Read .NET object and interop forms {#dotnet-interop}

| Form | Meaning |
|---|---|
| `Type(arguments)` | construct a .NET/F# object; `new` is usually optional |
| `value.Member` | property, field, or method lookup |
| `value.Method(argument)` | tupled/CLI-style member call |
| `object.Property <- value` | settable property assignment |
| `value :> BaseType` | statically checked upcast |
| `value :?> DerivedType` | runtime-checked downcast; can throw |
| `value :? Type` | runtime type test |
| `null` / `Type | null` | nullable .NET reference boundary under null checking |
| `Nullable<'T>` | .NET nullable value wrapper; distinct from `'T option` |

Member overloads, optional parameters, delegates, events, attributes, and null annotations come from the exposed .NET API. Add a type annotation at the narrow boundary when inference cannot select the intended overload; do not spread annotations through otherwise clear pure code.

## Decode common operators and symbols {#operators-symbols}

| Symbol | Read it as | Do not confuse it with |
|---|---|---|
| `=` / `<>` | structural equality / inequality when the type supports it | assignment (`<-`) |
| `<`, `<=`, `>`, `>=` | structural comparison when the type supports it | domain-specific ordering proof |
| `&&`, `||`, `not` | short-circuit Boolean operations | bitwise `&&&`, `|||`, `~~~` |
| `+`, `-`, `*`, `/`, `%`, `**` | overloaded arithmetic | identical behavior for every numeric type |
| `|>` / `<|` | forward / reverse application | composition |
| `>>` / `<<` | forward / reverse composition | executing either function immediately |
| `::` / `@` | list prepend / list concatenate | array mutation |
| `^` | F# string concatenation in ordinary expression use | caret seen in older/explicit SRTP syntax |
| `->` | function/result arrow or match-case separator | mutation |
| `<-` | assignment or property set | equality |
| `:` / `:>` / `:?>` / `:?` | annotation / upcast / downcast / type test | union case payload syntax |
| `|` | union or match-case delimiter | forward pipe `|>` |
| `!` after a keyword | computation-expression variant such as `let!` | deprecated ref-cell dereference syntax |
| `[<Attribute>]` | attribute | list syntax |
| `#load`, `#r`, `#if` | script/compiler directive | flexible type syntax `#Base` |
| `<@ expression @>` | typed quotation | ordinary execution |

Reference-cell `!cell` and `cell := value` forms produce advisories in current F#; prefer `cell.Value` and `cell.Value <- value` when a reference cell is the intentional state container.

## Use precedence as a warning, not a memory contest {#precedence}

Function/member application binds tightly; multiplication binds more tightly than addition; comparisons sit outside arithmetic; Boolean operators combine comparisons; pipelines are intentionally low-precedence data-flow operators. `::` associates to the right, as does function type `->`. Custom operator precedence follows its leading symbol family.

When two plausible readings would produce different behavior, add parentheses or a named intermediate value. In particular, clarify mixed arithmetic and comparison, pipeline arguments containing `if`/`match`, nested function values, casts, and custom operators. Formatting communicates a reading but does not override the grammar.

Whitespace also matters. Write binary subtraction as `x - 1` and unary negation as `-x`. A space-separated function call is not the same surface as a CLI-style member call with parentheses.

## Recognize declaration and file-level forms {#declarations-files}

| Form | Role |
|---|---|
| `namespace Company.Product` | CLR namespace; cannot directly contain ordinary value bindings |
| `module Name = ...` | named module containing types and values |
| `module Name` at file top | top-level module form |
| `open Namespace.Or.Module` | bring names into lookup; does not import files |
| `type Name = ...` | record, union, class, interface, enum, alias, or other type form |
| `member this.Name ...` | instance member |
| `static member Name ...` | static member |
| `abstract member Name: ...` | abstract/interface contract |
| `interface IName with ...` | explicit interface implementation |
| `.fsi` before matching `.fs` | signature restricting the visible F# surface |
| project `<Compile Include="..." />` order | compilation order; definitions precede consumers |

`open` only shortens name lookup. Project references make assemblies available; file entries compile source; `#load` is an FSI/script mechanism. They solve different problems.

## Route the question back to the chapter {#chapter-map}

| If the confusing surface is… | Return to… |
|---|---|
| values, annotations, conversion, shadowing | [Chapter 2](../part-01/ch-02-values-bindings-expressions) |
| currying, partial application, higher-order functions | [Chapter 3](../part-01/ch-03-functions-as-values) |
| `if`, `match`, guards, tuple/list patterns | [Chapter 4](../part-01/ch-04-branching-patterns) |
| list pipelines or folds | [Chapters 5–6](../part-01/ch-05-lists-pipelines) |
| records, equality, comparison | [Chapter 7](../part-02/ch-07-records-equality) |
| unions and exhaustive state modeling | [Chapter 8](../part-02/ch-08-discriminated-unions) |
| option and result composition | [Chapter 9](../part-02/ch-09-option-result) |
| generics, constraints, units of measure | [Chapter 11](../part-02/ch-11-generics-constraints) |
| modules, namespaces, file order, projects | [Chapter 16](../part-03/ch-16-modules-namespaces-projects) |
| exceptions, resources, async/task, cancellation | [Chapters 21–23](../part-04/ch-21-exceptions-resources-io) |
| objects and .NET boundary syntax | [Chapters 25–27](../part-05/ch-25-objects-interfaces) |
| quotations, current SRTP, flexible types, byrefs | [Appendix H](h-advanced-index) |

## Official entry points {#official-entry-points}

- [F# Language Reference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/)
- [F# types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-types)
- [Type inference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Pattern matching](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Computation expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
- [Symbol and operator reference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/symbol-and-operator-reference/)
- [F# code formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
