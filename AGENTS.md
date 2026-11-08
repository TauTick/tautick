# Agent guidance

## Linear task target

Use this default target for issues authored for this repository when the user does not specify a different target:

- Workspace: **[Mitja Siska](https://linear.app/mitjasiska)**.
- Team: **TauTick** (key: `TAU`).
- Project: **[TauTick](https://linear.app/mitjasiska/project/tautick-dfc1f79ea8d6)**.

Resolve the team by its key and the project by its linked URL when using Linear tools. Set both on new issues. An explicitly selected issue retains its existing team and project unless a move is requested.

## Project direction

- Build a native .NET/Avalonia application for Windows and Linux, with a custom chart control using Avalonia/Skia drawing.
- Keep source adapters and market calculations independent of Avalonia. Chart drawing consumes prepared data; it does not read provider files or reconstruct market state.
- Candles and footprints share the chart viewport and interactions. Changing display detail preserves the underlying bar boundaries and timeframe.
- Agree on the relevant data contract and fixtures before splitting work across components. Keep each task scoped to its owned component and include an integration check.

## Documentation

- Read [README.md](README.md) for the current milestone and [GLOSSARY.md](GLOSSARY.md) for shared terminology.
- Use the [chart viewer plan](docs/charting-mvp.md) for MVP scope, proposed component boundaries and performance checks. Distinguish agreed direction from proposals still awaiting validation.
- Consult the [recorded-session brief](docs/recorded-session-research.md) when working on the later replay and experiment workflow.
- Use the [platform research guide](docs/platform-research.md) when creating tasks or resolving relevant design questions. Read the applicable sections, and keep task specifications self-contained because the external research repository is private.

## Market data and validation

- Follow the [sample-data guide](docs/sample-data/README.md). Keep downloaded market data, private recordings, archives and credentials outside Git. Commit instructions, source links and clearly identified synthetic fixtures.
- Preserve instrument identity, timestamp meaning and precision, source ordering, and coverage. Distinguish unavailable data from zero values; do not infer footprint detail from OHLCV bars.
- Validate the behavior affected by a change. Report checks actually run, and distinguish measured performance and native UI validation from estimates or source inspection. Verify Windows and Linux behavior before claiming cross-platform acceptance.
