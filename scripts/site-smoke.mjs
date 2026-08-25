import {
  existsSync,
  readdirSync,
  readFileSync,
  statSync
} from 'node:fs'
import { dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { buildNavigationModel } from './generate-navigation.mjs'
import {
  normalizeSiteBase,
  stripSiteBase,
  withSiteBase
} from './lib/site-base.mjs'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')
const defaultDistDir = resolve(defaultDocsDir, '.vitepress/dist')
const siteOrigin = 'https://thinking-in-fsharp.invalid'

function pairedSearch(enQuery, zhQuery, localeRelativeRoute) {
  return {
    en: {
      locale: 'en',
      query: enQuery,
      route: `/en/${localeRelativeRoute}`
    },
    zh: {
      locale: 'zh',
      query: zhQuery,
      route: `/zh/${localeRelativeRoute}`
    }
  }
}

export const representativeSearchPairs = [
  pairedSearch('partial application', '部分应用', 'part-01/ch-03-functions-as-values'),
  pairedSearch('discriminated union', '可辨识联合', 'part-02/ch-08-discriminated-unions'),
  pairedSearch('active pattern', '活动模式', 'part-03/ch-15-active-patterns'),
  pairedSearch('computation expression', '计算表达式', 'part-03/ch-18-workflow-validation'),
  pairedSearch('cancellation timeout', '取消 超时', 'part-04/ch-23-cancellation-timeouts'),
  pairedSearch('property test', '性质测试', 'part-05/ch-29-property-testing'),
  pairedSearch('idempotency conflict', '幂等 冲突', 'part-06/ch-37-consistency-idempotency'),
  pairedSearch('type provider', '类型提供器', 'part-07/ch-40-data-analytics'),
  pairedSearch('Unity IL2CPP', 'Unity IL2CPP', 'part-07/ch-44-unity'),
  pairedSearch(
    'statically resolved type parameter',
    '静态解析类型参数',
    'appendices/h-advanced-index'
  )
]

const searchExpectations = representativeSearchPairs.flatMap(({ en, zh }) => [
  en,
  zh
])

function filesUnder(directory, predicate = () => true) {
  const files = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) files.push(...filesUnder(path, predicate))
    else if (predicate(path)) files.push(path)
  }
  return files
}

function isFile(path) {
  return existsSync(path) && statSync(path).isFile()
}

function decodeHtmlAttribute(value) {
  return value
    .replaceAll('&amp;', '&')
    .replaceAll('&quot;', '"')
    .replaceAll('&#39;', "'")
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
}

function routeFromHtmlPath(distDir, htmlPath) {
  const path = relative(distDir, htmlPath).split(sep).join('/')
  if (path === 'index.html') return '/'
  if (path.endsWith('/index.html')) {
    return `/${path.slice(0, -'index.html'.length)}`
  }
  return `/${path.slice(0, -'.html'.length)}`
}

