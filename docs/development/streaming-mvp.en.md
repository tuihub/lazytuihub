# Streaming pipelines and RSS example

English | [中文](streaming-mvp.zh.md)

## Purpose and scope

The RSS reader uses SQLite and exercises typed streaming and synchronized ports, local execution and remote dispatch of eligible operations. This guide describes the runnable prototype. [Framework design](../../.agents/notes/2026-11-17-mvp-framework.en.md) · [Node execution](../../.agents/notes/2026-11-17-mvp-node-execution.en.md)

Interaction uses an Avalonia desktop reader, CLI, in-process APIs, and generated HTTP clients. Plugins define workflows for operations such as feed refresh. During plugin development you can inspect or edit a graph as JSON. Use a fresh workspace when switching between incompatible prototype schemas or graph definitions, including those predating the separate Articles plugin.

The [desktop MVP guide](desktop-mvp.en.md) covers the runnable Avalonia UI, real RSS/SQLite operations, workflow inspection, and plugin availability. The [UI comparison guide](ui-comparison.en.md) retains the earlier independent experiments.

## Framework and business

| Part | Responsibility |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | Generic data relationships, node/port registration, planning, asynchronous delivery, synchronized-input alignment, fan-out, backpressure, and cancellation. |
| [Node library](../../src/LazyTuiHub.Nodes/) | Common port types and helpers for source, transform and sink nodes. |
| [Plugin SDK](../../src/LazyTuiHub.Sdk/) | Registration, workflow calls, optional capability lookup and lifecycle hooks. |
| [LazyTuiHub.Sdk.Avalonia](../../src/LazyTuiHub.Sdk.Avalonia/) | Page registration, workspace refresh, adaptive panes, motion and host-tracked GUI operations. |
| [Plugins](../../src/LazyTuiHub.Plugins/) | Trusted DLL composition, retained declarations/intent, workflow management, and task/hook invocation. |
| [DAO toolkit](../../src/LazyTuiHub.Dao/) | Backend-neutral contracts, access requirements/capabilities, and namespaced table targets. |
| [SQLite provider](../../plugins/LazyTuiHub.Plugin.TuiHub.Storage.Sqlite/) | Required storage plugin supplying common JSON records/TTL, SQLite FTS5, native connections, and delegated cleanup. |
| [Core plugin](../../plugins/LazyTuiHub.Plugin.TuiHub.Core/Backend/) | Required distribution plugin for greetings and built-in node registration. |
| [RSS plugin](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/) | Subscriptions, scheduled fetching, fetch observations, and RSS/Atom parsing. |
| [Articles](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Backend/) | Article ingestion from any source, storage, FTS5 search, read state and reading API. |
| [Game library and providers](../../.agents/notes/2026-11-23-mvp-games.en.md) | Personal games, categories and sessions, with Steam, Bangumi and VNDB lookup and import workflows. |
| [Host](../../src/LazyTuiHub/) | Startup, plugin/workflow/DAO CLI, desktop/web composition, shared DI infrastructure, and lifecycle. |
| [CLI](../../src/LazyTuiHub.Cli/) | The host CLI lists local plugin operations with `mise run run:cli`. Pass `--endpoint https://SERVER` for remote sessions, feeds and articles. The standalone CLI connects to a remote server. [CLI and Windows 7](../../.agents/notes/2026-11-23-mvp-win7-cli.en.md) |
| [Desktop](../../src/LazyTuiHub.Desktop/) / [Article UI](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Gui/) | Avalonia shell and diagnostics / reader using IArticleApi and optional RSS subscription controls. |

`PipelineDefinition` describes processing. `DataReference` identifies a business object, and `DataRelation` links objects. A business plugin that needs catalog registration uses `BusinessStorage` with the workspace connection source. Its changes, index updates and framework relationships commit in one transaction. Plugins own their business tables. The framework owns catalog writes. The boundary relies on trusted code in the shared process. Synchronized-input keys have a separate role.

