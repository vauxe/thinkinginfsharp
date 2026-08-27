# Thinking in F#

English | [简体中文](README.zh-CN.md)

A bilingual static learning site for F# beginners. English is the default edition, Chinese has its own path, and readers can switch languages from the site navigation.

- [Read online](https://vauxe.github.io/thinkinginfsharp/)
- [English manuscript](docs/en/index.md)
- [Chinese manuscript](docs/zh/index.md)

## Run locally

The site needs Node.js 24 or later; npm is included with Node.js.

```console
npm ci
npm run dev
```

Before committing, run:

```console
npm run check
```

The complete check also needs the .NET 10 SDK. It tests the reading theme, verifies bilingual structure, executes the focused fixtures against their expected output or diagnostic, and builds the VitePress site.

## Repository layout

```text
docs/                         Book content and site configuration
examples/                     Focused fixtures that need repository context
scripts/                      Focused book, example, and theme checks
.github/workflows/            GitHub Pages deployment
```

Most book examples stay in the page. `examples/` is reserved for integrated, multi-file, interop, diagnostic, or effectful cases that need repository-level verification.
