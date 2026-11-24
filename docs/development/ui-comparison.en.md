# UI framework comparison prototypes

English | [中文](ui-comparison.zh.md)

Avalonia was selected as the product UI framework on 2026-11-18. The [selection Note](../../.agents/notes/2026-11-17-mvp-ui-comparison.en.md) records the decision and product integration. The [desktop MVP guide](desktop-mvp.en.md) describes the real RSS/SQLite application. This guide retains both prototypes so the comparison can be reproduced; subsequent product UI development uses Avalonia. Platform design systems and the Android/legacy Windows support scope remain separate decisions.

## Run

Run these commands from the repository root with the normal mise environment:

```sh
mise run demo:ui:avalonia
mise run demo:ui:uno
mise run demo:ui:uno -- --cupertino
mise run demo:ui:avalonia -- --without-plugin
mise run demo:ui:uno -- --without-plugin
```

Each command builds the selected prototype in Release mode with analyzer warnings treated as errors. Close its window to stop it. The default theme is Fluent on both frameworks; Uno's optional Cupertino run uses its published theme package.

On macOS, `mise run bundle:ui` creates local application wrappers in `.agents/prototypes/ui-comparison/artifacts/`. Open `LazyTuiHub-Avalonia.app` and `LazyTuiHub-Uno.app` to compare them. `mise run bundle:ui:cupertino` creates the separate `LazyTuiHub-Uno-Cupertino.app` variant. These wrappers use the local mise-managed .NET runtime; they are development launchers, not distributable installers. Windows and Linux use the run commands; their native behavior has not been verified in this iteration.

## Common workload

Both prototypes use the same in-memory session and 600 deterministic bilingual articles. This count is a comparison fixture, not an application limit. They implement:

- A three-pane reader with feed selection, search, article selection, read/unread state, and editable reading notes.
- A toolbar that wraps using the framework's published Flex panel, light/dark switching, and a short page fade.
- A workflow canvas with five nodes, four Bézier connections, pointer-captured node dragging, scrollbars, and zoom/reset buttons. The graph illustrates the RSS pipeline; it does not execute or edit the product's stored workflow definitions.
- A plugin page containing its own text box, button, and counter, instantiated from a separately built managed DLL at cold startup.

The fixture does not fetch feeds, use SQLite, or change the product's plugin contracts. Read state, notes, node positions, and plugin counters last only for the current process. The product now has separate Avalonia integration described in the [desktop MVP guide](desktop-mvp.en.md).

## Source boundaries

The source lives in [the prototype directory](../../.agents/prototypes/ui-comparison/):

| Directory | Responsibility |
| - | - |
| `Shared/` | Identical fixture, reading state, typed plugin contract/loader, graph model, and diagnostic measurements. |
| `Avalonia/`, `Uno/` | Framework-specific controls, layout, input handling, animation, and application startup. |
| `AvaloniaPlugin/`, `UnoPlugin/` | Framework-native plugin views compiled into separate assemblies. |

Hosts build plugin projects with `ReferenceOutputAssembly="false"` and copy only the entry DLL to `plugins/SamplePlugin.dll`. Startup loads that path through the default `AssemblyLoadContext`, sharing framework and contract types. Switching pages retains the loaded view. Disabling a plugin takes effect on the next process start; missing files retain a visible diagnostic rather than preventing the reader from starting. This prototype does not implement hot unload, private dependency versions, or persisted plugin management.

Avalonia uses its native menu API for Reader/Workflow entries in addition to the shared in-window navigation. Uno currently implements in-window navigation only; this is an implementation difference, not evidence that Uno cannot integrate native menus.

## Automated verification

```sh
mise run check:ui
```

This optional check builds both prototypes and runs their official headless hosts. It checks reader layout and initial selection, search results, shared feed/read-state behavior, plugin view creation, disabled-plugin startup, missing-DLL diagnostics, Flex wrapping/growth, and graph/zoom state. Uno is checked with both Fluent and Cupertino resources. The Uno search check awaits `TextChanged`, which is delivered asynchronously.

The prototypes are outside the product solution and default CI. They retain repository formatting, file-size, and applicable analyzer rules; their local `.editorconfig` exempts literal layout dimensions from the magic-number rule. No product lint gate changes.

Generated JSON is written under the prototype's ignored `artifacts/` directory. It records environment, time to the application `Loaded` handler, working set, managed allocation estimate, and realized list-container count. Headless measurements describe this test host: they do not establish native first-frame time, peak memory, frame rate, or release package size.