export function routeToHtmlPath(distDir, route) {
  const pathname = route.split(/[?#]/, 1)[0]
  if (pathname === '/') return join(distDir, 'index.html')
  const relativePath = pathname.replace(/^\//, '')
  return pathname.endsWith('/')
    ? join(distDir, relativePath, 'index.html')
    : join(distDir, `${relativePath}.html`)
}

export function tokenizeSearchQuery(query) {
  return query
    .toLowerCase()
    .split(/[\n\r\p{Z}\p{P}]+/u)
    .filter(Boolean)
}

export function findIndexedMatches(serializedIndex, query) {
  const queryTerms = tokenizeSearchQuery(query)
  if (queryTerms.length === 0) return []

  const idsByQueryTerm = queryTerms.map((queryTerm) => {
    const ids = new Set()
    for (const [indexedTerm, fields] of serializedIndex.index) {
      if (!indexedTerm.startsWith(queryTerm)) continue
      for (const postings of Object.values(fields)) {
        for (const id of Object.keys(postings)) ids.add(id)
      }
    }
    return ids
  })

  return [...idsByQueryTerm[0]]
    .filter((id) => idsByQueryTerm.every((ids) => ids.has(id)))
    .map((id) => serializedIndex.documentIds[id])
    .filter(Boolean)
    .sort()
}

function internalTargets(html, sourceRoute, base) {
  const targets = []
  for (const match of html.matchAll(/\b(?:href|src)="([^"]+)"/g)) {
    const raw = decodeHtmlAttribute(match[1])
    if (/^(?:data:|mailto:|tel:|javascript:)/i.test(raw)) continue
    let url
    try {
      url = new URL(raw, `${siteOrigin}${withSiteBase(base, sourceRoute)}`)
    } catch {
      targets.push({ raw, invalid: true })
      continue
    }
    if (url.origin === siteOrigin) targets.push({ raw, url })
  }
  return targets
}

function targetFile(distDir, pathname, base) {
  const route = stripSiteBase(base, pathname)
  if (route === undefined) return undefined

  let decodedPath
  try {
    decodedPath = decodeURIComponent(route)
  } catch {
    return undefined
  }
  const exact = resolve(distDir, decodedPath.replace(/^\//, ''))
  const normalizedDistDir = resolve(distDir)
  if (
    exact !== normalizedDistDir &&
    !exact.startsWith(`${normalizedDistDir}${sep}`)
  ) {
    return undefined
  }
  const candidates = route.endsWith('/')
    ? [join(exact, 'index.html')]
    : [exact, `${exact}.html`, join(exact, 'index.html')]
  return candidates.find(isFile)
}

function htmlHasRoute(html, sourceRoute, expectedRoute, base) {
  return internalTargets(html, sourceRoute, base).some(
    ({ url }) => url?.pathname === withSiteBase(base, expectedRoute)
  )
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

async function loadSearchIndex(distDir, locale, errors) {
  const chunksDir = join(distDir, 'assets/chunks')
  const pattern = new RegExp(`^@localSearchIndex${locale}\\.[^.]+\\.js$`)
  const matches = readdirSync(chunksDir).filter((name) => pattern.test(name))
  if (matches.length !== 1) {
    errors.push(
      `search/${locale}: expected one generated index, found ${matches.length}`
    )
    return undefined
  }

  const path = join(chunksDir, matches[0])
  try {
    const module = await import(
      `${pathToFileURL(path).href}?site-smoke=${statSync(path).mtimeMs}`
    )
    return JSON.parse(module.default)
  } catch (error) {
    errors.push(
      `search/${locale}: cannot read generated index: ${
        error instanceof Error ? error.message : String(error)
      }`
    )
    return undefined
  }
}

function validateBookPages({ base, distDir, model, errors }) {
  const expectedRoutes = ['/', ...model.routePairs.flatMap(({ en, zh }) => [en, zh])]
  const rootHtml = isFile(routeToHtmlPath(distDir, '/'))
    ? readFileSync(routeToHtmlPath(distDir, '/'), 'utf8')
    : ''
  if (
    !htmlHasRoute(rootHtml, '/', '/en/', base) ||
    !htmlHasRoute(rootHtml, '/', '/zh/', base)
  ) {
    errors.push('index.html: neutral landing page must link to both language editions')
  }

  for (const pair of model.routePairs) {
    for (const locale of ['en', 'zh']) {
      const route = pair[locale]
      const path = routeToHtmlPath(distDir, route)
      if (!isFile(path)) {
        errors.push(`${route}: missing static HTML output`)
        continue
      }
      const html = readFileSync(path, 'utf8')
      const expectedLang = locale === 'en' ? 'en' : 'zh-Hans'
      const searchLabel = locale === 'en' ? 'Search this book' : '搜索本书'
      const counterpart = pair[locale === 'en' ? 'zh' : 'en']
      if (!html.includes(`<html lang="${expectedLang}"`)) {
        errors.push(`${route}: expected html lang ${expectedLang}`)
      }
      if (!html.includes(`aria-label="${searchLabel}"`)) {
        errors.push(`${route}: missing localized search control`)
      }
      if (!htmlHasRoute(html, route, counterpart, base)) {
        errors.push(`${route}: missing same-page language route ${counterpart}`)
      }

      const chapterMatch = route.match(/^\/(en|zh)\/part-\d{2}\/(ch-\d{2}-.+)$/)
      if (chapterMatch) {
        const solutionRoute = `/${locale}/solutions/${chapterMatch[2]}`
        if (!html.includes('class="copy"')) {
          errors.push(`${route}: no code-copy control was rendered`)
        }
        if (!html.includes('title="Copy code / 复制代码"')) {
          errors.push(`${route}: code-copy control lacks the bilingual title`)
        }
        if (!htmlHasRoute(html, route, solutionRoute, base)) {
          errors.push(`${route}: missing chapter-to-solution link ${solutionRoute}`)
        }
      }

      const solutionMatch = route.match(/^\/(en|zh)\/solutions\/(ch-(\d{2})-.+)$/)
      if (solutionMatch) {
        const chapter = Number.parseInt(solutionMatch[3], 10)
        const part = String(expectedPart(chapter)).padStart(2, '0')
        const chapterRoute = `/${locale}/part-${part}/${solutionMatch[2]}`
        if (!htmlHasRoute(html, route, chapterRoute, base)) {
          errors.push(`${route}: missing solution-to-chapter link ${chapterRoute}`)
        }
      }
    }
  }

  return expectedRoutes
}

function validateInternalLinks({ base, distDir, errors }) {
  const htmlFiles = filesUnder(distDir, (path) => path.endsWith('.html'))
  const checked = new Set()
  const idCache = new Map()

  for (const path of htmlFiles) {
    if (path.endsWith(`${sep}404.html`)) continue
    const sourceRoute = routeFromHtmlPath(distDir, path)
    const html = readFileSync(path, 'utf8')
    for (const target of internalTargets(html, sourceRoute, base)) {
      const key = `${sourceRoute}\0${target.raw}`
      if (checked.has(key)) continue
      checked.add(key)
      if (target.invalid) {
        errors.push(`${sourceRoute}: invalid internal URL ${target.raw}`)
        continue
      }
      const path = targetFile(distDir, target.url.pathname, base)
      if (!path) {
        errors.push(`${sourceRoute}: missing target ${target.raw}`)
        continue
      }
      if (!target.url.hash || !path.endsWith('.html')) continue
      let id
      try {
        id = decodeURIComponent(target.url.hash.slice(1))
      } catch {
        errors.push(`${sourceRoute}: invalid anchor encoding ${target.raw}`)
        continue
      }
      if (!id) continue
      if (!idCache.has(path)) {
        const targetHtml = readFileSync(path, 'utf8')
        idCache.set(
          path,
          new Set(
            [...targetHtml.matchAll(/\bid="([^"]+)"/g)].map((match) =>
              decodeHtmlAttribute(match[1])
            )
          )
        )
      }
      if (!idCache.get(path).has(id)) {
        errors.push(`${sourceRoute}: missing anchor ${target.raw}`)
      }
    }
  }

  return { htmlFiles: htmlFiles.length, checkedLinks: checked.size }
}

async function validateSearch({ base, distDir, expectedRoutes, errors }) {
  const sectionCounts = {}
  const indexes = {}
  for (const locale of ['en', 'zh']) {
    const index = await loadSearchIndex(distDir, locale, errors)
    if (!index) continue
    indexes[locale] = index
    sectionCounts[locale] = index.documentCount
    const ids = Object.values(index.documentIds)
    for (const route of expectedRoutes.filter((candidate) =>
      candidate.startsWith(`/${locale}/`)
    )) {
      const indexedRoute = withSiteBase(base, route)
      if (!ids.some((id) => id === indexedRoute || id.startsWith(`${indexedRoute}#`))) {
        errors.push(`search/${locale}: route is not indexed: ${route}`)
      }
    }
  }

  for (const expectation of searchExpectations) {
    const index = indexes[expectation.locale]
    if (!index) continue
    const matches = findIndexedMatches(index, expectation.query)
    const expectedRoute = withSiteBase(base, expectation.route)
    if (!matches.some((id) => id.startsWith(expectedRoute))) {
      errors.push(
        `search/${expectation.locale}: "${expectation.query}" cannot find ${expectation.route}`
      )
    }
  }

  return sectionCounts
}

export async function auditSite(options = {}) {
  const docsDir = resolve(options.docsDir ?? defaultDocsDir)
  const distDir = resolve(options.distDir ?? defaultDistDir)
  const base = normalizeSiteBase(options.base ?? process.env.VITEPRESS_BASE)
  const errors = []
  if (!isFile(join(distDir, 'index.html'))) {
    return {
      errors: [`${relative(process.cwd(), distDir)}: no production build; run pnpm build`],
      stats: {}
    }
  }

  const model = buildNavigationModel({ docsDir })
  const expectedRoutes = validateBookPages({ base, distDir, model, errors })
  const linkStats = validateInternalLinks({ base, distDir, errors })
  const sectionCounts = await validateSearch({
    base,
    distDir,
    expectedRoutes,
    errors
  })

  return {
    errors,
    stats: {
      bookPages: expectedRoutes.length,
      ...linkStats,
      searchSections: sectionCounts,
      searchQueries: searchExpectations.length,
      searchQueryPairs: representativeSearchPairs.length
    }
  }
}

async function cli(argv = process.argv.slice(2)) {
  if (argv.length > 0) {
    console.error(`Unknown argument: ${argv[0]}`)
    return 1
  }
  try {
    const { errors, stats } = await auditSite()
    if (errors.length > 0) {
      console.error(
        [
          `Site smoke failed with ${errors.length} error(s):`,
          ...errors.slice(0, 50).map((error) => `- ${error}`),
          ...(errors.length > 50 ? [`- … ${errors.length - 50} more`] : [])
        ].join('\n')
      )
      return 1
    }
    console.log(
      `Site smoke passed: ${stats.bookPages} book pages, ${stats.htmlFiles} HTML files, ` +
        `${stats.checkedLinks} internal links, ${stats.searchSections.en}/${stats.searchSections.zh} ` +
        `English/Chinese search sections, ${stats.searchQueries} representative queries ` +
        `in ${stats.searchQueryPairs} bilingual pairs.`
    )
    return 0
  } catch (error) {
    console.error(error instanceof Error ? error.stack : String(error))
    return 1
  }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exitCode = await cli()
