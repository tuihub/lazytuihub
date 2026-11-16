# Project conventions

## Engineering trade-offs

Use the [defensive-review skill](.agents/skills/defensive-review/SKILL.md) when choosing or reviewing defensive measures. The [repository defensive-posture Note](.agents/notes/2026-11-16-defensive-posture.en.md) is the policy source for validation, limits, compatibility, dependencies, tests, tooling, and automation. Follow actual trust boundaries, failure consequences, explicit agreements, and maintenance cost. Preserve existing explicit gates; additional conservatism needs a concrete project basis.

Maintain that policy through complete replacement Notes: implement a full successor, archive the previous Note without rewriting its body, and update the skill and entry-point links together. A proposed successor does not replace the current effective policy.

## Tools and environment

Use mise to manage development tools and tasks on macOS, Windows, and Linux. Install mise using the [official installation guide](https://mise.jdx.dev/installing-mise.html).

Use `global.json` as the version source for the .NET SDK and `package.json` for Node.js and pnpm. Let mise read and manage these versions; do not duplicate declarations.

Run `mise doctor project` to inspect the current environment. It reports problems and suggested fixes without installing tools or repairing the environment. Use `mise install` to install project tools managed by mise.

## Development commands

```sh
mise install
mise exec -- pnpm install --frozen-lockfile
mise exec -- dotnet tool restore
mise run setup:aip
mise run check
```

Use `mise run <task>` to build and run the application, run checks, or format C# code. Task definitions live in `mise.toml`; use `mise tasks ls` to list the available commands.

## API development

Use the [API design skill](.agents/skills/api-design/SKILL.md) when designing or changing Proto contracts, HTTP mappings, or API behavior. The [API development guide](docs/development/api-design.en.md) covers the adopted standard and development workflow; consult relevant official AIP originals from the local checkout prepared by `mise run setup:aip`. Its upstream revision is declared in `mise.toml`.

Proto is the source of transport contracts and API reference content. Write interface explanations and examples in Proto comments. Run `mise run generate:api` after contract changes and `mise run check:api` to verify Google rules and generated artifacts. Use `mise run demo:api` to compare local calls with the generated HTTP client.

`docs/api/` is entirely generated English Markdown, an explicit exception to bilingual pairing. Keep all other Markdown and file-size checks. Generation owns the complete directory and removes stale pages; do not add handwritten files there. Put handwritten development instructions in `docs/development/`. This pipeline does not deploy a documentation website.

## Continuous integration

The [CI workflow](.github/workflows/ci.yml) checks main-branch snapshots. Run `mise run check:ci` to reproduce its quality job locally; `mise run check:workflows` validates workflow definitions. CI uses snapshot checks rather than local commit-message or Note-history gates. The local `mise run check` entry point retains those development checks.

The separate [CodeQL workflow](.github/workflows/codeql.yml) analyzes supported repository languages and uploads security results. The [CI Note](.agents/notes/2026-11-16-github-ci.en.md) records its coverage, compatibility limits, and pending runner validation.

## Streaming MVP

The [streaming MVP guide](docs/development/streaming-mvp.en.md) explains the framework, RSS module, SQLite workspace, and runnable examples. Keep generic data relationships and pipeline execution in `src/LazyTuiHub.Framework/`, common nodes in `src/LazyTuiHub.Nodes/`, RSS behavior in `src/LazyTuiHub.Rss/`, and transport adaptation in the host. Modules register nodes rather than adding business branches to the runner.

The proposed [MVP framework Note](.agents/notes/2026-11-17-mvp-framework.en.md) records framework responsibilities, execution plans, and public-network execution boundaries. The proposed [node execution Note](.agents/notes/2026-11-17-mvp-node-execution.en.md) records named ports, streaming and synchronized delivery, and node state. The first local implementation has named streaming/synchronized ports, grouped input delivery, and executable local plans; resource scheduling, remote execution, and replay remain design targets. The guide describes the current runnable prototype.

The proposed [plugin Note](.agents/notes/2026-11-17-mvp-plugins.en.md) records trusted, in-process DLL loading at cold startup, framework-owned metadata, preservation despite disabled or missing plugins, and contractual cleanup/uninstall hooks. Plugin contracts/composition live in `src/LazyTuiHub.Plugins/`; the host ships core and RSS DLLs through `plugins/plugins.json` without runtime implementation references. Keep framework diagnostics available when any plugin, including core, is missing. Use `--command plugins` for management and `--command workflow` for independent graph tools; changes take effect after restart.

The proposed [storage and DAO Note](.agents/notes/2026-11-17-mvp-storage-dao.en.md) records three access modes for all DAO resources: consistent encapsulation across implementations, backend-specific encapsulation, and native access. Contracts live in `src/LazyTuiHub.Dao/`; the cold-start `storage.sqlite` provider in `src/LazyTuiHub.Storage.Sqlite/` implements records/TTL, FTS5, and a native connection source. Plugins pass namespaced tables to reusable tools. Common cleanup only processes explicit persisted delegations; RSS details do not expire unless configured. Use `--command dao` for diagnostics and retention controls. SQLite remains the sole implemented backend; durable execution inputs and cross-store coordination remain open.

Run `mise run demo:workflow` for streaming with synchronized configuration updates and cancellation. Run `mise run demo:rss` for an isolated local RSS/HTTP/SQLite example with a cross-business branch. Run `mise run test` for product tests; both local checks and CI include them. The current interface is CLI and generated HTTP APIs; pipeline definitions are editable JSON stored in SQLite. This is an exploratory implementation, without a visual graph editor or distributed execution guarantees.

## Commits

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). The initial development dependency install runs `prepare` to register the repository-local commit-message hook. A no-op reinstall may skip `prepare`; use `mise run setup:hooks` to register it again when needed. The shared setup preserves existing custom hooks by refusing to replace them. The hook checks without rewriting messages or installing missing tools.

