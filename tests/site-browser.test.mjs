import assert from 'node:assert/strict'
import { readFile, stat } from 'node:fs/promises'
import { createServer } from 'node:http'
import { dirname, extname, relative, resolve, sep } from 'node:path'
import test from 'node:test'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { gzip as gzipCallback } from 'node:zlib'

import { chromium } from 'playwright-core'

const gzip = promisify(gzipCallback)
const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(scriptDirectory, '..')
const distributionRoot = resolve(repositoryRoot, 'docs/.vitepress/dist')

const contentTypes = new Map([
  ['.css', 'text/css; charset=utf-8'],
  ['.html', 'text/html; charset=utf-8'],
  ['.ico', 'image/x-icon'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.map', 'application/json; charset=utf-8'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml'],
  ['.txt', 'text/plain; charset=utf-8'],
  ['.webmanifest', 'application/manifest+json'],
  ['.woff2', 'font/woff2']
])

const compressibleExtensions = new Set([
  '.css',
  '.html',
  '.js',
  '.json',
  '.map',
  '.svg',
  '.txt',
  '.webmanifest'
])

function isInside(root, candidate) {
  const difference = relative(root, candidate)
  return difference === '' || (
    difference !== '..' &&
    !difference.startsWith(`..${sep}`)
  )
}

async function existingFile(candidates) {
  for (const candidate of candidates) {
    try {
      if ((await stat(candidate)).isFile()) return candidate
    } catch (error) {
      if (!error || typeof error !== 'object' || error.code !== 'ENOENT') throw error
    }
  }
  return undefined
}

async function resolveRequestPath(requestUrl) {
  let pathname
  try {
    pathname = decodeURIComponent(
      new URL(requestUrl, 'http://127.0.0.1').pathname
    )
  } catch {
    return undefined
  }

  const relativePath = pathname === '/' ? '' : pathname.replace(/^\/+/, '')
  const exact = resolve(distributionRoot, relativePath)
  if (!isInside(distributionRoot, exact)) return undefined

  const candidates = pathname === '/'
    ? [resolve(distributionRoot, 'index.html')]
    : pathname.endsWith('/')
      ? [resolve(exact, 'index.html')]
      : [exact, `${exact}.html`, resolve(exact, 'index.html')]

  const path = await existingFile(candidates)
  return path && isInside(distributionRoot, path) ? path : undefined
}

function startStaticServer() {
  const server = createServer(async (request, response) => {
    try {
      if (!['GET', 'HEAD'].includes(request.method ?? 'GET')) {
        response.writeHead(405, { Allow: 'GET, HEAD' }).end('Method not allowed')
        return
      }

      const path = await resolveRequestPath(request.url ?? '/')
      if (!path) {
        response.writeHead(404).end('Not found')
        return
      }

      const extension = extname(path)
      const body = await readFile(path)
      const acceptsGzip = /(?:^|,|\s)gzip(?:,|\s|$)/i.test(
        request.headers['accept-encoding'] ?? ''
      )
      const useGzip = acceptsGzip && compressibleExtensions.has(extension)
      const payload = useGzip ? await gzip(body, { level: 9 }) : body
      const headers = {
        'Cache-Control': 'no-store',
        'Content-Length': String(payload.length),
        'Content-Type': contentTypes.get(extension) ?? 'application/octet-stream',
        Vary: 'Accept-Encoding'
      }
      if (useGzip) headers['Content-Encoding'] = 'gzip'

      response.writeHead(200, headers)
      response.end(request.method === 'HEAD' ? undefined : payload)
    } catch (error) {
      response.writeHead(500).end('Internal error')
      console.error(error instanceof Error ? error.stack : String(error))
    }
  })

  return new Promise((resolvePromise, rejectPromise) => {
    server.once('error', rejectPromise)
    server.listen(0, '127.0.0.1', () => resolvePromise(server))
  })
}

