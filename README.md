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

The complete check also needs the .NET 10 SDK. It tests the reading theme, verifies bilingual structure, executes every registered example against its expected output or diagnostic, and builds the VitePress site.

## Repository layout

```text
docs/                         Book content and site configuration
examples/                     Runnable example sources referenced by the book
scripts/                      Focused book, example, and theme checks
.github/workflows/            GitHub Pages deployment
```

Book pages show focused excerpts; `examples/` contains their complete runnable form. The checks keep those excerpts synchronized with the scripts and verify exact output or the documented compiler diagnostic.
