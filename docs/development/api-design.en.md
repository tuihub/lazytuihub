# API development guide

English | [中文](api-design.zh.md)

## Basis and reference access

This guide describes API development and condenses the Google AIP requirements adopted by this project. Consult the official text at the adopted revision for complete context, conditions, exceptions, and related requirements. An omission from the summary is not an exemption. Record the location and reason for any deliberate project-specific difference.

[mise.toml](../../mise.toml) is the single configuration source for the official repository URL and adopted commit. During initialization, run `mise run setup:aip` to clone the originals into the Git-ignored `.agents/references/google-aip/` directory. General documents are under `aip/general/NNNN.md`; AIP-155 is `aip/general/0155.md`. `mise doctor project` checks the checkout's origin, commit, and worktree state without changing them.

Read this standard first, then the complete approved AIPs and necessary cross-references relevant to the task. Google's internal governance, product-specific Cloud or Workspace requirements, and unrelated SDK publishing context do not automatically become project constraints. To update the baseline, explicitly change the mise commit, prepare the checkout, review affected documents, and update this standard accordingly. Ordinary builds and checks do not update the adopted guidance.

## Contracts and business boundaries

Maintain Proto as the single transport-contract source. Generate C# messages, OpenAPI, and the HTTP client without independently editing those outputs. A transport-independent C# application interface connects local and remote adapters; it describes the business-call boundary without duplicating HTTP routes or JSON schemas.

Application services own business rules, authorization, transactions, and workflows. In-process calls enter those services directly; remote entry points adapt transport. Separate domain objects where actual needs justify it rather than copying every DTO.

In the current implementation, [IGreetingApi](../../src/LazyTuiHub.Api/IGreetingApi.cs) is the shared calling boundary. [GreetingApplication](../../src/LazyTuiHub.CorePlugin/GreetingApplication.cs) implements it directly, and the [transport service](../../src/LazyTuiHub.CorePlugin/GreetingService.cs) forwards transcoded requests and cancellation to it. The generated HTTP client's [handwritten partial](../../src/LazyTuiHub.Client/GreetingHttpClient.cs) implements the same interface and configures JSON field naming and relative paths. The `./` path prefix keeps custom-method colons from being parsed as a URI scheme.

Subscriptions use [IRssApi](../../src/LazyTuiHub.Api/IRssApi.cs). Articles from all sources use [IArticleApi](../../src/LazyTuiHub.Api/IArticleApi.cs). Its application service invokes the shared pipelines, while the [client partial](../../src/LazyTuiHub.Client/RssHttpClient.cs) adapts resource names and query parameters to generated methods. Proto messages use Protobuf JSON serialization to preserve timestamps and repeated fields. Generation emits one client per configured service. The [MVP guide](streaming-mvp.en.md) describes composition and the explicit prototype exception for native ASP.NET Core HTTP error representation; this exception does not change other AIP requirements.

## Design requirements

