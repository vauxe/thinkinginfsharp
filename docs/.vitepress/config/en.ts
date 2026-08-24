export const enLocale = {
  label: 'English',
  lang: 'en',
  link: '/en/',
  title: 'Thinking in F#',
  description: 'Learn functional modeling and production .NET engineering from F# itself.',
  themeConfig: {
    nav: [
      { text: 'Home', link: '/en/' },
      { text: 'Start reading', link: '/en/part-01/ch-01-first-session' }
    ],
    sidebar: {
      '/en/part-01/': [
        {
          text: 'Part I · Expressions and functions',
          items: [
            { text: '1 · A First F# Session', link: '/en/part-01/ch-01-first-session' },
            { text: '2 · Values, Bindings, and Expressions', link: '/en/part-01/ch-02-values-bindings-expressions' },
            { text: '3 · Functions Are Values', link: '/en/part-01/ch-03-functions-as-values' },
            { text: '4 · Branching and Basic Patterns', link: '/en/part-01/ch-04-branching-patterns' }
          ]
        }
      ],
      '/en/solutions/': [
        {
          text: 'Solutions',
          items: [
            { text: 'Chapter 1', link: '/en/solutions/ch-01-first-session' },
            { text: 'Chapter 2', link: '/en/solutions/ch-02-values-bindings-expressions' },
            { text: 'Chapter 3', link: '/en/solutions/ch-03-functions-as-values' },
            { text: 'Chapter 4', link: '/en/solutions/ch-04-branching-patterns' }
          ]
        }
      ]
    },
    outline: { label: 'On this page', level: [2, 3] as [number, number] },
    lastUpdated: { text: 'Last updated' },
    docFooter: { prev: 'Previous chapter', next: 'Next chapter' },
    darkModeSwitchLabel: 'Appearance',
    lightModeSwitchTitle: 'Switch to light theme',
    darkModeSwitchTitle: 'Switch to dark theme',
    sidebarMenuLabel: 'Contents',
    returnToTopLabel: 'Return to top',
    langMenuLabel: 'Change language',
    skipToContentLabel: 'Skip to content'
  }
} as const
