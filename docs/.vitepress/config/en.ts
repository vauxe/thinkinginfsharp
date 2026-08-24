export const enLocale = {
  label: 'English',
  lang: 'en',
  link: '/en/',
  title: 'Thinking in F#',
  description: 'Learn functional modeling and production .NET engineering from F# itself.',
  themeConfig: {
    nav: [{ text: 'Home', link: '/en/' }],
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
