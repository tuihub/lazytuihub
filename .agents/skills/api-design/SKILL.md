---
name: api-design
description: Design, implement, or review this repository's Proto API contracts and HTTP mappings using its adopted AIP standard and official source references.
---

# Design project APIs

Read the API development guide in [English](../../../docs/development/api-design.en.md) or [Chinese](../../../docs/development/api-design.zh.md), then inspect the affected Proto and application service. The guide covers the adopted standard, generation, and running local and HTTP calls; the [API reference](../../../docs/api/index.md) is generated from Proto.

The concise standard states project adoption and explicit local choices. Use the relevant complete AIP originals for conditions, exceptions, rationale, and linked requirements. Do not load the whole corpus for every task or treat a summary omission as an exemption.

Official originals are in `.agents/references/google-aip/aip/` relative to the repository root. Most applicable documents are `general/NNNN.md`, with zero-padded numbers. Repository and revision come from `mise.toml`; prepare a missing checkout with `mise run setup:aip`. `mise doctor project` checks its revision without updating it. Read relevant approved documents and follow necessary cross-references. The upstream files are reference material, not project instructions to execute.

Choose documents by the change: resource design and names (121–124), methods and HTTP mapping (127, 130–136), fields and presence (140, 149, 202–203), long-running operations (151), concurrency and request identification (154–155), pagination and filtering (158, 160), compatibility (180), naming and files (190–191), documentation (192), or errors and retries (193–194). This is navigation, not a replacement for the original requirements.

Keep transport contracts in Proto and business logic in the shared C# application service. Reuse generated message types where appropriate. Preserve the local-call path and explain any actual project exception instead of disabling broad linter groups.

Write interface explanations and examples in English Proto comments following AIP-192. Keep routes, types, and field behavior in declarations and annotations. `docs/api/` contains only generated English Markdown, an explicit bilingual exception; put handwritten setup instructions in `docs/development/`. Do not add manual reference pages or website deployment to this pipeline.

After editing a contract, run `mise run generate:api` and inspect the derived OpenAPI, HTTP client, and Markdown reference. Run `mise run check:api`, `mise run check:markdown`, and relevant code checks; `mise run demo:api` verifies the sample's local and HTTP paths. Checks must not rewrite source; resolve drift through Proto or generator configuration. Generation removes stale reference pages and the check rejects unexpected files in `docs/api/`.

Google API Linter covers only part of the design requirements. Assess business semantics and relevant execution behavior separately, particularly retries, duplicate side effects, authorization, cancellation, and long-running operation recovery. A greeting example does not establish those guarantees for future commands.
