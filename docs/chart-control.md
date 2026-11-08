# Candle chart control

`TauTick.Charting.BarChart` draws an immutable `BarDataset` as candles with price
and UTC time axes. It uses Avalonia 12.1.5's `DrawingContext` on the application's
Skia backend. It has no adapter dependency, file access or market calculations.

## Public usage and lifetime

Create the control in an Avalonia application and give it layout space, for example
as a stretching child of a `Grid`. Use the existing foundation API on the UI thread:

```csharp
var chart = new BarChart();
chart.SetDataset(accepted.Dataset); // BarReadResult.Success from the application
chart.SetDataset(replacement);     // Replaces the snapshot and fits the entire dataset
chart.ClearDataset();              // Idempotent; removes candles and axes
chart.FitToData();                 // Fits the accepted dataset; no-op when clear
```

`Dataset` remains the original immutable snapshot. Null replacement throws without
discarding it. The control owns no disposable dataset resources. It displays
"No bar data" when clear and an enlargement message when there is insufficient
space for readable axes. The application supplies instrument/provenance information
outside the chart, as established by the foundation contract.

The desktop's existing fit action and a primary-button double-click in the plot
restore fit-to-data. Replacement always fits the new snapshot and releases the old
inspection and pointer capture. The internal time-range setter remains a test and
measurement seam; the public dataset handoff is unchanged.

## Navigation and candle inspection

- Drag the primary mouse button inside the plot to pan time horizontally. Pointer
  capture keeps the drag active outside the plot/window until release. Panning is
  bounded by dataset coverage. A fully fitted range cannot pan further.
- Turn the wheel inside the plot to zoom around the time under the pointer. Each
  vertical wheel unit scales the span by `1.2` (up zooms in). The anchor is preserved
  to timestamp resolution unless coverage clamping requires moving it. The range
  spans at least one bar interval and at most the full coverage, including gaps.
- Double-click inside the plot, or use the desktop's **Fit to data**, to reset.
  The fitted dataset-wide price scale stays fixed while navigating; vertical
  navigation and visible-range price scaling are outside this increment.
- Move within the plot for a crosshair and exact UTC start/OHLC/volume readout.
  A bar owns `[StartUtc, EndUtc)`: its start is included, its end is excluded.
  Missing intervals show the crosshair without invented candle values. Known zero
  volume is displayed as `0`; a gap has no readout. Time always retains all seven
  fractional digits (100 ns), decimals are not rounded, and volume stays an exact
  integer. The panel wraps within the chart; exceptionally small space prompts
  enlargement instead of silently cutting off values.
- Axis gutters do not initiate gestures. Leaving the plot hides inspection while
  an existing drag can continue. Release, capture loss, reset, replacement, clear
  and detachment end capture. Resize and display-scale changes cancel a drag and
  hide inspection; the next pointer move uses the new logical coordinates.

Navigation never resamples, fills missing intervals or changes bar boundaries.
`ChartNavigation` performs bounded relative tick arithmetic independently of input
and drawing. Inspection binary-searches actual interval coordinates in O(log n).
Price extrema are computed once at assignment; reset reuses that fitted viewport.
Pointer-only updates invalidate drawing without rebuilding `ChartLayout`, candle
geometry, axis labels or pens. Readout text is reshaped only when its bar or layout
changes. Framework drawing/recording can still allocate and redraw cached content.

The composed real CSV workflow is recorded in the [integration report](mvp-validation.md).
Navigation results and the historical renderer evidence are recorded separately below.

## Drawing and coordinate decisions

- A bar occupies its actual `[StartUtc, EndUtc)` interval. Its candle is centered
  on that interval; missing intervals remain gaps. Time-axis ticks denote exact
  UTC instants, not ordinal bar indices. Neither data nor timeframe is changed.
- Rising bodies/wicks are green, falling ones red, and doji gold. Bodies connect
  open and close; wicks connect high and low. Body width is 70% of interval width,
  capped at 24 logical pixels, with a one-device-pixel minimum. At dense scales
  subpixel intervals inevitably overlap; this is drawing, not aggregation.
