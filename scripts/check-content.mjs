import {
  existsSync,
  readFileSync,
  realpathSync,
  statSync
} from 'node:fs'
import {
  dirname,
  extname,
  isAbsolute,
  join,
  posix,
  relative,
  resolve,
  sep
} from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  assertPageFrontmatter,
  sharedCodeReferenceTarget,
  validateSharedCodeReference,
  validateTerminology
} from './lib/content-contract.mjs'
import { collectMarkdownPages } from './lib/markdown.mjs'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')
const MAX_EXAMPLE_BYTES = 5 * 1024 * 1024
const MAX_TERMINOLOGY_BYTES = 1024 * 1024

function diagnostic(page, line, message) {
  return `${page.relativePath}:${line}: ${message}`
}

function safeDecode(value) {
  try {
    return decodeURIComponent(value)
  } catch {
    return undefined
  }
}

function pageCandidates(relativeTarget) {
  if (relativeTarget === '') return ['index.md']
  if (relativeTarget.endsWith('/')) return [`${relativeTarget}index.md`]

  const extension = extname(relativeTarget)
  if (extension === '.md') return [relativeTarget]
  if (extension === '.html') {
    return [`${relativeTarget.slice(0, -5)}.md`]
  }
  if (extension) return []
  return [`${relativeTarget}.md`, `${relativeTarget}/index.md`]
}

