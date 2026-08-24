import { defineConfig } from 'vite'

export default defineConfig({
  appType: 'mpa',
  base: './',
  clearScreen: false,
  server: {
    host: '127.0.0.1',
    watch: {
      ignored: ['**/*.fs']
    }
  },
  preview: {
    host: '127.0.0.1'
  },
  build: {
    emptyOutDir: true,
    outDir: 'dist'
  }
})
