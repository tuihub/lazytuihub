---
status: proposed
category: architecture
---
# MVP design-system fidelity and ownership

English | [中文](2026-11-18-mvp-design-systems.zh.md)

## Decision scope

The user accepted the interactive official component specimens on 2026-11-18. They are references for the next design-system review, not approval of the Avalonia implementations. This Note separates application misuse, installed-library limits, available alternatives, and platform/rendering differences. The [UI comparison Note](2026-11-17-mvp-ui-comparison.en.md) retains the framework comparison and current desktop implementation.

Every MVP selection remains provisional. Optimize for the final deliverable; preserving earlier code, avoiding replacement, and sunk implementation effort are not criteria. Long-term dependency stability, future switching exposure, and ownership of maintenance do matter to the final design. A self-maintained component library is a first-class option. The audit below records the input to the current product implementation.

## Product implementation

The desktop host owns the visual roles used by the shell, inspectors and RSS plugin. Components.axaml defines navigation and collection rows, selection and focus indicators, and semantic text styles. Each adapter supplies role metrics, colors and typography. Plugin pages apply roles to standard Avalonia controls. Other controls use the active base theme. A future theme replacement can be exercised against the same pages and workflow.

Fluent targets desktop Fluent with the Avalonia theme and its accent button. macOS uses Devolutions control behavior with a project-owned compact sidebar. AppKit guides spacing and hierarchy. The local headless capture resolved the macOS font to Helvetica Neue. Native compositor effects and exact system-font rendering require native verification.

Material uses CCSWE for standard controls including editing, selection, menus and popups. The project owns its filled, outlined, text and icon button templates, pointer-origin ripple, keyboard focus ring, M3 color roles and reading typography. Regular and Medium Roboto assets from Uno.Fonts.Roboto 2.10.1 ship with their OFL notice. The product applies a common light/dark palette to its surfaces and CCSWE controls. Business pages use the same roles in all three designs.

