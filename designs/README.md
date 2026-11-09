# TauTick design references

This folder contains the concept images retained after manual review. They are references for the primary chart workspace, not a finalized design system.

The clearest stated preference is **[00-first-attempt.png](00-first-attempt.png)**, followed by a request to make that design slightly darker. **[05-primary-workspace-reference-darker.png](05-primary-workspace-reference-darker.png)** explores that refinement. Keeping an image does not mean every detail in it has been approved.

## Reference images

| Image | Role | Useful characteristics |
| --- | --- | --- |
| [00 — First attempt](00-first-attempt.png) | Main reference explicitly preferred by the user. | Dense desktop structure, separate workspace and chart tabs, compact outlined controls, copper selections, and a sectioned right inspector. |
| [05 — Darker reference](05-primary-workspace-reference-darker.png) | Darker refinement of 00; a candidate for further review. | Preserves the original layout and control language while deepening the chart, chrome and inspector surfaces. |
| [01 — Primary workspace](01-primary-workspace-fresh.png) | Retained comparison. | Quieter toolbar, more open inspector, copper emphasis and a large chart. Its surfaces were considered too light. |
| [02 — Dark blue](02-primary-workspace-dark-blue.png) | Retained alternative palette/composition study. | Near-black surfaces, compact header and cyan-blue interface accents. Further requests for modernization followed this version. |

Images 00, 01 and 02 are **1586 × 992** pixels; 05 is **1585 × 992** pixels. Filenames identify explorations, not an approval order.

## Chart research workflow concept

[06 — Chart research workflow](06-chart-research-workflow.png) is a **1536 × 1024** static design study extending the dark desktop direction above. It shows a selected VWAP-touch interval, separate preceding preparation history, candle/footprint presentation, price/trade/MBO coverage lanes, action readiness, and preserved indicator settings. The lower annotation strip depicts selection gestures, simulated preparation, a conceptual return to the chart, and an alternate selection with missing detail.

The alternate state retains price candles and summary-backed session VWAP while marking the range volume profile and MBO replay unavailable. Preparation and any future replay/evaluation path are illustrative; no replay or strategy was executed.

Created with the built-in image generation tool. The final raster was visually inspected on Windows. This is **not a native Avalonia capture**: no native interactions, responsiveness, selection logic, or Windows/Linux acceptance were tested. Plotted values, timestamp positioning, footprint rows, and coverage alignment are illustrative rather than validated fixtures. No feature implementation accompanies this image, and it does not establish approval of new design choices.

## Direction to carry forward

- **Start with the structure of 00 and compare the darker surfaces in 05.** Keep the chart dominant, the tool rail compact and the inspector on the right. Preserve the distinction between global commands, workspace tabs, chart tabs and chart-local actions.
- **Make the first scheme dark.** Use deep graphite or blue-charcoal with enough difference between the chart, surrounding chrome, inspector and raised controls. Maintain readable axes, labels and icons as surfaces darken.
- **Keep the precise desktop control language.** Compact selectors, restrained corner radii, coherent line icons and clear selected states are part of the preferred reference. Modernization should improve typography, spacing, edges and surface finish.
- **Use color with clear roles.** Copper is the interface accent in the main reference. Blue/cyan remains a possible alternative scheme; it is not a settled replacement. Teal and coral communicate market direction, and VWAP uses a quiet neutral.
- **Aim for clean matte surfaces.** Avoid cloudy texture, decorative lighting, gloss and beveled highlights visible in some generated details. Keep useful control boundaries and section separators, with consistent, understated strokes.
- **Treat typography as working UI.** Use a contemporary readable sans-serif, tabular price numerals, consistent precision and deliberate differences in size and weight. Essential small text must remain clear.

## Feedback to preserve

- Standalone colored **dots as selection indicators were explicitly rejected**. Use the reference's selected surfaces, underlines and control outlines.
- A pale full-height navigation column and replacing the right inspector with a bottom console were rejected. They are outside the current preferred direction.
- Earlier attempts to look more modern did not satisfy the user. Modernity remains an open design problem; these retained images do not establish a finished solution.
- The user liked the darkness of T3 Code and expressed interest in cyan-blue accents, while asking for TauTick to retain its own identity.
- Multiple color schemes may be explored later. The immediate reference is a darker version of the preferred desktop workspace.

## Using these references

Inspect the relevant retained image before making a visual change. Preserve existing files and use a unique filename for each new concept. Produce one concept at a time and stop for feedback unless a broader set is requested. Deleted explorations are not part of this reference set.

The charts show **synthetic data**. These raster images establish visual intent; their text, plotted values, candle/volume alignment and indicator placement are not validated rendering fixtures. Exact palette values, fonts, dimensions and interaction states still need to be defined and checked in the native UI.

For implementation, use the [shared UI library](../src/TauTick.UI/README.md) and [native gallery](../samples/TauTick.UI.Gallery/README.md), following their local guidance. Current scope and terminology remain in the repository [README](../README.md) and [glossary](../GLOSSARY.md).
