# TAU-8 workspace validation

Recorded 2026-11-08. These results apply to the workspace implementation, not the
older CSV viewer's Windows acceptance record. Cross-platform interactive acceptance
is **not complete**.

## Automated checks

Pinned SDK 10.0.114; Avalonia and Dock 12.1.5. Locked solution restore and Release
build passed with zero warnings/errors. The affected suites pass after corrections:

| Suite | Passing tests |
| --- | ---: |
| MarketData | 25 |
| Databento adapter | 99 |
| Charting | 42 |
| Desktop | 32 |
| Total | 198 |

The final full Release solution test run passed all 198 tests. The final Release
solution build passed. The standalone desktop native host's locked
restore and Release build passed. The native X11 smoke script mapped the 1200×800
main window and normal `WM_DELETE_WINDOW` shutdown returned exit code 0; it now uses
an isolated settings profile. No new performance/FPS measurements were made.

Workspace tests use invented CSV bytes and temporary files. Coverage includes:

- Same document/dataset/range/settings across split, tab, chart float and whole
  workspace moves; actual close notifications and participant disposal.
- Active-chart targeting, including an inactive workspace whose last active chart
  is floating across save/restart; queued presentation activation cannot replace the
  saved target, and closing during layout presentation cannot reactivate disposed
  charts; local fit/error retention, independent cancellation and
  ignored late results; native-host close policy and empty host collections after
  application close.
- Tab order, split proportions, active identity, floating layouts, input hashes and
  settings round trips; redocking restored chart/workspace hosts removes the host.
- Missing/changed CSV refusal and explicit replacement with a new contract year;
  queued input references and rejected feature settings survive a save.
- Unsupported view type, corrupt JSON, invalid containment, duplicate ownership,
  invalid active index and retained backup file; absent-monitor/scaling geometry
  calculations. Simulated screen tests are not physical monitor evidence.

Existing navigation/inspection, exact-value CSV, picker cancellation, failed
replacement retention and reader/resource tests remain in place. Adapter parsing
and MarketData invariants were not changed.

## Native Linux environment and observations

Ubuntu 24.04.5 x64, Linux 6.8.0-147-generic, Avalonia X11/Skia with software rendering,
Openbox, private Xvfb screen 2400×1800×24, separate D-Bus session and temporary XDG
profile. No physical monitors were attached to this session. AT-SPI located ordinary
controls; XTEST supplied pointer/keyboard input and screenshots were inspected.
Some moved Dock tab peers were absent or had zero bounds in AT-SPI; visible Dock
chrome and current native window bounds were used for those drag gestures. This
was not a browser or Avalonia.Headless run.

Two chart documents opened the real public reference independently via native GTK
pickers with explicit year 2025: ESZ5, 960 M1 bars, coverage 2025-09-22 [00:00,16:00)
UTC. The external file was verified as 107363 bytes, SHA-256
`cab4d4c81f7c8ac1ae1c109614f892ac22886ea14f49cbdbbeb68aaa1309d1d7`.
Market data, profiles, disposable scripts and captures stayed outside Git.

At scale 1, the following were directly exercised:

- Two independent real charts; horizontal splits, splitter resize, pan/zoom and
  chart inspection; vertical split. Native center-target dragging regrouped both
  charts into one tab group after the destination pane highlighted. Dragging a tab
  along that strip reordered the charts; saved child IDs confirmed both operations.
- Explicit chart detach/redock; native tab drag-out; visible edge targets and
  dragging back into a split. Both accepted charts remained visible and usable.
- Native file picker in a detached chart, cancellation, and focus via Tab/Return.
  After regrouping/reordering, Ctrl+0 fitted the selected chart while the sibling
  retained its exact zoomed range; Ctrl+O opened the GTK picker and Escape cancelled
  it. These shortcuts also worked after detaching the chart.
  A synthetic malformed replacement produced a row/header error on one chart while
  both retained their real data; the sibling had no error.
- Whole-workspace detach retaining two panes, native maximize to 2400×1781 client
  area, save/normal main-window close (exit 0), restart with the same chart IDs,
  references, internal split and maximized floating window, then redock.
- Workspace rename to Research, creating another workspace and reordering it ahead
  of Research. Keyboard Open/Fit commands and normal shutdown were exercised.
- Restore activation was checked with an external seeded layout containing two
  verified real inputs: Research kept chart 2 floating and remembered it as its
  active chart while Workspace 2 was selected globally. Startup kept the main
  window active, and a subsequent save preserved both targets. After explicitly
  activating the floating chart and closing via `WM_DELETE_WINDOW` without
  refocusing the main window, restart activated that floating native window.

Native restart/redock exposed a restored floating-root collapse issue; it was
corrected and rechecked natively and in automated tests. A restored workspace now
redocks with no leftover blank native host. Main-window close also removed its
floating hosts and exited normally. A later restart/redock check also retained
both exact saved chart ranges and input references; normal shutdown returned 0
and left no native application frames.

At `AVALONIA_GLOBAL_SCALE_FACTOR=2`, the restored two-chart workspace, chart detach,
native maximize, hover crosshair/UTC-OHLCV readout, redock, resize to 1800×1400
client pixels and normal close were also exercised. This is an Avalonia scale
override on a virtual screen, not a physical high-DPI or mixed-monitor test. GTK
was not scaled by that override.

Captures and the disposable driver for this run are under
`/tmp/tautick-tau8-native/` on the implementation machine. They are ephemeral local
evidence, not portable fixtures or a dependency of tests.

## Outstanding acceptance work

- Windows native workflow has not been run for TAU-8. The earlier MVP's Windows
  results cannot establish workspace docking/window compatibility.
- Physical multi-monitor moves, unplugged monitors, mixed physical DPI and
  transitions between display scales are unavailable here. Reachability unit
  tests and virtual-screen checks do not substitute for them.
- A full Linux desktop/Wayland or portal picker session and GPU/presented-frame
  performance have not been verified by this Xvfb run.
- Exact current ATAS X gestures/build were not run locally; the dated primary
  references and deliberate TauTick choices are documented separately in
  [workspace-reference.md](workspace-reference.md).

Use the [acceptance walkthrough](workspace.md#controls-and-acceptance-walkthrough)
on those environments before declaring complete cross-platform acceptance.
