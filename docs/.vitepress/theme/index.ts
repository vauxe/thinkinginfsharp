import { defineComponent, h, nextTick, onMounted, watch } from 'vue'
import { useRoute } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import ReadingProgress from './ReadingProgress.vue'
import './styles.css'

function localizeThemeLabels() {
  const isChinese = /(?:^|\/)zh(?:\/|$)/.test(location.pathname)
  const copyLabel = isChinese ? '复制代码' : 'Copy code'

  for (const button of document.querySelectorAll('button.copy')) {
    button.setAttribute('title', copyLabel)
    button.setAttribute('aria-label', copyLabel)
  }

  for (const anchor of document.querySelectorAll<HTMLAnchorElement>('a.header-anchor')) {
    const heading = anchor.parentElement
    const title = [...(heading?.childNodes ?? [])]
      .filter((node) => node !== anchor)
      .map((node) => node.textContent)
      .join('')
      .trim()
    anchor.setAttribute(
      'aria-label',
      isChinese ? `“${title}”的永久链接` : `Permalink to "${title}"`
    )
  }

  if (isChinese) {
    const hiddenLabels: Record<string, string> = {
      'Sidebar Navigation': '侧边栏导航',
      Pager: '翻页导航'
    }
    for (const label of document.querySelectorAll<HTMLElement>('.visually-hidden')) {
      const translation = hiddenLabels[label.textContent?.trim() ?? '']
      if (translation) label.textContent = translation
    }
    for (const toggle of document.querySelectorAll('[aria-label="toggle section"]')) {
      toggle.setAttribute('aria-label', '展开或折叠小节')
    }
    document.querySelector('button[aria-label="extra navigation"]')
      ?.setAttribute('aria-label', '更多导航')
    document.querySelector('button[aria-label="mobile navigation"]')
      ?.setAttribute('aria-label', '移动导航')
  }
}

const Layout = defineComponent({
  setup() {
    const route = useRoute()
    onMounted(localizeThemeLabels)
    watch(() => route.path, () => nextTick(localizeThemeLabels))
    return () => h(DefaultTheme.Layout, null, {
      'layout-top': () => h(ReadingProgress)
    })
  }
})

export default {
  extends: DefaultTheme,
  Layout
}
