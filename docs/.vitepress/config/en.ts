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
            { text: '19 · .NET APIs and Null Boundaries', link: '/en/part-04/ch-19-dotnet-null-boundaries' },
            { text: '20 · Functional Core and Effect Boundaries', link: '/en/part-04/ch-20-functional-core-effects' },
            { text: '21 · Exceptions, Resources, and I/O', link: '/en/part-04/ch-21-exceptions-resources-io' },
            { text: "22 · Async<'T> and Task<'T>", link: '/en/part-04/ch-22-async-task' },
            { text: '23 · Cancellation, Timeouts, Faults, and Disposal', link: '/en/part-04/ch-23-cancellation-timeouts' },
            { text: '24 · Parallelism, Concurrency, Agents, and State', link: '/en/part-04/ch-24-concurrency-agents-state' }
          ]
        }
      ],
      '/en/part-05/': [
        {
          text: 'Part V · .NET interop and engineering quality',
          items: [
            { text: '25 · Defining Objects in F#', link: '/en/part-05/ch-25-objects-interfaces' },
            { text: '26 · Deeper .NET Boundaries', link: '/en/part-05/ch-26-dotnet-runtime-boundaries' },
            { text: '27 · Designing F# APIs for C#', link: '/en/part-05/ch-27-fsharp-api-for-csharp' },
            { text: '28 · Example Tests, Doubles, and Boundary Tests', link: '/en/part-05/ch-28-testing-boundaries' },
            { text: '29 · Property Testing with FsCheck', link: '/en/part-05/ch-29-property-testing' },
            { text: '30 · Diagnostics, Tooling, and Reproducible Builds', link: '/en/part-05/ch-30-diagnostics-tooling-builds' },
            { text: '31 · Measure Before Optimizing', link: '/en/part-05/ch-31-measure-before-optimizing' },
            { text: '32 · From Functions to Applications', link: '/en/part-05/ch-32-functions-to-applications' }
          ]
        }
      ],
      '/en/part-06/': [
        {
          text: 'Part VI · The booking system',
          items: [
            { text: '33 · Business Language, Commands, Events, and Model', link: '/en/part-06/ch-33-domain-language-model' }
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
            { text: 'Chapter 19', link: '/en/solutions/ch-19-dotnet-null-boundaries' },
            { text: 'Chapter 20', link: '/en/solutions/ch-20-functional-core-effects' },
            { text: 'Chapter 21', link: '/en/solutions/ch-21-exceptions-resources-io' },
            { text: 'Chapter 22', link: '/en/solutions/ch-22-async-task' },
            { text: 'Chapter 23', link: '/en/solutions/ch-23-cancellation-timeouts' },
            { text: 'Chapter 24', link: '/en/solutions/ch-24-concurrency-agents-state' },
            { text: 'Chapter 25', link: '/en/solutions/ch-25-objects-interfaces' },
            { text: 'Chapter 26', link: '/en/solutions/ch-26-dotnet-runtime-boundaries' },
            { text: 'Chapter 27', link: '/en/solutions/ch-27-fsharp-api-for-csharp' },
            { text: 'Chapter 28', link: '/en/solutions/ch-28-testing-boundaries' },
            { text: 'Chapter 29', link: '/en/solutions/ch-29-property-testing' },
            { text: 'Chapter 30', link: '/en/solutions/ch-30-diagnostics-tooling-builds' },
            { text: 'Chapter 31', link: '/en/solutions/ch-31-measure-before-optimizing' },
            { text: 'Chapter 32', link: '/en/solutions/ch-32-functions-to-applications' },
            { text: 'Chapter 33', link: '/en/solutions/ch-33-domain-language-model' }
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
