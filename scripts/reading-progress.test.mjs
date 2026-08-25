import assert from 'node:assert/strict'
import test from 'node:test'

import { readingProgress } from '../docs/.vitepress/theme/reading-progress.mjs'

test('readingProgress starts at zero', () => {
  assert.equal(readingProgress(0, 5000, 1000), 0)
})

test('readingProgress reports the readable distance rather than total document height', () => {
  assert.equal(readingProgress(2000, 5000, 1000), 0.5)
})

test('readingProgress clamps browser overscroll at both ends', () => {
  assert.equal(readingProgress(-100, 5000, 1000), 0)
  assert.equal(readingProgress(5000, 5000, 1000), 1)
})

test('readingProgress stays hidden when the page does not scroll', () => {
  assert.equal(readingProgress(0, 800, 1000), 0)
})
