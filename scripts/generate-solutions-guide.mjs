import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  statSync,
  writeFileSync
} from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { parse } from 'yaml'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const defaultDocsDir = resolve(scriptDirectory, '../docs')
const DEFAULT_EXPECTED_CHAPTERS = 45
const LOCALES = ['en', 'zh']

const copy = {
  en: {
    language: 'English',
    title: 'Appendix G: Solutions and Open-Exercise Review Guide',
    description: 'Reach all 45 solution pages and review closed, diagnostic, and open design exercises without pretending that engineering has one canonical answer.',
    heading: 'Appendix G: Solutions and Open-Exercise Review Guide',
    introduction: [
      'A solution is feedback, not a substitute for attempting the exercise. Compare contracts, types, effects, and evidence before comparing surface syntax. Producing the same printed line can still miss a modeling, ownership, failure, or interoperability goal.',
      'Some exercises have a narrow observable result; others ask for a diagnosis or an engineering design. The solution pages therefore show reasoning, constraints, and representative implementations. They do not claim that every open question has one canonical answer.',
      'This page is generated from chapter and solution metadata. Its check requires one bilingual solution page for every chapter, identical exercise identifiers, reachable exercise anchors, and links in both directions.'
    ],
    beforeHeading: 'Before opening a solution',
    before: [
      'Restate the required behavior and every explicit constraint in your own words.',
      'Predict the important type signatures, output, failure, and effect order before running code.',
      'Run the narrowest relevant command and retain unexpected evidence instead of editing toward the book output blindly.',
      'Explain why your answer satisfies the task; then inspect the solution and compare decisions, not line counts.'
    ],
    kindsHeading: 'Three exercise kinds',
    kindsTable: `| Kind | What can be checked directly | Where variation remains |
|---|---|---|
| closed behavior | required value, type, output order, diagnostic, or test | names and implementation may differ while preserving the whole contract |
| diagnosis | reproduction command, first relevant evidence, root cause, and repair | several repairs may compile, but only those preserving intended semantics qualify |
| open design | constraints, invariants, boundary, failure policy, and verification plan | representation, library, architecture, and rollout may differ with explicit tradeoffs |`,
    rubricHeading: 'Rubric for open design exercises',
    rubricTable: `| Dimension | Meets the exercise | Strong evidence |
|---|---|---|
| contract | covers every stated input, output, failure, and non-goal | identifies ambiguity and records a bounded assumption |
| model | types represent required states without needless ceremony | invalid states are excluded or rejected at one clear boundary |
| effects and ownership | names where I/O, time, mutation, resources, and cancellation live | lifetime and partial-failure behavior are testable and locally controlled |
| API and interop | callers can use the surface in their own language/tooling | compiled call sites, nullability, compatibility, and representation leakage are checked |
| evidence | supplies a reproducible build, test, probe, or explicitly marked review | tests counterexamples and separates executed, reviewed, and unverified claims |
| clarity and scope | solves the requested problem without hiding key decisions | compares a plausible alternative and explains the stop condition |`,
    variationHeading: 'Acceptable variation and hard failures',
    variationBody: [
      'Recursion, a fold, or a small loop may all be sound when stack use, order, and ownership match. A record or class, function or interface, list or array, `Result` or domain union, and `Async` or `Task` are likewise decisions made from the boundary—not style points awarded in isolation.',
      'A different answer is acceptable when it preserves explicit constraints, makes new assumptions visible, and supplies proportional evidence. Improve the published solution when your alternative is simpler and at least as well proven.',
      'Reject an answer that suppresses a relevant warning, hides a new union case behind a wildcard, uses timing sleeps as concurrency proof, leaks secrets or accidental representation, reports an unrun platform check as passing, changes the public contract silently, or offers output without explaining the causal model.'
    ],
    evidenceHeading: 'Match the claim to the evidence',
    evidenceTable: `| Claim | Minimum suitable evidence |
|---|---|
| a type relationship or diagnostic | locked compiler invocation and exact relevant signature/code |
| pure behavior or invariant | focused example/property tests including a counterexample or boundary |
| resource, async, concurrency, or interop behavior | real boundary test with deterministic coordination and cleanup |
| framework/platform adoption | compiled minimal slice plus explicit untested platform/deployment limits |
| proposed architecture or package choice | written constraints, official-source review, spike plan, rollback/removal condition |`,
    evidenceBody: 'Not every prose design in a solution page is an executed repository artifact. Each page distinguishes runnable example evidence, compiler evidence, official-source review, and proposed work. Do not promote a proposal to “verified” because it appears under Solutions.',
    indexHeading: 'All chapter answers',
    indexIntro: 'Every exercise link below targets its exact answer heading. “Review focus” comes from the corresponding solution page and summarizes what comparison should teach.',
    partNames: {
      1: 'Part 1 · Expressions and functions',
      2: 'Part 2 · Modeling with types',
      3: 'Part 3 · Composition and program structure',
      4: 'Part 4 · Effects, asynchrony, and concurrency',
      5: 'Part 5 · .NET interop and engineering quality',
      6: 'Part 6 · The booking system',
      7: 'Part 7 · The ecosystem map'
    },
    prompt: 'Chapter',
    answerPage: 'Solution page',
    answers: 'Answers',
    exercise: 'Exercise',
    focus: 'Review focus',
    finalHeading: 'Final self-review',
    finalQuestions: [
      'Can you explain the inferred or public types without relying on the solution text?',
      'Did you preserve ordering, evaluation, ownership, failure, cancellation, and compatibility requirements?',
      'Which evidence actually ran, and which claim is only a reviewed or proposed boundary?',
      'What counterexample would distinguish your design from a superficially similar but incorrect one?',
      'If your answer differs, can a reviewer see the tradeoff and the condition under which you would choose the book’s version instead?'
    ]
  },
  zh: {
    language: '中文',
    title: '附录 G：答案与开放题评审指南',
    description: '访问 45 章全部答案，并在不伪称工程问题只有唯一标准答案的前提下评审封闭题、诊断题与开放设计题。',
    heading: '附录 G：答案与开放题评审指南',
    introduction: [
      '答案是反馈，不能替代亲自作答。比较表层语法前，应先比较契约、类型、副作用与证据。即使打印出同一行，也仍可能错过建模、所有权、失败或互操作目标。',
      '有些练习具有狭窄的可观察结果；另一些要求诊断或工程设计。因此答案页展示推理、约束和代表性实现，并不声称每个开放问题都有唯一标准答案。',
      '本页由章节与答案元数据生成。检查器要求每章都有中英文答案页、练习标识完全相同、练习锚点可达，而且章节与答案之间双向链接。'
    ],
    beforeHeading: '打开答案之前',
    before: [
      '用自己的话重述必需行为与每项显式约束。',
      '运行代码前，预测重要类型签名、输出、失败与副作用顺序。',
      '运行范围最小的相关命令；遇到意外证据时保留它，不要盲目把代码改成书中输出。',
      '解释自己的答案为什么满足任务，再查看解答并比较决策，而不是比较行数。'
    ],
    kindsHeading: '三类练习',
    kindsTable: `| 类别 | 可以直接检查什么 | 哪些地方允许变化 |
|---|---|---|
| 封闭行为题 | 必需值、类型、输出顺序、诊断或测试 | 名称与实现可不同，但必须保留完整契约 |
| 诊断题 | 复现命令、第一条相关证据、根因与修复 | 多种修复都可能编译，但只有保留预期语义的才合格 |
| 开放设计题 | 约束、不变量、边界、失败策略与验证计划 | 表示、库、架构和发布方式可随显式取舍变化 |`,
    rubricHeading: '开放设计题评审维度',
    rubricTable: `| 维度 | 达到练习要求 | 有力证据 |
|---|---|---|
| 契约 | 覆盖每项规定输入、输出、失败与非目标 | 找出歧义并记录有界假设 |
| 模型 | 类型表达必需状态，且没有无谓仪式 | 非法状态无法构造，或只在一条清楚边界拒绝 |
| 副作用与所有权 | 指明 I/O、时间、可变性、资源与取消的位置 | 生命周期和部分失败行为可测试且局部受控 |
| API 与互操作 | 调用者能用自己的语言和工具自然消费表层 | 检查编译后调用点、可空性、兼容性与表示泄漏 |
| 证据 | 给出可复现构建、测试、探针，或明确标为资料审阅 | 测试反例，并区分已执行、已审阅与未验证主张 |
| 清晰度与范围 | 解决所问问题，同时不隐藏关键决策 | 比较一种可信替代方案并解释停止条件 |`,
    variationHeading: '可接受变体与硬性失败',
    variationBody: [
      '只要栈使用、顺序和所有权匹配，递归、折叠或小循环都可能正确。记录或类、函数或接口、列表或数组、`Result` 或领域联合、`Async` 或 `Task` 也都应从边界决定，而不是孤立地按风格加分。',
      '不同答案若保留显式约束、公开新增假设，并提供与风险相称的证据，就是可接受变体。若你的版本更简单且至少同样充分地得到证明，应反过来改进书中答案。',
      '若答案屏蔽相关警告、用通配符隐藏新联合案例、以计时 sleep 充当并发证明、泄漏机密或偶然表示、把未运行的平台检查报告为通过、静默改变公共契约，或只给输出却不解释因果模型，就应判为不合格。'
    ],
    evidenceHeading: '让主张与证据匹配',
    evidenceTable: `| 主张 | 最低合适证据 |
|---|---|
| 类型关系或诊断 | 锁定编译器调用与精确相关签名/编号 |
| 纯行为或不变量 | 聚焦示例/性质测试，并含反例或边界 |
| 资源、异步、并发或互操作行为 | 使用确定性协调与清理的真实边界测试 |
| 框架/平台采用 | 可编译最小切片，加明确未测平台/部署边界 |
| 拟议架构或包选择 | 书面约束、官方资料审阅、试验计划与回滚/移除条件 |`,
    evidenceBody: '答案页里的设计叙述并非全都是仓库中已执行的制品。各页会区分可运行样例证据、编译器证据、官方资料审阅与拟议工作。不要只因一项提案出现在“答案”之下，就把它升级成“已验证”。',
    indexHeading: '全部章节答案',
    indexIntro: '下面每个练习链接都指向精确的答案标题。“评审重点”取自对应答案页，用于概括比较时应学到什么。',
    partNames: {
      1: '第一部分 · 表达式与函数',
      2: '第二部分 · 用类型建立模型',
      3: '第三部分 · 组合与程序结构',
      4: '第四部分 · 副作用、异步与并发',
      5: '第五部分 · .NET 互操作与工程质量',
      6: '第六部分 · 活动预约系统',
      7: '第七部分 · 生态地图'
    },
    prompt: '本章',
    answerPage: '答案页',
    answers: '各题答案',
    exercise: '练习',
    focus: '评审重点',
    finalHeading: '最终自我评审',
    finalQuestions: [
      '你能否不依赖答案文字，解释推断类型或公共类型？',
      '是否保留了顺序、求值、所有权、失败、取消与兼容性要求？',
      '哪些证据真实运行过，哪些主张只是资料审阅或拟议边界？',
      '哪个反例能区分你的设计与表面相似但错误的设计？',
      '若答案不同，审阅者能否看到取舍，以及你会改用书中版本的条件？'
    ]
  }
}

