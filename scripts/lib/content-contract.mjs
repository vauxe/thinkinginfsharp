const PAGE_KINDS = new Set([
  'appendix',
  'chapter',
  'glossary',
  'guide',
  'home',
  'index',
  'preface',
  'solution'
])

const PAGE_STATUSES = new Set(['draft', 'review', 'complete'])
const STABLE_ID = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/
const TRANSLATION_KEY =
  /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\/[a-z0-9]+(?:-[a-z0-9]+)*)*$/
const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/
const SHARED_EXAMPLE_PREFIX = '@/../examples/'

// Metadata arrays are ordered content identities, not prose tags:
// - exampleIds: runnable examples presented on the page;
// - exerciseIds: exercises presented or answered on the page;
// - termIds: terminology first defined on the page;
// - sources: factual sources checked for this page.

function isPlainObject(value) {
  return (
    typeof value === 'object' &&
    value !== null &&
    !Array.isArray(value)
  )
}

function addError(errors, relativePath, message) {
  errors.push(`${relativePath}: ${message}`)
}

function requireText(frontmatter, field, relativePath, errors) {
  if (
    typeof frontmatter[field] !== 'string' ||
    frontmatter[field].trim().length === 0
  ) {
    addError(errors, relativePath, `${field} must be a non-empty string`)
  }
}

function isValidDateOnly(value) {
  if (typeof value !== 'string') return false

  const match = DATE_ONLY.exec(value)
  if (!match) return false

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])
  const date = new Date(Date.UTC(year, month - 1, day))

  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  )
}

function validateIdArray(frontmatter, field, relativePath, errors) {
  const value = frontmatter[field]
  if (!Array.isArray(value)) {
    addError(errors, relativePath, `${field} must be an array`)
    return
  }

  const seen = new Set()
  for (const [index, id] of value.entries()) {
    if (typeof id !== 'string' || !STABLE_ID.test(id)) {
      addError(
        errors,
        relativePath,
        `${field}[${index}] must be a stable kebab-case identifier`
      )
      continue
    }
    if (seen.has(id)) {
      addError(errors, relativePath, `${field} contains duplicate identifier "${id}"`)
    }
    seen.add(id)
  }
}

function validateSources(frontmatter, relativePath, errors) {
  const { sources } = frontmatter
  if (!Array.isArray(sources)) {
    addError(errors, relativePath, 'sources must be an array')
    return
  }

  const seen = new Set()
  for (const [index, source] of sources.entries()) {
    const field = `sources[${index}]`
    if (!isPlainObject(source)) {
      addError(errors, relativePath, `${field} must be an object`)
      continue
    }

    if (typeof source.id !== 'string' || !STABLE_ID.test(source.id)) {
      addError(errors, relativePath, `${field}.id must be a stable kebab-case identifier`)
    } else if (seen.has(source.id)) {
      addError(errors, relativePath, `sources contains duplicate id "${source.id}"`)
    } else {
      seen.add(source.id)
    }

    try {
      const url = new URL(source.url)
      if (url.protocol !== 'https:') {
        addError(errors, relativePath, `${field}.url must use https`)
      }
      if (url.username || url.password) {
        addError(errors, relativePath, `${field}.url must not contain credentials`)
      }
    } catch {
      addError(errors, relativePath, `${field}.url must be an absolute https URL`)
    }

    if (!isValidDateOnly(source.checked)) {
      addError(errors, relativePath, `${field}.checked must be a valid YYYY-MM-DD date`)
    }
  }
}

function validateVerifiedWith(frontmatter, relativePath, errors) {
  const { verifiedWith } = frontmatter
  if (!isPlainObject(verifiedWith)) {
    addError(errors, relativePath, 'verifiedWith must be an object')
    return
  }

  if (
    typeof verifiedWith.fsharp !== 'string' ||
    !/^\d+(?:\.\d+)?$/.test(verifiedWith.fsharp)
  ) {
    addError(errors, relativePath, 'verifiedWith.fsharp must be a language version')
  }

  if (
    typeof verifiedWith.dotnetSdk !== 'string' ||
    !/^\d+\.\d+\.\d+$/.test(verifiedWith.dotnetSdk)
  ) {
    addError(errors, relativePath, 'verifiedWith.dotnetSdk must be a full SDK version')
  }
}

