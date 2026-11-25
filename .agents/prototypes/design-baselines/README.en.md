# Official component specimens

English | [中文](README.zh.md)

Use these specimens to compare control geometry, typography, tokens and interaction states. The official controls and the five Avalonia implementations run independently of the product. The layout of the specimen page is local to this experiment.

## Verification status

The user accepted the interactive official specimens on 2026-11-18. Web capture now works with Chromium software rendering and produces 24 PNG/JSON pairs. Native AppKit view-cache images still have capture defects and are **not accepted image baselines**. An independent Avalonia program produces 50 PNG/JSON pairs from five libraries; it verifies input and examines theme lifecycle without product style overrides. The [design-system audit](../../notes/2026-11-18-mvp-design-systems.en.md) records the findings and maintenance choices. These samples do not establish pixel conformance or replace platform validation.

## Native Mac library window

On a Mac, open the two-game library in a real AppKit window with the command below. Close the window to exit. The scene uses an NSSplitViewController sidebar, a unified NSToolbar with a native search item, and SwiftUI shelf content. It shares the review scene’s two titles, artwork and missing-cover case. The page provides a static composition reference. Library actions are illustrative. Compare it with the Games page in the product window at the same width and appearance.

```sh
mise -E development run render:design-baselines:macos-library --show
```

