---
status: implemented
category: architecture
---
# Proto contracts and HTTP communication

English | [中文](2026-11-15-protobuf-api-contract.zh.md)

## Status and scope

This Note records the implemented protocol and generation baseline and its first Hello World example: declare APIs in Proto, check their contracts directly with Google API Linter, expose HTTP/JSON APIs through ASP.NET Core JSON transcoding, and route in-process calls to the same C# application services. The implemented status applies to this baseline. Business reliability requirements below apply when their scenarios arise, and future WebSocket communication retains a separate adaptation boundary.

The product is a monolithic application containing both client and server. Remote calls cross the public internet and must handle network failures as a conventional client/server application does. Server behavior may involve complex business logic with relatively little plain CRUD. Generation covers contracts and communication boilerplate; C# implements business rules and workflows.

The current [product entry point](../../src/LazyTuiHub/Program.cs) hosts the Hello World HTTP service and local demo, and the [project](../../src/LazyTuiHub/LazyTuiHub.csproj) targets net11.0. Proto, the official linter, OpenAPI, HTTP client and Markdown generation, the concise standard, and official reference access are connected. Reliability for mutating operations, authentication, long-running work, and special-type encoding require implementation and validation when introduced. They are not prerequisites for completing the current stateless baseline or recorded as implemented capabilities. Product and deployment targets continue to follow the [repository initialization Note](2026-11-15-repository-initialization.en.md).

## Contracts and design agreements

Proto is the single handwritten source for transport contracts, declaring requests, responses, service methods, and google.api.http mappings. C# message types and OpenAPI derive from it rather than becoming separately maintained protocol definitions. Generators must not overwrite handwritten business code, and derived outputs are not edited manually.

