# Chart viewer MVP and modular design

Status: MVP planning draft, 2026-11-06. The foundation scaffold and shared bar/import/chart-input contracts are implemented; see [the developer guide](development.md). The [custom candle chart](chart-control.md) provides axes, fit-to-data and resize/DPI handling, with native checks and basic software redraw measurements. The [Databento minute CSV reader](../src/TauTick.Adapters.Databento/README.md) produces datasets through the shared contract. Desktop real-file integration and the complete interactive viewer remain future work; presented-frame performance has not been measured.

The agreed direction is a native .NET/Avalonia application for Windows and Linux, a custom chart control using Avalonia/Skia drawing, and separate components for source adapters, calculations, and presentation. The chart should eventually reveal footprint detail as the user stretches its time scale. The current four production projects and their contracts are recorded in the developer guide; the additional module boundaries and performance targets below remain proposals for implementation planning.

## First usable milestone

Open one local Databento OHLCV-1m CSV, display candlesticks with time and price axes, pan and zoom, reset the view, and inspect a candle's timestamp, OHLC and volume with a crosshair. Show the contract, timeframe, UTC timezone and available coverage. Explain invalid input without presenting a partially accepted file as complete.

Start with the verified 960 ESZ5 minute bars described in the [sample-data guide](sample-data/README.md). They cover part of a session. Keep downloaded data outside Git; synthetic fixtures may be checked in and must be identified as synthetic.

VWAP, real footprint data, MBO reconstruction, historical playback and Entry Trigger are later increments. The first viewer establishes reusable data and chart boundaries without implementing all of them. The [recorded-session brief](recorded-session-research.md) describes the longer-term experiment workflow.

## One chart, multiple representations

A bar is the underlying market summary. A candle and a footprint are representations of that bar. The chart owns one viewport, time/price transforms, axes, crosshair, hit testing and interval selection; renderers share those services.

The intended interaction is semantic zoom:

- At a compact scale, display candles.
- When bars have enough horizontal room and price rows are readable, reveal footprint detail if the corresponding data is available.
- Keep the same instrument, bar boundaries, timeframe, selected interval and cursor anchor across the transition. Zooming changes presentation, not the aggregation period or calculation results.
- Use distinct entry/exit thresholds to avoid flickering between representations near the transition. Exact thresholds, vertical scaling behavior and a manual mode override remain UI decisions to validate.
- Treat grouping several price ticks into one displayed footprint row as a separate presentation setting. Preserve the underlying per-price data and make the displayed row size clear.

ATAS documents switching from candles to footprints by stretching the time scale. Its price-level scale setting is a separate form of grouping. These are interaction references, not claims about TauTick's implementation. [ATAS footprint guide](https://atas.net/blog/how-to-read-footprint/), [ATAS scale function](https://help.atas.net/en/support/solutions/articles/72000602462-scale-function).

Footprints require executed volume by price within each bar, with aggressor classification for a bid/ask breakdown. Individual trades or a validated precomputed footprint can supply it; full MBO is not required. OHLCV alone cannot recover that distribution. Resting-order additions and cancellations must not be counted as executed volume. Unknown aggressor side stays explicit.

Availability is specific to the recording, instrument and interval. Distinguish unsupported detail, not-yet-loaded detail and incomplete coverage from known zero volume. With the initial CSV, stretching the chart keeps candles visible and explains that footprint detail is unavailable. A future detail request runs outside drawing callbacks; a superseded request cannot replace the current view.

If bars and footprint detail come from different sources, verify instrument identity, timestamps, bar boundaries, trade eligibility and coverage before treating them as representations of the same data. Matching a symbol and approximate timestamp is insufficient.

## Component responsibilities

These are code boundaries within one application, not separate services or a runtime plugin system. Create only the projects and contracts needed by the first increment.

| Component | Owns | Boundary |
| --- | --- | --- |
| Market-data contracts | Instrument identity, price/tick metadata, bar interval and timestamp meaning, coverage and data availability | Plain .NET types without UI or provider SDK dependencies. Add trade, quote and order-update contracts when those capabilities are implemented. |
| Source adapters | Reading provider files or connecting to a source, decoding, mapping identities and preserving source semantics | Produce supported data plus provenance, coverage and errors. Do not draw or choose chart modes. The first adapter reads Databento minute CSVs. |
| Market calculations | Bar aggregation, footprint aggregation, VWAP and eventually order-book state | Consume market data independently of Avalonia; preserve ordering and declared calculation rules. Add each calculation when required. |
| Chart presentation | Viewport, shared scales, visible-range lookup, hit testing and representation selection | Read prepared data. No file reads, provider calls or market-state reconstruction during drawing. |
| Chart drawing | Candles, later footprints, axes and interaction overlays | Draw the visible prepared content through Avalonia/Skia. Keep expensive content separate from frequently changing overlays. |
| Desktop application | Windows, commands, file selection, progress, cancellation and component wiring | Compose adapters and chart components; keep market rules out of window code. |

