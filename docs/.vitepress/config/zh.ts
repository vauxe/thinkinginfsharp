export const zhLocale = {
  label: '简体中文',
  lang: 'zh-Hans',
  link: '/zh/',
  title: 'F# 思维',
  description: '从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。',
  themeConfig: {
    nav: [
      { text: '首页', link: '/zh/' },
      { text: '开始阅读', link: '/zh/part-01/ch-01-first-session' }
    ],
    sidebar: {
      '/zh/part-01/': [
        {
          text: '第一部分 · 表达式与函数',
          items: [
            { text: '第 1 章 · 第一次 F# 会话', link: '/zh/part-01/ch-01-first-session' },
            { text: '第 2 章 · 值、绑定与表达式', link: '/zh/part-01/ch-02-values-bindings-expressions' },
            { text: '第 3 章 · 函数也是值', link: '/zh/part-01/ch-03-functions-as-values' },
            { text: '第 4 章 · 分支与基本模式', link: '/zh/part-01/ch-04-branching-patterns' },
            { text: '第 5 章 · 列表、管道与数据流', link: '/zh/part-01/ch-05-lists-pipelines' },
            { text: '第 6 章 · 递归、尾调用与折叠', link: '/zh/part-01/ch-06-recursion-folds' }
          ]
        }
      ],
      '/zh/part-02/': [
        {
          text: '第二部分 · 用类型建立模型',
          items: [
            { text: '第 7 章 · 记录、更新、相等与比较', link: '/zh/part-02/ch-07-records-equality' },
            { text: '第 8 章 · 可辨识联合与状态建模', link: '/zh/part-02/ch-08-discriminated-unions' },
            { text: '第 9 章 · 缺失与预期失败', link: '/zh/part-02/ch-09-option-result' },
            { text: '第 10 章 · 递归类型与结构递归', link: '/zh/part-02/ch-10-recursive-types' },
            { text: '第 11 章 · 泛型、约束与度量单位', link: '/zh/part-02/ch-11-generics-constraints' },
            { text: '第 12 章 · 让非法状态无法表示', link: '/zh/part-02/ch-12-making-illegal-states-unrepresentable' }
          ]
        }
      ],
      '/zh/part-03/': [
        {
          text: '第三部分 · 组合与程序结构',
          items: [
            { text: '第 13 章 · 组合、参数顺序与管道 API', link: '/zh/part-03/ch-13-composition-pipeline-api' },
            { text: '第 14 章 · 集合选择与求值模型', link: '/zh/part-03/ch-14-collections-evaluation' },
            { text: '第 15 章 · 活动模式与领域匹配边界', link: '/zh/part-03/ch-15-active-patterns' },
            { text: '第 16 章 · 模块、命名空间、项目与编译设置', link: '/zh/part-03/ch-16-modules-namespaces-projects' },
            { text: '第 17 章 · 签名、访问控制与面向 F# 的 API', link: '/zh/part-03/ch-17-signatures-encapsulation' },
            { text: '第 18 章 · 显式工作流组合与验证累积', link: '/zh/part-03/ch-18-workflow-validation' }
          ]
        }
      ],
      '/zh/part-04/': [
        {
          text: '第四部分 · 副作用、异步与并发',
          items: [
            { text: '第 19 章 · .NET API 与空值边界', link: '/zh/part-04/ch-19-dotnet-null-boundaries' },
            { text: '第 20 章 · 函数式核心与副作用边界', link: '/zh/part-04/ch-20-functional-core-effects' },
            { text: '第 21 章 · 异常、资源与 I/O', link: '/zh/part-04/ch-21-exceptions-resources-io' },
            { text: "第 22 章 · Async<'T> 与 Task<'T>", link: '/zh/part-04/ch-22-async-task' },
            { text: '第 23 章 · 取消、超时、故障与释放', link: '/zh/part-04/ch-23-cancellation-timeouts' },
            { text: '第 24 章 · 并行、并发、代理与状态', link: '/zh/part-04/ch-24-concurrency-agents-state' }
          ]
        }
      ],
      '/zh/part-05/': [
        {
          text: '第五部分 · .NET 互操作与工程质量',
          items: [
            { text: '第 25 章 · 在 F# 中定义对象', link: '/zh/part-05/ch-25-objects-interfaces' },
            { text: '第 26 章 · 深入 .NET 边界', link: '/zh/part-05/ch-26-dotnet-runtime-boundaries' },
            { text: '第 27 章 · 为 C# 设计 F# API', link: '/zh/part-05/ch-27-fsharp-api-for-csharp' },
            { text: '第 28 章 · 示例测试、替身与边界测试', link: '/zh/part-05/ch-28-testing-boundaries' },
            { text: '第 29 章 · 使用 FsCheck 进行性质测试', link: '/zh/part-05/ch-29-property-testing' },
            { text: '第 30 章 · 诊断、工具与可复现构建', link: '/zh/part-05/ch-30-diagnostics-tooling-builds' },
            { text: '第 31 章 · 先测量再优化', link: '/zh/part-05/ch-31-measure-before-optimizing' },
            { text: '第 32 章 · 从函数到应用', link: '/zh/part-05/ch-32-functions-to-applications' }
          ]
        }
      ],
      '/zh/part-06/': [
        {
          text: '第六部分 · 活动预约系统',
          items: [
            { text: '第 33 章 · 业务语言、命令、事件与模型', link: '/zh/part-06/ch-33-domain-language-model' },
            { text: '第 34 章 · 纯预约工作流与验证', link: '/zh/part-06/ch-34-pure-booking-workflow' },
            { text: '第 35 章 · 端口、持久化、配置与替身', link: '/zh/part-06/ch-35-ports-persistence-config' },
            { text: '第 36 章 · Web API、JSON 与输入边界', link: '/zh/part-06/ch-36-web-api-boundaries' },
            { text: '第 37 章 · 一致性、幂等、重试与部分失败', link: '/zh/part-06/ch-37-consistency-idempotency' },
            { text: '第 38 章 · 集成、诊断、C# 客户端与发布证据', link: '/zh/part-06/ch-38-integration-diagnostics-release' }
          ]
        }
      ],
      '/zh/part-07/': [
        {
          text: '第七部分 · 生态地图',
          items: [
            { text: '第 39 章 · ASP.NET Core 与 F# Web 生态', link: '/zh/part-07/ch-39-web-ecosystem' },
            { text: '第 40 章 · 数据、类型提供器、分析与 ML', link: '/zh/part-07/ch-40-data-analytics' },
            { text: '第 41 章 · Fable、Elmish 与浏览器应用', link: '/zh/part-07/ch-41-fable-elmish' },
            { text: '第 42 章 · 云、容器、Serverless 与 .NET Aspire', link: '/zh/part-07/ch-42-cloud-containers-aspire' },
            { text: '第 43 章 · Avalonia、桌面端与移动端', link: '/zh/part-07/ch-43-avalonia-desktop-mobile' },
            { text: '第 44 章 · Unity 6.3 LTS 与 F#', link: '/zh/part-07/ch-44-unity' }
          ]
        }
      ],
      '/zh/solutions/': [
        {
          text: '练习答案',
          items: [
            { text: '第 1 章', link: '/zh/solutions/ch-01-first-session' },
            { text: '第 2 章', link: '/zh/solutions/ch-02-values-bindings-expressions' },
            { text: '第 3 章', link: '/zh/solutions/ch-03-functions-as-values' },
            { text: '第 4 章', link: '/zh/solutions/ch-04-branching-patterns' },
            { text: '第 5 章', link: '/zh/solutions/ch-05-lists-pipelines' },
            { text: '第 6 章', link: '/zh/solutions/ch-06-recursion-folds' },
            { text: '第 7 章', link: '/zh/solutions/ch-07-records-equality' },
            { text: '第 8 章', link: '/zh/solutions/ch-08-discriminated-unions' },
            { text: '第 9 章', link: '/zh/solutions/ch-09-option-result' },
            { text: '第 10 章', link: '/zh/solutions/ch-10-recursive-types' },
            { text: '第 11 章', link: '/zh/solutions/ch-11-generics-constraints' },
            { text: '第 12 章', link: '/zh/solutions/ch-12-making-illegal-states-unrepresentable' },
            { text: '第 13 章', link: '/zh/solutions/ch-13-composition-pipeline-api' },
            { text: '第 14 章', link: '/zh/solutions/ch-14-collections-evaluation' },
            { text: '第 15 章', link: '/zh/solutions/ch-15-active-patterns' },
            { text: '第 16 章', link: '/zh/solutions/ch-16-modules-namespaces-projects' },
            { text: '第 17 章', link: '/zh/solutions/ch-17-signatures-encapsulation' },
            { text: '第 18 章', link: '/zh/solutions/ch-18-workflow-validation' },
            { text: '第 19 章', link: '/zh/solutions/ch-19-dotnet-null-boundaries' },
            { text: '第 20 章', link: '/zh/solutions/ch-20-functional-core-effects' },
            { text: '第 21 章', link: '/zh/solutions/ch-21-exceptions-resources-io' },
            { text: '第 22 章', link: '/zh/solutions/ch-22-async-task' },
            { text: '第 23 章', link: '/zh/solutions/ch-23-cancellation-timeouts' },
            { text: '第 24 章', link: '/zh/solutions/ch-24-concurrency-agents-state' },
            { text: '第 25 章', link: '/zh/solutions/ch-25-objects-interfaces' },
            { text: '第 26 章', link: '/zh/solutions/ch-26-dotnet-runtime-boundaries' },
            { text: '第 27 章', link: '/zh/solutions/ch-27-fsharp-api-for-csharp' },
            { text: '第 28 章', link: '/zh/solutions/ch-28-testing-boundaries' },
            { text: '第 29 章', link: '/zh/solutions/ch-29-property-testing' },
            { text: '第 30 章', link: '/zh/solutions/ch-30-diagnostics-tooling-builds' },
            { text: '第 31 章', link: '/zh/solutions/ch-31-measure-before-optimizing' },
            { text: '第 32 章', link: '/zh/solutions/ch-32-functions-to-applications' },
            { text: '第 33 章', link: '/zh/solutions/ch-33-domain-language-model' },
            { text: '第 34 章', link: '/zh/solutions/ch-34-pure-booking-workflow' },
            { text: '第 35 章', link: '/zh/solutions/ch-35-ports-persistence-config' },
            { text: '第 36 章', link: '/zh/solutions/ch-36-web-api-boundaries' },
            { text: '第 37 章', link: '/zh/solutions/ch-37-consistency-idempotency' },
            { text: '第 38 章', link: '/zh/solutions/ch-38-integration-diagnostics-release' },
            { text: '第 39 章', link: '/zh/solutions/ch-39-web-ecosystem' },
            { text: '第 40 章', link: '/zh/solutions/ch-40-data-analytics' },
            { text: '第 41 章', link: '/zh/solutions/ch-41-fable-elmish' },
            { text: '第 42 章', link: '/zh/solutions/ch-42-cloud-containers-aspire' },
            { text: '第 43 章', link: '/zh/solutions/ch-43-avalonia-desktop-mobile' },
            { text: '第 44 章', link: '/zh/solutions/ch-44-unity' }
          ]
        }
      ]
    },
    outline: { label: '本页目录', level: [2, 3] as [number, number] },
    lastUpdated: { text: '最后更新' },
    docFooter: { prev: '上一章', next: '下一章' },
    darkModeSwitchLabel: '外观',
    lightModeSwitchTitle: '切换到浅色主题',
    darkModeSwitchTitle: '切换到深色主题',
    sidebarMenuLabel: '目录',
    returnToTopLabel: '返回顶部',
    langMenuLabel: '切换语言',
    skipToContentLabel: '跳到正文'
  }
} as const
