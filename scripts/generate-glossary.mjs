import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  statSync,
  writeFileSync
} from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { parse } from 'yaml'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')
const LOCALES = ['en', 'zh']

const copy = {
  en: {
    title: 'Appendix F: English–Chinese F# Glossary',
    description: 'A self-contained glossary generated from the bilingual terminology catalog, with stable anchors and links to each term’s first teaching chapter.',
    heading: 'Appendix F: English–Chinese F# Glossary',
    introduction: [
      'This glossary defines the book’s F# vocabulary in English and records the preferred Chinese counterpart. Each definition is complete in English; knowing Chinese is optional. The stable identifier in each entry is used by content metadata and remains unchanged when display wording improves.',
      '“First introduced” means the earliest chapter in reading order whose frontmatter declares that term identifier. It is a teaching location, not a claim that the word never appeared earlier in ordinary prose. Follow the link for motivation, examples, and surrounding concepts.',
      'The entries and links are generated from `docs/terminology.json` and chapter metadata. Edit those sources, then run `pnpm generate:glossary`; `pnpm check:content` rejects stale generated pages.'
    ],
    useHeading: 'How to use this glossary',
    useBody: 'Search the visible English or Chinese term, follow a stable anchor for a direct link, or read by part to revisit concepts in their original learning order. Definitions describe this book’s usage; the linked chapter supplies the operational detail.',
    groups: {
      1: 'Part 1 · Foundations: values, functions, and flow',
      2: 'Part 2 · Modeling with types',
      3: 'Part 3 · Composition and program structure'
    },
    aliases: 'Also called',
    introduced: 'First introduced',
    stableId: 'Stable ID',
    counterpart: (term) => term.zh.preferred
  },
  zh: {
    title: '附录 F：F# 中英文术语表',
    description: '由双语术语目录生成的自足术语表，提供稳定锚点以及每个术语首次教学章节的链接。',
    heading: '附录 F：F# 中英文术语表',
    introduction: [
      '本术语表用中文定义全书的 F# 词汇，并记录首选英文对应词。每项定义都能只靠中文独立理解，不要求读者先会英文。条目中的稳定标识供内容元数据使用；即使日后改进显示用词，它也保持不变。',
      '“首次讲解”指阅读顺序中最早在 frontmatter 声明该术语标识的章节；它是教学入口，并不声称这个词此前从未在一般叙述中出现。可沿链接查看动机、示例和周围概念。',
      '条目与链接由 `docs/terminology.json` 和章节元数据生成。请修改这些源，再运行 `pnpm generate:glossary`；`pnpm check:content` 会拒绝过期的生成页面。'
    ],
    useHeading: '如何使用本术语表',
    useBody: '可以搜索可见的中文或英文术语，通过稳定锚点直接链接某项，也可以按部分阅读，以原学习顺序复习概念。定义说明本书中的用法；所链接章节提供操作细节。',
    groups: {
      1: '第 1 部分 · 基础：值、函数与控制流',
      2: '第 2 部分 · 用类型建模',
      3: '第 3 部分 · 组合与程序结构'
    },
    aliases: '亦称',
    introduced: '首次讲解',
    stableId: '稳定标识',
    counterpart: (term) => term.en.preferred
  }
}

function markdownFiles(directory) {
  const files = []

  for (const name of readdirSync(directory).sort()) {
    const path = join(directory, name)
    const metadata = statSync(path)
    if (metadata.isDirectory()) files.push(...markdownFiles(path))
    else if (metadata.isFile() && name.endsWith('.md')) files.push(path)
  }

  return files
}

function readFrontmatter(path) {
  const source = readFileSync(path, 'utf8')
  const match = /^---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(source)
  if (!match) throw new Error(`${path}: missing YAML frontmatter`)
  const frontmatter = parse(match[1])
  if (!frontmatter || typeof frontmatter !== 'object' || Array.isArray(frontmatter)) {
    throw new Error(`${path}: frontmatter must be an object`)
  }
  return frontmatter
}

function pageRank(page) {
  if (page.frontmatter.kind === 'chapter' && Number.isInteger(page.frontmatter.chapter)) {
    return page.frontmatter.chapter
  }
  if (page.frontmatter.kind === 'preface') return -100
  if (page.frontmatter.kind === 'appendix') return 1000
  return 2000
}

function collectIntroductions(docsDir, locale) {
  const localeDir = join(docsDir, locale)
  const introductions = new Map()

  for (const path of markdownFiles(localeDir)) {
    if (path === join(localeDir, 'glossary.md')) continue
    const frontmatter = readFrontmatter(path)
    const page = {
      frontmatter,
      path,
      relativePath: relative(localeDir, path).replaceAll('\\', '/'),
      rank: pageRank({ frontmatter })
    }

    for (const termId of frontmatter.termIds ?? []) {
      const current = introductions.get(termId)
      if (
        !current ||
        page.rank < current.rank ||
        (page.rank === current.rank && page.relativePath < current.relativePath)
      ) {
        introductions.set(termId, page)
      }
    }
  }

  return introductions
}

function catalogTerms(docsDir) {
  const path = join(docsDir, 'terminology.json')
  const catalog = JSON.parse(readFileSync(path, 'utf8'))
  if (!catalog || typeof catalog.terms !== 'object' || Array.isArray(catalog.terms)) {
    throw new Error('terminology.json: terms must be an object')
  }
  return catalog.terms
}

