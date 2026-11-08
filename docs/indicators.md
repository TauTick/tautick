# Chart indicators

Open a CSV, then use **Indicators…**, the chart context menu or **Ctrl+I** while
focus is in the chart. The modeless window belongs to that chart. Search for VWAP,
choose **Add to chart** twice, and select each instance in the middle list. Set its
price convention, loaded-bar UTC anchor, color and width, then **Apply**. Settings
are drafts until Apply; selecting another instance or closing discards drafts.
Add/remove, visibility and order are immediate. The chart legend offers the same
settings, show/hide, order and removal actions. Hover a candle to inspect each
visible ready VWAP. Reopen the manager to see the remaining instances.

## Meaning and numerical limits

VWAP is labeled a **bar-based approximation**, never exact trade/session VWAP.
HLC3 is `(high + low + close) / 3`; Close is the explicit alternative. The result
at each available completed bar is cumulative `price * volume / volume`, beginning
with the anchor bar. Default anchor is dataset start; explicit anchors select an
existing loaded bar's UTC start. Before the anchor or with zero cumulative volume,
there is no value. After positive volume, known-zero-volume bars retain the value.
Missing intervals contribute nothing, remain missing and break the drawn line.
The input represents completed historical bars; no live/incomplete-bar semantics
are inferred. Panning/zooming never changes calculation inputs or anchor.

Arithmetic uses checked .NET decimal operations, including the HLC3 sum, product
and accumulated totals; division follows decimal's representable precision. Any
overflow fails the entire calculation with a numeric-limit message and no partial
series. There is no saturation or substitution of zero. Extreme but valid input
may therefore be unavailable rather than silently rounded to a fabricated value.

Default dataset-start anchors follow accepted replacement data. Explicit anchors
are bound to a SHA-256 of instrument/source identity and the full immutable bars
(including timestamps, intervals, prices and volumes), computed off the UI thread.
A different input produces an anchor error. Select a different loaded bar, or
check **Bind selected anchor to this dataset on Apply**, then Apply to deliberately
rebind it. Appearance-only edits preserve an incompatible binding and its error.
A matching restored input restores the anchor. Failed imports leave the
previous accepted studies intact. Workspace input verification still checks the
original file bytes before parsing. The public reference data covers part of a
session; neither dataset start nor midnight implies a session boundary.

## Extension contracts and ownership

`TauTick.Studies` depends only on MarketData. `IStudyDefinition` supplies stable type
identity, defaults, version validation, input requirements and cancellable calculation.
Its result is a study-specific immutable object, not a required numeric-series shape.
`StudyDescriptor` supplies stable instance identity, versioned calculation settings
and visibility; the ordered saved list also includes appearance and anchor binding.

`ChartStudies` lives with `ChartDocument` and participates in the existing workspace
settings mechanism. It runs calculations on workers and publishes on the UI thread.
Each instance has a generation and cancellation source; replaced, removed, restored
or closed work cannot publish into a newer generation. Status distinguishes waiting,
calculating, ready, unsupported input/settings, errors and disposal. Appearance-only
changes recreate presentation without calculation. Unknown types and unsupported
settings remain in saved state and explain why they cannot run. Invalid whole state
is retained by the workspace participant failure mechanism.

Register calculation and renderer factories with `StudyRegistration` through
`ChartStudies.Register` during application composition. Non-VWAP
registered studies have a generic JSON editor; specialized editors may be added
without changing the instance list or drawing interface. The default application
catalog contains only VWAP; there is no dynamic DLL or scripting loader.

`IStudyRenderer` owns UI-thread presentation and optional hit testing/press handling.
`StudyDrawingContext` supplies time/price transforms, viewport, visible index bounds,
DPI scale, font/theme brushes, ordered layer index, plot/axis/margin regions and a
reserved bottom region. The host allocates requested heights (capped at 200 DIP per
study and one third of chart height collectively) separately from docking panes.
Renderers draw after candles and axes, before inspection. Later instances are drawn
above earlier ones and receive press input first; returning false preserves chart
pan/double-click gestures. Wheel navigation belongs to the candle plot.

Render uses only prepared results. `DrawingContext` supports filled geometry, text,
images and arbitrary paths. For direct Skia work, submit an `ICustomDrawOperation`
through `DrawingContext.Custom`, acquire `ISkiaSharpApiLeaseFeature` from its
`ImmediateDrawingContext`, and release the lease in the render callback. Such an
operation must own immutable render-thread inputs and dispose its own graphics
resources when Avalonia disposes it. Never capture a mutable document, UI controls
or a UI-owned disposable renderer inside the operation. Renderer disposal is for
UI-side resources; moving surfaces does not dispose document-owned renderers.

The shared synthetic level study in `tests/StudyFixtures/SyntheticLevelStudy.cs`
demonstrates a distinct price-level result, filled shapes, text, a reserved region,
hit testing and input, using the
same registration/host interface. Its labels explicitly say synthetic, and it is
absent from the production catalog. It is not TPO or a volume profile.

See [reference evidence and deliberate differences](indicators-reference.md) and
[validation](indicators-validation.md). Current ATAS X interaction fidelity and full
Windows/Linux acceptance require the outstanding checks recorded there.

## Native development study

The opt-in development host runs the real workspace and production CSV parser;
its synthetic level study is linked only into test/development projects. Build and
launch it with the same native environment as the normal application:

```sh
dotnet restore tests/TauTick.Desktop.Native --locked-mode
dotnet build tests/TauTick.Desktop.Native --no-restore
dotnet run --project tests/TauTick.Desktop.Native --no-build --no-restore -- --study-demo
```

Enter the full contract year and open an appropriate CSV. The reserved teal region
and orange level marker are labeled synthetic; VWAP remains calculated from the
accepted bars. Hover/click the reserved region: it shows a synthetic level tooltip
and writes `SYNTHETIC_REGION_PRESSED` to stdout. Chart wheel/drag still operate in
the candle plot. Replacement and final close write renderer disposal markers. This
host does not save a workspace and does not add synthetic studies to the default
application catalog.
