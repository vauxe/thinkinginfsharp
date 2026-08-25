# English Independent Reader and Semantic-Parity Review

## 1. Record identity

| Field | Value |
| --- | --- |
| Scope | English home, preface, 45 chapters, 45 solution pages, eight appendices, and the examples, exercises, and navigation contracts shared with Chinese |
| Review type | bilingual independence / English reader journey |
| Reviewer | Codex `/root` |
| Context | route-isolated; each English route was read without relying on Chinese, then compared with its paired pages; this was not an independent human usability study |
| Commit | `b2be72904f4f1cbf733b7263efbf1ad5ff6b708d`; English wording fixes in `a43ed081d8302a5e931312fafbc610180cae12db` |
| Review time | `2026-08-25 11:17 JST` |
| Source cutoff | not applicable; R01 owns source and version review |
| Locales | en; parity checks cover both |

## 2. Environment

```text
OS and architecture: macOS 26.3 (25D125), arm64
.NET SDK and F#: 10.0.301; FSI 15.2.301.0 for F# 10.0
Node and pnpm: 26.4.0; 11.7.0
Browser and viewport: not run — R05 owns rendered interaction and visual evidence
Other material inputs: docs/en, paired docs/zh, shared examples, content/navigation generators
```

## 3. Scope and sampling

### In scope

- Full structural scan of all 100 English locale pages and their 100 Chinese counterparts.
- Quick-start route: home, preface, Chapters 1–6 introductions, outcomes, examples, and exercises, plus the Chapter 1 solution.
- Systematic route: Chapters 7, 9, 12, 15, 18, and 33–38, with the Chapter 22 and 38 solutions for effect and release reasoning.
- C#/.NET/ecosystem route: Appendix D; Chapters 19, 22, 24, 27, 32, and 39–45; and the Chapter 44 solution.
- Terminology, limitation language, exercise-to-answer flow, return links, and whether prerequisites and evidence boundaries survive when English is the only language read.

### Out of scope

- R02 owns F# semantic correctness; R01 owns external sources and versions.
- R05 owns rendered search, copy controls, keyboard behavior, narrow layouts, and visual accessibility.
- This was not a literary copyedit of every one of the 25,022 English lines or a usability study with external English learners.

### Sampling rule

Automation covered every page's metadata, heading shape, anchors, targets, code references, exercise identifiers, and generated navigation. Manual reading followed all three specified routes, spanning foundations, advanced language use, the capstone, and ecosystem decisions. Each English segment had to stand on its own before its Chinese peer was consulted for omissions or narrowed claims.

## 4. Commands and evidence

| ID | Status | Command, sample, or action | Observed result |
| --- | --- | --- | --- |
| E-01 | passed | `env CI=true pnpm run check:parity` | All 100 pairs passed paired metadata, heading/anchor, link-target, and shared-code-reference contracts |
| E-02 | passed | `env CI=true pnpm run check:content` | Glossary, all 45 solution entries, navigation, and content contracts passed; no missing, draft, placeholder, broken, orphaned, or stale generated page |
| E-03 | passed | `env CI=true pnpm run test:content` | 38/38, including negative tests for bilingual link, code-reference, exercise-answer, and navigation drift |
| E-04 | passed | Full structural recount | 100 pages and 25,022 lines per locale; 100/100 equal-line pairs, with 1,757 heading anchors, 239 shared code references, and 432 exercise/answer subanchors paired in order |
| E-05 | passed | Page-kind and answer-contract recount | Each locale has one home, one preface, 45 chapters, 45 solutions, seven ordinary appendices, and one glossary; every chapter has exercises with exact answer anchors |
| E-06 | passed | `rg -n '[\p{Han}]' docs/en -g '*.md'` plus manual classification | Han text occurs only in the generated bilingual glossary and one intentional “equivalent to / 等价于” teaching line; English prose does not require Chinese |
| E-07 | passed | Three English-only routes and sampled solutions | Outcomes, prerequisites, reasoning, failure cases, limitations, and next steps remain understandable without opening Chinese pages |
| E-08 | passed | `git diff --check`, then E-01–E-03 after wording fixes | No whitespace defect and no structural regression |

## 5. Review checklist

### F# and technical correctness

- `not applicable` — R02 separately reviewed language semantics, idiom, null, async, interop, and capstone boundaries.
- `passed` — Samples encountered on the routes come from shared executable references or are explicitly labeled as illustrative; there is no English-only code fork.

### Bilingual independence

- `passed` — English independently carries outcomes, prerequisites, reasoning, warnings, limitations, sources, exercises, and answers.
- `passed` — Translation keys, metadata, heading structure, stable anchors, link targets, shared source references, and exercise identifiers agree with Chinese.
- `passed` — English prose is idiomatic and does not depend on untranslated Chinese terminology.
- `passed` — Parity preserves concepts and evidence boundaries without forcing word-for-word phrasing.

### Sources and versions

- `not applicable` — R01 completed the authoritative-source, version, and external-link audit.

### Site and reader journey

- `passed` — Static contracts cover home, preface, chapters, exercises, solutions, return links, adjacent chapters, and same-page locale mapping.
- `not run` — R05 will exercise rendered search, copy, keyboard focus, responsive layout, and visual accessibility.

## 6. Findings

| ID | Severity | Location | Claim or failure | Required change | Status and retest |
| --- | --- | --- | --- | --- | --- |
| R03-EN-F01 | low | `docs/en/index.md` | “The shortest route in” was grammatical but less natural as a landing-page heading | Use “The quickest start” while preserving `#quick-start` | fixed in `a43ed08`; parity/content/38 tests passed |
| R03-EN-F02 | low | Chapter 38 | “foreign-language path” framed the C# consumer as foreign rather than describing the actual boundary | Use “cross-language path” | fixed in `a43ed08`; route reread passed |
| R03-EN-F03 | low | Chapters/Solutions 43–44 | Reader prose embedded a mutable full-suite test total that had already drifted as coverage grew | Keep the focused result where relevant and describe the complete repository suite without a brittle count | fixed in `a43ed08`; parity/content/38 tests passed |

Open high / medium / low findings: `0 / 0 / 0`.

## 7. Conclusion

| Decision | Value |
| --- | --- |
| Review result | `passed` |
| Release effect | `eligible for R04–R06; not an overall release decision` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Residual risk | Structural checks cannot exhaust prose quality, and future paired edits can reintroduce translation-shaped English. R05 has not yet established the rendered English control and visual experience |
| Follow-up | Keep parity/content mandatory for paired edits; reread all three English routes after broad prose changes; complete R05 in a real browser |

Conclusion: an English-only reader can start at the home page and preface, follow any planned route, and complete chapters, examples, exercises, and solutions without Chinese filling a conceptual gap. The two editions preserve the same claims and limits while allowing natural English phrasing.

### Sign-off

`Codex /root, 2026-08-25 11:17 JST, b2be72904f4f1cbf733b7263efbf1ad5ff6b708d`
