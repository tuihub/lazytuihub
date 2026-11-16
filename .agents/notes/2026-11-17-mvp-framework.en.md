---
status: proposed
category: architecture
---
# MVP framework responsibilities and execution plans

English | [中文](2026-11-17-mvp-framework.zh.md)

## Scope and maintenance

This Note records the framework direction. The [node execution Note](2026-11-17-mvp-node-execution.en.md) separately maintains ports, synchronized delivery, backpressure, and node state. Resource modeling will be discussed later; this proposal does not establish a resource taxonomy or scheduling policy.

Under the [MVP maintenance agreement](../../AGENTS.md), all MVP-specific Notes remain proposed until the MVP merges into main. Their content and organization may change directly with discussion and implementation, without freezing partially implemented decisions. Prototype code, graph definitions, and database schemas have no compatibility commitment. The goal on feat/mvp is a clean, coherent result. Each commit combines a runnable code increment and corresponding Note changes: carefully implement settled contracts and use explicit, replaceable simple behavior for unsettled details, refining it through execution.

This Note maintains both the target design and the first local implementation. Named ports, synchronized-input alignment, and local execution plans now exist; resource scheduling, remote execution, and limited replay remain design goals. The [running guide](../../docs/development/streaming-mvp.en.md) describes executable behavior. Existing protocol and repository agreements remain effective.

## Goals and application boundaries

The application is a client/server monolith with inexpensive in-process calls and conventional HTTP communication across processes and devices. Future WebSocket support retains a separate adaptation boundary. Follow the [protocol baseline](2026-11-15-protobuf-api-contract.en.md): C# implements business logic while generation covers contracts and communication code. Internal workflow ports do not thereby all become public APIs.

The MVP uses an RSS reader and SQLite to explore framework/business separation. The framework lets modules connect and reuse registered functionality and data rather than establishing an RSS-specific executor. Subscriptions, fetching, parsing, storage, reading, and read state belong to RSS; generic data relationships, workflow structure, execution plans, and execution driving belong to the framework.

Workflows are optional. Business services support direct calls and may also participate through node adapters. Both paths reuse business implementations without requiring every API or simple query to traverse a workflow. Reading pipelines remain useful without becoming a prerequisite for reading functionality.

## Core and node libraries

| Part | Responsibility |
| - | - |
| Business modules | Own domain data and constraints, implement functionality, and register reusable nodes and ports. |
| Framework core | Maintain generic data relationships, node/connection declarations and resource information; generate and drive plans; provide transport, synchronized dependencies, fan-out, backpressure, and execution explanations. |
| Built-in node library | Supply timers, HTTP fetch, and other common components, with conversion or signal helpers when needed, using the same registration mechanism as business modules. |
| Host and transport adapters | Compose modules, expose CLI and protocol entry points, and call business services or workflow entry points. |

Separate timers and HTTP fetch from core execution rules. The core does not infer business behavior from node names, RSS types, control values, or an idempotency flag. Business implementations and their inputs determine idempotency, deduplication, recomputation, and side effects; the core does not turn a boolean into a universal retry policy.

## Data relationships and processing relationships

Retain two independent generic relationship models: associations between data objects, and processing connections between node ports. Data associations help modules reference or discover other modules' data; processing connections describe movement through functionality. Modules retain business fields and invariants rather than flattening all business tables into a universal document model.

Persistent object identity, runtime payload references, and synchronized-input messagekeys serve different purposes and are not implicitly interchangeable. In-process execution may share object references under a read-only agreement and produce new results when transforming data. Processes and devices need transferable representations or resolvable references rather than CLR pointers. The current DataReference identifies data; it is not an implemented distributed object-store reference.

An RSS fetch can connect timer, HTTP fetch, RSS parsing, and storage nodes. Another business module can consume parsing output without changing RSS business implementation or the core runner.

## Workflows, execution plans, and run records

| Layer | Content and purpose |
| - | - |
| Logical workflow | Nodes, named ports, connections, and parameters defined by a user or module, expressing intended processing relationships. |
| Resource information | Configured resources and availability conditions on execution instances, used for planning and binding; the detailed model remains open. |
| Execution plan | Combine the logical graph with current resources to determine placement, resource binding, transport paths, and synchronized dependencies, explaining how each step executes. |
| Run record | Track actual execution and attempts, progress, waiting, failure, and results; plan feasibility is not execution success. |

Generate a plan or diagnostics when creating a workflow. At execution, regenerate or confirm the plan against current resources rather than retaining stale availability assumptions. Users need not insert every cross-device transfer into logical graphs; plans expose and arrange necessary data movement.

