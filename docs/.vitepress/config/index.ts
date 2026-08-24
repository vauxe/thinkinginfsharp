import { defineConfig } from 'vitepress'

// VitePress 1.6.4 resolves ../config.ts. That file re-exports this module so
// locale-specific configuration can stay small and independently reviewable.
// Source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/reference/site-config.md#config-resolution
export default defineConfig({
  title: 'F# 思维 / Thinking in F#',
  description: 'A bilingual, F#-first book for learning and mastering F#.',
  lang: 'en-US',
  cleanUrls: true,
  lastUpdated: true
})
