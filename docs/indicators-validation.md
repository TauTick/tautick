# Indicator validation

Checked on 2026-11-08 with .NET SDK 10.0.114, pinned Avalonia 12.1.5, Ubuntu 24.04
Linux x64. Dependencies were restored in locked mode after adding the local Studies
project and regenerating affected lock files.

## Automated

`dotnet test TauTick.slnx --no-restore`: 209 passed (99 adapter, 25 MarketData,
42 chart, 43 desktop). Eleven `IndicatorTests` cases cover independently expected
HLC3/Close values, inclusive anchors, zero volume, negative/flat prices, gaps,
numeric overflow, cancellation, the manager's two-instance flow, appearance-only
result reuse, hide/reorder/remove/reopen, failed/replaced data, anchor binding,
docking and workspace restoration with a real temporary CSV, unsupported input and
unknown settings retention, and rejection of a late non-cooperative calculation.

The custom synthetic level fixture uses the same public study host and drawing
interface, its own model, filled geometry/text, a reserved region, hit testing and
press handling. The shared fixture runs in headless tests and the opt-in native
development host described in [the extension guide](indicators.md#native-development-study).

The focused indicator suite passed all 11 cases. It also verifies that
indented/restored and reordered JSON settings preserve prepared results for
appearance-only changes, while a real price-convention change recalculates; missing
settings in both known and unknown descriptors preserve the raw workspace payload
and allow an actual workspace save/load. Invalid descriptors are rejected before
replacing an existing instance collection. Manager-level coverage replaces the
dataset with matching timestamps but different values, applies only a color edit,
and verifies that the incompatible anchor remains in error with its original
binding. Checking the explicit rebind control then permits calculation on the new
dataset and clears the checkbox after Apply.

## Native Linux

Actual desktop application, isolated Xvfb 2400×1800, Openbox, private D-Bus and XDG
profile, X11 software rendering. AT-SPI located ordinary controls; XTEST drove
controls/chart input and GTK file selection. Synthetic CSVs/captures/scripts live
outside Git under `/tmp/tau9-native`.

At application scale 1, opened the synthetic CSV, searched VWAP, added two instances,
changed one to Close with a red line, applied, reordered, hid/showed, closed/reopened
using Ctrl+I, removed an instance, saved, and closed normally (exit 0). Inspected
native screenshots show the manager and the distinct Close/HLC3 lines without
bridging the missing interval. Wheel and drag actions were also exercised.
At application scale 2, repeated the flow, opened the manager from a detached
chart, redocked it and opened the context-menu manager. Inspected native crosshair
values were Close = 5 and HLC3 = 3 for the independently calculated synthetic bar.
Restart restored the remaining study and verified input, with a ready result.
Resized the native workspace to 1800×1200 pixels at scale 2, inspected the chart
and controls, then closed normally (exit 0).

Initial automation attempts did not focus the GTK picker correctly; explicitly
focusing its native window resolved this automation issue.

The opt-in `TauTick.Desktop.Native --study-demo` also ran at scales 1 and 2 with
the real `MainWindow`, workspace and CSV parser. Inspected captures show the
synthetic orange level geometry, teal reserved region, synthetic caption and hover
tooltip alongside calculated VWAP. Native clicks reached the study's input handler;
wheel/drag and Fit continued to work in the candle plot. Renderer-disposal markers
were observed on accepted replacement and final close; both runs exited normally
with code 0. This development host supplements the production-application checks
above; it does not save a workspace.

In both development-host runs, a Close VWAP anchored at 09:33 entered an error after
accepting a different CSV with matching timestamps. A color-only Apply retained
that error. Explicitly checking the rebind control and applying then produced
Ready, with an inspected crosshair value of 6. Captures and disposable automation
are under `/tmp/tau9-r3-native`. Early driver attempts failed during GTK file
selection; the passing runs used native window focus, AT-SPI Open and load checks.

## Outstanding acceptance

Current ATAS X installed-build interaction comparison, especially Apply semantics,
remains unverified; see [reference evidence](indicators-reference.md). Windows
native manager/focus/detach/DPI validation and physical mixed-DPI/multiple monitors
remain unverified. The full native in-flight cancellation/replacement/close matrix
has not been exercised; automated lifecycle tests cover stale publication and
disposal, while the native runs cover completed replacement and normal close.
No latency, presented-frame performance or complete cross-platform acceptance is
claimed. Headless docking/state tests do not replace these native checks.