function markdownFiles(directory) {
  const files = []
  for (const name of readdirSync(directory).sort()) {
    const path = join(directory, name)
    const metadata = statSync(path)
    if (metadata.isDirectory()) files.push(...markdownFiles(path))
    else if (metadata.isFile() && name.endsWith('.md')) files.push(path)
  }
  return files
}

function readPage(path, localeDir) {
  const source = readFileSync(path, 'utf8')
  const match = /^---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(source)
  if (!match) throw new Error(`${path}: missing YAML frontmatter`)
  return {
    path,
    relativePath: relative(localeDir, path).replaceAll('\\', '/'),
    source,
    frontmatter: parse(match[1])
  }
}

function sameArray(left, right) {
  return Array.isArray(left) && Array.isArray(right) &&
    left.length === right.length && left.every((value, index) => value === right[index])
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function containsLink(source, target) {
  return new RegExp(`\\]\\(${escapeRegex(target)}(?:\\.md)?(?:#[^)]+)?\\)`).test(source)
}

function markdownText(value) {
  return String(value).replaceAll('\\', '\\\\').replaceAll('[', '\\[').replaceAll(']', '\\]')
}

function requireExerciseAnchors({ page, exerciseIds, chapter, language, role }) {
  const chapterPrefix = `ch${String(chapter).padStart(2, '0')}-exercise-`
  const expectedAnchors = new Set()
  for (const id of exerciseIds) {
    if (!id.startsWith(chapterPrefix) || !/^\d{2}$/.test(id.slice(chapterPrefix.length))) {
      throw new Error(`${language} chapter ${chapter}: invalid exercise id ${id}`)
    }
    const suffix = id.slice(chapterPrefix.length)
    expectedAnchors.add(suffix)
    const occurrences = page.source.match(new RegExp(`\\{#exercise-${suffix}\\}`, 'g')) ?? []
    if (occurrences.length !== 1) {
      throw new Error(
        `${language} chapter ${chapter}: ${role} must contain exactly one #exercise-${suffix} anchor`
      )
    }
  }

  for (const match of page.source.matchAll(/\{#exercise-(\d{2})\}/g)) {
    const suffix = match[1]
    if (!expectedAnchors.has(suffix)) {
      throw new Error(
        `${language} chapter ${chapter}: ${role} contains orphan exercise anchor #exercise-${suffix}`
      )
    }
  }
}

function loadLocale({ docsDir, locale, expectedChapters }) {
  const localeDir = join(docsDir, locale)
  const language = copy[locale].language
  const pages = markdownFiles(localeDir).map((path) => readPage(path, localeDir))
  const chapters = new Map()
  const solutions = new Map()

  for (const page of pages) {
    const kind = page.frontmatter.kind
    if (kind !== 'chapter' && kind !== 'solution') continue
    const chapter = page.frontmatter.chapter
    if (!Number.isInteger(chapter)) throw new Error(`${page.relativePath}: chapter must be an integer`)
    const target = kind === 'chapter' ? chapters : solutions
    if (target.has(chapter)) throw new Error(`${language}: duplicate ${kind} page for chapter ${chapter}`)
    target.set(chapter, page)
  }

  if (chapters.size !== expectedChapters || solutions.size !== expectedChapters) {
    throw new Error(
      `${language}: expected ${expectedChapters} chapters and solutions, found ${chapters.size} and ${solutions.size}`
    )
  }

  const records = []
  for (let chapter = 1; chapter <= expectedChapters; chapter += 1) {
    const chapterPage = chapters.get(chapter)
    const solutionPage = solutions.get(chapter)
    if (!chapterPage || !solutionPage) {
      throw new Error(`${language}: missing chapter or solution for chapter ${chapter}`)
    }
    if (chapterPage.frontmatter.part !== solutionPage.frontmatter.part) {
      throw new Error(`${language} chapter ${chapter}: part differs between chapter and solution`)
    }
    if (!sameArray(chapterPage.frontmatter.exerciseIds, solutionPage.frontmatter.exerciseIds)) {
      throw new Error(`${language} chapter ${chapter}: exerciseIds differ between chapter and solution`)
    }
    if (chapterPage.frontmatter.exerciseIds.length === 0) {
      throw new Error(`${language} chapter ${chapter}: exerciseIds must not be empty`)
    }

    const chapterSlug = basename(chapterPage.path, '.md')
    const solutionSlug = basename(solutionPage.path, '.md')
    if (chapterSlug !== solutionSlug) {
      throw new Error(`${language} chapter ${chapter}: chapter and solution slugs differ`)
    }
    const chapterTarget = `../${chapterPage.relativePath.replace(/\.md$/, '')}`
    const solutionTarget = `../solutions/${solutionSlug}`
    if (!containsLink(chapterPage.source, solutionTarget)) {
      throw new Error(`${language} chapter ${chapter}: chapter does not link to its solution page`)
    }
    if (!containsLink(solutionPage.source, chapterTarget)) {
      throw new Error(`${language} chapter ${chapter}: solution does not link back to its chapter`)
    }

    requireExerciseAnchors({
      page: chapterPage,
      exerciseIds: chapterPage.frontmatter.exerciseIds,
      chapter,
      language,
      role: 'chapter'
    })
    requireExerciseAnchors({
      page: solutionPage,
      exerciseIds: solutionPage.frontmatter.exerciseIds,
      chapter,
      language,
      role: 'solution'
    })

    records.push({ chapter, chapterPage, solutionPage, chapterSlug })
  }

  return records
}

function validateLocales(recordsByLocale) {
  for (let index = 0; index < recordsByLocale.en.length; index += 1) {
    const english = recordsByLocale.en[index]
    const chinese = recordsByLocale.zh[index]
    if (
      english.chapterPage.frontmatter.translationKey !== chinese.chapterPage.frontmatter.translationKey ||
      english.solutionPage.frontmatter.translationKey !== chinese.solutionPage.frontmatter.translationKey ||
      !sameArray(english.chapterPage.frontmatter.exerciseIds, chinese.chapterPage.frontmatter.exerciseIds)
    ) {
      throw new Error(`chapter ${english.chapter}: chapter/solution metadata differs by locale`)
    }
  }
}

function pageFrontmatter(locale) {
  return `---
title: "${copy[locale].title}"
description: "${copy[locale].description}"
translationKey: appendices/g-solutions-guide
kind: appendix
appendix: G
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---`
}

function numbered(items) {
  return items.map((item, index) => `${index + 1}. ${item}`).join('\n')
}

function bullets(items) {
  return items.map((item) => `- ${item}`).join('\n')
}

function renderRecord(locale, record) {
  const text = copy[locale]
  const chapterPage = record.chapterPage.frontmatter
  const solutionPage = record.solutionPage.frontmatter
  const chapterPath = `../${record.chapterPage.relativePath.replace(/\.md$/, '')}#overview`
  const solutionPath = `../solutions/${record.chapterSlug}`
  const answerLinks = solutionPage.exerciseIds.map((id, index) => {
    const suffix = id.match(/exercise-(\d+)$/)[1]
    return `[${text.exercise} ${index + 1}](${solutionPath}#exercise-${suffix})`
  }).join(' · ')

  return `### ${markdownText(chapterPage.title)} {#chapter-${String(record.chapter).padStart(2, '0')}}

[${text.prompt}](${chapterPath}) · [${text.answerPage}](${solutionPath}#overview)

**${text.answers}:** ${answerLinks}

**${text.focus}:** ${solutionPage.description}`
}

function renderIndex(locale, records) {
  const text = copy[locale]
  const groups = new Map()
  for (const record of records) {
    const part = record.chapterPage.frontmatter.part
    const group = groups.get(part) ?? []
    group.push(record)
    groups.set(part, group)
  }

  return [...groups.entries()].map(([part, group]) => {
    const heading = text.partNames[part] ?? `${locale === 'en' ? 'Part' : '第'} ${part}`
    return `## ${heading} {#part-${part}}\n\n${group.map((record) => renderRecord(locale, record)).join('\n\n')}`
  }).join('\n\n')
}

function renderGuide(locale, records) {
  const text = copy[locale]
  return `${pageFrontmatter(locale)}

# ${text.heading} {#overview}

${text.introduction.join('\n\n')}

## ${text.beforeHeading} {#before-opening}

${numbered(text.before)}

## ${text.kindsHeading} {#exercise-kinds}

${text.kindsTable}

## ${text.rubricHeading} {#open-design-rubric}

${text.rubricTable}

## ${text.variationHeading} {#acceptable-variation}

${text.variationBody.join('\n\n')}

## ${text.evidenceHeading} {#evidence}

${text.evidenceTable}

${text.evidenceBody}

## ${text.indexHeading} {#answer-index}

${text.indexIntro}

${renderIndex(locale, records)}

## ${text.finalHeading} {#final-review}

${bullets(text.finalQuestions)}
`
}

export function buildSolutionsGuideOutputs({
  docsDir = defaultDocsDir,
  expectedChapters = DEFAULT_EXPECTED_CHAPTERS
} = {}) {
  const absoluteDocsDir = resolve(docsDir)
  const recordsByLocale = Object.fromEntries(LOCALES.map((locale) => [
    locale,
    loadLocale({ docsDir: absoluteDocsDir, locale, expectedChapters })
  ]))
  validateLocales(recordsByLocale)

  return new Map(LOCALES.map((locale) => [
    join(absoluteDocsDir, locale, 'appendices/g-solutions-guide.md'),
    renderGuide(locale, recordsByLocale[locale])
  ]))
}

export function checkSolutionsGuideOutputs(options = {}) {
  const outputs = buildSolutionsGuideOutputs(options)
  const docsDir = resolve(options.docsDir ?? defaultDocsDir)
  const errors = []

  for (const [path, expected] of outputs) {
    const displayPath = relative(docsDir, path).replaceAll('\\', '/')
    if (!existsSync(path)) {
      errors.push(`${displayPath}: generated solutions guide is missing; run pnpm generate:solutions-guide`)
    } else if (readFileSync(path, 'utf8') !== expected) {
      errors.push(`${displayPath}: generated solutions guide is stale; run pnpm generate:solutions-guide`)
    }
  }
  return errors.sort()
}

export function writeSolutionsGuideOutputs(options = {}) {
  const outputs = buildSolutionsGuideOutputs(options)
  for (const [path, source] of outputs) {
    mkdirSync(dirname(path), { recursive: true })
    writeFileSync(path, source)
  }
  return outputs
}

function cli(argv = process.argv.slice(2)) {
  const check = argv.includes('--check')
  const unknown = argv.filter((argument) => argument !== '--check')
  if (unknown.length > 0) {
    console.error(`Unknown argument: ${unknown[0]}`)
    return 1
  }

  try {
    if (check) {
      const errors = checkSolutionsGuideOutputs()
      if (errors.length > 0) {
        console.error(errors.join('\n'))
        return 1
      }
      console.log('Generated solutions guide check passed.')
    } else {
      const outputs = writeSolutionsGuideOutputs()
      console.log(`Generated ${outputs.size} solutions guide pages.`)
    }
    return 0
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    return 1
  }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) process.exitCode = cli()
