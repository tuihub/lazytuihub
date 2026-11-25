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

The host owns startup configuration, discovery/composition, the shared service container, and application lifecycle. Business plugins own business services, storage/initialization, node adapters, HTTP adapters and typed operations shared by the CLI and GUI, and background tasks. Using DLLs does not make every framework library or shared contract an optional plugin. Existing Proto/HTTP agreements remain effective; this iteration defines no separate plugin-management API.

## Source ownership and cooperation

Each plugin ID owns one source root under [plugins](../../plugins/) and one C# project at its root. Its backend, shared frontend logic, GUI, CLI, public types and resources compile into the same DLL. Small plugins can keep files together. Larger plugins group them by responsibility. Prefer the PascalCase spelling of the ID for the package and source directory. Moving to another repository may change project references, build configuration, scripts and CI while preserving business source.

The root project declares PluginId, the display Title, an optional PluginVersion and each PluginEntryPoint kind and type. The base SDK build generates and embeds the manifest. The distribution index gets IDs and DLL filenames from the selected projects. The host chooses required members. Entry points share one plugin identity and enablement setting. The compiler checks the named types and their contracts. At cold startup the host reads assembly metadata, then instantiates the entries for its mode. Entry types can be internal and expose a public parameterless constructor.

| Process | Selected entry points when declared |
| - | - |
| Server only | Server |
| Local GUI or CLI | Server and Client, plus ClientGui or ClientCli for the current interface |
| Remote GUI or CLI | Client, plus ClientGui or ClientCli for the current interface |

Server registers business services, nodes and tasks. Client registers operations shared across interfaces and binds local services or HTTP adapters. ClientGui and ClientCli contribute their own interactions. A headless host can use the same DLL when the server and shared client types and their initialization resolve there. One DLL is the complete plugin for an OS when the host supplies the dependencies. Private dependencies and external resources ship alongside it.

Plugin projects reference [LazyTuiHub.Sdk](../../src/LazyTuiHub.Sdk/) for entry contracts, build-time declarations and workflow execution. The package ID, assembly and root namespace share that name. Workflow, DAO and interaction types keep their own namespaces and flow through package dependencies. Plugins register nodes through NodeRegistration and request plan execution through IWorkflowExecutor. The host owns the node registry, plan execution and plugin catalog. Plugins that expose Proto endpoints can also reference the shared API transport helpers.

A ClientGui entry can reference [LazyTuiHub.Sdk.Avalonia](../../src/LazyTuiHub.Sdk.Avalonia/) to register an Avalonia control, receive workspace updates and use the shared page model, adaptive layout and motion. The host supplies the active theme and tracks GUI operations through RunDesktopOperation. Its windows, theme packages and shutdown logic stay in the desktop host. Keep renderer types inside GUI code so the same DLL can load its server and CLI entries on a headless host.

Prefer workflow nodes for reusable capabilities. Other plugins may directly reference deliberately public types. Both parties maintain their semantics and compatibility. The framework checks explicitly declared plugin dependencies. Each plugin chooses how to organize its public types. For example, Steam, Bangumi and VNDB directly reference the [game provider types](../../plugins/LazyTuiHub.Plugin.TuiHub.Games/Providers/). Articles publishes article drafts and a subscription-source interface. RSS implements that interface and the reader offers subscription controls when the service is available.

## Plugin identity and resource namespaces

Each plugin declares a stable ID of the form `lazytuihub.plugin.<publisher>.<name>` in its project. The publisher occupies one segment. The name may span dot-separated segments. Each segment uses lowercase ASCII letters and digits, with hyphens between words. The loader compares complete IDs exactly. Dependencies, workflow provenance, plugin ACL namespaces and storage ownership use the same ID. Versions are declared separately. A host composes at most one plugin instance per ID in a process. Deployments of the same plugin on different devices retain the same ID. Device IDs identify deployment locations separately.

The default name for the project, entry assembly and root namespace of plugin-owned C# types is the full dotted PascalCase plugin ID, including the publisher. For example, the Games assembly is LazyTuiHub.Plugin.TuiHub.Games.dll and exposes LazyTuiHub.Plugin.TuiHub.Games.GameEntry. Another publisher can use its own prefix for the same business type name. A matching project name supplies the .NET SDK defaults for AssemblyName and RootNamespace. A differently named project can set both explicitly. Plugin-owned HTTP clients follow their partial classes under the plugin root. Proto controls the generated transport namespaces. PackageId independently names the NuGet package, so a package rename preserves CLR type names and consumer imports. Renaming a public namespace changes the C# API.

The installed folder uses the exact lowercase PluginId. Changing this ID and rebuilding updates distribution indexes, project relationships, resource URIs and new workflows. Existing workspace records retain their original identity. Provider keys, CLI commands and node types retain their own business names.

When a business plugin contributes access-controlled resources, its plugin ID is the first naming component in their URIs. The framework binds the namespace to the plugin ID established during loading. The plugin defines resource identifiers and read/write semantics within that namespace. Each resource declaration supplies its own account ownership and identity. [Account identity and access control Note](2026-11-19-mvp-account-identity.en.md)

## Plugin relationships and versions

Each cold start evaluates the plugins selected for the current process. A local host considers enabled selections with a present DLL. A remote frontend checks its own distribution. A declares relationships in its project. The build resolves project references to the target PluginId.

| A's declaration about B | B absent or disabled | B's version matches | B outside the range or missing a required version |
| - | - | - | - |
| requires | Block A | Configure B first. Local activation of A requires an active B. | Block A and report B, the requested range and the installed version. |
| compatibleWith | A may start independently. | A may use B when the feature runs. Either may start first. | Block A. B follows its own declarations. |

