---
status: implemented
category: repository
---
# C# lint and test analysis

English | [中文](2026-11-15-csharp-lint.zh.md)

## Status and scope

This Note records the implemented C# lint configuration and the boundary between product and test rules. Analyzers, formatting, duplication, and repository file-size checks are connected. Status is implemented; the test framework, additional dedicated rules, and test timeouts remain open within the boundaries below.

Current behavior is defined by [Directory.Build.props](../../Directory.Build.props), [Directory.Build.targets](../../Directory.Build.targets), the [shared EditorConfig](../../.editorconfig), the [product diagnostic set](../lint/product.globalconfig), and [mise.toml](../../mise.toml). Product projects use Recommended, security extensions, and third-party analyzers, retaining CA1821 and CA2200 as errors. The solution still has no C# product test project or selected framework; separate configuration samples verify test isolation.

The plan supports native development on modern macOS, Windows, and Linux. Markdown, Note metadata, and commit messages continue to use the existing checks.

## Comparison baseline and tool responsibilities

The reference is the `.golangci.yml` read from the [tuihub/librarian](https://github.com/tuihub/librarian) working tree for this discussion. It explicitly enables 75 linters and two formatters, golines and gci, covering defects, security, resource handling, complexity, duplication, dependency boundaries, and style. Organize the migration around those purposes; rule counts do not establish equivalent coverage.

| Tool | Responsibility in this plan |
| - | - |
| Analyzers included in the .NET SDK | Use Recommended as the product baseline, with additional resource, dataflow, and security checks. |
| Meziantou.Analyzer | Add checks for asynchronous usage, cancellation propagation, exception wrapping, and method length. |
| SonarAnalyzer.CSharp | Add cyclomatic and cognitive complexity, magic numbers, repeated strings, and other applicable defect checks. |
| CSharpier | Format C#, wrap lines, and order using directives consistently. |
| jscpd | Detect duplicated handwritten product C#. |

Analyzers run during build; formatting and duplication have separate checks. Sonar's standalone NuGet analyzer is not the complete SonarQube product. Server-side duplication and advanced security capabilities must not be counted as standalone coverage. This plan does not require a SonarQube service.

## Product code rules

Apply the following baseline to product projects. These product settings must not be placed unconditionally in root configuration inherited by every project:

```xml
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisLevelSecurity>latest-all</AnalysisLevelSecurity>
```

`latest` follows the SDK selected by [global.json](../../global.json), without a second SDK version declaration. Preserve the existing CA1821 and CA2200 gates. Start with each package's default enabled set and apply explicit additions and exemptions through the product configuration, without promoting every rule to warning or error.

| Concern | Selected rules and handling |
| - | - |
| Dead conditions and ineffective logic | Enable CA1508 for conditions proved constant by dataflow. |
| Resource disposal | Enable CA2000 and CA2213 for local resources and disposable fields. |
| Asynchronous resource lifetime | Enable CA2025 for paths where tasks still use disposed resources. |
| Repeated enumeration | Enable CA1851 for repeated enumeration costs or side effects. |
| ValueTask usage | Include CA2012 in build checks for incorrect consumption or reuse. |
| Cancellation propagation | Use MA0040 as the owner and disable duplicate CA2016 and S8949 reports. |
| Blocking in asynchronous code | Promote MA0042 to a build warning. |
| Exception wrapping | Use MA0054 to detect a wrapper that omits the original exception. |
| Regular expression safety | Use MA0009 to detect missing match timeouts. |
| Magic numbers and repeated strings | Enable S109 and S1192, with exceptions grounded in business meaning. |
| Logging | Retain CA2017 and CA2023; promote CA2253 and CA2254 for arguments, placeholders, and dynamic templates. |
| Unused code | Use IDE0059 and IDE0060 for ineffective assignments and parameters, supplemented by applicable Sonar and Meziantou rules for private members and internal types. |

Configure these additional diagnostics as warnings and enforce them in the code check. A diagnostic does not need an automatic fix to be enforced. Ownership transfers, reflection entry points, and concurrent behavior may require local configuration or suppression; these checks must not be described as having no false positives.

The application is intended to provide a desktop UI. CA2007 and MA0004 are not repository-wide gates. Decide whether to change synchronization context at each relevant call site rather than requiring `ConfigureAwait(false)` on every await. Do not introduce blanket requirements to ban finalizers, seal every type, or document every public member with XML comments.

## Complexity, formatting, and duplication

Use the Go configuration as the basis for thresholds without adding tighter limits:

| Check | Configuration | Basis |
| - | - | - |
| Cyclomatic complexity | S1541: `maximumFunctionComplexityThreshold = 30`. | `cyclop.max-complexity: 30`. |
| Cognitive complexity | S3776: `threshold = 20` and `propertyThreshold = 20`. | `gocognit.min-complexity: 20`; explicitly apply the same threshold to property accessors. |
| Method lines | `MA0051.maximum_lines_per_method = 100`. | `funlen.lines: 100`. |
| Method statements | `MA0051.maximum_statements_per_method = 50`. | `funlen.statements: 50`. |
| Formatting target width | CSharpier reads `max_line_length = 120`. | `golines.max-len: 120`. |
| Duplicate fragments | Start jscpd at 150 tokens, normalize identifiers and literals, and fail on qualifying duplicates. | The Go configuration retains dupl's default threshold of 150 tokens. |

S1541 owns cyclomatic complexity; do not also enable CA1502 for duplicate reporting. Configure Sonar parameters through `.agents/lint/SonarLint.xml` included as AdditionalFiles, and MA0051 parameters directly in `.editorconfig`. CSharpier reads indentation, line endings, and width from the existing EditorConfig instead of duplicating formatting settings. The width of 120 is a wrapping target, not a hard limit on every line.

When adopting CSharpier, disable IDE0055 and replace the existing full `dotnet format` formatting gate to avoid conflicting formatters. Use the C# convention of System directives first and the rest sorted, rather than reproducing Go's module-prefix groups mechanically.

The [jscpd configuration](../../.jscpd.json) scans handwritten product C# under src, skips comments, and excludes bin, obj, and common generated filenames. Add actual paths when introducing other generator outputs. Tests are outside the duplication scope, and no extra file-line limit is set. Retain the tool's default 1 MiB file-size limit; the preceding repository check rejects oversized files instead of relying on the detector to skip them silently.

Its token definitions and structural analysis differ from Go dupl. Complexity, length, and duplication can be aligned by purpose, but their results are not interchangeable across languages. There is no direct existing gate selected for `cyclop.package-average: 10`, and S1192 is not an exact replacement for goconst.

## Repository file sizes

Tracked files and non-ignored new files must not exceed 1 MiB (1,048,576 bytes), as explicitly agreed by the user. Oversized files fail validation; widening scan limits or skipping them does not satisfy the requirement. Dependencies and build outputs follow Git ignore rules. Tracked files remain subject to the limit even when an ignore pattern matches them.

The [file-size checker](../scripts/check-file-sizes.mjs) enumerates files through Git and precedes check:analyzers, check:format, and check:duplicates. Pending deletions follow the current working tree; dependency folders and Git internals are not traversed.

## Dedicated test rules

Tests use a separately selected rule set. They do not first inherit the complete product policy and then escape individual warnings. Tests must express inputs, expected values, exceptions, and boundaries directly, without adding defensive branches, splitting scenarios, extracting meaningless constants, or introducing shared abstractions solely to satisfy product lint.

| Check | Product code | Test code |
| - | - | - |
| Compilation, types, and nullable | Retain. | Retain; allow explicit `null!` in negative tests. |
| Automatic formatting | Retain. | Retain. |
| Test signatures, assertions, and asynchronous test usage | Select where applicable. | Add applicable analyzers after selecting the test framework. |
| Method length, cyclomatic complexity, and cognitive complexity | Enforce. | Do not gate. |
| Magic numbers, repeated strings, and duplicated code | Enforce with justified exceptions. | Do not gate; allow direct inputs, expected values, and repeated setup. |
| Performance and allocation suggestions | Apply product policy. | Do not gate. |
| Cancellation propagation, exception wrapping, and logging conventions | Apply product policy. | Do not inherit product requirements. |
| Disposal and security rules | Apply broadly. | Select by purpose; allow deliberately exceptional, leaking, or unsafe inputs. |
| Business dependency boundaries and complete initialization | Apply product agreements. | Allow test doubles, partial construction, and deliberately incomplete data. |

The Go configuration already exempts tests from bodyclose, dupl, errcheck, funlen, goconst, gosec, noctx, and wrapcheck, while retaining complexity and magic-number constraints. This plan removes those remaining product constraints from test gates to address the observed maintenance cost. Express the difference in project or test-directory configuration rather than requiring repeated suppressions in each test.

Compilers and general-purpose analyzers do not automatically understand every negative test. A general defect rule must not enter the test set merely because it has few false positives in product code. The framework remains undecided; this Note selects neither xUnit, NUnit, nor MSTest and does not assume identical analyzer coverage across them.

## Configuration ownership and execution

Shared root configuration holds language, nullable, formatting, and method-length parameters. Directory.Build.targets selects rules after IsTestProject is available: test projects disable general SDK and build-time style analyzers and do not reference product analyzer packages; other C# product projects enable the product set. This does not blanket-disable a future framework's own analyzers. Add the product globalconfig directly to EditorConfigFiles, avoiding late GlobalAnalyzerConfigFiles additions after the SDK has collected compiler inputs. Do not classify rules solely by test method or filename suffix.

Analyzer versions are pinned in Directory.Build.targets with `PrivateAssets="all"` to avoid transitive development dependencies. CSharpier is pinned in the SDK-created [dotnet-tools.json](../../dotnet-tools.json), allowing tool runtime roll-forward; jscpd uses the existing [package.json](../../package.json) and [pnpm-lock.yaml](../../pnpm-lock.yaml). Do not duplicate .NET, Node.js, or pnpm version sources. Restore development tools with `mise exec -- pnpm install --frozen-lockfile` and `mise exec -- dotnet tool restore`. Doctor checks availability without installing or repairing tools.

| Configuration location | Responsibility |
| - | - |
| Shared build configuration and project-role settings | Common language settings and the scope of product and test analyzers. |
| Product globalconfig and shared EditorConfig | Product severities, MA0051 parameters, and CSharpier settings; tests do not load the product diagnostic set. |
| `.agents/lint/SonarLint.xml` | Sonar complexity and other rule parameters, included as AdditionalFiles. |
| Native manifests, .csharpierignore, and duplication configuration | Tool versions and scopes; CSharpier formats C# without taking over XML, project, or solution files. |
| `mise.toml` | Unified build, check, and formatting entry points. |

`mise run check:code` aggregates these checks, with file-size validation preceding all three code entry points:

| Check | Execution |
| - | - |
| Repository file sizes | `mise run check:files`. |
| Compilation and selected analyzers | `dotnet build LazyTuiHub.slnx --no-incremental -warnaserror`. |
| Formatting | `dotnet csharpier check .`, respecting the configured scan scope. |
| Product duplication | `pnpm exec jscpd`, reading repository configuration. |

Keep ordinary build as the development build. Warnings from selected rules fail check:code. Disable unselected product rules in tests before diagnostics are produced rather than ignoring the check process's failure. `dotnet format --verify-no-changes` is not a substitute for enforcing diagnostics without automatic fixes. Checks do not rewrite source; formatting changes remain behind the separate format:code entry point.

## Exceptions and coverage still to establish

Express exceptions by rule and actual location using native configuration. Do not introduce repository-wide ignores or blanket NoWarn. Explain concrete ownership, reflection, or framework-entry behavior. Suspicious code is not necessarily a business defect, and static analysis cannot establish domain requirements such as order transitions or monetary calculations by itself.

- Dependency and API bans: configure Microsoft.CodeAnalysis.BannedApiAnalyzers after component boundaries, banned APIs, and replacements are known. Do not copy Go package names or invent a ban list.
- Exhaustiveness: the compiler and IDE0010 or IDE0072 cover only some switch scenarios. The IDE rules may require enum members even when a default exists, making them stricter than the current Go rule that accepts default. Do not promote them directly to hard gates; map and sum-type checks also differ.
- Complete initialization: express required data with constructors, required members, and nullable analysis rather than requiring explicit values for every optional property.
- Global state and initialization: constrain state through C# project structure and lifetime. Do not translate gochecknoglobals and gochecknoinits into blanket bans on static members or type initializers.
- Suppression management: require a specific rule and a reason as policy. Unnecessary suppression detection, checking that a reason exists, and judging its meaning are separate concerns. This combination does not fully reproduce nolintlint; automated reason enforcement needs a separately selected or implemented rule.
- Identifier and character safety: this combination does not fully reproduce asciicheck or bidichk. Dedicated rules still need selection and validation.

Tool names or configuration comments do not close these gaps. When automated blocking is needed, define the required behavior and verify the corresponding implementation.

## Test duration

Test duration limits belong to execution-layer or CI timeout controls, outside lint. Inputs, dependencies, and the execution environment affect elapsed time; static analysis cannot reliably guarantee it.

This proposal records that boundary only. Timeout thresholds and execution mechanisms remain outside the current discussion, and lint gains no test-duration rule.

## Implementation and validation

`mise run test:lint` runs separate [analyzer, formatting, and duplication samples](../tests/csharp-lint.test.mjs) and [file-size boundary samples](../tests/check-file-sizes.test.mjs). These development-tool tests do not select a product test framework or run by default within every check:code. Samples use temporary projects and restored dependencies without downloading test dependencies.

Verified locally on macOS: product dead conditions, resource leaks, disposable fields, asynchronous resource lifetime, and cancellation propagation fail the gate. The same negative examples pass as a test project, while nullable errors still fail. Ownership transfer and correctly awaited operations are accepted. Cyclomatic complexity 30, method and property cognitive complexity 20, statement count 50, and line count 100 pass at the boundary and fail above it. Format checking preserves source, while the separate formatter repairs it. Renamed and literal-modified product clones fail; tests and configured generated files are excluded. File-size samples accept exactly 1 MiB and reject larger tracked or new files.

At this verification, the product entry point is below the duplicate-fragment threshold, so jscpd reports no qualifying files. Representative samples separately establish scanning and failure behavior. Passing checks do not establish false-positive rates for a complete application. Tools provide native entry points for the three development systems; this work did not execute on Windows or Linux hardware.

Use `mise run check`, `mise run test:lint`, and `mise doctor project` to verify the configuration. check:markdown and check:notes continue to validate Note metadata, bilingual structure, and lifecycle. Coverage gaps and open choices remain in their respective sections and are not recorded as implemented capabilities.

Configuration references: [.NET analysis modes](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysismode), [Meziantou rules and configuration](https://github.com/meziantou/Meziantou.Analyzer), [Sonar standalone analyzer configuration](https://github.com/SonarSource/sonar-dotnet#configuring-rules), [CSharpier configuration](https://csharpier.com/docs/Configuration), [formatter integration](https://csharpier.com/docs/IntegratingWithLinters), and [jscpd](https://github.com/kucherenko/jscpd).
