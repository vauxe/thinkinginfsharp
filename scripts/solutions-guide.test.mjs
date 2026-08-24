import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import {
  buildSolutionsGuideOutputs,
  checkSolutionsGuideOutputs,
  writeSolutionsGuideOutputs
} from './generate-solutions-guide.mjs'

function write(root, relativePath, content) {
  const target = join(root, relativePath)
  mkdirSync(dirname(target), { recursive: true })
  writeFileSync(target, content)
}

function metadata({ locale, chapter, kind, slug, exerciseIds }) {
  const solution = kind === 'solution'
  const title = locale === 'en'
    ? `Chapter ${chapter}${solution ? ' Solutions' : `: Topic ${chapter}`}`
    : `第 ${chapter} 章${solution ? '解答' : `：主题 ${chapter}`}`
  return `---
title: "${title}"
description: "${locale === 'en' ? 'A complete review focus.' : '一段完整的评审重点。'}"
translationKey: ${solution ? 'solutions' : 'part-01'}/${slug}
kind: ${kind}
part: 1
chapter: ${chapter}
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds: []
exerciseIds:
${exerciseIds.map((id) => `  - ${id}`).join('\n')}
termIds: []
sources:
  - id: fixture
    url: https://example.com/source
    checked: "2026-08-25"
---`
}

function page({ locale, chapter, kind, slug, exerciseIds }) {
  const solution = kind === 'solution'
  const title = locale === 'en'
    ? `Chapter ${chapter}${solution ? ' Solutions' : `: Topic ${chapter}`}`
    : `第 ${chapter} 章${solution ? '解答' : `：主题 ${chapter}`}`
  const links = solution
    ? `[${locale === 'en' ? 'Return to chapter' : '返回本章'}](../part-01/${slug}).`
    : `[${locale === 'en' ? 'Read solutions' : '阅读解答'}](../solutions/${slug}).`
  const exercises = exerciseIds.map((id, index) => {
    const suffix = id.match(/exercise-(\d+)$/)[1]
    return `## ${locale === 'en' ? 'Exercise' : '练习'} ${index + 1} {#exercise-${suffix}}\n\n${locale === 'en' ? 'Complete reasoning.' : '完整推理。'}`
  }).join('\n\n')

  return `${metadata({ locale, chapter, kind, slug, exerciseIds })}

# ${title} {#overview}

${links}

${exercises}
`
}

function createFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-solutions-'))
  const docsDir = join(root, 'docs')
  t.after(() => rmSync(root, { recursive: true, force: true }))

  for (const locale of ['en', 'zh']) {
    for (const chapter of [1, 2]) {
      const number = String(chapter).padStart(2, '0')
      const slug = `ch-${number}-topic-${chapter}`
      const exerciseIds = [`ch${number}-exercise-01`, `ch${number}-exercise-02`]
      write(root, `docs/${locale}/part-01/${slug}.md`, page({ locale, chapter, kind: 'chapter', slug, exerciseIds }))
      write(root, `docs/${locale}/solutions/${slug}.md`, page({ locale, chapter, kind: 'solution', slug, exerciseIds }))
    }
  }

  return { root, docsDir }
}

test('builds a bilingual index whose exercise links reach every answer anchor', (t) => {
  const { docsDir } = createFixture(t)
  const outputs = buildSolutionsGuideOutputs({ docsDir, expectedChapters: 2 })
  const english = outputs.get(join(docsDir, 'en/appendices/g-solutions-guide.md'))
  const chinese = outputs.get(join(docsDir, 'zh/appendices/g-solutions-guide.md'))

  assert.match(english, /### Chapter 1: Topic 1 \{#chapter-01\}/)
  assert.match(english, /\.\.\/solutions\/ch-01-topic-1#exercise-01/)
  assert.match(english, /Review focus/)
  assert.match(chinese, /### 第 1 章：主题 1 \{#chapter-01\}/)
  assert.match(chinese, /\.\.\/solutions\/ch-02-topic-2#exercise-02/)
  assert.equal((english.match(/^### /gm) ?? []).length, 2)
  assert.equal((chinese.match(/^### /gm) ?? []).length, 2)
})

test('check mode detects missing output and accepts deterministic generated pages', (t) => {
  const { docsDir } = createFixture(t)
  const options = { docsDir, expectedChapters: 2 }

  assert.equal(checkSolutionsGuideOutputs(options).length, 2)
  writeSolutionsGuideOutputs(options)
  assert.deepEqual(checkSolutionsGuideOutputs(options), [])
})

test('rejects exercise ids that do not match between a chapter and its solution', (t) => {
  const { root, docsDir } = createFixture(t)
  const slug = 'ch-02-topic-2'
  write(
    root,
    `docs/en/solutions/${slug}.md`,
    page({
      locale: 'en',
      chapter: 2,
      kind: 'solution',
      slug,
      exerciseIds: ['ch02-exercise-01', 'ch02-exercise-03']
    })
  )

  assert.throws(
    () => buildSolutionsGuideOutputs({ docsDir, expectedChapters: 2 }),
    /English chapter 2: exerciseIds differ between chapter and solution/
  )
})

test('rejects an answer anchor that has no exercise id', (t) => {
  const { root, docsDir } = createFixture(t)
  const slug = 'ch-01-topic-1'
  const exerciseIds = ['ch01-exercise-01', 'ch01-exercise-02']
  write(
    root,
    `docs/en/solutions/${slug}.md`,
    `${page({ locale: 'en', chapter: 1, kind: 'solution', slug, exerciseIds })}\n## Bonus {#exercise-99}\n\nOrphan answer.\n`
  )

  assert.throws(
    () => buildSolutionsGuideOutputs({ docsDir, expectedChapters: 2 }),
    /English chapter 1: solution contains orphan exercise anchor #exercise-99/
  )
})
