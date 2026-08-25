import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const docsRoot = join(resolve(dirname(fileURLToPath(import.meta.url)), '..'), 'docs')

function markdownFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) return markdownFiles(path)
    return entry.name.endsWith('.md') ? [path] : []
  })
}

function pagePaths(locale) {
  const localeRoot = join(docsRoot, locale)
  return new Map(
    markdownFiles(localeRoot).map((path) => [
      relative(localeRoot, path).replaceAll('\\', '/'),
      path
    ])
  )
}

function frontmatter(source, path) {
  const match = /^---\r?\n([\s\S]*?)\r?\n---/.exec(source)
  if (!match) throw new Error(`${path}: missing frontmatter`)
  return match[1]
}

function requiredField(metadata, field, path) {
  const match = new RegExp(`^${field}:\\s*(?:"([^"]+)"|(.+))$`, 'm').exec(metadata)
  const value = (match?.[1] ?? match?.[2])?.trim()
  if (!value) throw new Error(`${path}: missing ${field}`)
  return value
}

function headingShape(source, path) {
  const headings = []
  let fence

  for (const [index, line] of source.split(/\r?\n/).entries()) {
    const fenceMatch = /^\s*(`{3,}|~{3,})/.exec(line)
    if (fenceMatch) {
      const marker = fenceMatch[1][0]
      fence = fence === marker ? undefined : fence ?? marker
      continue
    }
    if (fence) continue

    const heading = /^(#{1,6})\s+(.+)$/.exec(line)
    if (!heading) continue
    const anchor = /\s+\{#([a-z0-9][a-z0-9-]*)\}\s*$/.exec(heading[2])
    if (!anchor) throw new Error(`${path}:${index + 1}: heading needs an explicit anchor`)
    headings.push(`${heading[1].length}:${anchor[1]}`)
  }

  return headings
}

function sharedCodeBlocks(source) {
  return [...source.matchAll(/^```([^\n]*\[[^\]]+\][^\n]*)\r?\n([\s\S]*?)^```\s*$/gm)]
    .map((match) => match[2])
}

function assertEqual(left, right, message) {
  if (JSON.stringify(left) !== JSON.stringify(right)) throw new Error(message)
}

function checkBook() {
  const english = pagePaths('en')
  const chinese = pagePaths('zh')
  const paths = [...new Set([...english.keys(), ...chinese.keys()])].sort()

  for (const path of paths) {
    const enPath = english.get(path)
    const zhPath = chinese.get(path)
    if (!enPath || !zhPath) throw new Error(`${path}: missing ${enPath ? 'Chinese' : 'English'} page`)

    const enSource = readFileSync(enPath, 'utf8')
    const zhSource = readFileSync(zhPath, 'utf8')
    const expectedKey = path.slice(0, -'.md'.length)
    const enMetadata = frontmatter(enSource, `en/${path}`)
    const zhMetadata = frontmatter(zhSource, `zh/${path}`)

    for (const [locale, metadata] of [['en', enMetadata], ['zh', zhMetadata]]) {
      requiredField(metadata, 'title', `${locale}/${path}`)
      requiredField(metadata, 'description', `${locale}/${path}`)
      const key = requiredField(metadata, 'translationKey', `${locale}/${path}`)
      if (key !== expectedKey) {
        throw new Error(`${locale}/${path}: translationKey must be ${expectedKey}`)
      }
    }

    assertEqual(
      headingShape(enSource, `en/${path}`),
      headingShape(zhSource, `zh/${path}`),
      `${path}: bilingual heading structure differs`
    )

    assertEqual(
      sharedCodeBlocks(enSource),
      sharedCodeBlocks(zhSource),
      `${path}: shared code blocks differ`
    )
  }

  console.log(`Book check passed: ${paths.length} bilingual page pairs.`)
}

try {
  checkBook()
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error))
  process.exitCode = 1
}
