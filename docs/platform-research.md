# Existing trading-platform research

[trading-platform-research](https://github.com/mitjasiska/trading-platform-research) contains architecture studies of existing trading platforms. Use them as optional reference material when creating or refining TauTick tasks, especially when a task involves a design question or a known failure mode.

## Available studies

These links pin the reports to the revision reviewed during TauTick planning (`98e5fa60d5c3e75c625420295596b7b6f6c9071c`):

| Study | Topics relevant to TauTick |
| --- | --- |
| [QuantConnect LEAN](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/lean/architecture.md) | Historical-data processing, event ordering, indicator warmup, and memory retained by streaming pipelines. |
| [NautilusTrader](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/nautilus-trader/architecture.md) | Event timestamps, simulated clocks, replay processing, and boundaries between market, order, and execution state. |

The reports distinguish source findings, interpretations, and possible lessons. They describe particular upstream revisions; their findings do not establish TauTick's architecture or demonstrate TauTick's performance.

## Using the research in tasks

1. Start with the TauTick behavior the task should deliver and the question the research could help resolve.
2. Read the relevant report sections. Routine implementation tasks do not need a full review of both platforms.
3. Include the applicable lesson or constraint in the task itself, with a link to the report section and revision. Distinguish an observed upstream behavior from a proposed TauTick decision; verify upstream details if the task depends on them.
4. Turn the lesson into concrete acceptance criteria or a focused investigation. Keep unresolved choices explicit.

For example, a VWAP task should specify where accumulation starts, what happens when earlier session data is missing, and whether the inputs support trade VWAP or a labeled bar-based approximation. Later, a replay task can require identical results for the same inputs and arming boundary at different playback speeds. The reports can help identify these questions; the task must define the expected TauTick behavior.

For the initial chart-rendering milestone and possible VWAP calculation, consult the studies only where they help with historical data, timestamps, or indicator initialization. Broader execution-engine features can wait for tasks that need them.

## Access

The research repository is private. Agents and contributors need authorized GitHub access to inspect it. Keep task descriptions self-contained, including the required behavior, constraints, and acceptance criteria, so implementation does not depend on access to the reports. If access is unavailable, record that limitation rather than claiming the research was checked.