function resolveInternalLink(page, target, pagesByPath, docsDir) {
  if (!target) return { error: 'link target is empty' }
  if (/^(?:https:|mailto:|tel:)/i.test(target)) {
    return { external: true }
  }
  if (/^http:/i.test(target)) {
    try {
      const { hostname } = new URL(target)
      if (
        hostname === 'localhost' ||
        hostname.endsWith('.localhost') ||
        hostname === '127.0.0.1' ||
        hostname === '[::1]'
      ) {
        return { external: true }
      }
    } catch {
      return { error: `external link is not a valid URL: ${target}` }
    }
    return { error: `external links must use https: ${target}` }
  }
  if (/^data:/i.test(target) || target.startsWith('//')) {
    return { error: `ambiguous or embedded link targets are not allowed: ${target}` }
  }
  if (/^[a-z][a-z\d+.-]*:/i.test(target)) {
    return { error: `unsupported link scheme: ${target}` }
  }

  const hashIndex = target.indexOf('#')
  const rawPath = hashIndex >= 0 ? target.slice(0, hashIndex) : target
  const rawHash = hashIndex >= 0 ? target.slice(hashIndex + 1) : ''
  const pathWithoutQuery = rawPath.split('?')[0]
  const decodedPath = safeDecode(pathWithoutQuery)
  const decodedHash = safeDecode(rawHash)
  if (decodedPath === undefined || decodedHash === undefined) {
    return { error: `link has invalid percent encoding: ${target}` }
  }

  let relativeTarget
  if (decodedPath === '') {
    relativeTarget = page.relativePath
  } else if (decodedPath.startsWith('/')) {
    relativeTarget = decodedPath === '/'
      ? ''
      : posix.normalize(decodedPath.slice(1))
  } else {
    relativeTarget = posix.normalize(
      posix.join(posix.dirname(page.relativePath), decodedPath)
    )
  }

  if (
    relativeTarget === '..' ||
    relativeTarget.startsWith('../') ||
    posix.isAbsolute(relativeTarget)
  ) {
    return { error: `internal link escapes docs/: ${target}` }
  }

  const matchedPage = pageCandidates(relativeTarget)
    .map((candidate) => pagesByPath.get(candidate))
    .find(Boolean)

  if (matchedPage) {
    if (decodedHash && !matchedPage.anchors.includes(decodedHash)) {
      return {
        error: `internal link anchor does not exist: ${target}`
      }
    }
    return { page: matchedPage }
  }

  const absoluteAsset = decodedPath.startsWith('/')
    ? join(docsDir, 'public', relativeTarget)
    : join(docsDir, relativeTarget)
  if (existsSync(absoluteAsset)) {
    return { asset: absoluteAsset }
  }

  return { error: `internal link target does not exist: ${target}` }
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function validateCodeReferences(page, docsDir) {
  const errors = []
  const examplesDir = resolve(docsDir, '../examples')

  for (const codeReference of page.codeReferences) {
    const referenceErrors = validateSharedCodeReference(codeReference.reference)
    for (const error of referenceErrors) {
      errors.push(diagnostic(page, codeReference.line, error))
    }
    if (referenceErrors.length > 0) continue

    const target = sharedCodeReferenceTarget(codeReference.reference)
    const absolutePath = resolve(examplesDir, target.path)
    let realTarget
    let fileStats
    try {
      const realExamplesDir = realpathSync(examplesDir)
      realTarget = realpathSync(absolutePath)
      fileStats = statSync(realTarget)
      const withinExamples = relative(realExamplesDir, realTarget)
      if (
        withinExamples === '..' ||
        withinExamples.startsWith(`..${sep}`) ||
        isAbsolute(withinExamples) ||
        !fileStats.isFile()
      ) {
        throw new Error('outside examples')
      }
    } catch {
      errors.push(
        diagnostic(
          page,
          codeReference.line,
          `shared code file does not exist or leaves examples/: examples/${target.path}`
        )
      )
      continue
    }

    if (fileStats.size > MAX_EXAMPLE_BYTES) {
      errors.push(
        diagnostic(
          page,
          codeReference.line,
          `shared code file exceeds the 5 MiB safety limit: examples/${target.path}`
        )
      )
      continue
    }

    if (target.region) {
      const source = readFileSync(realTarget, 'utf8')
      const marker = new RegExp(
        `(?:^|\\s)#region\\s+${escapeRegExp(target.region)}(?:\\s|$)`,
        'm'
      )
      if (!marker.test(source)) {
        errors.push(
          diagnostic(
            page,
            codeReference.line,
            `shared code region does not exist: #${target.region}`
          )
        )
      }
    }
  }

  return errors
}

function loadTerminology(terminologyPath, docsDir) {
  const displayPath = relative(docsDir, terminologyPath).replaceAll('\\', '/')
  if (!existsSync(terminologyPath)) {
    return {
      errors: [`${displayPath}: terminology catalog does not exist`],
      terms: new Set()
    }
  }
  if (statSync(terminologyPath).size > MAX_TERMINOLOGY_BYTES) {
    return {
      errors: [`${displayPath}: terminology catalog exceeds the 1 MiB safety limit`],
      terms: new Set()
    }
  }

  try {
    const catalog = JSON.parse(readFileSync(terminologyPath, 'utf8'))
    return {
      errors: validateTerminology(catalog, displayPath),
      terms: new Set(Object.keys(catalog.terms ?? {}))
    }
  } catch (error) {
    return {
      errors: [
        `${displayPath}: invalid JSON: ${error instanceof Error ? error.message : String(error)}`
      ],
      terms: new Set()
    }
  }
}

export function checkContent({
  docsDir = defaultDocsDir,
  terminologyPath = join(docsDir, 'terminology.json')
} = {}) {
  const pages = collectMarkdownPages(docsDir)
  const pagesByPath = new Map(
    pages.map((page) => [page.relativePath, page])
  )
  const terminology = loadTerminology(terminologyPath, docsDir)
  const errors = [...terminology.errors]

  for (const page of pages) {
    errors.push(...page.errors)

    if (/^(?:zh|en)\//.test(page.relativePath)) {
      try {
        assertPageFrontmatter(page.frontmatter, page.relativePath)
      } catch (error) {
        errors.push(...(error.errors ?? [`${page.relativePath}: ${error.message}`]))
      }

      const h1Count = page.headings.filter(({ level }) => level === 1).length
      if (h1Count !== 1) {
        errors.push(`${page.relativePath}: page must contain exactly one level-one heading`)
      }
      if (page.plainText.replace(/\s/g, '').length < 80) {
        errors.push(`${page.relativePath}: page has too little substantive text and looks like a placeholder`)
      }

      for (const termId of page.frontmatter.termIds ?? []) {
        if (!terminology.terms.has(termId)) {
          errors.push(`${page.relativePath}: unknown term id "${termId}"`)
        }
      }
    }

    for (const finding of page.placeholderFindings) {
      errors.push(
        diagnostic(
          page,
          finding.line,
          `placeholder text "${finding.text}" is not allowed`
        )
      )
    }
    for (const finding of page.unsafeHtmlFindings) {
      errors.push(
        diagnostic(page, finding.line, 'unsafe active HTML is not allowed in book pages')
      )
    }

    errors.push(...validateCodeReferences(page, docsDir))

    for (const link of page.links) {
      const result = resolveInternalLink(page, link.target, pagesByPath, docsDir)
      if (result.error) {
        errors.push(diagnostic(page, link.line, result.error))
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

export function runContentCli(argv = process.argv.slice(2)) {
  let errors
  try {
    const docsDir = optionValue(argv, '--docs', defaultDocsDir)
    const terminologyPath = optionValue(
      argv,
      '--terminology',
      join(docsDir, 'terminology.json')
    )
    errors = checkContent({ docsDir, terminologyPath })
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }

  if (errors.length > 0) {
    console.error(errors.join('\n'))
    return 1
  }

  console.log('Content check passed.')
  return 0
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)

if (isMain) {
  process.exitCode = runContentCli()
}