async function closeServer(server) {
  server.closeAllConnections?.()
  await new Promise((resolvePromise, rejectPromise) => {
    server.close(error => error ? rejectPromise(error) : resolvePromise())
  })
}

function launchOptions() {
  return process.env.CHROME_PATH
    ? { executablePath: process.env.CHROME_PATH, headless: true }
    : { channel: process.env.PLAYWRIGHT_CHANNEL ?? 'chrome', headless: true }
}

function monitorRuntime(page) {
  const problems = []
  page.on('console', message => {
    if (['error', 'warning'].includes(message.type())) {
      problems.push(`console ${message.type()}: ${message.text()}`)
    }
  })
  page.on('pageerror', error => problems.push(`page error: ${error.message}`))
  page.on('requestfailed', request => {
    problems.push(
      `request failed: ${request.method()} ${request.url()} (${request.failure()?.errorText ?? 'unknown'})`
    )
  })
  page.on('response', response => {
    if (response.status() >= 400) {
      problems.push(`HTTP ${response.status()}: ${response.url()}`)
    }
  })
  return problems
}

async function gotoRoute(page, origin, route) {
  const response = await page.goto(`${origin}${route}`, { waitUntil: 'networkidle' })
  assert.equal(response?.status(), 200, `${route} did not return HTTP 200`)
}

async function assertNoPageOverflow(page, label) {
  const widths = await page.evaluate(() => ({
    body: document.body.scrollWidth,
    document: document.documentElement.scrollWidth,
    viewport: window.innerWidth
  }))
  assert(
    widths.body <= widths.viewport + 1,
    `${label}: body width ${widths.body} exceeds viewport ${widths.viewport}`
  )
  assert(
    widths.document <= widths.viewport + 1,
    `${label}: document width ${widths.document} exceeds viewport ${widths.viewport}`
  )
}

async function assertSemanticStructure(page, expectedLang, h1Pattern) {
  await page.locator('html').waitFor()
  await page.waitForFunction(
    lang => document.documentElement.lang === lang,
    expectedLang
  )
  await page.waitForFunction(
    ({ source, flags }) => {
      const heading = document.querySelector('h1')?.innerText.trim() ?? ''
      return new RegExp(source, flags).test(heading)
    },
    { source: h1Pattern.source, flags: h1Pattern.flags }
  )
  assert.equal(await page.locator('html').getAttribute('lang'), expectedLang)
  assert.equal(await page.locator('main').count(), 1, 'The page must expose one main landmark.')
  assert.equal(await page.locator('h1').count(), 1, 'The page must expose one h1.')
  assert.match((await page.locator('h1').innerText()).trim(), h1Pattern)

  const issues = await page.evaluate(() => {
    const isVisible = element => {
      const style = getComputedStyle(element)
      const box = element.getBoundingClientRect()
      return style.display !== 'none' && style.visibility !== 'hidden' && box.width > 0 && box.height > 0
    }
    const ids = [...document.querySelectorAll('[id]')].map(element => element.id)
    const duplicateIds = [...new Set(ids.filter((id, index) => ids.indexOf(id) !== index))]
    const unnamedNavigations = [...document.querySelectorAll('nav')]
      .filter(isVisible)
      .filter(element => !element.getAttribute('aria-label') && !element.getAttribute('aria-labelledby'))
      .map(element => element.className || '<nav>')
    const unnamedControls = [...document.querySelectorAll('button, input, select, textarea')]
      .filter(isVisible)
      .filter(element => {
        const id = element.getAttribute('id')
        const hasLabel = id && document.querySelector(`label[for="${CSS.escape(id)}"]`)
        return !(
          element.getAttribute('aria-label') ||
          element.getAttribute('aria-labelledby') ||
          element.getAttribute('title') ||
          element.textContent?.trim() ||
          hasLabel
        )
      })
      .map(element => element.outerHTML.slice(0, 160))
    const imagesWithoutAlt = [...document.querySelectorAll('img:not([alt])')]
      .filter(isVisible)
      .map(element => element.getAttribute('src'))
    const headingLevels = [...document.querySelectorAll('main h1, main h2, main h3, main h4, main h5, main h6')]
      .map(element => Number(element.tagName.slice(1)))
    const skippedHeadings = headingLevels
      .slice(1)
      .flatMap((level, index) => level > headingLevels[index] + 1
        ? [`h${headingLevels[index]} -> h${level}`]
        : [])

    return {
      duplicateIds,
      imagesWithoutAlt,
      skippedHeadings,
      unnamedControls,
      unnamedNavigations
    }
  })

  assert.deepEqual(issues, {
    duplicateIds: [],
    imagesWithoutAlt: [],
    skippedHeadings: [],
    unnamedControls: [],
    unnamedNavigations: []
  })
}

