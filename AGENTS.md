# Project conventions

## Tools and environment

Use mise to manage development tools and tasks on macOS, Windows, and Linux. Install mise using the [official installation guide](https://mise.jdx.dev/installing-mise.html).

Use `global.json` as the version source for the .NET SDK and `package.json` for Node.js and pnpm. Let mise read and manage these versions; do not duplicate declarations.

Run `mise doctor project` to inspect the current environment. It reports problems and suggested fixes without installing tools or repairing the environment. Use `mise install` to install project tools managed by mise.

## Development commands

```sh
mise install
mise exec -- pnpm install --frozen-lockfile
mise exec -- dotnet tool restore
mise run check
```

Use `mise run <task>` to build and run the application, run checks, or format C# code. Task definitions live in `mise.toml`; use `mise tasks ls` to list the available commands.

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

Use `mise run notes --where status=implemented --json` to filter Notes. `check:markdown` validates current metadata and references; `check:notes` checks snapshot transitions and frozen content against Git. `mise run check` includes both.
