import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { assertPageFrontmatter } from './lib/content-contract.mjs'
import { collectMarkdownPages } from './lib/markdown.mjs'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')

const PAIRED_FIELDS = [
  'translationKey',
  'kind',
  'status',
  'part',
  'chapter',
  'appendix',
  'exampleIds',
  'exerciseIds',
  'termIds',
  'sources',
  'verifiedWith'
]

function comparable(value) {
  return JSON.stringify(value ?? null)
}

function comparableTarget(target) {
  return (target ?? '').replace(/^\/(?:zh|en)(?=\/|[?#]|$)/, '/{locale}')
}

function comparableCodeReference(reference) {
  return reference.replace(/\s+\[[^\]]*\]\s*$/, '').trim()
}

function pairLabel(zh, en) {
  return `${zh.relativePath} <-> ${en.relativePath}`
}

export function checkParity({ docsDir = defaultDocsDir } = {}) {
  const pages = collectMarkdownPages(docsDir, { localesOnly: true })
  const errors = pages.flatMap((page) => page.errors)
  const byLocale = { zh: new Map(), en: new Map() }

  for (const page of pages) {
    const match = /^(zh|en)\/(.+)$/.exec(page.relativePath)
    if (!match) continue

    try {
      assertPageFrontmatter(page.frontmatter, page.relativePath)
    } catch (error) {
      errors.push(...(error.errors ?? [`${page.relativePath}: ${error.message}`]))
    }

    byLocale[match[1]].set(match[2], page)
  }

  const relativePaths = new Set([
    ...byLocale.zh.keys(),
    ...byLocale.en.keys()
  ])

  for (const relativePath of [...relativePaths].sort()) {
    const zh = byLocale.zh.get(relativePath)
    const en = byLocale.en.get(relativePath)

    if (!zh) {
      errors.push(
        `en/${relativePath}: missing Chinese translation zh/${relativePath}`
      )
      continue
    }
    if (!en) {
      errors.push(
        `zh/${relativePath}: missing English translation en/${relativePath}`
      )
      continue
    }

    for (const field of PAIRED_FIELDS) {
      if (comparable(zh.frontmatter[field]) !== comparable(en.frontmatter[field])) {
        errors.push(`${pairLabel(zh, en)}: ${field} differs`)
      }
    }

    const zhHeadingShape = zh.headings.map(({ level }) => level)
    const enHeadingShape = en.headings.map(({ level }) => level)
    if (comparable(zhHeadingShape) !== comparable(enHeadingShape)) {
      errors.push(`${pairLabel(zh, en)}: heading levels differ`)
    }

    const zhAnchors = zh.headings.map(({ anchor }) => anchor)
    const enAnchors = en.headings.map(({ anchor }) => anchor)
    if (comparable(zhAnchors) !== comparable(enAnchors)) {
      errors.push(`${pairLabel(zh, en)}: heading anchors differ`)
    }

    const zhLinks = zh.links.map(({ target, type }) => ({
      target: comparableTarget(target),
      type
    }))
    const enLinks = en.links.map(({ target, type }) => ({
      target: comparableTarget(target),
      type
    }))
    if (comparable(zhLinks) !== comparable(enLinks)) {
      errors.push(`${pairLabel(zh, en)}: link targets differ`)
    }

    const zhCodeReferences = zh.codeReferences.map(({ reference }) =>
      comparableCodeReference(reference)
    )
    const enCodeReferences = en.codeReferences.map(({ reference }) =>
      comparableCodeReference(reference)
    )
    if (comparable(zhCodeReferences) !== comparable(enCodeReferences)) {
      errors.push(`${pairLabel(zh, en)}: code references differ`)
    }

    for (const page of [zh, en]) {
      for (const heading of page.headings.filter(({ explicit }) => !explicit)) {
        errors.push(
          `${page.relativePath}:${heading.line}: heading "${heading.title}" needs an explicit stable anchor`
        )
      }
    }
  }

  return [...new Set(errors)].sort()
}

function optionValue(argv, name, fallback) {
  const index = argv.indexOf(name)
  if (index < 0) return fallback
  if (!argv[index + 1]) {
    throw new Error(`${name} requires a path`)
  }
  return resolve(argv[index + 1])
}

export function runParityCli(argv = process.argv.slice(2)) {
  let errors
  try {
    const docsDir = optionValue(argv, '--docs', defaultDocsDir)
    errors = checkParity({ docsDir })
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }

  if (errors.length > 0) {
    console.error(errors.join('\n'))
    return 1
  }

  console.log('Bilingual parity check passed.')
  return 0
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)

if (isMain) {
  process.exitCode = runParityCli()
}