Buttons follow waiter's action-based icon mapping. Views request add, refresh, navigation, back, close and appearance. The host supplies vector outlines for the selected design, preserves their optical bounds, and draws them in the button's foreground color. Fluent uses Microsoft Regular 20 icons, macOS uses Cupertino Icons 1.0.9, and Material uses Material Symbols Outlined at optical size 24, weight 400, fill 0 and grade 0. Standalone glyphs measure 20, 18 and 24 logical units respectively. Beside text they measure 20, 16 and 18. Material draws its 40-unit state layer inside a 48-unit pointer target. Theme dictionaries identify the upstream glyphs and distributions include the license notices. [Waiter mappings](https://github.com/tuihub/waiter/blob/ae1ae6f349eef99a8a300ed9276c319683ef5797/packages/universal_ui/lib/src/icons.dart), [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), [Cupertino Icons](https://pub.dev/packages/cupertino_icons/versions/1.0.9), [Material Symbols revision](https://github.com/google/material-design-icons/tree/10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7/variablefont).

The product headless scenario operates the subscription panel in each design, exercises keyboard focus and activation, and records narrow/wide layouts, the appearance popup and dark mode. It also verifies the Material primary button resolves Roboto and responds to pointer press/release. Snapshot JSON includes the resolved primary typeface. The independent five-library gallery remains the unmodified reference for evaluating vendor updates.

## Verified references and method

The [specimen tool](../prototypes/design-baselines/README.en.md) uses Microsoft Fluent UI React, Google Material Web, and native Apple AppKit. The first two are Web references. Fluent Web is not WinUI; the Material reference is Material 3, while the earlier Material.Avalonia integration uses an earlier design vocabulary. AppKit is a native platform reference rather than another Skia renderer.

The Web capture path now uses Chromium software rendering and produces 24 PNG/JSON pairs across light/dark, default, hover, input focus, popup, selector, and narrow layouts. These inspect states; they are not animation-conformance tests. AppKit's view-cache PNGs still lose composited content and must not serve as image baselines. The user's confirmation concerns interactive specimens.

An independent Avalonia program loads five libraries directly without product templates. Each runs in its own process and exercises light/dark, button activation, text input, keyboard selection in an opened ComboBox, and flyout opening. It captures default, hover, keyboard focus, pressed, and popup states, producing 50 PNG/JSON pairs. JSON records requested fonts and resolved primary typefaces, not per-character fallback glyphs. It also attaches/removes four additional themes and checks weak-reference reachability after collection. Devolutions receives its required BeginInit/EndInit lifecycle, equivalent to XAML initialization; an initial probe that omitted this lifecycle was a harness error, not a missing library control.

A separate temporary substitution of FluentAvalonia and CCSWE into the actual desktop host passed all five existing desktop tests. That establishes compatibility with those exercised operations, not full visual fidelity or a final selection. The isolated project retains both candidates for comparison.

## Audit of the original desktop prototype

| Area | Evidence and classification | Consequence for the final implementation |
| - | - | - |
| Button emphasis and interaction | The application's `primary` class only changes font weight. Its QuietButton replaces vendor templates with a shared Border, removing vendor ripple, elevation, focus treatment, padding, and typography. The installed Fluent/macOS themes provide `accent`; Material.Avalonia provides MaterialOutlineButton, MaterialFlatButton, and MaterialIconButton. The independent specimen exercised accent, outline and text variants; icon variants were identified in source. | This is incorrect/incomplete integration, not unavailable button support. A chosen vendor implementation must use its actual variants; an owned implementation must deliberately implement every required state. |
| Color roles | Three adapters hard-code similar neutral, hover, selection, and dark accent colors while ordinary vendor controls use different resources. Material's page accent and its configured Indigo/Teal controls are inconsistent. | Use one coherent per-design semantic palette for surfaces, text, accents, selection, and overlays. Do not approximate a design by recoloring a few shared elements. |
| Navigation and lists | NavigationItem and ArticleCard replace selection templates. FluentAvalonia already renders a selection indicator. Devolutions' stock ListBox renders check-mark selection; it is not an AppKit source-list row. | Distinguish a normal list, source-list navigation, and an article card. App-specific composition is legitimate, but its styles must implement the correct design and states. A menu-like list cannot be assumed to provide native sidebar semantics. |
| Typography | Shared headings force size/weight choices independently of each design's text roles. In this machine's isolated probe, requested normal body text resolved to Helvetica for both Fluent packages, Helvetica Neue for Devolutions, Roboto for Material.Avalonia, and DM Sans 9pt for CCSWE. Resolved weights were Normal. | Use explicit semantic type roles and verify actual font resolution. The observed heavier appearance alone does not demonstrate accidental bold. The Mac theme's fallback differs from AppKit system typography. |
| Material generation | Material.Avalonia supplies real text/outline buttons, ripples, typography and themed controls, but those do not collectively implement the accepted Material 3 reference. CCSWE provides M3 pill buttons, tonal containers, type roles, and navigation themes. | M3 is not absent from the Avalonia ecosystem. The current package is a selection mismatch if M3 is the target; changing a corner radius is not an adequate conversion. |
| Fluent platform variants | FluentAvalonia's WinUI-inspired controls differ from Microsoft's React specimens in sizing, fields, switches and typography. | Compare intended roles and interaction states, not pixel identity between Web and desktop systems. Decide whether the product targets Fluent Web or desktop Fluent explicitly. |
| Native macOS composition | Devolutions draws Avalonia templates, has native-looking controls and Liquid Glass resources, and falls back to Fluent for uncovered controls. It does not turn a Button into NSButton or a Flyout into NSPopover. | Native window/compositor behavior, font rasterization, material effects and exact AppKit widget behavior are separate platform work. No tested package establishes complete native equivalence. This does not prove Avalonia cannot integrate native views. |

No framework-level inability to provide the current buttons, inputs, lists, theme variants, focus states, or responsive composition was established. Missing support in one installed package must not be generalized to Avalonia. Conversely, the existence of a theme package does not establish complete adherence to its design system.

## Alternatives and maintenance evidence

| Implementation | Verified useful capability | Maintenance and fidelity boundary |
| - | - | - |
| Avalonia.Themes.Fluent 12.1.6 | Official Avalonia theme; controls and both variants pass the independent scenario; detached-theme probe retains 0/4. | Its styling is not Microsoft's Fluent React implementation. The current app also overrides parts it already provides. |
| Material.Avalonia 3.21.0 | Existing control coverage, button variants and ripple templates; 0/4 retained themes after disposal. | Earlier Material vocabulary, despite the package's 3.x version number. Keeping it does not satisfy an M3 target. |
| Devolutions.AvaloniaTheme.MacOS 2026.11.5 | Mac-oriented controls and states; source published by a company using the library; 0/4 retained with vendor cleanup. Repository activity was present on 2026-11-17. | Vendor still marks it work in progress. Coverage/fallbacks, app-level wallpaper resources, font fallback and native equivalence require deliberate integration. Vendor priorities are not identical to ours. |
| FluentAvaloniaUI 3.1.0 | WinUI-inspired controls, built-in selected indicator, transparent and accent buttons; independent scenario and product substitution pass. | Its theme subscribes to platform color changes. The audited implementation has no matching unsubscription, and 4/4 removed themes remained reachable in the probe. Repeated replacement needs a lifecycle solution. Its README also does not promise Windows 7/8 support; our conditional legacy-platform goals remain unverified. |
| CCSWE.Avalonia.Material 12.2.1 | Published Avalonia 12 M3 implementation; correct broad button shapes and state layers, controls and light/dark work in the probe; 0/4 retained themes. | Created 2026-06-04, active on 2026-11-16: a short operating history, not evidence of abandonment. Ships its own fonts and palette. The inspected button template uses state-layer opacity rather than a spatial ripple, and the probe's keyboard focus is an Avalonia dotted rectangle. It does not exactly match the accepted Google sample out of the box. |
| jgwy/material3 source | Independent M3 project with dynamic color, ripple/focus infrastructure and control mappings. | README explicitly says no stable release; roadmap lists unfinished core coverage and platform validation. Last repository push observed was 2026-08-09. Source review only; no runtime comparison was performed, so it is not ranked as a validated replacement. |

The weak-reference probe is evidence about detached theme-object retention in a live headless application, not a heap-size estimate or proof of every native lifecycle. The 0/4 results do not certify leak freedom. Repository activity and release history inform maintenance confidence; stars, a package's existence, and a passing small scenario do not prove long-term stability.

## Self-maintained visual components

A realistic owned library keeps Avalonia's control behavior: Button commands/input, TextBox editing and IME, selection, focus routing, layout, virtualization and automation peers. It owns design tokens, typography, ControlThemes, required template parts, states, overlays and motion. It need not rebuild a UI engine. Ownership still includes verifying that templates preserve access keys, focus indicators, hit targets, selection colors, popup boundaries, scaling and disabled states. Reusing base control behavior does not validate these automatically.

The initial delivered surface is the actual reader and inspectors: buttons and icons, text inputs, selectors, lists/navigation, tabs, text, scrolling, split panes, progress, menus and flyouts. Expand coverage when a delivered feature requires it. A fallback for additional plugin controls must be explicit; a partially owned theme cannot claim complete consistency for arbitrary controls.

| Choice | Long-term benefit | Long-term obligation |
| - | - | - |
| Use vendor themes with explicit adapters | Upstream fixes, coverage expansion and platform work can be shared. | Follow multiple maintainers and differing design goals; adapters may depend on template parts, global resources and lifecycle behavior. Extensive overrides can become an unacknowledged fork. |
| Own the required visual layer over Avalonia controls | One owner defines fidelity, cross-design switching, public semantic roles and fixes. Business views need not depend on several vendor-specific APIs. | Maintain templates against Avalonia changes, design-system evolution, font behavior and cross-platform interaction. This work does not disappear because the library is small. |
| Maintain a curated fork of useful templates | Reuses inspected implementation while giving the project control of changes. | Preserve licenses and provenance, review upstream changes deliberately, and accept fork maintenance. Copying a large theme is not evidence of quality or independence from its contracts. |

The product uses the mixed ownership described above. Published package stability and the long-term maintenance of three visual designs remain empirical questions.

## Sources

- [Avalonia Fluent button implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.6/src/Avalonia.Themes.Fluent/Controls/Button.xaml).
- [Material.Avalonia button variants and ripple implementation](https://github.com/AvaloniaCommunity/Material.Avalonia/blob/v3.21.0/Material.Styles/Resources/Themes/Button.axaml).
- [Devolutions coverage and fallback documentation](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/README.md), [list template](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/Controls/ListBoxItem.axaml) and [font resources](https://github.com/Devolutions/avalonia-extensions/blob/7ef3ab09b1696938009c7923efda44490a848445/src/Devolutions.AvaloniaTheme.MacOS/Accents/ThemeResources.axaml).
- [FluentAvalonia published theme implementation](https://github.com/amwx/FluentAvalonia/blob/215ee0481e8b0e6d396cbe2a0f33dc791c5646ab/src/FluentAvalonia/Styling/Core/FluentAvaloniaTheme.axaml.cs) and [package requirements](https://www.nuget.org/packages/FluentAvaloniaUI/3.1.0).
- [CCSWE published button implementation](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material/blob/b75cfa8a65ecb02560888a57ea517cf5bf3bf284/src/CCSWE.Avalonia.Material/Controls/Button.axaml), [fonts](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material/blob/b75cfa8a65ecb02560888a57ea517cf5bf3bf284/src/CCSWE.Avalonia.Material/Fonts.axaml), and [repository](https://github.com/CCSWE-Avalonia/CCSWE.Avalonia.Material).
- [jgwy/material3 current coverage and release limits](https://github.com/jgwy/material3/blob/main/README.md) and [roadmap](https://github.com/jgwy/material3/blob/main/ROADMAP.md).