`rss.ingest` connects `builtin.timer`, `rss.feed-requests`, `builtin.http-fetch`, `rss.parse`, `articles.store` and `articles.output`. Plugins can subscribe to parser output. RSS records a fetch observation before parsing and Articles commits each article separately. A later interruption preserves these writes. RSS and Articles also use graphs for their reader operations. Both local and HTTP callers use `IRssApi` and `IArticleApi`. [API reference](../api/index.md)

To connect another source, emit [ArticleDraft](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Api/ArticleDraft.cs) and connect the output to articles.store. The source resource name and its entry ID identify the article together. For example, notebooks/a and notebooks/b can both use entry ID 1. Repeating an entry updates its content and preserves its article name and read state. A packet carrying a registered origin reference also records the relationship in the same transaction.

Reference `LazyTuiHub.Sdk` directly when building a plugin with NuGet. The package supplies the registration contracts and imports the build targets that generate the embedded manifest from the project. Add `LazyTuiHub.Sdk.Avalonia` for a ClientGui entry that registers a page. Keep Avalonia types in that entry and its views so the same DLL can supply its other entries in a CLI or server process.

Name the project at each plugin root using the full dotted PascalCase ID, including the publisher. The .NET SDK uses the project name for AssemblyName and RootNamespace by default. Place plugin-owned types under that namespace. For example, the Games plugin exposes LazyTuiHub.Plugin.TuiHub.Games.GameEntry. Reference entry types through `$(RootNamespace)` in the project. Declare PluginId, Title, optional PluginVersion and each PluginEntryPoint in the [project file](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/LazyTuiHub.Plugin.TuiHub.Articles.csproj). Server registers backend services, Client registers shared client operations, and ClientGui and ClientCli register interface-specific interactions. The host selects distribution members with BundledPlugin. The build generates the embedded manifest and distribution index. Code obtains its identity from the registration context or generated PluginProject.Id. PackageId independently sets the NuGet name. [Plugin Note](../../.agents/notes/2026-11-17-mvp-plugins.en.md)

A report can reference article operations and offer game statistics when Games is available. Rebuilding resolves the article project's current PluginId. A PackageReference with PluginRelation reads the plugin identity shipped in its NuGet package. Use PluginReference for a runtime relationship by ID when there is no compiler reference.

```xml
<ItemGroup>
  <ProjectReference Include="../LazyTuiHub.Plugin.TuiHub.Articles/LazyTuiHub.Plugin.TuiHub.Articles.csproj" PluginRelation="Requires" />
  <PluginReference Include="lazytuihub.plugin.tuihub.games"
                   PluginRelation="CompatibleWith" PluginVersionRange="[1.0.0,2.0.0)" />
</ItemGroup>
```

The host calls the Articles entry points before the reporting plugin’s. Games and the reporting plugin may start in either order, even if both declare a compatible version range for the other. Resolve the game capability when a user opens game statistics. Without Games the report still starts. If Games 2.0.0 is enabled, the host blocks the reporting plugin and reports the requested range and actual version.

## Ports and execution plans

NodeRegistration.Register accepts typed, named input/output declarations and one asynchronous execution function. Each category may have any number of ports, including zero. Connections identify source, sourcePort, target, and targetPort. Type and delivery mode must match exactly; object is not a catch-all receiving port. Unused outputs may remain unconnected; every declared input needs a producer.

The planner combines each node descriptor's required resource with any extra resource names in the workflow node’s `requires` array. Each instance registers its resources independently. For example, `articles.store` requires `workspace.primary`, while a digest workflow can request `compute.batch`. Workflow JSON contains only logical nodes, settings, requirements, and edges. A plan shows the resolved requirements and candidate instances, plus any unreachable inventory endpoints. Running a saved definition builds a fresh plan. Saving retains definitions with missing capabilities and reports diagnostics.

## Multi-node prototype

Use `mise -E development run test:multinode` with a running Docker engine and Compose. It publishes the host, runs the scenarios on isolated networks, writes observations to `.local/multinode/result.json`, and removes the containers, networks, and test volume. The [Compose fixture](../../tests/multinode/compose.yaml) shows the complete configuration. Set `TEST_APP_PLATFORM=linux/amd64` when the cached .NET image is AMD64 on an ARM64 engine. This selects the RID and platform for the application nodes only. The fixture configures peer URLs and pushes work. Worker polling and plugin-inventory parity are design targets. [Coordination Note](../../.agents/notes/2026-11-19-mvp-coordination.en.md)

