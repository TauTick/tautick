# TauTick

**Open-source .NET workspace for visual order-flow research and reproducible historical experiments.**

TauTick is an open-source .NET trading research platform in development, designed to bridge discretionary order-flow analysis with systematic research and backtesting.

The goal is to let traders observe market behavior visually, turn those observations into testable hypotheses, validate them across historical data, and return to the chart to inspect what actually happened.

## Vision

TauTick aims to bring together:

- visual order-flow research
- quantitative strategy validation
- historical replay and event inspection
- L2/L3 market data and microstructure analysis
- systematic and discretionary trading workflows

Rather than treating discretionary chart analysis and quantitative research as separate disciplines, TauTick explores one continuous workflow:

**Observe → define → test → inspect → refine**

TauTick is being designed around a modular .NET core that can support desktop, research, data, and external-platform integrations.

## First milestone

The native **Avalonia** viewer opens a local Databento minute-bar CSV and displays candles with price and UTC time axes, fit-to-data and resizing. Pan/zoom, crosshairs and indicators are later work.

Use a custom chart control with Avalonia/Skia drawing. Keep source adapters, market calculations and chart presentation separate so they can be developed and tested independently. Later, stretching the chart should reveal footprint detail for the same bars when suitable data is available; the initial OHLCV sample does not contain that detail.

The desktop shell uses the [production CSV reader](src/TauTick.Adapters.Databento/README.md) and [custom chart](docs/chart-control.md) through the shared immutable contracts. Loading runs off the UI thread. Failed replacement preserves the previous chart and metadata; closing cancels pending work. The production application has no synthetic reader or synthetic-data dependency. See the [shell guide](docs/desktop-shell.md) and [integration validation report](docs/mvp-validation.md) for usage, measurements and outstanding acceptance checks.

See the [developer and contract guide](docs/development.md) for setup and component ownership, the [chart viewer MVP and modular design](docs/charting-mvp.md) for the remaining scope and proposed performance checks, and the [sample-data guide](docs/sample-data/README.md) for eventual input download instructions. Market data stays outside the repository.

## Build and run the viewer

Install .NET SDK **10.0.114** (pinned by `global.json`). From the repository root on Windows or Linux:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet run --project src/TauTick.Desktop --no-build --no-restore
```

Download the small public minute CSV using the [sample-data guide](docs/sample-data/README.md), outside the checkout. The window starts empty: enter the known **full contract year** (**2025** for the verified ESZ5 sample), then choose **Open CSV**. Short symbols do not identify a decade, so the application does not guess from a filename or recording date. The verified sample contains 960 M1 bars over **2025-09-22 [00:00, 16:00) UTC**, a partial session. **Fit to data** restores the full accepted range. Linux needs a graphical X11/XWayland session. The public sample needs no account or credentials; ordinary tests use only synthetic fixtures.

## Longer-term research workflow

The eventual experiment workflow makes a recorded NQ session easy to investigate:

1. Browse the session on a one-minute chart with relevant context, including session VWAP.
2. Select an interesting interval on the chart and save it with an optional description.
3. Open the interval for testing with preceding indicator history prepared automatically.
4. Play historical events at their recorded pace, slow playback, or step through events. Manually arm Entry Trigger and inspect its qualification, confirmation, and rejection reasons.
5. Change a setting and rerun against the same market inputs and manual arming boundary.
6. Save the interval and experiment together, including configurations and results, and reopen them later.

Playback speed controls how quickly the experiment is watched. It must not change detector timing or results. Historical playback is the initial scope; connection to the current live market comes later.

After the chart viewer, introduce richer data and indicators, then repeatable replay and automatic state preparation. The existing portable Entry Trigger detector in TradingLab is the integration target for that later workflow; the reuse mechanism still needs validation.

See the [recorded-session research brief](docs/recorded-session-research.md) for the later workflow's evidence, acceptance scenarios and open decisions. Shared terms are defined in the [glossary](GLOSSARY.md).

## Platform research

Research on existing trading platforms is maintained in [trading-platform-research](https://github.com/mitjasiska/trading-platform-research). See [how to use this research when planning tasks](docs/platform-research.md) for the LEAN and NautilusTrader reports, relevant topics, and guidance for agents.

## Status

Production CSV-to-candles integration is implemented. Release builds and 163 tests pass on Windows and Linux. Native loading, error recovery, replacement, fit, resizing and normal close were exercised on Windows at 100% and Ubuntu/WSLg at 100% and 200% scaling; Linux closing-during-load was also exercised. Windows high-DPI and closing-during-load interaction remain unverified. See the [validation report](docs/mvp-validation.md) for measured scope and limitations. Complete cross-platform interactive acceptance and presented-frame performance are not claimed.

The architecture, APIs, and product direction will evolve as the first research workflows are implemented and validated.

## License

To be determined.
