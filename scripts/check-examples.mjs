import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, extname, isAbsolute, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultRepoRoot = resolve(scriptDirectory, '..')
const projectExtensions = new Set(['.fsproj', '.csproj'])

function normalizedLines(source) {
  const lines = source.replaceAll('\r\n', '\n').split('\n')

  while (lines[0]?.trim() === '') lines.shift()
  while (lines.at(-1)?.trim() === '') lines.pop()

  const indentation = lines
    .filter((line) => line.trim() !== '')
    .map((line) => /^\s*/.exec(line)[0].length)

  const commonIndentation = indentation.length === 0 ? 0 : Math.min(...indentation)
  return lines.map((line) => line.slice(Math.min(commonIndentation, line.length)))
}

function containsSnippet(script, snippet) {
  const scriptLines = script.replaceAll('\r\n', '\n').split('\n')
  const snippetLines = normalizedLines(snippet)

  for (let index = 0; index <= scriptLines.length - snippetLines.length; index += 1) {
    const candidate = normalizedLines(scriptLines.slice(index, index + snippetLines.length).join('\n'))
    if (candidate.join('\n') === snippetLines.join('\n')) return true
  }

  return false
}

function namedScriptBlocks(source) {
  const blocks = []
  const pattern = /^```fsharp[^\n]*\[([^\]]*?([A-Za-z0-9_.-]+\.fsx)[^\]]*)\][^\n]*\r?\n([\s\S]*?)^```\s*$/gm

  for (const match of source.matchAll(pattern)) {
    blocks.push({ fileName: match[2], source: match[3] })
  }

  return blocks
}

export function checkDocumentedSnippets({ pages, scripts }) {
  const errors = []
  const referencedScripts = new Set()

  for (const [pagePath, pageSource] of pages) {
    for (const fileName of scripts.keys()) {
      if (pageSource.includes(fileName)) referencedScripts.add(fileName)
    }

    for (const block of namedScriptBlocks(pageSource)) {
      const script = scripts.get(block.fileName)
      referencedScripts.add(block.fileName)

      if (script === undefined) {
        errors.push(`${pagePath}: ${block.fileName} has no runnable source file`)
      } else if (!containsSnippet(script, block.source)) {
        errors.push(`${pagePath}: documented snippet does not match ${block.fileName}`)
      }
    }
  }

  for (const fileName of scripts.keys()) {
    if (!referencedScripts.has(fileName)) {
      errors.push(`examples: ${fileName} is not referenced by an English book page`)
    }
  }

  return errors
}

function normalizedOutput(output) {
  return output.replaceAll('\r\n', '\n').trimEnd().split('\n')
}

function resultText(result) {
  return [result.stdout, result.stderr].filter(Boolean).join('\n').trim()
}

export function runManifestEntries({ entries, repoRoot, runner = spawnSync }) {
  const errors = []

  for (const entry of entries) {
    const path = resolve(repoRoot, entry.path)
    const options = {
      cwd: repoRoot,
      encoding: 'utf8',
      maxBuffer: 4 * 1024 * 1024,
      timeout: 60_000
    }

    if (entry.kind === 'project') {
      const build = runner(
        'dotnet',
        ['build', path, '--configuration', 'Release', '--nologo', '--verbosity:quiet', '-p:NuGetAudit=false'],
        options
      )

      if (build.error) {
        errors.push(`${entry.id}: ${build.error.message}`)
        continue
      }
      if (build.status !== 0) {
        errors.push(`${entry.id}: build failed\n${resultText(build)}`)
        continue
      }
      if (!entry.expectedOutput) continue

      const runArguments = ['run', '--project', path, '--configuration', 'Release', '--no-build']
      if (entry.runArguments?.length > 0) runArguments.push('--', ...entry.runArguments)

      const run = runner('dotnet', runArguments, options)

      if (run.error) {
        errors.push(`${entry.id}: ${run.error.message}`)
      } else if (run.status !== 0) {
        errors.push(`${entry.id}: execution failed\n${resultText(run)}`)
      } else if (JSON.stringify(normalizedOutput(run.stdout)) !== JSON.stringify(entry.expectedOutput)) {
        errors.push(
          `${entry.id}: output differs\nexpected: ${JSON.stringify(entry.expectedOutput)}\nactual:   ${JSON.stringify(normalizedOutput(run.stdout))}`
        )
      }
      continue
    }

    const arguments_ = projectExtensions.has(extname(path))
      ? [
          'build',
          path,
          '--configuration',
          'Release',
          '--nologo',
          '--verbosity:quiet',
          '--warnaserror',
          '--no-incremental',
          '-p:NuGetAudit=false'
        ]
      : ['fsi', '--nologo', '--langversion:10', '--checknulls+', '--warnaserror+', '--exec', path]
    const result = runner('dotnet', arguments_, options)

    if (result.error) {
      errors.push(`${entry.id}: ${result.error.message}`)
      continue
    }

    if (entry.kind === 'expected-error') {
      if (result.status === 0) {
        errors.push(`${entry.id}: expected compilation to fail`)
        continue
      }

      const output = resultText(result)
      for (const diagnostic of entry.diagnostics) {
        if (!output.includes(diagnostic)) {
          errors.push(`${entry.id}: expected diagnostic ${diagnostic} was not reported`)
        }
      }
      continue
    }

    if (result.status !== 0) {
      errors.push(`${entry.id}: execution failed\n${resultText(result)}`)
      continue
    }

    const actualOutput = normalizedOutput(result.stdout)
    if (JSON.stringify(actualOutput) !== JSON.stringify(entry.expectedOutput)) {
      errors.push(
        `${entry.id}: output differs\nexpected: ${JSON.stringify(entry.expectedOutput)}\nactual:   ${JSON.stringify(actualOutput)}`
      )
    }
  }

  return errors
}

function markdownFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) return markdownFiles(path)
    return entry.name.endsWith('.md') ? [path] : []
  })
}

function manifestEntries(repoRoot) {
  const manifestPath = join(repoRoot, 'examples', 'manifest.json')
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  const errors = []

  if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.entries)) {
    throw new Error('examples/manifest.json: expected schemaVersion 1 and an entries array')
  }

  const ids = new Set()
  const paths = new Set()

  for (const entry of manifest.entries) {
    if (!entry || typeof entry !== 'object') {
      errors.push('examples/manifest.json: every entry must be an object')
      continue
    }
    if (typeof entry.id !== 'string' || ids.has(entry.id)) {
      errors.push(`examples/manifest.json: invalid or duplicate id ${String(entry.id)}`)
    }
    ids.add(entry.id)

    if (!['script', 'project', 'expected-error'].includes(entry.kind)) {
      errors.push(`${entry.id}: unsupported kind ${String(entry.kind)}`)
    }
    if (typeof entry.path !== 'string' || paths.has(entry.path)) {
      errors.push(`${entry.id}: invalid or duplicate path ${String(entry.path)}`)
      continue
    }
    paths.add(entry.path)

    const absolutePath = resolve(repoRoot, entry.path)
    const difference = relative(repoRoot, absolutePath)
    const extension = extname(absolutePath)
    const validExtension = entry.kind === 'script'
      ? extension === '.fsx'
      : entry.kind === 'project'
        ? projectExtensions.has(extension)
        : extension === '.fsx' || projectExtensions.has(extension)
    if (
      isAbsolute(difference) ||
      difference === '..' ||
      difference.startsWith(`..${sep}`) ||
      !validExtension ||
      !existsSync(absolutePath)
    ) {
      errors.push(`${entry.id}: path has the wrong type or does not exist inside the repository`)
    }

    if (entry.kind === 'script' && (
      !Array.isArray(entry.expectedOutput) ||
      entry.expectedOutput.length === 0 ||
      entry.expectedOutput.some((line) => typeof line !== 'string')
    )) {
      errors.push(`${entry.id}: expectedOutput must be a non-empty string array`)
    }

    if (entry.kind === 'project' && entry.expectedOutput !== undefined && (
      !Array.isArray(entry.expectedOutput) ||
      entry.expectedOutput.length === 0 ||
      entry.expectedOutput.some((line) => typeof line !== 'string')
    )) {
      errors.push(`${entry.id}: expectedOutput must be a non-empty string array when provided`)
    }

    if (entry.runArguments !== undefined && (
      entry.kind !== 'project' ||
      !Array.isArray(entry.runArguments) ||
      entry.runArguments.some((argument) => typeof argument !== 'string')
    )) {
      errors.push(`${entry.id}: runArguments must be a string array on a project entry`)
    }

    if (entry.kind === 'expected-error' && (
      !Array.isArray(entry.diagnostics) ||
      entry.diagnostics.length === 0 ||
      entry.diagnostics.some((diagnostic) => !/^FS\d{4}$/.test(diagnostic))
    )) {
      errors.push(`${entry.id}: diagnostics must contain F# diagnostic codes`)
    }
  }

  return { entries: manifest.entries, errors }
}

export function checkExamples({ repoRoot = defaultRepoRoot, runner = spawnSync } = {}) {
  const { entries, errors } = manifestEntries(repoRoot)
  if (errors.length > 0) return errors

  const pages = new Map(
    markdownFiles(join(repoRoot, 'docs', 'en')).map((path) => [
      relative(repoRoot, path).replaceAll('\\', '/'),
      readFileSync(path, 'utf8')
    ])
  )

  const scripts = new Map(
    entries.filter((entry) => extname(entry.path) === '.fsx').map((entry) => [
      entry.path.split('/').at(-1),
      readFileSync(join(repoRoot, entry.path), 'utf8')
    ])
  )

  return [
    ...checkDocumentedSnippets({ pages, scripts }),
    ...runManifestEntries({ entries, repoRoot, runner })
  ]
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : undefined
if (invokedPath === fileURLToPath(import.meta.url)) {
  try {
    const errors = checkExamples()
    if (errors.length > 0) {
      console.error(errors.join('\n\n'))
      process.exitCode = 1
    } else {
      const { entries } = manifestEntries(defaultRepoRoot)
      console.log(`Example check passed: ${entries.length} executable contracts.`)
    }
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exitCode = 1
  }
}
