# Avalonia desktop MVP

English | [中文](desktop-mvp.zh.md)

## Run and use

Run the product desktop application from the repository root:

```sh
mise run run:desktop
mise run run:desktop -- --database /path/to/workspace.db
```

The default workspace is `LazyTuiHub/workspace.db` under .NET's local application-data directory, shared with CLI/server mode. Use an explicit database path for an isolated experiment. The adjacent `keys/` directory holds the existing API pagination-key material. The application loads the distribution's `plugins/plugins.json`; `--plugin-directory` selects another deployment directory.

In Reader, choose the + button beside Library, enter an HTTP/HTTPS feed URL and an optional name, then choose Subscribe. Select the feed and use the refresh button to retrieve articles through the RSS pipeline. Select an article to read its stored content, choose Mark read, or filter read/unread articles. Load more requests the next API page; the UI does not cap the total number of articles. With All sources selected, refresh reloads saved articles. The refresh button’s context menu also offers Reload saved articles, including changes from background refresh. Article content is displayed as selectable text, including any source markup; HTML rendering and opening external links are not implemented.

Workflows shows saved definitions, the current local execution plan, and reasons a graph cannot execute. It is a read-only inspector. Use the existing JSON/CLI tools to save and run workflow definitions; this page does not submit executions or collect their required inputs.

Plugins shows saved enablement separately from this startup's activation and file availability. Enable/disable changes apply after restarting. Required plugins cannot be disabled. The reader (`articles.desktop`) requires `articles`. Disabling `rss` removes subscription controls and stops fetching. Saved articles remain readable. Missing or disabled plugins retain page declarations and existing data. Ordinary retention policies continue under the storage agreement. Changing pages preserves the current page instance and its in-memory selection; selection and filters are not saved across restarts. Appearance preferences are saved separately on this device.

## Appearance

Open Appearance at the bottom of the navigation pane, then choose Automatic, Fluent, macOS, or Material under Design system. Automatic follows the platform: macOS uses its macOS theme, Windows/Linux use Fluent, and the future Android mapping uses Material. Color mode independently selects System, Light, or Dark. Choices are saved in `LazyTuiHub/desktop.json` under the local application-data directory, across workspace changes and restarts. For isolated runs, pass `--desktop-settings /path/to/desktop.json` as well as a separate database path.

Changing the design rebuilds the window while retaining the current page, reader selection/filter, loaded articles, unfinished subscription input, and ongoing operations. Color-only changes update existing controls. Scrolling, focus, and text selection may reset when the window is rebuilt. Background tasks continue; plugin enablement still requires a cold restart.

Use standard Avalonia controls in plugin pages. Apply `accent` to the main action, `secondary` to a supporting action, `quiet` to a text action and `icon` to an icon button with an accessible name. Set `navigation` or `collection` on a ListBox to use the corresponding selection and keyboard focus treatment. Text roles include `title`, `headline`, `section`, `item-title`, `reading`, `caption` and `metadata`. Use dynamic resources such as `SurfaceBrush`, `DividerBrush` and `SecondaryText` for custom composition. Other standard controls inherit the active base theme. `AddDesktopPage` registers a transient view factory; keep state in plugin-owned services/models when it should survive a design switch. Initialize data once per model rather than on every view load. The shell caches views within their window; design changes do not reload plugin assemblies.

