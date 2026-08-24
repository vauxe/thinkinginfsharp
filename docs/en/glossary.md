---
title: "Appendix F: English–Chinese F# Glossary"
description: "A self-contained glossary generated from the bilingual terminology catalog, with stable anchors and links to each term’s first teaching chapter."
translationKey: glossary
kind: glossary
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---

# Appendix F: English–Chinese F# Glossary {#overview}

This glossary defines the book’s F# vocabulary in English and records the preferred Chinese counterpart. Each definition is complete in English; knowing Chinese is optional. The stable identifier in each entry is used by content metadata and remains unchanged when display wording improves.

“First introduced” means the earliest chapter in reading order whose frontmatter declares that term identifier. It is a teaching location, not a claim that the word never appeared earlier in ordinary prose. Follow the link for motivation, examples, and surrounding concepts.

The entries and links are generated from `docs/terminology.json` and chapter metadata. Edit those sources, then run `pnpm generate:glossary`; `pnpm check:content` rejects stale generated pages.

## How to use this glossary {#how-to-use}

Search the visible English or Chinese term, follow a stable anchor for a direct link, or read by part to revisit concepts in their original learning order. Definitions describe this book’s usage; the linked chapter supplies the operational detail.

## Part 1 · Foundations: values, functions, and flow {#part-1}

### expression · 表达式 {#expression}

A piece of code that is evaluated and, when it completes normally, produces a value.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `expression`

### F# Interactive · F# Interactive {#fsharp-interactive}

The F# interactive environment included with the .NET SDK; it runs submissions in a read-evaluate-print loop and can also execute F# scripts.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `fsharp-interactive`

### F# script · F# 脚本 {#fsharp-script}

A source file with the .fsx extension, normally executed directly by F# Interactive for experiments, automation, and small tools.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `fsharp-script`

### literal · 字面量 {#literal}

A representation of a value written directly in source code, such as 40, true, "hello", or 1.5m.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `literal`

### unit · unit {#unit}

A type with exactly one value, (), used when an expression has no specific result to pass to a later computation.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `unit`

### value · 值 {#value}

A result produced when evaluation completes normally and available to other expressions; a function is itself a value.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview) · **Stable ID:** `value`

### binding · 绑定 {#binding}

An association, introduced by a pattern such as let, between a name and a value; it is not an implicitly rewritable storage slot.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `binding`

### immutability · 不可变性 {#immutability}

The property of not being changed in place; for a binding, it means the name is not reassigned to another value, but it does not automatically make a referenced object's internals immutable.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `immutability`

### numeric conversion · 数值转换 {#numeric-conversion}

Explicitly producing a value of one numeric type from another, such as using decimal to obtain a decimal from an int.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `numeric-conversion`

### shadowing · 遮蔽 {#shadowing}

Introducing a new binding with the same name in an inner or later scope so that the old binding can no longer be reached by that name there; it does not mutate the old value.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `shadowing`

### type annotation · 类型标注 {#type-annotation}

An explicitly written type constraint used to record intent or supply context the compiler cannot infer reliably.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `type-annotation`

### type inference · 类型推断 {#type-inference}

The compiler's deduction of static types from how expressions are used and from their context, without requiring annotations everywhere.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview) · **Stable ID:** `type-inference`

### anonymous function · 匿名函数 {#anonymous-function}

A function value created directly with a fun parameter -> body expression without first naming the function itself.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `anonymous-function`

### argument · 实参 {#argument}

A value or expression actually supplied for a parameter when a function is applied.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `argument`

### automatic generalization · 自动泛化 {#automatic-generalization}

The compiler's promotion of inferred unknown types to type parameters that can be instantiated with multiple types when doing so is safe and the definition does not depend on one concrete type.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `automatic-generalization`

### closure · 闭包 {#closure}

A function value together with surrounding values captured at its definition site and retained for later calls.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `closure`

### currying · 柯里化 {#currying}

Representing a multi-parameter computation as successive single-parameter functions that return the next function; this is the usual form of F# let-bound functions.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `currying`

### function · 函数 {#function}

A value that accepts input and computes a result; in F#, functions can be bound, passed, and returned like other values.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `function`

### function application · 函数应用 {#function-application}

Supplying an argument to a function value and evaluating its body to produce a result.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `function-application`

