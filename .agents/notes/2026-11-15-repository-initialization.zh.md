---
status: superseded
category: repository
superseded_by:
    - 2026-11-24-dotnet-lts.zh.md
---
# 仓库初始化约定

[English](2026-11-15-repository-initialization.en.md) | 中文

## 目的

这是仓库的第一份正式 Note，记录已落地并完成检查的初始化基线及其边界背后的理由。implemented 表示仓库基础设施已落实，下文的产品目标和待定选择不代表对应功能已经实现。

项目从零设计，不参考同组织其他仓库中较旧的业务实现。DeepSeek Harness 用于参考文档维护机制，不成套引入它的模板、目录分类、配额或一致性 sidecar。

当前行为以文中链接的配置和脚本为准。[AGENTS.md](../../AGENTS.md) 为 Agent 提供简短的长期约定入口。

## 产品范围与身份

产品以 C# 为主要语言。目标是在当前 macOS、Windows、Linux 上提供 UI，并在所有目标操作系统上提供无头运行模式。客户端和服务端属于同一应用，在同进程内直接交互；服务端同时向其他进程提供远程调用接口。当前已接通 Proto 契约、HTTP/JSON 转码和 Hello World 的本地与远程调用样例，见[协议 Note](2026-11-15-protobuf-api-contract.zh.md)。UI、完整业务、身份认证和进一步的组件边界尚未实现或确定。

开发环境原生支持现代 macOS、Windows、Linux，Windows 开发不要求 WSL。Windows 7、Windows XP 属于仍在研究的有条件部署目标，可以限定虚拟机、宿主辅助和明确的外部依赖。兼容方案应维持同一产品架构，避免成为独立开发的另一个应用。这不要求在 XP 上安装现代开发 SDK，也不承诺兼容实现已经存在。

品牌由 Lazy 与 TuiHub 组合。Tui 沿用 TuiHub 的品牌含义，不展开为界面技术缩写。C# 名称使用 `LazyTuiHub`，工具包使用 `lazy-tuihub`。

## 工具链与任务入口

