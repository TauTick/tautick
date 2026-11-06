# Recorded-session research: longer-term workflow

Status: planning draft, updated 2026-11-06. No TauTick application or replay implementation exists yet. The immediate milestone is the [Avalonia chart viewer](charting-mvp.md) for Windows and Linux; this brief describes the later experiment workflow. Verified ES bar samples and short book extracts are available through the [sample-data guide](sample-data/README.md), but a suitable NQ recording for the full workflow remains to be acquired. Product requirements below come from the owner's proposed workflow. Replay approaches remain proposals to validate.

## Purpose and experience

Make it practical to revisit a discretionary observation, inspect Entry Trigger's response, and change one setting without reconstructing the experiment. Learning agentic development is an additional benefit; reducing friction in the trading research workflow is the product objective.

The eventual experiment workflow supports one recorded NQ contract/session at a time:

1. Browse an M1 chart with relevant context, initially including session VWAP.
2. Select an interval directly on the chart and optionally describe the observation, such as “strong selling with little downward progress.” Save it without having to arm anything.
3. Open the interval for testing. Prepare preceding history automatically and show preparation progress or missing coverage. The trader should not calculate warmup dates manually.
4. Play at the recorded pace, slow down, pause, or step through events. Manually arm long or short and inspect qualification, confirmation, invalidation, expiry, or rejection.
5. Rerun a changed configuration against the same data and manual actions. Show when and why the result differs, including no-signal outcomes.
6. Save and reopen the interval and experiment with their inputs, settings, actions, and results intact.

Historical playback means recorded events presented at their original pace. A current-market connection belongs to later work. Initial evaluation concerns detector decisions and signal prices; execution simulation, automated exits, and trading-profit results need separate scope.

Example: select a few minutes around a reversal near VWAP, start watching before the approach, and arm at the point where an entry would have been considered. Compare the original configuration with a changed response threshold. Inspect the blocking conditions, confirmation delay, and price movement before the signal. VWAP supplies visual research context; it is not automatically a new Entry Trigger condition.

## Existing work and reuse boundary