function localeRelativeKey(relativePath) {
  if (typeof relativePath !== 'string') return undefined
  const normalized = relativePath.replaceAll('\\', '/')
  const match = /^(?:zh|en)\/(.+)\.md$/.exec(normalized)
  return match?.[1]
}

export function validatePageFrontmatter(frontmatter, relativePath) {
  const errors = []
  const page = isPlainObject(frontmatter) ? frontmatter : {}

  if (!isPlainObject(frontmatter)) {
    addError(errors, relativePath, 'frontmatter must be an object')
  }

  requireText(page, 'title', relativePath, errors)
  requireText(page, 'description', relativePath, errors)
  requireText(page, 'translationKey', relativePath, errors)

  if (
    typeof page.translationKey === 'string' &&
    !TRANSLATION_KEY.test(page.translationKey)
  ) {
    addError(
      errors,
      relativePath,
      'translationKey must contain lowercase path segments in kebab case'
    )
  }

  const expectedKey = localeRelativeKey(relativePath)
  if (!expectedKey) {
    addError(errors, relativePath, 'page path must begin with zh/ or en/ and end in .md')
  } else if (page.translationKey !== expectedKey) {
    addError(
      errors,
      relativePath,
      `translationKey must match the locale-relative path "${expectedKey}"`
    )
  }

  if (!PAGE_KINDS.has(page.kind)) {
    addError(
      errors,
      relativePath,
      `kind must be one of: ${[...PAGE_KINDS].join(', ')}`
    )
  }

  if (!PAGE_STATUSES.has(page.status)) {
    addError(
      errors,
      relativePath,
      `status must be one of: ${[...PAGE_STATUSES].join(', ')}`
    )
  }

  for (const field of ['exampleIds', 'exerciseIds', 'termIds']) {
    validateIdArray(page, field, relativePath, errors)
  }
  validateSources(page, relativePath, errors)

  if (page.kind === 'chapter' || page.kind === 'solution') {
    if (!Number.isInteger(page.part) || page.part < 1 || page.part > 7) {
      addError(errors, relativePath, 'part must be an integer from 1 to 7')
    }
    if (!Number.isInteger(page.chapter) || page.chapter < 1 || page.chapter > 45) {
      addError(errors, relativePath, 'chapter must be an integer from 1 to 45')
    }
    validateVerifiedWith(page, relativePath, errors)
  } else {
    if (page.part !== undefined) {
      addError(errors, relativePath, 'part is only valid for chapter and solution pages')
    }
    if (page.chapter !== undefined) {
      addError(errors, relativePath, 'chapter is only valid for chapter and solution pages')
    }
  }

  if (page.kind === 'appendix' && !/^[A-H]$/.test(page.appendix ?? '')) {
    addError(errors, relativePath, 'appendix must be a letter from A to H')
  }

  if (
    page.kind !== 'chapter' &&
    page.kind !== 'solution' &&
    Array.isArray(page.exampleIds) &&
    page.exampleIds.length > 0
  ) {
    validateVerifiedWith(page, relativePath, errors)
  }

  if (
    page.kind === 'chapter' &&
    Array.isArray(page.sources) &&
    page.sources.length === 0
  ) {
    addError(errors, relativePath, 'chapter pages must cite at least one source')
  }

  return errors
}

export function assertPageFrontmatter(frontmatter, relativePath) {
  const errors = validatePageFrontmatter(frontmatter, relativePath)
  if (errors.length > 0) {
    const error = new Error(['Invalid content metadata:', ...errors].join('\n- '))
    error.name = 'ContentContractError'
    error.errors = errors
    throw error
  }
  return frontmatter
}

