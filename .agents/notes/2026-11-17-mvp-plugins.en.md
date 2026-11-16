---
status: proposed
category: architecture
---
# MVP trusted plugins and lossless enablement

English | [中文](2026-11-17-mvp-plugins.zh.md)

## Scope and status

This Note records business-plugin boundaries and conclusions from official documentation/source research on 2026-11-17. It guides a pluggable prototype without defining a complete plugin system. Follow the [framework Note](2026-11-17-mvp-framework.en.md) and [node execution Note](2026-11-17-mvp-node-execution.en.md): business services support direct calls, and nodes adapt them to optional workflows.

The [storage and DAO Note](2026-11-17-mvp-storage-dao.en.md) records subsequent discussions of three DAO access modes, namespaces, managed templates, and retention, distinguishing established direction from open semantics.

A runnable cold-start prototype now implements the boundaries described below. Under the [MVP maintenance agreement](../../AGENTS.md), this Note remains proposed and freely editable until merge into main. Implementation commits update it alongside code, distinguishing implemented behavior, simple provisional choices, and open boundaries without adding compatibility layers for exploratory interfaces, graphs, or schemas.

## Fixed boundaries and responsibilities

| Boundary | Agreement |
| - | - |
| Execution | Host and plugins share one process and cooperate through direct C# calls. |
| Loading | Dynamically load .NET managed DLLs; one logical plugin may include an entry assembly, dependencies, and resources. |
| Lifecycle | Load only at cold startup. Enablement changes and assembly replacements take effect on the next startup, without runtime load/unload guarantees. |
| Trust | Plugins are fully trusted and share process permissions and resources; this iteration establishes no sandbox or permission-isolation system. |
| Core plugins | The distribution manifest marks core plugins as required. Currently core supplies greetings and built-in timer/HTTP nodes and cannot be manually disabled. |
| Minimal framework | Loading, framework metadata storage, and necessary diagnostics remain independent of plugin implementations and can start when plugins are missing. |

The host owns startup configuration, discovery/composition, the shared service container, and application lifecycle. Business plugins own business services, storage/initialization, node adapters, HTTP/CLI entry points, and background tasks. Using DLLs does not make every framework library or shared contract an optional plugin. Existing Proto/HTTP agreements remain effective; this iteration defines no separate plugin-management API.

Treat required service dependencies separately from workflow data connections. A graph connecting RSS to another business does not make that plugin an intrinsic dependency of RSS. Missing node implementations explain affected workflows; missing required services explain why a plugin or capability cannot activate, while independent capabilities remain available.

## Framework metadata ownership

The framework exclusively owns writes and lifecycle for its metadata. Plugins contribute capability declarations, business results, and necessary relationship intent for the framework to register at registration or business-submission boundaries. Plugins must not directly modify core metadata tables or receive unrestricted metadata CRUD interfaces. This boundary supports both direct business calls and node execution without requiring every write to traverse a workflow.

Persist information needed to identify unavailable capabilities: plugin identity/provenance, node/data-type ownership, necessary port/type declarations, original workflow parameters/connections, and existing data identities, relationships, task definitions, and configuration. The prototype stores these in SQLite alongside business tables. BusinessStorage owns transaction commit: the plugin writes its business tables and returns BusinessChange with data identities and relationship intent; the framework applies metadata before committing the same transaction. Failure rolls back both. DataCatalog exposes relationship reads, with mutation methods private to the framework. Shared database access remains a trusted ownership contract; plugins must only write their own business tables.

Descriptions must remain readable without plugin code, object construction, or plugin-specific deserialization. Current strict runtime type matching applies when implementations are available; missing implementations cannot prevent reading descriptions. Cross-plugin calls still require shared assembly type identity rather than matching type names alone.

The declaration catalog is independent of deployment directories. DLL scanning finds present implementations and cannot replace retained declarations. Imported workflows should carry necessary provenance; with neither prior records nor source declarations, report an unknown node type rather than guessing ownership from name prefixes. Preserve missing-node parameters/connections without automatically removing nodes or repairing graphs.

Under full trust in one process, prohibiting plugin writes to framework metadata is a code contract and ownership boundary expressed through interfaces and storage layering, not an in-process security boundary.

## Enablement intent, availability, and current activation

Record saved user enablement, current file/dependency availability, and this startup's binding/activation results separately. Management may present a common unavailable outcome while retaining its cause. If RSS is enabled but its DLL is missing, do not rewrite the setting as disabled; restoration binds according to the original intent.

Disabled behavior includes both explicit disablement and failure to find a plugin at startup. Missing, disabled, and unloadable implementations retain declarations and diagnostics while the base framework starts. Related business entry points are hidden or unavailable, and related tasks do not start or receive further scheduling; management/diagnostics still identify ownership. Core-plugin enablement is a normal configuration constraint, not an exception to missing-plugin behavior: dependent capabilities remain unavailable while the minimal framework starts.