| Setting | Purpose |
| --- | --- |
| `Cluster:Id`, `Cluster:Resources` | This instance's identity and named resources. Assign `workspace.primary` to the instance that owns the authoritative article database. Standalone mode uses its local workspace. |
| `Cluster:Peers` | Addresses and bearer credentials for reachable worker endpoints. Each entry has `Url` and `Token`. The planner reads operations and resources from those endpoints when planning. |
| `--worker true`, `Cluster:Token`, `Cluster:Export` | Start the authenticated worker endpoint with its own credential and allowed operation types. Only loaded packet implementations can be exported. |
| `Cluster:FetchOrigins` | HTTP origins the worker may fetch. A worker does not follow redirects. Use HTTPS for peer traffic. The internal Docker networks explicitly enable `Cluster:AllowInsecureHttp`. |

The coordinator calls a local implementation directly or sends a typed packet with its reference over the generated HTTP client. Only registered single-input, single-output streaming packet operations can cross this boundary. Each packet of a repeatable transform tries the local instance first, then eligible workers in instance ID order. Other streams, synchronized groups, and queues stay on the coordinator. A plan takes one inventory snapshot. New or recovered workers enter on the next plan. A plan can continue using reachable candidates if others become unavailable during execution.

For an owner-bound operation such as `articles.store`, the planner requires exactly one eligible owner. A lost write acknowledgement produces an unknown outcome. Inspect the owner before deciding whether to submit again. RSS feed selection and fetch details live in the submitting instance’s workspace. The article and its source relation live on the selected owner. The [worker protocol](../api/operations/WorkerService_ExecutePacket.md) describes the wire contract.

## Streaming and synchronized delivery

StreamInput contains the port and DataPacket. Streaming data becomes readable promptly subject to backpressure; the framework promises no business processing order across stream messages or ports. Local fan-out separately delivers the same read-only payload reference to each branch. Transformations create new results, and GC reclaims released objects. This is not cross-process shared memory.

A node reads the key from `SynchronizedInput.MessageKey` and the complete group from the read-only `Values` dictionary. Writers supply the key. The framework queues values by key and port and delivers a group when it can take one value from each port. A key can form successive groups without deduplication. Different keys advance as their groups become ready. Plugins define how rounds from different producers relate.

Synchronization establishes grouped visibility and declared dependencies, not physically simultaneous arrival or timing relative to stream data. Nodes own configuration application, state, and side effects. An async event loop can replace local configuration after reading a complete group and use a snapshot for subsequent stream processing. See the executable [mixed-input example](../../src/LazyTuiHub/Examples/WorkflowDemo.cs).

Each node has a bounded ingress with bufferCapacity defaulting to 16. Full queues wait; sequential fan-out lets slow branches backpressure upstream. Incomplete synchronized values move to a separate in-memory table without a current count/byte budget or disk spill. Bounded ingress therefore does not bound total memory. HTTP fetch buffers a full response; XML parsing and SQLite have their own costs.

A port closes after its last producer finishes. An incomplete group missing a closed port fails with the node, key, and missing ports, even if another stream is still open. Cancellation interrupts cooperative waits. Returning nodes stop accepting input; later writes fail instead of hanging. A node failure cancels peers and reports its identity and cause. Previously committed data remains; no whole-graph rollback or automatic node retry occurs.

## Running the examples

```sh
mise -E development run demo:workflow
mise -E development run demo:rss
mise run test
```

demo:workflow runs without HTTP or SQLite. One node receives two synchronized configuration inputs while processing a stream. Explicit acknowledgments coordinate the example: incomplete configuration leaves the initial stream result at 2, complete updates produce 7 and 13, then cancellation stops every task. These acknowledgments do not add cross-category ordering guarantees to the framework.

