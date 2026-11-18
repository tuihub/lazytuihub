# Streaming pipelines and RSS example

English | [中文](streaming-mvp.zh.md)

## Purpose and scope

The MVP uses an RSS reader and SQLite to explore reusable nodes across business modules. The first local runtime supports named streaming/synchronized ports and executable local plans. The proposed [framework Note](../../.agents/notes/2026-11-17-mvp-framework.en.md) and [node execution Note](../../.agents/notes/2026-11-17-mvp-node-execution.en.md) distinguish current choices from further design. MVP Notes remain editable proposals until merge into main; each increment commits code and corresponding Notes together.

Interaction uses an Avalonia desktop reader, CLI, in-process APIs, and generated HTTP clients. There is no visual graph editor or distributed execution. Graph definitions and database schemas have no prototype compatibility commitment; use a fresh workspace for incompatible changes. Use a fresh database for the split article schema and default ingestion graph.

The [desktop MVP guide](desktop-mvp.en.md) covers the runnable Avalonia UI, real RSS/SQLite operations, workflow inspection, and plugin availability. The [UI comparison guide](ui-comparison.en.md) retains the earlier independent experiments.

## Framework and business

| Part | Responsibility |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | Generic data relationships, node/port registration, planning, asynchronous delivery, synchronized-input alignment, fan-out, backpressure, and cancellation. |
| [Node library](../../src/LazyTuiHub.Nodes/) | Timer, HTTP fetch, typed output consumers, and helpers for common source/transform/sink patterns; uses ordinary core registration. |
| [Plugins](../../src/LazyTuiHub.Plugins/) | Trusted DLL composition, retained declarations/intent, workflow management, and task/hook invocation. |
| [DAO toolkit](../../src/LazyTuiHub.Dao/) | Backend-neutral contracts, access requirements/capabilities, and namespaced table targets. |
| [SQLite provider](../../src/LazyTuiHub.Storage.Sqlite/) | Required storage plugin supplying common JSON records/TTL, SQLite FTS5, native connections, and delegated cleanup. |
| [Core plugin](../../src/LazyTuiHub.CorePlugin/) | Required distribution plugin for greetings and built-in node registration. |
| [RSS plugin](../../src/LazyTuiHub.Rss/) | Subscriptions, scheduled fetching, fetch observations, and RSS/Atom parsing. |
| [Articles](../../src/LazyTuiHub.Articles/) | Article ingestion from any source, storage, FTS5 search, read state and reading API. |
| [Host](../../src/LazyTuiHub/) | Startup, plugin/workflow/DAO CLI, desktop/web composition, shared DI infrastructure, and lifecycle. |
| [Desktop](../../src/LazyTuiHub.Desktop/) / [Article UI](../../src/LazyTuiHub.Articles.Desktop/) | Avalonia shell and diagnostics / reader using IArticleApi and optional RSS subscription controls. |

PipelineDefinition represents processing relationships; DataReference and DataRelation identify persistent data and its associations. Each business plugin writes its own tables through BusinessStorage and returns data identities/relationship intent; the framework applies metadata and commits business rows, FTS updates, and metadata in one workspace transaction. BusinessStorage requires the workspace connection source explicitly; another store cannot join that transaction implicitly. Plugins must not modify framework tables. This is an ownership contract within a fully trusted process, without database permission isolation. These identities are separate from synchronized-input keys and do not implement a distributed object store.

The rss.ingest graph connects builtin.timer, rss.feed-requests, builtin.http-fetch, rss.parse, articles.store, and articles.output. Another module may branch from parsing output without changing the runner. Reading and other direct RSS operations currently also use workflows; making workflows optional remains further work. Local and HTTP callers use IRssApi for subscriptions and IArticleApi for articles; fields, routes, pagination, deduplication, and errors are described in the [generated API reference](../api/index.md).

To connect another source, emit [ArticleDraft](../../src/LazyTuiHub.Api/Articles/ArticleDraft.cs) and connect the output to articles.store. The source resource name and its entry ID identify the article together. For example, notebooks/a and notebooks/b can both use entry ID 1. Repeating an entry updates its content and preserves its article name and read state. A packet carrying a registered origin reference also records the relationship in the same transaction.