## Current evidence

On 2026-11-24, both Release builds and the headless checks passed on macOS ARM64 with .NET 10.0. Both created real framework-native views from the separate DLL and supported the common reader/canvas mechanisms. Uno additionally required XAML resource initialization and asynchronous search-event handling; Avalonia provided a layout-transform wrapper for the zoomable surface. Neither prototype eliminates the need to implement node-editor interaction.

Native macOS inspection subsequently exercised both applications. The initial size is 1180 × 800 logical units: Uno's physical-pixel AppWindow API now converts using XamlRoot.RasterizationScale, following its [macOS resize implementation](https://github.com/unoplatform/uno/blob/30034eefe42ecf5f3e3fff7d49a05340aaa0152e/src/Uno.UI.Runtime.MacOS/UI/Xaml/Window/MacOSWindowWrapper.cs). This corrected a prototype mismatch on the 2× Retina display rather than a framework layout defect. Both normal windows produced 2360 × 1656 pixel captures including their title bars.

| Native operation | Avalonia | Uno Fluent |
| - | - | - |
| Feed/search, read state, independent list scrolling | Observed working. | Observed working. |
| DLL page, counter and state retained through navigation | Observed working. | Observed working. |
| Node dragging, connection updates, dragging at 115% zoom, reset | Observed working. | Observed working. |
| Toolbar wrapping at about 500 logical units wide | Observed working; size then restored. | Observed working; size then restored. |
| Light/dark content themes | Observed working. | Observed working. |
| Return in notes and Command+F from notes | Multiline input and search focus worked; undo also exercised. | Return did not insert a line break and Command+F did not focus search in two fresh processes. Root cause remains open. |
| Accessibility tree after Workflow → + | Controls remained readable. | Two fresh processes reduced the reported tree to an unknown node while pixels and pointer interaction continued working. |
| Native menu | View → Workflow actually opened the canvas. | No equivalent native menu implemented in this prototype; no claim of framework inability. |

The keyboard and accessibility observations describe this prototype, runtime, and desktop tool. They need manual input, VoiceOver or an independent accessibility inspector before attribution to the framework. A missing clear-button glyph in the Fluent run also leaves font-asset configuration to inspect.

Cupertino now explicitly sets `WithImplicitStyles = true` to invoke its [implicit-style export](https://github.com/unoplatform/Uno.Themes/blob/b43eec86d060a991fb24fe5f0c5f24eba2533631/src/library/Uno.Cupertino/CupertinoResources.cs); merely creating its resource dictionaries had left controls using Fluent. The check now verifies the effective implicit button-style lookup as well as resource loading. Native captures confirmed different buttons, input boxes and typography while lists retained the other styling. This is partial theme coverage, not a complete macOS shell. The Cupertino run also showed a black edge region in captures and no observed counter change after a coordinate Clip click; layout, input injection, hit testing and capture effects have not been isolated, so its full native interaction pass remains open.

Both Chinese display and pasted text were exercised, but real input-method composition/candidate placement, GPU frame pacing, Android, Windows, legacy Windows and Linux remain unverified. Desktop-tool timeouts and early coordinate errors are not framework failures or application startup measurements. These results informed the decision to use Avalonia. The Note remains proposed under the MVP lifecycle agreement; framework selection is settled, the first product integration is implemented, and broader platform validation remains open.

## Interactive comparison

Use the same window size and perform these operations on each app:

1. Select Design: the article list should contain 200 entries. Search for `workflow`: it should contain 100. Select an article, toggle its read state, and switch away and back.
2. Scroll the article list and reading pane independently. Resize the window and inspect toolbar wrapping and the three-pane layout.
3. Enter Chinese through an actual input method in reading notes. Check composition, candidate placement, selection, copy/paste, and undo. Pasting Chinese alone does not verify an input method.
4. Open Workflow, drag a node, zoom in/out, reset, and scroll. Connections should follow the node. Check dragging again after zooming.
5. Open Plugin, edit a passage, click Clip a passage, switch away and return. The counter should remain. Restart with `--without-plugin`: the reader should remain usable and the plugin page should explain its absence.
6. Toggle light/dark and use keyboard navigation, including Command+F on macOS or Control+F elsewhere. Inspect the fade and any native menu separately.

Record native results separately from headless checks. A missing observation remains pending rather than becoming a framework failure or a passing result.