`mise run check:commit` checks HEAD; use `--edit <message-file>` for a candidate or `--revision <commit>` for another commit. The check imposes no extra type whitelist, case policy, or length limit. Local hooks can be bypassed with Git's `--no-verify`; they do not enforce remote repository policy.

## Repository layout

- `src/`: Product code.
- `tests/`: Product tests, organized as separate projects from product logic.
- `docs/`: Current documentation for users and developers. Keep the latest content on the main branch; do not create a versioned documentation tree.
- `.agents/`: Development tools, their tests, and dated decision history.

Tracked files and non-ignored new files must not exceed 1 MiB (1,048,576 bytes). Run `mise run check:files` to check this limit. Oversized files fail validation instead of being skipped by other checkers.

## Notes

Keep decision Notes flat in `.agents/notes/` as `YYYY-MM-DD-topic.zh.md` and `.en.md` pairs. The [Note metadata schema](.agents/schemas/note-metadata.yaml) defines the authoritative frontmatter format and category whitelist.

Use the [maintain-notes skill](.agents/skills/maintain-notes/SKILL.md) when creating, changing, reviewing, rejecting, partially superseding, or fully archiving Notes. It defines the working lifecycle, frozen-content boundaries, and semantic coverage review that automated checks cannot perform.

Until the MVP is merged into main, keep all MVP-specific Notes in `proposed`, freely editing their content and organization as the design evolves, even when parts have been implemented. Each MVP commit includes a coherent, runnable code increment and corresponding Note updates: implement settled contracts carefully and record simple provisional behavior for unsettled details. Review their maturity and status when preparing the merge rather than promoting them automatically. This agreement does not reopen existing frozen repository or API Notes. The MVP has no compatibility commitment for its exploratory code, graph definitions, or SQLite schema; favor a clean result over preserving prototype behavior.

Use `mise run notes --where status=implemented --json` to filter Notes. `check:markdown` validates current metadata and references; `check:notes` checks snapshot transitions and frozen content against Git. `mise run check` includes both.
