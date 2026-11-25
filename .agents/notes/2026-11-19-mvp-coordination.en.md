---
status: proposed
category: architecture
---
# MVP fixed primary and task coordination

English | [中文](2026-11-19-mvp-coordination.zh.md)

## Responsibilities and current implementation

One fixed primary builds and drives all execution plans and schedules every task. An offline primary makes the system unavailable. Its installed plugins define the deployment’s business operations and provide their workflows and node implementations. When it has the resources and permissions, the primary executes a whole operation locally. It hosts the isolated account service and stores device enrollment, account resource bindings, workflows, plans and attempts. The account service owns credential records. [Account and access control](2026-11-19-mvp-account-identity.en.md)

In the MVP the primary must verify every worker’s installed plugin inventory before treating it as eligible for task execution. The set of plugin IDs and the version of each plugin must match the primary exactly. The primary reports missing plugins, extra plugins and version differences and excludes the affected worker from dispatch. This check applies to server devices participating in execution. A client connecting directly to a remote service continues to use its user identity. Each device independently advertises its available resources and executable operations.

The prototype already validates workflows using node implementations from the local registry. The coordinator queries peer inventories and calls workers directly. In account mode it filters repeatable operations by device-job grants, checks the business URI for owner-bound writes and sends the request’s account scope to the worker. The storage binding is the unique configured `workspace.primary` resource. The current protocol reports resources and exported operations. Cross-device plugin inventory validation and worker polling remain to be implemented. [Framework Note](2026-11-17-mvp-framework.en.md) · [Current workflow validation](../../src/LazyTuiHub.Plugins/Workflows/WorkflowRuntime.cs) · [Current worker protocol](../../src/LazyTuiHub.Api/Protos/lazytuihub/execution/v1/worker_service.proto)

## From resource requirements to an execution plan

| Input | Supplied by | Planning decision |
| - | - | - |
| Logical graph, ports, and resource requirements | The plugin defining the workflow and the participating node contracts | Inputs, outputs, and capabilities used by each step |
| Effective URI permissions | Primary account service | Whether the acting principal and its current credentials allow those accesses |
| Resource bindings, device inventory, and availability | Primary and devices | Where bound resources are hosted and which devices export the required capabilities |

A user starts a business operation through the client with a user session. The plugin defines any workflow the operation needs, and the coordinator dispatches its steps under the authorization for that call. When a device initiates an operation on the user’s behalf, the recipient verifies its device identity and the user’s grant to that device. The primary records the acting principal, target account, and applicable grant on the run, and checks the URIs each step accesses under its node contract. New, retried, and fallback attempts use the permissions of that run. The workflow defines resource requirements so the plan can select devices and transport routes. [Primary authorization model](2026-11-19-mvp-account-identity.en.md)

The planner locates data services through resource bindings. For example, when the acting principal can write an article library URI, the planner follows the library's authoritative binding to the host that exports its storage service. Its hosting location informs placement. The article library URI determines access. An inventory claim that a device supplies SQLite informs matching. The primary controls changes to authoritative bindings.

When a step uses a network or compute capability supplied by a device, the capability forms part of the resources the step accesses. Candidate filtering checks the corresponding capability URI, using the same grant as direct use of that capability. The primary runs each step locally whenever it can. If a resource registration or authoritative binding already rules out local execution, the primary can assign the step directly to an eligible worker. If local execution encounters an availability failure, such as an unreachable source, the primary can delegate only when the operation permits a retry. Every assignment respects the run’s permissions and resource bindings. Workers execute only their assigned steps. If a bound resource's host goes offline, the plan retains the binding and explains why execution is unavailable.

## Polling, execution, and recovery

