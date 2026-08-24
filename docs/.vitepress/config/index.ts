import { defineConfig } from 'vitepress'
import { enLocale } from './en'
import { zhLocale } from './zh'

// VitePress 1.6.4 resolves config/index.ts directly, which keeps each locale
// small and independently reviewable.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/guide/i18n.md
export default defineConfig({
  title: 'F# 思维 / Thinking in F#',
  description: 'A bilingual, F#-first book for learning and mastering F#.',
  lang: 'en-US',
  cleanUrls: true,
  lastUpdated: true,
  locales: {
    zh: zhLocale,
    en: enLocale
  },
  themeConfig: {
    i18nRouting: true,
    langMenuLabel: 'Language / 语言',
    skipToContentLabel: 'Skip to content / 跳到正文',
    returnToTopLabel: 'Return to top / 返回顶部',
    darkModeSwitchLabel: 'Appearance / 外观'
  }
})
