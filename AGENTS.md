# Project conventions

## Engineering trade-offs

Use the [defensive-review skill](.agents/skills/defensive-review/SKILL.md) when choosing or reviewing defensive measures. The [repository defensive-posture Note](.agents/notes/2026-11-16-defensive-posture.en.md) is the policy source for validation, limits, compatibility, dependencies, tests, tooling, and automation. Follow actual trust boundaries, failure consequences, explicit agreements, and maintenance cost. Preserve existing explicit gates; additional conservatism needs a concrete project basis.

Maintain that policy through complete replacement Notes: implement a full successor, archive the previous Note without rewriting its body, and update the skill and entry-point links together. A proposed successor does not replace the current effective policy.

## Third-party Internet access

Repository code may retrieve third-party data or call a third-party service over the Internet only through a channel that the third party officially provides. This applies to plugins, tools, prototypes and fallback paths. Official database dumps, feeds, login APIs and APIs served to a site's own frontend can all qualify. For frontend APIs, follow the site's robots.txt, honor rate limits and use a widely compatible User-Agent that truthfully identifies LazyTuiHub. Local file access is unrestricted by this rule. See the [third-party access Note](.agents/notes/2026-11-26-third-party-access.en.md).

## MVP choices remain provisional

**No MVP technology or architecture choice is immutable.** Until the MVP is merged into main, frameworks, dependencies, design-system libraries, protocols, storage implementations, schemas, and component boundaries may all be reconsidered or replaced. Existing code, earlier selections, passing tests, and previous commits are evidence of the current implementation, not reasons to preserve it. Optimize for the final MVP deliverable. Replacement cost, sunk implementation effort, minimizing rework, and preserving the current stack are not decision criteria during the MVP. Replace earlier work whenever the final result calls for it; do not silently narrow work to installed dependencies, retain an inferior option to save work, or add compatibility for discarded prototypes.

Long-term maintenance ownership, third-party stability, and switching exposure after the MVP are valid design criteria. Distinguish these future obligations from the sunk effort and replacement cost of the current prototype. Self-maintained components are an option, not an automatic preference.

When reviewing a limitation, distinguish incorrect use, a limitation of the installed library/version, an available alternative, and a verified framework/platform constraint. Do not generalize one package's missing feature to the whole ecosystem. Update the relevant proposed MVP Notes with changed decisions and evidence. Frozen repository/API Notes retain their documented lifecycle; that lifecycle governs how decisions are revised, not whether a technology can ever be replaced.

## Tools and environment

