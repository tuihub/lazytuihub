# Desktop UI self-review

English | [中文](desktop-ui-review.zh.md)

When adding a page or changing a shared control, exercise the interaction whose appearance or layout changed. Run it against the actual plugin page and its active themes. The [reader toolbar](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Layout.cs) shows the smallest integration. The [game shelf](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Visual.cs) also measures typography, artwork, text contrast and motion.

## Declare a scene

1. Use the existing headless desktop driver and an isolated workspace. Load representative data, including missing assets or long text when relevant. Wait for an observable ready state before measuring.
2. Mark the xUnit test with `[Trait(DesktopVisualScene.Trait, "true")]`. Choose a short unique page key and control keys suitable for filenames. Use each supported design and color mode for a cross-design change. Exercise relevant window sizes, selection, disabled states, click/press behavior, popups, or return navigation in the interaction fixture.
3. Register the controls and regions that should stay in place across hover and focus. Give the driver an unrelated focusable control and a resting pointer position outside the targets. Views with intentionally moving elements can verify their changing bounds in the interaction fixture. Capture motion from the actual transition and check reduced motion where applicable.

```csharp
var scene = new DesktopVisualScene(driver, "reader", design, mode);
scene.Interaction("subscribe", "Add subscription", addButton);
scene.Track("feed-picker", feedPicker);
await scene.CheckAsync(readFilter);
```

`Interaction` drives pointer entry, exit, keyboard focus, hover with focus and exit with focus retained. Supply `hover` and `alternate` to exercise different parts of the same control. An optional `checkHover` applies a component-specific assertion while the pointer is over it. `IconLabel` measures the visible glyphs of a compound action. Its optional `fluentSpecimen` names a Microsoft Fluent UI React icon-and-text sample with the same content in the [official reference harness](../../.agents/prototypes/design-baselines/web/fluent.jsx). `Contrast` measures a text crop against its rendered background. The shared driver and report discover declared regions and tests automatically. [Scene API](../../tests/LazyTuiHub.Tests/Desktop/DesktopVisualScene.cs)

## Run and interpret

```sh
mise run setup:design-baselines
mise run review:desktop
```

Run setup on first use and after reference dependencies change. Open `.local/desktop-review/index.html`. The latest completed report links to its own JSON measurements and PNGs. For a correction, keep the previous run and compare the same fixture, theme, scale, font, and input state. Both the Avalonia capture and the reference browser run headless.

| Inspect | Evidence |
| --- | --- |
| Layout and interaction | Assert the intended bounds, focus, hit target, baseline or state transition in product tests. Resize and perform the real action that exposed the problem. |
| Painted surfaces | Inspect the two rest captures’ noise count, hover and focus differences, and any pixels remaining after exit. Inspect artwork and title separately when an entire card is clickable. An unsettled frame calls for another look at asset readiness or motion. |
| Icon and text | Compare the visible text-to-icon offset at four antialias cutoffs, the actual crops and the matching official specimen. The report expresses offsets in logical units and paint envelopes in device pixels. |
| Design decision | Use the applicable platform guidance and authoritative specimen to interpret the result. Record the renderer, theme and platform tested. Use native evidence for compositor, font fallback or platform-specific questions that headless rendering cannot settle. |

Keep reproducible behavior and layout regressions in the ordinary product suite. The optional review adds image evidence without starting a desktop window or placing platform-dependent full-screen comparisons in CI. [Design-system Note](../../.agents/notes/2026-11-18-mvp-design-systems.en.md)
