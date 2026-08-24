import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import {
  assertPageFrontmatter,
  sharedCodeReferenceTarget,
  validatePageFrontmatter,
  validateSharedCodeReference,
  validateTerminology
} from './lib/content-contract.mjs'

const homePage = {
  title: 'Thinking in F#',
  description: 'Learn F# from the language itself.',
  translationKey: 'index',
  kind: 'home',
  status: 'draft',
  exampleIds: [],
  exerciseIds: [],
  termIds: [],
  sources: []
}

test('accepts a complete locale-home contract', () => {
  assert.doesNotThrow(() => assertPageFrontmatter(homePage, 'en/index.md'))
  assert.deepEqual(validatePageFrontmatter(homePage, 'en/index.md'), [])
})

test('reports every missing required field with its page path', () => {
  const errors = validatePageFrontmatter(
    { title: 'Incomplete', translationKey: 'index' },
    'zh/index.md'
  )

  assert.ok(errors.length > 1)
  assert.ok(errors.every((error) => error.startsWith('zh/index.md:')))
  assert.ok(errors.some((error) => error.includes('description')))
  assert.ok(errors.some((error) => error.includes('sources')))
  assert.throws(
    () => assertPageFrontmatter({ title: 'Incomplete' }, 'zh/index.md'),
    /zh\/index\.md: translationKey/
  )
})

test('enforces path identity and chapter-only fields', () => {
  const chapter = {
    ...homePage,
    translationKey: 'part-02/ch-08-discriminated-unions',
    kind: 'chapter',
    part: 2,
    chapter: 8,
    verifiedWith: { fsharp: '10', dotnetSdk: '10.0.301' },
    sources: [
      {
        id: 'fsharp-discriminated-unions',
        url: 'https://learn.microsoft.com/dotnet/fsharp/language-reference/discriminated-unions',
        checked: '2026-08-24'
      }
    ]
  }

  assert.deepEqual(
    validatePageFrontmatter(
      chapter,
      'en/part-02/ch-08-discriminated-unions.md'
    ),
    []
  )

  const errors = validatePageFrontmatter(
    { ...chapter, translationKey: 'part-02/wrong', chapter: 0 },
    'zh/part-02/ch-08-discriminated-unions.md'
  )

  assert.ok(errors.some((error) => error.includes('must match the locale-relative path')))
  assert.ok(errors.some((error) => error.includes('chapter must be an integer from 1 to 45')))
})

test('rejects duplicate identifiers and unverifiable sources', () => {
  const errors = validatePageFrontmatter(
    {
      ...homePage,
      exampleIds: ['ch01-example-01', 'ch01-example-01'],
      sources: [
        {
          id: 'not stable!',
          url: 'http://example.com/reference',
          checked: '2026-02-30'
        }
      ]
    },
    'en/index.md'
  )

  assert.ok(errors.some((error) => error.includes('exampleIds contains duplicate')))
  assert.ok(errors.some((error) => error.includes('sources[0].id')))
  assert.ok(errors.some((error) => error.includes('must use https')))
  assert.ok(errors.some((error) => error.includes('valid YYYY-MM-DD')))
})

test('allows only normalized snippet references inside the shared examples tree', () => {
  assert.deepEqual(
    validateSharedCodeReference(
      '@/../examples/scripts/ch01-first-session.fsx#binding{1,3 fsharp:line-numbers}'
    ),
    []
  )
  assert.deepEqual(
    sharedCodeReferenceTarget(
      '@/../examples/scripts/ch01-first-session.fsx#binding{1,3 fsharp:line-numbers}'
    ),
    {
      path: 'scripts/ch01-first-session.fsx',
      region: 'binding'
    }
  )

  for (const reference of [
    '@/local-copy.fsx',
    '@/../examples/../private.fsx',
    '../../examples/scripts/ch01.fsx',
    'https://example.com/sample.fsx'
  ]) {
    assert.ok(
      validateSharedCodeReference(reference).length > 0,
      `expected ${reference} to be rejected`
    )
  }
})

test('requires a self-contained bilingual definition for every term', () => {
  const valid = {
    schemaVersion: 1,
    terms: {
      binding: {
        zh: { preferred: '绑定', definition: '名称与值之间的关联。' },
        en: { preferred: 'binding', definition: 'An association between a name and a value.' }
      }
    }
  }

  assert.deepEqual(validateTerminology(valid, 'docs/terminology.json'), [])

  const errors = validateTerminology(
    {
      schemaVersion: 2,
      terms: {
        'Not Stable': {
          zh: { preferred: '', definition: '定义' }
        }
      }
    },
    'docs/terminology.json'
  )

  assert.ok(errors.some((error) => error.includes('schemaVersion')))
  assert.ok(errors.some((error) => error.includes('Not Stable')))
  assert.ok(errors.some((error) => error.includes('.zh.preferred')))
  assert.ok(errors.some((error) => error.includes('.en must be an object')))
})

test('the repository terminology catalog satisfies the contract', () => {
  const path = new URL('../docs/terminology.json', import.meta.url)
  const terminology = JSON.parse(readFileSync(path, 'utf8'))

  assert.deepEqual(
    validateTerminology(terminology, 'docs/terminology.json'),
    []
  )
})
