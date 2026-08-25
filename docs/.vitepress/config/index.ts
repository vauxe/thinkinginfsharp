import { defineConfig } from 'vitepress'
import { enLocale } from './en'
import { zhLocale } from './zh'

const siteBase = process.env.VITEPRESS_BASE || '/'

// VitePress 1.6.4 resolves config/index.ts directly, which keeps each locale
// small and independently reviewable.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/guide/i18n.md
export default defineConfig({
  base: siteBase,
  title: 'F#',
  description: 'Learn F# from expressions, types, and functions.',
  lang: 'en',
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: `${siteBase}favicon.svg` }]
  ],
  cleanUrls: true,
  lastUpdated: true,
  vite: {
    // Each locale's full-text index is one lazy-loaded chunk.
    build: { chunkSizeWarningLimit: 2300 }
  },
  markdown: {
    codeCopyButtonTitle: 'Copy code',
    theme: {
      light: 'github-light-high-contrast',
      dark: 'github-dark-high-contrast'
    },
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
    langMenuLabel: 'Language',
    skipToContentLabel: 'Skip to content',
    returnToTopLabel: 'Return to top',
    darkModeSwitchLabel: 'Appearance'
  }
})
