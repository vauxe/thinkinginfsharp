import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { stringify } from 'yaml'

import { checkContent } from './check-content.mjs'
import { checkParity } from './check-parity.mjs'

const parityCommand = fileURLToPath(
  new URL('./check-parity.mjs', import.meta.url)
)

function createFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-content-'))
  const docsDir = join(root, 'docs')
  mkdirSync(docsDir, { recursive: true })
  t.after(() => rmSync(root, { recursive: true, force: true }))
  return { root, docsDir }
}

function write(root, relativePath, content) {
  const filePath = join(root, relativePath)
  mkdirSync(dirname(filePath), { recursive: true })
  writeFileSync(filePath, content)
}

function metadata(overrides = {}) {
  return {
    title: 'Page title',
    description: 'A complete page used by the content checker.',
    translationKey: 'index',
    kind: 'home',
    status: 'draft',
    exampleIds: [],
    exerciseIds: [],
    termIds: [],
    sources: [],
    ...overrides
  }
}

function markdown(frontmatter, body) {
  return `---\n${stringify(frontmatter).trim()}\n---\n\n${body.trim()}\n`
}

function validBody(title, prose) {
  return `
# ${title} {#overview}

${prose}

## Capabilities {#capabilities}

[Return to the overview](#overview). This second paragraph provides enough
substantive prose to represent a real page rather than a generated placeholder.
`
}

function writeTerminology(docsDir) {
  write(
    docsDir,
    'terminology.json',
    JSON.stringify({
      schemaVersion: 1,
      terms: {
        expression: {
          zh: { preferred: '表达式', definition: '一段求值后产生结果的代码。' },
          en: { preferred: 'expression', definition: 'Code that is evaluated to produce a result.' }
        }
      }
    })
  )
}

test('reports a missing translation with the source page path', (t) => {
  const { docsDir } = createFixture(t)
  write(
    docsDir,
    'zh/index.md',
    markdown(
      metadata(),
      validBody('中文首页', '这是一段足够完整的中文说明，用于验证缺失英文页面时的错误路径。')
    )
  )

  const errors = checkParity({ docsDir })

  assert.ok(errors.some((error) => error.includes('zh/index.md')))
  assert.ok(errors.some((error) => error.includes('missing English translation')))
})

test('reports paired metadata and structural mismatches', (t) => {
  const { docsDir } = createFixture(t)
  write(
    docsDir,
    'zh/index.md',
    markdown(metadata(), validBody('中文首页', '中文页面包含与英文页面相同的结构和学习目标。'))
  )
  write(
    docsDir,
    'en/index.md',
    markdown(
      metadata({ exerciseIds: ['ch01-exercise-01'] }),
      validBody('English home', 'The English page follows the same structure but declares a different exercise.')
        .replace('{#capabilities}', '{#different-anchor}')
    )
  )

  const errors = checkParity({ docsDir })

  assert.ok(errors.some((error) => error.includes('exerciseIds')))
  assert.ok(errors.some((error) => error.includes('heading anchors')))
  assert.ok(errors.every((error) => /(?:zh|en)\/index\.md/.test(error)))
})

test('reports paired link-target and shared-code-reference drift', (t) => {
  const { docsDir } = createFixture(t)
  const zhBody = `${validBody(
    '中文首页',
    '中文页面包含与英文页面相同的结构，但夹具故意让链接和代码引用发生漂移。'
  )}

[下一页](/zh/right-target)

<<< @/../examples/right.fsx#sample{fsharp} [中文标题]
`
  const enBody = `${validBody(
    'English home',
    'The page has the same heading structure, but this fixture deliberately drifts its link and code reference.'
  )}

[Next](/en/wrong-target)

<<< @/../examples/wrong.fsx#sample{fsharp} [English title]
`

  write(docsDir, 'zh/index.md', markdown(metadata(), zhBody))
  write(docsDir, 'en/index.md', markdown(metadata(), enBody))

  const errors = checkParity({ docsDir })

  assert.ok(errors.some((error) => error.includes('link targets differ')))
  assert.ok(errors.some((error) => error.includes('code references differ')))
})

test('finds placeholders, broken links, duplicate anchors, unknown terms, and copied code', (t) => {
  const { docsDir } = createFixture(t)
  writeTerminology(docsDir)
  write(
    docsDir,
    'zh/index.md',
    markdown(
      metadata({ termIds: ['unknown-term'] }),
      `
# 页面 {#duplicate}

TODO：稍后补充。这段可见文字故意表示尚未完成的页面。

## 第二节 {#duplicate}

[失效链接](/zh/missing)
[不安全链接](http://example.com)

<img src="x" onerror="alert(1)">

<<< @/copied-example.fsx
`
    )
  )

  const errors = checkContent({ docsDir })
  const expected = [
    'placeholder text',
    'duplicate anchor',
    'unknown term id',
    'internal link target does not exist',
    'code reference must begin',
    'external links must use https',
    'unsafe active HTML'
  ]

  for (const phrase of expected) {
    assert.ok(errors.some((error) => error.includes(phrase)), phrase)
  }
  assert.ok(errors.every((error) => error.startsWith('zh/index.md:')))
})

test('accepts paired pages, stable anchors, valid links, and one shared snippet', (t) => {
  const { root, docsDir } = createFixture(t)
  writeTerminology(docsDir)
  write(root, 'examples/scripts/example.fsx', '// #region sample\nlet answer = 42\n// #endregion\n')
  write(
    docsDir,
    'index.md',
    markdown(
      {
        layout: 'page',
        title: 'Language choice',
        description: 'Choose a language.'
      },
      '# Language choice'
    )
  )

  const sharedSnippet =
    '@/../examples/scripts/example.fsx#sample{2 fsharp:line-numbers}'
  const zh = validBody(
    '中文首页',
    '这是一段完整的中文说明，为只阅读中文的读者解释页面目的和学习路径。'
  )
  const en = validBody(
    'English home',
    'This complete English introduction explains the page purpose and learning path without relying on Chinese context.'
  )

  write(
    docsDir,
    'zh/index.md',
    markdown(
      metadata({ termIds: ['expression'] }),
      `${zh}\n\n[中文首页](/zh/). [Language choice](/). [Local API](http://localhost:5000/health).\n\n<<< ${sharedSnippet} [中文标题]\n\n` + '```fsharp\n// TODO is code, not placeholder prose\n```'
    )
  )
  write(
    docsDir,
    'en/index.md',
    markdown(
      metadata({ termIds: ['expression'] }),
      `${en}\n\n[English home](/en/). [Language choice](/). [Local API](http://localhost:5000/health).\n\n<<< ${sharedSnippet} [English title]\n`
    )
  )

  assert.deepEqual(checkParity({ docsDir }), [])
  assert.deepEqual(checkContent({ docsDir }), [])
})

test('the parity CLI returns a non-zero status and prints file paths', (t) => {
  const { docsDir } = createFixture(t)
  write(
    docsDir,
    'zh/index.md',
    markdown(metadata(), validBody('中文首页', '这个夹具故意没有英文页面，用来验证命令行失败状态。'))
  )

  const result = spawnSync(
    process.execPath,
    [parityCommand, '--docs', docsDir],
    { encoding: 'utf8' }
  )

  assert.notEqual(result.status, 0)
  assert.match(result.stderr, /zh\/index\.md/)
})
