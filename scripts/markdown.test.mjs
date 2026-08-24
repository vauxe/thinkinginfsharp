import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  collectMarkdownPages,
  parseMarkdownSource
} from './lib/markdown.mjs'

function temporaryDocs(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-markdown-'))
  const docsDir = join(root, 'docs')
  mkdirSync(join(docsDir, 'en'), { recursive: true })
  t.after(() => rmSync(root, { recursive: true, force: true }))
  return docsDir
}

test('parses frontmatter without mistaking an indented block line for its delimiter', () => {
  const source = `---
title: Page
description: |
  First description line.
  ---
translationKey: index
kind: home
status: draft
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---

# Page {#overview}
`

  const parsed = parseMarkdownSource(source, 'en/index.md')

  assert.equal(parsed.frontmatter.translationKey, 'index')
  assert.deepEqual(parsed.errors, [])
})

test('rejects YAML aliases at the frontmatter trust boundary', () => {
  const parsed = parseMarkdownSource(
    '---\nsource: &source [one, two]\ncopy: *source\n---\n\n# Page {#overview}\n',
    'en/index.md'
  )

  assert.ok(parsed.errors.some((error) => error.includes('alias count')))
})

test('extracts stable structure while excluding fenced code from prose checks', () => {
  const parsed = parseMarkdownSource(
    `---
title: Page
description: Page description
---

# Page {#overview}

[Home](/)

<img src="image.png" onerror="alert(1)">

<<< @/../examples/scripts/example.fsx#sample

` + '```fsharp\n// TODO is code, not placeholder prose\n```',
    'en/index.md'
  )

  assert.deepEqual(parsed.anchors, ['overview'])
  assert.deepEqual(parsed.links.map(({ target }) => target), ['/', 'image.png'])
  assert.deepEqual(
    parsed.codeReferences.map(({ reference }) => reference),
    ['@/../examples/scripts/example.fsx#sample']
  )
  assert.deepEqual(parsed.placeholderFindings, [])
  assert.equal(parsed.unsafeHtmlFindings.length, 1)
})

test('rejects oversized Markdown before parsing it', (t) => {
  const docsDir = temporaryDocs(t)
  writeFileSync(
    join(docsDir, 'en', 'oversized.md'),
    'x'.repeat(2 * 1024 * 1024 + 1)
  )

  const [page] = collectMarkdownPages(docsDir, { localesOnly: true })

  assert.ok(page.errors.some((error) => error.includes('2 MiB safety limit')))
})