Separate startup responsibilities: read the catalog and intent; discover/load available implementations; collect service/capability contributions; build shared services and node registration; then bind implementations, plan required work, and start tasks. Registration order must not accidentally determine cross-plugin workflow feasibility. The framework updates declaration metadata instead of letting plugins arbitrarily mutate global registration state.

The provisional IApplicationPlugin entry collects services, nodes, data types, default workflows, commands, routes, tasks, and optional hooks through PluginRegistration. PluginRuntime stages service contributions before host construction, then stages node/endpoint declarations before activation. Required plugin IDs form a simple startup dependency graph; missing or failed providers block dependent activation. Workflow connections do not create these service dependencies. All node registration finishes before default workflows are saved and task feasibility is checked. Plugin side effects and later activation failures have no global rollback guarantee.

The prototype uses the default AssemblyLoadContext with one shared dependency baseline, preserving exact shared-contract type identity. AssemblyDependencyResolver locates additional managed dependencies; separate versions and arbitrary native dependency deployment are not implemented. The [official .NET plugin tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) informs DLL/dependency handling rather than requiring private loading contexts. There is no runtime unload, private-version solver, marketplace, or hot reload.

## Lossless enablement and cleanup hooks

The framework must introduce no data loss through repeated enable/disable cycles. Without dedicated plugin cleanup behavior, changes are limited to unavailable/hidden functionality and stopped tasks. Disablement or absence must not delete, reset, or overwrite business data, framework metadata, configuration, workflows, task definitions, or relationships. Re-enablement must not duplicate default tasks or overwrite user changes with defaults.

Preservation must work with the plugin entirely absent and cannot depend on its shutdown hook. The framework owns metadata/relationships, retains references to temporarily unresolvable objects, and does not inspect or interpret plugin business data to clean it. Business cleanup belongs to the plugin; absence, disablement, or unresolved references trigger no substitute cleanup.

Explicit managed retention contracts authorize normal expiry, with potentially distinct data owners and cleanup providers. Such authorization is independent of enablement and explicit maintenance hooks; data without declared policies remains preserved under the boundary above. The record tool hides expired rows and clears expiry through Keep. Persisted target delegations continue cleanup while the owner is disabled or missing if the provider remains active; missing providers preserve data and policy with unavailable diagnostics. Pauses survive recomposition. RSS observations default to permanent retention; configured expiry does not apply to articles or execution inputs. The storage Note records these implemented semantics and remaining durable-input/recovery decisions.

| Operation | Behavioral boundary |
| - | - |
| Disablement / missing at startup | Stop related capabilities/tasks by availability while retaining data and definitions; invoke no data-cleanup or uninstall hook. |
| Re-enablement / restored implementation | Rebind using existing identities, configuration, and data, restoring capabilities and scheduling eligibility. |
| Explicit data cleanup | The framework may supply a common entry point invoking an optional plugin hook; the plugin defines scope and business behavior. |
| Explicit uninstall | The framework may supply a common flow and optional hook, separate from disablement/data cleanup; code removal remains to be designed. |

Hooks rely on contracts only. The framework handles invocation, progress, returned outcomes, errors, and cancellation without scanning databases/files to prove complete cleanup or verifying conformance to the hook's business definition. Missing implementations make their hooks unavailable; do not report fictitious success or perform compensation. Hooks do not acquire ownership of framework metadata.

Distinguish runtime resource release from persistent-data cleanup. Task cancellation and connection disposal may follow Host/DI mechanisms without equating stopping with cleaning data or checking whether plugin disposal faithfully fulfills its contract.

Do not silently narrow losslessness to saving graph definitions. Active stream queues, incomplete synchronized inputs, and node-local state are currently in memory; preservation of unfinished data and execution progress across shutdown still needs decisions. Existing cancellation is not lossless suspension, and rerunning does not prove absence of loss. Recovery, missed timer firings, and side-effect semantics remain open rather than assuming catch-up or instruction-level continuation.

## Research conclusions and adoption

The following observations come from official sources at research time. Adoption is a project design choice, not dependency selection or proof that a reference implementation provides our lossless contract.