| Topic | Requirement and applicability | Original |
| - | - | - |
| Resources | Model identifiable business objects and relationships, not database tables. Follow consistent resource names and references. | [121](https://google.aip.dev/121), [122](https://google.aip.dev/122), [123](https://google.aip.dev/123) |
| Methods | Use standard methods where their semantics fit. Represent complex business intent through appropriate custom actions, with corresponding request and response messages. | [130](https://google.aip.dev/130), [136](https://google.aip.dev/136) |
| HTTP | Declare methods, paths, and request bindings in google.api.http. Choose HTTP verbs according to the guidance for reads, mutations, and stateless computation. Follow Protobuf JSON field names and type representations instead of mixing ordinary DTO serialization conventions. | [127](https://google.aip.dev/127), [136](https://google.aip.dev/136) |
| Fields | Use consistent names and accurate comments, specifying behaviors such as REQUIRED, OPTIONAL, and OUTPUT_ONLY. Select appropriate presence semantics where omission must differ from an explicit value. | [140](https://google.aip.dev/140), [149](https://google.aip.dev/149), [203](https://google.aip.dev/203) |
| Documentation | Describe services, methods, messages, fields, enums, and values in English Proto comments. Explain defaults, side effects, idempotency, errors, and examples where applicable. Use supported CommonMark without headings, tables, or raw HTML in comments. | [192](https://google.aip.dev/192) |
| Idempotency and retries | Design request_id for commands needing deduplication and reuse it on retries. Define requiredness and scope per operation. Automatically retry only semantically safe requests and retryable errors, honoring cancellation and deadlines. | [155](https://google.aip.dev/155), [194](https://google.aip.dev/194) |
| Concurrency | Apply ETag preconditions where changes depend on existing resource state. Design concurrency control separately from request deduplication. | [154](https://google.aip.dev/154) |
| Long-running work | Use google.longrunning.Operation and corresponding queries for operations that continue executing. A C# Task alone does not establish a long-running business operation. State and results must remain queryable within the agreed recovery scope. | [151](https://google.aip.dev/151) |
| Errors | Follow google.rpc.Status, canonical codes, and ErrorInfo, checking their HTTP representation. Clients depend on stable machine-readable information rather than parsing messages. | [193](https://google.aip.dev/193) |
| Collection queries | Apply pagination or filtering where collections actually need them. Returning a list alone does not justify adding business limits. | [158](https://google.aip.dev/158), [160](https://google.aip.dev/160) |
| Naming and evolution | Follow Proto file, package, message, and method conventions. Preserve field-number semantics and review changes against promised compatibility. Implementation or wording changes do not alone justify another API version. | [180](https://google.aip.dev/180), [190](https://google.aip.dev/190), [191](https://google.aip.dev/191) |

Preserve the original requirement levels, conditions, and exceptions; do not turn SHOULD or MAY into an unconditional MUST. This navigation table does not replace complete requirements in standard-method or other related documents.

## API reference documentation

Proto comments are the source of interface explanations and examples. Structural facts such as routes, types, requiredness, and output-only fields come from declarations and annotations. Keep these consistent with the implementation; do not maintain a second interface description in Markdown or edit derived OpenAPI.

`mise run generate:api` runs Proto → OpenAPI → Scalar Markdown alongside client generation. The generated [reference index](../api/index.md) links operation and message pages. Put request and response examples and HTTP invocation examples in method comments using labeled fenced code blocks; describe relevant defaults and behavior on fields. Comments follow AIP-192; complete requirements and exceptions remain in the adopted original.

`docs/api/` is entirely generated English Markdown, explicitly exempt from bilingual naming and pairing. Other Markdown rules and the 1 MiB file limit still apply. Generation removes obsolete pages; the drift check rejects edited, missing, extra, or stale files without rewriting them. Linked model pages avoid recursive schema expansion; detected upstream truncation fails generation instead of publishing incomplete field documentation. Put handwritten development instructions in `docs/development/`. Website rendering and deployment are outside this API workflow.

Scalar's synthesized date examples use fixed placeholder dates so repeated generation is deterministic. This affects only the renderer's input copy; supplied examples, defaults, and the OpenAPI contract remain unchanged.

## Initialization and generation

Prepare tools and dependencies using the [repository setup commands](../../AGENTS.md#development-commands). After changing Proto, generate and check the derived artifacts:

```sh
mise run generate:api
mise run check:api
```

Grpc.Tools generates C# messages and service bases during the .NET build. The explicit generation task uses protoc-gen-openapi for OpenAPI, NSwag for the HTTP client, and Scalar for Markdown. Inspect their generated differences after each contract change.

`mise run check:proto` uses Google API Linter's default enabled rules and fails through its exit status when findings occur. No rule groups or Hello World exemptions are currently disabled. Future exceptions must identify their specific rule, API, and justification.

`mise run check:generated` regenerates into a temporary directory and compares outputs without rewriting checked files. `mise run check:api` combines both checks and is included in `mise run check`.

Automated checks cannot establish correct resource modeling, authorization, deduplication commits, or recovery. Validate actual behavior where changes involve those capabilities. Tracked generated files remain subject to the repository's 1 MiB limit. The external source checkout is an ignored development reference rather than project-maintained Markdown or bilingual documentation.

## Running local and HTTP calls

Run the integrated example to compare direct application calls with generated-client HTTP calls:

```sh
mise run demo:api
```

The demo starts the service on a temporary loopback port, checks matching responses from both paths, and stops. To run the HTTP service for manual calls:

```sh
mise run run -- --urls http://127.0.0.1:5080
```

Use the generated [operation reference](../api/operations/GreetingService_GenerateGreeting.md) for interface behavior and request examples. For its HTTP invocation example, set `BASE_URL` to the running server's address.

## Integration boundaries

The current integration verifies string messages. When introducing field presence, enums, 64-bit integers, timestamps, Any, or other Protobuf types, verify agreement between derived OpenAPI and client/server wire encodings.

When introducing business errors, verify and adapt agreement between framework errors and AIP-193's HTTP representation. Public-network authentication, HTTPS deployment, mutating commands, persistent operations, and WebSocket integration follow the [protocol Note](../../.agents/notes/2026-11-15-protobuf-api-contract.en.md); the sample does not establish those capabilities.
