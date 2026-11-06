# CSV-to-candles integration

TAU-5 connects the production Databento reader, shared immutable dataset, desktop
session and custom chart. Normal startup contains no synthetic reader or fallback.
The delivered features are local CSV loading, candles, UTC/price axes, fit-to-data
and resizing. Pan/zoom, crosshairs, indicators, footprints, replay and live data are
deferred. Windows high-DPI and closing-during-load interaction remain unverified;
the checks below do not establish complete cross-platform acceptance.

## Run the viewer

Use the pinned .NET SDK 10.0.114 and the native prerequisites in
[development.md](development.md). From the repository root:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx -c Release --no-restore
dotnet test TauTick.slnx -c Release --no-build --no-restore
dotnet run --project src/TauTick.Desktop -c Release --no-build --no-restore
```

Download the public minute CSV **outside Git** using the
[sample-data guide](sample-data/README.md). Enter the known full contract year
**2025** and use **Open CSV**. The verified file is ESZ5 (December 2025), source
instrument 294973: 960 M1 bars starting at 2025-09-22 00:00 through 15:59 UTC,
with coverage **[00:00, 16:00) UTC**. This is partial-session data. The year field
starts empty because the CSV's short symbol does not identify a decade. The reader
validates its suffix against the supplied year; it cannot independently establish
the decade without external instrument metadata.

Loading disables Open and year editing. Picker cancellation preserves accepted
data. Failed replacement preserves the chart and its original metadata/viewport;
successful replacement publishes one shared snapshot and fits the full range.
Changing the year prepares the next import without relabeling the current chart.
Closing cancels pending reading and rejects late callbacks. Fit-to-data and resizing
never modify bar boundaries or timeframe.

## Automated and source checks

On 2026-11-06 UTC, locked restore, Release build (zero warnings/errors) and all
**163 tests** passed on Ubuntu/WSL and Windows: 25 MarketData, 99 adapter,
20 chart and 19 desktop cases. Desktop integration uses invented CSV values with
the normal production composition and real reader/chart. It verifies values,
interval positions/gaps, year input, invalid schema, retained metadata/viewport,
replacement, fit, control resizing and cancellation of pending real-parser I/O.
Controlled older reader tests additionally cover selection errors, ignored
cancellation, owned-stream lifetime and repeat-open/UI responsiveness.
These are headless tests, separate from native evidence below.

The source verification command also passed on Linux:

```sh
python3 scripts/verify-databento-sample.py /outside/checkout/reference.csv 2025 \
  --reader samples/TauTick.Databento.Verify/bin/Release/net10.0/TauTick.Databento.Verify.dll
