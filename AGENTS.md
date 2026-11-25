# Project conventions

## Engineering trade-offs

Use the [defensive-review skill](.agents/skills/defensive-review/SKILL.md) when choosing or reviewing defensive measures. The [repository defensive-posture Note](.agents/notes/2026-11-16-defensive-posture.en.md) is the policy source for validation, limits, compatibility, dependencies, tests, tooling, and automation. Follow actual trust boundaries, failure consequences, explicit agreements, and maintenance cost. Preserve existing explicit gates; additional conservatism needs a concrete project basis.

Maintain that policy through complete replacement Notes: implement a full successor, archive the previous Note without rewriting its body, and update the skill and entry-point links together. A proposed successor does not replace the current effective policy.

## MVP choices remain provisional

**No MVP technology or architecture choice is immutable.** Until the MVP is merged into main, frameworks, dependencies, design-system libraries, protocols, storage implementations, schemas, and component boundaries may all be reconsidered or replaced. Existing code, earlier selections, passing tests, and previous commits are evidence of the current implementation, not reasons to preserve it. Optimize for the final MVP deliverable. Replacement cost, sunk implementation effort, minimizing rework, and preserving the current stack are not decision criteria during the MVP. Replace earlier work whenever the final result calls for it; do not silently narrow work to installed dependencies, retain an inferior option to save work, or add compatibility for discarded prototypes.

Long-term maintenance ownership, third-party stability, and switching exposure after the MVP are valid design criteria. Distinguish these future obligations from the sunk effort and replacement cost of the current prototype. Self-maintained components are an option, not an automatic preference.

When reviewing a limitation, distinguish incorrect use, a limitation of the installed library/version, an available alternative, and a verified framework/platform constraint. Do not generalize one package's missing feature to the whole ecosystem. Update the relevant proposed MVP Notes with changed decisions and evidence. Frozen repository/API Notes retain their documented lifecycle; that lifecycle governs how decisions are revised, not whether a technology can ever be replaced.

## Tools and environment