### higher-order function · 高阶函数 {#higher-order-function}

A function that accepts at least one function value as a parameter or returns a function value as its result.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `higher-order-function`

### parameter · 形参 {#parameter}

A name or pattern in a function definition that receives an argument supplied by a caller.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `parameter`

### partial application · 部分应用 {#partial-application}

Supplying only some arguments to a curried function to obtain a new function that awaits the remaining arguments.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `partial-application`

### tuple · 元组 {#tuple}

A type that combines a fixed number of positional values, whose component types may differ and are joined by an asterisk in a type signature.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview) · **Stable ID:** `tuple`

### exhaustiveness · 穷尽性 {#exhaustiveness}

The property that a set of patterns covers every possible shape of the input type; without it, some input may have no matching branch.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `exhaustiveness`

### guard · 守卫 {#guard}

A Boolean when condition evaluated only after its pattern initially matches; when false, matching continues with later rules.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `guard`

### list · 列表 {#list}

An ordered immutable singly linked collection of elements of one type; [] is empty, and :: constructs or decomposes a head and tail.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `list`

### pattern · 模式 {#pattern}

A shape rule used to test input structure, decompose components, and optionally bind local names to them.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `pattern`

### pattern matching · 模式匹配 {#pattern-matching}

Selecting a branch by the shape of a value while optionally binding names to its constituent parts.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `pattern-matching`

### wildcard pattern · 通配符模式 {#wildcard-pattern}

The _ pattern, which matches any input without binding an available name to that input.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview) · **Stable ID:** `wildcard-pattern`

### eager evaluation · 立即求值 {#eager-evaluation}

Computing a result when an operation is called rather than delaying work until later enumeration or demand.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview) · **Stable ID:** `eager-evaluation`

### effect · 效果 {#effect}

Observable behavior during evaluation that is not described by the return value alone, such as output, file writes, or state changes.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview) · **Stable ID:** `effect`

### mutable binding · 可变绑定 {#mutable-binding}

A binding introduced with let mutable whose storage location can subsequently be updated with <-.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview) · **Stable ID:** `mutable-binding`

### option · option {#option}

An F# type representing a present value as Some value and absence as None; Chapter 9 develops its full modeling rules.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview) · **Stable ID:** `option`

### pipeline · 管道 {#pipeline}

Using |> to pass the result on the left as the final argument of the function application on the right, expressing transformations in data-flow order.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview) · **Stable ID:** `pipeline`

### accumulator · 累加器 {#accumulator}

A value threaded into the next recursive or folding step that represents the result completed so far.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `accumulator`

### fold · 折叠 {#fold}

A higher-order operation that combines collection elements into an accumulator in a defined order and returns the final state.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `fold`

### recursion · 递归 {#recursion}

A function calling itself directly or indirectly to continue with a smaller problem or one closer to a termination condition.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `recursion`

### structural recursion · 结构递归 {#structural-recursion}

Recursion that branches on a data type's construction shape and recursively processes a structurally smaller component.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `structural-recursion`

### tail call · 尾调用 {#tail-call}

A call made as the last operation before a function branch returns, whose result needs no further processing by the current stack frame.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `tail-call`

### tail recursion · 尾递归 {#tail-recursion}

Recursion whose recursive paths place the recursive call in tail position, allowing the compiler an opportunity to eliminate recursive stack growth.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview) · **Stable ID:** `tail-recursion`

## Part 2 · Modeling with types {#part-2}

### anonymous record · 匿名记录 {#anonymous-record}

A record value whose exact shape is determined by its field labels and types without a separately declared type name.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `anonymous-record`

### hash code · 哈希码 {#hash-code}

An integer summary derived consistently with equality to locate candidates in hash-based structures; unequal values may still share one code.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `hash-code`

### record · 记录 {#record}

A product type made of named fields; an ordinary F# record is immutable by default and automatically supports structural equality and comparison when its components do.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `record`

### reference identity · 引用身份 {#reference-identity}

The relation of two references pointing to the same runtime object, separate from whether their contents are structurally equal.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `reference-identity`

### structural comparison · 结构比较 {#structural-comparison}

An ordering obtained by recursively comparing a composite value's components in a defined order, requiring each relevant component type to support comparison.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `structural-comparison`

