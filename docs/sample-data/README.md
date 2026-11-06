# Public sample data for TauTick development

Verified on **2026-11-06**. This guide contains source links, download instructions and verification notes. Market-data files, archives and private recordings are kept outside the repository.

Start the charting MVP with Databento's public **ES** minute bars. They are sufficient for CSV import, candlestick/volume rendering, navigation and candle inspection. NQ remains a later data acquisition choice; none of the samples described here should be labeled NQ.

## Download the bars locally

The following public endpoints returned CSV files without an account, API key or subscription when verified:

- [One-minute OHLCV](https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/ohlcv-1m?sample_type=futures)
- [One-second OHLCV](https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/ohlcv-1s?sample_type=futures)

Example for Bash with `curl`, saving outside the checkout:

```bash
export TAUTICK_SAMPLE_DIR="$HOME/.local/share/tautick/samples/databento"
mkdir -p "$TAUTICK_SAMPLE_DIR"

curl --fail --location --show-error --retry 2 \
  'https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/ohlcv-1m?sample_type=futures' \
  --output "$TAUTICK_SAMPLE_DIR/sample.ohlcv-1m.csv"

curl --fail --location --show-error --retry 2 \
  'https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/ohlcv-1s?sample_type=futures' \
  --output "$TAUTICK_SAMPLE_DIR/sample.ohlcv-1s.csv"
```

These commands replace files with the same names in that local directory. Preserve an existing copy separately if it is already an experiment input. Public sample URLs can change their contents; inspect and verify each new download before assigning a contract/date to it.

## Verified sample inventory

All five inspected CSVs contain **ESZ5**, the December 2025 E-mini S&P 500 contract, instrument ID **294973**, from **GLBX.MDP3**. The date is **2025-09-22**; times below are UTC. Bar ranges use `ts_event`; book ranges use `ts_recv`.

| Schema | Records inspected | First timestamp | Last timestamp | Extent |
| --- | ---: | --- | --- | --- |
| OHLCV-1m | 960 | 00:00:00 | 15:59:00 | Entire public minute-bar sample |
| OHLCV-1s | 22,972 | 00:00:00 | 15:59:59 | Entire public second-bar sample |
| MBP-1 | 10,000 | 00:00:00 | 00:12:22.205462340 | Local prefix of the larger sample |
| MBP-10 | 10,000 | 00:00:00 | 00:08:12.267119977 | Local prefix of the larger sample |
| MBO | 10,000 | 00:00:00 | 00:00:34.742986657 | Local prefix of the larger sample |

