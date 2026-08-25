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
const PART_CHECKPOINT_CHAPTERS = new Set([6, 12, 18, 24, 32])
const INTERNAL_IMPLEMENTATION_ID =
  /(?<![A-Za-z0-9_-])(?:K(?:0[1-9]|1[0-2])(?:[ab])?|X(?:39|4[0-5]))(?![A-Za-z0-9_-])/g
const COUNTED_TEST_EN =
  /\b(?<!chapter )(?:two|three|four|five|six|seven|eight|nine|ten|\d+)\s+(?:(?:focused|contract|property|chapter|repository|complete|unit|integration|passing)\s+)*(?:tests\b|test executions\b|focused cases\b)/i
const TEST_EVIDENCE_EN =
  /\b(?:assert(?:s|ed)?|compile(?:s|d)?|cover(?:s|ed)?|establish(?:es|ed)?|green|pass(?:ed|es|ing)?|ran|run(?:s|ning)?)\b/i
const COUNTED_TEST_ZH =
  /(?<!第)(?:两|[二三四五六七八九十百]+|\d+)\s*(?:项|个)?\s*(?:(?:聚焦|契约|性质|单元|集成|通过的)\s*)?(?:测试|聚焦用例)/
const TEST_EVIDENCE_ZH = /(?:断言|编译|覆盖|建立|绿色|通过|运行|证明)/
const MUTABLE_TEST_COMMAND_EN =
  /\brun\b[^.\n]{0,32}\b(?:two|three|four|five|six|seven|eight|nine|ten|\d+)\s+chapter\s+\d+\s+tests?\b/i
const MUTABLE_TEST_COMMAND_ZH =
  /运行第\s*\d+\s*章的(?:两|[二三四五六七八九十百]+|\d+)\s*(?:项|个)?测试/
const MUTABLE_TEST_TABLE_EN = /^\s*\|.*\b\d+\s+tests?\b.*\|\s*$/i
const MUTABLE_TEST_TABLE_ZH = /^\s*\|.*\b\d+\s*(?:项|个)?测试.*\|\s*$/
const MUTABLE_TEST_RATIO_EN = /(?:test|focused run)[^.\n]{0,48}\b\d+\/\d+\b/i
const MUTABLE_TEST_RATIO_ZH = /(?:测试|聚焦运行)[^。\n]{0,48}\b\d+\/\d+\b/
const MUTABLE_TEST_CASE_TOTAL_EN =
  /(?:\bsame\s+(?:two|three|four|five|six|seven|eight|nine|ten|\d+)\s+cases\b|\b(?:two|three|four|five|six|seven|eight|nine|ten|\d+)\s+(?:`TestServer`\s+|HTTP\s+|passing\s+)cases\b)/i
const MUTABLE_TEST_CASE_TOTAL_ZH =
  /(?:相同(?:的)?\s*(?:两|[二三四五六七八九十百]+|\d+)\s*(?:项|个)?\s*用例|(?:两|[二三四五六七八九十百]+|\d+)\s*(?:项|个)?\s*(?:`TestServer`|HTTP|通过的?)\s*(?:测试|用例))/

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

function validateReaderProse(page) {
  const errors = []
  let fence

  for (const [index, line] of page.body.split('\n').entries()) {
    const fenceMatch = /^\s*(`{3,}|~{3,})/.exec(line)
    if (fenceMatch) {
      const marker = fenceMatch[1][0]
      if (fence === marker) fence = undefined
      else if (!fence) fence = marker
      continue
    }
    if (fence) continue

    const lineNumber = index + page.bodyStartLine
    for (const [implementationId] of line.matchAll(INTERNAL_IMPLEMENTATION_ID)) {
      errors.push(
        diagnostic(
          page,
          lineNumber,
          `reader prose must not expose internal implementation id "${implementationId}"`
        )
      )
    }

    const mutableTestTotal = page.relativePath.startsWith('en/')
      ? (COUNTED_TEST_EN.test(line) && TEST_EVIDENCE_EN.test(line)) ||
        MUTABLE_TEST_COMMAND_EN.test(line) ||
        MUTABLE_TEST_TABLE_EN.test(line) ||
        MUTABLE_TEST_RATIO_EN.test(line) ||
        MUTABLE_TEST_CASE_TOTAL_EN.test(line)
      : (COUNTED_TEST_ZH.test(line) && TEST_EVIDENCE_ZH.test(line)) ||
        MUTABLE_TEST_COMMAND_ZH.test(line) ||
        MUTABLE_TEST_TABLE_ZH.test(line) ||
        MUTABLE_TEST_RATIO_ZH.test(line) ||
        MUTABLE_TEST_CASE_TOTAL_ZH.test(line)
    if (mutableTestTotal) {
      errors.push(
        diagnostic(
          page,
          lineNumber,
          'reader prose must describe tested behavior instead of a mutable test-suite total'
        )
      )
    }
  }

  if (
    page.frontmatter.kind === 'chapter' &&
    PART_CHECKPOINT_CHAPTERS.has(page.frontmatter.chapter) &&
    !page.anchors.includes('part-checkpoint')
  ) {
    errors.push(
      `${page.relativePath}: part-ending chapters must contain a {#part-checkpoint} heading`
    )
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

      errors.push(...validateReaderProse(page))
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
