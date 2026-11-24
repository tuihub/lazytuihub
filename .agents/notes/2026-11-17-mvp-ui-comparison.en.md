---
status: proposed
category: architecture
---
# MVP UI framework selection and desktop integration

English | [中文](2026-11-17-mvp-ui-comparison.zh.md)

## Scope and decision

On 2026-11-18, the user selected Avalonia as the product UI framework after the source/ecosystem investigation and native prototype comparison. Subsequent product UI development uses Avalonia; Uno remains only in the retained comparison prototypes. This Note stays proposed under the [MVP agreement](../../AGENTS.md), which keeps all MVP Notes editable until merge into main. Avalonia is the current implementation choice and can be replaced under that agreement; neither the selection nor the existing code constrains the final MVP.

The comparison established the reader, streaming-workflow canvas interaction, and DLL-owned view mechanisms needed for the next increment. Avalonia's observed macOS input, accessibility-tree continuity, and native menu behavior support the choice. Uno's unresolved observations remain scoped to this prototype and tool; adopting Avalonia does not require proving those observations are framework defects.

The [comparison guide](../../docs/development/ui-comparison.en.md) provides commands, source boundaries, test scope, and matched interactive operations. Code lives in `.agents/prototypes/ui-comparison/`, outside the product solution and default CI. The optional `check:ui` builds with analyzer warnings as errors and runs official headless hosts. Existing product gates remain unchanged; the prototype exempts literal layout dimensions from the magic-number rule.

## Comparison prototype implementation

Both implementations share a three-pane RSS reader fixture with 600 bilingual articles, reading state, graph positions, and a typed page-plugin contract. That count is a comparison workload, not an application limit. Framework-specific controls implement wrapping toolbars, page fades, light/dark themes, a draggable/zoomable five-node canvas, and independent managed DLL pages. The graph illustrates connections without executing product workflows.

Hosts build plugins with `ReferenceOutputAssembly="false"`, copy the entry DLL to `plugins/SamplePlugin.dll`, and cold-load it through the default assembly context with shared framework/contract types. Loaded views retain navigation state. Missing/disabled implementations keep the reader and diagnostics available. There is no hot unload or private dependency-version solver.

This experimental UI contract does not change the [product plugin contract](2026-11-17-mvp-plugins.en.md). Fixture data remains process-local; no product workspace is read or changed. SQLite, product routes, persistence, and a complete graphical workflow editor remain outside this comparison. Versions live in prototype build configuration rather than replacing the repository SDK baseline.

## Observed evidence

On 2026-11-24, Release builds and automated checks passed on macOS ARM64 with .NET 10.0 for Avalonia 12.1.6/Labs.Panels 12.1.0 and Uno 7.0.65/Toolkit 11.0.1. Uno also ran with Cupertino 9.0.1. Checks cover initial reader layout, search, viewport-based list realization, shared feed/read-state behavior, DLL-owned view creation, disabled-plugin startup, missing-DLL diagnostics, Flex wrapping/growth, and graph/zoom state.

At the fixed headless viewport, Avalonia realized 13 list containers and Uno Fluent 24 out of 600 items. This establishes that neither eagerly created every container in the fixture; buffering differences prevent interpreting the counts as a performance ranking. Uno requires XAML resource initialization and asynchronous search-event handling. Avalonia provides a layout-transform wrapper; the Uno prototype adjusts the scroll extent around a render transform.

Avalonia also has native Reader/Workflow menu code; Uno currently has in-window navigation only. This is an implementation difference, not proof that Uno lacks native menu integration. Compilation/API mistakes and download failures are not framework defects.

## Native comparison and remaining scope

A subsequent native macOS pass exercised both renderers with matched logical sizes. Uno's AppWindow physical-pixel sizing now uses XamlRoot.RasterizationScale, correcting the Retina mismatch in the prototype. Both applications actually supported filtering/search, read state, list scrolling, DLL-owned views with retained counters, node dragging before/after 115% zoom, reset, content theme changes, and wrapping toolbars around 500 logical units wide. Avalonia's native View menu actually switched to Workflow.

Two fresh Uno Fluent processes reproduced Return not inserting a newline and Command+F not focusing search from notes. Workflow → + also reduced the tool-reported accessibility tree to an unknown node while visual and pointer behavior remained available; Avalonia retained its tree. These are current prototype/tool/runtime observations, requiring independent input and accessibility verification before assigning root cause to the framework. They are not claims that Uno lacks multiline controls, shortcuts, or accessibility altogether.

The Cupertino variant explicitly exports implicit styles; the earlier resource-only setup had not applied them. Native captures now confirm changed basic controls and typography with mixed list styling. This does not provide a complete macOS shell. Its captured edge black area and Clip click without an observed counter update remain open layout/input/capture observations rather than a completed native interaction pass. The guide records exact scope and reproduction steps.

