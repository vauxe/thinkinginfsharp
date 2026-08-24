import { spawnSync } from 'node:child_process'
import {
  existsSync,
  lstatSync,
  readFileSync,
  readdirSync,
  realpathSync,
  statSync
} from 'node:fs'
import { dirname, extname, isAbsolute, join, posix, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultRepoRoot = resolve(scriptDirectory, '..')
const MAX_MANIFEST_BYTES = 1024 * 1024
const MAX_TEXT_BYTES = 5 * 1024 * 1024
const MAX_PROCESS_OUTPUT_BYTES = 8 * 1024 * 1024
const PROCESS_TIMEOUT_MS = 5 * 60 * 1000

const KINDS = new Set([
  'script',
  'compile',
  'test',
  'contract',
  'unity-plugin',
  'expected-error',
  'illustrative'
])
const PROJECT_KINDS = new Set([
  'compile',
  'test',
  'contract',
  'unity-plugin'
])
const CODE_EXTENSIONS = new Set(['.fs', '.fsi', '.fsx', '.cs', '.fsproj', '.csproj'])
const PROJECT_EXTENSIONS = new Set(['.fsproj', '.csproj'])
const IGNORED_DIRECTORIES = new Set(['bin', 'obj', 'node_modules', '.git'])

function manifestDiagnostic(manifestLabel, message) {
  return `${manifestLabel}: ${message}`
}

function displayPath(repoRoot, path) {
  return relative(repoRoot, path).replaceAll('\\', '/') || '.'
}

function isWithin(root, target) {
  const difference = relative(root, target)
  return difference === '' || (
    difference !== '..' &&
    !difference.startsWith(`..${sep}`) &&
    !isAbsolute(difference)
  )
}

function normalizedRepositoryPath(value, field, manifestLabel, errors) {
  if (
    typeof value !== 'string' ||
    value.length === 0 ||
    value.includes('\\') ||
    value.includes('\0') ||
    value.startsWith('/') ||
    value.startsWith('./') ||
    value === '.' ||
    posix.normalize(value) !== value ||
    value === '..' ||
    value.startsWith('../')
  ) {
    errors.push(
      manifestDiagnostic(
        manifestLabel,
        `${field} must be a normalized repository-relative path: ${String(value)}`
      )
    )
    return undefined
  }
  return value
}

function validateRegularFile(repoRoot, path, field, manifestLabel, errors) {
  const absolutePath = resolve(repoRoot, path)
  try {
    const realRoot = realpathSync(repoRoot)
    const realPath = realpathSync(absolutePath)
    if (!isWithin(realRoot, realPath) || !statSync(realPath).isFile()) {
      throw new Error('not a file inside the repository')
    }
  } catch {
    errors.push(
      manifestDiagnostic(manifestLabel, `${field} does not name a regular file inside the repository: ${path}`)
    )
    return false
  }
  return true
}

function readCappedText(path, maxBytes, label, errors) {
  try {
    if (statSync(path).size > maxBytes) {
      errors.push(`${label}: file exceeds the ${Math.floor(maxBytes / 1024 / 1024)} MiB safety limit`)
      return undefined
    }
    return readFileSync(path, 'utf8')
  } catch (error) {
    errors.push(`${label}: cannot read file: ${error instanceof Error ? error.message : String(error)}`)
    return undefined
  }
}

function stringArray(value, field, manifestLabel, errors, { required = false } = {}) {
  if (value === undefined && !required) return []
  if (
    !Array.isArray(value) ||
    (required && value.length === 0) ||
    value.some((item) => typeof item !== 'string' || item.length === 0)
  ) {
    errors.push(
      manifestDiagnostic(
        manifestLabel,
        `${field} must be ${required ? 'a non-empty' : 'an'} array of non-empty strings`
      )
    )
    return []
  }
  if (new Set(value).size !== value.length) {
    errors.push(manifestDiagnostic(manifestLabel, `${field} must not contain duplicates`))
  }
  return value
}

function loadSolutionProjects(repoRoot, manifestLabel, errors) {
  const solutionPath = join(repoRoot, 'ThinkingInFSharp.slnx')
  if (!existsSync(solutionPath)) {
    errors.push(manifestDiagnostic(manifestLabel, 'ThinkingInFSharp.slnx does not exist'))
    return new Set()
  }
  const source = readCappedText(solutionPath, MAX_TEXT_BYTES, 'ThinkingInFSharp.slnx', errors)
  if (source === undefined) return new Set()

  const projects = new Set()
  const pattern = /<Project\s+[^>]*Path=(['"])([^'"]+)\1[^>]*\/?\s*>/g
  for (const match of source.matchAll(pattern)) {
    projects.add(match[2].replaceAll('\\', '/'))
  }
  return projects
}

function explicitCompileItems(projectPath, projectSource) {
  const items = new Set()
  const pattern = /<Compile\s+[^>]*Include=(['"])([^'"]+)\1[^>]*\/?\s*>/gi
  for (const match of projectSource.matchAll(pattern)) {
    const include = match[2]
      .replaceAll('&amp;', '&')
      .replaceAll('\\', '/')
    items.add(posix.normalize(posix.join(posix.dirname(projectPath), include)))
  }
  return items
}

function sourceIsCompiled(projectPath, projectSource, sourcePath) {
  if (explicitCompileItems(projectPath, projectSource).has(sourcePath)) return true

  return (
    extname(projectPath) === '.csproj' &&
    extname(sourcePath) === '.cs' &&
    !/<EnableDefaultCompileItems>\s*false\s*<\/EnableDefaultCompileItems>/i.test(projectSource) &&
    sourcePath.startsWith(`${posix.dirname(projectPath)}/`)
  )
}

function loadProjectAndValidateSources({
  repoRoot,
  projectPath,
  sourcePaths,
  manifestLabel,
  errors
}) {
  const projectErrors = []
  const projectSource = readCappedText(
    resolve(repoRoot, projectPath),
    MAX_TEXT_BYTES,
    projectPath,
    projectErrors
  )
  errors.push(...projectErrors)
  if (projectSource === undefined) return undefined

  for (const sourcePath of sourcePaths) {
    if (!sourceIsCompiled(projectPath, projectSource, sourcePath)) {
      errors.push(
        manifestDiagnostic(
          manifestLabel,
          `${sourcePath} is registered for ${projectPath} but is not compiled by that project`
        )
      )
    }
  }
  return projectSource
}

function discoverCodeFiles(repoRoot, errors) {
  const files = []

  function visit(absoluteDirectory) {
    for (const entry of readdirSync(absoluteDirectory, { withFileTypes: true })) {
      if (IGNORED_DIRECTORIES.has(entry.name)) continue
      const absolutePath = join(absoluteDirectory, entry.name)
      const relativePath = displayPath(repoRoot, absolutePath)

      if (entry.isSymbolicLink()) {
        errors.push(`${relativePath}: symbolic links are not allowed in code roots`)
        continue
      }
      if (entry.isDirectory()) {
        visit(absolutePath)
      } else if (entry.isFile() && CODE_EXTENSIONS.has(extname(entry.name))) {
        files.push(relativePath)
      }
    }
  }

  for (const rootName of ['examples', 'tests']) {
    const absoluteRoot = join(repoRoot, rootName)
    if (!existsSync(absoluteRoot)) continue
    if (lstatSync(absoluteRoot).isSymbolicLink()) {
      errors.push(`${rootName}: symbolic links are not allowed for code roots`)
      continue
    }
    visit(absoluteRoot)
  }
  return files.sort()
}

function parseManifest({ repoRoot, manifestPath }) {
  const errors = []
  const manifestLabel = displayPath(repoRoot, manifestPath)
  let realRoot
  let realManifest
  try {
    realRoot = realpathSync(repoRoot)
    realManifest = realpathSync(manifestPath)
    if (!isWithin(realRoot, realManifest) || !statSync(realManifest).isFile()) {
      throw new Error('outside repository')
    }
  } catch {
    return {
      entries: [],
      errors: [manifestDiagnostic(manifestLabel, 'manifest does not exist inside the repository')]
    }
  }

  const source = readCappedText(realManifest, MAX_MANIFEST_BYTES, manifestLabel, errors)
  if (source === undefined) return { entries: [], errors }

  let manifest
  try {
    manifest = JSON.parse(source)
  } catch (error) {
    return {
      entries: [],
      errors: [
        ...errors,
        manifestDiagnostic(
          manifestLabel,
          `invalid JSON: ${error instanceof Error ? error.message : String(error)}`
        )
      ]
    }
  }

  if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) {
    return {
      entries: [],
      errors: [...errors, manifestDiagnostic(manifestLabel, 'manifest must be an object')]
    }
  }
  if (manifest.schemaVersion !== 1) {
    errors.push(manifestDiagnostic(manifestLabel, 'schemaVersion must be 1'))
  }
  if (!Array.isArray(manifest.entries)) {
    return {
      entries: [],
      errors: [...errors, manifestDiagnostic(manifestLabel, 'entries must be an array')]
    }
  }

  const entries = []
  const ids = new Set()
  const primaryPaths = new Set()
  const needsSolution = manifest.entries.some((entry) => PROJECT_KINDS.has(entry?.kind))
  const solutionProjects = needsSolution
    ? loadSolutionProjects(repoRoot, manifestLabel, errors)
    : new Set()

  for (const [index, rawEntry] of manifest.entries.entries()) {
    const fieldPrefix = `entries[${index}]`
    if (!rawEntry || typeof rawEntry !== 'object' || Array.isArray(rawEntry)) {
      errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix} must be an object`))
      continue
    }

    const { id, kind } = rawEntry
    if (typeof id !== 'string' || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)) {
      errors.push(
        manifestDiagnostic(manifestLabel, `${fieldPrefix}.id must be a lowercase kebab-case identifier`)
      )
    } else if (ids.has(id)) {
      errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.id is duplicated: ${id}`))
    } else {
      ids.add(id)
    }
    if (!KINDS.has(kind)) {
      errors.push(
        manifestDiagnostic(manifestLabel, `${fieldPrefix}.kind is not supported: ${String(kind)}`)
      )
    }

    const path = normalizedRepositoryPath(
      rawEntry.path,
      `${fieldPrefix}.path`,
      manifestLabel,
      errors
    )
    if (path) {
      validateRegularFile(repoRoot, path, `${fieldPrefix}.path`, manifestLabel, errors)
      if (primaryPaths.has(path)) {
        errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.path is registered more than once: ${path}`))
      }
      primaryPaths.add(path)
    }

    const sources = stringArray(rawEntry.sources, `${fieldPrefix}.sources`, manifestLabel, errors)
      .map((sourcePath, sourceIndex) =>
        normalizedRepositoryPath(
          sourcePath,
          `${fieldPrefix}.sources[${sourceIndex}]`,
          manifestLabel,
          errors
        )
      )
      .filter(Boolean)
    for (const [sourceIndex, sourcePath] of sources.entries()) {
      validateRegularFile(
        repoRoot,
        sourcePath,
        `${fieldPrefix}.sources[${sourceIndex}]`,
        manifestLabel,
        errors
      )
      if (!CODE_EXTENSIONS.has(extname(sourcePath))) {
        errors.push(
          manifestDiagnostic(manifestLabel, `${fieldPrefix}.sources[${sourceIndex}] is not a code file: ${sourcePath}`)
        )
      }
      if (!sourcePath.startsWith('examples/') && !sourcePath.startsWith('tests/')) {
        errors.push(
          manifestDiagnostic(
            manifestLabel,
            `${fieldPrefix}.sources[${sourceIndex}] must be inside examples/ or tests/: ${sourcePath}`
          )
        )
      }
    }

    if (path && kind === 'script') {
      if (extname(path) !== '.fsx') {
        errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must be an .fsx file for script entries`))
      }
      stringArray(rawEntry.expectedOutput, `${fieldPrefix}.expectedOutput`, manifestLabel, errors, {
        required: true
      })
    }

    if (path && PROJECT_KINDS.has(kind)) {
      if (!PROJECT_EXTENSIONS.has(extname(path))) {
        errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must be a .fsproj or .csproj file`))
      }
      if (sources.length === 0) {
        errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.sources must register the project's code files`))
      }
      if (!solutionProjects.has(path)) {
        errors.push(manifestDiagnostic(manifestLabel, `${path} is not included in ThinkingInFSharp.slnx`))
      }

      const projectSource = loadProjectAndValidateSources({
        repoRoot,
        projectPath: path,
        sourcePaths: sources,
        manifestLabel,
        errors
      })
      if (
        projectSource !== undefined &&
        (kind === 'test' || kind === 'contract') &&
        !/<IsTestProject>\s*true\s*<\/IsTestProject>/i.test(projectSource)
      ) {
        errors.push(manifestDiagnostic(manifestLabel, `${path} must set IsTestProject to true`))
      }
      if (projectSource !== undefined && kind === 'unity-plugin') {
        if (!/<TargetFramework>\s*netstandard2\.1\s*<\/TargetFramework>/i.test(projectSource)) {
          errors.push(manifestDiagnostic(manifestLabel, `${path} must target netstandard2.1`))
        }
        const documentation = normalizedRepositoryPath(
          rawEntry.documentation,
          `${fieldPrefix}.documentation`,
          manifestLabel,
          errors
        )
        if (
          documentation &&
          validateRegularFile(
            repoRoot,
            documentation,
            `${fieldPrefix}.documentation`,
            manifestLabel,
            errors
          )
        ) {
          const documentationErrors = []
          const documentationSource = readCappedText(
            resolve(repoRoot, documentation),
            MAX_TEXT_BYTES,
            documentation,
            documentationErrors
          )
          errors.push(...documentationErrors)
          if (documentationSource !== undefined && !documentationSource.includes('FSharp.Core')) {
            errors.push(
              manifestDiagnostic(manifestLabel, `${documentation} must explain how FSharp.Core is packaged`)
            )
          }
        }
      }
    }

    if (path && kind === 'expected-error') {
      if (!path.startsWith('examples/expected-errors/')) {
        errors.push(
          manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must be inside examples/expected-errors/`)
        )
      }
      if (!new Set(['.fsx', '.fsproj', '.csproj']).has(extname(path))) {
        errors.push(
          manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must be an .fsx, .fsproj, or .csproj file`)
        )
      }
      const diagnostics = stringArray(
        rawEntry.diagnostics,
        `${fieldPrefix}.diagnostics`,
        manifestLabel,
        errors,
        { required: true }
      )
      for (const diagnostic of diagnostics) {
        if (!/^(?:FS|CS)\d{4}$/.test(diagnostic)) {
          errors.push(
            manifestDiagnostic(manifestLabel, `${fieldPrefix}.diagnostics contains an invalid compiler code: ${diagnostic}`)
          )
        }
      }
      if (PROJECT_EXTENSIONS.has(extname(path))) {
        if (sources.length === 0) {
          errors.push(
            manifestDiagnostic(
              manifestLabel,
              `${fieldPrefix}.sources must register the expected-error project's code files`
            )
          )
        }
        loadProjectAndValidateSources({
          repoRoot,
          projectPath: path,
          sourcePaths: sources,
          manifestLabel,
          errors
        })
      }
    }

    if (path && kind === 'illustrative') {
      if (!new Set(['.fs', '.fsi', '.fsx', '.cs']).has(extname(path))) {
        errors.push(
          manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must name an F# or C# source file`)
        )
      }
      if (typeof rawEntry.reason !== 'string' || rawEntry.reason.trim().length < 20) {
        errors.push(
          manifestDiagnostic(manifestLabel, `${fieldPrefix}.reason must explain why the code cannot run independently`)
        )
      }
      const documentedIn = stringArray(
        rawEntry.documentedIn,
        `${fieldPrefix}.documentedIn`,
        manifestLabel,
        errors,
        { required: true }
      )
      for (const [documentIndex, documentPathValue] of documentedIn.entries()) {
        const documentPath = normalizedRepositoryPath(
          documentPathValue,
          `${fieldPrefix}.documentedIn[${documentIndex}]`,
          manifestLabel,
          errors
        )
        if (documentPath) {
          if (extname(documentPath) !== '.md') {
            errors.push(
              manifestDiagnostic(
                manifestLabel,
                `${fieldPrefix}.documentedIn[${documentIndex}] must name a Markdown page`
              )
            )
          }
          validateRegularFile(
            repoRoot,
            documentPath,
            `${fieldPrefix}.documentedIn[${documentIndex}]`,
            manifestLabel,
            errors
          )
        }
      }
    }

    if (path && kind && !['test', 'contract'].includes(kind) && !path.startsWith('examples/')) {
      errors.push(manifestDiagnostic(manifestLabel, `${fieldPrefix}.path must be inside examples/`))
    }

    entries.push({ ...rawEntry, id, kind, path, sources })
  }

  const registeredCode = new Set()
  for (const entry of entries) {
    if (entry.path && CODE_EXTENSIONS.has(extname(entry.path))) registeredCode.add(entry.path)
    for (const source of entry.sources) registeredCode.add(source)
  }
  for (const codeFile of discoverCodeFiles(repoRoot, errors)) {
    if (!registeredCode.has(codeFile)) {
      errors.push(`${codeFile}: code file is not registered in examples/manifest.json`)
    }
  }

  return { entries, errors }
}

