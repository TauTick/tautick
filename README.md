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

Build a native **Avalonia** viewer for **Windows and Linux** that opens a local minute-bar CSV and displays an interactive candlestick chart. Start with the verified ES sample: draw price/time axes, pan and zoom, and inspect a candle's timestamp, OHLC and volume with a crosshair.

Use a custom chart control with Avalonia/Skia drawing. Keep source adapters, market calculations and chart presentation separate so they can be developed and tested independently. Later, stretching the chart should reveal footprint detail for the same bars when suitable data is available; the initial OHLCV sample does not contain that detail.

The foundation now includes the .NET solution, immutable bar/import contracts, a chart input control and a native placeholder window wired to clearly synthetic data. CSV parsing and candle drawing are still to come.

See the [developer and contract guide](docs/development.md) for setup and component ownership, the [chart viewer MVP and modular design](docs/charting-mvp.md) for the remaining scope and proposed performance checks, and the [sample-data guide](docs/sample-data/README.md) for eventual input download instructions. Market data stays outside the repository.

## Build and run the foundation

Install .NET SDK **10.0.114** (pinned by `global.json`). From the repository root on Windows or Linux:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
dotnet test TauTick.slnx --no-build --no-restore
dotnet run --project src/TauTick.Desktop --no-build --no-restore
```

The window shows the synthetic dataset's identity and coverage above an empty chart area. Closing it exits the application. Linux needs a graphical X11/XWayland session; the guide also provides a native launch/close check using Xvfb. No market files or credentials are needed. Windows native startup remains unverified; see the guide's validation record.

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

TauTick has a buildable foundation and focused contract/wiring tests. The first CSV-to-candles workflow is not implemented yet. No chart performance capacity or Windows/Linux UI acceptance is claimed by the scaffold.

The architecture, APIs, and product direction will evolve as the first research workflows are implemented and validated.

## License

To be determined.
