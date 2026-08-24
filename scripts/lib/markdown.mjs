import {
  existsSync,
  readFileSync,
  readdirSync,
  statSync
} from 'node:fs'
import { join, relative } from 'node:path'
import MarkdownIt from 'markdown-it'
import { parseDocument, visit } from 'yaml'

const markdown = new MarkdownIt({
  html: true,
  linkify: false,
  typographer: false
})

const CUSTOM_ANCHOR = /\s+\{#([^{}\s]+)\}\s*$/
const STABLE_ANCHOR = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/
const PLACEHOLDER =
  /\b(?:TODO|TBD|WIP|PLACEHOLDER)\b|待补(?:充)?|待写|稍后补充|占位(?:页|内容)?/
const UNSAFE_HTML =
  /<\s*(?:base|embed|form|iframe|link|meta|object|script|style)\b|\s(?:on[a-z]+|srcdoc|v-html|@[a-z][\w:-]*)\s*=/i
const MAX_MARKDOWN_BYTES = 2 * 1024 * 1024

// This matches VitePress 1.6.4's default slugifier so link checks observe the
// same ids as the generated site.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/src/node/markdown/markdown.ts
export function slugifyHeading(text) {
  return text
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[\u0000-\u001f]/g, '')
    .replace(/[\s~\x60!@#$%^&*()\-_+=[\]{}|\\;:"'“”‘’<>,.?/]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-+|-+$/g, '')
    .replace(/^(\d)/, '_$1')
    .toLowerCase()
}

function diagnostic(relativePath, line, message) {
  const location = line ? `${relativePath}:${line}` : relativePath
  return `${location}: ${message}`
}

function extractFrontmatter(source, relativePath) {
  const errors = []
  const normalized = source.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n')
  const lines = normalized.split('\n')

  if (lines[0] !== '---') {
    return {
      body: normalized,
      bodyStartLine: 1,
      errors: [diagnostic(relativePath, 1, 'missing YAML frontmatter')],
      frontmatter: {}
    }
  }

  const closingIndex = lines.findIndex(
    (line, index) => index > 0 && line === '---'
  )
  if (closingIndex < 0) {
    return {
      body: '',
      bodyStartLine: lines.length + 1,
      errors: [diagnostic(relativePath, 1, 'frontmatter has no closing --- delimiter')],
      frontmatter: {}
    }
  }

  const yamlSource = lines.slice(1, closingIndex).join('\n')
  const body = lines.slice(closingIndex + 1).join('\n')
  let frontmatter = {}

  try {
    const document = parseDocument(yamlSource, {
      prettyErrors: false,
      strict: true,
      uniqueKeys: true,
      version: '1.2'
    })
    for (const issue of [...document.errors, ...document.warnings]) {
      errors.push(
        diagnostic(
          relativePath,
          1,
          `invalid frontmatter: ${String(issue.message).split('\n')[0]}`
        )
      )
    }
    let hasAlias = false
    visit(document, {
      Alias() {
        hasAlias = true
        return visit.BREAK
      }
    })
    if (hasAlias) {
      errors.push(
        diagnostic(relativePath, 1, 'invalid frontmatter: YAML alias count must be zero')
      )
    }
    if (document.errors.length === 0 && !hasAlias) {
      frontmatter = document.toJS() ?? {}
    }
  } catch (error) {
    errors.push(
      diagnostic(
        relativePath,
        1,
        `invalid frontmatter: ${error instanceof Error ? error.message : String(error)}`
      )
    )
  }

  return {
    body,
    bodyStartLine: closingIndex + 2,
    errors,
    frontmatter
  }
}

function lineFor(token, bodyStartLine) {
  return token.map ? token.map[0] + bodyStartLine : bodyStartLine
}

function htmlAttributes(content, attribute) {
  const values = []
  const pattern = new RegExp(
    String.raw`\b${attribute}\s*=\s*(?:"([^"]+)"|'([^']+)'|([^\s"'=<>\x60]+))`,
    'gi'
  )
  for (const match of content.matchAll(pattern)) {
    values.push(match[1] ?? match[2] ?? match[3])
  }
  return values
}

export function parseMarkdownSource(source, relativePath) {
  const parsed = extractFrontmatter(source, relativePath)
  const errors = [...parsed.errors]
  const headings = []
  const anchors = []
  const links = []
  const codeReferences = []
  const placeholderFindings = []
  const unsafeHtmlFindings = []
  const visibleText = []
  const usedAnchors = new Set()
  let tokens = []

  try {
    tokens = markdown.parse(parsed.body, {})
  } catch (error) {
    errors.push(
      diagnostic(
        relativePath,
        parsed.bodyStartLine,
        `Markdown parse failed: ${error instanceof Error ? error.message : String(error)}`
      )
    )
  }

  function addExplicitAnchor(anchor, line) {
    if (usedAnchors.has(anchor)) {
      errors.push(diagnostic(relativePath, line, `duplicate anchor "#${anchor}"`))
    } else {
      usedAnchors.add(anchor)
    }
    anchors.push(anchor)
  }

  function addAutomaticAnchor(base) {
    let anchor = base
    let suffix = 1
    while (usedAnchors.has(anchor)) {
      anchor = `${base}-${suffix}`
      suffix += 1
    }
    usedAnchors.add(anchor)
    anchors.push(anchor)
    return anchor
  }

  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index]
    const line = lineFor(token, parsed.bodyStartLine)

    if (token.type === 'heading_open') {
      const inline = tokens[index + 1]
      const rawTitle = inline?.content?.trim() ?? ''
      const anchorMatch = CUSTOM_ANCHOR.exec(rawTitle)
      const title = rawTitle.replace(CUSTOM_ANCHOR, '').trim()
      let anchor

      if (anchorMatch) {
        anchor = anchorMatch[1]
        if (!STABLE_ANCHOR.test(anchor)) {
          errors.push(
            diagnostic(
              relativePath,
              line,
              `custom anchor "#${anchor}" must use lowercase kebab case`
            )
          )
        }
        addExplicitAnchor(anchor, line)
      } else {
        if (rawTitle.includes('{#')) {
          errors.push(
            diagnostic(relativePath, line, 'custom heading anchor has invalid syntax')
          )
        }
        const base = slugifyHeading(title)
        if (base.length === 0) {
          errors.push(
            diagnostic(relativePath, line, 'heading cannot produce a usable anchor')
          )
        }
        anchor = addAutomaticAnchor(base)
      }

      headings.push({
        anchor,
        explicit: Boolean(anchorMatch),
        level: Number(token.tag.slice(1)),
        line,
        title
      })
    }

    if (token.type === 'inline') {
      for (const child of token.children ?? []) {
        if (child.type === 'link_open') {
          links.push({ line, target: child.attrGet('href'), type: 'link' })
        } else if (child.type === 'image') {
          links.push({ line, target: child.attrGet('src'), type: 'image' })
        } else if (child.type === 'text') {
          const text = child.content.replace(CUSTOM_ANCHOR, '')
          visibleText.push(text)
          const match = PLACEHOLDER.exec(text)
          if (match) {
            placeholderFindings.push({ line, text: match[0] })
          }
        }
      }
    }

    if (token.type === 'html_block' || token.type === 'html_inline') {
      for (const anchor of htmlAttributes(token.content, 'id')) {
        addExplicitAnchor(anchor, line)
      }
      for (const target of htmlAttributes(token.content, 'href')) {
        links.push({ line, target, type: 'link' })
      }
      for (const target of htmlAttributes(token.content, 'src')) {
        links.push({ line, target, type: 'asset' })
      }

      const placeholder = PLACEHOLDER.exec(token.content)
      if (placeholder) {
        placeholderFindings.push({ line, text: placeholder[0] })
      }
      if (UNSAFE_HTML.test(token.content)) {
        unsafeHtmlFindings.push({ line })
      }
      visibleText.push(
        token.content
          .replace(/<!--[\s\S]*?-->/g, ' ')
          .replace(/<[^>]*>/g, ' ')
      )
    }
  }

  const excludedLines = new Set()
  for (const token of tokens) {
    if ((token.type === 'fence' || token.type === 'code_block') && token.map) {
      for (let line = token.map[0]; line < token.map[1]; line += 1) {
        excludedLines.add(line)
      }
    }
  }

  for (const [index, line] of parsed.body.split('\n').entries()) {
    if (excludedLines.has(index)) continue
    const match = /^\s*<<<\s+(.+?)\s*$/.exec(line)
    if (match) {
      codeReferences.push({
        line: index + parsed.bodyStartLine,
        reference: match[1]
      })
    }
  }

  return {
    ...parsed,
    anchors,
    codeReferences,
    errors,
    headings,
    links,
    placeholderFindings,
    plainText: visibleText.join(' ').replace(/\s+/g, ' ').trim(),
    unsafeHtmlFindings
  }
}

