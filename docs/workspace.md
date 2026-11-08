# Dockable candle workspace

TAU-8 adds named workspaces around the existing CSV/candle viewer. A workspace owns
its chart documents and internal tab/split layout. Detaching a workspace carries
that layout into another native window of the same application. The selected
library, ATAS X evidence and deliberate differences are in
[workspace-reference.md](workspace-reference.md).

## Controls and acceptance walkthrough

Build/run with a compatible SDK from the [developer guide](development.md#build-test-and-launch)
and locked dependencies:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx -c Release --no-restore
dotnet test TauTick.slnx -c Release --no-build --no-restore
dotnet run --project src/TauTick.Desktop -c Release --no-build --no-restore
```

1. In the initial chart enter the explicit full contract year and choose **Open
   CSV…**. Use **2025** for the public ESZ5 reference in the
   [sample-data guide](sample-data/README.md). Choose **+ Chart** and open another
   CSV. These are separate documents even when they reference the same file.
2. Pan with primary-button drag, wheel to zoom, hover to inspect exact UTC/OHLCV,
   and choose **Fit** to reset that chart. **Ctrl+O** opens the picker and **Ctrl+0**
   fits while focus is within that chart. Tab moves focus through ordinary controls.
3. Choose **Chart ▾ → Split right / Split below** to move the selected chart into
   a neighboring pane. Drag the splitter to resize. Drag chart tabs onto the
   visible center target to tab together, an edge target to split, or along the
   tab strip to reorder. Dock targets appear during a drag; move onto the center
   square until the destination pane is highlighted before releasing to tab together.
4. Drag a chart tab out of the window, or choose **Detach chart**. Move/resize or
   maximize the native window. Open/cancel a file picker in this window and try a
   malformed replacement: only this chart reports the error, retaining its previous
   accepted data and viewport. Use **Dock back**, or drag its tab onto an appropriate
   target in its workspace. Explicit Dock back selects the first tab group.
5. Use **+ Workspace** for another independent layout. Edit the workspace name and
   press Enter or leave the field. Reorder by dragging tabs, or use **Workspace ▾ →
   Move tab left / right**. Detach the whole workspace with its menu or tab drag.
   Its internal layout and chart state travel together. Use **Dock workspace back**
   or drag its workspace tab onto the main workspace strip. Workspace tabs stay as
   tabs; chart splits belong inside a workspace. Charts dock within their owning
   workspace, including its floating chart windows.
6. Choose **Save layout**, close the main window, and relaunch. Verify names/order,
   active charts, splits, viewport and floating windows. Close one chart and confirm
   its siblings still work. Close a detached workspace and confirm its charts close.
7. With the app closed, move a referenced CSV away or change its bytes. Relaunch:
   the affected chart explains the missing/changed input; it does not silently load
   the replacement. Restore the original file and relaunch, or explicitly Open CSV
   to accept new input. Test absent monitors on actual hardware when available.

Metadata describes available coverage, not necessarily a complete trading session.
All data and axes retain UTC semantics. Changing the year affects the next import,
not an accepted dataset. Instruments/timezones, linked charts and indicator controls
are outside this task.

## Lifetime and close policy

`ChartDocument` owns the import session, immutable accepted dataset, current viewport,
explicit year and feature settings. A Dock `Document` presents a rebuildable
`ChartView`. Detach, redock, regroup and workspace moves can recreate the control;
they do not reimport data, cancel a load, or send the final chart-close notification.
The new surface receives the same accepted dataset reference and saved time range.
Hover inspection and pointer capture are transient surface interactions.

Closing a chart tab is final: it cancels only that chart's pending load, rejects
late results, releases its dataset/control references, disposes registered features,
and sends `Closed` once. Closing a floating window closes its contained chart(s),
or the contained workspace and all of its charts, including its separately floating
charts. Use Dock back when the intent is to keep them. Closing the main window saves
the whole arrangement first, then closes every document and floating host. A save
failure leaves it open with retry and **Close without saving** actions.

Inputs load/verify on workers. Picker selection and state publication stay on the UI
thread. Each picker obtains the current native window from its attached chart view.
Closing during selection rejects/disposes the eventual selection. A reader that
ignores cancellation still owns its borrowed stream until it returns; it cannot
publish into a closed document.

## Restoration and format

The app saves `TauTick/workspace-v1.json` under .NET's `LocalApplicationData`
(typically `$XDG_DATA_HOME` or `~/.local/share` on Linux, `%LOCALAPPDATA%` on Windows).
Save is explicit and also occurs on normal main-window close. It is not continuous
crash recovery or an experiment store. Writes use a same-directory temporary file
and atomic replacement; the previous file becomes `.backup`.

Version 1 contains stable workspace/chart GUIDs, a restricted root/tab/split tree,
tab order and active indexes, each workspace’s active chart across docked/floating
windows, proportions, native window
positions/sizes/maximized state, chart year/range, and feature settings. No Dock
runtime types are serialized. Bounds use desktop pixel positions and logical window
sizes. Restored geometry is clamped to available screen work areas; an absent monitor
falls back to the primary available screen. Physical mixed-DPI behavior still needs
hardware validation.

Restored floating windows are presented without activating each in turn. Once the
queued layout presentation completes, the saved global chart/window is activated.
CSV verification then runs independently, so later user focus changes are not
replaced when an input finishes loading.

CSV references contain an absolute local path, length and SHA-256. A worker hashes
and parses the same private temporary snapshot, avoiding a verify/reopen race and
an extra whole-file memory copy. The spool is deleted on close. Market data is never
embedded in the layout or committed to Git. Layout files contain local paths and
should be treated as personal settings, not shared project fixtures.

Restart verifies each reference before parsing, using the accepted import's full
year. Failed verification retains the reference/year/range for another restart;
explicit Open CSV is how a user accepts changed data. Charts awaiting startup
verification cannot start a competing import. An invalid saved range fits verified
data with an explanation. Invalid JSON, unsupported format versions, duplicate
owners and malformed/oversized trees open a safe workspace with an explanation.
Unsupported chart types are skipped with a notice; supported charts remain usable
and the previous file is backed up on save. Unknown feature settings remain in the
supported chart's state until their feature registers. Settings rejected by a
participant are retained rather than overwritten by defaults.

## Chart-host handoff for TAU-9

The public API is [IChartHost.cs](../src/TauTick.Desktop/Workspace/IChartHost.cs),
available as `MainWindow.ChartHost`. Access it on the Avalonia UI thread:

- `Charts` lists live chart documents. A document's `Id` is stable across control
  recreation and restart. `Dataset`, `VisibleTimeRange`, `DatasetChanged` and
  `Changed` describe document state, independently of Dock objects.
- `ActiveChart`, `ActiveChartChanged` and `ActivateChart(id)` target the focused or
  selected chart across native windows. Commands should resolve `ActiveChart` at
  invocation time; a missing/closed ID cannot be activated.
- `FitToData()` changes only that document. `Closed` is final disposal, never a
  move notification. Unsubscribe external observers at final close.
- Register one `IChartSettingsParticipant` per unique feature key. `Capture()`
  returns a version plus JSON value; `Restore()` receives the prior value when
  present. The feature owns migrations/validation of its payload. The document
  owns participant disposal on actual close. Unknown or unapplied values are
  preserved. Capture failures abort the save and surface the retry/close choice.

TAU-9 owns indicator management, calculation/rendering and indicator-specific
payloads. It should consume this host/document lifetime rather than creating a
second docking system. No indicator implementation or speculative module is added
here. The only chart-control extension is a public validated time-range value,
restore method and change event; parsing and market invariants remain unchanged.

## Validation record

See [workspace-validation.md](workspace-validation.md) for checks actually executed,
native environment details and outstanding acceptance work. Automated tests use
clearly identified synthetic prices/CSV bytes and temporary files outside Git.
The developer guide's [native Linux procedure](development.md#native-visual-checks-on-linux)
provides isolated Xvfb, D-Bus, profile and capture commands. The earlier
[MVP measurements](mvp-validation.md) are historical and are not measurements of
this workspace implementation.