function resolvedOptions({ repoRoot = defaultRepoRoot, manifestPath } = {}) {
  const absoluteRoot = resolve(repoRoot)
  return {
    repoRoot: absoluteRoot,
    manifestPath: manifestPath
      ? resolve(manifestPath)
      : join(absoluteRoot, 'examples/manifest.json')
  }
}

export function checkExamples(options = {}) {
  const result = parseManifest(resolvedOptions(options))
  return [...new Set(result.errors)].sort()
}

function processFailure(label, result) {
  if (result.error) {
    return `${label}: process failed to start: ${result.error.message}`
  }
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`.trim()
  const suffix = output ? `\n${output.slice(-4000)}` : ''
  return `${label}: command exited with status ${String(result.status)}${suffix}`
}

export function runExampleChecks({ runner = spawnSync, ...options } = {}) {
  const resolved = resolvedOptions(options)
  const parsed = parseManifest(resolved)
  if (parsed.errors.length > 0) return [...new Set(parsed.errors)].sort()

  const errors = []
  const run = (label, args) => {
    const result = runner('dotnet', args, {
      cwd: resolved.repoRoot,
      encoding: 'utf8',
      timeout: PROCESS_TIMEOUT_MS,
      maxBuffer: MAX_PROCESS_OUTPUT_BYTES,
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
    })
    if (result.error || result.status !== 0) {
      errors.push(processFailure(label, result))
    }
    return result
  }

  const solutionEntries = parsed.entries.filter((entry) => PROJECT_KINDS.has(entry.kind))
  if (solutionEntries.length > 0) {
    const restore = run('ThinkingInFSharp.slnx restore', [
      'restore',
      'ThinkingInFSharp.slnx',
      '--locked-mode'
    ])
    if (!restore.error && restore.status === 0) {
      const build = run('ThinkingInFSharp.slnx Release build', [
        'build',
        'ThinkingInFSharp.slnx',
        '--configuration',
        'Release',
        '--no-restore'
      ])
      if (
        !build.error &&
        build.status === 0 &&
        parsed.entries.some((entry) => entry.kind === 'test' || entry.kind === 'contract')
      ) {
        run('ThinkingInFSharp.slnx tests', [
          'test',
          'ThinkingInFSharp.slnx',
          '--configuration',
          'Release',
          '--no-build'
        ])
      }
    }
  }

  for (const entry of parsed.entries.filter(({ kind }) => kind === 'script')) {
    const result = run(entry.path, ['fsi', '--exec', entry.path])
    if (!result.error && result.status === 0) {
      for (const expected of entry.expectedOutput) {
        if (!String(result.stdout ?? '').includes(expected)) {
          errors.push(`${entry.path}: missing expected output ${JSON.stringify(expected)}`)
        }
      }
    }
  }

  for (const entry of parsed.entries.filter(({ kind }) => kind === 'expected-error')) {
    const args = extname(entry.path) === '.fsx'
      ? ['fsi', '--exec', entry.path]
      : ['build', entry.path, '--configuration', 'Release']
    const result = runner('dotnet', args, {
      cwd: resolved.repoRoot,
      encoding: 'utf8',
      timeout: PROCESS_TIMEOUT_MS,
      maxBuffer: MAX_PROCESS_OUTPUT_BYTES,
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
    })
    if (result.error) {
      errors.push(processFailure(entry.path, result))
      continue
    }
    if (result.status === 0) {
      errors.push(`${entry.path}: compilation was expected to fail but succeeded`)
      continue
    }
    const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`
    for (const diagnostic of entry.diagnostics) {
      const compilerError = new RegExp(`\\berror\\s+${diagnostic}\\b`, 'i')
      if (!compilerError.test(output)) {
        errors.push(`${entry.path}: missing expected compiler diagnostic ${diagnostic}`)
      }
    }
  }

  return [...new Set(errors)].sort()
}

function optionValue(argv, name, fallback) {
  const index = argv.indexOf(name)
  if (index < 0) return fallback
  if (!argv[index + 1]) throw new Error(`${name} requires a path`)
  return resolve(argv[index + 1])
}

export function runExamplesCli(argv = process.argv.slice(2)) {
  try {
    const repoRoot = optionValue(argv, '--root', defaultRepoRoot)
    const manifestPath = optionValue(
      argv,
      '--manifest',
      join(repoRoot, 'examples/manifest.json')
    )
    const errors = runExampleChecks({ repoRoot, manifestPath })
    if (errors.length > 0) {
      console.error(errors.join('\n'))
      return 1
    }
    console.log('Example build and execution checks passed.')
    return 0
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }
}

const isMain =
  process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)

if (isMain) process.exitCode = runExamplesCli()