Use mise to manage development tools and tasks on macOS, Windows, and Linux. Install mise using the [official installation guide](https://mise.jdx.dev/installing-mise.html).

Follow the [repository .NET toolchain and delivery policy](.agents/notes/2026-11-24-dotnet-delivery.en.md). Use `global.json` as the version source for the .NET SDK and `package.json` for Node.js and pnpm. Let mise read and manage these versions; do not duplicate declarations.

Run `mise doctor project` to inspect the current environment. It reports problems and suggested fixes without installing tools or repairing the environment. Use `mise install` to install project tools managed by mise.

## Development commands

```sh
mise install
mise exec -- pnpm install --frozen-lockfile
mise exec -- dotnet tool restore
mise run setup:aip
mise run check
```

Use `mise run <task>` to build and run the application, run checks, or format C# code. Use `mise run release [rid]` to produce the self-contained JIT host and remote CLI. The [release guide](docs/development/release.en.md) describes the packages. Task definitions live in `mise.toml`; use `mise tasks ls` to list the available commands.

## API development

Use the [API design skill](.agents/skills/api-design/SKILL.md) when designing or changing Proto contracts, HTTP mappings, or API behavior. The [API development guide](docs/development/api-design.en.md) covers the adopted standard and development workflow; consult relevant official AIP originals from the local checkout prepared by `mise run setup:aip`. Its upstream revision is declared in `mise.toml`.

Proto is the source of transport contracts and API reference content. Write interface explanations and examples in Proto comments. Run `mise run generate:api` after contract changes and `mise run check:api` to verify Google rules and generated artifacts. Use `mise run demo:api` to compare local calls with the generated HTTP client.

`docs/api/` is entirely generated English Markdown, an explicit exception to bilingual pairing. Keep all other Markdown and file-size checks. Generation owns the complete directory and removes stale pages; do not add handwritten files there. Put handwritten development instructions in `docs/development/`. This pipeline does not deploy a documentation website.

## Continuous integration

The [CI workflow](.github/workflows/ci.yml) checks main-branch snapshots. Run `mise run check:ci` to reproduce its quality job locally; `mise run check:workflows` validates workflow definitions. CI uses snapshot checks rather than local commit-message or Note-history gates. The local `mise run check` entry point retains those development checks.

The separate [CodeQL workflow](.github/workflows/codeql.yml) analyzes supported repository languages and uploads security results. The [CI Note](.agents/notes/2026-11-16-github-ci.en.md) records its coverage, compatibility limits, and pending runner validation.

## Streaming MVP

The [streaming MVP guide](docs/development/streaming-mvp.en.md) explains the framework, RSS module, SQLite workspace, and runnable examples. Keep generic data relationships and pipeline execution in `src/LazyTuiHub.Framework/`, common node types and helpers in `src/LazyTuiHub.Nodes/`, built-in timer and fetch implementations in `plugins/LazyTuiHub.Plugin.TuiHub.Core/runtime/`, RSS subscriptions and ingestion in `plugins/LazyTuiHub.Plugin.TuiHub.Rss/runtime/`, and source-independent article storage and reading in `plugins/LazyTuiHub.Plugin.TuiHub.Articles/runtime/`. Each business plugin owns its transport adapter. Modules register nodes rather than adding business branches to the runner.

The proposed [MVP framework Note](.agents/notes/2026-11-17-mvp-framework.en.md) records framework responsibilities, execution plans, and public-network execution boundaries. The proposed [node execution Note](.agents/notes/2026-11-17-mvp-node-execution.en.md) records named ports, streaming and synchronized delivery, and node state. The planner matches workflow requirements to independently advertised instance resources. Repeatable packet operations can fall back across workers. Owner-bound writes target one eligible instance. Synchronized groups and queues stay on the coordinator. The guide describes the current runnable prototype.

The proposed [plugin Note](.agents/notes/2026-11-17-mvp-plugins.en.md) records trusted, in-process DLL loading at cold startup, framework-owned metadata, preservation despite disabled or missing plugins, and contractual cleanup/uninstall hooks. Each manifest declares a lowercase `lazytuihub.plugin.<publisher>.<name>` ID. Prefer its dotted PascalCase form for the package and source root under `plugins/`. The public registration and workflow SDK lives in `src/LazyTuiHub.PluginSdk/`; loading and catalog management live in `src/LazyTuiHub.Plugins/`. Prefer typed workflow nodes for shared behavior. Direct plugin integrations own their compatibility. A project may switch to package references when moved to another repository. The distribution index selects IDs and required membership; each cold startup reads the selected owner manifests; the host ships business and core DLLs through `plugins/plugins.json` without runtime implementation references. Keep framework diagnostics available when any plugin, including core, is missing. Use `--command plugins` for management and `--command workflow` for independent graph tools; changes take effect after restart.

The proposed [storage and DAO Note](.agents/notes/2026-11-17-mvp-storage-dao.en.md) records three access modes for all DAO resources: consistent encapsulation across implementations, backend-specific encapsulation, and native access. Contracts live in `src/LazyTuiHub.Dao/`; the cold-start SQLite provider in `plugins/LazyTuiHub.Plugin.TuiHub.Storage.Sqlite/` implements records/TTL, FTS5, and a native connection source. Plugins pass namespaced tables to reusable tools. Common cleanup only processes explicit persisted delegations; RSS details do not expire unless configured. Use `--command dao` for diagnostics and retention controls. SQLite remains the sole implemented backend; durable execution inputs and cross-store coordination remain open.

Run `mise run demo:workflow` for streaming with synchronized configuration updates and cancellation. Run `mise run demo:rss` for an isolated local RSS/HTTP/SQLite example with a cross-business branch. Run `mise run test:multinode` for optional Docker scenarios covering network asymmetry, resource changes, and remote primary writes. Run `mise run test` for product tests; both local checks and CI include them. The current interfaces are CLI, generated HTTP APIs, and an Avalonia desktop reader; pipeline definitions are editable JSON stored in SQLite. This is an exploratory implementation, without a visual graph editor or distributed execution guarantees.

CLI and GUI share the [interactive capability contract](.agents/notes/2026-11-23-mvp-win7-cli.en.md). Register application operations once with `PluginRegistration.AddCapabilities<T>` and `[Capability]`. Dedicated views call the typed operations. The CLI and desktop operation browser derive discovery, arguments and invocation from those same methods. Select the local workspace or the HTTPS client before composing presentations.

Avalonia is the current MVP renderer and remains replaceable under the provisional-choice agreement above. Keep business services, workflow execution, and storage independent of the renderer. The [UI selection Note](.agents/notes/2026-11-17-mvp-ui-comparison.en.md) records the decision, comparison evidence, and remaining platform work; it remains proposed under the MVP agreement. The [desktop MVP guide](docs/development/desktop-mvp.en.md) covers `mise run run:desktop`, real RSS/SQLite operations, workflow inspection, and cold-start plugin management. Keep generic UI composition in `src/LazyTuiHub.Desktop/` and the article reader in the dynamically loaded `plugins/LazyTuiHub.Plugin.TuiHub.Articles/desktop/`; business/framework code must remain independent of Avalonia. Desktop mode shares application services and tasks without an HTTP listener. Product tests include headless desktop integration.

Use `mise run test:desktop` to drive the product UI with programmatic pointer/keyboard input and produce screenshots plus control-state JSON in `.local/desktop/`. For new UI and changes to shared controls, follow the [desktop UI self-review standard](docs/development/desktop-ui-review.en.md). Declare relevant interaction targets in the product test and run `mise run review:desktop` for local state differences, glyph measurements and reference comparisons. Functional checks do not establish visual fidelity. The proposed [design-system audit](.agents/notes/2026-11-18-mvp-design-systems.en.md) separates integration errors, library limits, alternatives, and maintenance ownership. Use `mise run run:design-baselines` to view official references and `mise run render:design-baselines:avalonia` to regenerate isolated library specimens; these experiments remain outside default CI.

The optional [UI comparison guide](docs/development/ui-comparison.en.md) covers the retained Avalonia and Uno prototypes in `.agents/prototypes/ui-comparison/`. They share a reader fixture and graph model, with separate framework-native DLL plugin pages. Use `mise run demo:ui:avalonia` to run the selected framework's prototype; `mise run demo:ui:uno` and `mise run check:ui` remain available to reproduce the comparison. These experiments are outside the product solution and default CI; Uno is retained as comparison evidence.

## Commits

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). The initial development dependency install runs `prepare` to register the repository-local commit-message hook. A no-op reinstall may skip `prepare`; use `mise run setup:hooks` to register it again when needed. The shared setup preserves existing custom hooks by refusing to replace them. The hook checks without rewriting messages or installing missing tools.

