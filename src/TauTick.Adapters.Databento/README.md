# Databento minute CSV adapter

`DatabentoMinuteCsvReader` implements the shared `IHistoricalBarReader` contract.
It reads one outright futures contract from a local Databento **GLBX.MDP3
OHLCV-1m CSV** into a complete immutable `BarDataset`. Its only dependency is
`TauTick.MarketData`; there is no Avalonia, provider SDK, credential or network use.
See the [shared contract guide](../../docs/development.md) for the model boundary.

## Call the reader

```csharp
using TauTick.Adapters.Databento;
using TauTick.MarketData;

// A full contract year from known source metadata, not the filename or today's date.
IHistoricalBarReader reader = new DatabentoMinuteCsvReader(contractYear: 2025);
await using var input = new FileStream(localPath, FileMode.Open, FileAccess.Read,
    FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
BarReadResult result = await reader.ReadAsync(input, Path.GetFileName(localPath), token);
if (result is BarReadResult.Success accepted)
{
    BarDataset dataset = accepted.Dataset;
    // The desktop can supply this stable snapshot to the chart on its UI thread.
}
```

The constructor requires the **full contract year**. Short raw symbols such as
`ESZ5` do not distinguish 2025 from 2035. The reader checks that the supplied year
matches the symbol's year suffix and derives the contract month from its futures
month code. The recording date comes exclusively from the rows. It makes no
assumption that recording year equals contract year, or that the filename describes
either. Callers must supply a known year; resolving it from instrument definitions
or a future UI is outside this adapter. The reference sample's documented year is
2025. This explicit configuration is permitted by the foundation reader contract.

The input stream belongs to the caller and remains open on success, failure and
cancellation. Reading starts at its current position; nonseekable readable streams
are supported. `sourceName` is only a provenance label. The reader opens no files
and disposes its own text wrapper with `leaveOpen: true`. Opening errors belong to
the caller; errors while reading become structured I/O failures. Keep the stream
exclusively available until the awaited operation finishes, then dispose it.

Both parsing and I/O run in a worker task, including when a memory stream completes
reads synchronously. The reader has no mutable per-read state and can be reused
with separately owned streams. It checks cancellation before work, while reading
and scanning fields, between records and before publishing. Cancellation throws
`OperationCanceledException` with the caller's token; it is never an import failure
or successful partial dataset. The underlying stream must cooperate with canceled
asynchronous reads to interrupt pending I/O promptly. No background read is abandoned.

## Supported input

The header must contain exactly these ten case-sensitive names, each once; order
may vary:

```text
ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
```

* UTF-8 with an optional BOM; LF, CRLF and CR line endings; an optional final line
  ending. CSV fields may be quoted, with doubled quotes for embedded quotes and
  line endings inside quotes. The tokenizer preserves those characters, then the
  field's schema decides whether they are valid. It does not trim values or skip
  blank records. Each decoded field is limited to 4096 characters.