Use mise to manage development tools and tasks on macOS, Windows, and Linux. Install mise using the [official installation guide](https://mise.jdx.dev/installing-mise.html).

Follow the [repository .NET toolchain and delivery policy](.agents/notes/2026-11-24-dotnet-delivery.en.md). Use `global.json` as the version source for the .NET SDK and `package.json` for Node.js and pnpm. Let mise read and manage these versions; do not duplicate declarations.

Run `mise doctor project` to inspect the current environment. The [project diagnostics](.mise/conf.d/doctor.toml) report problems and suggested fixes without installing tools or repairing the environment. Use `mise install` to install project tools managed by mise.

## Development commands

```sh
mise install
mise run setup
mise doctor project
mise run check
```

Keep frequent commands and project basics in `mise.toml`. The default list (`mise tasks ls`) covers setup, building, running, testing, code lint and formatting, and Note lookup. `mise run check` runs the full local checks. Its individual checks live in [the shared check definitions](.agents/mise-checks.toml) and can be listed with `mise tasks ls --hidden` or invoked by name.

Use `mise -E development tasks ls` for specialized development tasks, including API generation, CI reproduction, desktop review, multi-node tests and experiments. Put personal tasks in the automatically loaded, Git-ignored `mise.local.toml`. Use `mise -E production run release [rid]` for the self-contained JIT host and remote CLI. The [release guide](docs/development/release.en.md) describes the packages.

## API development

Use the [API design skill](.agents/skills/api-design/SKILL.md) when designing or changing Proto contracts, HTTP mappings, or API behavior. The [API development guide](docs/development/api-design.en.md) covers the adopted standard and development workflow; consult relevant official AIP originals from the local checkout prepared by `mise run setup:aip`. Its upstream revision is declared in `mise.toml`.

Proto is the source of transport contracts and API reference content. Write interface explanations and examples in Proto comments. Run `mise -E development run generate:api` after contract changes and `mise run check:api` to verify Google rules and generated artifacts. Use `mise -E development run demo:api` to compare local calls with the generated HTTP client.

`docs/api/` is entirely generated English Markdown, an explicit exception to bilingual pairing. Keep all other Markdown and file-size checks. Generation owns the complete directory and removes stale pages; do not add handwritten files there. Put handwritten development instructions in `docs/development/`. This pipeline does not deploy a documentation website.

## Continuous integration

The [CI workflow](.github/workflows/ci.yml) checks main-branch snapshots. Run `mise -E development run check:ci` to reproduce its quality job locally; `mise -E development run check:workflows` validates workflow definitions. CI uses snapshot checks rather than local commit-message or Note-history gates. The local `mise run check` entry point retains those development checks.

The separate [CodeQL workflow](.github/workflows/codeql.yml) analyzes supported repository languages and uploads security results. The [CI Note](.agents/notes/2026-11-16-github-ci.en.md) records its coverage, compatibility limits, and pending runner validation.

## MVP architecture and development

Business plugins own their services and transport adapters. Keep data relationships and execution in `src/LazyTuiHub.Framework/`, shared node types and helpers in `src/LazyTuiHub.Nodes/`, and timer and HTTP implementations in the Core plugin. RSS owns subscriptions and parsing. Articles accepts drafts from any source and owns article storage and reading. The [streaming guide](docs/development/streaming-mvp.en.md) maps these responsibilities to code and runnable examples.

Plugins may compose operations with workflows. Keep CLI, HTTP and GUI actions understandable without graph knowledge. Graphs are stored as JSON in SQLite. `--command workflow` exposes inspection and editing for plugin development. The fixed primary owns planning and scheduling. It delegates a step only when local execution is unavailable and the operation permits fallback. Workers advertise resources separately from workflow requirements. Owner-bound writes have one eligible target. The [framework](.agents/notes/2026-11-17-mvp-framework.en.md), [node execution](.agents/notes/2026-11-17-mvp-node-execution.en.md) and [coordination](.agents/notes/2026-11-19-mvp-coordination.en.md) Notes distinguish the target contract from the current push prototype.

Each lowercase `lazytuihub.plugin.<publisher>.<name>` ID owns one root project and entry DLL. By default, name the project, assembly and root namespace after the full dotted PascalCase ID. `PackageId` can change independently. Reference `LazyTuiHub.Sdk` for registration and workflow contracts, and `LazyTuiHub.Sdk.Avalonia` for GUI contributions. Declare entry points (`Server`, `Client`, `ClientGui`, `ClientCli`) and relationships in the project. The SDK generates the embedded manifest and code identities. The host builds the distribution index, loads trusted DLLs at cold start and selects entries for its mode. Keep diagnostics and saved metadata available when even a required plugin is missing. Prefer typed workflow nodes for sharing. Direct integrations own their compatibility and may use package references outside this repository. Use `--command plugins` for lifecycle actions. Enablement takes effect after restart. [Plugin contract](.agents/notes/2026-11-17-mvp-plugins.en.md)

Plugins pass namespaced tables to reusable DAO tools. `src/LazyTuiHub.Dao/` defines portable, backend-specific and native access contracts. Storage.Sqlite implements records with optional TTL, FTS5 and native connections. Shared cleanup visits only explicitly delegated targets. RSS observations default to permanent retention. Use `--command dao` to inspect or manage cleanup. [Storage and DAO](.agents/notes/2026-11-17-mvp-storage-dao.en.md)

Run `mise -E development run demo:workflow` for synchronized configuration and streaming, `mise -E development run demo:rss` for RSS, HTTP and SQLite, and `mise -E development run test:multinode` for Docker network and resource scenarios. `mise run test` runs the product suite locally and in CI.

CLI and GUI share the [interactive capability contract](.agents/notes/2026-11-23-mvp-win7-cli.en.md). Register application operations once with `PluginRegistration.AddCapabilities<T>` and `[Capability]`. Dedicated views call the typed operations. The CLI and desktop operation browser derive discovery, arguments and invocation from those same methods. Select the local workspace or the HTTPS client before composing presentations.

The [desktop guide](docs/development/desktop-mvp.en.md) covers running Avalonia locally and against a remote endpoint. Keep generic composition in `src/LazyTuiHub.Desktop/` and business pages in their plugin’s `Gui/` directory. Business, storage and workflow code run independently of the renderer. Local desktop mode uses application services in-process without an HTTP listener. [UI selection evidence](.agents/notes/2026-11-17-mvp-ui-comparison.en.md)

Use `mise -E development run test:desktop` to drive the product UI with programmatic pointer/keyboard input and produce screenshots plus control-state JSON in `.local/desktop/`. For new UI and changes to shared controls, follow the [desktop UI self-review standard](docs/development/desktop-ui-review.en.md). Declare relevant interaction targets in the product test and run `mise -E development run review:desktop` for local state differences, glyph measurements and reference comparisons. Functional checks do not establish visual fidelity. The proposed [design-system audit](.agents/notes/2026-11-18-mvp-design-systems.en.md) separates integration errors, library limits, alternatives, and maintenance ownership. Use `mise -E development run run:design-baselines` to view official references and `mise -E development run render:design-baselines:avalonia` to regenerate isolated library specimens; these experiments remain outside default CI.

The optional [UI comparison guide](docs/development/ui-comparison.en.md) covers the retained Avalonia and Uno prototypes in `.agents/prototypes/ui-comparison/`. They share a reader fixture and graph model, with separate framework-native DLL plugin pages. Use `mise -E development run demo:ui:avalonia` to run the selected framework's prototype; `mise -E development run demo:ui:uno` and `mise -E development run check:ui` remain available to reproduce the comparison. These experiments are outside the product solution and default CI; Uno is retained as comparison evidence.

## Commits

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). The initial development dependency install runs `prepare` to register the repository-local commit-message hook. A no-op reinstall may skip `prepare`; use `mise run setup:hooks` to register it again when needed. The shared setup preserves existing custom hooks by refusing to replace them. The hook checks without rewriting messages or installing missing tools.

