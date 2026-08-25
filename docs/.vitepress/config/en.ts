import { enSidebar } from './navigation'

export const enLocale = {
  label: 'English',
  lang: 'en',
  link: '/',
  title: 'Thinking in F#',
  description: 'Learn functional modeling and production .NET engineering from F# itself.',
  themeConfig: {
    sidebar: enSidebar,
    search: {
      provider: 'local',
      options: {
        detailedView: 'auto',
        disableQueryPersistence: true,
        translations: {
          button: {
            buttonText: 'Search',
            buttonAriaLabel: 'Search this book'
          },
          modal: {
            displayDetails: 'Display detailed results',
            resetButtonTitle: 'Reset search',
            backButtonTitle: 'Close search',
            noResultsText: 'No results for',
            footer: {
              selectText: 'to select',
              selectKeyAriaLabel: 'Enter',
              navigateText: 'to navigate',
              navigateUpKeyAriaLabel: 'Up arrow',
              navigateDownKeyAriaLabel: 'Down arrow',
              closeText: 'to close',
              closeKeyAriaLabel: 'Escape'
            }
          }
        }
      }
    },
    outline: { label: 'On this page', level: 2 },
    docFooter: { prev: 'Previous', next: 'Next' },
    notFound: {
      title: 'Page not found',
      quote: 'The address is invalid or the page has moved.',
      linkLabel: 'Go to the book contents',
      linkText: 'Go to contents'
    },
    darkModeSwitchLabel: 'Appearance',
    lightModeSwitchTitle: 'Switch to light theme',
    darkModeSwitchTitle: 'Switch to dark theme',
    sidebarMenuLabel: 'Contents',
    returnToTopLabel: 'Return to top',
    langMenuLabel: 'Change language',
    skipToContentLabel: 'Skip to content'
  }
} as const