demo:rss creates a local RSS fixture, temporary SQLite database, and HTTP service, checks local/HTTP agreement, repeated refreshes, read state, and a cross-business branch, then removes its temporary data. Product tests additionally cover synchronization alignment and incomplete groups, strict port matching, backpressure, shared references, fan-in completion, failure cancellation, XML, pagination, and restart.

## Using an RSS workspace

```sh
mise run run -- --command rss --action help
mise run run -- --database .local/rss/workspace.db --command rss --action add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database .local/rss/workspace.db --command rss --action feeds
mise run run -- --database .local/rss/workspace.db --command rss --action refresh --name feeds/FEED_ID
mise run run -- --database .local/rss/workspace.db --command articles --action list --unread true
mise run run -- --database .local/rss/workspace.db --command articles --action read --name articles/ARTICLE_ID
```

Replace the example feed URL with a real one. `rss add` returns the subscription name. `articles list` returns article names and a token for the next page. Pass it with `--page-token`. `articles read` marks the article as read and prints its Protobuf JSON without executing any embedded HTML.

Without `--database` the host uses `LazyTuiHub/workspace.db` under the local application-data directory. In single-account mode, the HTTP or local desktop host starts eligible plugin tasks. The default RSS timer ticks every 15 minutes. Set `--refresh-interval 00:05:00` when creating a workspace, or edit the timer in an existing graph. `PeriodicTimer` coalesces missed ticks. A failed run logs its error and waits one refresh interval before retrying.

```sh
mise run run -- --database .local/rss/workspace.db --urls http://127.0.0.1:5080
```

## Enabling accounts and resource authorization

Use a fresh SQLite workspace. Set `Access__Enabled=true` on the primary and supply `Access__AdminUser` and `Access__AdminPassword` for initial provisioning. Configure an HTTPS listener with a certificate your clients trust. In account mode, start RSS ingestion through an explicit refresh.

```sh
mise run run -- --command access --action login --endpoint https://primary.example --username admin
mise run run -- --database .local/account/workspace.db --command access --action create-user --username reader
mise run run -- --database .local/account/workspace.db --command access --action set-device-owner --device fetcher --username admin
mise run run -- --database .local/account/workspace.db --command access --action grant --username reader --uri lazytuihub://core/devices/fetcher/jobs --mode write
```

The terminal hides the password prompt input. Supply the session token from the response through `Access__Session` for administration on the primary, local CLI, or the desktop app. Remote requests use the Bearer token in the `Authorization` header and select the user's own account by default. To access a shared resource, select its account with `X-LazyTuiHub-Account`. To share a subscription, set `--uri` to `lazytuihub://lazytuihub.plugin.tuihub.rss/accounts/ACCOUNT_ID/feeds/FEED_ID` and grant `read`. Revoke a grant using `--action revoke` with the same URI and username. Revoke a remote session with `--action logout` and the same HTTPS endpoint.

In account mode, the coordinator selects a fetcher using both its advertised resources and the user's write access to its job endpoint. Use the configured instance ID for `--device`. A standalone instance uses `local`. Article writes follow the workspace binding and require write access to the article collection. Workers in this workflow also use `Access__Enabled=true`. [Account and URI authorization Note](../../.agents/notes/2026-11-19-mvp-account-identity.en.md) · [Session API](../api/operations/SessionService_AuthenticateUser.md)

## Cold-start plugins

The host loads an explicit manifest at plugins/plugins.json under its application output; --plugin-directory DIR selects another directory. Build and publish place each selected entry DLL and its dependencies in `plugins/<id>/`. Plugins share the host dependency baseline and default assembly context. Additional plugin dependencies must be deployed with the plugin; this prototype has no private dependency versions, native-deployment system, hot reload, or sandbox. The [plugin Note](../../.agents/notes/2026-11-17-mvp-plugins.en.md) records the boundary and research.

```sh
mise run run -- --database .local/rss/workspace.db --command plugins --action list
mise run run -- --database .local/rss/workspace.db --command plugins --action inspect --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action disable --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action enable --plugin lazytuihub.plugin.tuihub.rss
mise run run -- --database .local/rss/workspace.db --command plugins --action configure --plugin lazytuihub.plugin.tuihub.rss --file ./rss-settings.json
```

