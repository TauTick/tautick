"""Estimate the cost and billable size of a Databento MBO request."""

import argparse
from datetime import datetime, timezone
import os
import re
import sys
import warnings


UTC_BOUNDARY = re.compile(
    r"(?P<date>[0-9]{4}-[0-9]{2}-[0-9]{2})"
    r"(?:T(?P<minute>[0-9]{2}:[0-9]{2})"
    r"(?::(?P<second>[0-9]{2})(?:\.(?P<fraction>[0-9]{1,9}))?)?"
    r"(?P<offset>Z|[+-](?:[01][0-9]|2[0-3]):[0-5][0-9])?)?"
)


def parse_boundary(value: str) -> str:
    match = UTC_BOUNDARY.fullmatch(value)
    if not match:
        raise argparse.ArgumentTypeError("expected a YYYY-MM-DD date or ISO 8601 timestamp")

    clock = f"{match['minute'] or '00:00'}:{match['second'] or '00'}"
    offset = match["offset"]
    if offset in (None, "Z"):
        offset = "+00:00"
    try:
        timestamp = datetime.fromisoformat(f"{match['date']}T{clock}{offset}")
        utc = timestamp.astimezone(timezone.utc).isoformat(timespec="seconds")
    except (ValueError, OverflowError):
        raise argparse.ArgumentTypeError("invalid date or time") from None

    # Keep the fractional digits separate from datetime's microsecond precision.
    seconds = utc.removesuffix("+00:00")
    fraction = (match["fraction"] or "").rstrip("0")
    return f"{seconds}.{fraction}Z" if fraction else f"{seconds}Z"


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__,
        epilog=(
            "Use YYYY-MM-DD for midnight UTC or an ISO 8601 timestamp such as 2026-11-02T14:30:00Z. "
            "Timestamps without an offset are treated as UTC. "
            "Install the client with 'python -m pip install databento' and set DATABENTO_API_KEY."
        ),
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--symbol", default="ESZ6", help="Raw contract symbol")
    parser.add_argument(
        "--start", type=parse_boundary, default="2026-10-28T00:00:00Z", help="Inclusive UTC start",
    )
    parser.add_argument(
        "--end", type=parse_boundary, default="2026-11-27T00:00:00Z", help="Exclusive UTC end",
    )
    args = parser.parse_args()
    symbol = args.symbol.strip()
    if not symbol or symbol.upper() == "ALL_SYMBOLS" or "," in symbol:
        parser.error("--symbol must name one raw contract")

    # Canonical UTC strings sort chronologically once the trailing Z is removed.
    if args.start.removesuffix("Z") >= args.end.removesuffix("Z"):
        parser.error("--end must be later than --start")

    key = os.environ.get("DATABENTO_API_KEY", "").strip()
    if not key:
        parser.error("set DATABENTO_API_KEY in the environment")

    try:
        import databento
    except ImportError:
        parser.exit(1, "error: Databento client unavailable. Run 'python -m pip install databento'.\n")

    request = dict(
        dataset="GLBX.MDP3",
        schema="mbo",
        symbols=[symbol],
        stype_in="raw_symbol",
        start=args.start,
        end=args.end,
    )
    try:
        with warnings.catch_warnings(record=True) as client_warnings:
            metadata = databento.Historical(key=key).metadata
            cost = metadata.get_cost(**request)
            size = metadata.get_billable_size(**request)
    except Exception:
        # Client exceptions can contain authentication details.
        parser.exit(1, "error: Databento quote failed. Check your API key, symbol, UTC range, and connection.\n")

    if type(cost) not in (int, float) or not 0 <= cost < float("inf") or type(size) is not int or size < 0:
        parser.exit(1, "error: Databento returned an invalid quote.\n")

    for warning in client_warnings:
        message = str(warning.message).replace(key, "[redacted]")
        print(f"Databento warning: {message}", file=sys.stderr)

    print(f"{request['dataset']} {request['schema']} quote for {symbol}")
    print(f"UTC range: {args.start} (inclusive) to {args.end} (exclusive)")
    print(f"Estimated cost: {cost} USD")
    print(f"Billable size: {size:,} bytes")


if __name__ == "__main__":
    main()
