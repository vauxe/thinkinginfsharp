import assert from 'node:assert/strict'
import test from 'node:test'

import {
  normalizeSiteBase,
  stripSiteBase,
  withSiteBase
} from './lib/site-base.mjs'

test('normalizes the optional deployment base', () => {
  assert.equal(normalizeSiteBase(), '/')
  assert.equal(normalizeSiteBase(''), '/')
  assert.equal(normalizeSiteBase('/thinkinginfsharp/'), '/thinkinginfsharp/')
  assert.throws(() => normalizeSiteBase('thinkinginfsharp/'), /start and end with/)
  assert.throws(() => normalizeSiteBase('/thinkinginfsharp'), /start and end with/)
})

test('adds and removes a project-site base without changing book routes', () => {
  const base = '/thinkinginfsharp/'

  assert.equal(withSiteBase(base, '/'), base)
  assert.equal(withSiteBase(base, '/en/'), '/thinkinginfsharp/en/')
  assert.equal(
    stripSiteBase(base, '/thinkinginfsharp/en/part-01/ch-01-first-session'),
    '/en/part-01/ch-01-first-session'
  )
  assert.equal(stripSiteBase(base, '/thinkinginfsharp'), '/')
  assert.equal(stripSiteBase(base, '/en/'), undefined)
})

test('keeps root-hosted routes unchanged', () => {
  assert.equal(withSiteBase('/', '/zh/'), '/zh/')
  assert.equal(stripSiteBase('/', '/zh/'), '/zh/')
})
