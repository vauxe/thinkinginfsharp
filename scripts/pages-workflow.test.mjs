import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import { parse } from 'yaml'

const workflowPath = new URL('../.github/workflows/deploy-pages.yml', import.meta.url)

test('deploys the verified project-site build through GitHub Pages', () => {
  const workflow = parse(readFileSync(workflowPath, 'utf8'))
  const build = workflow.jobs.build
  const deploy = workflow.jobs.deploy
  const buildSteps = build.steps

  assert.deepEqual(Object.keys(workflow.on).sort(), ['push', 'workflow_dispatch'])
  assert.deepEqual(workflow.on.push.branches, ['main'])
  assert.equal(workflow.env.VITEPRESS_BASE, '/thinkinginfsharp/')
  assert.equal(build.permissions.contents, 'read')
  assert.equal(build.permissions.pages, 'write')
  assert.equal(buildSteps.find(step => step.name === 'Verify and build').run, 'pnpm test')
  assert.equal(
    buildSteps.find(step => step.uses === 'actions/upload-pages-artifact@v4').with.path,
    'docs/.vitepress/dist'
  )
  assert.equal(deploy.needs, 'build')
  assert.equal(deploy.environment.name, 'github-pages')
  assert.equal(deploy.permissions.pages, 'write')
  assert.equal(deploy.permissions['id-token'], 'write')
  assert(deploy.steps.some(step => step.uses === 'actions/deploy-pages@v4'))
})
