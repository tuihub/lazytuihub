---
status: implemented
category: repository
---
# Repository initialization agreements

English | [中文](2026-11-15-repository-initialization.zh.md)

## Purpose

This first formal Note records the repository's implemented and checked initialization baseline and the reasons for its boundaries. The implemented status applies to repository infrastructure. Product targets and unresolved choices below are not claims that those features have been implemented.

Design the project from scratch without consulting the older business implementation in another repository of the organization. DeepSeek Harness informs the discussion of documentation maintenance; its templates, directory taxonomy, quotas, and consistency sidecars are not adopted as a package.

The executable sources of current behavior are the configuration and scripts linked below. [AGENTS.md](../../AGENTS.md) provides the short, durable entry point for agents.

## Product scope and identity

C# is the primary product language. The intended application supports UI operation on current macOS, Windows, and Linux, and headless operation on all target operating systems. Its client and server belong to one application and interact inside the same process; the server also exposes remote calls to other processes. Proto contracts, HTTP/JSON transcoding, and the Hello World local and remote example are connected as recorded in the [protocol Note](2026-11-15-protobuf-api-contract.en.md). UI, complete business behavior, authentication, and further component boundaries remain unimplemented or undecided.

Development must work natively on modern macOS, Windows, and Linux; Windows development does not require WSL. Windows 7 and Windows XP are conditional deployment targets under investigation. Virtual machines, host-side assistance, and explicit external dependencies are acceptable for that work. They must preserve one product architecture rather than becoming a separately developed application. This does not require installing the modern development SDK on XP and does not promise an implemented compatibility solution.

The brand combines Lazy and TuiHub. Tui retains its meaning in the TuiHub brand and is not an acronym for a UI technology. Use `LazyTuiHub` for C# names and `lazy-tuihub` for the tooling package.

## Toolchain and task entry points

