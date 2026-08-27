import assert from 'node:assert/strict'
import test from 'node:test'

import {
  checkDocumentedSnippets,
  runManifestEntries
} from './check-examples.mjs'

test('documented snippets must come from their runnable script', () => {
  const page = [
    '```fsharp:line-numbers [lesson.fsx]',
    'let answer = 42',
    '```'
  ].join('\n')

  assert.deepEqual(
    checkDocumentedSnippets({
      pages: new Map([['docs/en/lesson.md', page]]),
      scripts: new Map([['lesson.fsx', 'let answer = 42\nprintfn "%d" answer\n']])
    }),
    []
  )

  assert.match(
    checkDocumentedSnippets({
      pages: new Map([['docs/en/lesson.md', page]]),
      scripts: new Map([['lesson.fsx', 'let answer = 41\n']])
    })[0],
    /does not match lesson\.fsx/
  )
})

test('snippet comparison ignores indentation added by an enclosing module', () => {
  const page = [
    '```fsharp:line-numbers [lesson.fsx]',
    'let answer = 42',
    'printfn "%d" answer',
    '```'
  ].join('\n')

  const script = [
    'module Lesson =',
    '    let answer = 42',
    '    printfn "%d" answer',
    ''
  ].join('\n')

  assert.deepEqual(
    checkDocumentedSnippets({
      pages: new Map([['docs/en/lesson.md', page]]),
      scripts: new Map([['lesson.fsx', script]])
    }),
    []
  )
})

test('a complete script may be referenced by a run command without an excerpt', () => {
  assert.deepEqual(
    checkDocumentedSnippets({
      pages: new Map([[
        'docs/en/checkpoint.md',
        'dotnet fsi --exec examples/capstone/part-01/BookingBasics.fsx'
      ]]),
      scripts: new Map([['BookingBasics.fsx', 'printfn "checkpoint"\n']])
    }),
    []
  )
})

test('runnable and expected-error entries enforce their observable contracts', () => {
  const entries = [
    {
      id: 'working-script',
      kind: 'script',
      path: 'examples/scripts/working.fsx',
      expectedOutput: ['first', 'second']
    },
    {
      id: 'expected-error',
      kind: 'expected-error',
      path: 'examples/expected-errors/failure.fsx',
      diagnostics: ['FS0001']
    }
  ]

  const calls = []
  const runner = (_command, arguments_) => {
    calls.push(arguments_)
    if (arguments_.at(-1).endsWith('failure.fsx')) {
      return { status: 1, stdout: '', stderr: 'error FS0001: type mismatch' }
    }
    return { status: 0, stdout: 'first\nsecond\n', stderr: '' }
  }

  assert.deepEqual(runManifestEntries({ entries, runner, repoRoot: '/repo' }), [])
  assert.equal(calls.length, 2)
  assert.ok(calls.every((arguments_) => arguments_.includes('--warnaserror+')))

  const failures = runManifestEntries({
    entries: [entries[0]],
    repoRoot: '/repo',
    runner: () => ({ status: 0, stdout: 'second\nfirst\n', stderr: '' })
  })

  assert.match(failures[0], /output differs/)
})

test('expected-error projects bypass successful incremental outputs', () => {
  const calls = []
  const errors = runManifestEntries({
    repoRoot: '/repo',
    entries: [
      {
        id: 'compiler-warning',
        kind: 'expected-error',
        path: 'examples/expected-errors/compiler-warning/Warning.fsproj',
        diagnostics: ['FS3569']
      }
    ],
    runner: (_command, arguments_) => {
      calls.push(arguments_)
      return { status: 1, stdout: '', stderr: 'error FS3569: recursive call is not in tail position' }
    }
  })

  assert.deepEqual(errors, [])
  assert.ok(calls[0].includes('--no-incremental'))
})

test('project entries build before optional execution', () => {
  const calls = []
  const runner = (_command, arguments_) => {
    calls.push(arguments_)
    if (arguments_[0] === 'run') return { status: 0, stdout: 'project-ok\n', stderr: '' }
    return { status: 0, stdout: '', stderr: '' }
  }

  const errors = runManifestEntries({
    repoRoot: '/repo',
    runner,
    entries: [
      {
        id: 'sample-project',
        kind: 'project',
        path: 'examples/sample/Sample.fsproj',
        runArguments: ['--demo'],
        expectedOutput: ['project-ok']
      }
    ]
  })

  assert.deepEqual(errors, [])
  assert.equal(calls.length, 2)
  assert.equal(calls[0][0], 'build')
  assert.equal(calls[1][0], 'run')
  assert.ok(calls[1].includes('--no-build'))
  assert.deepEqual(calls[1].slice(-2), ['--', '--demo'])
})
