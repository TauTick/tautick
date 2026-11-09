# TAU-11 window/tab validation

Recorded 2026-11-09. TAU-11 replaces the independently active workspace-tab model
with one workspace containing native windows and movable content tabs. The
[walkthrough](workspace.md) describes the current behavior. Cross-platform native
acceptance remains incomplete because Windows was unavailable.

## Automated checks

.NET SDK 10.0.114, Avalonia/Dock 12.1.5, Ubuntu 24.04 x64. Locked solution restore,
Debug solution build and Release solution build passed with zero warnings/errors.
The full Release run passed **219 tests**: MarketData 25, adapter 99, Charting 42,
Desktop 47 and UI 6. After adding three legacy-name recovery regression cases,
the affected Desktop suite passed **50 tests in both Debug and Release**; the
other suites were unchanged.

Focused checks cover:

- Empty tab creation and active-chart clearing; cross-window insertion and sibling
  ordering, same-window reorder and last-tab replacement.
- Stable chart/document/dataset identity, explicit year, viewport, settings and
  pane proportions across transfers and host recreation. Moves preserve pending
  import tokens; actual close cancels only affected work and rejects late results.
- Receiving-window command/picker ownership, normal tab clicks after restoration,
  routed Escape cancellation, and ordinary/empty/native-window close policies.
- Named layout replacement from a receiving window; prior document disposal and
  host removal; restoration of window/tab membership, selection, empty tabs,
  chart inputs/settings/ranges and internal panes.
- Compatible v1 migration, including separately floating charts, with source-file
  preservation until save and backup retention. Invalid containment, duplicate
  ownership, invalid selection, corrupt JSON, unsupported chart types, missing or
  changed inputs, and rejected/unavailable feature settings remain recoverable.
- Indicator integration: study identity/configuration and verified CSV inputs
  survive transfer and persistence. Tests retain identified invented CSV/bar
  fixtures; they do not use downloaded market data.

No new performance or physical monitor measurements were made. The older TAU-8
and CSV viewer results below are historical, rather than current platform evidence.

## Native Linux checks

Ubuntu 24.04 x64, Linux 6.8.0-147-generic, Avalonia X11/Skia software rendering,
Openbox, private 2400×1800×24 Xvfb screens, separate D-Bus sessions and temporary
XDG profiles. Application scale was 1. AT-SPI identified ordinary controls;
XTEST supplied real keyboard/pointer input. Native captures were opened and
inspected. This was the actual desktop application, not Avalonia.Headless.

Observed:

- Real A/B/C drag sequence: C tore off into a normal window with +; + created
  empty D; B inserted between C and D; D reordered to the front; B moved back.
  Saved membership was A/B in the first window and D/C in the second.
- Beginning/header/gap and free-strip drops in both directions, including a third
  window containing only an empty tab. Insertion feedback appeared during drags.
  Escape cancellation kept tab identities and membership/order unchanged.
- An explicitly synthetic three-bar CSV loaded through the native GTK picker with
  year 2025. VWAP was added using the indicator manager; wheel zoom changed the
  viewport. After a populated-tab drag, saved chart identity, input reference,
  year, range and indicator settings were exactly equal to the pre-move state.
- A two-chart horizontal split was created through native controls. Its saved
  tree and both chart descriptors remained identical after moving the content tab
  to another window; both panes were inspected in the receiving host.
- The receiving chart opened/cancelled a GTK picker and reopened its indicator
  manager. X11 transient-parent hints associated both with the receiving native
  window. Ordinary tab selection continued to expose the intended chart.
- Two differently named layouts saved and loaded through application controls.
  The first removed the second's Extra tab; the second restored it. Prior native
  hosts were replaced, with two windows remaining after each load.
- Exit application returned process code 0. Restart restored both windows, tab
  membership/selection, the synthetic chart, input reference, zoom and VWAP settings.
  Saved chart state matched the prior named layout. Closing an empty third native
  host left its two sibling windows alive; Exit application then closed them.
- A malformed v1 file containing a floating chart and a null tab name opened
  a safe workspace with a recoverable explanation. Its bytes remained unchanged
  until save, then were retained in .backup; normal exit returned 0. Regression
  cases also cover empty and whitespace legacy names.
- The native gallery's toolbar scene used the same production tab strip. Selection,
  +, real drag reorder and keyboard activation were exercised. Changing its AXAML
  minimum height from 38 to 44 was observed in the same process, then restored;
  + and keyboard input still worked after reload. Gallery close returned 0.

Disposable profiles, drivers, saved snapshots and captures are in
`/tmp/tautick-tau11-native/`, `/tmp/tautick-tau11-gallery/` and
`/tmp/tautick-tau11-recovery/` on this machine.
They are ephemeral evidence, outside Git and independent of deterministic tests.

## Outstanding checks

- Windows native tab drags, focus/dialogs, save/load and close behavior were not
  available in this environment. No Windows or complete cross-platform acceptance
  is claimed.
- Physical multi-monitor, unavailable physical monitor, high/mixed physical DPI,
  Wayland/portal and GPU/presented-frame performance checks remain unverified.
  Automated reachability tests and one-scale Xvfb input do not establish these.

---

# Historical TAU-8 workspace validation

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
