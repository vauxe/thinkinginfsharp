import { zhNavigation } from './navigation.generated'

export const zhLocale = {
  label: '简体中文',
  lang: 'zh-Hans',
  link: '/zh/',
  title: 'F# 思维',
  description: '从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。',
  themeConfig: {
    nav: zhNavigation.nav,
    sidebar: zhNavigation.sidebar,
    search: {
      provider: 'local',
      options: {
        translations: {
          button: {
            buttonText: '搜索',
            buttonAriaLabel: '搜索本书'
          },
          modal: {
            displayDetails: '显示详细结果',
            resetButtonTitle: '清空搜索',
            backButtonTitle: '关闭搜索',
            noResultsText: '没有找到',
            footer: {
              selectText: '选择',
              selectKeyAriaLabel: '回车键',
              navigateText: '切换结果',
              navigateUpKeyAriaLabel: '上方向键',
              navigateDownKeyAriaLabel: '下方向键',
              closeText: '关闭',
              closeKeyAriaLabel: 'Escape 键'
            }
          }
        }
      }
    },
    outline: { label: '本页目录', level: [2, 3] as [number, number] },
    lastUpdated: { text: '最后更新' },
    docFooter: { prev: '上一页', next: '下一页' },
    darkModeSwitchLabel: '外观',
    lightModeSwitchTitle: '切换到浅色主题',
    darkModeSwitchTitle: '切换到深色主题',
    sidebarMenuLabel: '目录',
    returnToTopLabel: '返回顶部',
    langMenuLabel: '切换语言',
    skipToContentLabel: '跳到正文'
  }
} as const