| Project | Observed mechanism | Project adoption |
| - | - | - |
| ComfyUI | Its [loader](https://github.com/Comfy-Org/ComfyUI/blob/master/nodes.py#L2255) records import failures and continues; the [frontend](https://github.com/Comfy-Org/ComfyUI_frontend/blob/main/src/scripts/app.ts#L1328) collects missing types, instances, and available provenance. | Separate description loading from executability and retain missing nodes with ownership diagnostics. |
| Factorio | Its [lifecycle](https://lua-api.factorio.com/latest/auxiliary/data-lifecycle.html) separates declarations, initialization, load, and configuration changes; runtime cannot alter prototypes. | Separate startup binding from business-data initialization and give lifecycle phases distinct responsibilities. |
| DeepSeek Harness / Cordis | [Service dependencies and reversible registrations](https://github.com/deepseek-ai/deepseek-harness/blob/master/docs/cordis-primer.md) compose plugins; [management](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/boot/plugin-manager/README.md) distinguishes selection, availability, and runtime results while retaining missing-entry descriptions. | Connect providers/consumers through shared contracts, preserve enablement intent, and assign ownership to runtime registrations/resources. |
| Jellyfin | Its [.NET manager](https://github.com/jellyfin/jellyfin/blob/master/Emby.Server.Implementations/Plugins/PluginManager.cs) loads assemblies, registers DI services, and records failures; [documentation](https://jellyfin.org/docs/general/server/plugins/) separates retained configuration from plugin-file reinstallation. | Use existing .NET composition/lifecycle mechanisms and manage code, declarations, and business data separately. |

Factorio explicitly documents that [removing prototypes invalidates stored references](https://lua-api.factorio.com/latest/auxiliary/migrations.html); do not copy that behavior for lossless toggling. Its declaration stage also permits mods to alter other prototypes, unlike our exclusive framework-metadata ownership. DeepSeek Harness has broader replaceability, configuration layering, and hot reload; adopt composition/state separation without reproducing the complete system.

Retain readable declarations despite missing implementations, preserve user intent after load failures, rebind without resetting durable state, and separate runtime teardown from explicit business cleanup. Non-disableable core plugins, exclusive framework metadata writes, and contractual-only hooks remain explicit project choices.

## Current prototype and validation

The host builds core, RSS, and storage.sqlite as distribution artifacts without runtime references to their implementation assemblies. It loads the explicit plugins/plugins.json manifest from the application output, or --plugin-directory. The manifest supplies identity, entry DLL/type, required membership, and hard service dependencies. plugin_catalog retains manifest observations, enabled intent, opaque JSON settings, and plain declaration snapshots independently of the deployment directory. Missing manifests/files preserve prior catalog records. Presence is checked before loading even when an assembly identity is already cached.

LazyTuiHub.CorePlugin, LazyTuiHub.Rss, and LazyTuiHub.Storage.Sqlite implement the same entry contract. Public DAO contracts live in LazyTuiHub.Dao. Plugins declare access requirements independently of concrete table targets; providers publish capabilities, and consumers activate after matching mode/contract/instance/backend. Activation does not depend on manifest order. Declaration snapshots retain requirements/offers even when a consumer cannot activate at first startup. The storage plugin supplies records/TTL, FTS5, native connections, and delegated cleanup; minimal framework metadata remains usable without it. RSS owns its API implementation, transport adapter, CLI command, initialization, and refresh task. The minimal host retains plugin management, DAO diagnostics/retention controls, and workflow inspection/editing/execution. WorkflowRuntime owns graph storage, registration and planning; business services receive IWorkflowExecutor. Existing RSS business operations still traverse workflows, so making workflows optional for these services remains later work.

--command plugins manages list/inspect, enable/disable, opaque configuration, and explicit cleanup/uninstall hook invocation. Saved changes require restart and do not mutate current activation. Disabled maintenance targets may load at cold startup only to supply services/hooks, without nodes, routes, commands, or task activation. Hooks currently return Task: the command reports completion, errors, and cancellation without semantic verification or a dedicated progress protocol. RSS provides neither hook. Uninstall invokes an optional hook; it does not delete code or catalog records.

--command workflow remains usable with missing plugins. Saving preserves unavailable nodes, parameters, connections, and pluginId provenance while returning current feasibility diagnostics; execution requires a valid plan. Unknown imported types without provenance remain unknown. Default workflows use insert-if-absent, preserving user changes. Persisted task declarations retain ownership and required workflow names; runtime task state is transient. Normal server startup runs one instance of each available plugin task only when its required workflows can plan. Host shutdown cancels tasks cooperatively; business tasks own retry policy.

Product tests use separate CLI processes and one workspace to verify enabled/disabled/absent/restored RSS, retained articles/read state/relationships/settings/opaque graph parameters, missing core and manifest diagnostics, unknown imported types, and unavailable tasks/commands. Lifecycle tests verify cold maintenance, explicit-only hooks without a semantic cleanup verifier, task cancellation, and shared business/metadata transaction rollback. Storage tests additionally verify missing/restored provider DLLs, unavailable requirements on first startup, all three access modes, delegated-only cleanup, permanent records, pause persistence, and FTS/business/catalog rollback. Existing local/generated HTTP scenarios continue through dynamically loaded plugins.

Initialization failure cleanup, dependency deployment beyond the shared baseline, durable runtime-state preservation, richer hook outcomes/progress, and actual code removal still need refinement. This iteration establishes a runnable composition boundary rather than a complete plugin platform or lossless runtime suspension.
