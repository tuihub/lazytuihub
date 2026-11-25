---
status: proposed
category: architecture
---
# MVP node ports, synchronized delivery, and state

English | [中文](2026-11-17-mvp-node-execution.zh.md)

## Scope and status

The runner exposes the [framework’s](2026-11-17-mvp-framework.en.md) port and synchronization contracts. [WorkflowDemo](../../src/LazyTuiHub/Examples/WorkflowDemo.cs) exercises both delivery modes in a single node.

## Node and port declarations

Modules declare node names, any number of named inputs/outputs, port types, and execution functions. Each port category may have zero members, with no fixed port-count limit. Source, transformation, and consumption behavior follows port combinations rather than requiring three separate node execution mechanisms.

Both inputs and outputs support streaming or synchronized delivery. A plugin may use synchronized data for control signals, configuration, computed values or object references. The plugin interprets each value.

A connection requires matching data types and delivery modes. The planner uses these declarations to arrange transport and dependencies. Plugins define conversions, combinations, deduplication and retry semantics.

| Contract | Streaming port | Synchronized port |
| - | - | - |
| Delivery | Deliver available data promptly, subject to backpressure and resource conditions. | Satisfy dependencies and align required inputs by message key for grouped delivery. |
| Ordering | No business processing order or ordering across different streams is promised. | Honor declared dependencies within one associated computation. |
| Reading | Nodes decide how to consume their inputs. | Nodes obtain the complete synchronized input group in one read. |
| Main cost | Transport, queues, and buffering. | Additional alignment waits and retention of incomplete inputs. |

FIFO applies within the local queue. Delivery time depends on available resources and backpressure.

## Synchronized delivery

A message key identifies one computation. The node declares which inputs participate. The framework waits for all of them to supply a value with the same key and delivers those values in one read.

```text
A(k) arrives -> framework waits
B(k) arrives -> node obtains { A(k), B(k) } in one read
```

Simultaneous arrival means logically grouped delivery as observed by the node, not packets reaching threads or devices at the same physical instant. Writing into several independent queues does not establish this contract; port wrappers must provide grouped reading.

Each successor waits for its own declared dependencies. Independent nodes and different keys can advance separately. A plugin that needs idempotency supplies its own business key. For repeated keys, the runtime pairs values in receipt order at each port. The plugin owns any association between business rounds or across producers.

Synchronized dependencies do not implicitly extend to streaming ports. Sending configuration first does not establish which stream message uses it, and a completion signal does not prove that another stream has been consumed. Nodes express required relationships by binding configuration references or publishing synchronized outputs after the relevant work finishes. The core does not infer stream/synchronized timing from names.

When a participating input produces no result, distinguish waiting, explicit absence, and failure. The current implementation represents incomplete input by waiting or failure after producer completion; a generic absence-of-result representation is not yet implemented. Do not substitute defaults for inputs that have not arrived. Execution explanations identify the awaited port and key. Cancellation or actual failure must end the corresponding wait, without arbitrary timeouts or silent-drop policies.

## Continuous execution and state updates

C# can use one long-running asynchronous function to process both input categories continuously, waking for stream data or a complete synchronized group. The framework supplies typed reading/writing and multiple-input waiting wrappers; nodes decide consumption, combination, publication, and state changes. Synchronization describes visibility and dependencies while waits remain asynchronous rather than blocking threads.

Start with one logical execution flow owning node state, illustrated below:

```csharp
public async Task ExecuteAsync(NodeContext context, CancellationToken ct)
{
    var options = (Factor: 1, Offset: 0);
    await foreach (var input in context.ReadEventsAsync())
    {
        if (input is SynchronizedInput sync)
            options = (sync.Get<int>("factor"), sync.Get<int>("offset"));
        else if (input is StreamInput data)
        {
            var snapshot = options;
            await context.WriteAsync("result", data.Get<int>() * snapshot.Factor + snapshot.Offset, ct);
        }
    }
}
```

SynchronizedInput contains the complete factor/offset group; StreamInput retains port identity and payload. ReadEventsAsync uses the cancellation token bound by the runner, without starting and abandoning competing reads or introducing cross-category business ordering guarantees.