三种现代操作系统统一使用 mise 管理开发工具和任务，按其[官方说明](https://mise.jdx.dev/installing-mise.html)安装。每个版本只有一份可执行的权威声明：

| 工具 | 初始版本 | 版本来源 |
| - | - | - |
| .NET SDK | 11.0.100 | [global.json](../../global.json)，禁用版本滚动并禁止预览版本 |
| C# | 15.0 | [Directory.Build.props](../../Directory.Build.props) |
| Node.js | 26.13.1 | [package.json](../../package.json) 的 `devEngines.runtime` |
| pnpm | 12.21.0 | [package.json](../../package.json) 的 `packageManager` |

[mise.toml](../../mise.toml) 启用原生版本文件发现，不重复声明这些版本。[初始 C# 项目](../../src/LazyTuiHub/LazyTuiHub.csproj)以 `net11.0` 为目标。包管理器使用 pnpm，[pnpm-lock.yaml](../../pnpm-lock.yaml) 是唯一的 JavaScript 依赖锁文件。

协议开发另由 mise 固定 Go、protoc、Google API Linter 和 protoc-gen-openapi；产品仍使用 C#。NSwag 与 CSharpier 共用根 dotnet-tools.json 工具清单。官方 AIP 原文通过 setup:aip 准备为本地参考副本，其仓库与采用提交只在 mise.toml 中声明。

```sh
mise install
mise exec -- pnpm install --frozen-lockfile
mise exec -- dotnet tool restore
mise run setup:aip
mise doctor project
mise run check
```

Doctor 只检查 .NET SDK、Node.js、pnpm、CSharpier、jscpd、协议编译与生成工具，以及官方 AIP 副本的来源、提交和工作树状态，并报告修复建议，不下载工具、安装包或修复环境。安装和原文准备通过显式操作完成。

当前任务接口如下：

| 任务 | 职责 |
| - | - |
| `build` | 构建解决方案。 |
| `run` | 运行应用。 |
| `check:code` | 聚合文件大小、产品分析器、C# 格式和重复代码检查，不写回修改。 |
| `check:files` | 拒绝超过 1 MiB 的已跟踪文件及未被忽略的新文件。 |
| `check:analyzers` | 构建并将已选分析器的 warning 视为检查失败。 |
| `check:format` | 只读检查 C# 格式。 |
| `check:duplicates` | 检查手写产品 C# 的重复代码。 |
| `test:lint` | 验证 lint 门禁、阈值、测试隔离和文件大小边界；不默认加入 check。 |
| `check:markdown` | 执行 remark 管线。 |
| `check:notes` | 对照 Git 检查 Note 快照变化与冻结内容。 |
| `setup:aip` | 准备固定提交的官方 AIP 原文，不覆盖本地修改。 |
| `restore:api` | 还原契约项目的公共 Proto 和编译依赖。 |
| `generate:api` | 从 Proto 显式生成 OpenAPI、HTTP 客户端和 Markdown 参考文档。 |
| `check:proto` | 执行 Google API Linter 的默认启用规则。 |
| `check:generated` | 临时重新生成并比较派生产物，不写回源文件。 |
| `check:api` | 聚合 Proto 与生成差异检查。 |
| `demo:api` | 比较 Hello World 的进程内与生成客户端 HTTP 调用。 |
| `check:commit` | 校验 HEAD 或显式输入的提交消息。 |
| `setup:hooks` | 需要时重新注册本仓的提交消息 hook。 |
| `check` | 聚合代码、API、Markdown、Note 历史和提交消息检查。 |
| `format:code` | 写回 C# 格式化修改。 |
| `notes` | 列出或筛选平铺 Notes 目录中的 frontmatter。 |

通过 `mise run <task>` 调用，用 `mise tasks ls` 查看入口，以任务定义为准。校验与格式化分别提供入口，避免检查过程静默改写文件。

## 仓库边界

- `src/` 放产品代码。
- `tests/` 预留给产品测试，按独立项目组织，不混入产品逻辑。目前尚未选择 C# 测试框架或创建测试项目。
- `docs/` 在有实际内容时承载面向用户和开发者的当前文档。主分支维护最新内容，不自建按版本划分的文档目录树。
- `.agents/` 放开发辅助和带日期的决策历史。可执行辅助逻辑放 `.agents/scripts/`，对应测试独立放在 [.agents/tests/](../tests/)。
- 根配置保留在各工具要求的原生发现位置。这些文件承担入口和共享配置职责，不构成将辅助实现放出 `.agents/` 的理由。

已跟踪文件及未被 Git 忽略的新文件不得超过 1 MiB（1,048,576 字节）。`check:files` 在代码分析、格式和重复检查前拒绝超限文件，不通过扩大扫描上限或静默跳过来放行。

只有真实、可复用的工作流才值得创建 Skill。不预建技能骨架、笔记、索引或空文档结构；已有辅助脚本保持单一实现，不因 Skill 再复制一份。

## 提交消息

遵循 [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)。[提交辅助工具](../scripts/commits.mjs)复用成熟的 conventional-commits-parser 包，再以少量规则检查必需语法。它接受描述性的自定义 type、Unicode、大写文本、可选 scope 与破坏性变更标记，以及多行正文和 footer；不增加 type 白名单、长度上限、大小写策略或 merge/revert 自动豁免。Agent 仍需判断 type 和描述是否准确表达变更。

新检出仓库首次安装开发依赖时，[package.json](../../package.json) 的 `prepare` 生命周期会在依赖就绪后调用既有 setup，因此普通 `pnpm install --frozen-lockfile` 会自动注册 hook。没有依赖变化的重复安装可能跳过 `prepare`；保留 `mise run setup:hooks` 作为手动修复入口。共用的 setup 只设置本仓的 `core.hooksPath`，遇到既有自定义路径或 hook 时拒绝替换。[commit-msg hook](../githooks/commit-msg)将消息文件名安全传给 `mise run --skip-tools check:commit`；校验失败就阻止提交，不改写消息或安装缺失工具。跟踪的可执行位和[定向 LF 属性](../../.gitattributes)支持三个开发系统的原生 Git，包括 Git for Windows 自带的 shell。

`mise run check:commit` 默认校验 HEAD，可用 `--edit <message-file>` 校验候选消息，或用 `--revision <commit>` 校验既有提交；`check` 聚合入口也包含该检查。Git 的 `--no-verify` 可以绕过本地 hook，因此该机制不等于远程仓库强制约束。本轮初始化未加入 CI workflow。

## C# 校验

通过 [Directory.Build.props](../../Directory.Build.props)、[Directory.Build.targets](../../Directory.Build.targets)、[.editorconfig](../../.editorconfig) 和[产品诊断集](../lint/product.globalconfig)配置 C# 检查。共享语言和 nullable 设置保留在根配置，通过项目身份区分产品与测试规则。[C# lint Note](2026-11-15-csharp-lint.zh.md)记录具体规则、工具分工和验证边界。

首版显式诊断门禁取自已核对的 ASP.NET Core、Aspire、.NET Runtime、CommunityToolkit 生产规则交集：

| 规则 | 严重程度 | 含义 |
| - | - | - |
| CA1821 | error | 移除空析构器。 |
| CA2200 | error | 重抛异常时保留原始堆栈信息。 |

产品项目使用 SDK Recommended、安全类别的更广规则集，以及 Meziantou、Sonar 的补充检查。分析器包固定版本并设为 PrivateAssets=all。普通 build 保留开发构建行为，check:code 将已选规则的 warning 视为失败。CSharpier 使用本地工具清单管理，接管 C# 格式；jscpd 通过 pnpm 管理，只扫描手写产品代码。文件头模板尚未约定，不复制上游版权文字。

测试项目不加载产品分析器及诊断集，保留编译、nullable 和格式要求；测试框架确定后选择专用分析器。长度、复杂度、魔法数、重复代码和产品性能建议不作为测试门禁。生成源码不作为人工清理风格的对象，但仍必须通过编译；生成身份不能豁免编译正确性。测试运行时间约束留在执行层，当前 lint 不包含超时策略。

## Markdown 校验

Markdown 只使用 remark 一个生态。[根管线](../../.remarkrc.json)组合 GFM、YAML frontmatter 解析、官方 consistent 和 recommended lint 预设，并显式增加代码围栏语言标记、标题层级递增、同章节重复标题三项规则。不设置硬性行长限制。

保留原生 Markdown 和 HTML 行为，不重建另一套 linter 的策略。基线没有整体禁止 HTML，也未加入标签白名单。一般文档识别 YAML frontmatter 语法，Note 专属元数据要求由 Note 插件负责。

官方相对链接插件离线检查本地 Markdown 链接、文件存在性和跨文件的 GitHub 风格标题锚点。当前配置跳过外部 URL 与站点根路径，原始 HTML 的 `href` 不在此检查范围。普通链接指向 SVG fragment 时存在已接受的误报，目前没有实际场景要求增加自维护修补层。这不禁止 SVG 图片。

此管线不包含 Markdownlint。Prettier，以及面向仓库一般 JSON/XML 文件的格式化或校验管线，尚未选择；Note 专属元数据校验在下文单独约定。

## 双语文档与忽略规则

进入 remark 扫描范围的 Markdown 文档使用同目录、同 basename 的 `.zh.md` 和 `.en.md` 配对，下文列出明确例外。[双语辅助脚本](../scripts/remark-bilingual-structure.mjs)比较标题层级与顺序，以及代码块语言的数量，不比较译文、标题 slug、代码内容、段落数或行数，也不强制代码块顺序、列表或表格相同。

[.remarkignore](../../.remarkignore) 是独立的文档忽略文件。使用离文件最近的一份规则，辅助脚本不合并所有祖先规则，也不读取 Git 忽略配置。任一语言文件被忽略就跳过整对，包括被忽略的伙伴文件尚不存在的情况。

工具要求固定名称的 `AGENTS.md`、`SKILL.md` 已显式豁免双语命名与配对，仍然执行其他 remark 规则。普通 `README.md` 不继承该豁免。

`docs/api/` 整个目录只包含生成的英文 Markdown，明确豁免双语命名与配对。根 remark 配置将例外限定在该目录，其他 Markdown 规则和 1 MiB 文件上限继续生效。API 注释维护在 Proto 中，生成器负责全部参考页面；手写开发指南在 `docs/api/` 外继续双语配对。[协议 Note](2026-11-15-protobuf-api-contract.zh.md)记录生成链路，文档网页部署不属于 API 工作范围。

[.gitignore](../../.gitignore)以官方 Dotnet.gitignore 模板为基础，并加入本仓工具和操作系统相关条目。Git 忽略与文档忽略各自承担职责。

## 决策 Notes 与生命周期

Note 平铺在 `.agents/notes/`，命名为 `YYYY-MM-DD-topic.zh.md` 和 `.en.md`，首次提出日期、主题和语言保留在文件名中。[Note 元数据 schema](../schemas/note-metadata.yaml) 使用 YAML 编写的标准 Draft-07 JSON Schema，是允许字段、必填字段、值约束与类别白名单的唯一格式来源。未知字段会被拒绝；需要新增类别时直接扩展 schema 枚举。没有跨语言元数据相等门禁。本 Note 的 category 为 repository。

[maintain-notes skill](../skills/maintain-notes/SKILL.md) 描述完整工作过程及 Agent 必须作出的语义判断。工作过程中的生命周期为：

```text
new -> proposed
proposed -> implemented | rejected
implemented -> superseded | archived
superseded -> archived
```

Agent 创建 Note 时从 proposed 开始，只有 proposed 正文允许修改。implemented 文件保持不变，或转为 superseded、archived，此时仅修改状态和覆盖引用。superseded 可以维护覆盖引用并转为 archived，正文和其他元数据保持冻结。rejected、archived 整份文件冻结，包括引用顺序。任何状态都不能返回 proposed。

superseded 表示部分内容被覆盖，archived 表示全部内容被覆盖。既有 `superseded_by` 字段保存一至十二个覆盖 Note 文件名组成的数组，归档时确定最终引用集合。[元数据 lint](../scripts/remark-note-metadata.mjs)允许同一平铺目录内跨语言引用。superseded 来源的每个直接目标必须是 implemented 或 archived，不能是 superseded；archived 来源的目标可以是 implemented、superseded 或 archived。这些约束逐条作用于直接引用，不继承为所有后继的限制。允许多个目标和共享目标，每条分支都必须合法且无环，不要求替代记录日期递增。

Agent 必须保证 implemented Note 和 superseded Note 尚未被覆盖的部分准确。转为 archived 前，引用集合必须合计覆盖原文的全部事实、决定、约束、例外及代码行为描述，其中也包括它从更旧 Note 承接的事实、约束与覆盖范围。后继可以完整归档，部分覆盖的祖先保持不变：`A superseded -> B archived -> C superseded -> D implemented` 合法。保留已冻结的归档引用并追踪实际后继，不为此改写祖先链接。目标必须对应实际实现。合法引用图无法证明语义覆盖或当前事实；Agent 负责判断，通过新的替代记录维护，不改写冻结正文。

[Git 快照检查器](../scripts/check-note-history.mjs)对照 HEAD 或显式基准比较真实工作树，检查可达的快照变化，不追踪每次中间编辑或逐条重放历史提交。proposed 可以在两次快照间修改并到达任一后续可达状态。新文件首次进入比较时可以已经处于任一合法状态，不强制单独提交 proposed；尚无首次提交的仓库使用空基准。这无法证明最初的工作过程或所选基准之前的历史。门禁已接入 `check:notes`。

只为有长期价值的决策和取舍写 Note，不要求所有变更自动留档。不引入中央索引、状态或类别目录树、按年龄清理的配额、sidecar hash 或重复的变更流水账。

## 元数据工具与校验职责

[共享解析器](../scripts/frontmatter.mjs)复用 remark、frontmatter 解析和正规 YAML 库。[通用筛选器](../scripts/filter-frontmatter.mjs)只读当前目录下的 `.md`、`.markdown`，不改写文件；扫描范围由任务传入，不在工具中硬编码 Note 规则。

```sh
mise run notes --where status=proposed --where category=repository --json
```

重复的 `--where key=value` 按 AND 组合。键为顶层字面键，值按 YAML 字面量解析并深比较，支持字符串、数字、布尔、null、数组和映射；缺字段不等于 null。JSON 分别输出每个文件的路径和元数据。不存在的目录返回空结果，坏 YAML 和读取错误明确失败。

通用筛选器不校验 Note 文件名、必填字段、状态取值、生命周期转移或冻结内容。YAML schema 定义元数据格式，remark Note lint 执行该格式并检查文件名与覆盖引用；快照兼容由 Git 检查器负责，语义准确性由 Agent 维护，避免把一个小型浏览工具扩成状态管理系统。

## 测试与受影响检查

产品测试不混入产品逻辑，工具测试不混入工具实现。现有辅助工具使用 Node 内置测试能力；目前尚无 C# 测试框架、C# 测试项目或已实现的受影响测试选择器。

讨论确定的方向是提供可见的检查计划，根据变更和声明依赖选择项目或测试组，并纳入相关双语伙伴与入链文档。运行时协议、持久数据、生成代码、平台适配和共享 fixture 可能需要补充影响判断；被应用作为资源使用的文档不能只凭扩展名当作纯文档变更。

不单为可测试性强拆产品架构，也不要求每次小改都执行全部测试。没有默认的每日全量策略；具体依赖模型、回退规则、测试与平台矩阵仍待后续设计。

## 依赖与实现成本

合适时可以使用官方扩展。引入第三方依赖时衡量它消除的复杂度与带来的维护成本；简单的本仓策略用小型辅助脚本维护。复杂语法复用成熟解析器和算法，不用正则重新实现。

校验、测试、注释、文档和兼容承诺都有成本，应服务具体关切，不以基础设施数量评判质量。不为项目当前不需要支持的情况不断堆叠自维护修补。

## Agent 协作

初始化阶段由协调 Agent 与用户讨论方案和决策，具体工作交给子代理。独立调查和互不冲突的实现并行推进，需要用户决定的问题串行提出；共享文件集保持单一写者，并保留其他未提交改动。

## 待定选择与当前限制

UI 实现、业务协议的可靠性与身份策略、有条件的旧 Windows 支持、C# 测试框架、受影响检查选择，以及额外的文档和配置 lint 工具仍待确定。当前仓库提供 Hello World 协议样例和上述基础设施，尚未完成客户端/服务端产品。这些后续选择不属于初始化基线的完成范围，继续按具体需求设计和验证。
