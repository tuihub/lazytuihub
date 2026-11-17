---
status: proposed
category: architecture
---
# MVP UI framework selection and desktop integration

English | [中文](2026-11-17-mvp-ui-comparison.zh.md)

## Scope and decision

On 2026-11-18, the user selected Avalonia as the product UI framework after the source/ecosystem investigation and native prototype comparison. Subsequent product UI development uses Avalonia; Uno remains only in the retained comparison prototypes. This Note stays proposed under the [MVP agreement](../../AGENTS.md), which keeps all MVP Notes editable until merge into main. That lifecycle status does not leave framework selection undecided.

The comparison established the reader, streaming-workflow canvas interaction, and DLL-owned view mechanisms needed for the next increment. Avalonia's observed macOS input, accessibility-tree continuity, and native menu behavior support the choice. Uno's unresolved observations remain scoped to this prototype and tool; adopting Avalonia does not require proving those observations are framework defects.

The [comparison guide](../../docs/development/ui-comparison.en.md) provides commands, source boundaries, test scope, and matched interactive operations. Code lives in `.agents/prototypes/ui-comparison/`, outside the product solution and default CI. The optional `check:ui` builds with analyzer warnings as errors and runs official headless hosts. Existing product gates remain unchanged; the prototype exempts literal layout dimensions from the magic-number rule.

## Comparison prototype implementation

Both implementations share a three-pane RSS reader fixture with 600 bilingual articles, reading state, graph positions, and a typed page-plugin contract. That count is a comparison workload, not an application limit. Framework-specific controls implement wrapping toolbars, page fades, light/dark themes, a draggable/zoomable five-node canvas, and independent managed DLL pages. The graph illustrates connections without executing product workflows.

Hosts build plugins with `ReferenceOutputAssembly="false"`, copy the entry DLL to `plugins/SamplePlugin.dll`, and cold-load it through the default assembly context with shared framework/contract types. Loaded views retain navigation state. Missing/disabled implementations keep the reader and diagnostics available. There is no hot unload or private dependency-version solver.

This experimental UI contract does not change the [product plugin contract](2026-11-17-mvp-plugins.en.md). Fixture data remains process-local; no product workspace is read or changed. SQLite, product routes, persistence, and a complete graphical workflow editor remain outside this comparison. Versions live in prototype build configuration rather than replacing the repository SDK baseline.

## Observed evidence

On 2026-11-17, Release builds and automated checks passed on macOS ARM64 with .NET 11.0 for Avalonia 12.1.6/Labs.Panels 12.1.0 and Uno 7.0.65/Toolkit 11.0.1. Uno also ran with Cupertino 9.0.1. Checks cover initial reader layout, search, viewport-based list realization, shared feed/read-state behavior, DLL-owned view creation, disabled-plugin startup, missing-DLL diagnostics, Flex wrapping/growth, and graph/zoom state.

At the fixed headless viewport, Avalonia realized 13 list containers and Uno Fluent 24 out of 600 items. This establishes that neither eagerly created every container in the fixture; buffering differences prevent interpreting the counts as a performance ranking. Uno requires XAML resource initialization and asynchronous search-event handling. Avalonia provides a layout-transform wrapper; the Uno prototype adjusts the scroll extent around a render transform.

Avalonia also has native Reader/Workflow menu code; Uno currently has in-window navigation only. This is an implementation difference, not proof that Uno lacks native menu integration. Compilation/API mistakes and download failures are not framework defects.

## Native comparison and remaining scope

A subsequent native macOS pass exercised both renderers with matched logical sizes. Uno's AppWindow physical-pixel sizing now uses XamlRoot.RasterizationScale, correcting the Retina mismatch in the prototype. Both applications actually supported filtering/search, read state, list scrolling, DLL-owned views with retained counters, node dragging before/after 115% zoom, reset, content theme changes, and wrapping toolbars around 500 logical units wide. Avalonia's native View menu actually switched to Workflow.

Two fresh Uno Fluent processes reproduced Return not inserting a newline and Command+F not focusing search from notes. Workflow → + also reduced the tool-reported accessibility tree to an unknown node while visual and pointer behavior remained available; Avalonia retained its tree. These are current prototype/tool/runtime observations, requiring independent input and accessibility verification before assigning root cause to the framework. They are not claims that Uno lacks multiline controls, shortcuts, or accessibility altogether.