async function assertFocusIndicator(page) {
  await page.evaluate(() => {
    if (document.activeElement instanceof HTMLElement) document.activeElement.blur()
    window.scrollTo(0, 0)
  })
  await page.keyboard.press('Tab')
  const focused = await page.evaluate(() => {
    const element = document.activeElement
    if (!(element instanceof HTMLElement)) return undefined
    const style = getComputedStyle(element)
    return {
      className: element.className,
      outlineStyle: style.outlineStyle,
      outlineWidth: Number.parseFloat(style.outlineWidth),
      text: element.textContent?.trim()
    }
  })
  assert.equal(focused?.text, 'Skip to content')
  assert.match(String(focused?.className), /VPSkipLink/)
  assert.notEqual(focused?.outlineStyle, 'none')
  assert((focused?.outlineWidth ?? 0) >= 3)

  await page.keyboard.press('Enter')
  await page.waitForFunction(() => location.hash === '#VPContent')
}

async function sampleContrast(page, label) {
  const samples = await page.evaluate(() => {
    const parse = color => {
      const parts = color.match(/[\d.]+/g)?.map(Number) ?? []
      return parts.length >= 3 ? parts.slice(0, 3) : undefined
    }
    const luminance = rgb => {
      const channels = rgb.map(value => {
        const normalized = value / 255
        return normalized <= 0.04045
          ? normalized / 12.92
          : ((normalized + 0.055) / 1.055) ** 2.4
      })
      return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]
    }
    const ratio = (foreground, background) => {
      const light = Math.max(luminance(foreground), luminance(background))
      const dark = Math.min(luminance(foreground), luminance(background))
      return (light + 0.05) / (dark + 0.05)
    }
    const opaqueBackground = element => {
      let current = element
      while (current) {
        const color = getComputedStyle(current).backgroundColor
        if (color && !color.endsWith(', 0)') && color !== 'transparent') return parse(color)
        current = current.parentElement
      }
      return [255, 255, 255]
    }

    return [
      ['body text', document.querySelector('.vp-doc p')],
      ['body link', document.querySelector('.vp-doc a')]
    ].flatMap(([name, element]) => {
      if (!(element instanceof HTMLElement)) return []
      const foreground = parse(getComputedStyle(element).color)
      const background = opaqueBackground(element)
      return foreground && background ? [{ name, ratio: ratio(foreground, background) }] : []
    })
  })

  assert.equal(samples.length, 2, `${label}: expected two contrast samples`)
  for (const sample of samples) {
    assert(
      sample.ratio >= 4.5,
      `${label}: ${sample.name} contrast ${sample.ratio.toFixed(2)} is below 4.5:1`
    )
  }
}

async function assertRuntimeClean(problems, label) {
  await new Promise(resolvePromise => setImmediate(resolvePromise))
  assert.deepEqual(problems, [], `${label}: browser runtime problems were recorded`)
}

