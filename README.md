# TauTick

**Open-source .NET platform for order-flow research and backtesting.**

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

Development starts deliberately small.

The first end-to-end workflow will:

- load historical market data
- render candlestick charts
- calculate multiple session-aware VWAP variants
- define simple, testable market-behavior conditions around VWAP
- compare those behaviors across historical sessions
- inspect individual results directly on the chart

The purpose of this milestone is to establish the foundations for a deeper order-flow research environment while validating TauTick's core research workflow.

## Platform research

Research on existing trading platforms is maintained in [trading-platform-research](https://github.com/mitjasiska/trading-platform-research). See [how to use this research when planning tasks](docs/platform-research.md) for the LEAN and NautilusTrader reports, relevant topics, and guidance for agents.

## Status

TauTick is at the beginning of development.

The architecture, APIs, and product direction will evolve as the first research workflows are implemented and validated.

## License

To be determined.