- Minimum-height bodies, including flat doji, cover one complete device pixel.
  This shifts their center by less than one device pixel. Other geometry retains
  continuous coordinates. Flat datasets have one price tick at the actual price
  and a centered candle; there is no invented price range in the model.
- `ChartViewport`, `ChartCoordinates`, and binary visible-range lookup are separate
  from drawing and contain no Avalonia types. Prices remain `decimal`; differences
  are calculated before conversion to drawing coordinates, preserving small price
  movements at large offsets. Time math uses relative ticks, including 100 ns inputs.
- Price extrema are scanned once on dataset assignment. Visible intervals are
  selected with two binary searches, including partially visible boundary bars.
  The measurement range retains the dataset's fitted price scale.
- `ChartLayout` caches geometry for only the visible bars and shaped axis labels.
  Dataset, viewport, size, attachment and display-scale changes invalidate it.
  An unchanged redraw reuses the layout, brushes and pens. A single control draws
  the content; no bar gets a UI control or per-frame dataset copy. There is no
  continuous animation loop.
- Prices use 1/2/5-step ticks; dates and time precision adapt to the time span
  and each tick's actual timestamp, including unaligned fallback ticks.
  Labels are measured, the price gutter fits their width, and overlapping time
  labels are omitted. Candles/grid are clipped to the plot; everything is clipped
  to the control. Layout remains in logical pixels while minimum strokes track DPI.