## Ports and execution plans

NodeRegistry.Register accepts typed, named input/output declarations and one asynchronous execution function. Each category may have any number of ports, including zero. Connections identify source, sourcePort, target, and targetPort. Type and delivery mode must match exactly; object is not a catch-all receiving port. Unused outputs may remain unconnected; every declared input needs a producer.

PipelinePlanner creates a PipelinePlan containing bound node implementations, port connections, local placement, and buffering capacity. The current backend requires a DAG and assigns all nodes to local. Saving returns feasibility diagnostics while retaining unavailable nodes and their original settings/connections; execution requires a valid plan; running a definition builds a fresh plan, while callers may also execute a previously created plan. Resource inventories, remote placement, and remote transport are not implemented.

## Streaming and synchronized delivery

StreamInput contains the port and DataPacket. Streaming data becomes readable promptly subject to backpressure; the framework promises no business processing order across stream messages or ports. Local fan-out separately delivers the same read-only payload reference to each branch. Transformations create new results, and GC reclaims released objects. This is not cross-process shared memory.

SynchronizedInput contains a messageKey and a read-only dictionary of the complete input group. All synchronized inputs on a node form one group. Writers explicitly supply a key; the core aligns matching keys and never exposes a partial group. Each key maintains received values per port in FIFO order, taking one per port for each group. Repeated keys form more groups rather than triggering deduplication. Different keys advance when ready; cross-producer business-round association is not inferred.

Synchronization establishes grouped visibility and declared dependencies, not physically simultaneous arrival or timing relative to stream data. Nodes own configuration application, state, and side effects. An async event loop can replace local configuration after reading a complete group and use a snapshot for subsequent stream processing. See the executable [mixed-input example](../../src/LazyTuiHub/Examples/WorkflowDemo.cs).

Each node has a bounded ingress with bufferCapacity defaulting to 16. Full queues wait; sequential fan-out lets slow branches backpressure upstream. Incomplete synchronized values move to a separate in-memory table without a current count/byte budget or disk spill. Bounded ingress therefore does not bound total memory. HTTP fetch buffers a full response; XML parsing and SQLite have their own costs.

A port closes after its last producer finishes. An incomplete group missing a closed port fails with the node, key, and missing ports, even if another stream is still open. Cancellation interrupts cooperative waits. Returning nodes stop accepting input; later writes fail instead of hanging. A node failure cancels peers and reports its identity and cause. Previously committed data remains; no whole-graph rollback or automatic node retry occurs.

## Running the examples

```sh
mise run demo:workflow
mise run demo:rss
mise run test
```

demo:workflow runs without HTTP or SQLite. One node receives two synchronized configuration inputs while processing a stream. Explicit acknowledgments coordinate the example: incomplete configuration leaves the initial stream result at 2, complete updates produce 7 and 13, then cancellation stops every task. These acknowledgments do not add cross-category ordering guarantees to the framework.

demo:rss creates a local RSS fixture, temporary SQLite database, and HTTP service, checks local/HTTP agreement, repeated refreshes, read state, and a cross-business branch, then removes its temporary data. Product tests additionally cover synchronization alignment and incomplete groups, strict port matching, backpressure, shared references, fan-in completion, failure cancellation, XML, pagination, and restart.

## Using an RSS workspace

```sh
mise run run -- --command rss --action help
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action feeds
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action refresh --name feeds/FEED_ID
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action list --unread true
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action read --name articles/ARTICLE_ID
```

Replace the feed URI with a real address. add returns the subscription name; articles list returns article names and a continuation token, accepted by --page-token. read marks the article read and prints Proto JSON without executing HTML.