1. An enrolled worker uses its device identity over HTTPS to report resources and poll the fixed primary for work. The primary can hold an idle request and respond when work becomes available. The account service handles private-code polling during enrollment. Task polling requires an issued device identity. Standard control operations follow the repository's Proto and HTTP conventions. [API development guide](../../docs/development/api-design.en.md)
2. Before dispatch, the primary persists the attempt's account, run, step, target device, operation, and resource. Before delivery, it rechecks the target device identity, the URI permissions required by the attempt, and the resource binding. It then atomically records dispatch against the attempt ID and sends the assignment. Only a device acknowledgement establishes receipt. Concurrent polls cannot create a duplicate dispatch. A request already waiting for work undergoes the same checks before receiving its assignment.
3. The worker checks the target, operation, assignment's account scope, and local export rules. It runs the implementation and reports progress, result, or failure under its device identity and attempt ID. The primary accepts valid state transitions only from the device assigned to that attempt. A duplicate receipt yields the recorded outcome. A late receipt cannot overwrite a later attempt. Plan diagnostics distinguish pending dispatch, dispatched and awaiting a result, completed, failed, and outcome unknown.
4. Losing the polling connection changes reachability. On reconnect, the parties reconcile existing attempts first. After an availability failure, the primary can create a new attempt on an eligible device for a repeatable operation. Once an owner-bound write might have been delivered, the primary retains its attempt and reconciles it against the owner's execution record or business transaction result. An unresolved write remains outcome unknown and must not be resent automatically. After restart, the primary uses its durable dispatch records for these decisions. Durable dispatch state alone cannot replay a graph or reconstruct stream contents.

Revoking device identity, device use, or a user's data access stops new claims and fallback attempts for the affected work and requests cooperative cancellation of dispatched work. While a device is unreachable, the primary preserves the possibility that an external write has committed. A revoked credential cannot submit receipts. An authorized principal reconciles unresolved writes against the owner's execution record or business data. Diagnostics distinguish permission failures, connectivity problems, and business rejections.

## Streaming assignments and relays

The first transport uses polling to claim work and ordinary HTTPS requests to return results. Each claim delivers a unit of planned work with an identity independent of the HTTP request. A future streaming assignment can identify an authorized endpoint and session once claimed. The worker then opens a persistent connection, such as a WebSocket. The receiving endpoint verifies the device identity and the session's account, attempt, endpoint, and stream direction. The connection can carry continuous data, backpressure, and cancellation. A reconnect belongs to the same attempt. Resume positions, buffering, and the wire protocol remain open until the streaming implementation.

Device identity, assignment target, and network route have separate representations. Workers connect directly to the primary in the first implementation. Later C may establish an authenticated, encrypted connection to the primary through relay B. B forwards traffic. The plan and receipts still name C. If B also executes work, it claims its own assignments using its own device identity. Losing a relay does not transfer a task or dataset to that relay.

GitLab Runner informs the polling model, HTCondor the separation of job requirements and resource offers, and Syncthing the preservation of endpoint identity across a relay. The fixed primary retains planning and authorization authority. [GitLab long polling](https://docs.gitlab.com/ci/runners/long_polling/) · [HTCondor ClassAds](https://htcondor.readthedocs.io/en/latest/classads/classad-mechanism.html) · [Syncthing relaying](https://docs.syncthing.net/users/relaying.html)

## Coordination acceptance cases

- Refresh Bob’s feed on M. When M has the required resources and permissions, verify that it fetches, parses and stores the articles locally even with an eligible worker idle.
- Install the same plugins on M, B and C. Switch to an RSS source reachable only from B and bind Bob’s article library to C. Bob requests the same refresh. Verify that M builds and drives the entire plan. When M cannot reach the source, it delegates the fetch to B. M parses the result locally and assigns the write to C according to the library binding. C stores the articles and source relations. Access follows the library URI and Bob can omit general job access to C.
- Exercise a worker with a missing plugin, an extra plugin and a different plugin version. Verify that the primary identifies each difference and withholds work from that device. Once the inventories match again, the worker can participate according to its available resources.
- Without permission to use B, both planning and fallback exclude it. If the grant is revoked before delivery, a waiting poll does not receive the assignment. Diagnostics distinguish insufficient permission, a missing resource, and an offline host.
- Let C commit a write and lose its receipt, then restart M and reconnect C. The primary accepts C's report for the original attempt without issuing another write. When evidence is missing the outcome stays unknown. Runtime validation of persistent data connections and relays belongs to those later implementations.
