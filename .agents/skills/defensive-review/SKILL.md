---
name: defensive-review
description: Review defensive engineering choices and repository changes against the project's defensive-posture policy, especially validation, limits, compatibility, dependency references, and automation gates.
---

# Review defensive engineering choices

Read the active defensive-posture Note in [English](../../notes/2026-11-16-defensive-posture.en.md) or [Chinese](../../notes/2026-11-16-defensive-posture.zh.md). It is the policy source; do not duplicate it into this skill.

Inspect the affected agreements, implementation, and failure consequences. Apply the Note's criteria to concrete trust boundaries and costs. Preserve existing explicit requirements and necessary data protection. Prefer a targeted change supported by evidence over a general hardening pass or blanket weakening of checks.

For repository reviews, report the concrete findings, changes, retained protections, and relevant verification. A request to review does not authorize unrelated external actions or deployment. Ordinary work needs no new approval flow or per-measure record.

## Maintain the policy

The defensive-posture policy uses complete replacement Notes. Read the [maintain-notes skill](../maintain-notes/SKILL.md) when changing it. Create a complete successor, verify inherited coverage, and make it implemented before archiving the previous Note through status and superseded_by metadata only. Preserve the previous body. Update this skill and project entry points to the effective successor together; keep the current policy in force while a successor is proposed.