Without --database, the workspace is LazyTuiHub/workspace.db under system LocalApplicationData. Hosting HTTP starts available plugin tasks whose required workflows can plan. The initial graph uses a 15-minute interval, configurable with --refresh-interval 00:05:00 when first created; existing graph definitions are preserved. Timer backlog uses PeriodicTimer coalescing without replaying every missed tick. Refresh failures are logged and retried on the next interval.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --urls http://127.0.0.1:5080
```

## Cold-start plugins

The host loads an explicit manifest at plugins/plugins.json under its application output; --plugin-directory DIR selects another directory. Build and publish stage core, rss, articles, articles.desktop, and storage.sqlite entry DLLs with their dependency descriptions. Plugins share the host dependency baseline and default assembly context. Additional plugin dependencies must be deployed with the plugin; this prototype has no private dependency versions, native-deployment system, hot reload, or sandbox. The [plugin Note](../../.agents/notes/2026-11-17-mvp-plugins.en.md) records the boundary and research.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action list
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action inspect --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action disable --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action enable --plugin rss
mise run run -- --database ./artifacts/rss/workspace.db --command plugins --action configure --plugin rss --file ./rss-settings.json
```

Changes apply on restart. list separates saved enablement, file availability, activation, and task eligibility; inspect includes retained node/port/data-type and DAO declarations plus opaque settings. RSS settings may contain {"refreshInterval":"00:05:00","detailRetention":"7.00:00:00"}; unknown fields survive. detailRetention is optional and defaults to null (permanent). A positive duration sets expiry on new fetch observations; changing it leaves existing rows unchanged. The interval initializes new default graphs and controls retry delay; edit an existing timer node's settings to change its cadence. Stored graphs take precedence over new defaults.

Disabled/missing plugins expose no business commands/routes/nodes and start no tasks. Graph inspection/editing, relationships, settings, and plugin diagnostics remain available, including when core or storage.sqlite is missing. These two required plugins cannot be disabled through normal configuration. A missing DAO provider blocks RSS activation with the unavailable contract, instance, mode, or backend in diagnostics; it preserves saved requirements and data for restoration. Removing and restoring a DLL does not change enabled intent or delete data, read state, graphs, relationships, or task declarations. A missing workflow implementation prevents execution and explains why. --command workflow --action list reports all saved graphs with feasibility. Missing declarations can identify owners from retained snapshots or a graph's explicit pluginId; otherwise ownership remains unknown.

--command plugins --action cleanup or uninstall --plugin ID explicitly invokes the optional plugin hook. Disabled maintenance targets can provide hooks at cold startup without activating their capabilities. Missing code or absent hooks report an error; RSS currently provides neither. The framework accepts hook completion without proving business cleanup. uninstall does not remove DLLs or catalog records. No hook runs automatically on disablement or absence.

To contribute a plugin, implement IApplicationPlugin.Configure(PluginRegistration), declare services/nodes/data types/default workflows/commands/routes/tasks and any DAO requirements/offers or retention delegations, and add its identity and entry DLL/type to the distribution manifest. requires lists hard service dependencies; DAO requirements match active provider capabilities before consumer activation, independently of manifest order. Cross-business workflow edges remain separate. Framework services manage metadata; business services receive IWorkflowExecutor. Saved default graphs are inserted only when absent. Task declarations list required workflows; tasks own their retry behavior and honor host cancellation. Declaration snapshots survive missing implementations, but active queues, synchronized inputs, and node state are still volatile.

## DAO tools and retention

Plugins declare required mode, contract, access instance, and optional backend using registration.Daos.Requirements. The current instance is workspace. The provider offers three modes: IRecordDao under a common records.ttl contract, ISqliteSearch under sqlite.fts5, and a native IConnectionSource under relational.connection. Public contracts use BCL DbConnection/DbTransaction, without SQLite driver types. SQLite is currently the only implementation; declaring a backend-neutral requirement alone does not provide another backend.

Plugins obtain tools through their DaoScope and pass DaoTable targets at each call. scope.Table("fetch_details") combines the plugin identity and local name; DaoNames encodes both components into safe SQL identifiers. Plugins own schema creation and private tables. No per-table catalog entry is required for ordinary tool use. Native connections are disposed by callers; specialized SQL may retain SQLite-specific behavior. FTS5 availability is probed at startup, and the articles plugin requires it for search.

The common record tool creates a key/JSON/nullable-expiry table. Put replaces both value and explicit expiry; null is permanent. Reads hide records whose absolute millisecond expiry has arrived; includeExpired exposes undeleted records for inspection. Keep removes expiry, including on an expired record not yet physically deleted. It does not turn observations into articles or framework data objects.