Without `--show`, the tool keeps its window in the background, records dimensions in points, and captures its own window in light and dark mode under `.local/design-baselines/`. It requires a Mac with Xcode command-line tools and macOS 14.4 or later. ScreenCaptureKit includes the system-composited title bar and sidebar. If capture fails, the tool writes explicitly named `*.view-cache.png` diagnostics. Use the interactive window to judge materials in that case. The native controls follow the installed OS and linked SDK. [Apple’s AppKit window and sidebar walkthrough](https://developer.apple.com/videos/play/wwdc2025/310/)

## Native Windows and Android navigation

On Windows, open the NavigationView page in the WinUI 3 Gallery, choose Auto and resize the window. In the middle width range, open the compact pane and compare the position of the page before and after expanding it. [Microsoft NavigationView examples](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview)

On Android, install the official Material Components Catalog APK. In Navigation Bar → Label Visibility Demo, add a fourth item and choose Labeled. In Navigation Rail → Demo, add a fourth destination. Compare the icon, label, selection indicator and distance between destinations. Divide screenshot pixel measurements by the emulator’s density factor to compare logical sizes. The recorded run used Android 16 at 420 dpi (2.625 pixels per dp). [Material Catalog 1.14.0](https://github.com/material-components/material-components-android/releases/tag/1.14.0)

## Run

Use the repository's mise-managed Node.js and pnpm versions. The prototype has its own dependency lockfile and permits esbuild's required install script. It does not add another runtime version declaration.

```sh
mise -E development run setup:design-baselines
mise -E development run render:design-baselines:web
mise exec -- node .agents/prototypes/design-baselines/baseline.mjs render macos
mise -E development run render:design-baselines:avalonia
mise -E development run run:design-baselines
```

`setup` installs the local packages, downloads Playwright's Chromium headless shell into `.local/design-baselines/browsers/`, and caches Google's Roboto font in `.local/design-baselines/web/fonts/`. It records font URLs in `sources.json`. Rendering then uses local assets. `serve` prints a loopback URL for interactive Fluent and Material references and an Avalonia image gallery; stop it with Ctrl+C. The gallery switches between light/dark and five states, with links to measurements. `render` without a target runs Web, macOS, and Avalonia captures. The native target needs macOS, an available desktop session, and Xcode command-line tools. It briefly displays its own window, then closes it.

After building the native target, its interactive form is:

```sh
.local/design-baselines/appkit-baseline .local/design-baselines --interactive
```

The native interactive window retains real control behavior, including editable text, selectors, switches, a popover, and button actions. Terminate its process after inspection. No computer-use tool is required by these entry points.

## Reference scope

| Specimen | Official implementation | Valid comparison scope | Limits |
| - | - | - | - |
| Fluent | Microsoft Fluent UI React v9, `webLightTheme` / `webDarkTheme`, official text presets and icons | Fluent 2 **Web** component geometry, typography, selection, focus, hover, and popovers | This is not WinUI or Windows-native rendering. The default Segoe UI stack falls back to installed fonts on macOS; JSON records resolved fonts. The current official Nav guidance calls the component Preview. |
| Material | Google's `@material/web`, official type scale, Roboto, and Material color utilities | Implemented Material 3 Web buttons, text fields, selects, tabs, lists, dialog, and states | Material Web is in maintenance mode. It is not a complete reference for new Material 3 Expressive components or Material 2. The package lacks a production navigation drawer and card; the specimen uses its real tabs and lists instead of imitating missing components. |
| macOS | Apple's AppKit `NSButton`, `NSPopUpButton`, `NSTextField`, `NSTableView`, `NSSegmentedControl`, `NSSwitch`, `NSBox`, `NSPopover` | Native controls and semantic system fonts/colors on the recorded OS release | Current view-cache PNGs lose compositor content, show a black selected sidebar row, and produce a black popover. They cannot establish visual correctness. Native hover, an open selector menu, window chrome, blur, and shadows are not validated. |

Package versions are declared only in the local package manifest and lockfile. The native implementation comes from the installed OS SDK, and the output records the actual OS version. A package lock does not freeze OS fonts, browser rasterization, system accent, or accessibility settings.

## Samples and measurements

The Web runner captures both themes at 1120 CSS pixels, plus a 430-pixel layout, with default, hover, focused input, opened popup, and opened selector states. Selected tabs/navigation, checked controls, and disabled buttons appear in the default scene. The CSS arranges specimens without replacing component typography, corner radii, padding, or state styles. Material's page background uses its generated semantic color tokens.

Every completed Web PNG has a JSON companion with component and shadow-DOM geometry, CSS font family/size/weight/line height, colors, borders, spacing, focus/hover/disabled state, theme tokens, resolved platform fonts, browser version, and dependency versions. An outer custom-element font value does not necessarily describe its inner label: inspect its recorded shadow nodes. AppKit JSON records point geometry, system font descriptors and traits, semantic sRGB colors, native state, effective appearance, and whether the window actually became key. A requested focus state is not proof that the OS activated the window.

The native runner currently writes four main-view PNG/JSON pairs and two popover PNGs. On the tested macOS 26.6.2 session, `keyWindow` was false. These files preserve debugging evidence; do not use the defective images as golden files. Readable text with a normal font weight can still look heavier under a different font or renderer, so perceived weight alone is not a diagnosis.

The Avalonia program loads Avalonia Fluent, Devolutions macOS, Material.Avalonia, FluentAvalonia and CCSWE Material 3. Each library runs in a separate process, exercises button/text/selector/flyout input in light and dark modes, and captures default, hover, focus, pressed and popup states. Output lives in `.local/design-baselines/web/avalonia/`. JSON records control bounds, focus and requested/resolved primary typeface; it does not enumerate per-character fallback glyphs. Specimen title/body sizes arrange the page and do not certify the library's complete type scale.

Each process also attaches/removes four new theme instances, performs available disposal/vendor cleanup, collects garbage and records weak-reference reachability in `*-retention.json`. This identifies retained theme objects in the live headless application; it does not measure heap growth or certify leak freedom. The probe does not fail a library merely because its retention count differs.

No default CI task, snapshot gate, tolerance, or baseline update approval rule is added. Compare matching roles and states against product images and metrics; do not force all designs to have identical density or layout. Capturing settled states does not verify motion or animation timing.

## Official sources

- [Microsoft Fluent 2 React component catalog](https://fluent2.microsoft.design/components/web/react), [text presets](https://fluent2.microsoft.design/components/web/react/core/text/usage), [Nav guidance](https://fluent2.microsoft.design/components/web/react/core/nav/usage), and [official React Storybook](https://react.fluentui.dev/).
- [Microsoft Fluent UI source](https://github.com/microsoft/fluentui).
- [Google Material Web source and maintenance status](https://github.com/material-components/material-web), [component catalog](https://material-web.dev/), [typography setup](https://github.com/material-components/material-web/blob/main/docs/theming/typography.md), and [Material color utilities](https://github.com/material-foundation/material-color-utilities).
- [Google Material Icons source and license](https://github.com/google/material-design-icons), used for the two SVG icons inside official `md-icon` controls; [Roboto font repository](https://github.com/googlefonts/roboto-3-classic).
- [Apple's designing for macOS guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos), [typography guidance](https://developer.apple.com/design/human-interface-guidelines/typography), and [AppKit documentation](https://developer.apple.com/documentation/appkit).

These are upstream reference implementations and guidance. Third-party Avalonia packages remain implementations to assess against the relevant reference scope.
