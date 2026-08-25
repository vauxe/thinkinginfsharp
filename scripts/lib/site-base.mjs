export function normalizeSiteBase(value = '/') {
  const base = value || '/'
  if (!base.startsWith('/') || !base.endsWith('/')) {
    throw new Error(`Site base must start and end with "/": ${base}`)
  }
  return base
}

export function withSiteBase(baseValue, route) {
  const base = normalizeSiteBase(baseValue)
  if (!route.startsWith('/')) {
    throw new Error(`Site route must start with "/": ${route}`)
  }
  if (base === '/') return route
  return route === '/' ? base : `${base.slice(0, -1)}${route}`
}

export function stripSiteBase(baseValue, pathname) {
  const base = normalizeSiteBase(baseValue)
  if (base === '/') return pathname

  const baseWithoutTrailingSlash = base.slice(0, -1)
  if (pathname === baseWithoutTrailingSlash) return '/'
  if (!pathname.startsWith(base)) return undefined
  return `/${pathname.slice(base.length)}`
}
