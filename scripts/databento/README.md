# Databento MBO quote

[quote.py](quote.py) uses Databento’s free metadata API to estimate cost in USD and billable size in bytes. It defaults to `ESZ6` on `GLBX.MDP3` from 2026-10-28 to 2026-11-27.

Use Python 3.11 or later. Copy [`.env.example`](.env.example) to `.env` in this directory and set `DATABENTO_API_KEY` in the ignored file. [Databento API keys](https://databento.com/portal/keys)

Run these commands from the repository root.

```sh
python -m pip install databento "python-dotenv[cli]"
python -m dotenv -f scripts/databento/.env run -- python scripts/databento/quote.py
```

To override the defaults, append `--symbol NQZ6 --start 2026-11-02 --end 2026-11-03`. Dates mean midnight UTC. The start is inclusive and the end is exclusive.
