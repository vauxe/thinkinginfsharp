import { defineConfig } from 'vitepress'

// VitePress 1.6.4 resolves config/index.ts directly, which keeps each locale
// small and independently reviewable.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/guide/i18n.md
export default defineConfig({
  title: 'F# 思维 / Thinking in F#',
  description: 'A bilingual, F#-first book for learning and mastering F#.',
  lang: 'en-US',
  cleanUrls: true,
  lastUpdated: true
})