Changes apply on restart. list separates saved enablement, file availability, activation, and task eligibility; inspect includes retained node/port/data-type and DAO declarations plus opaque settings. RSS settings may contain {"refreshInterval":"00:05:00","detailRetention":"7.00:00:00"}; unknown fields survive. detailRetention is optional and defaults to null (permanent). A positive duration sets expiry on new fetch observations; changing it leaves existing rows unchanged. The interval initializes new default graphs and controls retry delay; edit an existing timer node's settings to change its cadence. Stored graphs take precedence over new defaults.

Disabled/missing plugins expose no business commands/routes/nodes and start no tasks. Graph inspection/editing, relationships, settings, and plugin diagnostics remain available, including when core or storage.sqlite is missing. These two required plugins cannot be disabled through normal configuration. A missing DAO provider blocks RSS activation with the unavailable contract, instance, mode, or backend in diagnostics; it preserves saved requirements and data for restoration. Removing and restoring a DLL does not change enabled intent or delete data, read state, graphs, relationships, or task declarations. A missing workflow implementation prevents execution and explains why. --command workflow --action list reports all saved graphs with feasibility. Missing declarations can identify owners from retained snapshots or a graph's explicit pluginId; otherwise ownership remains unknown.

--command plugins --action cleanup or uninstall --plugin ID explicitly invokes the optional plugin hook. Disabled maintenance targets can provide hooks at cold startup without activating their capabilities. Missing code or absent hooks report an error; RSS currently provides neither. The framework accepts hook completion without proving business cleanup. uninstall does not remove DLLs or catalog records. No hook runs automatically on disablement or absence.

Register services, nodes, data types, routes and tasks in the Server entry. The SDK generates the manifest from project declarations. The host orders required dependencies, resolves DAO capabilities and then checks the workflows each task requires. `IWorkflowExecutor` runs saved definitions. Defaults are inserted only when absent. Plugins own retry and cancellation behavior. The catalog retains declarations across restarts. Queued messages, synchronized inputs and node state live in memory.

## DAO tools and retention

Plugins declare required mode, contract, access instance, and optional backend using registration.Daos.Requirements. The current instance is workspace. The provider offers three modes: IRecordDao under a common records.ttl contract, ISqliteSearch under sqlite.fts5, and a native IConnectionSource under relational.connection. Public contracts use BCL DbConnection/DbTransaction, without SQLite driver types. SQLite is currently the only implementation; declaring a backend-neutral requirement alone does not provide another backend.

Plugins obtain tools through their DaoScope and pass DaoTable targets at each call. scope.Table("fetch_details") combines the plugin identity and local name; DaoNames encodes both components into safe SQL identifiers. Plugins own schema creation and private tables. No per-table catalog entry is required for ordinary tool use. Native connections are disposed by callers; specialized SQL may retain SQLite-specific behavior. FTS5 availability is probed at startup, and the articles plugin requires it for search.

The common record tool creates a key/JSON/nullable-expiry table. Put replaces both value and explicit expiry; null is permanent. Reads hide records whose absolute millisecond expiry has arrived; includeExpired exposes undeleted records for inspection. Keep removes expiry, including on an expired record not yet physically deleted. It does not turn observations into articles or framework data objects.

RSS records successful HTTP fetch URL, byte count, and completion time at parse ingress. It does not retain the response body, input checkpoints, or replay state. Article storage remains separate and permanent. Search currently returns article IDs using FTS5 query syntax; the articles search command exposes full-text search.

```sh
mise run run -- --database .local/rss/workspace.db --command articles --action search --query example
mise run run -- --database .local/rss/workspace.db --command rss --action details --include-expired true
mise run run -- --database .local/rss/workspace.db --command rss --action keep-detail --key RECORD_KEY
mise run run -- --database .local/rss/workspace.db --command dao --action list
mise run run -- --database .local/rss/workspace.db --command dao --action clean
mise run run -- --database .local/rss/workspace.db --command dao --action pause --owner lazytuihub.plugin.tuihub.rss --table fetch_details
mise run run -- --database .local/rss/workspace.db --command dao --action resume --owner lazytuihub.plugin.tuihub.rss --table fetch_details
```