```

It independently compared **every timestamp and OHLCV value** with Python's CSV
and decimal parsing, verified metadata/coverage, and matched SHA-256
`cab4d4c81f7c8ac1ae1c109614f892ac22886ea14f49cbdbbeb68aaa1309d1d7`.
The native measurement host checked source-derived bars 0, 480 and 959 against
their interval-center X positions, high/low wick positions and open/close body
edges (within one device pixel for minimum-height bodies). Its output includes
those values and coordinates for local inspection; keep redirected output outside Git.

When Windows and WSL share a checkout, use **different external artifacts paths**
for every restore/build/test, for example `--artifacts-path /tmp/tautick-linux`
versus `--artifacts-path C:/temp/tautick-windows`. This was used for the recorded
checks. Invoke the resulting DLL with `dotnet`; it resides at
`<artifacts>/bin/<ProjectName>/release/<ProjectName>.dll`. Pass that verifier DLL
to `--reader` as above. Do not share `obj` or native build outputs across platforms.

## Native checks and limitations

Linux: Ubuntu 22.04.5 x64, WSL2 kernel 6.18.54.1, WSLg/XWayland, actual desktop
executable and GTK native file picker. X11 input and native window captures were
used, not Avalonia.Headless. Verified:

- Empty startup, explicit year entry, picker cancellation, then opening the real
  reference through Open CSV. Loading and disabled inputs were visible; the
  accepted chart displayed ESZ5, 960 bars and matching UTC metadata/axes.
- Resize from 1000×680 to 640×520 logical client size; labels and metadata stayed
  readable. Fit to data remained usable.
- Opening a clearly synthetic file with `rtype=32`: contextual row 2/field rtype
  error, retained ESZ5 chart/metadata, then successful replacement by an invented
  three-bar TESTZ5 file with a gap. The error cleared and axes/metadata changed together.
- At **200% scaling** (`AVALONIA_GLOBAL_SCALE_FACTOR=2`), opening the reference
  again at 640×520 logical size produced readable fonts and axes without clipping.
- A slow, clearly synthetic CSV supplied through a local FIFO kept a real parser
  read pending. The native window resized during loading (to 700×550 logical at
  200%), retained the accepted reference chart and disabled Open/year. Closing via
  its native close request exited **0**, and the producer observed the reader close.
  Normal closing also exited 0. Automated tests separately verify token cancellation
  and stream disposal, since process exit alone cannot prove those semantics.

GTK emitted `WIDGET_REALIZED_FOR_EVENT` warnings during automated dialog dismissal;
no failed load, hang or crash was observed. These checks do not cover bare-metal
Linux, native Wayland or physical mixed-DPI monitor transitions.

Windows: Windows 11 Pro 10.0.26200 x64. The native Win32 measurement host loaded,
laid out and rasterized the real/synthetic inputs and closed normally at scale 1.
The main implementation session could not initialize Windows automation because
its `@oai/sky` / `@oai/cua/tinyskyAlt` packages were unavailable. The fresh independent
review session subsequently initialized Computer Use and exercised the actual
production viewer at **100% scaling**: picker cancellation, reference loading,
readable candles/UTC axes/fonts, fit, maximize/restore, failed replacement retaining
chart/metadata, successful synthetic replacement and normal close. These are
observations of this integrated change, not inherited component evidence.

The independent reviewer also repeated both platforms' locked Release builds and
163 tests, source verification, native measurement hosts and Linux native checks,
including resize/close during a pending real-parser FIFO read at 200%. No required
fixes were found. This paragraph records reviewer observations; the measurements
in the table below remain the main implementation session's explicitly described runs.

**Windows native high-DPI and closing-during-load interaction remain outstanding.**
To complete those checks, run the actual viewer with the verified reference plus
clearly synthetic valid/invalid CSVs outside Git at representative higher display
scales, and exercise a deliberately pending read while resizing and closing.
Record the observed OS, scale and outcomes. Physical mixed-DPI transitions,
native Wayland/bare-metal Linux and presented-frame/input latency also remain unverified.

## Reproduce the small measurement

The existing native shell host has an opt-in measurement mode. Ordinary test runs
do not download/read the reference. Run on a graphical desktop in **Release**:

```sh
dotnet restore tests/TauTick.Desktop.Native --locked-mode
dotnet build tests/TauTick.Desktop.Native -c Release --no-restore
dotnet run --project tests/TauTick.Desktop.Native -c Release --no-build --no-restore \
  -- --measure /outside/checkout/reference.csv 2025