The first source inspection used TradingLab revision [`1209cd8`](https://github.com/mitjasiska/TradingLab/tree/1209cd863bbd0eb9459c2754512ec329961ae911). This is read-only evidence; no build or native ATAS acceptance was performed during this planning pass.

- [`EntryTrigger.Core.csproj`](https://github.com/mitjasiska/TradingLab/blob/1209cd863bbd0eb9459c2754512ec329961ae911/apps/atas-indicators/src/EntryTrigger.Core/EntryTrigger.Core.csproj) targets plain `net10.0` and declares no package/project/vendor references. Root build properties only supply an ATAS installation path. This makes core reuse a credible starting point; cross-repository packaging and an offline build still require validation.
- The [detector contract](https://github.com/mitjasiska/TradingLab/blob/1209cd863bbd0eb9459c2754512ec329961ae911/apps/atas-indicators/docs/entry-trigger-detector.md) already defines continuous pre-arm observation, ineffective-aggression/fade candidates, fresh directional confirmation, manual activation, quote-path handling, and timing. Reuse its versioned rules rather than duplicate or redefine the detector here.
- [DEV-149](https://linear.app/mitjasiska/issue/DEV-149/add-opportunity-recording-and-statistical-evaluation-to-entry-trigger) covers opportunity recording; [DEV-150](https://linear.app/mitjasiska/issue/DEV-150/build-an-offline-entry-trigger-replay-and-comparison-tool) covers offline replay and configuration comparison. Both were In Progress when inspected. Their intended export/replay contracts are dependencies to follow, not completed capabilities to assume.
- Opportunity recordings may cover bounded windows around accepted arms. TauTick also needs session-wide history for browsing and VWAP, including intervals never armed in ATAS. An opportunity export alone must not be assumed to supply that history.

Proposed responsibility split: TradingLab remains the authoritative detector and recording-contract home; TauTick supplies session browsing, interval selection, saved experiments, and interactive presentation. Investigate reuse of the offline replay work as it becomes available. Neither moving the detector nor duplicating it is decided by this brief.

The earlier [LEAN study](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/lean/architecture.md) and [NautilusTrader study](https://github.com/mitjasiska/trading-platform-research/blob/98e5fa60d5c3e75c625420295596b7b6f6c9071c/research/nautilus-trader/architecture.md) motivate explicit timing, controlled state advancement, and measurement of retained memory. They do not establish a seekable TauTick engine or settle its storage/UI choices.

## Data requirements and acquisition research

The detector integration needs executed trades with price, contract volume and aggressor classification, plus both best bid and ask and their changes between trades. Preserve original ordering, source timestamps, timestamp meaning, instrument/tick metadata, and validity/reset information. Keep unknown aggression explicit. M1 candles alone cannot provide this evidence.

Databento documents MBP-1 as trades plus every top-of-book update, with bid/ask price and size, event/receive timestamps, sequence, action and quality fields. Its trade side can be unspecified. [MBP-1 schema](https://databento.com/docs/schemas/mbp-1).

TBBO includes trades with the BBO immediately before each trade's effect, omitting the intervening quote-only path. It is insufficient for the current Entry Trigger requirement to retain quote excursions between executions. [TBBO schema](https://databento.com/docs/schemas/tbbo), [schema distinctions](https://databento.com/docs/faqs/difference-between-mbp-and-tbbo).

Databento's CME notes identify `ts_event` as matching-engine receipt and `ts_recv` as capture-server receipt; neither is the trader's ATAS callback-processing time. Implied trades can have no aggressor side. These details must survive the adapter or receive an explicit policy. [CME dataset conventions](https://databento.com/docs/datasets/glbx-mdp3).

Two acquisition investigations remain open:

- **Databento:** inspect a small MBP-1 sample for the selected dated contract, coverage, trade/BBO association, ordering and unknown-side cases. Confirm initial quote availability and the mapping into core inputs. Select data only after estimating the chosen request; no provider purchase or request is part of this plan.
- **ATAS export:** determine whether an available exporter actually records the full session, individual trades, both-sided quote changes, source transitions and required clocks. Establish whether it records actual callbacks, a native recorded feed, or reconstructed replay data. Verify what DEV-149 contributes and what a session exporter would still need. Export fidelity has not been established.

Use a concrete dated NQ contract for the first full experiment dataset; the chart viewer starts separately with ES bars. Any continuous-symbol lookup must resolve to explicit contracts before an experiment is saved. Record session timezone, session start/end, VWAP reset convention, and calendar identity; do not equate a trading session with a local calendar day.

Prefer deriving the detailed chart and VWAP from the same validated trades used in replay. If separate candles aid browsing, label their provenance and verify consistency. Define trade eligibility/corrections, minute boundaries, empty bars and partial bars before treating their values as interchangeable.

## Replay contracts to validate

### Presentation time and detector time

The current [core input types](https://github.com/mitjasiska/TradingLab/blob/1209cd863bbd0eb9459c2754512ec329961ae911/apps/atas-indicators/src/EntryTrigger.Core/Contracts.cs) and [input methods](https://github.com/mitjasiska/TradingLab/blob/1209cd863bbd0eb9459c2754512ec329961ae911/apps/atas-indicators/src/EntryTrigger.Core/TriggerDetector.Inspection.cs) take market timestamps and monotonic receipt/processing values explicitly. The detector uses both. Its ATAS adapter deliberately resets evidence on replay pause/speed/source changes; that adapter behavior cannot be imported as TauTick's viewer behavior.

Proposed contract:

- The replay evaluator consumes an ordered input timeline with deterministic virtual clocks. The viewer controls only how quickly evaluation is presented. Pausing the viewer freezes progress in that timeline; it does not produce feed silence in the experiment.
- Faithful replay of a supported ATAS recording retains recorded controls, pulses, receipt/processing delays and source changes. A pause/reset recorded in that source still has its original meaning, independently of the viewer's pause button.
- A provider market-data file lacks ATAS processing history. It needs a declared simulation clock policy, not invented observed timings. A candidate policy is capture-receipt-based virtual arrival with zero modeled processing lag; its compatibility with detector freshness and pulse behavior must be tested before adoption. Results would be conditional on that model, not proof of ATAS parity.
- Recorded gaps remain gaps. Timers advance according to the selected policy even without trades. Timer passage can expire evidence, but must not manufacture confirmation without the fresh trade required by the detector.
- Preserve source order and original timestamps. Equal timestamps need an explicit secondary order, including manual actions and timer ties. Do not sort away source anomalies or infer missing records from a filtered instrument's sequence gaps without feed-specific evidence.

Keep original timestamp precision in stored input. Any conversion into the core's `DateTime` and millisecond fields needs documented rounding and tie behavior; it must not silently become the source's ordering authority.

### The exact arming boundary

Save manual actions at their experiment time and exact position among ordered inputs, including direction and activation mode/price when applicable. A timestamp alone is insufficient when multiple events share it. A paused manual action can be recorded immediately after the current event boundary; actions between events must retain their virtual time too.

Reruns replay the same instruction, not a newly timed click. The instruction's acceptance, candidate adoption and eventual signal can change with configuration. Do not force alternatives to adopt the reference candidate or erase rejected arms. Editing an action creates a new experiment revision, distinguishable from a settings-only comparison.

Initial scope must preserve the selected activation policy. Whether the first TauTick UI exposes optional price activation as well as ARM NOW remains a UI-scope decision; an imported gated attempt cannot silently become ARM NOW.

### Warmup and seeking

Three ranges are distinct: the selected research interval, the visible playback starting point, and the preceding data required for state preparation. Save the first two; calculate the third from the configuration and initialization policy.

Session VWAP requires all eligible session trades before the cursor, or an equivalent validated accumulator. Entry Trigger requires rolling trade and quote history plus candidate qualification, retirement, persistence, and source state. Its finite volume windows do not by themselves prove that an arbitrary short pre-roll recreates every candidate's history.

The reference implementation to investigate first is sequential processing from a declared initialization/reset boundary with sufficient session context. Warmup processes observation normally while replaying only actions actually present in the experiment history. It does not invent an ARM or renew candidate lifetimes at the visible start. Missing required history produces an explicit incomplete result.

Faster seeking is an optimization against that reference. Options include indexing input, caching session aggregates, and validated detector checkpoints. A UI/diagnostic snapshot is not a full detector checkpoint. Restoring just the last signal or visible windows is insufficient.

Checkpoint eligibility would need data identity, detector version, effective settings, clock/normalization policy, session convention, action-history prefix and the exact input boundary. It must include hidden state such as retired-candidate cutoffs and pending persistence. Changing settings can invalidate preparation before ARM; rerun from a valid earlier boundary instead of mutating the warmed reference instance. Full checkpoint serialization has not been established in the inspected core.

Perform preparation off the UI thread, report readiness and allow cancellation. A superseded seek/configuration request must not replace the state of the newer request. Measure cold opening, warm seeking, repeated reruns, peak memory and UI responsiveness on a real session before choosing cache budgets or latency promises.

### Step, inspect, compare

Define stepping over the ordered replay inputs, preserving trade, quote, timer and control semantics. Any feed-defined atomic grouping must remain intact; grouping and ties are part of the input adapter contract. Rendering may be sampled during playback, but detector inputs cannot be skipped to keep the chart smooth.

At the cursor, show only calculation state available at that boundary, including the partially formed candle. Browsing a completed session can reveal future context to the researcher; that is a retrospective investigation. Future candles or completed-session aggregates must never leak into the detector state being replayed.

Expose the existing detector evidence: candidate path and original age, fresh response progress, required versus observed conditions, waiting/active deadlines and the terminal reason. Distinguish missing, not yet evaluated and failed conditions. Keep the final attempt explanation available after playback continues.

Compare signals by event/time, decision reason and bid/ask-derived signal price. Measure delay from declared ARM/activation/candidate references and movement using specified price anchors. If subsequent-path metrics are added, paired variants need a common evaluation cutoff as well as clearly labeled per-entry horizons. End-of-interval while still watching is an unfinished observation, not proof that the detector would never signal. Missing coverage is not a zero or a successful no-signal result.

## What a saved experiment must retain

The [glossary](../GLOSSARY.md) distinguishes an interval, an experiment, a configuration, a run and an armed attempt. Proposed persistence requirements are:

- Interval/session identity, description, exact boundaries and playback start.
- Immutable input references with content hashes, contract identity, coverage, schema and normalization versions. A local path is a locator, not sufficient data identity.
- Session/calendar/VWAP conventions and the declared clock, ordering and initialization policies.
- The ordered manual action timeline, with exact boundaries and action revisions.
- Named configurations with complete effective settings and detector/core identity.
- Associated run outcomes, meaningful decision traces, coverage/readiness status and reproducibility metadata.

Persist changes without destroying the prior configuration or run. Reopening resolves and verifies inputs; unavailable or changed data needs a clear explanation rather than substitution. Storage format, relocation of local datasets and distribution of the shared core remain open. Viewing stored results can remain possible even when data needed for a fresh rerun is unavailable.

## Proposed development sequence and acceptance

These are planned stages after the chart viewer milestone, not tasks started by this document:

1. **Input and reuse investigation.** Obtain a suitable recording for the full experiment workflow, describe its actual event/clock semantics, validate a portable core build, and inspect DEV-149/DEV-150's available contracts. Produce reproducible fixture instructions and compatibility findings. Existing ES samples and synthetic fixtures can cover some invariants; they cannot establish NQ provider/export fidelity or sufficient session coverage.
2. **Replay reference.** Feed ordered inputs through the actual core with virtual clocks and fixed manual actions. Record original behavior and first-divergence diagnostics. Verify warmup and sequential reconstruction before optimizing seek.
3. **First complete interactive experiment.** Extend the chart viewer with interval selection/saving, state preparation, play/slow/step, arming, inspection, rerunning one changed configuration, and saving/reopening the experiment. This completes the later experiment workflow; a headless runner alone does not complete it.
4. **Seek and usability refinement.** Measure friction on real recordings, then introduce indexes/caches/checkpoints with reference-equivalence checks. Refine the Avalonia UI and choose storage technology using the measured workflow constraints.

Acceptance scenarios for the eventual implementation:

- Repeating the same data/settings/actions produces the same ordered logical outcomes at 1×, slow playback, stepping and unrestricted evaluation. Viewer pauses do not expire candidates.
- Jumping to a cursor yields the same VWAP, candidate state, and subsequent results as sequential replay from the declared start. Include a candidate qualified before ARM and one retired before the selected interval.
- A quote excursion between identical-looking trade endpoints affects the detector as specified. An invalid/unknown input is not silently converted to valid evidence.
- ARM and trade events with identical timestamps retain their exact order. Confirmation cannot use pre-activation response volume or complete merely because playback waited.
- A configuration needing earlier history either obtains it automatically or reports insufficient coverage; it cannot reuse incompatible warmed state.
- Reopening preserves the original actions, settings and outcomes; altered or missing files are detected. Changing the data/core produces a new identifiable experiment basis.
- Session boundaries, overnight dates and daylight-saving changes obey the named session definition. The cursor sees partial candles and no future VWAP contribution.
- No-signal, rejected-arm, expired, incomplete and unsupported runs remain inspectable alongside signals. Repeated runs of the same interval are not counted as independent market observations.

## Decisions still open

For this later workflow: data provider/export path and suitable NQ recording; session/VWAP convention; shared-core distribution; reuse of the developing offline runner; provider clock/timer mapping; first UI activation modes; storage format; and measured seek/memory targets. Windows/Linux and Avalonia are the current UI direction, with the immediate work defined in the [chart viewer plan](charting-mvp.md). Provider selection, live connectivity and detector integration remain later investigations.
