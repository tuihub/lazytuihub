---
status: implemented
category: repository
---
# Repository-wide defensive posture and engineering trade-offs

English | [中文](2026-11-16-defensive-posture.zh.md)

## Scope and purpose

This Note establishes shared defensive trade-offs across product code, protocols, durable data, internal calls, tests, dependencies, development tools, CI, deployment, documentation, and agent workflows. Defense includes validation, error handling, limits, compatibility branches, permissions, version pinning, and automated gates rather than security scanning alone.

This Note records the adopted defensive-posture agreement used to review the current repository. Implemented means the principles are effective and the necessary current changes have been made. It does not claim every future business mechanism or a complete security audit.

## Maintenance through complete replacement

The effective Note's body is frozen. For changes to its body or agreements, create a complete bilingual successor that carries forward all effective principles and states the new choices rather than maintaining this topic through incremental patches. The successor starts as proposed and becomes implemented after semantic coverage review and implementation. The current Note remains effective while its successor is a draft.

Once the successor fully covers the previous Note, archive the previous Note by changing only status and superseded_by metadata to reference the successor, preserving its body and other metadata. Maintain this topic through complete replacement rather than long-lived partial superseded records. Update the project skill, AGENTS.md, and other current entry points to the effective successor together.

Reuse existing Note metadata and lifecycle checks. Maintainers still review complete semantic coverage; add no maintenance fields, sidecar records, or dedicated validator for this policy.

## Default position

Use the minimum defense that satisfies actual needs and explicit agreements. Judge input provenance, trust boundaries, failure consequences, and maintenance cost. Conservatism, check counts, and earlier failure are not quality goals by themselves.

Use known constraints, observed failures, credible risk analysis, and the consequences of the current operation as evidence. An additional measure should explain what it protects, how it works, and why its implementation, runtime, and maintenance costs are justified. General best practices and tool recommendations inform that judgment rather than replacing it.

Trust follows data provenance and semantic boundaries. In-process calls do not automatically have business permission, and local files may originate externally. Controlled internal code may rely on established types and invariants. When prerequisites for identity, authorization, or durable-data safety cannot be established, fail explicitly rather than interpreting uncertainty as permission to proceed.

## Defensive boundaries by area

| Area | Default posture and responsibility |
| - | - |
| Public APIs, WebSocket, and other external input | Treat input as untrusted and validate protocol structure, caller identity, permissions, and business constraints at the appropriate boundary. Base necessary resource protection on actual cost and risk rather than adding arbitrary character, length, or feature allowlists. |
| In-process APIs and application services | Share business validation, permission semantics, and write constraints. Call application services directly without loopback HTTP, repeated serialization, or simulated transport contexts merely to appear uniform. |
| Domain logic and state changes | Preserve actual business invariants, transactions, and side-effect consistency. Implement deduplication, concurrency control, and operation recovery when applicable business scenarios arise rather than pre-building general systems for a stateless sample. |
| Internal modules, dependency injection, and type boundaries | Prefer types, construction, nullable analysis, and explicit calling agreements. Do not repeat already-established checks at every layer. Expose programming errors clearly rather than hiding them through empty results, default success, or broad exception handling. |
| Storage, files, and external services | Handle realistic I/O and communication failures while respecting data ownership, transactions, and agreed overwrite and recovery semantics. Preserve unknown write outcomes; retries must fit operation idempotency rather than serve as a general repair mechanism. |
| Product lint, tests, and generated code | Enforce adopted product rules, let tests express scenarios directly, and check generated compilation and consistency. Do not fragment tests, add meaningless logic, or manually clean generator style to satisfy lint. New tests address concrete risks. |
| SDKs, dependencies, and GitHub Actions | Distinguish build compatibility, source trust, and execution permissions, using existing ecosystem mechanisms. Evaluate fixed versions and update tracking separately rather than treating supply-chain risk as a reason for maximum pinning everywhere. |
| Local CLIs, initialization, and development helpers | Validate arguments, paths, ownership, and conditions required for execution, with diagnosable errors. Preserve existing configuration and user edits without building extra rejection, migration, or compatibility frameworks for unpromised historical usage. |
| CI and automated checks | Cover obtainable main snapshots and actual execution results with the necessary minimum permissions. Verify promised platforms and capabilities without auditing local development processes or demanding unavailable information. Report real execution failures accurately. |
| Deployment, release, and credentials | Grant permissions for actual operations and protect credentials and durable data. Apply isolation and recovery to real external side effects; additional signing, approval, or audit systems require concrete needs. |
| Documentation, Notes, skills, and agents | Retain current agreements, decision rationale, and necessary evidence, reading references relevant to each task. Respect explicit Note lifecycle and generation boundaries without adding sidecar evidence, duplicate records, or formal gates because semantic correctness cannot be automatically proven. |

