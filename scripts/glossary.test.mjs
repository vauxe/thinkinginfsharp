import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import {
  buildGlossaryOutputs,
  checkGlossaryOutputs,
  writeGlossaryOutputs
} from './generate-glossary.mjs'

function write(root, relativePath, content) {
  const target = join(root, relativePath)
  mkdirSync(dirname(target), { recursive: true })
  writeFileSync(target, content)
}

function chapter(locale, title, termIds) {
  return `---
title: "${title}"
description: "A complete chapter fixture."
translationKey: part-01/ch-01-fixture
kind: chapter
part: 1
chapter: 1
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds: []
exerciseIds: []
termIds:
${termIds.map((id) => `  - ${id}`).join('\n')}
sources:
  - id: fixture
    url: https://example.com/source
    checked: "2026-08-25"
---

# ${title} {#overview}

${locale === 'zh' ? '这是完整的中文章节夹具。' : 'This is a complete English chapter fixture.'}
`
}

function createFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-glossary-'))
  const docsDir = join(root, 'docs')
  t.after(() => rmSync(root, { recursive: true, force: true }))

  write(
    root,
    'docs/terminology.json',
    JSON.stringify({
      schemaVersion: 1,
      terms: {
        expression: {
          zh: { preferred: '表达式', definition: '一段求值后产生结果的代码。' },
          en: { preferred: 'expression', definition: 'Code evaluated to produce a result.' }
        },
        value: {
          zh: { preferred: '值', definition: '求值正常完成后得到的结果。' },
          en: { preferred: 'value', definition: 'A result produced by successful evaluation.' }
        }
      }
    }, null, 2)
  )
  write(root, 'docs/en/part-01/ch-01-fixture.md', chapter('en', 'Chapter 1: Fixture', ['expression', 'value']))
  write(root, 'docs/zh/part-01/ch-01-fixture.md', chapter('zh', '第 1 章：夹具', ['expression', 'value']))

  return { root, docsDir }
}

test('builds self-contained bilingual entries with stable anchors and first-introduction links', (t) => {
  const { docsDir } = createFixture(t)
  const outputs = buildGlossaryOutputs({ docsDir })
  const english = outputs.get(join(docsDir, 'en/glossary.md'))
  const chinese = outputs.get(join(docsDir, 'zh/glossary.md'))

  assert.match(english, /### expression · 表达式 \{#expression\}/)
  assert.match(english, /Code evaluated to produce a result\./)
  assert.match(english, /\.\/part-01\/ch-01-fixture#overview/)
  assert.match(chinese, /### 表达式 · expression \{#expression\}/)
  assert.match(chinese, /一段求值后产生结果的代码。/)
  assert.equal((english.match(/^### /gm) ?? []).length, 2)
  assert.equal((chinese.match(/^### /gm) ?? []).length, 2)
})

test('check mode detects stale output and accepts exactly generated pages', (t) => {
  const { docsDir } = createFixture(t)

  assert.deepEqual(checkGlossaryOutputs({ docsDir }), [
    'en/glossary.md: generated glossary is missing; run pnpm generate:glossary',
    'zh/glossary.md: generated glossary is missing; run pnpm generate:glossary'
  ])

  writeGlossaryOutputs({ docsDir })
  assert.deepEqual(checkGlossaryOutputs({ docsDir }), [])
  assert.match(readFileSync(join(docsDir, 'en/glossary.md'), 'utf8'), /\{#value\}/)
})

test('rejects a catalog term without a bilingual first-introduction page', (t) => {
  const { docsDir } = createFixture(t)
  const catalogPath = join(docsDir, 'terminology.json')
  const catalog = JSON.parse(readFileSync(catalogPath, 'utf8'))
  catalog.terms.orphan = {
    zh: { preferred: '孤立术语', definition: '没有正文首现位置的术语。' },
    en: { preferred: 'orphan term', definition: 'A term with no first-introduction page.' }
  }
  writeFileSync(catalogPath, JSON.stringify(catalog, null, 2))

  assert.throws(
    () => buildGlossaryOutputs({ docsDir }),
    /orphan: no English first-introduction page declares this term id/
  )
})
