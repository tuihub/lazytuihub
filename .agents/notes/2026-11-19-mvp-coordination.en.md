---
status: proposed
category: architecture
---
# MVP fixed primary and task coordination

English | [中文](2026-11-19-mvp-coordination.zh.md)

## Responsibilities and current implementation

Deployment designates one primary. It hosts the isolated account service, stores system records, builds plans, dispatches work, and explains execution outcomes. System records include enrolled devices, account resources and their assigned owners, workflow definitions, plans, and execution attempts. The account service owns identity and credential records. Workers can supply all business storage, private-network access, parsers, and compute resources. [Account and access control Note](2026-11-19-mvp-account-identity.en.md)

The current coordinator queries peer inventories and calls workers directly. In account mode, it filters repeatable operations by device-job grants, checks the business URI for owner-bound writes, and sends the request's account scope to the worker. The current storage binding is the unique configured `workspace.primary` resource. Workflow validation uses the local registry for node implementations. In the target design the primary holds the contracts required for planning, including ports, transferable data types, resource requirements, and execution modes. Workers load plugin implementations and report the contracts they can execute. The primary can validate connections, select placements, and arrange transfers without loading the corresponding business DLLs. [Framework Note](2026-11-17-mvp-framework.en.md) · [Current workflow validation](../../src/LazyTuiHub.Plugins/WorkflowRuntime.cs) · [Current worker protocol](../../src/LazyTuiHub.Api/Protos/lazytuihub/execution/v1/worker_service.proto)

## From resource requirements to an execution plan

| Input | Supplied by | Planning decision |
| - | - | - |
| Logical graph, ports, and resource requirements | Workflow author and plugin contracts | Inputs, outputs, and capabilities used by each step |
| Effective URI permissions | Primary account service | Whether the acting principal and its current credentials allow those accesses |
| Resource bindings, device inventory, and availability | Primary and devices | Where bound resources are hosted and which devices export the required capabilities |

A client can submit a workflow under the user's identity. For a delegated submission, the recipient verifies the device and the user's grant. The primary records the acting principal, target account, and applicable grant on the run, and checks the URIs each step accesses under its node contract. New, retried, and fallback attempts use the permissions of that run. The workflow defines resource requirements so the plan can select devices and transport routes. [Primary authorization model](2026-11-19-mvp-account-identity.en.md)

The planner locates data services through resource bindings. For example, when the acting principal can write an article library URI, the planner follows the library's authoritative binding to the host that exports its storage service. Its hosting location informs placement. The article library URI determines access. An inventory claim that a device supplies SQLite informs matching. The primary controls changes to authoritative bindings.

When a step uses a network or compute capability supplied by a device, the capability forms part of the resources the step accesses. Candidate filtering checks the corresponding capability URI, using the same grant as direct use of that capability. The primary is eligible for resources it registers. Local preference and remote fallback choose among authorized candidates that can supply the required resources. If a bound resource's host goes offline, the plan retains the binding and explains why execution is unavailable.

## Polling, execution, and recovery

1. An enrolled worker uses its device identity over HTTPS to report resources and poll the fixed primary for work. The primary can hold an idle request and respond when work becomes available. The account service handles private-code polling during enrollment. Task polling requires an issued device identity. Standard control operations follow the repository's Proto and HTTP conventions. [API development guide](../../docs/development/api-design.en.md)
2. Before dispatch, the primary persists the attempt's account, run, step, target device, operation, and resource. Before delivery, it rechecks the target device identity, the URI permissions required by the attempt, and the resource binding. It then atomically records dispatch against the attempt ID and sends the assignment. Only a device acknowledgement establishes receipt. Concurrent polls cannot create a duplicate dispatch. A request already waiting for work undergoes the same checks before receiving its assignment.
3. The worker checks the target, operation, assignment's account scope, and local export rules. It runs the implementation and reports progress, result, or failure under its device identity and attempt ID. The primary accepts valid state transitions only from the device assigned to that attempt. A duplicate receipt yields the recorded outcome. A late receipt cannot overwrite a later attempt. Plan diagnostics distinguish pending dispatch, dispatched and awaiting a result, completed, failed, and outcome unknown.
4. Losing the polling connection changes reachability. On reconnect, the parties reconcile existing attempts first. An availability failure can trigger a new attempt on an eligible device for a repeatable operation. Once an owner-bound write might have been delivered, the primary retains its attempt and reconciles it against the owner's execution record or business transaction result. An unresolved write remains outcome unknown and must not be resent automatically. After restart, the primary uses its durable dispatch records for these decisions. Durable dispatch state alone cannot replay a graph or reconstruct stream contents.

Revoking device identity, device use, or a user's data access stops new claims and fallback attempts for the affected work and requests cooperative cancellation of dispatched work. While a device is unreachable, the primary preserves the possibility that an external write has committed. A revoked credential cannot submit receipts. An authorized principal reconciles unresolved writes against the owner's execution record or business data. Diagnostics distinguish permission failures, connectivity problems, and business rejections.

## Streaming assignments and relays

The first transport uses polling to claim work and ordinary HTTPS requests to return results. Each claim delivers a unit of planned work with an identity independent of the HTTP request. A future streaming assignment can identify an authorized endpoint and session once claimed. The worker then opens a persistent connection, such as a WebSocket. The receiving endpoint verifies the device identity and the session's account, attempt, endpoint, and stream direction. The connection can carry continuous data, backpressure, and cancellation. A reconnect belongs to the same attempt. Resume positions, buffering, and the wire protocol remain open until the streaming implementation.

Device identity, assignment target, and network route have separate representations. Workers connect directly to the primary in the first implementation. Later C may establish an authenticated, encrypted connection to the primary through relay B. B forwards traffic. The plan and receipts still name C. If B also executes work, it claims its own assignments using its own device identity. Losing a relay does not transfer a task or dataset to that relay.

GitLab Runner informs the polling model, HTCondor the separation of job requirements and resource offers, and Syncthing the preservation of endpoint identity across a relay. The fixed primary retains planning and authorization authority. [GitLab long polling](https://docs.gitlab.com/ci/runners/long_polling/) · [HTCondor ClassAds](https://htcondor.readthedocs.io/en/latest/classads/classad-mechanism.html) · [Syncthing relaying](https://docs.syncthing.net/users/relaying.html)

## Acceptance for the next increment

- Primary M runs only infrastructure services, including accounts and coordination. B can reach a private RSS feed. C hosts Bob's article library. Bob submits the same logical graph with permission to use B and write the article library. Verify that B and C claim their own tasks, the source observes B's address, and C stores the article and origin relation. Access is based on the library's own URI, and Bob's grants can omit general job access to C.
- Without permission to use B, both planning and fallback exclude it. If the grant is revoked before delivery, a waiting poll does not receive the assignment. Diagnostics distinguish insufficient permission, a missing resource, and an offline host.
- Let C commit a write and lose its receipt, then restart M and reconnect C. The primary accepts C's report for the original attempt without issuing another write. When evidence is missing the outcome stays unknown. Runtime validation of persistent data connections and relays belongs to those later implementations.
