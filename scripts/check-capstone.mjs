import { spawn, spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(scriptDirectory, '..')
const processTimeoutMs = 5 * 60 * 1000
const serverTimeoutMs = 30 * 1000
const maximumOutputBytes = 2 * 1024 * 1024

function tail(value, maximumLength = 6000) {
  return value.length <= maximumLength ? value : value.slice(-maximumLength)
}

function run(label, arguments_, options = {}) {
  const result = spawnSync('dotnet', arguments_, {
    cwd: repositoryRoot,
    encoding: 'utf8',
    timeout: processTimeoutMs,
    maxBuffer: maximumOutputBytes,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      ...options.env
    }
  })

  if (result.error || result.status !== 0) {
    const detail = tail(`${result.stdout ?? ''}${result.stderr ?? ''}`.trim())
    throw new Error(
      `${label} failed${result.error ? `: ${result.error.message}` : ` with status ${String(result.status)}`}`
      + (detail ? `\n${detail}` : '')
    )
  }

  return result.stdout ?? ''
}

function requireCondition(condition, message) {
  if (!condition) throw new Error(message)
}

function startApi(snapshotPath) {
  const child = spawn(
    'dotnet',
    [
      'run',
      '--project',
      'examples/capstone/src/Booking.Api/Booking.Api.fsproj',
      '--configuration',
      'Release',
      '--no-build',
      '--',
      '--urls',
      'http://127.0.0.1:0'
    ],
    {
      cwd: repositoryRoot,
      stdio: ['ignore', 'pipe', 'pipe'],
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Production',
        BOOKING_STORE_PATH: snapshotPath,
        BOOKING_EVENT_ID: 'EVT-CAPSTONE-CHECK',
        BOOKING_CAPACITY: '4',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1'
      }
    }
  )

  let output = ''
  const waiters = new Set()

  const append = (chunk) => {
    output = tail(output + chunk.toString(), maximumOutputBytes)

    for (const notify of waiters) notify()
  }

  child.stdout.on('data', append)
  child.stderr.on('data', append)

  const waitForOutput = (predicate, label, timeoutMs = serverTimeoutMs) => new Promise((resolve_, reject) => {
    const inspect = () => {
      const value = predicate(output)
      if (!value) return false
      cleanup()
      resolve_(value)
      return true
    }

    const onExit = (code, signal) => {
      cleanup()
      reject(new Error(`Booking API exited before ${label}: code=${String(code)} signal=${String(signal)}\n${tail(output)}`))
    }

    const onError = (error) => {
      cleanup()
      reject(new Error(`Booking API failed before ${label}: ${error.message}\n${tail(output)}`))
    }

    const timer = setTimeout(() => {
      cleanup()
      reject(new Error(`Timed out waiting for ${label}.\n${tail(output)}`))
    }, timeoutMs)

    const cleanup = () => {
      clearTimeout(timer)
      waiters.delete(inspect)
      child.off('exit', onExit)
      child.off('error', onError)
    }

    waiters.add(inspect)
    child.once('exit', onExit)
    child.once('error', onError)
    inspect()
  })

  const stop = () => new Promise((resolve_) => {
    if (child.exitCode !== null || child.signalCode !== null) {
      resolve_()
      return
    }

    const timer = setTimeout(() => child.kill('SIGKILL'), 5000)
    child.once('exit', () => {
      clearTimeout(timer)
      resolve_()
    })
    child.kill('SIGTERM')
  })

  return {
    output: () => output,
    waitForOutput,
    stop
  }
}

async function main() {
  const temporaryDirectory = mkdtempSync(join(tmpdir(), 'thinking-in-fsharp-capstone-'))
  const snapshotPath = join(temporaryDirectory, 'bookings.json')
  let api

  try {
    run('locked solution restore', ['restore', 'ThinkingInFSharp.slnx', '--locked-mode'])
    run('Release solution build', [
      'build',
      'ThinkingInFSharp.slnx',
      '--configuration',
      'Release',
      '--no-restore'
    ])
    run('booking tests', [
      'test',
      'ThinkingInFSharp.slnx',
      '--configuration',
      'Release',
      '--no-build',
      '--filter',
      'FullyQualifiedName~Booking'
    ])

    api = startApi(snapshotPath)

    const baseAddress = await api.waitForOutput((output) => {
      const match = output.match(/Now listening on:\s+(http:\/\/127\.0\.0\.1:\d+)/)
      return match?.[1]
    }, 'the listening address')

    const clientOutput = run('C# contract client', [
      'run',
      '--project',
      'examples/capstone/clients/Booking.CSharpClient/Booking.CSharpClient.csproj',
      '--configuration',
      'Release',
      '--no-build',
      '--',
      `${baseAddress}/`,
      'REQ-CAPSTONE-CHECK'
    ])

    for (const expected of [
      'Placed: id=REQ-CAPSTONE-CHECK seats=2 status=pending',
      'Replay: status=201 same-body=True',
      'Confirmed: id=REQ-CAPSTONE-CHECK code=CONF-CSHARP status=confirmed',
      'Loaded: status=200 same-body=True'
    ]) {
      requireCondition(clientOutput.includes(expected), `C# client output is missing: ${expected}`)
    }

    const invalidResponse = await fetch(`${baseAddress}/api/bookings/place`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: '{not-json',
      signal: AbortSignal.timeout(10_000)
    })

    const invalidBody = await invalidResponse.json()
    const correlationId = invalidResponse.headers.get('x-correlation-id')

    requireCondition(invalidResponse.status === 400, `Invalid JSON returned ${invalidResponse.status}, expected 400.`)
    requireCondition(invalidBody?.code === 'invalid_json', 'Invalid JSON did not return the stable invalid_json code.')
    requireCondition(/^[0-9a-f]{32}$/.test(correlationId ?? ''), 'Response is missing a bounded W3C correlation ID.')

    await api.waitForOutput(
      (output) => output.includes(`correlationId=${correlationId}`) && output.includes('outcome=client_error'),
      'the correlated client-error diagnostic'
    )

    await api.waitForOutput(
      (output) => output.includes('Booking request completed') && output.includes('outcome=success'),
      'a success diagnostic'
    )

    const serverOutput = api.output()

    for (const forbidden of ['TX-LOCAL-STUB', 'CONF-CSHARP', snapshotPath]) {
      requireCondition(!serverOutput.includes(forbidden), `Server diagnostics leaked forbidden text: ${forbidden}`)
    }

    process.stdout.write('Capstone check passed.\n')
    process.stdout.write(clientOutput)
    process.stdout.write(`Diagnostics: success=true client-error=true correlation=${correlationId} secrets=false\n`)
  } finally {
    if (api) await api.stop()
    rmSync(temporaryDirectory, { recursive: true, force: true })
  }
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`)
  process.exitCode = 1
})
