import assert from 'node:assert/strict'
import { join } from 'node:path'
import test from 'node:test'

import {
  findIndexedMatches,
  representativeSearchPairs,
  routeToHtmlPath,
  tokenizeSearchQuery
} from './site-smoke.mjs'

test('maps clean routes and directory routes to static HTML files', () => {
  assert.equal(routeToHtmlPath('/dist', '/'), join('/dist', 'index.html'))
  assert.equal(
    routeToHtmlPath('/dist', '/en/preface/'),
    join('/dist', 'en/preface/index.html')
  )
  assert.equal(
    routeToHtmlPath('/dist', '/zh/part-02/ch-08-discriminated-unions'),
    join('/dist', 'zh/part-02/ch-08-discriminated-unions.html')
  )
})

test('tokenizes punctuation without breaking a Chinese term', () => {
  assert.deepEqual(tokenizeSearchQuery('Discriminated unions, modeled.'), [
    'discriminated',
    'unions',
    'modeled'
  ])
  assert.deepEqual(tokenizeSearchQuery('搜索：可辨识联合'), ['搜索', '可辨识联合'])
})

test('finds documents whose indexed terms satisfy every query token by prefix', () => {
  const index = {
    documentIds: {
      0: '/en/part-02/ch-08-discriminated-unions#overview',
      1: '/en/glossary#discriminated-union'
    },
    index: [
      ['discriminated', { 0: { 0: 1, 1: 1 } }],
      ['unions', { 0: { 0: 1 } }],
      ['union', { 0: { 1: 1 } }]
    ]
  }

  assert.deepEqual(findIndexedMatches(index, 'discriminated unions'), [
    '/en/part-02/ch-08-discriminated-unions#overview'
  ])
  assert.deepEqual(findIndexedMatches(index, 'missing'), [])
})

test('keeps ten bilingual search journeys across the complete book', () => {
  assert.equal(representativeSearchPairs.length, 10)

  for (const pair of representativeSearchPairs) {
    assert.match(pair.en.route, /^\/en\//)
    assert.match(pair.zh.route, /^\/zh\//)
    assert.ok(pair.en.query.length > 0)
    assert.ok(pair.zh.query.length > 0)
  }

  const routes = representativeSearchPairs.flatMap(({ en, zh }) => [
    en.route,
    zh.route
  ])
  for (let part = 1; part <= 7; part += 1) {
    const segment = `/part-${String(part).padStart(2, '0')}/`
    assert.ok(routes.some((route) => route.includes(segment)))
  }
  assert.ok(routes.some((route) => route.includes('/appendices/')))
})