Dependency direction: adapters and calculations depend on the market-data contracts; chart presentation consumes those contracts and prepared results; Avalonia drawing depends on chart presentation; the application wires them together. Adapters and calculation code must build and run without Avalonia.

A historical file reader and a live connection have different lifecycles. Share market meanings while keeping historical range requests separate from connection, subscription and recovery operations. Do not force every source to pretend it supports bars, trades, quotes and MBO equally. Preserve original timestamps, precision, ordering and source-specific validity/reset information in any future event mapping.

## Independent development

Agree on a minimal contract and a small synthetic fixture before splitting implementation work. For the first milestone this means instrument metadata, the bar structure, interval/timestamp semantics, coverage, validation errors and the lifetime of a loaded dataset. Do not design a universal event framework in advance.

After that agreement, the importer can be developed against the fixture expectations while chart work uses an in-memory source implementing the same contract. Calculations can later be tested without a window, and new adapters can be checked against the same relevant contract cases. A footprint renderer can use clearly synthetic per-price fixtures before a real trade adapter is available.

Every task should name its owned component, required contract, fixture and integration check. Shared contract changes must be coordinated explicitly rather than implemented differently in each component. A component that works alone still needs an end-to-end acceptance check through the application.

Useful contract cases include duplicate/out-of-order bars, missing intervals, partial coverage, unknown trade side, cancellation during loading, and different data availability across intervals. Start with cases applicable to the implemented capability; no placeholder implementations are required for later features.

## Performance and correctness boundaries

For the candle viewer, load and validate off the UI thread, then publish a stable bar dataset. Find the visible index range without scanning all loaded bars on every redraw. Reuse drawing resources where possible; avoid a separate UI control per bar or price cell. Profile axis labels and text as well as candle geometry.

Separate chart content from cursor/selection overlays so a mouse move need not redo market calculations or rebuild historical geometry. Define cache invalidation for data changes, viewport changes, style and DPI changes. Cached buffers must remain valid for the duration of a render; a producer must not overwrite data still being drawn.

For future event processing, keep a controlled owner of mutable book/calculation state and publish coherent presentation data separately. Coalesce intermediate screen updates, not required market events. Historical processing can apply backpressure or slow playback instead of dropping inputs. Bound queues and caches deliberately; a streaming API alone does not bound total memory.

These principles are informed by the [LEAN concurrency study](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/lean/architecture.md#61-historical-execution-topology) and [NautilusTrader representation study](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/nautilus-trader/architecture.md#82-representation-and-allocation-choices). Those reports did not run benchmarks. ATAS separately documents [visible-range drawing and resource reuse](https://docs.atas.net/en/chart-indicators/performance.html); Avalonia supports [custom drawing and direct Skia access](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering). None establishes TauTick's performance before measurement.

Initial proposed measurement workload: the real 960-bar sample and a synthetic case with 100,000 loaded bars and roughly 1,000 visible. Target responsive 60 FPS interaction initially, with a 16.7 ms frame budget; record frame-time percentiles, input latency, load-to-first-chart time, allocations and memory on named Windows and Linux machines, including high-DPI behavior. Measure loading separately from steady interaction and test repeated open/close cycles. These are proposed targets, not results.

MBO will require separate correctness, sustained-throughput and burst tests with sufficient recordings. The short book extracts are useful fixtures; smooth candle drawing does not prove MBO capacity.

## Next implementation sequence

1. The minimal bar/import contract, synthetic fixture, Databento CSV reader and custom candle chart are established. Retain the documented module checks and validate the composed native shell on Windows and Linux during integration.
2. Connect file opening to the reader and chart, then add pan/zoom, crosshair inspection, errors and cancellation to complete the first usable viewer.
3. Measure responsiveness, correctness and resource lifetime before broadening the feature set.
4. Add a clearly defined VWAP calculation and then validated per-price trade data with footprint rendering/semantic zoom. Exact session VWAP needs adequate session trade coverage; any bar-based approximation needs a separate label and convention.
5. Introduce MBO reconstruction, replay and experiment features as independently specified increments with their own data and performance acceptance criteria.

The foundation fixes the current project layout, package versions and bar representations in code. Open implementation choices include supported OS versions, price/time representations for future event data, footprint transition thresholds and row grouping, session conventions, and quantitative performance targets beyond the initial workload.
