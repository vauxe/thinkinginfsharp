# Thinking in F#

English | [简体中文](README.zh-CN.md)

A bilingual static learning site for F# beginners. English is the default edition, Chinese has its own path, and readers can switch languages from the site navigation.

- [Read online](https://vauxe.github.io/thinkinginfsharp/)
- [English manuscript](docs/en/index.md)
- [Chinese manuscript](docs/zh/index.md)

## Run locally

You only need Node.js 24 or later; npm is included with Node.js.

```console
npm ci
npm run dev
```

Before committing, run:

```console
npm run check
```

`npm run check` tests the reading theme, verifies that the English and Chinese pages, heading anchors, and code blocks correspond, then builds the VitePress site.

## Editorial style

Lead with what a concept is, what it guarantees, and what the reader should do. Put boundary conditions in a separate sentence and express one limitation at a time. Keep direct negation when it states an exact type invariant, failure state, safety rule, or contrast whose positive form would be less precise.

## Repository layout

```text
docs/                         Book content and site configuration
scripts/check-book.mjs        Minimal bilingual consistency check
.github/workflows/            GitHub Pages deployment
```

Code examples live directly in the book pages. The Markdown manuscript and its embedded examples are the source of truth; the Node-based checks validate that self-contained book structure.