export function validateTerminology(catalog, relativePath = 'terminology.json') {
  const errors = []
  const data = isPlainObject(catalog) ? catalog : {}

  if (!isPlainObject(catalog)) {
    addError(errors, relativePath, 'terminology catalog must be an object')
  }
  if (data.schemaVersion !== 1) {
    addError(errors, relativePath, 'schemaVersion must be 1')
  }
  if (!isPlainObject(data.terms)) {
    addError(errors, relativePath, 'terms must be an object keyed by stable term ids')
    return errors
  }
  if (Object.keys(data.terms).length === 0) {
    addError(errors, relativePath, 'terms must contain at least one entry')
  }

  for (const [id, term] of Object.entries(data.terms)) {
    const field = `terms.${id}`
    if (!STABLE_ID.test(id)) {
      addError(errors, relativePath, `${field} must use a stable kebab-case id`)
    }
    if (!isPlainObject(term)) {
      addError(errors, relativePath, `${field} must be an object`)
      continue
    }

    for (const locale of ['zh', 'en']) {
      const localized = term[locale]
      const localizedField = `${field}.${locale}`
      if (!isPlainObject(localized)) {
        addError(errors, relativePath, `${localizedField} must be an object`)
        continue
      }

      for (const property of ['preferred', 'definition']) {
        if (
          typeof localized[property] !== 'string' ||
          localized[property].trim().length === 0
        ) {
          addError(
            errors,
            relativePath,
            `${localizedField}.${property} must be a non-empty string`
          )
        }
      }

      if (localized.aliases !== undefined) {
        if (!Array.isArray(localized.aliases)) {
          addError(errors, relativePath, `${localizedField}.aliases must be an array`)
          continue
        }

        const seen = new Set()
        for (const [index, alias] of localized.aliases.entries()) {
          if (typeof alias !== 'string' || alias.trim().length === 0) {
            addError(
              errors,
              relativePath,
              `${localizedField}.aliases[${index}] must be a non-empty string`
            )
          } else if (alias === localized.preferred || seen.has(alias)) {
            addError(
              errors,
              relativePath,
              `${localizedField}.aliases contains duplicate preferred or alias "${alias}"`
            )
          }
          seen.add(alias)
        }
      }
    }
  }

  return errors
}

// VitePress resolves @/ from the documentation root. Requiring this explicit
// prefix keeps both editions on the same executable files in ../examples/.
// Syntax source: https://github.com/vuejs/vitepress/blob/v1.6.4/docs/en/guide/markdown.md#import-code-snippets
export function validateSharedCodeReference(reference) {
  const errors = []
  if (typeof reference !== 'string' || reference.trim().length === 0) {
    return ['code reference must be a non-empty string']
  }

  let normalized = reference.trim()
  normalized = normalized.replace(/\s+\[[^\]\r\n]+\]$/, '')
  normalized = normalized.replace(/\{[^{}\r\n]+\}$/, '')
  normalized = normalized.replace(/#[A-Za-z0-9_-]+$/, '')

  if (!normalized.startsWith(SHARED_EXAMPLE_PREFIX)) {
    return [
      `code reference must begin with "${SHARED_EXAMPLE_PREFIX}"`
    ]
  }

  const examplePath = normalized.slice(SHARED_EXAMPLE_PREFIX.length)
  const segments = examplePath.split('/')

  if (
    examplePath.length === 0 ||
    examplePath.includes('\\') ||
    segments.some((segment) => segment === '' || segment === '.' || segment === '..')
  ) {
    errors.push('code reference must be a normalized path inside examples/')
  }

  if (
    segments.some(
      (segment) => segment.length > 0 && !/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(segment)
    )
  ) {
    errors.push('code reference path segments may contain only letters, numbers, dot, dash, and underscore')
  }

  const filename = segments.at(-1) ?? ''
  if (!filename.includes('.') || filename.endsWith('.')) {
    errors.push('code reference must name a file')
  }

  return errors
}
