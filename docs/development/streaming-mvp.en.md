# Streaming pipelines and RSS example

English | [中文](streaming-mvp.zh.md)

## Purpose and scope

The MVP uses an RSS reader and SQLite to explore reusable nodes across business modules. The first local runtime supports named streaming/synchronized ports and executable local plans. The proposed [framework Note](../../.agents/notes/2026-11-17-mvp-framework.en.md) and [node execution Note](../../.agents/notes/2026-11-17-mvp-node-execution.en.md) distinguish current choices from further design. MVP Notes remain editable proposals until merge into main; each increment commits code and corresponding Notes together.

Interaction uses CLI, in-process APIs, and generated HTTP clients. There is no visual graph editor or distributed execution. Graph definitions and database schemas have no prototype compatibility commitment; use a fresh workspace when experimenting with incompatible graph changes.

## Framework and business

| Part | Responsibility |
| - | - |
| [Framework](../../src/LazyTuiHub.Framework/) | Generic data relationships, node/port registration, planning, asynchronous delivery, synchronized-input alignment, fan-out, backpressure, and cancellation. |
| [Node library](../../src/LazyTuiHub.Nodes/) | Timer, HTTP fetch, typed output consumers, and helpers for common source/transform/sink patterns; uses ordinary core registration. |
| [RSS](../../src/LazyTuiHub.Rss/) | Subscriptions, RSS/Atom parsing, article storage, queries, and read state. |
| [Host](../../src/LazyTuiHub/) | Module composition, CLI, background refresh, and transport adaptation. |

PipelineDefinition represents processing relationships; DataReference and DataRelation identify persistent data and its associations. RSS stores business fields in its own tables and commits article writes and generic relationships in one SQLite transaction. These identities are separate from synchronized-input keys and do not implement a distributed object store.

The rss.ingest graph connects builtin.timer, rss.feed-requests, builtin.http-fetch, rss.parse, rss.store, and a typed output consumer. Another module may branch from parsing output without changing the runner. Reading and other direct RSS operations currently also use workflows; making workflows optional remains further work. Local and HTTP calls share IRssApi; fields, routes, pagination, deduplication, and errors are described in the [generated API reference](../api/index.md).

## Ports and execution plans

NodeRegistry.Register accepts typed, named input/output declarations and one asynchronous execution function. Each category may have any number of ports, including zero. Connections identify source, sourcePort, target, and targetPort. Type and delivery mode must match exactly; object is not a catch-all receiving port. Unused outputs may remain unconnected; every declared input needs a producer.

PipelinePlanner creates a PipelinePlan containing bound node implementations, port connections, local placement, and buffering capacity. The current backend requires a DAG and assigns all nodes to local. Creating/saving definitions checks a plan; running a definition builds a fresh plan, while callers may also execute a previously created plan. Resource inventories, remote placement, and remote transport are not implemented.

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
mise run run -- --rss help
mise run run -- --database ./artifacts/rss/workspace.db --rss add --feed-url https://example.com/feed.xml --title Example
mise run run -- --database ./artifacts/rss/workspace.db --rss feeds
mise run run -- --database ./artifacts/rss/workspace.db --rss refresh --name feeds/FEED_ID
mise run run -- --database ./artifacts/rss/workspace.db --rss articles --unread true
mise run run -- --database ./artifacts/rss/workspace.db --rss read --name feeds/FEED_ID/articles/ARTICLE_ID
```

Replace the feed URI with a real address. add returns the subscription name; articles returns article names and a continuation token, accepted by --page-token. read marks the article read and prints Proto JSON without executing HTML.

Without --database, the workspace is LazyTuiHub/workspace.db under system LocalApplicationData. Hosting HTTP starts background refresh. The initial graph uses a 15-minute interval, configurable with --refresh-interval 00:05:00 when first created; existing graph definitions are preserved. Timer backlog uses PeriodicTimer coalescing without replaying every missed tick. Refresh failures are logged and retried on the next interval.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --urls http://127.0.0.1:5080
```

## Inspecting and editing graphs

```sh
mise run run -- --database ./artifacts/rss/workspace.db --rss nodes
mise run run -- --database ./artifacts/rss/workspace.db --rss pipeline --pipeline-name rss.ingest
mise run run -- --database ./artifacts/rss/workspace.db --rss plan --pipeline-name rss.ingest
```

nodes describes named ports, payload types, and delivery modes; pipeline prints editable graph JSON; plan prints placement and port connections. A connection has this shape:

```json
{ "source": "parse", "sourcePort": "output", "target": "store", "targetPort": "input" }
```

Save pipeline output to a JSON file and edit it, then run or save it. plan and run/watch accept --pipeline-file or --pipeline-name. save-pipeline checks a plan and replaces the stored definition by name. Active runs keep their plan; later runs use the new definition. --progress true writes node and synchronization progress to stderr. Ctrl+C cancels run/watch.

```sh
mise run run -- --database ./artifacts/rss/workspace.db --rss plan --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss run --pipeline-file ./pipeline.json --progress true
mise run run -- --database ./artifacts/rss/workspace.db --rss watch --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss save-pipeline --pipeline-file ./pipeline.json
mise run run -- --database ./artifacts/rss/workspace.db --rss relations --feed-id FEED_ID
```

## Adding nodes and current boundaries

Register NodeDescriptor inputs/outputs using PortDescriptor.Stream or Sync and supply an async function. NodeContext.ReadEventsAsync reads individual stream inputs or complete synchronized groups using the run's cancellation token. WriteAsync sends a typed value to a named output; synchronized outputs also require messageKey. Forward keys explicitly when association must continue. StreamNodes.Source, Transform, and Sink are optional node-library helpers for common single-stream patterns, not separate core execution mechanisms.

The runtime uses .NET Channels behind its port API. Node functions need not manage competing read tasks or concrete queues. Normal consumers read inputs to completion or explicitly arrange cancellation. Cancellation is cooperative and cannot forcibly interrupt synchronous work. Microsoft.Data.Sqlite performs synchronous I/O; parser and database work execute on worker tasks. XmlReader honors declared encoding, preserves supported RSS/Atom content, and disables DTDs/external entities; not every extension or xml:base rule is covered.

Run progress is transient. Saved graphs do not save active queues or create crash recovery, checkpoints, durable replay, or exactly-once effects. Resource scheduling, pending-input budgets, complex key association, feedback graphs, and remote execution remain further work.

Public-network identity and authorization are not implemented, so this prototype serves trusted operators; subscription URIs trigger outbound requests. HTTP errors retain the existing ASP.NET Core google.rpc.Status JSON/ErrorInfo difference from AIP-193's outer error envelope and HTTP-valued code. Pagination keys live beside the database in keys; retain them for existing tokens to survive restart. No database schema standard or historical upgrade layer is introduced.
