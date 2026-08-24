import {
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync
} from 'node:fs'
import { basename, dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { assertPageFrontmatter } from './lib/content-contract.mjs'
import { collectMarkdownPages } from './lib/markdown.mjs'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')
const defaultOutputPath = resolve(
  defaultDocsDir,
  '.vitepress/config/navigation.generated.ts'
)
const DEFAULT_EXPECTED_CHAPTERS = 45
const LOCALES = ['en', 'zh']
const APPENDIX_PATHS = new Map([
  ['A', 'appendices/a-setup.md'],
  ['B', 'appendices/b-syntax-reference.md'],
  ['C', 'appendices/c-collections.md'],
  ['D', 'appendices/d-csharp-migration.md'],
  ['E', 'appendices/e-compiler-errors.md'],
  ['G', 'appendices/g-solutions-guide.md'],
  ['H', 'appendices/h-advanced-index.md']
])

const copy = {
  en: {
    language: 'English',
    home: 'Home',
    preface: 'Preface and reading routes',
    startReading: 'Start reading',
    reference: 'Reference',
    solutions: 'Solutions',
    partNames: {
      1: 'Part I · Expressions and functions',
      2: 'Part II · Modeling with types',
      3: 'Part III · Composition and program structure',
      4: 'Part IV · Effects, asynchrony, and concurrency',
      5: 'Part V · .NET interop and engineering quality',
      6: 'Part VI · The booking system',
      7: 'Part VII · The ecosystem map'
    }
  },
  zh: {
    language: '中文',
    home: '首页',
    preface: '前言与阅读路线',
    startReading: '开始阅读',
    reference: '参考资料',
    solutions: '练习答案',
    partNames: {
      1: '第一部分 · 表达式与函数',
      2: '第二部分 · 用类型建立模型',
      3: '第三部分 · 组合与程序结构',
      4: '第四部分 · 副作用、异步与并发',
      5: '第五部分 · .NET 互操作与工程质量',
      6: '第六部分 · 活动预约系统',
      7: '第七部分 · 生态地图'
    }
  }
}

function expectedPart(chapter) {
  if (chapter <= 6) return 1
  if (chapter <= 12) return 2
  if (chapter <= 18) return 3
  if (chapter <= 24) return 4
  if (chapter <= 32) return 5
  if (chapter <= 38) return 6
  return 7
}

function localeRelativePath(page, locale) {
  return page.relativePath.slice(locale.length + 1)
}

function routeFor(locale, relativePath) {
  const withoutExtension = relativePath.replace(/\.md$/, '')
  if (withoutExtension === 'index') return `/${locale}/`
  if (withoutExtension.endsWith('/index')) {
    return `/${locale}/${withoutExtension.slice(0, -'index'.length)}`
  }
  return `/${locale}/${withoutExtension}`
}

function itemFor(page) {
  return {
    text: page.frontmatter.title,
    link: page.route
  }
}

function validatePages(docsDir) {
  const pages = collectMarkdownPages(docsDir, { localesOnly: true })
  for (const page of pages) {
    if (page.errors.length > 0) throw new Error(page.errors.join('\n'))
    assertPageFrontmatter(page.frontmatter, page.relativePath)
    if (page.frontmatter.status !== 'complete') {
      throw new Error(`${page.relativePath}: navigation cannot publish status ${page.frontmatter.status}`)
    }
  }
  return pages
}

function pairLocales(pages) {
  const byLocale = Object.fromEntries(LOCALES.map((locale) => [locale, new Map()]))
  for (const page of pages) {
    const locale = page.relativePath.slice(0, 2)
    const relativePath = localeRelativePath(page, locale)
    byLocale[locale].set(relativePath, page)
  }

  const relativePaths = new Set([
    ...byLocale.en.keys(),
    ...byLocale.zh.keys()
  ])
  const routePairs = []

  for (const relativePath of [...relativePaths].sort()) {
    const english = byLocale.en.get(relativePath)
    const chinese = byLocale.zh.get(relativePath)
    if (!chinese) {
      throw new Error(`en/${relativePath}: missing Chinese navigation counterpart zh/${relativePath}`)
    }
    if (!english) {
      throw new Error(`zh/${relativePath}: missing English navigation counterpart en/${relativePath}`)
    }
    if (english.frontmatter.translationKey !== chinese.frontmatter.translationKey) {
      throw new Error(`${relativePath}: bilingual navigation translationKey differs`)
    }
    if (english.frontmatter.kind !== chinese.frontmatter.kind) {
      throw new Error(`${relativePath}: bilingual navigation kind differs`)
    }

    english.route = routeFor('en', relativePath)
    chinese.route = routeFor('zh', relativePath)
    routePairs.push({
      translationKey: english.frontmatter.translationKey,
      en: english.route,
      zh: chinese.route
    })
  }

  return { byLocale, routePairs }
}

function exactlyOne(pages, predicate, description, language) {
  const matches = pages.filter(predicate)
  if (matches.length !== 1) {
    throw new Error(`${language}: expected exactly one ${description}, found ${matches.length}`)
  }
  return matches[0]
}

function numberedPages(pages, kind, expectedChapters, language) {
  const matches = pages.filter((page) => page.frontmatter.kind === kind)
  if (matches.length !== expectedChapters) {
    throw new Error(`${language}: expected ${expectedChapters} ${kind} pages, found ${matches.length}`)
  }

  const byChapter = new Map()
  for (const page of matches) {
    const chapter = page.frontmatter.chapter
    if (byChapter.has(chapter)) {
      throw new Error(`${language}: duplicate ${kind} page for chapter ${chapter}`)
    }
    byChapter.set(chapter, page)
  }

  return Array.from({ length: expectedChapters }, (_, index) => {
    const chapter = index + 1
    const page = byChapter.get(chapter)
    if (!page) throw new Error(`${language}: missing ${kind} page for chapter ${chapter}`)
    const part = expectedPart(chapter)
    if (page.frontmatter.part !== part) {
      throw new Error(
        `${language} chapter ${chapter}: expected part ${part}, found ${page.frontmatter.part}`
      )
    }
    return page
  })
}

function appendixPages(pages, language) {
  const matches = pages.filter((page) => page.frontmatter.kind === 'appendix')
  if (matches.length !== APPENDIX_PATHS.size) {
    throw new Error(
      `${language}: expected ${APPENDIX_PATHS.size} ordinary appendix pages, found ${matches.length}`
    )
  }

  const byLetter = new Map()
  for (const page of matches) {
    const letter = page.frontmatter.appendix
    if (byLetter.has(letter)) throw new Error(`${language}: duplicate appendix ${letter}`)
    byLetter.set(letter, page)
  }

  return [...APPENDIX_PATHS].map(([letter, expectedPath]) => {
    const page = byLetter.get(letter)
    if (!page) throw new Error(`${language}: missing appendix ${letter}`)
    if (page.localeRelativePath !== expectedPath) {
      throw new Error(
        `${language}: appendix ${letter} must be ${expectedPath}, found ${page.localeRelativePath}`
      )
    }
    return page
  })
}

function buildLocaleModel({ locale, localePages, expectedChapters }) {
  const text = copy[locale]
  const pages = [...localePages.values()].map((page) => ({
    ...page,
    localeRelativePath: localeRelativePath(page, locale)
  }))
  const home = exactlyOne(pages, (page) => page.frontmatter.kind === 'home', 'home page', text.language)
  const preface = exactlyOne(
    pages,
    (page) => page.frontmatter.kind === 'preface',
    'preface page',
    text.language
  )
  const glossary = exactlyOne(
    pages,
    (page) => page.frontmatter.kind === 'glossary',
    'glossary page',
    text.language
  )
  if (home.localeRelativePath !== 'index.md') {
    throw new Error(`${text.language}: home page must be index.md`)
  }
  if (preface.localeRelativePath !== 'preface/index.md') {
    throw new Error(`${text.language}: preface page must be preface/index.md`)
  }
  if (glossary.localeRelativePath !== 'glossary.md') {
    throw new Error(`${text.language}: glossary page must be glossary.md`)
  }

  const chapters = numberedPages(pages, 'chapter', expectedChapters, text.language)
  const solutions = numberedPages(pages, 'solution', expectedChapters, text.language)
  for (let index = 0; index < chapters.length; index += 1) {
    const chapter = chapters[index]
    const solution = solutions[index]
    const chapterSlug = basename(chapter.localeRelativePath, '.md')
    const solutionSlug = basename(solution.localeRelativePath, '.md')
    if (chapterSlug !== solutionSlug) {
      throw new Error(`${text.language} chapter ${index + 1}: chapter and solution slugs differ`)
    }
  }

  const ordinaryAppendices = appendixPages(pages, text.language)
  const appendices = [
    ...ordinaryAppendices.slice(0, 5),
    glossary,
    ...ordinaryAppendices.slice(5)
  ]
  const expectedPaths = new Set([
    home.localeRelativePath,
    preface.localeRelativePath,
    glossary.localeRelativePath,
    ...chapters.map((page) => page.localeRelativePath),
    ...solutions.map((page) => page.localeRelativePath),
    ...ordinaryAppendices.map((page) => page.localeRelativePath)
  ])
  for (const page of pages) {
    if (!expectedPaths.has(page.localeRelativePath)) {
      throw new Error(`${page.relativePath}: page is not represented in generated navigation`)
    }
  }

  const groups = new Map()
  for (const chapter of chapters) {
    const part = chapter.frontmatter.part
    const group = groups.get(part) ?? []
    group.push(chapter)
    groups.set(part, group)
  }
  const bookSidebar = [
    {
      text: text.startReading,
      items: [itemFor(home), itemFor(preface)]
    },
    ...[...groups].map(([part, partPages]) => ({
      text: text.partNames[part],
      collapsed: true,
      items: partPages.map(itemFor)
    })),
    {
      text: text.reference,
      collapsed: true,
      items: appendices.map(itemFor)
    }
  ]
  const solutionsSidebar = [{
    text: text.solutions,
    items: [itemFor(ordinaryAppendices.find((page) => page.frontmatter.appendix === 'G')), ...solutions.map(itemFor)]
  }]

  const sidebar = {}
  const bookPrefixes = [
    `/${locale}/preface/`,
    ...[...groups.keys()].map((part) => `/${locale}/part-${String(part).padStart(2, '0')}/`),
    `/${locale}/appendices/`,
    `/${locale}/glossary`
  ]
  for (const prefix of bookPrefixes) sidebar[prefix] = bookSidebar
  sidebar[`/${locale}/solutions/`] = solutionsSidebar

  return {
    nav: [
      { text: text.home, link: home.route },
      { text: text.preface, link: preface.route },
      { text: text.startReading, link: chapters[0].route },
      { text: text.reference, items: appendices.map(itemFor) }
    ],
    sidebar
  }
}

export function buildNavigationModel({
  docsDir = defaultDocsDir,
  expectedChapters = DEFAULT_EXPECTED_CHAPTERS
} = {}) {
  const absoluteDocsDir = resolve(docsDir)
  const pages = validatePages(absoluteDocsDir)
  const { byLocale, routePairs } = pairLocales(pages)
  const locales = Object.fromEntries(LOCALES.map((locale) => [
    locale,
    buildLocaleModel({ locale, localePages: byLocale[locale], expectedChapters })
  ]))

  return { locales, routePairs }
}

function indentedJson(value, spaces) {
  const indentation = ' '.repeat(spaces)
  return JSON.stringify(value, null, 2)
    .split('\n')
    .map((line, index) => index === 0 ? line : `${indentation}${line}`)
    .join('\n')
}

function renderLocaleNavigation(locale, navigation) {
  const exportName = `${locale}Navigation`
  const bookSidebarName = `${locale}BookSidebar`
  const solutionsSidebarName = `${locale}SolutionsSidebar`
  const solutionsPrefix = `/${locale}/solutions/`
  const bookPrefixes = Object.keys(navigation.sidebar).filter(
    (prefix) => prefix !== solutionsPrefix
  )
  const bookSidebar = navigation.sidebar[bookPrefixes[0]]
  const solutionsSidebar = navigation.sidebar[solutionsPrefix]
  const sidebarEntries = [
    ...bookPrefixes.map(
      (prefix) => `    ${JSON.stringify(prefix)}: ${bookSidebarName}`
    ),
    `    ${JSON.stringify(solutionsPrefix)}: ${solutionsSidebarName}`
  ].join(',\n')

  return `const ${bookSidebarName} = ${JSON.stringify(bookSidebar, null, 2)} as const

const ${solutionsSidebarName} = ${JSON.stringify(solutionsSidebar, null, 2)} as const

export const ${exportName} = {
  nav: ${indentedJson(navigation.nav, 2)},
  sidebar: {
${sidebarEntries}
  }
} as const`
}

export function buildNavigationSource(options = {}) {
  const model = buildNavigationModel(options)
  return `// Generated by scripts/generate-navigation.mjs. Do not edit by hand.\n\n${renderLocaleNavigation('en', model.locales.en)}\n\n${renderLocaleNavigation('zh', model.locales.zh)}\n`
}

export function checkNavigationOutput(options = {}) {
  const outputPath = resolve(options.outputPath ?? defaultOutputPath)
  const expected = buildNavigationSource(options)
  const displayPath = basename(outputPath)
  if (!existsSync(outputPath)) {
    return [`${displayPath}: generated navigation is missing; run pnpm generate:navigation`]
  }
  if (readFileSync(outputPath, 'utf8') !== expected) {
    return [`${displayPath}: generated navigation is stale; run pnpm generate:navigation`]
  }
  return []
}

export function writeNavigationOutput(options = {}) {
  const outputPath = resolve(options.outputPath ?? defaultOutputPath)
  const source = buildNavigationSource(options)
  mkdirSync(dirname(outputPath), { recursive: true })
  writeFileSync(outputPath, source)
  return outputPath
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
      const errors = checkNavigationOutput()
      if (errors.length > 0) {
        console.error(errors.join('\n'))
        return 1
      }
      console.log('Generated navigation check passed.')
    } else {
      const outputPath = writeNavigationOutput()
      console.log(`Generated ${relative(defaultDocsDir, outputPath).replaceAll('\\', '/')}.`)
    }
    return 0
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exitCode = cli()