The bars span **[00:00, 16:00) UTC**, not a complete trading session. Second bars are sparse: 22,972 rows do not mean 22,972 consecutive seconds. Databento timestamps each bar at its interval start and omits intervals with no trades; its documented OHLCV aggregation uses trade receive time. Preserve this convention when comparing bar and tick inputs. [OHLCV schema](https://databento.com/docs/schemas/ohlcv).

The local book prefixes each end on an `F_LAST` boundary. The MBO prefix begins with a clear-book record and contains **8,832 snapshot-flagged records**, followed by incremental activity. Snapshot event timestamps can precede receipt timestamps. Preserve source order; do not sort the snapshot and updates together by `ts_event`.

Verification performed independently for this guide:

- All CSV row widths, record counts, contract identifiers, relevant timestamp ordering and supplied SHA-256 hashes match.
- Both bar files satisfy OHLC bounds and nonnegative volume checks. Grouping the second bars by minute reproduces **all 960 minute bars exactly**, using decimal prices and integer volume.
- Fresh unauthenticated downloads of both bar files match the local copies byte for byte.
- The supplied ZIP passes its integrity check, and every member matches its extracted local file.
- All 14 native DBN fixture hashes match; independent decoding also agrees with their recorded schemas, versions, symbols, counts and time ranges.

The full book downloads were not re-downloaded or validated in this pass. Structural checks and event-boundary checks do not establish complete book reconstruction or exchange execution fidelity.

## Reproduce the bar checks

Run this after the download commands, using Python 3 without additional packages. It checks the exact sample revision described above and independently resamples the second bars. A hash mismatch means the file differs from this reference; inspect whether the upstream sample changed or the download failed before updating expectations.

```bash
python3 - <<'PY'
import csv
import hashlib
import os
from decimal import Decimal
from pathlib import Path

root = Path(os.environ["TAUTICK_SAMPLE_DIR"])
expected = {
    "1m": (960, "cab4d4c81f7c8ac1ae1c109614f892ac22886ea14f49cbdbbeb68aaa1309d1d7"),
    "1s": (22972, "34f129fbc697bddfbce29caa871ab145519a6e9c2b7a49a57228d81af02c8f94"),
}
bars = {}
for resolution, (count, digest) in expected.items():
    path = root / f"sample.ohlcv-{resolution}.csv"
    if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
        raise SystemExit(f"{path.name}: differs from the verified 2026-11-06 sample")
    with path.open(newline="") as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == count
    assert {row["symbol"] for row in rows} == {"ESZ5"}
    assert {row["instrument_id"] for row in rows} == {"294973"}
    bars[resolution] = rows

def values(row):
    return [Decimal(row[key]) for key in ("open", "high", "low", "close")] + [int(row["volume"])]

aggregated = {}
for row in bars["1s"]:
    minute = row["ts_event"][:16]  # Canonical UTC ISO timestamps in this sample.
    current = values(row)
    if minute not in aggregated:
        aggregated[minute] = current
    else:
        bar = aggregated[minute]
        bar[1] = max(bar[1], current[1])
        bar[2] = min(bar[2], current[2])
        bar[3] = current[3]
        bar[4] += current[4]

assert len(aggregated) == len(bars["1m"]) == 960
for row in bars["1m"]:
    assert aggregated[row["ts_event"][:16]] == values(row), row["ts_event"]
print("Verified both hashes and all 960 resampled minute bars.")
PY
```

## What this supports for the MVP

Use the minute bars first, then the second bars to verify resampling. CSV price fields in these downloads are already decimal prices; do not apply DBN integer price scaling again. Preserve original UTC timestamps and keep missing values distinct from zero.

An optional VWAP overlay needs two qualifications:

- OHLCV does not retain the sum of every executed price times volume. Weighting a chosen bar price by volume is an approximation; document its price convention and label it accordingly. One-second bars also remain aggregates.
- The sample starts partway through the broader trading session. A calculation anchored at the sample start is not a complete session VWAP. Choose an explicit anchor within available coverage or obtain the preceding session history before claiming a session-wide result.

These inputs support a real-data chart prototype. The bars cannot provide the intervening trade/quote sequence required by Entry Trigger, and the short book extracts cannot supply that sequence for the entire 16-hour chart. The bundle is a development input, not evidence about strategy performance.

## Optional book samples

Databento's [public tick-data page](https://databento.com/tick-data) links the larger files:

- [MBP-1](https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/mbp-1?sample_type=futures): top-of-book events and trades; advertised around 350 MB when checked.
- [MBP-10](https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/mbp-10?sample_type=futures): ten-level depth events and trades; around 1.5 GB.
- [MBO](https://hist.databento.com/v0/dataset/sample/download/glbx.mdp3/mbo?sample_type=futures): order events; around 493 MB.

Those URLs return the larger samples, not the 10,000-record local prefixes. Download them separately only when needed. When making a prefix, preserve the header and original row order, include the necessary initialization snapshot, and finish at a complete event boundary. For MBO, `F_LAST` applies per instrument; do not treat an arbitrary fixed line count as a universal safe boundary. Refer to [CME normalization](https://databento.com/docs/datasets/glbx-mdp3) and the [MBO schema](https://databento.com/docs/schemas/mbo) before implementing book replay.

## Optional native DBN fixtures

The [official Python client's GLBX.MDP3 test directory](https://github.com/databento/databento-python/tree/f6529f1062e0642fe52334d767a72f2a75244398/tests/data/GLBX.MDP3) supplies 14 tiny `.dbn.zst` files at pinned revision `f6529f1062e0642fe52334d767a72f2a75244398`. Keep any downloaded copies outside the checkout too.

These fixtures are **ESH1 on 2020-12-28**, separate from the ESZ5 CSVs. They cover trades, TBBO, MBP-1, MBP-10, MBO, BBO-1s/1m, OHLCV-1s/1m/1h/1d, definitions, statistics and status. Each contains four records except definitions and daily bars, which contain one. All decoded as DBN version 3 using the existing local `databento==0.90.0` environment. DataFrame conversion emitted an upstream pandas deprecation warning but passed the content checks.

They are useful for format/decoder checks; four MBO records do not establish a complete order-book replay. Preserve upstream attribution and review applicable terms before any future redistribution. A client library's software license should not be treated as a blanket license for every market dataset it reads.

## Public repository policy and later NQ access

Commit this guide and reproducible verification instructions. Keep downloaded CSV/DBN files, ZIP bundles, credentials and private captures outside Git, including releases and Git LFS. No machine-specific scratch paths or data copies are needed to use these public sources.

Databento states that redistribution rights depend on the dataset and the original publisher's restrictions. Public download availability alone is not a blanket redistribution grant. This guide makes no determination that redistributing these particular samples is permitted or prohibited; documenting their public sources avoids the need to redistribute them. [Redistribution FAQ on Databento's pricing page](https://databento.com/pricing).

No NQ sample was verified in this pass. Specific NQ dates can be evaluated later through Databento's authenticated historical service or a separately validated ATAS export. As checked on 2026-11-06, Databento offers usage-based historical data without a monthly subscription fee and advertises $125 in signup credits, shared per team and expiring after six months. These terms may change; consult the [pricing and credits FAQ](https://databento.com/docs/faqs/usage-pricing-and-data-credits) before relying on them. Account-based historical requests are separate from the public downloads above.
