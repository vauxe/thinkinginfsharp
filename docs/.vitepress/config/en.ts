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
      '/en/part-03/': [
        {
          text: 'Part III · Composition and program structure',
          items: [
            { text: '13 · Composition, Argument Order, and Pipeline APIs', link: '/en/part-03/ch-13-composition-pipeline-api' },
            { text: '14 · Choosing Collections and Evaluation Models', link: '/en/part-03/ch-14-collections-evaluation' },
            { text: '15 · Active Patterns and Domain Matching Boundaries', link: '/en/part-03/ch-15-active-patterns' },
            { text: '16 · Modules, Namespaces, Projects, and Compiler Settings', link: '/en/part-03/ch-16-modules-namespaces-projects' },
            { text: '17 · Signatures, Access Control, and F#-Facing APIs', link: '/en/part-03/ch-17-signatures-encapsulation' },
            { text: '18 · Explicit Workflow Composition and Validation Accumulation', link: '/en/part-03/ch-18-workflow-validation' }
          ]
        }
      ],
      '/en/part-04/': [
        {
          text: 'Part IV · Effects, asynchrony, and concurrency',
          items: [
            { text: '19 · .NET APIs and Null Boundaries', link: '/en/part-04/ch-19-dotnet-null-boundaries' }
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
            { text: 'Chapter 12', link: '/en/solutions/ch-12-making-illegal-states-unrepresentable' },
            { text: 'Chapter 13', link: '/en/solutions/ch-13-composition-pipeline-api' },
            { text: 'Chapter 14', link: '/en/solutions/ch-14-collections-evaluation' },
            { text: 'Chapter 15', link: '/en/solutions/ch-15-active-patterns' },
            { text: 'Chapter 16', link: '/en/solutions/ch-16-modules-namespaces-projects' },
            { text: 'Chapter 17', link: '/en/solutions/ch-17-signatures-encapsulation' },
            { text: 'Chapter 18', link: '/en/solutions/ch-18-workflow-validation' },
            { text: 'Chapter 19', link: '/en/solutions/ch-19-dotnet-null-boundaries' }
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
