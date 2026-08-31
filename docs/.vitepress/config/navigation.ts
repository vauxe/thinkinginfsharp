import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

type Locale = 'en' | 'zh'

const docsRoot = fileURLToPath(new URL('../../', import.meta.url))
const partNumbers = [1, 2, 3, 4, 5, 6, 7]

function partDirectory(number: number) {
  return `part-${String(number).padStart(2, '0')}`
}

const labels = {
  en: {
    contents: 'Contents',
    reference: 'Reference',
    parts: [
      'Part I · Expressions and functions',
      'Part II · Modeling with types',
      'Part III · Composition and program structure',
      'Part IV · Effects, asynchrony, and concurrency',
      'Part V · .NET interop and engineering quality',
      'Part VI · The booking system',
      'Part VII · The ecosystem map'
    ]
  },
  zh: {
    contents: '目录',
    reference: '参考资料',
    parts: [
      '第一部分 · 表达式与函数',
      '第二部分 · 用类型建立模型',
      '第三部分 · 组合与程序结构',
      '第四部分 · 副作用、异步与并发',
      '第五部分 · .NET 互操作与工程质量',
      '第六部分 · 完整预约工作流',
      '第七部分 · 生态地图'
    ]
  }
} as const

function titleOf(locale: Locale, path: string) {
  const source = readFileSync(join(docsRoot, locale, path), 'utf8')
  const match = /^title:\s*(.+)\s*$/m.exec(source)
  if (!match) throw new Error(`${locale}/${path}: missing title`)

  const title = match[1].trim()
  if (title.startsWith('"')) return JSON.parse(title) as string
  if (title.startsWith("'") && title.endsWith("'")) return title.slice(1, -1)
  return title
}

function route(locale: Locale, path: string) {
  const stem = path.slice(0, -'.md'.length)
  const localeRoot = locale === 'en' ? '' : `/${locale}`
  if (stem === 'index') return `${localeRoot}/`
  return `${localeRoot}/${stem.endsWith('/index') ? stem.slice(0, -'index'.length) : stem}`
}

function item(locale: Locale, path: string) {
  return { text: titleOf(locale, path), link: route(locale, path) }
}

function pages(locale: Locale, directory: string) {
  return readdirSync(join(docsRoot, locale, directory))
    .filter((name) => name.endsWith('.md'))
    .sort()
    .map((name) => item(locale, `${directory}/${name}`))
}

function createSidebar(locale: Locale) {
  const text = labels[locale]
  const localeRoot = locale === 'en' ? '' : `/${locale}`
  const contents = { text: text.contents, link: route(locale, 'index.md') }
  const preface = item(locale, 'preface/index.md')
  const parts = partNumbers.map((number) => ({
    text: text.parts[number - 1],
    collapsed: true,
    items: pages(locale, partDirectory(number))
  }))
  const appendices = pages(locale, 'appendices')
  const glossary = item(locale, 'glossary.md')
  const reference = [...appendices.slice(0, 5), glossary, ...appendices.slice(5)]
  const bookSidebar = [
    contents,
    preface,
    ...parts,
    { text: text.reference, collapsed: true, items: reference }
  ]
  const bookPrefixes = [
    `${localeRoot}/preface/`,
    ...partNumbers.map((number) => `${localeRoot}/${partDirectory(number)}/`),
    `${localeRoot}/appendices/`,
    `${localeRoot}/glossary`
  ]

  return Object.fromEntries(bookPrefixes.map((prefix) => [prefix, bookSidebar]))
}

export const enSidebar = createSidebar('en')
export const zhSidebar = createSidebar('zh')
