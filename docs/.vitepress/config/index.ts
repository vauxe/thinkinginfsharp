import { readFileSync } from 'node:fs'
import type { StateCore } from 'markdown-it'
import { defineConfig } from 'vitepress'
import {
  assertPageFrontmatter,
  validateTerminology
} from '../../../scripts/lib/content-contract.mjs'
import { normalizeSiteBase } from '../../../scripts/lib/site-base.mjs'
import { enLocale } from './en'
import { zhLocale } from './zh'

function accessibleHeadingPermalink(
  slug: string,
  _options: unknown,
  state: StateCore,
  index: number
) {
  const inline = state.tokens[index + 1]
  const title = (inline.children ?? [])
    .filter(
      (token) =>
        ['text', 'code_inline'].includes(token.type) &&
        !token.meta?.isPermalinkSymbol
    )
    .map((token: any) => token.content)
    .join('')
    .trim()
  const relativePath = state.env.relativePath
  const isChinese =
    typeof relativePath === 'string' && relativePath.startsWith('zh/')
  const ariaLabel = isChinese
    ? `“${title}”的永久链接`
    : `Permalink to “${title}”`
  const linkOpen = new state.Token('link_open', 'a', 1)
  linkOpen.attrSet('class', 'header-anchor')
  linkOpen.attrSet('href', `#${slug}`)
  linkOpen.attrSet('aria-label', ariaLabel)
  const symbol = new state.Token('html_inline', '', 0)
  symbol.content = '&ZeroWidthSpace;'
  symbol.meta = { isPermalinkSymbol: true }

  inline.children.push(
    linkOpen,
    symbol,
    new state.Token('link_close', 'a', -1)
  )
}

const terminologyPath = new URL('../../terminology.json', import.meta.url)
const terminology = JSON.parse(readFileSync(terminologyPath, 'utf8'))
const terminologyErrors = validateTerminology(terminology, 'docs/terminology.json')

if (terminologyErrors.length > 0) {
  throw new Error(
    ['Invalid terminology metadata:', ...terminologyErrors].join('\n- ')
  )
}

const siteBase = normalizeSiteBase(process.env.VITEPRESS_BASE)

// VitePress 1.6.4 resolves config/index.ts directly, which keeps each locale
// small and independently reviewable.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/guide/i18n.md
export default defineConfig({
  base: siteBase,
  title: 'F# 思维 / Thinking in F#',
  description: 'A bilingual, F#-first book for learning and mastering F#.',
  lang: 'en',
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: `${siteBase}favicon.svg` }]
  ],
  cleanUrls: true,
  lastUpdated: true,
  markdown: {
    codeCopyButtonTitle: 'Copy code / 复制代码',
    theme: {
      light: 'github-light-high-contrast',
      dark: 'github-dark-high-contrast'
    },
    // Stable custom ids keep translated headings on the same hash. Keep the
    // default invisible permalink: VitePress 1.6.4's local-search splitter
    // uses that anchor to divide a page into searchable sections.
    anchor: { permalink: accessibleHeadingPermalink }
  },
  // transformPageData runs in development and production builds in VitePress
  // 1.6.4, so invalid book metadata fails at the same boundary as rendering.
  // Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/reference/site-config.md#transformpagedata
  transformPageData(pageData) {
    if (/^(?:zh|en)\//.test(pageData.relativePath)) {
      assertPageFrontmatter(pageData.frontmatter, pageData.relativePath)
    }
  },
  locales: {
    zh: zhLocale,
    en: enLocale
  },
  themeConfig: {
    i18nRouting: true,
    // The build-time search plugin reads the root theme config. Each locale
    // replaces this object with its own translated options at render time.
    search: { provider: 'local' },
    langMenuLabel: 'Language / 语言',
    skipToContentLabel: 'Skip to content / 跳到正文',
    returnToTopLabel: 'Return to top / 返回顶部',
    darkModeSwitchLabel: 'Appearance / 外观'
  }
})