Use mise for development tools and tasks on all three modern operating systems. Follow its [official installation instructions](https://mise.jdx.dev/installing-mise.html). Each version has one executable source of truth:

| Tool | Initial version | Version source |
| - | - | - |
| .NET SDK | 11.0.100 | [global.json](../../global.json), with roll-forward disabled and prereleases disallowed |
| C# | 15.0 | [Directory.Build.props](../../Directory.Build.props) |
| Node.js | 26.13.1 | `devEngines.runtime` in [package.json](../../package.json) |
| pnpm | 12.21.0 | `packageManager` in [package.json](../../package.json) |

[mise.toml](../../mise.toml) enables native version-file discovery instead of duplicating these pins. The [initial C# project](../../src/LazyTuiHub/LazyTuiHub.csproj) targets `net11.0`. pnpm is the package manager, and [pnpm-lock.yaml](../../pnpm-lock.yaml) is the single JavaScript dependency lockfile.

mise additionally pins Go, protoc, Google API Linter, and protoc-gen-openapi for protocol development; the product remains C#. NSwag shares the root dotnet-tools.json manifest with CSharpier. setup:aip prepares the official AIP originals as a local reference checkout, with repository and adopted commit declared only in mise.toml.

```sh
mise install
mise exec -- pnpm install --frozen-lockfile
mise exec -- dotnet tool restore
mise run setup:aip
mise doctor project
mise run check
```

Doctor checks the .NET SDK, Node.js, pnpm, CSharpier, jscpd, protocol compiler and generation tools, and the official AIP checkout's origin, commit, and worktree state. It reports repair suggestions without downloading tools, installing packages, or repairing the environment. Installation and reference preparation are explicit operations.

The current task interfaces are:

| Task | Responsibility |
| - | - |
| `build` | Build the solution. |
| `run` | Run the application. |
| `check:code` | Aggregate file sizes, product analyzers, C# formatting, and duplication without rewriting source. |
| `check:files` | Reject tracked and non-ignored new files larger than 1 MiB. |
| `check:analyzers` | Build and enforce warnings from selected analyzers. |
| `check:format` | Verify C# formatting without rewriting. |
| `check:duplicates` | Check duplication in handwritten product C#. |
| `test:lint` | Verify gates, thresholds, test isolation, and file-size boundaries; outside the default check chain. |
| `check:markdown` | Run the remark pipeline. |
| `check:notes` | Compare Note snapshots and frozen content against Git. |
| `setup:aip` | Prepare the official AIP checkout at the adopted commit without overwriting local edits. |
| `restore:api` | Restore common Proto and compilation dependencies for contracts. |
| `generate:api` | Explicitly generate OpenAPI, the HTTP client, and the Markdown reference from Proto. |
| `check:proto` | Run Google API Linter's default enabled rules. |
| `check:generated` | Regenerate temporarily and compare derived artifacts without rewriting source. |
| `check:api` | Aggregate Proto and generated-artifact checks. |
| `demo:api` | Compare in-process and generated-client HTTP calls for Hello World. |
| `check:commit` | Validate HEAD or an explicit commit-message input. |
| `setup:hooks` | Re-register the repository-local commit-message hook when needed. |
| `check` | Aggregate code, API, Markdown, Note history, and commit-message checks. |
| `format:code` | Apply C# formatting changes. |
| `notes` | List or filter frontmatter in the flat Notes directory. |

Use `mise run <task>` and consult `mise tasks ls`; the task definitions remain authoritative. Checking and formatting have separate entry points so validation does not silently rewrite files.

## Repository boundaries

- `src/` contains the product.
- `tests/` is reserved for product tests in projects separate from product logic. No C# test framework or test project has been selected yet.
- `docs/` holds current user and developer documentation when there is content to publish. The main branch carries current documentation without a self-managed version directory tree.
- `.agents/` holds development assistance and dated decision history. Executable helpers live in `.agents/scripts/`; their tests live separately in [.agents/tests/](../tests/).
- Root configuration remains in the native discovery locations required by its tools. Those files are entry points and shared configuration, not a reason to move auxiliary implementation out of `.agents/`.

Tracked files and non-ignored new files must not exceed 1 MiB (1,048,576 bytes). check:files rejects oversized files before code analysis, formatting, and duplication. Widening scan limits or silently skipping files does not satisfy this requirement.

Create skills only for an actual reusable workflow. Do not pre-create skill skeletons, notes, indexes, or empty documentation structures. Existing helpers remain their single executable implementation rather than being copied into skills.

## Commit messages

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). The [commit helper](../scripts/commits.mjs) uses the mature conventional-commits-parser package with a thin mandatory-syntax check. It accepts descriptive custom types, Unicode, uppercase text, optional scope and breaking-change markers, and multiline bodies and footers. It adds no type whitelist, length limit, case policy, or automatic merge/revert exemption. Agents still judge whether a type and description accurately describe the change.

On a fresh checkout, the `prepare` lifecycle in [package.json](../../package.json) calls the existing setup after development dependencies are installed, so a normal `pnpm install --frozen-lockfile` registers the hook automatically. A no-op reinstall may skip `prepare`; keep `mise run setup:hooks` as the manual repair entry point. The shared setup changes only this repository's `core.hooksPath` and refuses to replace an existing custom path or hook. The [commit-msg hook](../githooks/commit-msg) forwards the message filename safely to `mise run --skip-tools check:commit`; failures block the commit without rewriting its message or installing missing tools. The tracked executable bit and a [targeted LF attribute](../../.gitattributes) support native Git on the three development systems, including the shell supplied by Git for Windows.

`mise run check:commit` validates HEAD by default. Use `--edit <message-file>` for a candidate message or `--revision <commit>` for an existing commit; the same check is included in `check`. Local hooks can be bypassed with Git's `--no-verify`, so this mechanism does not establish remote enforcement. No CI workflow is added by this initialization.

## C# validation

Configure C# checks through [Directory.Build.props](../../Directory.Build.props), [Directory.Build.targets](../../Directory.Build.targets), [.editorconfig](../../.editorconfig), and the [product diagnostic set](../lint/product.globalconfig). Root configuration retains shared language and nullable settings, while project identity selects product and test policies. The [C# lint Note](2026-11-15-csharp-lint.en.md) records detailed rules, tool responsibilities, and validation boundaries.

The first explicit diagnostic gates are the common production-rule intersection reviewed across ASP.NET Core, Aspire, .NET Runtime, and CommunityToolkit:

| Rule | Severity | Meaning |
| - | - | - |
| CA1821 | error | Remove empty finalizers. |
| CA2200 | error | Rethrow exceptions without losing the original stack details. |

Product projects use SDK Recommended, a broader security set, and supplementary Meziantou and Sonar checks. Analyzer versions are pinned with PrivateAssets=all. Ordinary build retains development behavior, while check:code fails on warnings from selected rules. CSharpier manages C# formatting through a local tool manifest; jscpd is managed by pnpm and scans handwritten product code only. No file-header template has been selected, and upstream copyright text is not copied.

Test projects do not load product analyzers or the product diagnostic set. Compilation, nullable, and formatting remain; dedicated analyzers follow the eventual framework choice. Length, complexity, magic numbers, duplication, and product performance suggestions are not test gates. Generated source is not a target for manual style cleanup, but must still compile. Test duration belongs to the execution layer; lint has no timeout policy.

## Markdown validation

Use one remark ecosystem. The [root pipeline](../../.remarkrc.json) combines GFM and YAML frontmatter parsing, the official consistent and recommended lint presets, and explicit rules for fenced-code language flags, heading-level increments, and duplicate headings within a section. It imposes no hard line-length limit.

Keep native Markdown and HTML behavior rather than reconstructing another linter's policy. The baseline adds no blanket HTML ban or tag allowlist. YAML frontmatter syntax is recognized generally; Note-specific metadata requirements belong to the Note plugin.

The official relative-link plugin checks local Markdown links, file existence, and cross-file GitHub-style heading anchors offline. External URLs and site-root paths are skipped by the current configuration; raw HTML `href` targets are outside this check. Ordinary links to SVG fragments have an accepted known false-positive case, and there is no current use case requiring a custom repair layer. This does not prohibit SVG images.

Markdownlint is not part of this pipeline. Prettier and a general formatting or validation pipeline for JSON/XML files have not been selected; Note-specific metadata validation is defined separately below.

## Bilingual documents and ignore rules

Markdown documents admitted by the remark scan use same-directory, same-basename `.zh.md` and `.en.md` pairs, with the explicit exceptions below. The [bilingual helper](../scripts/remark-bilingual-structure.mjs) compares heading levels and order, plus code-block language counts. It does not compare translated text, heading slugs, code contents, paragraph counts, or line counts, and it does not force code-block ordering or list/table equality.

[.remarkignore](../../.remarkignore) is the independent documentation ignore file. The nearest file supplies the ignore rules; the helper does not merge all ancestor files or consult Git ignore settings. If either language file is ignored, the pair is skipped, including when an ignored partner is absent.

`AGENTS.md` and `SKILL.md` are explicitly exempt from bilingual naming and pairing because their tools require those names. They still pass the other remark rules. Ordinary `README.md` files do not inherit that exemption.

The entire `docs/api/` directory contains generated English Markdown, explicitly exempt from bilingual naming and pairing. The root remark configuration scopes this exception to that directory; other Markdown rules and the 1 MiB file limit remain active. API comments live in Proto, generation owns all reference pages, and handwritten development guides remain bilingual outside `docs/api/`. The [protocol Note](2026-11-15-protobuf-api-contract.en.md) records the generation pipeline. Documentation website deployment is outside API work.

[.gitignore](../../.gitignore) starts from the official Dotnet.gitignore template with repository tooling and operating-system entries. Git ignore and documentation ignore have separate responsibilities.

## Decision Notes and lifecycle

Keep Notes flat in `.agents/notes/` as `YYYY-MM-DD-topic.zh.md` and `.en.md`. The first-proposed date, topic, and language stay in the filename. The [Note metadata schema](../schemas/note-metadata.yaml), a Draft-07 JSON Schema written in YAML, is the single source for permitted fields, required fields, value constraints, and the category whitelist. Unknown fields are rejected; add categories directly to the schema enum when needed. There is no cross-language metadata-equality gate. This Note has category repository.

The [maintain-notes skill](../skills/maintain-notes/SKILL.md) describes the complete workflow and the semantic judgments that agents must make. The working lifecycle is:

```text
new -> proposed
proposed -> implemented | rejected
implemented -> superseded | archived
superseded -> archived
```

Agents create Notes as proposed. Only proposed bodies may be edited. An implemented file remains unchanged unless it moves to superseded or archived, changing only status and coverage references. A superseded Note may maintain those references and become archived, while preserving its body and other metadata. Rejected and archived files are fully frozen, including reference order. No state returns to proposed.

Superseded means partially covered; archived means fully covered. The existing `superseded_by` field holds an array of one to twelve covering Note filenames, finalized when archiving. The [metadata lint](../scripts/remark-note-metadata.mjs) accepts cross-language references in the same flat directory. Each direct target of a superseded source must be implemented or archived, not superseded. Targets of an archived source may be implemented, superseded, or archived. These are direct-edge constraints, not inherited restrictions on every descendant. Multiple targets and shared targets are allowed; every branch must be valid and free of cycles. Replacement dates need not increase.

Agents must keep implemented Notes and the uncovered portions of superseded Notes accurate. Before archiving, the referenced collection must collectively cover every fact, decision, constraint, exception, and described code behavior in the source. This includes the facts, constraints, and coverage scope inherited from older Notes. A successor may be fully archived while its partial ancestors remain unchanged: `A superseded -> B archived -> C superseded -> D implemented` is valid. Preserve archived references and trace their effective successors rather than rewriting ancestor links. The targets must represent actual implementation. A valid reference graph cannot prove semantic coverage or current truth; agents review these and create replacements instead of rewriting frozen bodies.

The [Git snapshot checker](../scripts/check-note-history.mjs) compares the actual working tree with HEAD or an explicit baseline. It checks reachable snapshot changes, not every intermediate edit or every historical commit. A proposed Note may be edited and reach any later reachable state between snapshots. A newly seen file may first appear in any valid state; no separate proposed commit is required. An unborn repository has an empty baseline. This does not prove the initial working process or history before the chosen baseline. The gate is wired into `check:notes`.

Write Notes for lasting decisions and trade-offs, not automatically for every change. No centralized index, lifecycle/category directory tree, age-based deletion quota, sidecar hash, or repeated change log is introduced.

## Metadata tools and validation responsibilities

The [shared parser](../scripts/frontmatter.mjs) reuses remark, frontmatter parsing, and the standard YAML library. The [generic filter](../scripts/filter-frontmatter.mjs) reads top-level `.md` and `.markdown` files without rewriting them. Its scope is supplied by the task rather than hardcoded as a Note policy.

```sh
mise run notes --where status=proposed --where category=repository --json
```

Repeated `--where key=value` conditions use AND. Keys are literal top-level keys; values are parsed YAML literals and compared deeply, including strings, numbers, booleans, null, arrays, and mappings. Missing keys do not match null. JSON output contains each file's path and metadata independently. Missing directories are empty results; malformed YAML and read errors fail clearly.

The generic filter does not validate Note filenames, required fields, status values, lifecycle transitions, or frozen contents. The YAML schema defines the metadata format; remark Note lint enforces it and checks filenames and replacement references. Snapshot compatibility belongs to the Git checker, and semantic accuracy belongs to agents. This avoids turning a small browsing tool into a state-management system.

## Testing and affected checks

Keep product tests out of product logic, and tooling tests out of tooling implementation. Current helper tests use Node's built-in test runner. There is not yet a C# test framework, C# test project, or implemented affected-test selector.

The intended direction is a visible check plan that selects projects or groups from changes and declared dependencies, including relevant document partners and inbound links. Runtime protocols, durable data, generated code, platform adapters, and shared fixtures may require additional impact reasoning. A document used as an application resource is not automatically a documentation-only change.

Do not split product architecture solely for testability or require full testing for every small change. There is no default daily-full-test policy. The exact dependency model, fallback rules, and test/platform matrix remain future design work.

## Dependencies and implementation cost

Official extensions may be used when they fit. Evaluate third-party dependencies against the complexity they remove and the maintenance they introduce; keep simple repository-specific policy in small local helpers. Reuse mature parsers and algorithms for complex syntax rather than rebuilding them with regular expressions.

Every validation rule, test, comment, document, and compatibility promise has a cost. Add it for a concrete concern, not as a score for how much infrastructure exists. Do not accumulate custom fixes for unsupported cases that the project does not currently need.

## Agent collaboration

During this initialization, the coordinating agent discusses plans and decisions with the user and delegates concrete work to subagents. Independent research and non-conflicting implementation proceed in parallel. Questions that require the user's decision are presented serially. Keep one writer for a shared file set and preserve other uncommitted changes.

## Open decisions and current limits

UI implementation, reliability and identity policy for business APIs, conditional legacy-Windows support, C# test framework, affected-check selection, and additional document/configuration lint tools remain open. The repository provides the Hello World protocol example and infrastructure described above, not the completed client/server product. Those later choices are outside the initialization baseline's completion scope and remain subject to design and validation as concrete needs arise.