function walkMarkdown(directory, files) {
  if (!existsSync(directory)) return
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === '.vitepress' || entry.name === 'public') continue
    const absolutePath = join(directory, entry.name)
    if (entry.isDirectory()) {
      walkMarkdown(absolutePath, files)
    } else if (entry.isFile() && entry.name.endsWith('.md')) {
      files.push(absolutePath)
    }
  }
}

export function collectMarkdownPages(docsDir, { localesOnly = false } = {}) {
  const files = []
  if (localesOnly) {
    walkMarkdown(join(docsDir, 'zh'), files)
    walkMarkdown(join(docsDir, 'en'), files)
  } else {
    walkMarkdown(docsDir, files)
  }

  return files
    .map((absolutePath) => {
      const relativePath = relative(docsDir, absolutePath).replaceAll('\\', '/')
      if (statSync(absolutePath).size > MAX_MARKDOWN_BYTES) {
        const parsed = parseMarkdownSource('---\n---\n', relativePath)
        parsed.errors = [
          `${relativePath}: Markdown file exceeds the 2 MiB safety limit`
        ]
        return { absolutePath, relativePath, ...parsed }
      }
      return {
        absolutePath,
        relativePath,
        ...parseMarkdownSource(readFileSync(absolutePath, 'utf8'), relativePath)
      }
    })
    .sort((left, right) => left.relativePath.localeCompare(right.relativePath))
}
