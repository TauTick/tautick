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

## Installation and quick start

1. Obtain the source with Git, then enter the checkout:

   ```sh
   git clone https://github.com/TauTick/tautick.git
   cd tautick
   ```

2. Install a stable **.NET 10.0 SDK, version 10.0.114 or newer**, for your platform from the
   [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
   [global.json](global.json) sets the minimum version and uses `latestFeature`
   roll-forward to select the newest installed stable 10.0 SDK, including newer
   feature bands such as 10.0.302. Future 10.0 SDK updates do not require editing
   this file. A runtime alone does not include the SDK. Ensure that installation's
   `dotnet` is on `PATH`; reopen your terminal after installation.

   **Windows:** use a graphical Windows desktop and the included Windows PowerShell.
   PowerShell 7 is not required.

   **Linux:** use Bash and a graphical X11 or XWayland session. On Ubuntu 24.04,
   install the native libraries with:

   ```bash
   sudo apt-get install libx11-6 libice6 libsm6 libfontconfig1 libxrandr2 libxi6 libxcursor1
   ```

   Other distributions need equivalent packages; see the
   [Avalonia Linux guide](https://docs.avaloniaui.net/docs/platform-specific-guides/linux).
   No .NET workloads, provider SDK, API key or additional scripting runtime are needed.

3. Launch with the single command for your platform:

   **Windows PowerShell:**

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\run.ps1
   ```

   `Bypass` applies only to this process and does not change the saved execution
   policy. Organization policy may still apply.

   **Linux Bash:**

   ```bash
   bash ./run.sh
   ```

Use the same command on later launches. Each launcher restores with the committed
package locks in locked mode, builds the solution incrementally in **Debug**, then
opens `src/TauTick.Desktop` without another restore or build. Unchanged compiled
outputs are reused; relevant source or project changes rebuild as needed. Routine
launch does not run tests. Failures remain visible in the terminal and stop launch;
the launcher returns the failed stage's or application's exit status.

These commands assume the checkout is the current directory. From elsewhere, pass
the quoted full launcher path, for example
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Source Projects\tautick\run.ps1"`
or `bash "/home/me/Source Projects/tautick/run.sh"`.

The first launch opens an initially empty workspace; later launches restore saved
layouts and input references. Download the public minute CSV outside the checkout
using the [sample-data guide](docs/sample-data/README.md). Create/select a chart,
enter the known **full contract year** (**2025** for the verified ESZ5 sample), then
choose **Open CSV**. The application does not guess the decade. The sample contains
960 M1 bars over **2025-09-22 [00:00, 16:00) UTC**, a partial session. **Fit** restores
the full accepted range. The public sample needs no account or credentials.

## Current workspace

The native **Avalonia** workspace hosts independent candle charts in named workspace tabs, split panes and detached windows. Charts support pan/zoom, inspection, fit-to-data and explicit contract-year CSV imports. Layouts and verified input references restore on restart. The [indicator manager](docs/indicators.md) adds independent bar-based VWAP studies with explicit anchors and workspace persistence. See the [workspace guide](docs/workspace.md) for controls, the chart-host interface and validation limits.

Use a custom chart control with Avalonia/Skia drawing. Keep source adapters, market calculations and chart presentation separate so they can be developed and tested independently. Later, stretching the chart should reveal footprint detail for the same bars when suitable data is available; the initial OHLCV sample does not contain that detail.

The desktop shell uses the [production CSV reader](src/TauTick.Adapters.Databento/README.md) and [custom chart](docs/chart-control.md) through the shared immutable contracts. Loading runs off the UI thread. Failed replacement preserves the previous chart and metadata; closing cancels pending work. The production application has no synthetic reader or synthetic-data dependency. See the [shell guide](docs/desktop-shell.md) and [integration validation report](docs/mvp-validation.md) for usage, measurements and outstanding acceptance checks.

See the [developer and contract guide](docs/development.md) for setup and component ownership, the [chart viewer MVP and modular design](docs/charting-mvp.md) for the remaining scope and proposed performance checks, and the [sample-data guide](docs/sample-data/README.md) for eventual input download instructions. Market data stays outside the repository.

## Testing

After a successful launcher build, run tests separately from the repository root:

```sh
dotnet test TauTick.slnx --configuration Debug --no-build --no-restore
```

Ordinary tests use only synthetic fixtures. See the [developer guide](docs/development.md#build-test-and-launch) for explicit build/test commands.

Agents can also exercise the native viewer and visually inspect charts on a Linux
machine without a graphical login. See [native Linux visual checks](docs/development.md#native-visual-checks-on-linux) for setup, input, capture and cleanup.

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

The dockable workspace is implemented; see its [validation record](docs/workspace-validation.md) for current checks and outstanding Windows/physical monitor coverage. The following CSV integration results describe the earlier milestone.

Production CSV-to-candles integration is implemented. Release builds and 163 tests pass on Windows and Linux. Native loading, error recovery, replacement, fit, resizing and normal close were exercised on Windows at 100% and Ubuntu/WSLg at 100% and 200% scaling; Linux closing-during-load was also exercised. Windows high-DPI and closing-during-load interaction remain unverified. See the [validation report](docs/mvp-validation.md) for measured scope and limitations. Complete cross-platform interactive acceptance and presented-frame performance are not claimed.

The architecture, APIs, and product direction will evolve as the first research workflows are implemented and validated.

## License

To be determined.
