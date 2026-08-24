import assert from 'node:assert/strict'
import { access, readFile, stat } from 'node:fs/promises'
import { createServer } from 'node:http'
import { dirname, extname, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright-core'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(scriptDirectory, '..')
const distributionRoot = resolve(repositoryRoot, 'examples/ecosystem/fable/dist')

const contentTypes = new Map([
  ['.css', 'text/css; charset=utf-8'],
  ['.html', 'text/html; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.map', 'application/json; charset=utf-8'],
  ['.svg', 'image/svg+xml']
])

function isInside(root, candidate) {
  const difference = relative(root, candidate)
  return difference === '' || (
    difference !== '..' &&
    !difference.startsWith(`..${sep}`)
  )
}

async function resolveRequestPath(requestUrl) {
  const pathname = decodeURIComponent(new URL(requestUrl, 'http://127.0.0.1').pathname)
  const requestedPath = pathname === '/' ? 'index.html' : pathname.slice(1)
  const candidate = resolve(distributionRoot, requestedPath)

  if (!isInside(distributionRoot, candidate)) return undefined

  const candidateStat = await stat(candidate)
  return candidateStat.isDirectory() ? resolve(candidate, 'index.html') : candidate
}

function startStaticServer() {
  const server = createServer(async (request, response) => {
    try {
      const path = await resolveRequestPath(request.url ?? '/')
      if (!path || !isInside(distributionRoot, path)) {
        response.writeHead(403).end('Forbidden')
        return
      }

      const body = await readFile(path)
      response.writeHead(200, {
        'Cache-Control': 'no-store',
        'Content-Type': contentTypes.get(extname(path)) ?? 'application/octet-stream'
      })
      response.end(body)
    } catch (error) {
      const statusCode = error && typeof error === 'object' && error.code === 'ENOENT' ? 404 : 500
      response.writeHead(statusCode).end(statusCode === 404 ? 'Not found' : 'Internal error')
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

async function main() {
  await access(resolve(distributionRoot, 'index.html'))

  const server = await startStaticServer()
  const address = server.address()
  assert(address && typeof address === 'object', 'The static server did not bind to a TCP port.')

  const launchOptions = process.env.CHROME_PATH
    ? { executablePath: process.env.CHROME_PATH, headless: true }
    : { channel: process.env.PLAYWRIGHT_CHANNEL ?? 'chrome', headless: true }

  let browser
  try {
    browser = await chromium.launch(launchOptions)
    const page = await browser.newPage({ viewport: { width: 360, height: 800 } })
    const runtimeProblems = []

    page.on('console', message => {
      if (message.type() === 'error' || message.type() === 'warning') {
        runtimeProblems.push(`console ${message.type()}: ${message.text()}`)
      }
    })
    page.on('pageerror', error => runtimeProblems.push(`page error: ${error.message}`))
    page.on('requestfailed', request => {
      runtimeProblems.push(`request failed: ${request.method()} ${request.url()}`)
    })
    page.on('response', response => {
      if (response.status() >= 400) {
        runtimeProblems.push(`HTTP ${response.status()}: ${response.url()}`)
      }
    })

    const response = await page.goto(`http://127.0.0.1:${address.port}/`, {
      waitUntil: 'networkidle'
    })
    assert.equal(response?.status(), 200)

    await page.locator('html[data-fable-ready="true"]').waitFor()
    await assertText(page.getByRole('heading', { level: 1 }), 'Fable browser boundary')

    const count = page.getByTestId('count')
    await assertText(count, 'Count: 0')

    const increment = page.getByRole('button', { name: 'Increment count' })
    await increment.click()
    await increment.click()
    await increment.click()
    await assertText(count, 'Count: 3')

    await page.getByRole('button', { name: 'Reset count' }).click()
    await assertText(count, 'Count: 0')

    const layout = await page.evaluate(() => ({
      bodyWidth: document.body.scrollWidth,
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth
    }))
    assert(layout.bodyWidth <= layout.viewportWidth, 'The body overflows the 360 px viewport.')
    assert(layout.documentWidth <= layout.viewportWidth, 'The document overflows the 360 px viewport.')
    assert.deepEqual(runtimeProblems, [])

    console.log('Fable browser smoke passed: 0 -> 3 -> 0; no runtime errors or page overflow.')
  } finally {
    await browser?.close()
    await closeServer(server)
  }
}

async function assertText(locator, expected) {
  assert.equal((await locator.textContent())?.trim(), expected)
}

main().catch(error => {
  console.error(error instanceof Error ? error.stack : String(error))
  process.exitCode = 1
})