### structural equality · 结构相等 {#structural-equality}

Equality determined by recursively comparing corresponding components of composite values rather than checking whether they are the same object.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview) · **Stable ID:** `structural-equality`

### discriminated union · 可辨识联合 {#discriminated-union}

A type made of named cases; each value belongs to exactly one case, and a case may carry additional data.

**First introduced:** [Chapter 8: Discriminated Unions and State Modeling](./part-02/ch-08-discriminated-unions#overview) · **Stable ID:** `discriminated-union`

### union case · 联合案例 {#union-case}

One named possible shape of a discriminated union, carrying either no data or fields meaningful only for that shape.

**First introduced:** [Chapter 8: Discriminated Unions and State Modeling](./part-02/ch-08-discriminated-unions#overview) · **Stable ID:** `union-case`

### Result · Result {#result}

An F# type representing success as Ok value and an expected failure with a modeled reason as Error error.

**First introduced:** [Chapter 9: Absence and Expected Failure](./part-02/ch-09-option-result#overview) · **Stable ID:** `result`

### short-circuit · 短路 {#short-circuit}

Stopping a composition at a None or Error that cannot continue, preserving that result without running later dependent steps.

**First introduced:** [Chapter 9: Absence and Expected Failure](./part-02/ch-09-option-result#overview) · **Stable ID:** `short-circuit`

### recursive type · 递归类型 {#recursive-type}

A type that refers to itself in part of its own definition, allowing values with finite nested structure.

**First introduced:** [Chapter 10: Recursive Types and Structural Recursion](./part-02/ch-10-recursive-types#overview) · **Stable ID:** `recursive-type`

### comparison constraint · 比较约束 {#comparison-constraint}

The 'T : comparison requirement that a type parameter support F# generic comparison and ordering operations.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `comparison-constraint`

### equality constraint · 相等约束 {#equality-constraint}

The 'T : equality requirement that a type parameter support F# generic equality operations.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `equality-constraint`

### generic type parameter · 泛型类型参数 {#generic-type-parameter}

A type-level parameter representing an as-yet unspecified type within a definition and replaced consistently by a type argument at each concrete use.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `generic-type-parameter`

### statically resolved type parameter · 静态解析类型参数 {#statically-resolved-type-parameter}

An F# type parameter written ^T, resolved at an inline call site, and able to carry member constraints; it differs from an ordinary 'T generic parameter.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `statically-resolved-type-parameter`

### unit of measure · 度量单位 {#unit-of-measure}

A compile-time type annotation attached to supported numeric types to check dimensional relationships statically and erased at runtime.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `unit-of-measure`

### value restriction · 值限制 {#value-restriction}

The restriction of automatic generalization to safe binding shapes, rejecting nongeneralizable values with unresolved type variables so one storage location cannot be used unsafely at multiple types.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview) · **Stable ID:** `value-restriction`

### access control · 访问控制 {#access-control}

The mechanism for specifying which code locations may use a program entity through public, internal, private, or a signature file.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **Stable ID:** `access-control`

### invariant · 不变量 {#invariant}

A condition intended to remain true for every publicly obtainable value of a protected type.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **Stable ID:** `invariant`

### private representation · 私有表示 {#private-representation}

A design in which callers can use a type but cannot directly use its underlying union cases, record construction, or other representation details.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **Stable ID:** `private-representation`

### signature file · 签名文件 {#signature-file}

An F# .fsi file placed before its corresponding .fs implementation that declares the public surface visible to other files.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **Stable ID:** `signature-file`

### smart constructor · 智能构造函数 {#smart-constructor}

A function that validates or normalizes input before producing a protected domain value and reports rejection through an explicit return type.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **Stable ID:** `smart-constructor`

## Part 3 · Composition and program structure {#part-3}

### function composition · 函数组合 {#function-composition}

Connecting one function's output to the next function's input to obtain a new function value from multiple function values.

**First introduced:** [Chapter 13: Composition, Argument Order, and Pipeline APIs](./part-03/ch-13-composition-pipeline-api#overview) · **Stable ID:** `function-composition`

### array · 数组 {#array}

An ordered same-type collection with fixed length, contiguous storage, and elements that can be updated in place; changing length requires a new array.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `array`

### deferred evaluation · 延迟求值 {#deferred-evaluation}

Delaying value production or work until a consumer requests results; whether work repeats depends on the source and on caching.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `deferred-evaluation`

### enumeration · 枚举 {#enumeration}

A traversal in which a consumer requests collection elements through an enumerator; the concrete source determines the work performed by each enumeration.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `enumeration`

### map · 映射表（Map） {#map}

An immutable tree of key-value bindings organized by F# generic comparison of keys; each key has at most one value.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `map`

### sequence · 序列 {#sequence}

`seq<'T>` is a type abbreviation for `IEnumerable<'T>` that describes how to enumerate same-type elements but does not itself guarantee caching, purity, or repeatable traversal.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `sequence`

### set · 集合（Set） {#set}

An immutable tree of unique elements organized by F# generic comparison of the elements.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview) · **Stable ID:** `set`

### active pattern · 活动模式 {#active-pattern}

A function-backed view of an input used as a named pattern to classify or decompose a value during matching.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview) · **Stable ID:** `active-pattern`

### complete active pattern · 完整活动模式 {#complete-active-pattern}

An active pattern that returns a named case for every input; its multi-case form partitions the whole input space.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview) · **Stable ID:** `complete-active-pattern`

### parameterized active pattern · 参数化活动模式 {#parameterized-active-pattern}

A single-case active pattern that accepts extra arguments before the final matched input to specialize recognition at the use site.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview) · **Stable ID:** `parameterized-active-pattern`

### partial active pattern · 部分活动模式 {#partial-active-pattern}

A single-case active pattern that recognizes only part of the input space and may fail to match, with a wildcard case ending its name list.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview) · **Stable ID:** `partial-active-pattern`

### assembly · 程序集 {#assembly}

A .NET-compiled .dll or .exe, together with its metadata and code, used as a unit of deployment, loading, and reference.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `assembly`

### compilation order · 编译顺序 {#compilation-order}

The sequence in which F# source files are supplied to the compiler; later files can ordinarily use earlier definitions, but not the reverse.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `compilation-order`

### module · 模块 {#module}

An F# construct that groups related types, values, and functions in one named scope and can itself live in a namespace or another module.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `module`

### namespace · 命名空间 {#namespace}

A named container that can organize types and modules across files and assemblies but cannot directly contain F# values or functions.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `namespace`

### nullable reference type · 可空引用类型 {#nullable-reference-type}

With F# null checking enabled, a `T | null` reference-type annotation that explicitly permits null; it is a compile-time contract, not a runtime wrapper.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `nullable-reference-type`

### open declaration · open 声明 {#open-declaration}

A declaration that makes names from a namespace or module available by shorter references in the following scope without loading code or changing accessibility.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `open-declaration`

### project file · 项目文件 {#project-file}

An MSBuild XML file describing target framework, compile-item order, references, and build properties; an F# project normally uses the .fsproj extension.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview) · **Stable ID:** `project-file`

### abstract representation · 抽象表示 {#abstract-representation}

A signature exposes a type name while omitting its union cases, record fields, or other implementation shape, so consumers can use values of the type without depending on its representation.

**First introduced:** [Chapter 17: Signatures, Access Control, and F#-Facing APIs](./part-03/ch-17-signatures-encapsulation#overview) · **Stable ID:** `abstract-representation`

### public API surface · 公共 API 表面 {#public-api-surface}

The set of types, cases, functions, members, and signatures that a component intentionally exposes and commits to supporting for consumers.

**First introduced:** [Chapter 17: Signatures, Access Control, and F#-Facing APIs](./part-03/ch-17-signatures-encapsulation#overview) · **Stable ID:** `public-api-surface`

### computation expression · 计算表达式 {#computation-expression}

F# syntax interpreted through builder members to compose computations with a particular context or control flow.

**First introduced:** [Chapter 18: Explicit Workflow Composition and Validation Accumulation](./part-03/ch-18-workflow-validation#overview) · **Stable ID:** `computation-expression`

### validation accumulation · 验证错误累积 {#validation-accumulation}

A combination strategy that evaluates independent checks and merges their failures in an explicit order into one error collection.

**First introduced:** [Chapter 18: Explicit Workflow Composition and Validation Accumulation](./part-03/ch-18-workflow-validation#overview) · **Stable ID:** `validation-accumulation`