Connection checking follows declared endpoints and matching types, without interpreting business meaning. Planning additionally handles structural dependencies, resource binding, and transport feasibility rather than guessing business compatibility from names.

The framework drives validated workflows. It advances while execution conditions hold and explains concrete reasons when progress is impossible, such as missing resources, disconnected instances, incomplete inputs, or node failure. Validation describes feasibility at that point in time, not permanently available resources or networks; waiting must remain distinguishable from success.

## Execution across public-network devices

Future execution instances may have different resources and network reachability and cooperate over the public internet. Do not assume a low-latency, stable computing cluster. Resource configuration and rules may inform automatic placement, but resource descriptions, selection algorithms, data retention, and reconnect protocols remain undecided.

Hide necessary data movement from logical workflow authors while exposing waiting, transfer failure, and unknown write outcomes. A remote timeout does not establish that an operation never happened. Recovery or retry must follow node business semantics and existing protocol agreements. Synchronized ports provide logical dependencies and grouped delivery without requiring synchronized device clocks or physically simultaneous packet arrival.

Use local execution to establish these boundaries in the MVP rather than pre-building complete distributed scheduling, consensus, checkpoints, or cross-device transactions.

## Explainability and limited replay

The framework should explain placement, resource binding, data movement, and dependencies in a plan, as well as what each running node is doing, which dependency it awaits, and why it failed. Run identity, actual attempts, and synchronization associations must relate to the workflow and plan rather than leaving only a whole-graph failure log.

Replay scope depends on retained inputs, configuration, node implementation, and state. Reproducing node processing also requires its consumed synchronized input groups and the input-consumption order that affected state. A global clock cannot reconstruct an ordering guarantee that does not exist between streams and synchronized inputs.

Fetching an external website again, consulting the current time, or reading other mutable external state does not replay the original input. Business nodes still determine write/replay semantics; recording a run does not establish exactly-once effects or deterministic results. Refine record structure, storage, retention scope, and recovery entry points for concrete implementation needs.

## Sources of inspiration

- [ComfyUI](https://docs.comfy.org/custom-nodes/backend/server_overview): use node declarations and freely connected ports as an interaction reference, adding continuous flow and explicit synchronized-delivery contracts.
- [Ray Data](https://docs.ray.io/en/latest/data/key-concepts.html#operators-and-plans): reference logical/physical plan and data-reference separation without adopting its cluster-oriented object store and scheduler.
- [Flink](https://nightlies.apache.org/flink/flink-docs-stable/docs/ops/monitoring/back_pressure/): reference continuous dataflow and backpressure without introducing its complete event-time, state-backend, or checkpoint system for the current requirement.

These references inform design rather than select dependencies or promise feature equivalence. The node execution Note owns synchronized-delivery semantics.

## First local implementation and next steps

NodeRegistry registers any number of named streaming/synchronized ports with one execution function model. PipelineEdge identifies source node/port and target node/port. PipelinePlanner checks endpoints, strictly matching types and delivery modes, input producers, and the DAG required by the current backend. It produces a PipelinePlan binding node implementations, connections, and buffering capacity; PipelineRunner executes that plan. Every step currently runs on the local instance without invented resource inventories or remote paths.

Creating/saving a graph generates a plan for checking; running a graph generates a fresh plan, and an already generated plan can also execute directly. The CLI plan command exposes local placement, ports, and connections. --progress true reports running, synchronization waiting, group readiness, completion, and cancellation. Errors identify the node and cause; run history is not yet persisted.

Common timers, HTTP fetch, related payloads, and stream registration helpers now live in the separate LazyTuiHub.Nodes library, which the core does not reference. Source, Transform, and Sink are library conveniences for common single-stream patterns, all registering the same core node model. Output consumers register concrete payload types rather than relying on object assignability. Outputs may remain unconnected, allowing modules to declare branches that a particular graph does not use.

RSS now uses the port/plan model while retaining local/HTTP behavior, SQLite workspaces, and the cross-business branch after parsing. Direct business operations still traverse workflows; removing that mandatory dependency remains a later increment. SQLite remains the workspace store, without a new schema standard or migration layer for old graphs/schemas.

The demo:workflow example demonstrates streams alongside incomplete synchronized inputs, grouped configuration updates, and cancellation. The node execution Note records current key pairing, completion, and retention choices. Resource modeling, graph cycles/feedback, cross-device protocols, run-record storage, and limited-replay entry points need refinement and are neither implemented capabilities nor additional compatibility commitments.
