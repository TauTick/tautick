# Workspace windows and content tabs

TauTick has one active **workspace**: the complete arrangement of its native
windows and their contents. Each window owns an ordered strip of **content tabs**.
A content tab is empty or contains independently configured charts, chart tab groups
and split panes. Named saved layouts replace the whole active arrangement when
loaded. TAU-11 supersedes TAU-8's independently active workspace tabs.

## Controls and acceptance walkthrough

Build/run with the [developer guide](development.md#build-test-and-launch):

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet run --project src/TauTick.Desktop --no-build --no-restore
```

1. The initial content tab contains an unloaded candle chart. Enter the full
   contract year before **Open CSV…**; use **2025** for the public ESZ5 reference in
   the [sample-data guide](sample-data/README.md). Pan by dragging, wheel to zoom,
   hover to inspect UTC/OHLCV and choose **Fit** to restore the accepted range.
   **Ctrl+O**, **Ctrl+0** and **Ctrl+I** target the focused chart's picker, Fit and
   indicator manager. Indicators use the [existing study capabilities](indicators.md).
2. Click **+** in a window's top-level tab strip. It creates and selects an empty
   content tab with no copied chart, dataset, year or settings. Edit its name in
   the field below the strip; Enter or leaving the field applies the name.
   **+ Chart** adds an independently configured candle chart to that tab.
3. Create and name **A | B | C | +**. Drag **C** outside all application windows
   and release: a second ordinary native window opens with **C | +**. Its **+**
   creates empty **D**. Drag **B** from the first window onto the gap between C
   and D: the result is **C | B | D | +**, with **A | +** in the first window.
   Drag D to the beginning for **D | C | B | +**, then move B back.
4. The entire destination strip accepts a content tab: the first/second half of
   a header, insertion gaps, the + area and unused space. A blue marker shows the
   insertion boundary. Drop in either direction, including into a window that has
   only an empty tab. Other tabs retain their order. Drag along the same strip to
   reorder. **Escape**, lost pointer capture, or releasing over ordinary application
   content cancels the transfer. Ownership and order change only on a valid release.
   **Detach tab** is an additional keyboard-accessible tear-off action.
5. Inside a populated content tab, **+ Chart** creates a sibling chart. **Chart ▾ →
   Split right / Split below** creates resizable panes. Use Dock's chart targets to
   group or split charts inside that content tab. Native window creation belongs
   to the top-level content strip; the inner chart tabs remain in their content tab.
   Moving the content tab transfers its whole internal arrangement.
6. Enter a workspace name in the application row and choose **Save layout**. Save
   another arrangement under a different name. Select a saved name and choose
   **Load layout** from any workspace window: all prior content/windows are replaced.
   The invoking window is reused, and the saved additional windows are reconstructed.
7. Choose **Exit application** to save the complete current arrangement and close
   all application windows. Relaunch and verify membership/order, empty tabs,
   selected tabs/charts, chart input/settings/range and window geometry. Closing
   one window through its title bar instead closes only that window and its content.
8. With the app closed, move or alter a referenced CSV. Restart or load the saved
   layout: that chart explains the unavailable/changed input without silently
   accepting replacement bytes. Restore the original input or explicitly Open CSV
   with the intended full year. Check absent monitors on physical hardware when
   available; restored bounds are clamped to available screens.

## Ownership, commands and close policy

A workspace window owns content tabs; a content tab owns its chart documents and
restricted Dock layout. A move transfers the same tab and chart documents, with
stable identities, accepted dataset references, explicit year, viewport, settings,
indicators and any pending imports. Native host controls may be rebuilt. Moving
never imports again, cancels work, disposes studies or emits a final chart close.
Window targeting comes from current workspace ownership, independently of a
transient chart surface. Indicator manager windows close on transfer and reopen
against the receiving host; as elsewhere, unapplied editor fields are discarded.

Selecting an empty tab clears the active chart. Activating a native window targets
its selected tab's last active chart. A chart's own Open/Fit/Indicators actions
resolve that document; file pickers resolve its current native window.

Closing a chart is final and affects only that document. Closing a content tab
closes all of its charts. Closing or transferring a window's last content tab
leaves a new empty tab, retaining a usable window and +. Closing a native window
closes only its content; other windows remain usable. Closing the last native
window saves the remaining active arrangement and exits. **Exit application** saves
all currently open windows before closing the whole application.

Actual close cancels pending work, rejects late results, releases data/control
references, disposes registered features and sends each document's `Closed` once.
A failed final save keeps the window open with retry and **Close without saving**.
A picker that returns after final close cannot publish a selection. A reader that
ignores cancellation retains its borrowed stream until it returns.

## Named layouts, restoration and migration

The active arrangement is stored at `TauTick/workspace-v1.json` under .NET's
`LocalApplicationData` (`$XDG_DATA_HOME` or `~/.local/share` on Linux and
`%LOCALAPPDATA%` on Windows). The filename is retained for compatibility; its current
schema is **version 2**. Explicit **Save layout** also writes a named snapshot in
`TauTick/layouts/`. Names are kept in the snapshot; filenames use a hash of the
name, so Unicode and path punctuation do not become filesystem paths. Automatic
close/exit saves update the active snapshot, leaving explicitly named snapshots
available for later loading. Save/load operations are coordinated across windows.
This is local layout persistence, not continuous crash recovery or experiment storage.

Version 2 records the workspace name, stable window/tab/chart identities, each
window's ordered tabs and selection, the active window/chart, normal geometry and
maximized state, empty tabs, each tab's root/tab/split tree, proportions, focused
chart and chart input/settings state. Dock runtime objects and market data are
never serialized. Positions use desktop pixels and sizes use logical units;
restored geometry is clamped to available screen work areas.

Compatible version 1 files migrate on load: former workspace pages become content
tabs in the corresponding window, and separately floating chart layouts become
content tabs in ordinary windows. Chart identities, settings, references and
internal layouts are retained. The source file is unchanged until save. Saves use
a same-directory temporary file and atomic replacement; the prior file becomes
`.backup`. Corrupt/unsupported layouts retain their source and produce a recoverable
explanation; a named load failure keeps the current arrangement.

Saved CSV references contain an absolute local path, byte length and SHA-256.
Verification and parsing use the same private temporary snapshot on a worker.
Missing/changed inputs retain the saved reference, accepted year and range for
recovery; explicit Open CSV accepts replacement input. Unknown study settings are
retained until the feature can restore them. Unsupported chart types are skipped
with an explanation while supported content remains usable. Invalid viewport
ranges fit verified data with a notice. Layout files include personal local paths.

## Chart-host interface

[IChartHost.cs](../src/TauTick.Desktop/Workspace/IChartHost.cs), exposed as
`MainWindow.ChartHost`, is an Avalonia UI-thread contract shared by all workspace
windows. `Charts` lists live documents. `ActiveChart`, `ActiveChartChanged` and
`ActivateChart(id)` target the selected/focused chart across windows.

`IChartDocument.Id`, `Dataset`, `VisibleTimeRange`, `Changed` and `DatasetChanged`
belong to the document, independently of its control. `FitToData` affects that
chart only. `Closed` denotes final disposal. A unique-key `IChartSettingsParticipant`
contributes versioned JSON through Capture/Restore and is disposed at final close.
Unknown or rejected settings are preserved; capture failures abort save. Study
calculations and source adapters remain independent of the workspace/drawing code.

See [workspace-validation.md](workspace-validation.md) for measured checks and
remaining platform acceptance. Tests use identified synthetic fixtures; downloaded
inputs, native captures and disposable drivers stay outside Git.
