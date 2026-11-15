---
status: implemented
category: repository
---
# C# lint 与测试检查

[English](2026-11-15-csharp-lint.en.md) | 中文

## 状态与范围

本 Note 记录已落地的 C# lint 配置，以及产品代码与测试代码的规则边界。分析器、格式化、重复代码与仓库文件大小检查已接入，状态为 implemented。测试框架、额外专用规则及测试超时策略仍按下文边界保留待定。

当前行为由 [Directory.Build.props](../../Directory.Build.props)、[Directory.Build.targets](../../Directory.Build.targets)、[共享 EditorConfig](../../.editorconfig)、[产品诊断集](../lint/product.globalconfig)和 [mise.toml](../../mise.toml) 决定。产品项目已使用 Recommended、安全增强和第三方分析器，保留 CA1821、CA2200 的 error 严重程度。当前解决方案仍没有 C# 产品测试项目，测试框架尚未确定；测试规则隔离由独立的配置验证样例检查。

本方案面向现代 macOS、Windows、Linux 上的原生开发流程。Markdown、Note 元数据和提交消息继续使用既有检查入口。

## 比较基准与工具分工

检查强度参考本次读取的 [tuihub/librarian](https://github.com/tuihub/librarian) 工作树中的 `.golangci.yml`。该配置显式启用 75 个 linter，以及 golines、gci 两个 formatter，覆盖程序缺陷、安全、资源释放、复杂度、重复代码、依赖边界和风格。迁移按检查目的组织，不以规则数量证明覆盖等价。

| 工具 | 在本方案中的职责 |
| - | - |
| .NET SDK 自带分析器 | 产品代码使用 Recommended 基础集，并补充资源、数据流与安全检查。 |
| Meziantou.Analyzer | 补充异步用法、取消传播、异常包装和函数长度检查。 |
| SonarAnalyzer.CSharp | 补充圈复杂度、认知复杂度、魔法数、重复字符串及其他适用的代码缺陷检查。 |
| CSharpier | 统一 C# 格式、自动换行和 using 排序。 |
| jscpd | 扫描手写产品 C# 中的重复代码。 |

分析器随构建运行，格式与重复代码单独检查。Sonar 的独立 NuGet 分析器不等于完整 SonarQube 产品，不能将服务端的重复代码或高级安全分析能力算入其覆盖。本方案不要求部署 SonarQube 服务。

## 产品代码规则

产品项目采用以下基础设置；这些产品设置不能无条件放入所有项目都会继承的根配置：

```xml
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisLevelSecurity>latest-all</AnalysisLevelSecurity>
```

`latest` 跟随 [global.json](../../global.json) 选定的 SDK，不另设一份 SDK 版本声明。保留既有 CA1821、CA2200 门禁。第三方规则以各包默认启用集为起点，由产品诊断集补充和豁免，不将各包全部规则统一提升为 warning 或 error。

| 关注点 | 选择的规则与处理 |
| - | - |
| 死条件与无效逻辑 | 补开 CA1508，根据数据流检查恒真、恒假条件。 |
| 资源释放 | 补开 CA2000、CA2213，检查局部资源和成员资源的释放。 |
| 异步资源生命周期 | 补开 CA2025，检查资源释放后任务仍在使用资源的路径。 |
| 重复枚举 | 补开 CA1851，检查重复执行枚举造成的成本或副作用。 |
| ValueTask 使用 | 将 CA2012 纳入构建检查，检查不当消费与复用。 |
| 取消传播 | 由 MA0040 统一检查，关闭重复的 CA2016、S8949 报告。 |
| 异步中的同步阻塞 | 将 MA0042 提升为构建警告。 |
| 异常包装 | 使用 MA0054 检查包装异常时遗漏原始异常。 |
| 正则安全 | 使用 MA0009 检查缺少匹配超时。 |
| 魔法数与重复字符串 | 启用 S109、S1192；按业务语义处理必要例外。 |
| 日志 | 保留 CA2017、CA2023，提升 CA2253、CA2254，检查参数、占位符及动态模板。 |
| 未使用代码 | 使用 IDE0059、IDE0060 检查无效赋值和参数，再由适用的 Sonar、Meziantou 规则补充私有成员和内部类型检查。 |

上述补充诊断采用 warning，并在代码检查入口中阻断；不要求每条诊断都具备自动修复。资源所有权转移、反射入口和并发行为可能需要局部配置或豁免，不能将这些检查描述为没有误报。

项目计划提供桌面 UI，CA2007、MA0004 不作为全项目门禁。是否切换同步上下文由调用位置决定，不统一要求所有 await 添加 `ConfigureAwait(false)`。不引入禁止所有终结器、要求所有类型 sealed、要求所有公开成员写 XML 文档等整体约束。

## 复杂度、格式与重复代码

阈值沿用 Go 配置中的依据，不额外收紧：

| 检查 | 配置 | 依据 |
| - | - | - |
| 圈复杂度 | S1541 的 `maximumFunctionComplexityThreshold = 30`。 | `cyclop.max-complexity: 30`。 |
| 认知复杂度 | S3776 的 `threshold = 20`、`propertyThreshold = 20`。 | `gocognit.min-complexity: 20`；属性访问器也明确使用该阈值。 |
| 函数行数 | `MA0051.maximum_lines_per_method = 100`。 | `funlen.lines: 100`。 |
| 函数语句数 | `MA0051.maximum_statements_per_method = 50`。 | `funlen.statements: 50`。 |
| 格式化目标宽度 | CSharpier 读取 `max_line_length = 120`。 | `golines.max-len: 120`。 |
| 重复片段 | jscpd 以 150 token 为起点，启用标识符和字面量归一化，发现达到条件的重复片段即失败。 | Go 配置未覆盖 dupl 的默认 150 token 阈值。 |

圈复杂度由 S1541 负责，不同时启用 CA1502 产生重复报告。Sonar 参数通过 `.agents/lint/SonarLint.xml` 的 AdditionalFiles 配置；MA0051 参数直接放入 `.editorconfig`。CSharpier 读取现有 EditorConfig 的缩进、行尾和宽度设置，避免重复维护格式参数。120 是换行目标，不是所有行都必须满足的硬限制。

采用 CSharpier 后关闭 IDE0055，替换现有完整 `dotnet format` 的格式门禁，避免两套格式规则冲突。using 采用 System 在前、其余排序的 C# 格式约定，不机械复制 Go 的模块前缀分组。

[jscpd 配置](../../.jscpd.json)限定扫描 src 下的手写产品 C#，跳过注释，并按文件名排除 bin、obj 和常见生成文件；其他生成器输出在引入时配置实际路径。测试目录不参与重复检查，不设置额外的文件行数上限。保留工具默认的 1 MiB 文件大小上限，超限文件由前置仓库检查拒绝，不依赖重复检查器静默跳过。

它与 Go dupl 的结构分析、token 定义不同；复杂度、行数和重复检查都只能按同类目标对齐，不能承诺跨语言计算结果相同。`cyclop.package-average: 10` 尚无直接对应的现成门禁；S1192 也不能视为 goconst 的逐项替代。

## 仓库文件大小

仓库中已跟踪的文件及未被 Git 忽略的新文件不得超过 1 MiB（1,048,576 字节）。该阈值来自用户明确约定。超过阈值必须使检查失败，不能通过放宽扫描上限或跳过文件让检查通过。依赖与构建输出沿用 Git 忽略规则；已跟踪文件即使匹配忽略规则仍受检查。

[文件大小检查器](../scripts/check-file-sizes.mjs)通过 Git 枚举文件，并在 check:analyzers、check:format、check:duplicates 之前执行。删除中的文件按工作树现状处理，不遍历依赖目录或 .git 内部文件。

## 测试专用规则

测试使用独立选择的规则集，不先继承完整产品规则再逐条逃避告警。测试应能直接表达输入、预期值、异常和边界条件，不因产品 lint 增加防御分支、拆散场景、提取无意义常量或创建共享抽象。

| 检查 | 产品代码 | 测试代码 |
| - | - | - |
| 编译、类型、nullable | 保留。 | 保留；负例测试允许明确使用 `null!`。 |
| 自动格式化 | 保留。 | 保留。 |
| 测试签名、断言和异步测试用法 | 按实际用途选择。 | 测试框架确定后接入其适用的分析器。 |
| 函数长度、圈复杂度、认知复杂度 | 作为门禁。 | 不设门禁。 |
| 魔法数、重复字符串、重复代码 | 作为门禁，允许必要例外。 | 不设门禁，允许直接展示输入、预期值和重复的准备步骤。 |
| 性能优化、减少分配等建议 | 按产品规则检查。 | 不设门禁。 |
| 取消传播、异常包装和日志规范 | 按产品规则检查。 | 不继承产品要求。 |
| 资源释放和安全规则 | 广泛检查。 | 按用途选择，允许故意构造异常、泄漏和不安全输入。 |
| 业务依赖边界和完整初始化 | 按产品约定。 | 允许测试替身、局部构造和故意不完整的数据。 |

Go 配置已对测试豁免 bodyclose、dupl、errcheck、funlen、goconst、gosec、noctx、wrapcheck，但仍保留复杂度、魔法数等约束。本方案根据测试维护中的实际成本，进一步将这些产品约束移出测试门禁。规则差异在项目或测试目录配置中统一表达，不要求每个测试重复写 suppression。

编译器和通用分析器不自动理解所有负例测试；通用缺陷规则也不能仅因在产品中误报少就直接加入测试集。测试框架尚未选择，本 Note 不预设 xUnit、NUnit 或 MSTest，也不承诺其分析器覆盖范围相同。

## 配置归属与执行入口

根级共享配置承载语言、nullable、格式和方法长度参数。Directory.Build.targets 在项目属性可用后按 IsTestProject 选择规则：测试项目关闭 SDK 通用分析器及构建时风格分析，不引用产品分析器包；其他 C# 产品项目启用产品规则。后续测试框架的专用分析器不因此被统一关闭。产品 globalconfig 直接加入 EditorConfigFiles，避免在 SDK 已收集 GlobalAnalyzerConfigFiles 后添加项目项而漏掉编译器输入。不能只按测试方法或文件名后缀区分规则。

分析器包在 Directory.Build.targets 中固定版本并使用 `PrivateAssets="all"`，避免作为传递开发依赖泄漏。CSharpier 固定在 SDK 原生创建的 [dotnet-tools.json](../../dotnet-tools.json)，允许工具随可用的较新运行时运行；jscpd 使用既有 [package.json](../../package.json) 和 [pnpm-lock.yaml](../../pnpm-lock.yaml)。不重复声明 .NET、Node.js、pnpm 的版本。首次使用前执行 `mise exec -- pnpm install --frozen-lockfile` 和 `mise exec -- dotnet tool restore`。Doctor 只检查工具可用性，不安装或修复。

| 配置位置 | 职责 |
| - | - |
| 共享构建配置与项目角色设置 | 共同语言设置，以及产品、测试分析器的作用范围。 |
| 产品 globalconfig 与共享 EditorConfig | 产品诊断严重程度、MA0051 参数、CSharpier 格式参数；测试不加载产品诊断集。 |
| `.agents/lint/SonarLint.xml` | Sonar 复杂度等规则参数，通过 AdditionalFiles 引入。 |
| 工具原生清单、.csharpierignore 和重复检查配置 | 工具版本及作用范围；CSharpier 只格式化 C#，不接管 XML、项目和解决方案文件。 |
| `mise.toml` | 构建、检查、格式化的统一入口。 |

`mise run check:code` 聚合以下检查，文件大小检查在三个代码检查入口之前执行：

| 检查 | 执行方式 |
| - | - |
| 仓库文件大小 | `mise run check:files`。 |
| 编译和已选分析器 | `dotnet build LazyTuiHub.slnx --no-incremental -warnaserror`。 |
| 格式 | `dotnet csharpier check .`，遵循配置的扫描范围。 |
| 产品重复代码 | `pnpm exec jscpd`，读取仓库配置。 |

普通 build 保持开发构建；check:code 中已选规则的 warning 使检查失败。测试中未选的产品规则应在产生诊断前停用，而不是通过忽略检查进程的失败放行。`dotnet format --verify-no-changes` 不能代替没有自动修复能力的诊断门禁。检查不写回源码，格式化修改继续通过独立的 format:code 入口执行。

## 例外与尚未覆盖的能力

豁免按规则和实际位置表达，优先使用工具原生配置，不引入整仓库忽略或全部 NoWarn。针对对象所有权、反射和框架入口说明具体原因。通用的“看起来可疑”不等于业务错误，静态分析也不能证明订单状态、金额计算等领域约束正确。

- 依赖和 API 禁用：组件边界、禁用 API 及替代方式明确后，再配置 Microsoft.CodeAnalysis.BannedApiAnalyzers。不得照搬 Go 项目的包名或凭空创建禁用清单。
- 穷尽检查：编译器和 IDE0010、IDE0072 只能覆盖部分 switch 场景。IDE 规则可能要求补齐已有默认分支的枚举成员，比 Go 当前接受 default 的规则更严格，不直接设为硬门禁；map 与 sum type 的检查仍有差异。
- 完整初始化：用构造函数、required 和 nullable 表达必须提供的数据，不统一要求所有可选属性显式赋值。
- 全局状态与初始化：按 C# 的项目结构和状态生命周期约束，不把 gochecknoglobals、gochecknoinits 机械转成禁止所有 static 成员或类型初始化器。
- 抑制管理：约定指定规则并说明理由；无效抑制检查、说明存在性和说明是否合理是不同问题。现有组合不能宣称完整复刻 nolintlint，自动强制说明需要另外选择或实现专用规则。
- 标识符与字符安全：现有组合不能宣称完整复刻 asciicheck、bidichk；专用规则的选择和验证仍待完成。

以上缺口不能通过增加工具名称或配置注释视为已覆盖；需要自动阻断时，再明确所需行为并验证对应实现。

## 测试运行时间

测试运行时间约束属于测试执行层或 CI 的超时控制，不属于 lint。实际耗时受输入、依赖和执行环境影响，静态分析无法可靠保证。

本轮只记录这个边界，暂不讨论或选择超时门槛、执行机制，不在 lint 中加入运行时长规则。

## 落地与验证

`mise run test:lint` 执行独立的[分析器、格式和重复检查样例](../tests/csharp-lint.test.mjs)，以及[文件大小边界样例](../tests/check-file-sizes.test.mjs)。这些开发工具测试不选择产品测试框架，也不加入每次 check:code 的默认执行链。样例使用临时项目并复用已还原的依赖，不联网下载测试依赖。

已在本机 macOS 验证：产品死条件、资源泄漏、成员资源释放、异步资源生命周期和取消传播能阻断检查；同样的负例作为测试项目可以通过，nullable 错误仍会失败。所有权转移及正确等待的操作被接受。圈复杂度 30、方法和属性认知复杂度 20、语句数 50、行数 100 均验证阈值内通过、超过阈值失败。格式检查不会改写源码，独立格式命令可修复格式；改名及替换字面量后的重复产品代码会失败，测试和约定的生成文件被排除。文件大小样例验证恰好 1 MiB 可通过，超过 1 MiB 的已跟踪文件或新文件必须失败。

本次产品入口低于重复片段阈值，jscpd 报告没有符合条件的文件；代表性样例另行验证了扫描及阻断。检查通过不能证明完整业务中的误报水平。工具提供三个开发系统的原生入口，本次没有在 Windows、Linux 实机执行。

`mise run check`、`mise run test:lint` 和 `mise doctor project` 用于核对实际配置；Note 的元数据、双语结构和生命周期继续由 check:markdown、check:notes 验证。未覆盖能力和待定事项保留在对应章节，不将其记为已实现。

配置依据参见 [.NET 分析模式](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysismode)、[Meziantou 规则与配置](https://github.com/meziantou/Meziantou.Analyzer)、[Sonar 独立分析器配置](https://github.com/SonarSource/sonar-dotnet#configuring-rules)、[CSharpier 配置](https://csharpier.com/docs/Configuration)、[格式规则集成](https://csharpier.com/docs/IntegratingWithLinters)和 [jscpd](https://github.com/kucherenko/jscpd)。
