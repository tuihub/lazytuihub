# Avalonia desktop MVP

English | [中文](desktop-mvp.zh.md)

## Run and use

Run the product desktop application from the repository root:

```sh
mise run run:desktop
mise run run:desktop -- --database /path/to/workspace.db
```

The default workspace is `LazyTuiHub/workspace.db` under .NET's local application-data directory, shared with CLI/server mode. Use an explicit database path for an isolated experiment. The adjacent `keys/` directory holds the existing API pagination-key material. The application loads the distribution's `plugins/plugins.json`; `--plugin-directory` selects another deployment directory.

In Reader, expand Add subscription, enter an HTTP/HTTPS feed URL and an optional name, then choose Subscribe. Select the feed and choose Fetch to retrieve articles through the RSS pipeline. Select an article to read its stored content, choose Mark read, or filter read/unread articles. Load more requests the next API page; the UI does not cap the total number of articles. Reload reads the current workspace, including changes from background refresh. Article content is displayed as selectable text, including any source markup; HTML rendering and opening external links are not implemented.

Workflows shows saved definitions, the current local execution plan, and reasons a graph cannot execute. It is a read-only inspector. Use the existing JSON/CLI tools to save and run workflow definitions; this page does not submit executions or collect their required inputs.

Plugins shows saved enablement separately from this startup's activation and file availability. Enable/disable changes apply after restarting. Required plugins cannot be disabled. The RSS Reader UI plugin (`rss.desktop`) can be disabled independently of RSS business services (`rss`); disabling RSS makes its dependent UI unavailable. Missing or disabled plugins retain page declarations and existing data. Ordinary retention policies continue under the storage agreement. Changing tabs preserves the current page instance and its in-memory selection; selection and filters are not saved across restarts. Appearance preferences are saved separately on this device.

## Appearance

Use Design in the header to choose Automatic, Fluent, macOS, or Material. Automatic follows the platform: macOS uses its macOS theme, Windows/Linux use Fluent, and the future Android mapping uses Material. Appearance independently selects System, Light, or Dark. Choices are saved in `LazyTuiHub/desktop.json` under the local application-data directory, across workspace changes and restarts. For isolated runs, pass `--desktop-settings /path/to/desktop.json` as well as a separate database path.

Changing the design rebuilds the window while retaining the current page, reader selection/filter, loaded articles, unfinished subscription input, and ongoing operations. Color-only changes update existing controls. Scrolling, focus, and text selection may reset when the window is rebuilt. Background tasks continue; plugin enablement still requires a cold restart.

The host loads one base theme at a time and supplies `PageBackground`, `SecondaryText`, `PagePadding`, and `PageTitleSize` resources. Shared style classes include `title`, `caption`, `status`, and `primary`. Plugin pages use standard Avalonia controls and dynamic resources for appearance-dependent values. `AddDesktopPage` registers a transient view factory; keep state in plugin-owned services/models when it should survive a design switch. Initialize data once per model rather than on every view load. The shell caches views within their window; design changes do not reload plugin assemblies.

Fluent uses Avalonia's official theme; macOS uses Devolutions' theme with Fluent fallbacks for uncovered controls; Material uses Material.Avalonia. Native title bars remain controlled by the operating system. Complete native interaction, full Material 3 conformance, and Android support are not established by theme selection.

## Composition and lifecycle

| Component | Responsibility |
| - | - |
| [Application host](../../src/LazyTuiHub/) | Shared service/plugin composition; a generic host for desktop mode and a web host for HTTP/CLI mode. |
| [Desktop](../../src/LazyTuiHub.Desktop/) | Avalonia application/window, page composition, workflow/plugin inspectors, UI operation tracking, and shutdown. |
| [RSS desktop plugin](../../src/LazyTuiHub.Rss.Desktop/) | Reader controls and state; direct calls to the shared IRssApi contract. |
| [Plugin framework](../../src/LazyTuiHub.Plugins/) | Plain page declarations, owner identity, retained metadata, dependency activation, and cold-start enablement. |

The desktop host starts the same available background tasks without starting an HTTP listener. Business and DAO code do not reference Avalonia. The desktop plugin is built as a distribution artifact and loaded dynamically; the host has no runtime reference to its implementation assembly. `AddDesktopPage` contributes a plain page declaration and a keyed Avalonia control factory. Only active plugins contribute navigation entries; factories run on the UI thread when a page is first opened. The default shared assembly context preserves contract and Avalonia type identity.

Reader actions run service work away from the UI thread and serialize operations within the page. Errors remain visible; an error is not converted into an empty successful result. Closing the main window cancels and awaits tracked UI operations, then stops hosted tasks before disposing services. Cancellation does not undo completed database changes or provide durable workflow suspension.

The product uses Avalonia 12.1.6 with three design systems, compiled XAML bindings, a wrapping action bar, and list virtualization. Richer responsive layouts, remote-server selection, a visual workflow editor, HTML rendering, and Android/legacy Windows support remain future work. The framework decision is recorded in the proposed [UI Note](../../.agents/notes/2026-11-17-mvp-ui-comparison.en.md); the [comparison prototypes](ui-comparison.en.md) remain separate development artifacts.

## Verification

`mise run check` includes product build/analyzers, formatting, API consistency, document/Note rules, and product tests. `mise run test` includes the official Avalonia headless host with real dynamically composed RSS services and an isolated SQLite workspace. Scenarios cover pagination, read-state persistence, input errors, navigation, disabled/missing/restored UI plugins, unavailable business dependencies, absence of a desktop HTTP server, and cooperative shutdown. Views use constructor injection and compiled initialization; the two UI projects suppress only AVLN3001, which warns about unused URI-based construction without a parameterless constructor.

The 2026-11-18 macOS ARM64 native pass also exercised subscription creation, HTTP feed fetch, Chinese display, mark-read/unread filtering, workflow inspection, plugin disable/re-enable across restarts, retained read state, light/dark switching, and window shutdown. Accessibility labels identify article/plugin names rather than serialized objects. These observations do not establish full assistive-technology coverage, IME composition, GPU performance, or native behavior on other platforms.