async function testSearch({
  browser,
  expectedIndex,
  expectedLang,
  h1Pattern,
  oppositeIndex,
  origin,
  query,
  route,
  searchButtonName,
  target
}) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  const page = await context.newPage()
  const problems = monitorRuntime(page)
  try {
    await gotoRoute(page, origin, route)
    await assertSemanticStructure(page, expectedLang, h1Pattern)
    const before = await page.evaluate(() => performance.getEntriesByType('resource')
      .map(entry => entry.name)
      .filter(name => name.includes('localSearchIndex')))
    assert.deepEqual(before, [], `${route}: search index loaded before search opened`)

    await page.getByRole('button', { name: searchButtonName, exact: true }).click()
    const searchbox = page.getByRole('searchbox')
    await searchbox.fill(query)
    const result = page.locator(`[role="listbox"] a[href="${target}"]`)
    await result.waitFor()

    const indexes = await page.evaluate(() => performance.getEntriesByType('resource')
      .map(entry => ({
        encodedBodySize: entry.encodedBodySize,
        name: entry.name,
        transferSize: entry.transferSize
      }))
      .filter(entry => entry.name.includes('localSearchIndex')))
    assert(indexes.some(entry => entry.name.includes(expectedIndex)))
    assert(!indexes.some(entry => entry.name.includes(oppositeIndex)))
    assert(indexes.every(entry => entry.encodedBodySize > 0))

    await result.click()
    await page.waitForURL(url => `${url.pathname}${url.hash}` === target)
    assert(await page.locator(target.slice(target.indexOf('#'))).isVisible())
    await assertRuntimeClean(problems, `${route} search`)
  } finally {
    await context.close()
  }
}

