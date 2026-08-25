import { defineComponent, h, nextTick, onMounted, watch } from 'vue'
import { useRoute } from 'vitepress'
import DefaultTheme from 'vitepress/theme'
import './styles.css'

const navigationLabels = {
  en: {
    main: 'Main navigation',
    sidebar: 'Book contents',
    pager: 'Previous and next page',
    mobile: 'Mobile navigation',
    extra: 'More navigation',
    toggle: 'Expand or collapse section'
  },
  zh: {
    main: '主导航',
    sidebar: '全书目录',
    pager: '上一页与下一页',
    mobile: '移动导航',
    extra: '更多导航',
    toggle: '展开或收起分组'
  },
  root: {
    main: 'Main navigation / 主导航',
    sidebar: 'Book contents / 全书目录',
    pager: 'Previous and next page / 上一页与下一页',
    mobile: 'Mobile navigation / 移动导航',
    extra: 'More navigation / 更多导航',
    toggle: 'Expand or collapse section / 展开或收起分组'
  }
} as const

function localizeNavigationLabels() {
  const locale = location.pathname.startsWith('/zh/')
    ? 'zh'
    : location.pathname.startsWith('/en/')
      ? 'en'
      : 'root'
  const labels = navigationLabels[locale]
  const textLabels = [
    ['#main-nav-aria-label', labels.main],
    ['#sidebar-aria-label', labels.sidebar],
    ['#doc-footer-aria-label', labels.pager]
  ] as const
  for (const [selector, text] of textLabels) {
    const element = document.querySelector(selector)
    if (element) element.textContent = text
  }
  const ariaLabels = [
    ['.VPNavBarHamburger', labels.mobile],
    ['.VPNavBarExtra > .button', labels.extra],
    ['.VPSidebarItem .caret', labels.toggle]
  ] as const
  for (const [selector, label] of ariaLabels) {
    for (const element of document.querySelectorAll(selector)) {
      element.setAttribute('aria-label', label)
    }
  }
  for (const shortcut of document.querySelectorAll('.DocSearch-Button-Keys')) {
    shortcut.setAttribute('aria-hidden', 'true')
    shortcut.closest('button')?.setAttribute(
      'aria-keyshortcuts',
      'Control+K Meta+K /'
    )
    const finalKey = shortcut.querySelector('.DocSearch-Button-Key:last-child')
    if (finalKey) {
      const key = finalKey.textContent?.trim()
      if (key) finalKey.setAttribute('data-search-key', key)
      finalKey.textContent = ''
    }
  }
}

const LocalizedLayout = defineComponent({
  setup() {
    const route = useRoute()
    onMounted(localizeNavigationLabels)
    watch(
      () => route.path,
      () => nextTick(localizeNavigationLabels)
    )
    return () => h(DefaultTheme.Layout)
  }
})

export default {
  extends: DefaultTheme,
  Layout: LocalizedLayout
}
