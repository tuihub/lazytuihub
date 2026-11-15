---
name: maintain-notes
description: Create, edit, review, reject, partially supersede, or fully archive this repository's decision Notes while preserving frozen content and checking semantic coverage.
---

# Maintain decision Notes

Use this skill for decisions recorded in `.agents/notes/`. Ordinary document editing and generic frontmatter filtering do not require the Note lifecycle.

## Authority and scope

Read the [project conventions](../../../AGENTS.md), the [metadata schema](../../schemas/note-metadata.yaml), relevant existing Notes, and the current code, configuration, or tests that establish the decision's facts. The schema is the single authority for metadata fields, values, and reference-array bounds; do not copy its format into another schema or template. Add a category to its enum only when a real decision needs that category.

Keep Notes flat as `YYYY-MM-DD-topic.zh.md` and `.en.md` pairs. Use the first-proposed date in the filename. Do not create an index, sidecar snapshot, or record for every change. Preserve the bilingual document rules without imposing cross-language metadata equality.

## Lifecycle and frozen content

Create a new Note as `proposed`. It may be edited while the decision matures. A separate commit recording the proposed stage is unnecessary.

The working transitions are:

```text
new -> proposed
proposed -> implemented | rejected
implemented -> superseded | archived
superseded -> archived
```

No state returns to proposed. Apply these editing boundaries:

| Current state | Allowed changes |
| --- | --- |
| proposed | Edit content and metadata; implement or reject the decision when justified. |
| implemented | Leave the file unchanged, or move to superseded or archived by changing only status and coverage references. |
| superseded | Maintain coverage references or move to archived; preserve the body and all other metadata. |
| rejected or archived | Leave the whole file unchanged, including reference order. |

`superseded` means partially covered; `archived` means fully covered. Keep the existing `superseded_by` field as an array of one to twelve Note filenames. Finalize that reference set when archiving. Do not rename the field, add coverage metadata, or edit a frozen body to make a check pass.

The reference graph permits multiple targets and shared targets. Cross-language references within the same flat directory are allowed; filename dates need not increase. For a superseded source, each direct target must be implemented or archived, never superseded. For an archived source, each target may be implemented, superseded, or archived. These restrictions apply to each direct edge; do not propagate a partial ancestor's restriction through an archived successor. Follow every branch; self references, missing targets, and cycles are invalid.

## Semantic review before a transition

- Before implementing, confirm that the decision is mature and that its claims match the actual implementation. Distinguish implemented behavior, prerequisites, exceptions, and goals that remain unimplemented.
- Before rejecting, record the genuine reason while the Note is still proposed. Rejected content is then frozen.
- Before partial supersession, identify which claims the targets implement and which claims remain effective in the old Note. Ensure those remaining claims stay accurate. Each target must represent actual implementation rather than a proposal; follow archived targets to their effective successors.
- Before archiving, verify that the referenced Notes **collectively cover every fact, decision, constraint, exception, and described code behavior in the old Note**. Check the whole body, not only its main conclusion, and preserve the facts, constraints, and coverage scope this Note inherited from older Notes. Otherwise an older Note's claimed coverage can silently become incomplete. Existing references, valid states, and an array within the schema's bound do not prove complete coverage.

Keep implemented Notes accurate descriptions of the current project and keep uncovered content in superseded Notes accurate. If a frozen Note becomes inaccurate, create a proposed correction or replacement and apply the appropriate coverage transition. Do not rewrite its body, weaken a gate, or rewrite Git history to conceal the mismatch. A successor may be fully archived without archiving its partial ancestors or rewriting their references. For example, `A superseded -> B archived -> C superseded -> D implemented` is valid. Trace effective successors to confirm inherited coverage remains represented; archived reference lists stay frozen as historical links.

## Find Notes and run checks

Use the existing [generic frontmatter filter](../../scripts/filter-frontmatter.mjs) through the project task, for example:

```sh
mise run notes --where status=implemented --json
mise run notes --where category=repository --where status=proposed --json
```

The filter is read-only and does not validate Note policy. Conditions combine with AND; values are YAML literals. Use its `--help` through the script when more detail is needed.

After editing a Note pair or its references, run the existing checks from the repository root:

```sh
mise run check:markdown
mise run check:notes
```

The [metadata lint](../../scripts/remark-note-metadata.mjs) checks the current schema, filenames, and reference graph within the remark pipeline. The [history checker](../../scripts/check-note-history.mjs) compares the actual working tree with HEAD, using an empty baseline before the first commit. To review against a particular earlier commit, select it explicitly with `mise run check:notes --base <commit>`; do not silently change the baseline to bypass a violation. Use a baseline that covers the changes being reviewed and report its scope when that matters.

The history check compares snapshots, so a proposed baseline may reach any later reachable state without commits for each intermediate stage. A newly seen file may first appear in any valid state. These facts do not replace the working convention of creating Notes as proposed.

Automated checks establish format, file/reference validity, graph constraints, snapshot reachability, and frozen-content compatibility with the chosen baseline. They cannot prove initial uncommitted actions, all earlier Git history, implementation maturity, current semantic truth, or complete coverage. Make those judgments explicitly during review. Do not automatically commit Notes.