Chinese rendering/paste worked, but real input-method composition/candidate placement and GPU frame pacing were not established. Android, Windows, legacy Windows and Linux were not exercised. Tool timeouts or early coordinate errors are excluded from framework judgments. Headless Loaded timing, working-set snapshots and container counts remain diagnostic, not native first-frame time, peak memory, frame rate, or installation size.

## Product desktop integration

The first product increment now uses Avalonia 12.1.6 in the main solution. The [desktop guide](../../docs/development/desktop-mvp.en.md) provides commands and scope. `mise run run:desktop` starts a generic .NET host and the Avalonia window in one process; the existing CLI/web entry remains available. Shared composition supplies the same plugin runtime, services, SQLite workspace, data-protection keys, and background tasks. Desktop activation does not map HTTP endpoints or start a listener. Service work runs off the UI thread, and local reading uses IArticleApi and subscription actions use IRssApi.

LazyTuiHub.Desktop owns the shell, lazy page composition, theme switching, and workflow/plugin inspectors. LazyTuiHub.Articles.Desktop is a separate dynamically loaded plugin, `articles.desktop`, with a hard dependency on `articles` and optional RSS subscription controls; it owns reader controls/state but references the shared API contract rather than RSS implementation types. Business services, workflow execution, DAO access, and plugin metadata remain independent of Avalonia. The host builds the UI DLL as a distribution artifact without a runtime implementation reference.

PluginRegistration now accepts plain DeclaredPage identity/title records. The framework persists them with plugin ownership, including before activation, so disabled, missing, and blocked plugins remain diagnosable. AddDesktopPage adds the declaration and a keyed control factory; the shell invokes only active plugins' factories on the UI thread. Views are created on first navigation and cached within the current window. Factories create fresh controls; plugin-owned models retain state across window replacement. The shared assembly baseline preserves Avalonia and contract type identity. This is a provisional page-level extension, not a general navigation or plugin-dependency platform.

The reader creates subscriptions, fetches a selected feed, lists articles with API pagination, filters read state, and marks articles read. It uses real RSS workflows and SQLite; it does not copy fixture state into the product. Load more advances the next-page token without a UI total-count cap. Subscription lists consume all API pages. Content is selectable text, including source markup; HTML rendering, external-link navigation, full-text search, and marking an article unread are not implemented by this UI. Business API behavior remains unchanged.

Workflows displays saved definitions, current plans, and unavailable-node reasons. It does not edit the graph or submit arbitrary executions. Plugins displays saved intent separately from current activation and can change enablement for the next startup. Disabling the UI alone leaves RSS services/tasks available; disabling or losing RSS blocks its dependent UI. Missing UI code retains page declarations and data, and framework diagnostics stay available. Existing managed retention continues under its own agreement.

Reader operations are serialized within a page, with visible errors and busy state. Window closing first cancels and awaits tracked UI operations, then stops hosted tasks before host disposal. Already committed writes survive cancellation. This establishes cooperative shutdown, not durable replay or lossless suspension. Selection and filters remain process-local; appearance preferences are saved on the local device.

## Multiple design systems

The host combines project-owned component roles with Avalonia Fluent, Devolutions macOS, and CCSWE Material. Plugin views use standard Avalonia controls with the shared roles.

Design selection (Automatic, Fluent, macOS, Material) is independent of color mode (System, Light, Dark). Automatic selects macOS on macOS, Material on Android, and Fluent elsewhere; this mapping does not implement an Android host. Explicit selection is available on desktop. Color mode uses RequestedThemeVariant. The desktop host owns a replaceable theme slot and semantic resources for page background, secondary text, row padding, and title size. Plugin-local styles remain separate.

Changing the effective design recreates the window and lazily recreates page views, retaining window placement and the active page. ReaderModel survives with subscriptions, loaded articles, selected feed/article, filters, draft input, the subscription panel, pagination, errors, and in-flight operations. Workflow/plugin selection and the workflow detail tab also survive. Scroll position, text selection, and keyboard focus are not serialized across reconstruction. A color-only change keeps existing controls. Window replacement does not stop DesktopSession, the host, or plugin tasks; normal closing retains the existing shutdown path. Task admission, completion removal, and shutdown snapshots are synchronized so cancellation cannot race collection copying.

Preferences are local UI configuration in `LazyTuiHub/desktop.json` under the local application-data directory, independently of the workspace database. `--desktop-settings` selects another file for isolated runs. Writes use a temporary sibling and rename. Read errors remain visible with default appearance and do not overwrite the file; save/apply errors also remain visible. The exploratory MVP introduces no preference schema version or historical migration.