`mise run check:commit` checks HEAD; use `--edit <message-file>` for a candidate or `--revision <commit>` for another commit. The check imposes no extra type whitelist, case policy, or length limit. Local hooks can be bypassed with Git's `--no-verify`; they do not enforce remote repository policy.

## Repository layout

- `src/`: Application host, frontends, framework, and shared plugin SDK.
- `plugins/`: Bundled plugins, their business-specific SDKs, and plugin-owned tests.
- `tests/`: Product tests, organized as separate projects from product logic.
- `docs/`: Current documentation for users and developers. Keep the latest content on the main branch; do not create a versioned documentation tree.
- `.agents/`: Development tools, their tests, and dated decision history.

Run `mise run check:files` to check tracked files and non-ignored new files. Each file must fit within 1 MiB (1,048,576 bytes). Each directory may contain up to 16 directly contained handwritten C# files, including tests, code-behind and partial classes. When a directory exceeds the limit, review its responsibilities and group related files together. See the [C# directory layout Note](.agents/notes/2026-11-25-csharp-directory-layout.en.md) for counting conventions.

## Notes

Keep decision Notes flat in `.agents/notes/` as `YYYY-MM-DD-topic.zh.md` and `.en.md` pairs. The [Note metadata schema](.agents/schemas/note-metadata.yaml) defines the authoritative frontmatter format and category whitelist.

Use the [maintain-notes skill](.agents/skills/maintain-notes/SKILL.md) when creating, changing, reviewing, rejecting, partially superseding, or fully archiving Notes. It defines the working lifecycle, frozen-content boundaries, and semantic coverage review that automated checks cannot perform.

Until the MVP is merged into main, keep all MVP-specific Notes in `proposed`, freely editing their content and organization as the design evolves, even when parts have been implemented. Each MVP commit includes a coherent, runnable code increment and corresponding Note updates: implement settled contracts carefully and record simple provisional behavior for unsettled details. Review their maturity and status when preparing the merge rather than promoting them automatically. This agreement does not reopen existing frozen repository or API Notes. The MVP has no compatibility commitment for its exploratory code, graph definitions, or SQLite schema; favor a clean result over preserving prototype behavior.

Use `mise run notes --where status=implemented --json` to filter Notes. `check:markdown` validates current metadata and references; `check:notes` checks snapshot transitions and frozen content against Git. `mise run check` includes both.