These choices use [Avalonia custom rendering](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering).
[ATAS's rendering guidance](https://docs.atas.net/en/chart-indicators/performance.html)
informed visible-range work and resource reuse; it supplies no TauTick benchmark.

## Reproducible checks

Use pinned .NET SDK 10.0.114. The ordinary solution commands also run the chart tests:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx -c Release --no-restore
dotnet test TauTick.slnx -c Release --no-build --no-restore
```

The isolated native host is deliberately outside the production shell and solution:

```sh
dotnet restore tests/TauTick.Charting.Native --locked-mode
dotnet build tests/TauTick.Charting.Native -c Release --no-restore
dotnet run --project tests/TauTick.Charting.Native -c Release --no-build --no-restore
```

It has buttons for the four explicit OHLC cases, the existing foundation fixture,
single/flat, 960 bars, 100,000 bars with exactly 1,000 initially visible, clear, fit,
size changes and measurements. Keyboard equivalents are F1–F5 for the datasets,
F6 clear, F7/F8 small/large, F9 measure and F10 fit. The initial large viewport can
include partial boundary bars after navigation, so its visible count can change.
All inputs are deterministic and explicitly synthetic.
The four-case fixture starts at 2025-09-22 09:30 UTC: rising `(100,105,98,103)`,
falling `(103,104,97,99)`, doji `(100,102,98,100)`, a missing 09:33 interval,
then flat `(100,100,100,100)` at 09:34. Tuples are OHLC.

Pass `-- --unaligned` to show only the chart in a 240×300 window. This fixture
starts at 09:30:15.0000001 UTC and lasts 61 seconds; its fallback time label must
retain that exact timestamp even when ordinary axis ticks would be minutes apart.

On Linux, use a graphical X11/XWayland session and the native dependencies listed
in [development.md](development.md). To inspect 200% scaling in a manageable window:

```sh
AVALONIA_GLOBAL_SCALE_FACTOR=2 dotnet run --project tests/TauTick.Charting.Native -c Release --no-build --no-restore -- --small
```

For native input diagnostics, use `-- --trace-input`. The host prints JSON records
prefixed `INPUT` with viewport bounds, plot geometry, capture state, exact readout
and cached-layout identity after real routed pointer events. This mode observes
input; it does not manufacture events. On Linux, XTEST can send native mouse/key
input on an isolated X11 display; logs and captures should stay outside Git.

For focused automated checks:

```sh
dotnet test tests/TauTick.Charting.Tests -c Release --no-restore
```

For measurements, use `-- --measure --exit-after-measure` instead. The harness
prints its OS, native platform, scale, dimensions, loaded/visible counts and results.
It times both cached redraws and routed pointer inspection plus redraw. Inspection
samples traverse 50 positions from 25% to 74% of the plot width and assert layout
identity remains unchanged. These synthetic routed events are a software-work
measurement, separate from native OS input checks.
Use separate `--artifacts-path` directories for Windows and WSL builds of the same
checkout; do not share platform build outputs. No real market files are needed.

## Navigation validation — 2026-11-07

Environment: Ubuntu 24.04.5 x64 on a KVM guest reporting six Broadwell virtual CPUs,
Linux 6.8.0-147-generic, SDK 10.0.114/runtime 10.0.14, Release. Native applications
used Avalonia's **X11** backend on a dedicated **Xvfb** 1920×1200 display. XTEST
injected mouse/key input through the OS; these were native windows, separate from
Avalonia.Headless. Scale 2 used `AVALONIA_GLOBAL_SCALE_FACTOR=2`, not a physical
monitor transition.

Locked restores and Release builds passed with zero warnings/errors. All **185
solution tests** passed: 42 chart, 25 market-data, 99 adapter and 19 desktop.
The chart suite covers unclamped zoom anchors, coverage/minimum-span limits,
calendar extremes and one-tick intervals, one-bar/flat inputs, exact start/end and
gap hit testing, exact decimal/Int64 readouts, empty state, capture release/loss,
replacement, clear, detachment, resize and simulated scales 1/1.25/2. Pointer-only
software redraws retain the same layout/candle/axis objects. A counted 100,000-bar
lookup reads at most 19 bars, with enumeration prohibited. The production
reader/session/MainWindow integration also exercises navigation and inspection
through the existing dataset handoff, fit action, replacement and closing.

Native observations at **100% and 200%**:

- The standalone synthetic host accepted wheel zoom, primary-button dragging
  beyond the plot/window with capture, release outside and double-click reset.
  The known OHLC cases, missing interval and known zero-volume flat bar produced
  the expected inspection state. Captures showed readable readouts and crosshairs.
- The deterministic 100,000-bar fixture began with 1,000 visible bars and remained
  navigable through repeated inspection, zoom and drag. Pointer-only traces kept
  the layout identity. Resize, replacement, clear and single-bar zoom behaved as
  expected, and the windows closed normally. No stalled input step, crash or
  stale inspection/capture was observed in this bounded sequence. This is not a
  human-perceived latency or displayed-frame-rate measurement.
- The production `MainWindow` and actual CSV reader loaded the existing local
  960-bar ESZ5 reference (the hash in the sample guide). A temporary external
  harness supplied the file path through the existing picker seam and attached
  input diagnostics; production shell source was unchanged. Native input then
  exercised inspection, zoom, pan, double-click fit, resize and close. All 28
  observed readouts across the two scales matched source timestamps and exact
  OHLCV using independent CSV/decimal parsing. This increment did not repeat
  native file-picker interaction or CSV error scenarios; their regression tests
  remain passing.

The existing measurement command ran without input tracing or concurrent builds.
Each row uses 20 warmups then 100 synchronous operations, one reused software
render target at 96 DPI, and a **1060×569 logical-pixel** chart at native scale 1.
Managed allocations are current-thread bytes per operation, excluding native
memory and other threads. Dataset creation, initial layout and native presentation
are excluded. Inspection rows include creating/routing a pointer event, binary
lookup, exact readout shaping when the bar changes, framework recording and Skia
rasterization; they assert no candle/axis layout rebuild.

| Loaded / visible | Software operation | Median ms | p95 ms | Managed bytes/op |
| --- | --- | ---: | ---: | ---: |
| 960 / 960 | Cached redraw | 6.051 | 11.935 | 31,976 |
| 960 / 960 | Inspection + redraw | 5.988 | 8.704 | 53,298 |
| 100,000 / 1,000 | Cached redraw | 7.202 | 7.415 | 30,536 |
| 100,000 / 1,000 | Inspection + redraw | 7.614 | 7.988 | 50,268 |

These short local samples are not scaling comparisons, capacity guarantees or
evidence of presented FPS. **Outstanding for this increment:** Windows native
interaction/high-DPI checks, physical mixed-DPI transitions on either OS,
bare-metal Linux/Wayland, GPU/compositor frame timing and end-to-end input latency.
Earlier component checks below do not establish acceptance of these new gestures.

## Historical renderer validation — 2026-11-06

Machine: AMD Ryzen 7 2700X (8 cores/16 logical processors), Radeon RX 580
(Windows driver 31.0.21924.61), .NET SDK 10.0.114/runtime 10.0.14, Release builds,
Avalonia/Skia packages pinned by the foundation. Both platforms used this same PC:

- Windows 11 Pro, build 10.0.26200, Avalonia Win32 backend, native scale 1.
- Ubuntu 22.04.5 under WSL2 kernel 6.18.54.1, WSLg 1.0.79, Avalonia X11 backend.
  Native windows were inspected at scales 1 and 2 (the latter via the environment
  override above), not an Avalonia.Headless-only run.

Locked restore and Release solution tests passed on both platforms: 25 market-data
tests plus 21 chart tests. Chart checks cover coordinates, OHLC geometry, gap and
half-open boundary selection, logarithmic history lookup, exact adjacent extreme
prices, short intervals, flat/single data, replacement/clear, size changes, cached
layout reuse, unaligned second/fractional-second labels through 100 ns, and
display-scale notifications at 1, 1.25 and 2. Skia software pixel
checks verify bodies, wicks, doji, the gap and removal of stale pixels. Headless DPI
notifications are simulated; they do not prove physical monitor transitions.

Actual native windows were visually inspected for candles, price/time labels and
clipping. On Linux, the case view was resized from 1060×680 to 530×380 logical
window size, cleared, and replaced with the single flat dataset; the small case
view was also inspected at 200%. On Windows, the case view was resized, replaced
with the foundation dataset, cleared, then replaced with the 960-bar history.
Native windows closed normally. The foundation reader/result/chart wiring tests
also remain passing. The production shell's old placeholder header is still owned
by the separate shell task; use this host for chart validation.

The narrow unaligned-time fixture was also inspected in actual Windows and Linux
windows: its label displayed `2025-09-22` and `09:30:15.0000001` without clipping.

### Measured redraw work

The harness warms up 20 redraws, then measures 100 synchronous Skia software
`RenderTargetBitmap.Render(chart)` calls at 1060×567 logical pixels and 96 DPI,
reusing one render target and prepared chart layout. This includes framework
recording and rasterization; it excludes dataset construction, initial layout,
native GPU submission, presentation, VSync and input latency. Allocation values
are managed bytes on the calling thread, excluding native/graphics memory and
other threads. The results are basic local observations, not capacity guarantees.

| Platform | Loaded / visible bars | Median | p95 | Managed bytes/redraw |
| --- | ---: | ---: | ---: | ---: |
| Ubuntu/WSLg | 960 / 960 | 4.589 ms | 5.221 ms | 29,781 |
| Ubuntu/WSLg | 100,000 / 1,000 | 3.114 ms | 3.801 ms | 30,249 |
| Windows | 960 / 960 | 4.027 ms | 6.159 ms | 31,784 |
| Windows | 100,000 / 1,000 | 3.515 ms | 5.144 ms | 30,262 |

These runs do not measure presented FPS or establish the plan's proposed 16.7 ms
interactive frame budget. Allocations are nonzero despite cached chart geometry;
the software/framework path still allocates. Differences between these short runs
should not be interpreted as a scaling law or platform ranking.

Outstanding: physical mixed-DPI monitor transitions on Windows/Linux, Windows
high-DPI native inspection, bare-metal Linux desktops, native Wayland, presented
frame timings/input latency, and the separate real CSV-to-candles integration.

## Workspace hosting

TAU-8 exposes `ChartTimeRange`, `VisibleTimeRange`,
`VisibleTimeRangeChanged` and `TryRestoreTimeRange` so a chart document can preserve
its viewport when Dock recreates a surface. Restore validates the range against
the accepted dataset and minimum bar interval; a full-range restore uses the
existing fit behavior. This API does not read files or rebuild bars. The desktop
[chart-host interface](workspace.md#chart-host-handoff-for-tau-9) owns lifetime and
feature settings; consumers must not keep a control as the chart's identity.

## Study presentation

The [indicator contract](indicators.md#extension-contracts-and-ownership) adds prepared custom drawing,
inspection and reserved regions without provider access during render.
