"""Exercise the quote CLI offline with a metadata-only client stub."""

from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
import os
from pathlib import Path
import runpy
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, call, patch
import warnings


SCRIPT = Path(__file__).with_name("quote.py")
STUB_KEY = "stub-credential-for-testing"


class QuoteTests(unittest.TestCase):
    def setUp(self):
        self.metadata = Mock(spec_set=["get_cost", "get_billable_size"])
        self.metadata.get_cost.return_value = 12.3456789
        self.metadata.get_billable_size.return_value = 12345678
        self.historical = Mock(return_value=SimpleNamespace(metadata=self.metadata))

    def run_quote(self, *args, key=STUB_KEY, client_available=True):
        stdout, stderr = StringIO(), StringIO()
        env = {"DATABENTO_API_KEY": key} if key is not None else {}
        client = SimpleNamespace(Historical=self.historical) if client_available else None
        with (
            patch.dict(os.environ, env, clear=True),
            patch.dict(sys.modules, {"databento": client}),
            patch.object(sys, "argv", [str(SCRIPT), *args]),
            redirect_stdout(stdout),
            redirect_stderr(stderr),
        ):
            status = 0
            try:
                runpy.run_path(str(SCRIPT), run_name="__main__")
            except SystemExit as error:
                status = error.code
        return status, stdout.getvalue(), stderr.getvalue()

    def assert_metadata_quote(self, symbol, start, end):
        request = dict(
            dataset="GLBX.MDP3", schema="mbo", symbols=[symbol], stype_in="raw_symbol", start=start, end=end,
        )
        self.historical.assert_called_once_with(key=STUB_KEY)
        self.assertEqual(self.metadata.mock_calls, [call.get_cost(**request), call.get_billable_size(**request)])

    def test_default_quotes_only_the_raw_contract(self):
        status, stdout, stderr = self.run_quote()
        self.assertEqual(status, 0, stderr)
        self.assertEqual(stderr, "")
        self.assert_metadata_quote("ESZ6", "2026-10-28T00:00:00Z", "2026-11-27T00:00:00Z")
        for expected in (
            "GLBX.MDP3 mbo quote for ESZ6",
            "2026-10-28T00:00:00Z (inclusive) to 2026-11-27T00:00:00Z (exclusive)",
            "12.3456789 USD",
            "12,345,678 bytes",
        ):
            self.assertIn(expected, stdout)
        self.assertNotIn(STUB_KEY, stdout)

    def test_overrides_reach_both_calls_as_utc(self):
        status, stdout, stderr = self.run_quote(
            "--symbol", "NQZ6", "--start", "2026-11-02", "--end", "2026-11-02T01:00:00.000000001+01:00",
        )
        self.assertEqual(status, 0, stderr)
        self.assert_metadata_quote("NQZ6", "2026-11-02T00:00:00Z", "2026-11-02T00:00:00.000000001Z")
        self.assertIn("2026-11-02T00:00:00.000000001Z (exclusive)", stdout)

    def test_invalid_input_does_not_call_databento(self):
        cases = (
            (["--start", "2026-02-30"], "invalid date or time"),
            (["--start", "2026-11-29"], "--end must be later"),
            (["--end", "2026-10-28T01:00:00+01:00"], "--end must be later"),
            (["--symbol", "ALL_SYMBOLS"], "one raw contract"),
        )
        for args, message in cases:
            with self.subTest(args=args):
                status, stdout, stderr = self.run_quote(*args)
                self.assertNotEqual(status, 0)
                self.assertEqual(stdout, "")
                self.assertIn(message, stderr)
        self.historical.assert_not_called()

    def test_help_and_missing_setup(self):
        status, stdout, stderr = self.run_quote("--help", key=None, client_available=False)
        self.assertEqual(status, 0, stderr)
        for expected in ("Exclusive UTC end", "--symbol", "DATABENTO_API_KEY", "pip install databento"):
            self.assertIn(expected, stdout)

        for key, message in ((None, "set DATABENTO_API_KEY"), (STUB_KEY, "pip install databento")):
            with self.subTest(message=message):
                status, stdout, stderr = self.run_quote(key=key, client_available=False)
                self.assertNotEqual(status, 0)
                self.assertEqual(stdout, "")
                self.assertIn(message, stderr)
        self.historical.assert_not_called()

    def test_quote_error_hides_sdk_exception_details(self):
        for method in (self.metadata.get_cost, self.metadata.get_billable_size):
            with self.subTest(method=method):
                method.side_effect = RuntimeError(f"SDK error with {STUB_KEY}")
                status, stdout, stderr = self.run_quote()
                method.side_effect = None
                self.assertNotEqual(status, 0)
                self.assertEqual(stdout, "")
                self.assertIn("Databento quote failed", stderr)
                self.assertNotIn(STUB_KEY, stderr)

    def test_unexpected_response_fails_without_printing_partial_quote(self):
        self.metadata.get_billable_size.return_value = None
        status, stdout, stderr = self.run_quote()
        self.assertNotEqual(status, 0)
        self.assertEqual(stdout, "")
        self.assertIn("Databento returned an invalid quote", stderr)

    def test_warnings_keep_diagnostics_without_the_key(self):
        def get_cost(**request):
            warnings.warn(f"Partial coverage for {STUB_KEY}")
            return 0

        self.metadata.get_cost.side_effect = get_cost
        self.metadata.get_billable_size.return_value = 0
        status, stdout, stderr = self.run_quote()
        self.assertEqual(status, 0, stderr)
        self.assertIn("0 USD", stdout)
        self.assertIn("0 bytes", stdout)
        self.assertIn("Partial coverage for [redacted]", stderr)
        self.assertNotIn(STUB_KEY, stderr)
