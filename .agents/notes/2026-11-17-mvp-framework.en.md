---
status: proposed
category: architecture
---
# MVP framework responsibilities and execution plans

English | [中文](2026-11-17-mvp-framework.zh.md)

## Provisional choices

Until the MVP merges into main, choose frameworks, libraries, protocols, storage, schemas and component boundaries for the final product. Compare the supported uses of a dependency with alternatives. Sunk effort, the cost of replacing prototype code and compatibility with discarded experiments carry no weight. Report a package limitation at the scope the evidence supports. Frozen repository and API Notes follow their own revision lifecycle. [Repository conventions](../../AGENTS.md)

Evaluate long-term maintenance, third-party stability and future switching costs for the delivered product, including the option to maintain components in-house.

## Scope and maintenance

The [node execution Note](2026-11-17-mvp-node-execution.en.md) defines ports, synchronization, backpressure and node state. The [plugin Note](2026-11-17-mvp-plugins.en.md) defines cold loading, metadata ownership and preservation when a plugin is missing.

The [storage and DAO Note](2026-11-17-mvp-storage-dao.en.md) maintains three access modes for all DAO resources, namespaces, managed templates, retention, and requirement matching. Its declarations will inform plan resource binding; the detailed resource model remains open.

MVP Notes remain `proposed` until merge. Changes to prototype code, graphs and SQLite schemas carry no compatibility commitment. [MVP maintenance agreement](../../AGENTS.md#notes)

The [running guide](../../docs/development/streaming-mvp.en.md) covers named ports, synchronized inputs, local execution and remote packet dispatch. Durable replay remains open.

## Goals and application boundaries

The application is a client/server monolith with inexpensive in-process calls and conventional HTTP communication across processes and devices. Future WebSocket support retains a separate adaptation boundary. Follow the [protocol baseline](2026-11-15-protobuf-api-contract.en.md): C# implements business logic while generation covers contracts and communication code. Internal workflow ports do not thereby all become public APIs.

RSS owns subscriptions, fetch observations, and RSS/Atom parsing. Its parser emits the shared ArticleDraft stream. The articles plugin accepts that stream from any producer and owns durable article identity, indexing and read state. The producer supplies a source resource name and an entry ID stable within that source. A repeated pair updates the content and preserves the article name and read state. The framework records source relationships and drives the graph. The rss.ingest graph connects the two plugins without a hard plugin dependency.

Plugins can use workflows to compose their own operations and reuse nodes from other plugins. A plugin can call a business service directly or define, register and update a workflow whose connections the framework validates and whose plan it builds and runs. Both paths reuse the business implementation.

Users work with subscriptions and articles through the CLI, GUI or API. For example, when a user chooses to refresh a subscription, the RSS plugin can invoke fetch, parse and article-storage nodes. The business entry point should explain the refresh result or why it failed. Users need no knowledge of nodes, execution plans or graph editing. Definition, editing and inspection interfaces serve plugin development and debugging. A general-purpose workflow editor falls outside the user-facing product requirements.

## Core and node libraries

| Part | Responsibility |
| - | - |
| Business modules | Own domain data and constraints, implement functionality, and register reusable nodes and ports. |
| Framework core | Maintain generic data relationships, node/connection declarations and resource information; generate and drive plans; provide transport, synchronized dependencies, fan-out, backpressure, and execution explanations. |
| Built-in node library | Supply timers, HTTP fetch, and other common components, with conversion or signal helpers when needed, using the same registration mechanism as business modules. |
| Host and transport adapters | Compose modules and expose plugin business operations through CLI and protocol entry points. |

Separate timers and HTTP fetch from core execution rules. The core does not infer business behavior from node names, RSS types, control values, or an idempotency flag. Business implementations and their inputs determine idempotency, deduplication, recomputation, and side effects; the core does not turn a boolean into a universal retry policy.

## Data relationships and processing relationships

Retain two independent generic relationship models: associations between data objects, and processing connections between node ports. Data associations help modules reference or discover other modules' data; processing connections describe movement through functionality. Modules retain business fields and invariants rather than flattening all business tables into a universal document model.

Persistent object identity, runtime payload references, and synchronized-input message keys serve different purposes and are not implicitly interchangeable. In-process execution may share object references under a read-only agreement and produce new results when transforming data. Processes and devices need transferable representations or resolvable references rather than CLR pointers. The current DataReference identifies data; it is not an implemented distributed object-store reference.

## Publishing workflow results

A plugin chooses which business changes to publish together and writes them in one database transaction. The commit publishes the whole result to subsequent queries without a separate flag. The plugin and storage layer handle rollback and verify uncertain commits. The workflow advances according to the outcome the node reports.

Downstream nodes can consume in-memory data as soon as it arrives during a run. For example, the RSS parser can stream article drafts to article storage and other plugins at the same time. The article plugin currently commits each article with its index update and source relationship. If a refresh stops, readers can still access the articles already committed. A plugin can also commit per feed so that the articles from one fetch become visible together. Both granularities satisfy the workflow publication contract. Resuming work on the same input after a restart also requires access to the original content and the context needed to process it. [RSS workflow](../../plugins/LazyTuiHub.Plugin.TuiHub.Rss/Backend/RssModule.cs) · [Article storage](../../plugins/LazyTuiHub.Plugin.TuiHub.Articles/Backend/ArticleStore.cs)

## Workflows, execution plans, and run records

| Layer | Content and purpose |
| - | - |
| Logical workflow | Nodes, named ports, connections, and parameters declared by a plugin for a business operation, expressing intended processing relationships. |
| Resource information | Configured resources and availability conditions on execution instances, used for planning and binding; the detailed model remains open. |
| Execution plan | Combine the logical graph with current resources to determine placement, resource binding, transport paths, and synchronized dependencies, explaining how each step executes. |
| Run record | Track actual execution and attempts, progress, waiting, failure, and results; plan feasibility is not execution success. |

Generate a plan or diagnostics when creating a workflow. At execution, regenerate or confirm the plan against current resources rather than retaining stale availability assumptions. Plugins declare the resources their nodes need. The planner arranges the necessary transfers across devices.

The planner checks connectivity, port types, dependencies, resource bindings and transport feasibility. Plugins define the business meaning of a connection.

Validation reflects the resources available at planning time. During execution, the framework identifies the step waiting for input or blocked by a missing resource, unreachable device or node failure.

## Execution across public-network devices

In the MVP, one fixed primary builds and drives every execution plan and schedules all tasks. It installs all business plugins supported by the deployment and runs workflows locally. It delegates a step to a worker only when it cannot execute that step locally and the operation allows fallback. When the primary is offline, the system is unavailable. The account service independently manages identity and authorization. Devices register their resources separately. Each participating worker must have the same plugins and versions as the primary. The primary persists account-specific resource bindings across device outages. The target behavior and acceptance cases are described in [Account service and access control](2026-11-19-mvp-account-identity.en.md) · [Multi-node coordination](2026-11-19-mvp-coordination.en.md)

Devices can have different resources and network reachability. Across public networks, the planner matches named resources and exported packet operations. Capacity allocation, data retention and stream resumption remain open.

Hide necessary data movement from logical workflow authors while exposing waiting, transfer failure, and unknown write outcomes. A remote timeout does not establish that an operation never happened. Recovery or retry must follow node business semantics and existing protocol agreements. Synchronized ports provide logical dependencies and grouped delivery without requiring synchronized device clocks or physically simultaneous packet arrival.

Logical workflow nodes may declare extra named requirements in `requires`. A plugin can require a resource in its node descriptor. In the current Docker prototype each instance independently registers its resources. The coordinator uses configured peer addresses and credentials to query the authenticated inventory API. For each plan it matches the combined requirements to the inventories and execution modes it can reach. Workflow definitions carry no device identities or transfer steps.

Repeatable single-input, single-output stream transforms try the eligible local instance first and then workers ordered by instance ID. Reachability of an input can differ for every packet. The executor moves to the next candidate on an availability failure and stops on terminal rejection or cancellation. Remote dispatch transfers a typed value and its data reference. Local dispatch keeps the original object. The coordinator retains the graph, bounded queues, and synchronized groups.

An owner-bound packet operation requires one eligible owner of its declared resource. Missing or conflicting owners prevent planning. For `articles.store` the resource is `workspace.primary`. A coordinator may send a write to this owner. It sends one application-level attempt and reports an unknown outcome if it loses confirmation. The owner commits the article, its source reference, and their relation together. The Docker prototype relies on operators assigning one authoritative primary across the deployment. A reachable inventory snapshot cannot establish cluster-wide exclusivity during a partition.

## Explainability and limited replay

The framework should explain placement, resource binding, data movement, and dependencies in a plan, as well as what each running node is doing, which dependency it awaits, and why it failed. Run identity, actual attempts, and synchronization associations must relate to the workflow and plan rather than leaving only a whole-graph failure log.

Replay scope depends on retained inputs, configuration, node implementation, and state. Reproducing node processing also requires its consumed synchronized input groups and the input-consumption order that affected state. A global clock cannot reconstruct an ordering guarantee that does not exist between streams and synchronized inputs.

Fetching an external website again, consulting the current time, or reading other mutable external state does not replay the original input. Business nodes still determine write/replay semantics; recording a run does not establish exactly-once effects or deterministic results. Refine record structure, storage, retention scope, and recovery entry points for concrete implementation needs.

## Sources of inspiration

- [ComfyUI](https://docs.comfy.org/custom-nodes/backend/server_overview): use node declarations and freely connected ports as an interaction reference, adding continuous flow and explicit synchronized-delivery contracts.
- [Ray Data](https://docs.ray.io/en/latest/data/key-concepts.html#operators-and-plans): reference logical/physical plan and data-reference separation without adopting its cluster-oriented object store and scheduler.
- [Flink](https://nightlies.apache.org/flink/flink-docs-stable/docs/ops/monitoring/back_pressure/): reference continuous dataflow and backpressure without introducing its complete event-time, state-backend, or checkpoint system for the current requirement.

## Runnable implementation and next steps

Plugin nodes declare any number of named streaming or synchronized ports and one execution function through NodeRegistration. The host retains NodeRegistry for planning and execution. PipelineEdge identifies source node/port and target node/port. PipelinePlanner checks endpoints, strictly matching types and delivery modes, input producers, and the DAG required by the current backend. It produces a PipelinePlan binding node implementations, connections, and buffering capacity; PipelineRunner executes that plan. The plan includes each step’s resource requirements, candidates, and unreachable peer diagnostics.

Creating/saving a graph returns a plan or feasibility diagnostics. Saving retains definitions with missing implementations; execution requires a valid plan. Running a graph generates a fresh plan, and an already generated plan can also execute directly. The CLI plan command exposes placement, ports, and connections. --progress true reports running, synchronization waiting, group readiness, completion, and cancellation. Packet progress identifies the attempted instance, input reference, outcome, and cause. Run history is transient.

The Core plugin implements `builtin.timer` and `builtin.http-fetch`. `LazyTuiHub.Nodes` supplies their payload types and the `Source`, `Transform` and `Sink` helpers. Each helper registers an ordinary framework node. Consumers declare the concrete payload type, and unused outputs can remain unconnected.

`WorkflowRuntime` registers and stores graphs independently of RSS. The local and HTTP RSS operations use `rss.ingest` for refresh and SQLite for storage. Other plugins can consume parsing output or call their business services directly. The [storage Note](2026-11-17-mvp-storage-dao.en.md) covers TTL, FTS5, native connections and the limits of a workspace transaction.

The demo:workflow example demonstrates streams alongside incomplete synchronized inputs, grouped configuration updates, and cancellation. The node execution Note records current key pairing, completion, and retention choices. The Docker scenarios exercise asymmetric networks, per-source failover, resource withdrawal and addition, and remote writes to the primary. Dynamic membership during an active plan, cycles, cross-device synchronized ports, run history, and replay remain open.