`mise run check:commit` checks HEAD; use `--edit <message-file>` for a candidate or `--revision <commit>` for another commit. The check imposes no extra type whitelist, case policy, or length limit. Local hooks can be bypassed with Git's `--no-verify`; they do not enforce remote repository policy.

## Repository layout

- `src/`: Application host, frontends, framework, and shared plugin SDK.
- `plugins/`: Bundled plugins, their public types for direct integrations, and plugin-owned tests.
- `tests/`: Product tests, organized as separate projects from product logic.
- `docs/`: Current documentation for users and developers. Keep the latest content on the main branch; do not create a versioned documentation tree.
- `.agents/`: Development tools, their tests, and dated decision history.

Run `mise run check:files` to check tracked files and non-ignored new files. Each file must fit within 1 MiB (1,048,576 bytes). Each directory may contain up to 16 directly contained handwritten C# files, including tests, code-behind and partial classes. When a directory exceeds the limit, review its responsibilities and group related files together. See the [C# directory layout Note](.agents/notes/2026-11-25-csharp-directory-layout.en.md) for counting conventions.

## Notes

Keep decision Notes flat in `.agents/notes/` as `YYYY-MM-DD-topic.zh.md` and `.en.md` pairs. The [Note metadata schema](.agents/schemas/note-metadata.yaml) defines the authoritative frontmatter format and category whitelist.

Use the [maintain-notes skill](.agents/skills/maintain-notes/SKILL.md) when creating, changing, reviewing, rejecting, partially superseding, or fully archiving Notes. It defines the working lifecycle, frozen-content boundaries, and semantic coverage review that automated checks cannot perform.

Until the MVP is merged into main, keep all MVP-specific Notes in `proposed`, freely editing their content and organization as the design evolves, even when parts have been implemented. Each MVP commit includes a coherent, runnable code increment and corresponding Note updates: implement settled contracts carefully and record simple provisional behavior for unsettled details. Review their maturity and status when preparing the merge rather than promoting them automatically. This agreement does not reopen existing frozen repository or API Notes. The MVP has no compatibility commitment for its exploratory code, graph definitions, or SQLite schema; favor a clean result over preserving prototype behavior.

Use `mise run notes --where status=implemented --json` to filter Notes. `check:markdown` validates current metadata and references; `check:notes` checks snapshot transitions and frozen content against Git. `mise run check` includes both.