## Limits, errors, and compatibility

Evaluate resource budgets separately from functional limits. CPU, memory, concurrency, and retry budgets may follow resource cost and operational goals. Output length, word counts, data sizes, and allowed types require actual needs, explicit evidence, or user agreement. When necessity or a threshold is unclear, explain consequences and trade-offs rather than choosing a conservative value arbitrarily.

Inspect upstream defaults before adopting them, particularly content loss, silent truncation, automatic retries, and error handling. Defaults have consequences and do not establish suitability. Retain the existing 1 MiB (1,048,576-byte) limit for tracked and non-ignored new repository files without extending it to business files or API requests.

Do not add rejection branches for unpromised behavior that can be handled safely. Design compatibility for promised usage rather than automatically supporting every old version, historical call, or platform. Handle errors at their responsible boundary without swallowing them, truncating data, or reporting success, and without accumulating fallback paths for hypothetical failures.

## Dependencies and version references

Existing SDK, compiler, and protocol-tool versions remain the build baseline under their authoritative configuration. Packages and lockfiles retain existing ecosystem mechanisms. Maintenance cost does not authorize weakening explicit version agreements or maintaining another copy of the same version.

Trusted official or maintainer-provided Actions may follow supported major-version tags and receive corresponding maintenance updates. Pin a full SHA when a particular implementation, concrete audit requirement, or execution authority and consequences justify tighter control, accounting for updates and long-term cost. Third-party origin alone does not mandate SHA pinning; assess origin, permissions, purpose, and maintenance capacity together.

A fixed reference covers only what it actually fixes, without establishing complete environment reproducibility or safety. Tracking tags and pinning commits are trade-offs that require more reasoning than a claim of greater security. A choice in one location does not automatically become the default for all repository dependencies.

## Evidence for additional measures

For a new check, limit, dependency pin, test, compatibility branch, or gate, provide the following in suitable code structure, explanation, or review context without creating a separate per-measure registry:

- The actual boundary, data, or behavior being protected and evidence-based failure consequences.
- How the measure works and whether existing types, frameworks, or responsibility boundaries already cover it.
- Effects on normal use, false positives, maintenance, updates, and execution cost.

Resolve routine trade-offs directly from existing agreements and evidence. Clarify only choices genuinely requiring the user that cannot be inferred from context; hypothetical risk does not automatically trigger approval. Do not alter correct business behavior to satisfy tools or weaken existing gates to hide defects.

## Relationship to existing agreements

This Note refines implementation-cost principles in the [initialization agreements](2026-11-15-repository-initialization.en.md), alongside [C# lint boundaries](2026-11-15-csharp-lint.en.md), the [protocol baseline](2026-11-15-protobuf-api-contract.en.md), and [CI scope](2026-11-16-github-ci.en.md). Explicit agreements on product/test isolation, file limits, AIP adoption, generated documentation, and frozen Notes remain effective.

It does not wholly replace those Notes, rewrite frozen bodies, or automatically disable rules, change permissions, or relax data protection. Identify conflicts and address concrete choices within their actual scope. Validate later implementation when its needs and conditions arise rather than pre-building every defensive facility to mark a Note complete.
