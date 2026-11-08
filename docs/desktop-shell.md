# Desktop CSV shell

TAU-8 hosts this per-chart workflow in the [dockable workspace](workspace.md). The
workspace guide describes current window lifetime, restoration and chart-host APIs.

TAU-4 owns file selection, loading/error state and application orchestration. It uses
the existing `IHistoricalBarReader`, `BarReadResult`, `BarDataset` and `BarChart`
contracts without changing them. The chart and Databento reader are separate modules.

## Launch and checks

Use a compatible .NET SDK on Windows or Linux as described in the
[developer guide](development.md#build-test-and-launch):

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet run --project src/TauTick.Desktop --no-build --no-restore
```

A new chart starts empty. Enter a known four-digit **Full contract year**, then
**Open CSV** opens the platform file picker and the actual Databento minute reader
parses the selected file. Use **2025** for the verified ESZ5 reference sample.
The year is an explicit import setting: a short symbol cannot distinguish decades.
It is never inferred from the filename, current year or recording timestamp.
Changing it does not relabel an already accepted dataset. It applies to the next
attempt and is disabled while selecting/loading. Invalid or missing years disable
Open. See the [adapter guide](../src/TauTick.Adapters.Databento/README.md) for supported CSV conventions.

Open is disabled from picker invocation until the attempt finishes. Canceling the
picker leaves accepted data intact. A successful read replaces the chart and its
instrument, timeframe, UTC coverage and source metadata in one UI turn. Failure
shows the first structured error (including available row/field context), retains
the previous chart/metadata, and enables another attempt. The coverage is a
start-inclusive/end-exclusive available range, not a claim of a complete session.
**Fit to data** fits the accepted snapshot; it also remains available while a
replacement is loading. The ES reference is partial-session data.

## Composition and lifetime

`App.OnFrameworkInitializationCompleted` creates `MainWindow` with a local workspace
store. Each `ChartDocument` owns its `CsvViewerSession`; docking recreates controls
without closing documents. Normal construction
selects a fresh `DatabentoMinuteCsvReader` for each attempt, using the explicit year.
`CsvViewerSession` captures its reader factory on the UI thread before selecting
the file, then runs the entire read on a worker. Passing an explicit reader to
`MainWindow` remains a development/test seam; the application contains no fake
reader or synthetic-data dependency. The native picker is the default;
`ICsvFilePicker` is the small shell-specific test seam.
`CsvFile` wraps a selected storage handle, its display
name and stream opener. It is not a second market-data model.

`MainWindow.Chart` remains a first-chart seam for legacy development hosts. Features
use `MainWindow.ChartHost` and stable document IDs. Each `BarChart` receives the exact accepted
dataset reference through `SetDataset`; no bars are copied into UI objects. A chart
replacement must preserve that shared input/clear API. No renderer internals or
provider parsing are implemented by the shell.

`CsvViewerSession` is owned by the Avalonia UI thread. Native selection runs there;
stream opening and the entire reader invocation run on a worker, including any
synchronous work before the reader returns its Task. The session owns and disposes
the stream only after reading actually ends. It then releases the selected storage
handle. Closing marks the session closed, releases the chart reference and requests
cancellation without running reader cancellation callbacks on the UI thread. Late
picker selections, read results and errors cannot update a closed chart document. A reader
that ignores cancellation must still finish before its borrowed stream is disposed;
the UI does not wait for it. Production readers must honor the shared cancellation
contract. Unexpected exceptions are reported distinctly from format failures and
are not logged with local paths or file contents.

## Focused and native validation

`tests/TauTick.Desktop.Tests` includes production-composition integration checks
using invented CSV bytes and the actual parser/chart, plus controlled readers and
selections through the published contracts. It covers success/replacement, metadata consistency,
picker cancellation, repeated Open, structured failures and recovery, missing and
unreadable files, unexpected exceptions, synchronous reader work, close during
selection/read, cancellation, ignored cancellation and resource ownership. These
are Avalonia.Headless tests, not native file-dialog validation. The production
checks additionally cover exact values and interval positions, invalid schema,
year configuration, failed replacement with retained viewport/metadata, fit,
control resizing and closing while real parser I/O awaits cancellation.

The current real-data/native checks, measurement mode and outstanding Windows
interactive checks are in [mvp-validation.md](mvp-validation.md). The following
legacy scenario mode and platform record describe the earlier shell-only checks.

The separate native host composes the same shell and native picker with a controlled
reader. It is intentionally outside the solution's ordinary test run:

```sh
dotnet restore tests/TauTick.Desktop.Native --locked-mode
dotnet run --project tests/TauTick.Desktop.Native --no-restore
```

Use a graphical desktop (X11/XWayland on Linux) and a disposable synthetic CSV
outside the repository. The host ignores its contents and never logs its path.

1. Open the picker and cancel; verify the empty/current chart is unchanged.
2. Select the file. The first read performs two seconds of deliberately synchronous
   worker work, then succeeds. Verify loading, disabled Open, metadata and candles.
3. Select it again. The second read returns a synthetic validation error at row 7,
   field `volume`; verify the chart and metadata remain and Open re-enables.
4. Select it a third time; success clears the error. Resize the window and verify
   both the metadata and chart remain usable.
5. Select it a fourth time. This read waits until cancellation. Resize/move the window
   during loading, then close it. Require prompt close and exit code 0.

For an isolated close-during-read check, append `-- --close-during-read` to the run
command; then the first read waits until canceled by closing the window.

The host checks chart/metadata consistency and retained data on each transition,
and checks cancellation/clearing on close. Its console reports `ACCEPTED`,
`FAILURE_RETAINED`, `CLOSED` and window sizes. This is shell validation with synthetic
data, not production CSV integration or a performance benchmark.

## Platform record

On 2026-11-06, .NET SDK 10.0.114 and Avalonia 12.1.5:

- Ubuntu 22.04.5 x64 under WSLg/XWayland, scale 1: exercised the GTK native picker,
  cancellation, accepted data/metadata, contextual failure with retained chart,
  successful retry, maximize/restore (1000×680 to 1908×1119 client size), animated
  loading and resize during a synchronous worker read. The isolated indefinite-read
  check closed through the title-bar button, verified cancellation and exited 0.
- Windows desktop, scale 1: exercised native picker cancellation and selection,
  visible loading/disabled Open, successful data/metadata delivery, maximize
  (1000×680 to 1920×1129 client size), and native close. The host reported `CLOSED`
  and the process exited without error output.
- Locked restore, Debug build and all 160 tests (including 16 shell tests) passed
  on both platforms. Build
  outputs were placed in separate external artifact directories to avoid sharing
  Windows/Linux intermediates. The native validation host was also built on both.

The initial WSLg session emitted GTK/dconf warnings about its missing settings bus.
The later isolated run used the process-local `GSETTINGS_BACKEND=memory`; no system
settings were changed. Headless tests and these synthetic native checks do not
establish production CSV integration, general Linux desktop/portal behavior,
Wayland-only support or performance capacity. High-DPI/mixed-DPI, minimum-size
visual checks, the full Windows failure/recovery/close-during-read matrix and real
reader/file-dialog edge cases remain for final integration. No market data was used.