Adopt the Google API Design Guide and applicable AIP requirements. AIPs center on Protobuf contracts and also explicitly define HTTP/JSON mappings; this contract choice does not require public clients to use gRPC. Follow [AIP-127](https://google.aip.dev/127) for HTTP bindings, [AIP-136](https://google.aip.dev/136) for custom business actions, and the guidance on request idempotency, long-running operations, concurrency, and structured errors.

Organize service methods around business intent. Use standard resource methods where their semantics fit, and appropriate custom methods for complex actions such as publishing and synchronization. Do not split workflows into database CRUD calls that clients must assemble, or generate business and persistence implementations from data models.

Use [Google API Linter](https://github.com/googleapis/api-linter) directly on Proto and its HTTP annotations without translating Google's rules into OpenAPI rules. The current setup uses default enabled rules and a failing exit status, with no disabled rule groups or example exemptions. Java package metadata follows AIP-191, and go_package supports the OpenAPI generator; this work generates no Java or Go product code. Future exceptions must follow actual API semantics and explain their reasons. The linter covers only statically checkable AIP provisions; passing it establishes neither complete AIP compliance nor runtime reliability.

## Concise standard and official originals

The [project API standard](../../docs/development/api-design.en.md) maintains adopted requirements and project choices; the [API design skill](../skills/api-design/SKILL.md) guides task-specific reading. The standard removes unrelated product context and internal process while preserving requirement levels, conditions, exceptions, and AIP numbers. Explicit project differences are recorded separately. Without such a difference, use the adopted original to correct summary errors rather than treating omissions as exemptions.

The aip_repository and aip_revision variables in [mise.toml](../../mise.toml) are the single configuration source for the official repository and adopted commit. `mise run setup:aip` clones it into `.agents/references/google-aip/` and checks out that commit. An existing checkout changes revisions only with the expected origin and a clean worktree; initialization preserves local edits. Repeated runs reuse the checkout instead of following upstream's latest commit automatically.

Keep the original directory structure and content for the skill to read relevant `aip/general/NNNN.md` files and necessary references, without loading the whole corpus for each task. Git and remark ignore the checkout separately, excluding it from project bilingual and Note checks. `mise doctor project` checks origin, commit, and worktree without mutation. Updates explicitly change the adopted commit, prepare the checkout, and review the concise standard, without another excerpt mirror, index, or synchronization service.

## Generation pipeline and responsibilities

| Stage | Approach and boundary |
| - | - |
| Contract checks | Google API Linter reads Proto; C# interfaces and OpenAPI do not replace this input. |
| C# messages and service bases | protoc and Grpc.Tools generate message types and service bases, with handwritten thin service implementations receiving calls. |
| HTTP server entry points | Microsoft.AspNetCore.Grpc.JsonTranscoding establishes routes, parameter binding, and JSON encoding from annotations. |
| OpenAPI | Use google/gnostic's protoc-gen-openapi to derive it from the same Proto without making its output a new handwritten source. |
| HTTP clients | Generate them from derived OpenAPI with NSwag. The current example reuses Proto messages and implements the shared application interface through a partial class. |
| API reference | Render English Markdown operation and model pages from derived OpenAPI with Scalar, preserving explanations and examples authored in Proto comments. |
| Application and domain logic | Handwrite business rules, authorization, transactions, and workflows instead of generating a complete business layer from entities. |

[Official JSON transcoding](https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding?view=aspnetcore-10.0) supports conventional HTTP/1.1, HTTP/2, and JSON requests and responses. It parses requests and invokes the service implementation directly inside one ASP.NET Core application, without a separate transcoding gateway or network hop between services. This automation generates service bases and establishes HTTP endpoints at runtime; it does not require generated MVC Controller source files.

The default client generated by [Grpc.Tools](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/grpc/basics.md) uses gRPC and is not a conventional HTTP/JSON client. This example connects a separate pipeline using [protoc-gen-openapi](https://github.com/google/gnostic/tree/main/cmd/protoc-gen-openapi) and NSwag. The [generation helper](../scripts/api.mjs) uses NuGet-restored common Proto definitions and mise compiler tools. NSwag runtime selection derives from global.json without another SDK version declaration.

OpenAPI, the HTTP client, and the Markdown reference are tracked derived files. `mise run generate:api` explicitly writes them; `mise run check:generated` regenerates into a temporary directory and compares without changing checked files. `mise run check:proto` runs official rules, and check:api adds both to check. C# messages and service bases remain build artifacts in obj rather than tracked files.

This repository uses .NET 11. Microsoft.AspNetCore.Grpc.Swagger, the experimental package used in older tutorials, has been deprecated with no direct replacement according to the official documentation. JSON transcoding itself has not been deprecated. Evaluate OpenAPI derivation separately rather than depending on that older Swagger integration. See the [official deprecation notice](https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding-openapi?view=aspnetcore-10.0).

## API reference generation

The earlier [tuihub/protos generation configuration](https://github.com/tuihub/protos/blob/master/buf.gen.yaml) produced a single Proto reference through [a custom protoc-gen-doc template](https://github.com/tuihub/protos/blob/master/docs/markdown.tmpl) and produced OpenAPI separately. This repository renders its existing HTTP contract through [Scalar's OpenAPI-to-Markdown library](https://github.com/scalar/scalar/tree/main/packages/openapi-to-markdown), using the dependency version and lockfile in the Node toolchain. No custom full schema renderer or documentation website is introduced.

API explanations and examples live in English Proto comments under [AIP-192](https://google.aip.dev/192); routes, data types, requiredness, and output-only behavior derive from declarations and annotations. The Hello World method documents defaults, stateless repeated calls, and actual request and response examples. Changes to those comments flow through OpenAPI to the generated reference without another handwritten interface description.

The entire `docs/api/` directory is generated English Markdown, an explicit exception to bilingual pairing. A generated [index](../../docs/api/index.md) links operation and model pages. Models use links for nested references; detected renderer truncation fails generation rather than publishing incomplete fields. Other Markdown checks and the repository's 1 MiB file limit remain active. Handwritten generation, running, and component instructions belong in the bilingual [API development guide](../../docs/development/api-design.en.md). Interface behavior and HTTP invocation examples live in Proto comments and the generated reference. Website rendering and deployment are outside the API work scope.

The generation task owns the complete reference directory and removes obsolete pages. The generated-artifact check compares both content and inventory, rejecting edited, missing, extra, and stale files without rewriting them. Tooling tests cover recursive schema links, preservation of authored examples, truncation detection, directory drift, and the narrow language exception. These checks establish synchronization, not the factual correctness of comments or API behavior.

## Calls and business boundaries

```text
In-process client -> Local adapter ------------------> C# application service
Public HTTP -> Automatic HTTP endpoint -> Thin service -> C# application service
Future WebSocket -> Separate adapter ----------------> C# application service
```

In-process calls enter application services directly, without loopback HTTP, serialization, or simulated gRPC service contexts. Remote entry points adapt the transport and call the same application services. Complex business logic does not belong in transcoding components.

The application interface used by callers remains independent of transport. Grpc.Tools service bases and clients include gRPC call types; they do not automatically provide one pure C# interface suitable for both in-process calls and HTTP clients. This example handwrites [IGreetingApi](../../src/LazyTuiHub.Api/IGreetingApi.cs), implemented by the application service and generated client's partial class. Evaluate custom interface generation only when actual duplication warrants it, without first building a general RPC framework.

The [client partial](../../src/LazyTuiHub.Client/GreetingHttpClient.cs) configures camelCase JSON names and prefixes NSwag's relative path with `./` so .NET does not interpret v1:generateGreeting as a URI scheme. NSwag declares the instance partial callback's signature, so that method alone suppresses CA1822 with a reason; generated code and product-wide rules remain unchanged. This establishes reuse for the example's string messages, not direct support for every Protobuf special type under the same serialization configuration.

Application services may reuse generated message types. Model domain objects independently where business needs justify them rather than copying every DTO for formal layering. Business validation, authorization semantics, and idempotent execution belong on the shared path; local calls must not depend on HTTP model binding to satisfy business constraints. Establishing and passing caller identity remains part of concrete integration.

A unified API means shared business operations, data, and business results, without promising to erase network differences. Remote adapters must distinguish business rejection, communication failure, and an unknown outcome for writes. Callers cannot interpret a timeout or disconnect as proof that the server did not execute the operation.

## Public-network reliability

These are adopted design constraints to implement as the corresponding business or public-network scenarios arise. Implement idempotent execution when introducing writes that need deduplication; configure safe operations, error classification, and call budgets when adding automatic retries; implement operation recovery and concurrency control when introducing long-running work and concurrent resource changes respectively. Establish caller identity and permission enforcement before public-network access. The stateless sample does not pre-build deduplication storage, a task system, or an account system; later implementations remain subject to these requirements.

- Request identification: following [AIP-155](https://google.aip.dev/155), provide request_id for commands requiring deduplication. Retries of one business intent reuse its identifier; a new intent gets a new identifier. The identifier belongs to the request rather than the resource and maps to the HTTP body or query parameters according to the contract. Decide whether it is required per operation rather than rejecting every request that lacks one.
- Idempotent execution: deduplicate within an explicit caller or tenant, operation, and request-identifier scope, checking business parameters to distinguish different requests. Successful duplicates return the previous successful result or a contractually permitted equivalent. Retrying a long-running command should identify the same operation object. Define scope, retention, and responses for requests still in progress without choosing a duration in this Note.
- Commit consistency: atomically commit deduplication records and business writes when they share a database. For files, external services, or other effects outside that transaction, design business-specific idempotency and recovery. A UUID or an in-memory response cache alone does not establish these guarantees or promise global exactly-once execution.
- Retries and cancellation: follow [AIP-194](https://google.aip.dev/194) in selecting retries by operation semantics and error category, with backoff, jitter, and an overall call budget. Do not retry every failure; honor user cancellation and exhausted deadlines. Propagate cancellation to the server without promising that canceling a wait rolls back committed work.
- Long-running operations: use google.longrunning.Operation and its corresponding operation-query service as required by [AIP-151](https://google.aip.dev/151). Return an operation object before retrieving progress, results, or errors. Completion must not require one uninterrupted connection. Operation state and execution lifetime must support the agreed disconnect-recovery scope. Select persistence and restart recovery during implementation; an in-memory Task alone cannot establish that requirement.
- Concurrency and errors: apply [AIP-154](https://google.aip.dev/154) ETag preconditions where business behavior requires them and follow [AIP-193](https://google.aip.dev/193) error models, HTTP representation, and stable machine-readable information. Idempotency handles repeated business intent, while concurrency control handles changes based on stale state. Design each explicitly.

Google API Linter checks some contract identifiers and annotations, not correct commits, deduplication, or recovery. For example, the [existing AIP-155 format rule](https://linter.aip.dev/155/request-id-format) checks request_id's UUID4 annotation rather than server execution. Establish these guarantees through relevant business implementations and focused behavioral verification.

## WebSocket boundaries

| Capability | Official JSON transcoding support |
| - | - |
| Ordinary requests and responses | Supported. |
| Server-streaming responses | Supported through line-delimited JSON in an HTTP response stream. |
| Client-streaming requests | Not supported. |
| Bidirectional streaming calls | Not supported. |
| Automatic WebSocket endpoint and client generation | Not supported. |

Proto's stream declaration describes a message stream without choosing WebSocket transport. [AIP-127](https://google.aip.dev/127) also excludes bidirectional streaming from ordinary HTTP mapping requirements. A line-delimited JSON response stream does not establish an implemented WebSocket protocol.

Future work can declare event messages in Proto and generate their types while implementing a separate WebSocket adapter. Reconnection, subscriptions, acknowledgments, replay, and ordering require agreements based on actual needs; Google API Linter and this transcoding pipeline do not cover them. If WebSocket carries operation-progress notifications, operation queries still restore state after reconnecting. Introduce event history or cursors only where recovery requirements warrant them.

This proposal selects no WebSocket framework and does not promise automatic generation of a complete HTTP and WebSocket bidirectional calling system from one Proto contract. Future adapters or generators must continue to reuse application services rather than creating another business implementation.

## Example validation and remaining work

The first [Hello World API](../../docs/api/operations/GreetingService_GenerateGreeting.md) uses the stateless GenerateGreeting custom method at POST /v1:generateGreeting. `mise run demo:api` starts a loopback service on a temporary port and compares in-process and real HTTP paths through matching call signatures. Default, Ada, and Unicode recipients return matching results. This example needs neither a request-deduplication store nor a long-running operation object.

1. The same Proto now passes default Google rules, generates and builds C# messages and service bases, and produces OpenAPI and an ordinary HTTP client. The service implementation adapts transport while local calls enter the same application service directly.
2. When introducing business errors, verify structured failures and agreement between default framework errors and AIP-193 HTTP representation, adapting transport where necessary. Also verify cancellation propagation and relevant business constraints. Proto alone does not establish compatibility.
3. When introducing field presence, enums, 64-bit integers, timestamps, Any, or other types, verify agreement between derived OpenAPI, shared messages, and client/server wire encodings. Successful string messages do not establish compatibility for every type.
4. Mutating commands still require verification of retries after lost responses, concurrent duplicates, reuse of identifiers with different parameters, operation queries, and promised restart recovery. Hello World's successful responses do not replace those reliability checks.

The full local check passed with zero build warnings and errors. The documented run command and Ada and empty-object requests were exercised. Temporary negative cases confirmed that an invalid custom-method HTTP verb fails AIP checks and derived-file drift fails without rewriting files; those changes were restored afterward. Official reference preparation was repeatable, all ten Doctor checks passed, and the skill passed format validation.

This work ran the stateless example without integrating a new product test framework. Identity propagation, operation storage, and deduplication retention still need decisions with actual business operations. Validate WebSocket when introduced. The example ran on macOS, not Windows, Linux, or a public-network deployment.

## Implementation and repository agreements

mise manages tools and tasks. Go, protoc, and the Go protocol tools are pinned in mise.toml; NSwag uses the existing dotnet-tools.json manifest, and NuGet project references declare runtime and compiler dependencies. The [API development guide](../../docs/development/api-design.en.md) provides adopted standards, generation and run commands, and access to repository initialization instructions. Contract and generation checks read real declarations, and changes to contracts or generation settings resolve derived-file differences.

Generated code follows the [existing lint boundaries](2026-11-15-csharp-lint.en.md): do not manually clean up generator style, but compilation must pass. Build artifacts follow existing Git ignore rules. Tracked or non-ignored generated files remain subject to the 1 MiB file limit; do not widen checks or silently skip files to accommodate a generator.

The implemented scope of this Note covers protocol declarations, adopted standards, generation and checks, and the Hello World local and HTTP calling baseline. Later business changes implement and validate reliability, identity, long-running work, special types, and WebSocket capabilities under the conditions above. Adopting standards and passing static checks do not replace runtime guarantees.
