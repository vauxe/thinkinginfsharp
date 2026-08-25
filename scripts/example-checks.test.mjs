import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import { checkExamples, runExampleChecks } from './check-examples.mjs'

const checkerCommand = fileURLToPath(
  new URL('./check-examples.mjs', import.meta.url)
)

function createFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-examples-'))
  t.after(() => rmSync(root, { recursive: true, force: true }))
  return root
}

function write(root, relativePath, content = '') {
  const filePath = join(root, relativePath)
  mkdirSync(dirname(filePath), { recursive: true })
  writeFileSync(filePath, content)
}

function writeManifest(root, entries) {
  write(
    root,
    'examples/manifest.json',
    `${JSON.stringify({ schemaVersion: 1, entries }, null, 2)}\n`
  )
}

function project({
  target = 'net10.0',
  testProject = false,
  executable = false,
  source
}) {
  return `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>${target}</TargetFramework>
    ${testProject ? '<IsTestProject>true</IsTestProject>' : ''}
    ${executable ? '<OutputType>Exe</OutputType>' : ''}
  </PropertyGroup>
  <ItemGroup><Compile Include="${source}" /></ItemGroup>
</Project>\n`
}

function writeCompleteFixture(root) {
  const projectPaths = [
    'examples/compiled/Compiled.fsproj',
    'tests/Tests/Tests.fsproj',
    'tests/Contracts/Contracts.fsproj',
    'examples/unity/Plugin.fsproj'
  ]
  write(
    root,
    'ThinkingInFSharp.slnx',
    `<Solution>\n${projectPaths.map((path) => `  <Project Path="${path}" />`).join('\n')}\n</Solution>\n`
  )

  write(root, projectPaths[0], project({ executable: true, source: 'Library.fs' }))
  write(root, 'examples/compiled/Library.fs', 'module Compiled\nlet answer = 42\n')
  write(root, projectPaths[1], project({ testProject: true, source: 'Tests.fs' }))
  write(root, 'tests/Tests/Tests.fs', 'module Tests\n')
  write(root, projectPaths[2], project({ testProject: true, source: 'Contracts.fs' }))
  write(root, 'tests/Contracts/Contracts.fs', 'module Contracts\n')
  write(
    root,
    projectPaths[3],
    project({ target: 'netstandard2.1', source: 'Plugin.fs' })
  )
  write(root, 'examples/unity/Plugin.fs', 'module Plugin\n')
  write(
    root,
    'examples/unity/README.md',
    'Copy the plugin assembly and FSharp.Core into the Unity project.\n'
  )
  write(root, 'examples/scripts/hello.fsx', 'printfn "answer: 42"\n')
  write(
    root,
    'examples/expected-errors/type-mismatch.fsx',
    'let answer: string = 42\n'
  )
  write(root, 'examples/illustrative/pseudocode.fs', 'module Pseudocode\n')
  write(root, 'docs/en/illustration.md', '# Deliberately incomplete example\n')

  writeManifest(root, [
    {
      id: 'compiled-example',
      kind: 'compile',
      path: projectPaths[0],
      sources: ['examples/compiled/Library.fs'],
      runArguments: ['--verify-only'],
      expectedOutput: ['verification passed']
    },
    {
      id: 'contract-tests',
      kind: 'contract',
      path: projectPaths[2],
      sources: ['tests/Contracts/Contracts.fs']
    },
    {
      id: 'expected-type-error',
      kind: 'expected-error',
      path: 'examples/expected-errors/type-mismatch.fsx',
      diagnostics: ['FS0001']
    },
    {
      id: 'hello-script',
      kind: 'script',
      path: 'examples/scripts/hello.fsx',
      expectedOutput: ['answer: 42']
    },
    {
      id: 'intentional-pseudocode',
      kind: 'illustrative',
      path: 'examples/illustrative/pseudocode.fs',
      reason: 'The surrounding page explicitly explains the omitted host API.',
      documentedIn: ['docs/en/illustration.md']
    },
    {
      id: 'unit-tests',
      kind: 'test',
      path: projectPaths[1],
      sources: ['tests/Tests/Tests.fs']
    },
    {
      id: 'unity-plugin',
      kind: 'unity-plugin',
      path: projectPaths[3],
      sources: ['examples/unity/Plugin.fs'],
      documentation: 'examples/unity/README.md'
    }
  ])
}

test('supports all seven kinds and executes their required checks', (t) => {
  const root = createFixture(t)
  writeCompleteFixture(root)
  const calls = []

  const runner = (command, args) => {
    calls.push([command, ...args])
    const invocation = args.join(' ')
    if (invocation.includes('type-mismatch.fsx')) {
      return { status: 1, stdout: '', stderr: 'error FS0001: type mismatch' }
    }
    if (invocation.includes('hello.fsx')) {
      return { status: 0, stdout: 'answer: 42\n', stderr: '' }
    }
    if (invocation.includes('--verify-only')) {
      return { status: 0, stdout: 'verification passed\n', stderr: '' }
    }
    return { status: 0, stdout: '', stderr: '' }
  }

  assert.deepEqual(runExampleChecks({ repoRoot: root, runner }), [])
  assert.ok(calls.some((call) => call.includes('restore')))
  assert.ok(calls.some((call) => call.includes('build')))
  assert.ok(calls.some((call) => call.includes('test')))
  assert.ok(
    calls.some((call) => call.includes('run') && call.includes('--verify-only'))
  )
  assert.ok(
    calls.some((call) => call.some((part) => part.endsWith('/hello.fsx')))
  )
  assert.ok(
    calls.some((call) => call.some((part) => part.endsWith('/type-mismatch.fsx')))
  )
})