function markdownText(value) {
  return String(value).replaceAll('\\', '\\\\').replaceAll('[', '\\[').replaceAll(']', '\\]')
}

function linkFor(page) {
  return `./${page.relativePath.replace(/\.md$/, '')}#overview`
}

function validateIntroductions(terms, introductionsByLocale) {
  for (const termId of Object.keys(terms)) {
    for (const locale of LOCALES) {
      if (!introductionsByLocale[locale].has(termId)) {
        const language = locale === 'en' ? 'English' : 'Chinese'
        throw new Error(`${termId}: no ${language} first-introduction page declares this term id`)
      }
    }

    const english = introductionsByLocale.en.get(termId).frontmatter
    const chinese = introductionsByLocale.zh.get(termId).frontmatter
    if (english.translationKey !== chinese.translationKey) {
      throw new Error(
        `${termId}: first-introduction translation keys differ: ${english.translationKey} / ${chinese.translationKey}`
      )
    }
    if (english.part !== chinese.part || english.chapter !== chinese.chapter) {
      throw new Error(`${termId}: first-introduction part/chapter metadata differs by locale`)
    }
  }
}

function frontmatter(locale) {
  return `---
title: "${copy[locale].title}"
description: "${copy[locale].description}"
translationKey: glossary
kind: glossary
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---`
}

function renderEntry({ locale, termId, term, introduction }) {
  const localized = term[locale]
  const text = copy[locale]
  const title = `${markdownText(localized.preferred)} · ${markdownText(text.counterpart(term))}`
  const aliases = localized.aliases?.length
    ? `\n\n**${text.aliases}:** ${localized.aliases.map(markdownText).join(', ')}`
    : ''

  return `### ${title} {#${termId}}

${localized.definition}${aliases}

**${text.introduced}:** [${markdownText(introduction.frontmatter.title)}](${linkFor(introduction)}) · **${text.stableId}:** \`${termId}\``
}

function renderGlossary({ locale, terms, introductions }) {
  const text = copy[locale]
  const entries = Object.entries(terms).map(([termId, term]) => ({
    termId,
    term,
    introduction: introductions.get(termId)
  }))

  entries.sort((left, right) => {
    const chapterDifference = left.introduction.rank - right.introduction.rank
    if (chapterDifference !== 0) return chapterDifference
    return left.term.en.preferred.localeCompare(right.term.en.preferred, 'en')
  })

  const groups = new Map()
  for (const entry of entries) {
    const part = entry.introduction.frontmatter.part
    if (!Number.isInteger(part)) {
      throw new Error(`${entry.termId}: first-introduction page must declare an integer part`)
    }
    const group = groups.get(part) ?? []
    group.push(entry)
    groups.set(part, group)
  }

  const sections = [...groups.entries()].map(([part, group]) => {
    const heading = text.groups[part] ?? `${locale === 'en' ? 'Part' : '第'} ${part}`
    const body = group.map((entry) => renderEntry({ locale, ...entry })).join('\n\n')
    return `## ${heading} {#part-${part}}\n\n${body}`
  }).join('\n\n')

  return `${frontmatter(locale)}

# ${text.heading} {#overview}

${text.introduction.join('\n\n')}

## ${text.useHeading} {#how-to-use}

${text.useBody}

${sections}
`
}

export function buildGlossaryOutputs({ docsDir = defaultDocsDir } = {}) {
  const absoluteDocsDir = resolve(docsDir)
  const terms = catalogTerms(absoluteDocsDir)
  const introductionsByLocale = Object.fromEntries(
    LOCALES.map((locale) => [locale, collectIntroductions(absoluteDocsDir, locale)])
  )
  validateIntroductions(terms, introductionsByLocale)

  return new Map(LOCALES.map((locale) => [
    join(absoluteDocsDir, locale, 'glossary.md'),
    renderGlossary({ locale, terms, introductions: introductionsByLocale[locale] })
  ]))
}

export function checkGlossaryOutputs(options = {}) {
  const outputs = buildGlossaryOutputs(options)
  const docsDir = resolve(options.docsDir ?? defaultDocsDir)
  const errors = []

  for (const [path, expected] of outputs) {
    const displayPath = relative(docsDir, path).replaceAll('\\', '/')
    if (!existsSync(path)) {
      errors.push(`${displayPath}: generated glossary is missing; run pnpm generate:glossary`)
    } else if (readFileSync(path, 'utf8') !== expected) {
      errors.push(`${displayPath}: generated glossary is stale; run pnpm generate:glossary`)
    }
  }

  return errors.sort()
}

export function writeGlossaryOutputs(options = {}) {
  const outputs = buildGlossaryOutputs(options)
  for (const [path, source] of outputs) {
    mkdirSync(dirname(path), { recursive: true })
    writeFileSync(path, source)
  }
  return outputs
}

function cli(argv = process.argv.slice(2)) {
  const check = argv.includes('--check')
  const unknown = argv.filter((argument) => argument !== '--check')
  if (unknown.length > 0) {
    console.error(`Unknown argument: ${unknown[0]}`)
    return 1
  }

  try {
    if (check) {
      const errors = checkGlossaryOutputs()
      if (errors.length > 0) {
        console.error(errors.join('\n'))
        return 1
      }
      console.log('Generated glossary check passed.')
    } else {
      const outputs = writeGlossaryOutputs()
      console.log(`Generated ${outputs.size} glossary pages.`)
    }
    return 0
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exitCode = cli()