Replacing local state applies configuration updates to subsequently started processing. After one operation takes an immutable snapshot, updates arriving during processing and output remain pending until consumed by the loop, then affect later messages. A node may supply initial defaults or wait for initial configuration before consuming data.

To affect work already in progress, nodes explicitly choose finer application points, such as reading state for each block or exiting cooperatively through cancellation. Parallel stream processing can use immutable snapshots and explicit shared-state coordination; it is not a mandatory default core policy. Nodes own state scope, configuration switching, signal edges, and the business meaning of repeated pulses. General helpers may live in the node library.

Actual input-consumption order determines these local state changes; sending time does not determine which configuration applies to a stream message. Strict binding uses explicit associations or configuration references. Replay retains consumed inputs and necessary order, as described in the framework Note.

## Transport, fan-out, and backpressure

The local runner implements asynchronous ports with [.NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels). Each node reads from its own inbox. The runner delivers an output to each connected branch so consumers of different branches do not compete for a single message.

Local branches may share the same payload reference under a read-only agreement. Full buffers wait asynchronously and propagate backpressure rather than silently dropping data to appear prompt. Incomplete synchronized inputs also consume resources. Choose later budgets from actual costs rather than fixing counts, sizes, or retention periods here. Slow-branch isolation remains subject to concrete needs.

Multiple-input waiting should wrap readiness checks or manage pending reads correctly. Repeatedly starting reads and abandoning losing tasks can let unobserved reads consume messages. Port wrappers must also propagate cancellation, input completion, and errors rather than only successful payloads.

Shared references reduce local transport cost without establishing cross-device shared memory. Remote transport must preserve synchronized-group identity and completeness and cannot derive synchronization from network arrival time. Its protocol will be designed separately.

## Runtime behavior and verification

All synchronized inputs declared on a node participate in one group, and every input must have a producer. Synchronized outputs explicitly supply a messageKey, matched as the original string. The core does not generate keys or derive them from persistent object identity; nodes explicitly forward them when association must continue. Each key maintains a FIFO for each port and takes one value from each for a group. Repeated keys may produce successive groups without automatic deduplication. Multiple producers on one port pair by actual receipt order; different keys advance by readiness without global ordering.

A port closes after all its producers finish. If an incomplete group lacks a closed port, fail with the node, key, and missing ports immediately, even while other streams remain open. Inputs that can still arrive remain pending; cancellation interrupts queue operations and cooperative node waits. A returning node stops accepting input, so subsequent upstream writes fail with a closed-input error rather than hanging on an abandoned full queue. Normal consumers read to completion or explicitly arrange cancellation.

Each bounded node ingress uses the graph's bufferCapacity, retaining the default of 16; full buffers wait. Fan-out currently delivers sequentially, so slow branches backpressure upstream. Incomplete synchronized values move to an in-memory pending table outside ingress capacity. There is no pending-count or process-wide byte budget and no spill-to-disk implementation. Bounded ingress must not be described as bounded overall memory; resource budgets and slow-branch isolation remain later work.

The runner reports running, waiting-synchronization, synchronized, completed, and canceled events; waits include the key and missing ports. These are local progress callbacks without a durable replay log. demo:workflow uses explicit acknowledgments to coordinate example inputs, showing initial parameters while synchronization is incomplete, then grouped updates producing 2, 7, 13 before canceling all tasks. Example coordination establishes no new framework ordering guarantee between stream and synchronized input.

Product tests exercise interleaved and repeated keys, incomplete groups on port closure or cancellation, port name/type/mode mismatches and early consumer exit. They also cover fan-out, backpressure, fan-in, failure cancellation, RSS and HTTP.

When build and test tasks run together, mise uses [wait_for](https://mise.jdx.dev/tasks/task-configuration.html#wait-for) to order writes to shared .NET outputs. Running a test or demo on its own adds no build-task prerequisite.

## Remaining boundaries

The MVP supports streams and synchronized delivery by key. Batch execution, stage materialization, batch completion barriers, windows and watermarks remain outside its scope.

Further group declarations, automatic key propagation, business association of rounds/one-to-many outputs, absence-of-result representation, and dependency cycles/feedback need refinement. Current simple choices are not permanent contracts. The state example establishes no concurrency, priority, or fairness guarantees. Resources and remote recovery continue under the framework Note's boundaries.
