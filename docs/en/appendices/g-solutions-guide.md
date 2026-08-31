---
title: "Appendix G: Working with Exercises and Answers"
description: "Use each chapter's inline answers to review reasoning, constraints, and evidence without treating open engineering exercises as having one canonical response."
translationKey: appendices/g-solutions-guide
---

# Appendix G: Working with Exercises and Answers {#overview}

Each exercise is followed by a collapsed answer. Attempt the exercise first, record what you expect, and expand the answer only when you are ready to compare reasoning. Matching the final output is not enough when the exercise also asks about types, effects, failure, or ownership.

## Before opening an answer {#before-opening}

1. Restate the required behavior and explicit constraints in your own words.
2. Predict important types, output, failure, and effect order before running code.
3. Run the smallest relevant command and keep unexpected diagnostics instead of editing blindly toward the published answer.
4. Explain why your answer satisfies the task, then compare decisions rather than line counts.

## Three exercise kinds {#exercise-kinds}

| Kind | What can be checked directly | Where variation remains |
|---|---|---|
| Closed behavior | Required value, type, output order, diagnostic, or test | Names and implementation may differ while preserving the whole contract |
| Diagnosis | Reproduction command, first relevant evidence, root cause, and repair | Several repairs may compile; only those preserving intended semantics qualify |
| Open design | Constraints, invariants, boundaries, failure policy, and verification plan | Representation, library, architecture, and rollout may vary with explicit tradeoffs |

## Review open designs {#review-rubric}

A strong open-design answer:

- covers every required input, output, failure, and non-goal;
- represents valid states clearly and rejects invalid input at a deliberate boundary;
- identifies where I/O, time, mutation, resources, and cancellation are owned;
- keeps public APIs natural for their callers without leaking internal representation;
- distinguishes executed checks, documentation review, and unverified proposals;
- compares a credible alternative and states when to stop or reverse the choice.

Different answers are acceptable when they preserve the written constraints, expose new assumptions, and verify the risks that matter. A simpler answer with equal or better evidence should improve the published answer, not be rejected for looking different.

## Match claims to evidence {#evidence}

| Claim | Minimum suitable evidence |
|---|---|
| Type relationship or diagnostic | Exact SDK/compiler command and the relevant signature or diagnostic |
| Pure behavior or invariant | Focused example or property test with a boundary or counterexample |
| Resource, async, concurrency, or interoperability behavior | Real boundary test with deterministic coordination and cleanup |
| Framework, package, or architecture choice | Written constraints, authoritative-source review, a focused trial, and a rollback condition |

Do not report an unrun platform check as passing, hide a relevant warning, leak a secret, silently change a public API, or use timing sleeps as proof of concurrency behavior.
