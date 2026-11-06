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
```

`Dataset` remains the original immutable snapshot. Null replacement throws without
discarding it. The control owns no disposable dataset resources. It displays
"No bar data" when clear and an enlargement message when there is insufficient
space for readable axes. The application supplies instrument/provenance information
outside the chart, as established by the foundation contract.

The chart currently has no pan/zoom, crosshair or other user interaction. The
internal time-range setter exists for the chart test/measurement host; it is not
a new public navigation API. Replacement always restores fit-to-data.

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
single/flat, 960 bars, 100,000 bars with exactly 1,000 visible, clear, size changes,
and measurements. All inputs are deterministic and explicitly synthetic.
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

For measurements, use `-- --measure --exit-after-measure` instead. The harness
prints its OS, native platform, scale, dimensions, loaded/visible counts and results.
Use separate `--artifacts-path` directories for Windows and WSL builds of the same
checkout; do not share platform build outputs. No real market files are needed.

## Validation record — 2026-11-06

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