Set PluginRelation and an optional PluginVersionRange on a ProjectReference or PackageReference. The build reads the ID from the referenced project or from build metadata in its NuGet package. PluginProjectReference reads another project’s identity without a compiler reference. PluginReference names an independently published ID. PluginAlias exposes a referenced ID to code that needs it for workflow provenance. An alias alone imposes no load order. Omitting the range accepts any version. Authors can express major, minor, patch and prerelease versions with SemVer. The host checks the installed version with NuGet.Versioning. A bare `1.2.0` means at least 1.2.0, `[1.2.0]` is exact and `[1.0.0,2.0.0)` includes the lower bound and excludes the upper bound. `1.*` uses NuGet floating matching. Each ID still has one selected instance. Steam, Bangumi and VNDB require the game plugin's `[1.0.0,2.0.0)` interface. [NuGet versions and ranges](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning) · [SemVer](https://semver.org/)

Before invoking entry points, the host checks versions against the manifests selected for this startup. A and B may declare compatibleWith in both directions. Each range constrains its declarer. Only requires determines configuration order and dependency cycles. Local activation also waits for required plugins and DAO capabilities. If a matching B fails to configure or activate, A can still run its independent features. Plugins register services or factories in their entry points and resolve optional capabilities when the feature is used. Activation state observed during startup can still change. Workflow edges are resolved by the workflow planner.

Factorio also blocks the declarer when a present optional dependency has an incompatible version and separately supports mutual exclusion. This increment adopts required and compatible relationships. The three game providers coexist, and command registration already diagnoses duplicate names. Revisit explicit exclusion and its resolution policy when two implementations need exclusive ownership of a process resource or a known bad release needs a targeted exclusion. [Factorio mod dependencies](https://lua-api.factorio.com/latest/auxiliary/mod-structure.html#dependencies)

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

The Server entry implements IApplicationPlugin and collects services, nodes, data types, default workflows, commands, routes, tasks, and optional hooks through PluginRegistration. PluginRuntime stages service contributions before host construction, then stages node/endpoint declarations before activation. Required relationships order plugin configuration. Required plugins and DAO requirements govern local activation. All node registration finishes before default workflows are saved and task feasibility is checked. Plugin side effects and later activation failures have no global rollback guarantee.

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

The [desktop increment](2026-11-17-mvp-ui-comparison.en.md) adds plain page identity/title declarations to framework-owned plugin metadata. Avalonia factories live in the desktop layer; the desktop reader requires the article library and offers subscription controls when RSS is active. Navigation uses pages from active plugin entry points. The catalog retains previously observed pages and commands per entry point across changes of host mode, disablement or absence. Desktop and web hosts share service composition; desktop activation takes a service provider without HTTP endpoint mapping. Existing task and cold-start preservation semantics apply to both hosts.

The host selects distribution members and marks core plugins. Build and publish place the assembly with its embedded manifest and any private payload in `plugins/<id>/`. The distribution index plugins/plugins.json records IDs, DLL filenames and required membership. Replacing a selected plugin changes its declared version and relationships at the next cold start. The catalog retains user intent, settings and contributions independently of deployment.

The core, RSS, article and SQLite plugins implement the same entry contract. Public DAO contracts live in LazyTuiHub.Dao. Plugins declare access requirements independently of concrete table targets; providers publish capabilities, and consumers activate after matching mode/contract/instance/backend. Activation does not depend on manifest order. Declaration snapshots retain requirements/offers even when a consumer cannot activate at first startup. The storage plugin supplies records/TTL, FTS5, native connections, and delegated cleanup; minimal framework metadata remains usable without it. RSS owns its API implementation, transport adapter, CLI command, initialization, and refresh task. The minimal host retains plugin management, DAO diagnostics/retention controls, and workflow inspection/editing/execution. WorkflowRuntime owns graph storage, registration and planning; business services receive IWorkflowExecutor. Existing RSS business operations still traverse workflows, so making workflows optional for these services remains later work.

--command plugins manages list/inspect, enable/disable, opaque configuration, and explicit cleanup/uninstall hook invocation. Saved changes require restart and do not mutate current activation. Disabled maintenance targets may load at cold startup only to supply services/hooks, without nodes, routes, commands, or task activation. Hooks currently return Task: the command reports completion, errors, and cancellation without semantic verification or a dedicated progress protocol. RSS provides neither hook. Uninstall invokes an optional hook; it does not delete code or catalog records.

--command workflow remains usable with missing plugins. Saving preserves unavailable nodes, parameters, connections, and pluginId provenance while returning current feasibility diagnostics; execution requires a valid plan. Unknown imported types without provenance remain unknown. Default workflows use insert-if-absent, preserving user changes. Persisted task declarations retain ownership and required workflow names; runtime task state is transient. Normal server startup runs one instance of each available plugin task only when its required workflows can plan. Host shutdown cancels tasks cooperatively; business tasks own retry policy.

Product tests use separate CLI processes and one workspace to verify enabled/disabled/absent/restored RSS, retained articles/read state/relationships/settings/opaque graph parameters, missing core and manifest diagnostics, unknown imported types, and unavailable tasks/commands. Lifecycle tests verify cold maintenance, explicit-only hooks without a semantic cleanup verifier, task cancellation, and shared business/metadata transaction rollback. Storage tests additionally verify missing/restored provider DLLs, unavailable requirements on first startup, all three access modes, delegated-only cleanup, permanent records, pause persistence, and FTS/business/catalog rollback. Existing local/generated HTTP scenarios continue through dynamically loaded plugins.

Initialization failure cleanup, dependency deployment beyond the shared baseline, durable runtime-state preservation, richer hook outcomes/progress, and actual code removal still need refinement. This iteration establishes a runnable composition boundary rather than a complete plugin platform or lossless runtime suspension.