test('project execution requires an executable and expected output', (t) => {
  const root = createFixture(t)
  const projectPath = 'examples/compiled/Compiled.fsproj'
  write(
    root,
    'ThinkingInFSharp.slnx',
    `<Solution>\n  <Project Path="${projectPath}" />\n</Solution>\n`
  )
  write(root, projectPath, project({ source: 'Library.fs' }))
  write(root, 'examples/compiled/Library.fs', 'module Compiled\nlet answer = 42\n')
  writeManifest(root, [
    {
      id: 'compiled-example',
      kind: 'compile',
      path: projectPath,
      sources: ['examples/compiled/Library.fs'],
      runArguments: ['--verify-only']
    }
  ])

  const errors = checkExamples({ repoRoot: root })

  assert.ok(
    errors.some((error) => error.includes('expectedOutput must be a non-empty array'))
  )
  assert.ok(
    errors.some((error) => error.includes('must set OutputType to Exe'))
  )
})

test('rejects code files that are not registered in the manifest', (t) => {
  const root = createFixture(t)
  write(root, 'examples/scripts/unregistered.fsx', 'printfn "not checked"\n')
  writeManifest(root, [])

  const errors = checkExamples({ repoRoot: root })

  assert.ok(
    errors.some((error) =>
      error.includes('examples/scripts/unregistered.fsx: code file is not registered')
    )
  )
})

test('rejects a registered F# source that its project does not compile', (t) => {
  const root = createFixture(t)
  write(
    root,
    'ThinkingInFSharp.slnx',
    '<Solution>\n  <Project Path="examples/sample/Sample.fsproj" />\n</Solution>\n'
  )
  write(root, 'examples/sample/Sample.fsproj', project({ source: 'Included.fs' }))
  write(root, 'examples/sample/Included.fs', 'module Included\n')
  write(root, 'examples/sample/Skipped.fs', 'module Skipped\n')
  writeManifest(root, [
    {
      id: 'sample',
      kind: 'compile',
      path: 'examples/sample/Sample.fsproj',
      sources: [
        'examples/sample/Included.fs',
        'examples/sample/Skipped.fs'
      ]
    }
  ])

  const errors = checkExamples({ repoRoot: root })

  assert.ok(
    errors.some((error) =>
      error.includes('examples/sample/Skipped.fs is registered') &&
      error.includes('is not compiled by that project')
    )
  )
})

test('requires script output and an actual expected compiler failure', (t) => {
  const root = createFixture(t)
  write(root, 'examples/scripts/hello.fsx', 'printfn "wrong"\n')
  write(root, 'examples/expected-errors/failure.fsx', 'let answer = 42\n')
  writeManifest(root, [
    {
      id: 'hello',
      kind: 'script',
      path: 'examples/scripts/hello.fsx',
      expectedOutput: ['answer: 42']
    },
    {
      id: 'must-fail',
      kind: 'expected-error',
      path: 'examples/expected-errors/failure.fsx',
      diagnostics: ['FS0001']
    }
  ])

  const runner = () => ({ status: 0, stdout: 'wrong\n', stderr: '' })
  const errors = runExampleChecks({ repoRoot: root, runner })

  assert.ok(errors.some((error) => error.includes('missing expected output')))
  assert.ok(errors.some((error) => error.includes('was expected to fail')))
})

test('requires expected script output to appear in manifest order', (t) => {
  const root = createFixture(t)
  write(
    root,
    'examples/scripts/ordered.fsx',
    'printfn "first"\nprintfn "second"\n'
  )
  writeManifest(root, [
    {
      id: 'ordered',
      kind: 'script',
      path: 'examples/scripts/ordered.fsx',
      expectedOutput: ['first', 'second']
    }
  ])

  const runner = () => ({ status: 0, stdout: 'second\nfirst\n', stderr: '' })
  const errors = runExampleChecks({ repoRoot: root, runner })

  assert.ok(errors.some((error) => error.includes('out of order')))
})

test('rejects traversal paths and the CLI reports manifest failures', (t) => {
  const root = createFixture(t)
  writeManifest(root, [
    {
      id: 'escape',
      kind: 'script',
      path: '../outside.fsx',
      expectedOutput: ['unsafe']
    }
  ])

  const errors = checkExamples({ repoRoot: root })
  assert.ok(errors.some((error) => error.includes('normalized repository-relative path')))

  const result = spawnSync(
    process.execPath,
    [checkerCommand, '--root', root],
    { encoding: 'utf8' }
  )
  assert.notEqual(result.status, 0)
  assert.match(result.stderr, /examples\/manifest\.json/)
})