RSS records successful HTTP fetch URL, byte count, and completion time at parse ingress. It does not retain the response body, input checkpoints, or replay state. Article storage remains separate and permanent. Search currently returns article IDs using FTS5 query syntax; the articles search command exposes full-text search.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command articles --action search --query example
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action details --include-expired true
mise run run -- --database ./artifacts/rss/workspace.db --command rss --action keep-detail --key RECORD_KEY
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action list
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action clean
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action pause --owner rss --table fetch_details
mise run run -- --database ./artifacts/rss/workspace.db --command dao --action resume --owner rss --table fetch_details
```

Common cleanup only visits explicitly persisted PluginRegistration.Retentions delegations; using a template alone does not authorize cleanup. RSS delegates its fetch_details table. The storage plugin cleans once at HTTP server startup and then every minute by default; its cleanupInterval setting changes scheduling, not expiry. CLI mode starts no background tasks. Each delegated table uses a single DELETE of expired records without a work budget or batching promise. Target errors are retained independently with the last attempt and deletion count.

Delegation survives owner disablement/absence, so cleanup continues when the provider remains active; that behavior follows the prior explicit authorization. Missing providers retain data, expiry, delegation, and diagnostics. dao pause/resume persists across restart and controls subsequent passes; an already started deletion may finish. Pausing physical cleanup does not change expiry visibility. Runtime queues and synchronized inputs remain volatile and are not subject to these retention policies. Cross-store commits, durable-input holds, and recovery remain open in the [storage and DAO Note](../../.agents/notes/2026-11-17-mvp-storage-dao.en.md).

## Inspecting and editing graphs

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action nodes
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action definition --pipeline-name rss.ingest
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action plan --pipeline-name rss.ingest
```

nodes describes active named ports, payload types, and delivery modes; definition prints editable graph JSON; plan prints placement and port connections. A connection has this shape:

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

Save definition output to a JSON file and edit it, then run or save it. plan and run/watch accept --pipeline-file or --pipeline-name. save replaces the stored definition by name and reports feasibility, preserving missing nodes and pluginId provenance. plan reports missing implementations and owners; execution requires a valid plan. Active runs keep their plan; later runs use the new definition. --progress true writes node and synchronization progress to stderr. Ctrl+C cancels run/watch.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action plan --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action run --pipeline-file ./pipeline.json --progress true
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action watch --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action save --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --command workflow --action relations --data-type rss.feed --data-key FEED_ID
```

## Adding nodes and current boundaries

Register NodeDescriptor inputs/outputs using PortDescriptor.Stream or Sync and supply an async function. NodeContext.ReadEventsAsync reads individual stream inputs or complete synchronized groups using the run's cancellation token. WriteAsync sends a typed value to a named output; synchronized outputs also require messageKey. Forward keys explicitly when association must continue. StreamNodes.Source, Transform, and Sink are optional node-library helpers for common single-stream patterns, not separate core execution mechanisms.

The runtime uses .NET Channels behind its port API. Node functions need not manage competing read tasks or concrete queues. Normal consumers read inputs to completion or explicitly arrange cancellation. Cancellation is cooperative and cannot forcibly interrupt synchronous work. Microsoft.Data.Sqlite performs synchronous I/O; parser and database work execute on worker tasks. XmlReader honors declared encoding, preserves supported RSS/Atom content, and disables DTDs/external entities; not every extension or xml:base rule is covered.

Run progress is transient. Saved graphs do not save active queues or create crash recovery, checkpoints, durable replay, or exactly-once effects. Resource scheduling, pending-input budgets, complex key association, feedback graphs, and remote execution remain further work.

Public-network identity and authorization are not implemented, so this prototype serves trusted operators; subscription URIs trigger outbound requests. HTTP errors retain the existing ASP.NET Core google.rpc.Status JSON/ErrorInfo difference from AIP-193's outer error envelope and HTTP-valued code. Pagination keys live beside the database in keys; retain them for existing tokens to survive restart. No database schema standard or historical upgrade layer is introduced.
