import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import {
  buildNavigationModel,
  buildNavigationSource,
  checkNavigationOutput,
  writeNavigationOutput
} from './generate-navigation.mjs'

function write(root, relativePath, content) {
  const target = join(root, relativePath)
  mkdirSync(dirname(target), { recursive: true })
  writeFileSync(target, content)
}

function page({ locale, relativePath, kind, title, chapter, part, appendix }) {
  const translationKey = relativePath.replace(/\.md$/, '')
  const chapterFields = chapter === undefined
    ? ''
    : `part: ${part}\nchapter: ${chapter}\nverifiedWith:\n  fsharp: "10"\n  dotnetSdk: "10.0.301"\n`
  const appendixField = appendix === undefined ? '' : `appendix: ${appendix}\n`
  const sources = kind === 'chapter'
    ? `\n  - id: fixture\n    url: https://example.com/source\n    checked: "2026-08-25"`
    : ' []'

  return `---
title: "${title}"
description: "${locale === 'en' ? 'A complete navigation fixture page.' : '一页完整的导航夹具。'}"
translationKey: ${translationKey}
kind: ${kind}
${chapterFields}${appendixField}status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources:${sources}
---

# ${title} {#overview}

${locale === 'en' ? 'Complete fixture content for navigation.' : '用于导航的完整夹具内容。'}
`
}

const appendices = [
  ['A', 'appendices/a-setup.md'],
  ['B', 'appendices/b-syntax-reference.md'],
  ['C', 'appendices/c-collections.md'],
  ['D', 'appendices/d-csharp-migration.md'],
  ['E', 'appendices/e-compiler-errors.md'],
  ['G', 'appendices/g-solutions-guide.md'],
  ['H', 'appendices/h-advanced-index.md']
]

function createFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-navigation-'))
  const docsDir = join(root, 'docs')
  const outputPath = join(root, 'navigation.generated.ts')
  t.after(() => rmSync(root, { recursive: true, force: true }))

  for (const locale of ['en', 'zh']) {
    write(root, `docs/${locale}/index.md`, page({
      locale,
      relativePath: 'index.md',
      kind: 'home',
      title: locale === 'en' ? 'Thinking in F#' : 'F# 思维'
    }))
    write(root, `docs/${locale}/preface/index.md`, page({
      locale,
      relativePath: 'preface/index.md',
      kind: 'preface',
      title: locale === 'en' ? 'Preface' : '前言'
    }))

    for (const chapter of [1, 2]) {
      const number = String(chapter).padStart(2, '0')
      const slug = `ch-${number}-topic-${chapter}.md`
      const chapterTitle = locale === 'en' ? `Chapter ${chapter}: Topic` : `第 ${chapter} 章：主题`
      const solutionTitle = locale === 'en' ? `Chapter ${chapter} Solutions` : `第 ${chapter} 章答案`
      write(root, `docs/${locale}/part-01/${slug}`, page({
        locale,
        relativePath: `part-01/${slug}`,
        kind: 'chapter',
        title: chapterTitle,
        chapter,
        part: 1
      }))
      write(root, `docs/${locale}/solutions/${slug}`, page({
        locale,
        relativePath: `solutions/${slug}`,
        kind: 'solution',
        title: solutionTitle,
        chapter,
        part: 1
      }))
    }

    for (const [letter, relativePath] of appendices) {
      write(root, `docs/${locale}/${relativePath}`, page({
        locale,
        relativePath,
        kind: 'appendix',
        title: locale === 'en' ? `Appendix ${letter}` : `附录 ${letter}`,
        appendix: letter
      }))
    }
    write(root, `docs/${locale}/glossary.md`, page({
      locale,
      relativePath: 'glossary.md',
      kind: 'glossary',
      title: locale === 'en' ? 'Appendix F: Glossary' : '附录 F：术语表'
    }))
  }

  return { root, docsDir, outputPath }
}

test('builds symmetric book, solution, and same-page language navigation', (t) => {
  const { docsDir } = createFixture(t)
  const model = buildNavigationModel({ docsDir, expectedChapters: 2 })
  const englishBook = model.locales.en.sidebar['/en/part-01/']
  const chineseBook = model.locales.zh.sidebar['/zh/part-01/']
  const englishReferences = englishBook.at(-1).items
  const englishSolutions = model.locales.en.sidebar['/en/solutions/'][0].items

  assert.deepEqual(
    englishBook[1].items.map(({ link }) => link),
    ['/en/part-01/ch-01-topic-1', '/en/part-01/ch-02-topic-2']
  )
  assert.deepEqual(
    chineseBook[1].items.map(({ link }) => link),
    ['/zh/part-01/ch-01-topic-1', '/zh/part-01/ch-02-topic-2']
  )
  assert.equal(englishReferences.length, 8)
  assert.equal(englishReferences[5].link, '/en/glossary')
  assert.deepEqual(
    englishSolutions.map(({ link }) => link),
    [
      '/en/appendices/g-solutions-guide',
      '/en/solutions/ch-01-topic-1',
      '/en/solutions/ch-02-topic-2'
    ]
  )
  assert.deepEqual(
    model.routePairs.find(({ translationKey }) => translationKey === 'preface/index'),
    { translationKey: 'preface/index', en: '/en/preface/', zh: '/zh/preface/' }
  )
  assert.equal(model.routePairs.length, 14)
  assert.match(buildNavigationSource({ docsDir, expectedChapters: 2 }), /export const enNavigation/)
})

test('check mode detects a missing or stale generated module', (t) => {
  const { docsDir, outputPath } = createFixture(t)
  const options = { docsDir, outputPath, expectedChapters: 2 }

  assert.deepEqual(checkNavigationOutput(options), [
    'navigation.generated.ts: generated navigation is missing; run pnpm generate:navigation'
  ])
  writeNavigationOutput(options)
  assert.deepEqual(checkNavigationOutput(options), [])
  writeFileSync(outputPath, `${readFileSync(outputPath, 'utf8')}\n// stale\n`)
  assert.deepEqual(checkNavigationOutput(options), [
    'navigation.generated.ts: generated navigation is stale; run pnpm generate:navigation'
  ])
})

test('rejects a locale page without its same-key counterpart', (t) => {
  const { root, docsDir } = createFixture(t)
  rmSync(join(root, 'docs/zh/part-01/ch-02-topic-2.md'))

  assert.throws(
    () => buildNavigationModel({ docsDir, expectedChapters: 2 }),
    /en\/part-01\/ch-02-topic-2\.md: missing Chinese navigation counterpart/
  )
})

test('rejects chapter metadata that breaks the canonical reading order', (t) => {
  const { root, docsDir } = createFixture(t)
  const target = join(root, 'docs/en/part-01/ch-02-topic-2.md')
  writeFileSync(target, readFileSync(target, 'utf8').replace('part: 1', 'part: 2'))

  assert.throws(
    () => buildNavigationModel({ docsDir, expectedChapters: 2 }),
    /English chapter 2: expected part 1, found 2/
  )
})