* `rtype` must be **33**, the one-minute schema. `publisher_id` must be **1**,
  mapping to dataset **GLBX.MDP3** and venue **GLBX**. Other schemas/publishers fail,
  including a change after valid rows. These mappings follow Databento's
  [OHLCV schema](https://databento.com/docs/schemas/ohlcv) and
  [publisher definitions](https://github.com/databento/dbn/blob/main/rust/dbn/src/publishers.rs).
* `instrument_id` is an unsigned 32-bit decimal integer. Its original text is
  retained; every row must have the identical identifier text and symbol. `symbol`
  is an uppercase alphanumeric root, a standard futures month letter
  (`FGHJKMNQUVXZ`), and one, two or four year digits. Generic/continuous symbols,
  spreads and options are unsupported. The resulting identity is a dated contract,
  with a first-of-month `DateOnly`; tick size remains unknown (`null`).
* `ts_event` is an ISO UTC timestamp (`yyyy-MM-ddTHH:mm:ss`, optional 1–9 fractional
  digits, then `Z` or `+00:00`). Fractions finer than 100 ns must be zero beyond
  the seventh digit; no precision is rounded away. One-minute bars must start on
  a UTC minute boundary. Numeric epoch timestamps and nonzero offsets fail.
* OHLC fields are exact decimal prices with optional leading sign, digits and an
  optional decimal point/fraction. No locale grouping, exponent, whitespace, NaN
  or missing value is accepted. Values outside .NET `decimal`'s exact range or
  precision fail; insignificant trailing fractional zeros are allowed. **No DBN
  integer scaling is applied.** Negative, flat and doji prices are valid when
  `low <= open/close <= high` holds.
* Volume must be an integer in `[0, Int64.MaxValue]`. Zero is known zero; a missing
  field is never substituted with zero. Out-of-range or fractional values fail.
* Rows must be strictly increasing with unique starts and representable interval
  ends. Missing minutes remain gaps. The reader never sorts, deduplicates or fills.

Coverage is the first start through the last bar's exclusive end, with the shared
`HasGaps` flag. It describes available data, never a complete trading session.
The dataset preserves source identity (`Databento`, `GLBX.MDP3`, `ohlcv-1m`, source
label, source instrument ID) and retains the original CSV symbol.

## Errors

The first useful error returns `BarReadResult.Failure`, which has no dataset:

| Kind | Examples |
| --- | --- |
| `Format` | Bad header/CSV/UTF-8, unsupported schema/publisher, missing fields, nonrepresentable number or timestamp |
| `Validation` | Empty file or header-only file, invalid OHLC/negative volume, unsupported or inconsistent instrument, invalid minute alignment, duplicate/out-of-order starts |
| `Io` | An `IOException` or `UnauthorizedAccessException` during stream reading |

Errors carry a one-based **physical line** (including the header) and a field when
known. Record errors use the record's first line even for quoted multiline values;
I/O/decoding errors report the line reached by the tokenizer. Shared OHLC failures
use field `ohlc`; structural CSV errors may have no field. Header mismatches identify
`header`. Invalid API arguments throw; unexpected implementation errors propagate.

## Deterministic validation

With the pinned .NET SDK installed, from the repository root:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test tests/TauTick.Adapters.Databento.Tests --no-build --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet list src/TauTick.Adapters.Databento package --include-transitive --no-restore
```

The focused tests contain only labeled, invented CSV values. They do not read any
downloaded sample or require network access after restore. They exercise real reader
behavior through the shared interface: values/identity/coverage, culture and precision,
quoting and tiny read chunks, nonseekable streams, malformed/mixed data, ordering,
cancellation, actual file ownership and controlled stream I/O failures. The adapter
test project inherits existing test dependencies; central package settings and shared
contracts are unchanged. Its registration in the solution also runs it in the normal
repository test command.

## Separate local sample verification

Download only the small public minute sample using the
[sample-data guide](../../docs/sample-data/README.md), **outside the checkout**.
Use an explicit local path and the known contract year; no filename inference occurs:

```sh
python3 scripts/verify-databento-sample.py /absolute/path/to/sample.ohlcv-1m.csv 2025
```

On Windows, the equivalent is `python scripts/verify-databento-sample.py
"C:\\outside-checkout\\sample.ohlcv-1m.csv" 2025`. The script runs the built Debug
`TauTick.Databento.Verify` console program, which calls `IHistoricalBarReader` and
emits exact decimal JSON. Python independently reads the original CSV and checks
**every** timestamp, OHLCV value, symbol and instrument ID, plus coverage and SHA-256.
It identifies a changed sample revision separately from the recorded reference.
The checker uses explicit assertions; do not invoke Python with `-O`. Neither tool
downloads or writes data. The standalone console program can also inspect a local
file without Python:

```sh
dotnet run --project samples/TauTick.Databento.Verify --no-build --no-restore -- /absolute/path/to/sample.csv 2025
```

This prints the entire dataset as JSON. Keep any redirected output outside Git too.
The verifier disposes the file it opens on success, failure and cancellation.

## Validation record and integration handoff

On 2026-11-06, Ubuntu 24.04 x64 with SDK 10.0.114: Debug solution build passed with
zero warnings/errors; **99 adapter tests**, **25 existing market-data tests** and
**4 existing headless chart tests** passed. Commands above were used for the build,
focused/solution tests, locked restore and dependency check. The adapter reports no
direct or transitive packages.

The separate local check matched reference SHA-256
`cab4d4c81f7c8ac1ae1c109614f892ac22886ea14f49cbdbbeb68aaa1309d1d7` and compared all
**960 ESZ5** bars exactly with source. Contract month was **2025-12**, instrument ID
**294973**, first start **2025-09-22 00:00 UTC**, last start **15:59 UTC**, coverage
**[00:00, 16:00) UTC**, with no interior gaps. The downloaded CSV remains outside Git.

That adapter-only pass exercised build/test and actual-file ownership with spaces
and non-ASCII paths on Linux only. The later [combined validation record](../../docs/development.md#combined-reader-and-chart-validation--2026-11-06)
also covers Windows build/test and file ownership. No desktop UI, native window
check or chart performance test was performed for this headless adapter task.
TAU-5 must connect this reader to the shell/chart and validate the application on
both platforms. The shell needs a known full contract year, caller-owned file
lifetime, cancellation/error handling and UI-thread publication of accepted data.
