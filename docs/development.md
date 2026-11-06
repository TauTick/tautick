# Foundation development and contract guide

TAU-1 establishes the shared boundary for the candle viewer. TAU-2 adds the
[Databento minute CSV reader](../src/TauTick.Adapters.Databento/README.md), and TAU-3
adds the [custom candle chart](chart-control.md). TAU-4 adds the [desktop shell](desktop-shell.md)
with native file selection, responsive loading, errors and cancellation. TAU-5
connects the production reader and chart, with explicit full contract-year input.
See the [integration report](mvp-validation.md) for checks, performance and outstanding
Windows interactive acceptance. Pan/zoom and crosshairs remain separate work.

## Build, test and launch

Install [the .NET SDK 10.0.114](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
for your platform. `global.json` requires that exact SDK with no roll-forward.
All projects target `net10.0`; `Directory.Packages.props` centrally pins Avalonia
12.1.5, xUnit v3 and the test tools. Each project's `packages.lock.json` records
transitive versions and hashes. `NuGet.Config` selects the public NuGet feed.

Run from the repository root in Bash or PowerShell:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet run --project src/TauTick.Desktop --no-build --no-restore
```

Windows: use a graphical desktop with the SDK installed; close the window using
its title-bar close button. Linux: use an X11 or XWayland desktop. On Ubuntu 24.04,
the native prerequisites include `libx11-6`, `libice6`, `libsm6`, `libfontconfig1`,
`libxrandr2`, `libxi6` and `libxcursor1`; see the
[Avalonia Linux guide](https://docs.avaloniaui.net/docs/platform-specific-guides/linux).
The application uses Avalonia's default platform renderer and bundled Inter font.
No workloads, provider SDK or API key are needed. Tests need no market data;
opening the reference file requires the external download in the sample-data guide.

For a Linux machine without a desktop session, install `xvfb`, `xauth` and Python 3
in addition to the native libraries, then run:

```sh
xvfb-run -a python3 scripts/smoke-linux.py
```

This starts the built Debug executable, checks that its native X11 window is mapped,
sends `WM_DELETE_WINDOW`, and requires normal process exit with code 0. Use a
dedicated Xvfb display as shown. It is a native lifecycle check, not a visual
review, interactive desktop test, or performance measurement. The xUnit chart
tests use Avalonia.Headless and separately verify wiring; they do not prove native
startup. On Windows, perform the launch/close check manually using the run command.

To update dependencies deliberately, edit the central file, run `dotnet restore
TauTick.slnx --force-evaluate`, and include the changed lock files with the change.
Ordinary validation uses locked mode. Release builds use `-c Release` consistently
on build, test and run commands.

## Ownership and dependencies

| Location | Responsibility / next task |
| --- | --- |
| `src/TauTick.MarketData` | Shared immutable contracts. Coordinate changes here across consumers. No UI or provider dependencies. |
| `src/TauTick.Adapters.Databento` | `DatabentoMinuteCsvReader` implements `IHistoricalBarReader`, referencing only MarketData. See the [adapter guide](../src/TauTick.Adapters.Databento/README.md) for supported CSV input and explicit contract-year configuration. |
| `src/TauTick.Charting` | Avalonia `BarChart` with custom candles, UTC/price axes, fit-to-data and resize/DPI handling behind the dataset input/clear API. Interactions remain future work. No file reads or market reconstruction. |
| `src/TauTick.Desktop` | Production reader/chart composition, explicit contract year, native picker, loading/error/cancellation UI and fit action. No synthetic-data dependency. |
| `samples/TauTick.SyntheticData` | Development fixture and fake reader; only depends on MarketData. The demo and tests consume it. It is not a source adapter. |
| `tests/TauTick.MarketData.Tests` | Model validity, snapshot ownership, and implemented fake-reader contract behavior. |
| `tests/TauTick.Charting.Tests` | Coordinates, visible ranges, cached layout, Skia raster checks and reader/result/chart handoff under Avalonia.Headless. |
| `tests/TauTick.Charting.Native` | Isolated synthetic chart host for native visual checks and software redraw measurements; outside the solution. |
| `tests/TauTick.Desktop.Tests` | Shell transitions, retained data/metadata, cancellation, resource ownership and UI responsiveness under Avalonia.Headless. |
| `tests/TauTick.Desktop.Native` | Legacy controlled shell scenarios and opt-in real CSV integration measurements; outside the solution. |
| `tests/TauTick.Adapters.Databento.Tests` | Synthetic CSV validation, precision, cancellation and stream ownership. No UI dependency. |
| `samples/TauTick.Databento.Verify` | Explicit local-file adapter verification; never part of deterministic test execution. |

The graph is acyclic: adapters → MarketData, chart → MarketData, synthetic data →
MarketData, desktop → adapter + chart + MarketData. Only the chart,
desktop and their validation hosts use Avalonia. No speculative calculations project exists.
Future test projects under `tests/` inherit the common test packages/settings.

## Bar and dataset contract

`Bar` is an immutable reference record. Its constructor enforces:

* A zero-offset `DateTimeOffset` start and positive `TimeSpan` interval with a
  representable end. The interval is **[StartUtc, EndUtc)**. Timestamp precision
  is 100 ns; it preserves the supplied minute bars and is not a nanosecond MBO
  storage decision. Nonzero offsets are rejected rather than silently converted.
* Exact `decimal` OHLC prices and `low <= open/close <= high`. Negative prices,
  flat bars and doji are valid. There is no inferred tick rounding.
* Nonnegative signed 64-bit integer volume. Zero means known zero. Missing values
  cannot be represented as a valid bar and must produce a reader failure.

`InstrumentIdentity` separates `DatedContract` from `GenericSymbol`. A dated contract
requires a contract month (`DateOnly` with day 1); this is not an expiry date.
A generic symbol cannot carry a contract month. `TickSize` is nullable (unknown)
or positive, never an invented zero. Symbol/venue strings are retained verbatim.

`DataSourceIdentity` retains provider, source dataset, schema, source name and an
optional source instrument ID. These are provenance, not authentication or a file
handle. A reader must validate that its bars belong to the declared instrument,
source and timeframe; the model cannot authenticate this metadata.

`BarDataset` takes instrument, source and a nonempty sequence of bars. It copies
the sequence into its own read-only collection of immutable values. The producer
must not mutate the input during construction; afterwards it can reuse/change
its collection without changing the dataset. No mutable array is exposed.

The constructor rejects null bars, different intervals, duplicate/out-of-order
starts and overlapping bars. It never sorts or fills. Gaps are valid and preserved.
`Coverage` is derived from first start to last end, with `HasGaps` describing
interior discontinuities only. Neither `HasGaps == false` nor successful import
claims a complete trading session or proves source completeness. An empty file
is a validation failure; an empty chart is represented by clearing its dataset.

## Historical reader and failures

`IHistoricalBarReader.ReadAsync(Stream input, string sourceName, CancellationToken)`
returns `Task<BarReadResult>`. The caller opens/owns a readable stream, retains it
until the awaited operation finishes, then disposes it. Reading begins at its
current position, with no requirement to seek. The reader must never close it,
including on failure/cancellation. The final position is unspecified. `sourceName`
is a provenance/display label, never a path the reader opens.

The CSV reader owns format/schema checks, required fields, exact parsing,
identity consistency and row-level validation. It must reject out-of-range values
and excess precision it cannot preserve (rather than rounding), including volume
outside `Int64` and timestamps finer than 100 ns. Validate shared invariants while
row context is available, before constructing a successful dataset. Reject invalid
ordering; preserve gaps. Source-specific timestamp conventions must be documented
by that adapter (see [the sample guide](sample-data/README.md)).

`BarReadResult` is a closed union:

* `Success` contains exactly one complete accepted `BarDataset`.
* `Failure` contains a nonempty, defensively copied list of `ImportError`, and has
  no dataset property. Errors carry `Format`, `Validation` or `Io`, a message, and
  optional row/field. Rows are one-based physical lines including the header;
  missing context is null. Returning the first useful error is sufficient.

Expected input, validation and I/O problems return Failure. Invalid API arguments
throw; unexpected implementation errors are not disguised as input failures.
Cancellation throws `OperationCanceledException` with the caller's token, not a
failure result or partial success. Check before work, during long processing and
before publishing. Cancellation after completion does not revoke that result.
CPU-heavy parsing must not block the UI thread; asynchronous I/O alone does not
move parsing off it. A completed Task is permitted and must still be awaited.

## Synthetic handoff and chart lifetime

`SyntheticBars.Create()` contains three invented minute bars, including a doji,
a flat bar with known zero volume, and a missing minute. Its contract and provider
are explicitly synthetic; tick size is unknown. This is independent of downloaded
market data. `InMemoryBarReader` returns a supplied immutable result, deliberately
ignoring input bytes and the source label; configure the fixture's source name to
match the call when demonstrating provenance. The pending-Task constructor allows
deterministic cancellation tests. The fake does not implement CSV validation.

For a direct synthetic handoff, publish on the Avalonia UI thread:

```csharp
var data = SyntheticBars.Create();
IHistoricalBarReader reader = new InMemoryBarReader(new BarReadResult.Success(data));
using var input = new MemoryStream();
var result = await reader.ReadAsync(input, data.Source.SourceName, cancellationToken);
if (result is BarReadResult.Success accepted)
    chart.SetDataset(accepted.Dataset);
```

`BarChart.SetDataset` accepts a non-null snapshot and replaces the entire previous
input. `Dataset` reads, replacement and `ClearDataset()` require the UI thread;
the guard rejects worker-thread access. Null replacement throws without clearing.
Clear is idempotent. Assignment/clear invalidate the control for future rendering;
the chart fits and draws the supplied candles (see the chart control guide). The
control holds a managed reference, owns no file/stream, and does not dispose data. Readers can safely retain
an old snapshot after replacement/clear. The desktop shell releases its reference on close.

## Parallel-task handoff

The adapter, chart and shell share these foundation types and fixtures. Their
integration checks are:

* CSV adapter: read synthetic CSVs into `BarReadResult`, assert exact values,
  identity/coverage, contextual errors, cancellation and stream ownership. Keep
  provider parsing in its module; no chart calls.
* Chart: feed `SyntheticBars.Create()` through `SetDataset`, then replace and clear
  it while validating drawing/interaction. Preserve bar boundaries/timeframe.
* Shell: compose the real adapter and `BarChart` through the same asynchronous
  result boundary. Keep source errors/cancellation distinct from successful input.

The [CSV-to-candle integration report](mvp-validation.md) records the composed checks.
Coordinate changes to
MarketData and central package/solution files; do not invent parallel shared types.

## Validation record

On 2026-11-06, a fresh source copy without build outputs passed the documented
locked restore, Debug build (zero warnings/errors), and tests on Ubuntu 24.04 x64
with .NET SDK 10.0.114: 25 model/reader tests and 4 headless chart/wiring tests.
The native smoke command mapped the 900×560 X11 window under Xvfb and closed it
through `WM_DELETE_WINDOW` with exit code 0. The MarketData and Databento projects
also report no direct or transitive package references.

That foundation pass did not verify Windows native startup, interactive Linux
desktop behavior, Wayland, high DPI or chart performance. Later chart checks and
their remaining platform limits are recorded in the [chart guide](chart-control.md).
Build/test success alone does not establish cross-platform UI acceptance.

### Combined reader and chart validation — 2026-11-06

After combining TAU-2 and TAU-3, locked solution restore and Release build passed
with zero warnings/errors on Windows 11 and Ubuntu 22.04.5 under WSL2, using SDK
10.0.114 and separate `--artifacts-path` directories. The solution passed **145
tests on each platform**: 99 adapter, 25 market-data and 21 chart tests. This includes
the adapter's actual-file ownership cases with spaces and non-ASCII paths on both
platforms, plus the chart's existing handoff, coordinate and software raster checks.

A separate temporary synthetic probe also passed on both platforms: the actual
CSV reader produced a dataset that was supplied to `BarChart` and rasterized under
Avalonia.Headless/Skia. It checked exact decimal values, instrument identity, stream
ownership, interval-gap placement, replacement and clearing. This does not implement
desktop file loading. No native windows or performance measurements were repeated
for this documentation-only reconciliation; the chart guide's recorded native
checks and outstanding platform limits remain applicable.