RSS delegates `fetch_details` through `PluginRegistration.Retentions`. When plugin tasks run, the storage plugin cleans delegated tables at startup and then every minute by default. `cleanupInterval` sets the schedule. Each pass issues one `DELETE` per table and records its result, error or deletion count independently. CLI commands and account-mode hosts do not start the background cleaner.

Delegation survives owner disablement/absence, so cleanup continues when the provider remains active; that behavior follows the prior explicit authorization. Missing providers retain data, expiry, delegation, and diagnostics. dao pause/resume persists across restart and controls subsequent passes; an already started deletion may finish. Pausing physical cleanup does not change expiry visibility. Runtime queues and synchronized inputs remain volatile and are not subject to these retention policies. Cross-store commits, durable-input holds, and recovery remain open in the [storage and DAO Note](../../.agents/notes/2026-11-17-mvp-storage-dao.en.md).

## Inspecting and editing graphs during plugin development

```sh
mise run run -- --database .local/rss/workspace.db --command workflow --action nodes
mise run run -- --database .local/rss/workspace.db --command workflow --action definition --pipeline-name rss.ingest
mise run run -- --database .local/rss/workspace.db --command workflow --action plan --pipeline-name rss.ingest
```

nodes describes active named ports, payload types, and delivery modes; definition prints editable graph JSON; plan prints placement and port connections. A connection has this shape:

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

Save definition output to a JSON file and edit it, then run or save it. plan and run/watch accept --pipeline-file or --pipeline-name. save replaces the stored definition by name and reports feasibility, preserving missing nodes and pluginId provenance. plan reports missing implementations and owners; execution requires a valid plan. Active runs keep their plan; later runs use the new definition. --progress true writes node and synchronization progress to stderr. Ctrl+C cancels run/watch.

```sh
mise run run -- --database .local/rss/workspace.db --command workflow --action plan --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action run --pipeline-file ./pipeline.json --progress true
mise run run -- --database .local/rss/workspace.db --command workflow --action watch --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action save --pipeline-file ./pipeline.json
mise run run -- --database .local/rss/workspace.db --command workflow --action relations --data-type rss.feed --data-key FEED_ID
```

## Adding nodes and current boundaries

Register NodeDescriptor inputs/outputs using PortDescriptor.Stream or Sync and supply an async function. NodeContext.ReadEventsAsync reads individual stream inputs or complete synchronized groups using the run's cancellation token. WriteAsync sends a typed value to a named output; synchronized outputs also require messageKey. Forward keys explicitly when association must continue. StreamNodes.Source, Transform, and Sink are optional node-library helpers for common single-stream patterns, not separate core execution mechanisms.

The runtime uses .NET Channels behind its port API. Node functions need not manage competing read tasks or concrete queues. Normal consumers read inputs to completion or explicitly arrange cancellation. Cancellation is cooperative and cannot forcibly interrupt synchronous work. Microsoft.Data.Sqlite performs synchronous I/O; parser and database work execute on worker tasks. XmlReader honors declared encoding, preserves supported RSS/Atom content, and disables DTDs/external entities; not every extension or xml:base rule is covered.

Run progress is transient. Saved graphs do not save active queues or create crash recovery, checkpoints, durable replay, or exactly-once effects. Capacity scheduling, pending-input budgets, complex key association, feedback graphs, and remote synchronized execution remain further work.

Single-account mode assumes a trusted operator. Account mode requires HTTPS and a user session for business requests. In either mode, a subscription URL can trigger outbound HTTP access. ASP.NET Core returns `google.rpc.Status` with `ErrorInfo`, which differs from AIP-193’s outer HTTP error envelope and HTTP status code field. Keep the `keys/` directory beside the database to preserve pagination tokens across restarts. The MVP provides no migration path for discarded prototype schemas.
