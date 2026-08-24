export const zhLocale = {
  label: '简体中文',
  lang: 'zh-Hans',
  link: '/zh/',
  title: 'F# 思维',
  description: '从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。',
  themeConfig: {
    nav: [
      { text: '首页', link: '/zh/' },
      { text: '开始阅读', link: '/zh/part-01/ch-01-first-session' }
    ],
    sidebar: {
      '/zh/part-01/': [
        {
          text: '第一部分 · 表达式与函数',
          items: [
            { text: '第 1 章 · 第一次 F# 会话', link: '/zh/part-01/ch-01-first-session' },
            { text: '第 2 章 · 值、绑定与表达式', link: '/zh/part-01/ch-02-values-bindings-expressions' },
            { text: '第 3 章 · 函数也是值', link: '/zh/part-01/ch-03-functions-as-values' },
            { text: '第 4 章 · 分支与基本模式', link: '/zh/part-01/ch-04-branching-patterns' },
            { text: '第 5 章 · 列表、管道与数据流', link: '/zh/part-01/ch-05-lists-pipelines' },
            { text: '第 6 章 · 递归、尾调用与折叠', link: '/zh/part-01/ch-06-recursion-folds' }
          ]
        }
      ],
      '/zh/part-02/': [
        {
          text: '第二部分 · 用类型建立模型',
          items: [
            { text: '第 7 章 · 记录、更新、相等与比较', link: '/zh/part-02/ch-07-records-equality' },
            { text: '第 8 章 · 可辨识联合与状态建模', link: '/zh/part-02/ch-08-discriminated-unions' },
            { text: '第 9 章 · 缺失与预期失败', link: '/zh/part-02/ch-09-option-result' },
            { text: '第 10 章 · 递归类型与结构递归', link: '/zh/part-02/ch-10-recursive-types' }
          ]
        }
      ],
      '/zh/solutions/': [
        {
          text: '练习答案',
          items: [
            { text: '第 1 章', link: '/zh/solutions/ch-01-first-session' },
            { text: '第 2 章', link: '/zh/solutions/ch-02-values-bindings-expressions' },
            { text: '第 3 章', link: '/zh/solutions/ch-03-functions-as-values' },
            { text: '第 4 章', link: '/zh/solutions/ch-04-branching-patterns' },
            { text: '第 5 章', link: '/zh/solutions/ch-05-lists-pipelines' },
            { text: '第 6 章', link: '/zh/solutions/ch-06-recursion-folds' },
            { text: '第 7 章', link: '/zh/solutions/ch-07-records-equality' },
            { text: '第 8 章', link: '/zh/solutions/ch-08-discriminated-unions' },
            { text: '第 9 章', link: '/zh/solutions/ch-09-option-result' },
            { text: '第 10 章', link: '/zh/solutions/ch-10-recursive-types' }
          ]
        }
      ]
    },
    outline: { label: '本页目录', level: [2, 3] as [number, number] },
    lastUpdated: { text: '最后更新' },
    docFooter: { prev: '上一章', next: '下一章' },
    darkModeSwitchLabel: '外观',
    lightModeSwitchTitle: '切换到浅色主题',
    darkModeSwitchTitle: '切换到深色主题',
    sidebarMenuLabel: '目录',
    returnToTopLabel: '返回顶部',
    langMenuLabel: '切换语言',
    skipToContentLabel: '跳到正文'
  }
} as const
