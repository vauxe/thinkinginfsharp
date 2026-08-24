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
            { text: '4 · Branching and Basic Patterns', link: '/en/part-01/ch-04-branching-patterns' },
            { text: '5 · Lists, Pipelines, and Data Flow', link: '/en/part-01/ch-05-lists-pipelines' },
            { text: '6 · Recursion, Tail Calls, and Folds', link: '/en/part-01/ch-06-recursion-folds' }
          ]
        }
      ],
      '/en/part-02/': [
        {
          text: 'Part II · Modeling with types',
          items: [
            { text: '7 · Records, Updates, Equality, and Comparison', link: '/en/part-02/ch-07-records-equality' },
            { text: '8 · Discriminated Unions and State Modeling', link: '/en/part-02/ch-08-discriminated-unions' },
            { text: '9 · Absence and Expected Failure', link: '/en/part-02/ch-09-option-result' },
            { text: '10 · Recursive Types and Structural Recursion', link: '/en/part-02/ch-10-recursive-types' },
            { text: '11 · Generics, Constraints, and Units', link: '/en/part-02/ch-11-generics-constraints' },
            { text: '12 · Making Illegal States Unrepresentable', link: '/en/part-02/ch-12-making-illegal-states-unrepresentable' }
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
            { text: 'Chapter 4', link: '/en/solutions/ch-04-branching-patterns' },
            { text: 'Chapter 5', link: '/en/solutions/ch-05-lists-pipelines' },
            { text: 'Chapter 6', link: '/en/solutions/ch-06-recursion-folds' },
            { text: 'Chapter 7', link: '/en/solutions/ch-07-records-equality' },
            { text: 'Chapter 8', link: '/en/solutions/ch-08-discriminated-unions' },
            { text: 'Chapter 9', link: '/en/solutions/ch-09-option-result' },
            { text: 'Chapter 10', link: '/en/solutions/ch-10-recursive-types' },
            { text: 'Chapter 11', link: '/en/solutions/ch-11-generics-constraints' },
            { text: 'Chapter 12', link: '/en/solutions/ch-12-making-illegal-states-unrepresentable' }
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