The Cupertino variant explicitly exports implicit styles; the earlier resource-only setup had not applied them. Native captures now confirm changed basic controls and typography with mixed list styling. This does not provide a complete macOS shell. Its captured edge black area and Clip click without an observed counter update remain open layout/input/capture observations rather than a completed native interaction pass. The guide records exact scope and reproduction steps.

Chinese rendering/paste worked, but real input-method composition/candidate placement and GPU frame pacing were not established. Android, Windows, legacy Windows and Linux were not exercised. Tool timeouts or early coordinate errors are excluded from framework judgments. Headless Loaded timing, working-set snapshots and container counts remain diagnostic, not native first-frame time, peak memory, frame rate, or installation size.

## Product desktop integration

The first product increment now uses Avalonia 12.1.6 in the main solution. The [desktop guide](../../docs/development/desktop-mvp.en.md) provides commands and scope. `mise run run:desktop` starts a generic .NET host and the Avalonia window in one process; the existing CLI/web entry remains available. Shared composition supplies the same plugin runtime, services, SQLite workspace, data-protection keys, and background tasks. Desktop activation does not map HTTP endpoints or start a listener. Service work runs off the UI thread, and local reader calls use IRssApi directly.

LazyTuiHub.Desktop owns the shell, lazy page composition, theme switching, and workflow/plugin inspectors. LazyTuiHub.Rss.Desktop is a separate dynamically loaded plugin, `rss.desktop`, with a hard dependency on `rss`; it owns reader controls/state but references the shared API contract rather than RSS implementation types. Business services, workflow execution, DAO access, and plugin metadata remain independent of Avalonia. The host builds the UI DLL as a distribution artifact without a runtime implementation reference.

PluginRegistration now accepts plain DeclaredPage identity/title records. The framework persists them with plugin ownership, including before activation, so disabled, missing, and blocked plugins remain diagnosable. AddDesktopPage adds the declaration and a keyed control factory; the shell invokes only active plugins' factories on the UI thread. Views are created on first navigation and retained for the process. The shared assembly baseline preserves Avalonia and contract type identity. This is a provisional page-level extension, not a general navigation or plugin-dependency platform.

The reader creates subscriptions, fetches a selected feed, lists articles with API pagination, filters read state, and marks articles read. It uses real RSS workflows and SQLite; it does not copy fixture state into the product. Load more advances the next-page token without a UI total-count cap. Subscription lists consume all API pages. Content is selectable text, including source markup; HTML rendering, external-link navigation, full-text search, and marking an article unread are not implemented by this UI. Business API behavior remains unchanged.

Workflows displays saved definitions, current plans, and unavailable-node reasons. It does not edit the graph or submit arbitrary executions. Plugins displays saved intent separately from current activation and can change enablement for the next startup. Disabling the UI alone leaves RSS services/tasks available; disabling or losing RSS blocks its dependent UI. Missing UI code retains page declarations and data, and framework diagnostics stay available. Existing managed retention continues under its own agreement.

Reader operations are serialized within a page, with visible errors and busy state. Window closing first cancels and awaits tracked UI operations, then stops hosted tasks before host disposal. Already committed writes survive cancellation. This establishes cooperative shutdown, not durable replay or lossless suspension. Selection, filters, and theme overrides remain process-local.

## Product verification and remaining work

Product checks and CI now include the desktop projects and real-service headless tests. Tests cover a multi-page feed, read-state persistence, input errors, navigation, disabled/missing/restored UI code, unavailable RSS dependencies, no desktop HTTP server, task shutdown, and in-flight UI cancellation. A native macOS ARM64 pass on 2026-11-18 additionally exercised subscription/fetch/read/filter, plan inspection, plugin disable/restore across restarts, retained data, theme switching, and closing the application. Native inspection led to readable article/plugin accessibility names instead of object dumps.

The UI projects retain product analyzer rules. They suppress only AVLN3001 for constructor-injected views initialized through compiled XAML: the unused resource-URI construction path cannot call their constructors. The [official compiler implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.6/src/Avalonia.Build.Tasks/XamlCompilerTaskExecutor.cs) explains this diagnostic; no placeholder services or parameterless constructors are added to satisfy it. Headless tests use the official session's asynchronous disposal.

Operating-system-specific design systems remain an application design task. Fluent is the initial working theme. Remote-server selection, richer responsive layouts, a visual workflow editor, HTML rendering, installers, and Android/Windows 7/XP support remain open. The framework choice does not establish a tested platform support matrix. IME composition, complete assistive-technology behavior, and GPU performance still need validation. Uno remains in the opt-in comparison artifacts and introduces no second product renderer or cross-renderer abstraction.
