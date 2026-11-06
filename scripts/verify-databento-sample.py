"""Compare the headless reader's output against an explicit local Databento minute CSV.

No network requests, downloads, or data files are created. Run from any directory
after building samples/TauTick.Databento.Verify in Debug. Python standard library only.
"""

import argparse
import csv
from datetime import datetime, timedelta
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("csv", type=Path)
parser.add_argument("contract_year", type=int)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
path = args.csv.resolve()
reference_hash = "cab4d4c81f7c8ac1ae1c109614f892ac22886ea14f49cbdbbeb68aaa1309d1d7"
actual_hash = hashlib.sha256(path.read_bytes()).hexdigest()
run = subprocess.run(
    ["dotnet", str(root / "samples/TauTick.Databento.Verify/bin/Debug/net10.0/TauTick.Databento.Verify.dll"),
     str(path), str(args.contract_year)],
    check=True, capture_output=True, text=True,
)
data = json.loads(run.stdout, parse_float=Decimal)
assert data["Sha256"] == actual_hash
with path.open(encoding="utf-8-sig", newline="") as stream:
    rows = list(csv.DictReader(stream))
assert len(rows) == data["Count"] == len(data["Bars"])
for source, bar in zip(rows, data["Bars"], strict=True):
    # This minute-only check demands a zero fractional part, avoiding datetime's microsecond truncation.
    timestamp = source["ts_event"].replace("Z", "+00:00")
    main, _, offset = timestamp.partition("+")
    seconds, dot, fraction = main.partition(".")
    assert not dot or set(fraction) <= {"0"}
    assert offset == "00:00"
    start = datetime.fromisoformat(seconds + "+00:00")
    assert start == datetime.fromisoformat(bar["StartUtc"])
    assert start + timedelta(minutes=1) == datetime.fromisoformat(bar["EndUtc"])
    assert bar["Interval"] == "00:01:00"
    for field in ("open", "high", "low", "close"):
        assert Decimal(source[field]) == bar[field.capitalize()], (source["ts_event"], field)
    assert int(source["volume"]) == bar["Volume"]
    assert source["symbol"] == data["Instrument"]["Symbol"]
    assert source["instrument_id"] == data["Source"]["InstrumentId"]
    assert source["rtype"] == "33" and source["publisher_id"] == "1"
assert data["Source"]["Provider"] == "Databento"
assert data["Source"]["Dataset"] == "GLBX.MDP3"
assert data["Source"]["Schema"] == "ohlcv-1m"
assert data["Instrument"]["Venue"] == "GLBX"
assert data["Coverage"]["StartUtc"] == data["Bars"][0]["StartUtc"]
assert data["Coverage"]["EndUtc"] == data["Bars"][-1]["EndUtc"]
assert data["Coverage"]["HasGaps"] == any(
    current["StartUtc"] != previous["EndUtc"]
    for previous, current in zip(data["Bars"], data["Bars"][1:])
)
is_reference = actual_hash == reference_hash
if is_reference:
    assert data["Count"] == 960 and data["Instrument"]["Symbol"] == "ESZ5"
    assert data["Instrument"]["ContractMonth"] == "2025-12-01"
    assert data["Coverage"]["StartUtc"] == "2025-09-22T00:00:00+00:00"
    assert data["Bars"][-1]["StartUtc"] == "2025-09-22T15:59:00+00:00"
    assert data["Coverage"]["EndUtc"] == "2025-09-22T16:00:00+00:00"
print(f"SHA-256: {actual_hash}")
print("Recorded reference sample" if is_reference else "Different sample revision; reference acceptance not established")
print(f"Verified all {data['Count']} bars exactly against source; {data['Instrument']['Symbol']}, "
      f"contract month {data['Instrument']['ContractMonth']}, source instrument {data['Source']['InstrumentId']}")
print(f"Coverage: [{data['Coverage']['StartUtc']}, {data['Coverage']['EndUtc']}), "
      f"gaps={data['Coverage']['HasGaps']}; no session-completeness claim")