test('production site supports both languages, browser interactions, keyboard use, and narrow screens', {
  timeout: 180_000
}, async t => {
  const server = await startStaticServer()
  const address = server.address()
  assert(address && typeof address === 'object', 'The static server did not bind to a TCP port.')
  const origin = `http://127.0.0.1:${address.port}`

  let browser
  try {
    browser = await chromium.launch(launchOptions())

    await t.test('neutral root and desktop reading journey', async () => {
      const context = await browser.newContext({ viewport: { width: 1280, height: 900 } })
      await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin })
      const page = await context.newPage()
      const problems = monitorRuntime(page)
      try {
        await gotoRoute(page, origin, '/')
        await assertSemanticStructure(page, 'en', /F# 思维\s*\/\s*Thinking in F#/u)
        await assertNoPageOverflow(page, 'neutral root desktop')
        const choices = page.locator('.language-choice__item')
        assert.equal(await choices.count(), 2)
        assert.equal(await choices.nth(0).getAttribute('href'), '/zh/')
        assert.equal(await choices.nth(0).getAttribute('lang'), 'zh-Hans')
        assert.equal(await choices.nth(1).getAttribute('href'), '/en/')
        assert.equal(await choices.nth(1).getAttribute('lang'), 'en')

        await choices.nth(1).click()
        await page.waitForURL(`${origin}/en/`)
        await assertSemanticStructure(page, 'en', /^Thinking in F#/u)

        const chapter = '/en/part-01/ch-03-functions-as-values'
        await gotoRoute(page, origin, chapter)
        await assertSemanticStructure(page, 'en', /^Chapter 3: Functions Are Values/u)
        await page.getByRole('navigation', { name: 'Main navigation' }).waitFor()
        const searchButton = page.getByRole('button', {
          name: 'Search this book',
          exact: true
        })
        const searchShortcut = page.locator('.DocSearch-Button-Keys')
        assert.equal(
          await searchShortcut.getAttribute('aria-hidden'),
          'true',
          'The visible search shortcut must not become part of the button name.'
        )
        assert.equal(
          await searchButton.getAttribute('aria-keyshortcuts'),
          'Control+K Meta+K /'
        )
        const finalSearchKey = searchShortcut.locator('.DocSearch-Button-Key').last()
        assert.equal(await finalSearchKey.textContent(), '')
        assert.equal(await finalSearchKey.getAttribute('data-search-key'), 'K')
        assert.equal(
          await finalSearchKey.evaluate(element => getComputedStyle(element, '::after').content),
          '"K"'
        )
        await assertFocusIndicator(page)
        await page.keyboard.press('/')
        await page.getByRole('searchbox').waitFor()
        await page.keyboard.press('Escape')
        await page.getByRole('searchbox').waitFor({ state: 'hidden' })

        await gotoRoute(page, origin, chapter)
        const outlineAnchor = page.locator(
          'nav[aria-labelledby="doc-outline-aria-label"] a[href="#partial-application"]'
        )
        await outlineAnchor.click()
        await page.waitForURL(`${origin}${chapter}#partial-application`)
        assert(await page.locator('#partial-application').isVisible())

        const codeBlock = page.locator('.vp-doc div[class*="language-"]').first()
        const expectedCode = (await codeBlock.locator('code').innerText()).trim()
        const copyButton = codeBlock.locator('button.copy')
        assert.equal(await copyButton.getAttribute('title'), 'Copy code / 复制代码')
        await copyButton.click()
        await copyButton.evaluate(element => new Promise(resolvePromise => {
          if (element.classList.contains('copied')) resolvePromise()
          else {
            const observer = new MutationObserver(() => {
              if (!element.classList.contains('copied')) return
              observer.disconnect()
              resolvePromise()
            })
            observer.observe(element, { attributeFilter: ['class'], attributes: true })
          }
        }))
        assert.equal(
          (await page.evaluate(() => navigator.clipboard.readText())).trim(),
          expectedCode
        )

        const pager = page.getByRole('navigation', { name: 'Previous and next page' })
        assert.equal(
          await pager.getByRole('link', { name: /Previous/u }).getAttribute('href'),
          '/en/part-01/ch-02-values-bindings-expressions'
        )
        const next = pager.getByRole('link', { name: /Next/u })
        assert.equal(await next.getAttribute('href'), '/en/part-01/ch-04-branching-patterns')
        await next.click()
        await page.waitForURL(`${origin}/en/part-01/ch-04-branching-patterns`)
        await assertSemanticStructure(page, 'en', /^Chapter 4:/u)

        await gotoRoute(page, origin, chapter)
        await page.locator('.VPNavBarTranslations > button').click()
        await page.locator(
          '.VPNavBarTranslations a[href="/zh/part-01/ch-03-functions-as-values"]'
        ).click()
        await page.waitForURL(`${origin}/zh/part-01/ch-03-functions-as-values`)
        await assertSemanticStructure(page, 'zh-Hans', /^第 3 章：函数也是值/u)
        await page.getByRole('navigation', { name: '主导航' }).waitFor()
        assert.equal(
          (await page.locator('html').evaluate(element =>
            getComputedStyle(element).getPropertyValue('--vp-code-copy-copied-text-content')
          )).trim(),
          '"已复制"'
        )

        await page.locator('.VPNavBarTranslations > button').click()
        await page.locator(
          '.VPNavBarTranslations a[href="/en/part-01/ch-03-functions-as-values"]'
        ).click()
        await page.waitForURL(`${origin}${chapter}`)
        await assertSemanticStructure(page, 'en', /^Chapter 3:/u)

        await sampleContrast(page, 'light theme')
        const appearance = page.locator('.VPNavBarAppearance .VPSwitchAppearance')
        await appearance.click()
        await page.locator('html.dark').waitFor()
        await sampleContrast(page, 'dark theme')
        await assertNoPageOverflow(page, 'English chapter desktop')
        await assertRuntimeClean(problems, 'desktop reading journey')
      } finally {
        await context.close()
      }
    })

    await t.test('English search is lazy and locale-isolated', async () => {
      return testSearch({
        browser,
        expectedIndex: '@localSearchIndexen.',
        expectedLang: 'en',
        h1Pattern: /^Chapter 3:/u,
        oppositeIndex: '@localSearchIndexzh.',
        origin,
        query: 'partial application',
        route: '/en/part-01/ch-03-functions-as-values',
        searchButtonName: 'Search this book',
        target: '/en/part-01/ch-03-functions-as-values#partial-application'
      })
    })

    await t.test('Chinese search is lazy and locale-isolated', async () => {
      return testSearch({
        browser,
        expectedIndex: '@localSearchIndexzh.',
        expectedLang: 'zh-Hans',
        h1Pattern: /^第 3 章：/u,
        oppositeIndex: '@localSearchIndexen.',
        origin,
        query: '部分应用',
        route: '/zh/part-01/ch-03-functions-as-values',
        searchButtonName: '搜索本书',
        target: '/zh/part-01/ch-03-functions-as-values#partial-application'
      })
    })

    await t.test('360 px root, mobile navigation, and long pages do not overflow', async () => {
      const context = await browser.newContext({ viewport: { width: 360, height: 800 } })
      const page = await context.newPage()
      const problems = monitorRuntime(page)
      try {
        await gotoRoute(page, origin, '/')
        await assertSemanticStructure(page, 'en', /Thinking in F#/u)
        await assertNoPageOverflow(page, 'neutral root at 360 px')
        assert(await page.locator('.language-choice__item').nth(0).isVisible())
        assert(await page.locator('.language-choice__item').nth(1).isVisible())

        await gotoRoute(page, origin, '/zh/part-06/ch-37-consistency-idempotency')
        await assertSemanticStructure(page, 'zh-Hans', /^第 37 章：/u)
        const hamburger = page.getByRole('button', { name: '移动导航' })
        assert(await hamburger.isVisible())
        const hamburgerBox = await hamburger.boundingBox()
        assert((hamburgerBox?.width ?? 0) >= 24 && (hamburgerBox?.height ?? 0) >= 24)
        await hamburger.click()
        assert.equal(await hamburger.getAttribute('aria-expanded'), 'true')
        assert(await page.locator('#VPNavScreen').isVisible())
        await hamburger.click()
        assert.equal(await hamburger.getAttribute('aria-expanded'), 'false')

        const contents = page.locator('.VPLocalNav > .container > button.menu')
        assert.equal((await contents.innerText()).trim(), '目录')
        const contentsBox = await contents.boundingBox()
        assert((contentsBox?.width ?? 0) >= 24 && (contentsBox?.height ?? 0) >= 24)
        await contents.click()
        assert.equal(await contents.getAttribute('aria-expanded'), 'true')
        assert(await page.locator('#VPSidebarNav').isVisible())
        await page.keyboard.press('Escape')
        await contents.evaluate(element => new Promise(resolvePromise => {
          if (element.getAttribute('aria-expanded') === 'false') resolvePromise()
          else {
            const observer = new MutationObserver(() => {
              if (element.getAttribute('aria-expanded') !== 'false') return
              observer.disconnect()
              resolvePromise()
            })
            observer.observe(element, {
              attributeFilter: ['aria-expanded'],
              attributes: true
            })
          }
        }))
        await assertNoPageOverflow(page, 'Chinese consistency chapter at 360 px')

        const longRoutes = [
          ['/en/appendices/c-collections', 'en', /^Appendix C:/u],
          ['/zh/part-07/ch-44-unity', 'zh-Hans', /^第 44 章：/u]
        ]
        for (const [route, lang, heading] of longRoutes) {
          await gotoRoute(page, origin, route)
          await assertSemanticStructure(page, lang, heading)
          await assertNoPageOverflow(page, `${route} at 360 px`)
        }

        await assertRuntimeClean(problems, 'mobile reading journey')
      } finally {
        await context.close()
      }
    })

    console.log(`Browser exercised: ${await browser.version()}`)
  } finally {
    await browser?.close()
    await closeServer(server)
  }
})