```

Use a quoted Windows path on Windows. The host rejects a reference hash/year
mismatch. It creates an explicitly invented **100,000-bar TESTZ5 CSV** in memory,
parses it with the actual adapter, and bounds the chart to exactly 1,000 visible
bars through the existing internal development seam. No user-facing navigation
or alternative dataset model is introduced. The legacy controlled shell scenarios
remain available when `--measure` is omitted.

Method:

- Stream opening and CSV reading: 2 warmups + **10 samples** per workload, timed
  independently of chart drawing. Reference input is a local file; synthetic input
  is a 6,684,742-byte memory stream. The workloads differ in both input and size;
  their timings do not isolate a platform or scaling law.
- Open-to-first-complete-software-raster: 2 warmups + **10 samples** through the
  production MainWindow/session, with a deterministic picker seam that supplies
  the stream. Includes opening, parsing, UI publication, fit/range selection,
  layout, render-target construction and synchronous Skia rasterization. Excludes
  human picker time. This is **CPU-ready first-chart latency**, not measured native
  presentation/input-to-display latency. The native application was separately inspected.
- Cached redraw: 20 warmups + **100 samples**, reusing a RenderTargetBitmap at
  96 DPI. Resize work: 20 warmups + **50 samples** alternating the real chart
  control between its normal and 80% size, including layout rebuild and raster.
  It does not time the OS window manager or GPU/compositor presentation.
- Memory: **20 reference/large replacement pairs**, retaining the large dataset
  at each checkpoint. Full GC is explicitly requested at pairs 2, 10 and 20;
  report managed bytes, process working set and private bytes. No forced GC is
  added to production code. Managed read allocation is process-wide across awaits;
  redraw allocation is calling-thread only. Native graphics allocations are excluded.

## Observations — 2026-11-06 UTC

Same PC: AMD Ryzen 7 2700X, 8 cores/16 logical processors, 32 GiB physical RAM,
Radeon RX 580 (Windows driver 31.0.21924.61). Linux had a 16 GiB WSL memory limit;
the host had roughly 11.5 GiB free at the observation. SDK 10.0.114/runtime 10.0.14,
Avalonia/Skia versions from the lock files, Release, native scale 1, chart
**958×429 logical pixels**, no concurrent builds. X11/WSLg and Win32 native backends;
timed rasterization is software Skia, not a GPU benchmark. Linux reference reads
used the Windows-mounted filesystem. Each row below is median / p95 milliseconds.

| Platform / loaded / visible | Stream + CSV | First complete raster | Cached redraw | Resize + redraw |
| --- | ---: | ---: | ---: | ---: |
| Ubuntu/WSLg / 960 / 960 | 24.000 / 30.127 | 35.594 / 48.079 | 4.449 / 4.874 | 7.201 / 8.373 |
| Ubuntu/WSLg / 100,000 / 1,000 | 243.638 / 273.898 | 267.217 / 289.316 | 2.557 / 2.888 | 2.822 / 3.352 |
| Windows / 960 / 960 | 9.052 / 10.800 | 18.690 / 36.404 | 4.201 / 5.947 | 5.918 / 8.455 |
| Windows / 100,000 / 1,000 | 248.479 / 540.084 | 285.978 / 335.870 | 2.820 / 3.608 | 3.349 / 4.149 |

Synthetic CSV construction (excluded from these timings) took 129.039 ms on Linux
and 92.671 ms on Windows. Approximate managed allocation per read was 2.30 MB for
the reference and 177–178 MB for the synthetic file. Cached redraw allocated about
39.8 KB/reference and 28.9 KB/large visible range; resize work about 110–133 KB.
Parsing allocation is substantial but happens off the UI thread; this task does
not establish future streaming/MBO capacity or optimize speculative workloads.

Memory below is decimal MB after explicit GC, with the same 100,000-bar snapshot
and synthetic input buffer retained. Working set/private memory include runtime
and graphics resources, not just the dataset.

| Platform / pairs | Managed | Working set | Private |
| --- | ---: | ---: | ---: |
| Ubuntu/WSLg / 2 | 22.76 | 322.86 | 292.86 |
| Ubuntu/WSLg / 10 | 22.74 | 332.77 | 301.87 |
| Ubuntu/WSLg / 20 | 22.75 | 325.01 | 293.81 |
| Windows / 2 | 23.08 | 191.79 | 136.89 |
| Windows / 10 | 23.08 | 196.63 | 141.45 |
| Windows / 20 | 23.10 | 189.26 | 133.73 |

No reproducible freeze, incorrect candle placement or continuously growing
retained-memory pattern was observed in these bounded runs. This is not a proof
against every leak. The proposed **60 FPS / 16.7 ms** frame target is still a
proposal: software draw duration cannot establish presented FPS, GPU latency,
VSync behavior or end-to-end interaction latency.