Place `<PathIcon Classes="action" Data="{DynamicResource ActionIconRefresh}" />` in an icon button. Use `action inline` beside text. Icons follow the active design and foreground color. Put the tooltip and accessible name on the button. For a new action, define the same resource key in each [design's icon dictionary](../../src/LazyTuiHub.Desktop/Appearance/Icons/).

Fluent uses Avalonia's official theme; macOS uses Devolutions' theme with Fluent fallbacks for uncovered controls; Material uses CCSWE Material with the project’s M3 palette, Roboto type, and button templates. Native title bars remain controlled by the operating system. Complete native interaction, full Material 3 conformance, and Android support are not established by theme selection.

## Responsive content

Wide windows keep application navigation, article summaries, and the reading surface side by side. At 992 logical units or less, Menu opens application navigation as an overlay. Each `AdaptivePane` independently changes from a list/detail split to a single pane at 768 logical units of available page width. Click a list item or press Enter to open its details; Back restores the list and keyboard focus. Workflows and Plugins use the same arrangement.

Resizing keeps the existing views, selection, draft input, and loaded data. It does not reload services or change the theme. The current detail/list state also survives a design switch. Article cards prioritize source, title, excerpt, unread state, and publication date. Card previews occupy a limited number of lines; the stored title/content and full reading view remain complete. Reading text uses a maximum line width, and source URLs follow the body. Subscription entry opens a separate panel; ordinary loaded-count messages appear beside Library, while failures stay visible. The source content still displays as text rather than rendered HTML.

## Composition and lifecycle

| Component | Responsibility |
| - | - |
| [Application host](../../src/LazyTuiHub/) | Shared service/plugin composition; a generic host for desktop mode and a web host for HTTP/CLI mode. |
| [Desktop](../../src/LazyTuiHub.Desktop/) | Avalonia application/window, page composition, workflow/plugin inspectors, UI operation tracking, and shutdown. |
| [Article reader plugin](../../src/LazyTuiHub.Articles.Desktop/) | Reader controls and state; IArticleApi for reading and optional IRssApi for subscriptions. |
| [Plugin framework](../../src/LazyTuiHub.Plugins/) | Plain page declarations, owner identity, retained metadata, dependency activation, and cold-start enablement. |

The desktop host starts the same available background tasks without starting an HTTP listener. Business and DAO code do not reference Avalonia. The desktop plugin is built as a distribution artifact and loaded dynamically; the host has no runtime reference to its implementation assembly. `AddDesktopPage` contributes a plain page declaration and a keyed Avalonia control factory. Only active plugins contribute navigation entries; factories run on the UI thread when a page is first opened. The default shared assembly context preserves contract and Avalonia type identity.

Reader actions run service work away from the UI thread and serialize operations within the page. Errors remain visible; an error is not converted into an empty successful result. Closing the main window cancels and awaits tracked UI operations, then stops hosted tasks before disposing services. Cancellation does not undo completed database changes or provide durable workflow suspension.

The product uses Avalonia 12.1.6 with three design systems, compiled XAML bindings, adaptive list/detail panes, and list virtualization. Further layout refinement, remote-server selection, a visual workflow editor, HTML rendering, and Android/legacy Windows support remain future work. The framework decision is recorded in the proposed [UI Note](../../.agents/notes/2026-11-17-mvp-ui-comparison.en.md); the [comparison prototypes](ui-comparison.en.md) remain separate development artifacts.

## Verification

Run the desktop interaction suite and generate review images without controlling the computer:

```sh
mise run test:desktop
```

[DesktopUiDriver](../../tests/LazyTuiHub.Tests/DesktopUiDriver.cs) sends Avalonia headless mouse and keyboard input to real controls, resizes windows, and captures Skia-rendered frames. [The layout scenario](../../tests/LazyTuiHub.Tests/DesktopTests.Layout.cs) creates a temporary SQLite workspace and local RSS server, enters a subscription through the form, fetches articles, opens details, marks read, returns with restored focus, and navigates inspectors. It checks all three designs at 1280, 820, and 390 logical units, plus the appearance flyout and dark mode. The task saves PNGs and matching JSON control snapshots (bounds, fonts, foreground colors, enabled/focused states) to ignored `artifacts/desktop/`; `LAZYTUIHUB_UI_ARTIFACTS` controls capture output when invoking the test runner directly. Tests also run under the normal product test gate without writing screenshots.

The appearance flyout is checked through its open state, its visual root, and keyboard interaction with its color picker. Captures use the popup’s actual visual root, whether an overlay or a separate window; a missing popup in a computer-use image alone is not evidence that the application failed. Generated images support visual review rather than exact-pixel golden-file gates. The [design-system audit](../../.agents/notes/2026-11-18-mvp-design-systems.en.md) records the library comparison, component ownership and platform limits. Run `mise run setup:design-baselines` once, `mise run render:design-baselines:avalonia` to generate library samples, and `mise run run:design-baselines` to compare them with official interactive references. Headless rendering does not establish native popup placement, operating-system font parity, accessibility, or GPU performance.


`mise run check` includes product build/analyzers, formatting, API consistency, document/Note rules, and product tests. `mise run test` includes the official Avalonia headless host with real dynamically composed RSS services and an isolated SQLite workspace. Scenarios cover pagination, read-state persistence, input errors, navigation, disabled/missing/restored UI plugins, unavailable business dependencies, absence of a desktop HTTP server, and cooperative shutdown. Views use constructor injection and compiled initialization; the two UI projects suppress only AVLN3001, which warns about unused URI-based construction without a parameterless constructor.

The 2026-11-18 macOS ARM64 native pass also exercised subscription creation, HTTP feed fetch, Chinese display, mark-read/unread filtering, workflow inspection, plugin disable/re-enable across restarts, retained read state, light/dark switching, and window shutdown. Accessibility labels identify article/plugin names rather than serialized objects. These observations do not establish full assistive-technology coverage, IME composition, GPU performance, or native behavior on other platforms.