The macOS adapter clears application-level wallpaper tint resources when replacing its theme. CCSWE follows the active Avalonia theme variant.

Headless integration checks exercise repeated design changes, independent color modes, draft/filter/selection retention, ongoing operations, inspector navigation, preference restoration, and visible configuration failures. A macOS native pass also confirmed distinct control rendering, selectors, Chinese draft retention, active navigation retention, light mode, and restored appearance after reopening. Other operating systems and complete assistive-technology behavior remain unverified.

## Responsive layout and UI automation

The next increment follows waiter's separation of application navigation, page content, and secondary forms. The reference is its [frame and module layout](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/frame_page.dart), [breakpoints](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/layout/bootstrap_breakpoints.dart), [recent-article page](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/yesod/yesod_recent_page.dart), and [article previews](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/lib/view/pages/yesod/yesod_preview_card.dart). This adopts its information hierarchy and narrow-screen navigation rather than importing Flutter or duplicating its entire widget layer.

Application navigation is inline above 992 logical units and opens through Menu below that width. A shared AdaptivePane uses the width actually allocated to its page: above 768 units it shows a 360-unit list beside details; otherwise it shows one pane at a time. Selecting an item by pointer or Enter opens details; Back restores the selected item's keyboard focus after layout. Reader, Workflows, and Plugins share this behavior. These are presentation breakpoints, not minimum window sizes or platform-support claims. Resizing reuses controls and models. Detail/list state survives design changes with the existing retained page state.

The reader gives its list a Library heading and count, subscription/read filters, a contextual refresh action, and a separate subscription form. Refresh fetches the selected feed or reloads saved articles in All sources; its context menu offers a local reload explicitly. Cards distinguish source, title, excerpt, unread state, and date. Preview line clamping does not truncate stored content or the complete article view. Reading text has a bounded line width; source URLs move below the body. Ordinary operation counts no longer occupy a permanent status bar. Errors remain visible. Appearance stays in the navigation footer's flyout. Navigation, article cards, and quiet toolbar controls own small application-specific templates; ordinary input controls retain the chosen design system.

Programmatic UI control is a maintained development path. `mise run test:desktop` runs the desktop tests and writes review PNGs and control-state JSON under ignored `artifacts/desktop/`. DesktopUiDriver uses official Avalonia.Headless mouse/keyboard events, actual control coordinates, window sizing, and Skia frame capture. The scenario exercises the product UI against real dynamically loaded RSS services, temporary SQLite, and a local RSS fixture; it does not introduce automation endpoints or test switches into the product. The usual test gate runs the same scenario without screenshot output. Generated images support review, without platform-dependent pixel-equality gates.

The scenario covers subscription entry, fetch, reading, read state, narrow-screen return/focus, inspector navigation, three designs at wide/medium/narrow sizes, and dark mode. Popup behavior is observed through open state and its own visual root, then operated by keyboard and captured separately. A popup omitted by computer-use capture is a tool observation, not sufficient evidence of a UI failure or a reason to replace the interaction. Headless images do not replace native validation for fonts, window chrome, accessibility, popup placement, or graphics performance. Android and legacy Windows remain outside this increment.

## Design-system audit

The [design-system Note](2026-11-18-mvp-design-systems.en.md) records the five-library comparison, product implementation and verification.

## Product verification and remaining work

Product checks and CI now include the desktop projects and real-service headless tests. Tests cover a multi-page feed, read-state persistence, input errors, navigation, disabled/missing/restored UI code, unavailable RSS dependencies, no desktop HTTP server, task shutdown, and in-flight UI cancellation. A native macOS ARM64 pass on 2026-11-18 additionally exercised subscription/fetch/read/filter, plan inspection, plugin disable/restore across restarts, retained data, theme switching, and closing the application. Native inspection led to readable article/plugin accessibility names instead of object dumps.

The UI projects retain product analyzer rules. They suppress only AVLN3001 for constructor-injected views initialized through compiled XAML: the unused resource-URI construction path cannot call their constructors. The [official compiler implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.6/src/Avalonia.Build.Tasks/XamlCompilerTaskExecutor.cs) explains this diagnostic; no placeholder services or parameterless constructors are added to satisfy it. Headless tests use the official session's asynchronous disposal.

Three design systems now have application adapters; their control coverage does not establish complete native platform interaction. Remote-server selection, a visual workflow editor, HTML rendering, installers, and Android/Windows 7/XP support remain open. The framework choice does not establish a tested platform support matrix. IME composition, complete assistive-technology behavior, and GPU performance still need validation. Uno remains in the opt-in comparison artifacts and introduces no second product renderer or cross-renderer abstraction.
