# TAU-12 validation and provisional design

Recorded 2026-11-09 on **aquila**, Ubuntu 24.04.5 x64, Linux 6.8.0-147-generic. SDK **10.0.114**, net10.0, Avalonia/Dock **12.1.5**, HotAvalonia **3.1.5**, runtime XAML loader **12.1.5**, bundled Inter. Native X11/Skia under an owned Openbox/Xvfb screen 1600×1100×24 with GLX disabled, scale **1**, separate D-Bus and temporary XDG profiles. This is software/virtual-display evidence, not physical-monitor or GPU performance evidence.

## Default reload choice and compatibility gate

The initial shared toolbar/settings slice was built and operated before expanding the gallery or integrating the desktop. Shared control text, resource padding/color, native text input and malformed-markup recovery were observed in PID 3829308. HotAvalonia 3.1.5 is MIT licensed and needs no entitlement or IDE. It was chosen because that actual cross-project native probe passed on this pinned stack. Official Avalonia hot reload and DevTools MCP were not installed or licensed.

Two setup failures matter for reproduction: a URI-only style include initially failed to find precompiled markup; using an explicit `Theme : Styles` class gave the resource a stable identity. A C#-only Application root then recompiled resources without updating live controls. Moving the gallery Application styles to `GalleryApp.axaml` allowed HotAvalonia's global resource reload to reapply the application and dependent scenes. Merely installing the package or starting dotnet watch would not have proved this.

## Timing and successive warm edits

Cold launch was measured separately after the initial build: **5.650 s** from starting the built gallery to the mapped native window, with `GALLERY_READY` emitted and controls subsequently inspected. This is one cold process launch, not a filesystem-cache flush or presented-frame measurement. Restore/build time is excluded.

The final uninterrupted measured series used gallery PID **3832005**, private display **:107**, initially the toolbar scene (1040×820 DIP), then settings. From each source write, the driver waited for the tool's reload activity, expected accessible text/bounds or resource pixels, and a native capture update. Captures were opened for inspection. These are polling-based development timings, not instrumented render-frame timestamps.

| Edit | Shared source change | Save to observed update (s) |
| --- | --- | ---: |
| 1 | shared text: Open label | 0.358 |
| 2 | shared text: Fit label | 0.405 |
| 3 | add existing TextBlock | 0.461 |
| 4 | remove existing TextBlock | 0.670 |
| 5 | shared toolbar layout margin | 0.426 |
| 6 | shared color resource | 0.472 |
| 7 | shared padding resource | 0.405 |
| 8 | control template corner treatment | 0.439 |
| 9 | shared settings heading | 0.459 |
| 10 | shared spacing resource | 0.606 |

**n=10; median 0.449 s; worst 0.670 s.** All ten valid saves appeared in the same PID; zero reload failures or process restarts in this measured series. No restore, MSBuild invocation, normal application launcher or tests ran during it. HotAvalonia performed runtime XAML compilation internally. Scene selection remained available; resource reloads recreated dependent views. This small sample establishes the observed loop, not a general performance guarantee.

The disposable driver first needed a bounds-condition correction. An earlier successful series overlapped a separate incremental Release build and is excluded from the reported timing sample. The final series above was repeated without that build. Temporary text, margins, colors and corner changes were then restored to authored source; these experiments are not saved design choices.

After the series, an invalid `Broken` property in shared `SettingsPanel.axaml` produced `Failed to reload`, the source URI and property/type plus line/position. Correcting it displayed a changed recovery heading in the same PID. A separate error check verified the visible `GalleryStatus` diagnostic and recovery. The process stayed alive; no stale success was accepted. Native reset and a later restart check also verified that resetting a scene does not erase the last reload diagnostic.

## Native interactions and reuse

Passed on Linux through PID-scoped AT-SPI targeting, current screen bounds and actual XTEST input, with opened native captures:

- Scene selection/reset and settings startup; compact 560 DIP and wide 1040 DIP sizes, long selected display text, ordinary content tabs.
- Real keyboard Tab focus, hover tooltip and held button press (pressed resource pixels checked), disabled example, checkbox/toggle, text/numeric fields, native selector popup and menu.
- Settings empty-name validation and disabled Apply, editing, selecting the long price-display choice, Apply, changing a draft and Cancel restoring the applied sample, then Reset restoring defaults.
- The existing custom chart with clearly identified synthetic immutable bars, a gap and zero volume; native wheel zoom, inspection crosshair and Fit.
- One saved `TauSurface` change to `#ECF7EF` appeared in the running gallery and in a newly built **Release** desktop toolbar loaded from the exact shared implementation/resources. Same OS/display/scale and idle button state; the resource pixels agreed. Desktop contract-year input enabled Open, the existing indicator manager opened and closed, and desktop normal shutdown returned **0**. The experiment was restored to source afterward.
- Normal gallery close returned **0**. The direct process and its in-process watcher exited; the session wrappers stopped the owned WM/display/bus. No owned gallery/desktop windows were left behind.
- Explicit restart path: a C# gallery-shell change (including a new field and revised reset status) was followed by the incremental build and direct `--scene settings` launch in a new PID. The requested scene and new C# status appeared, the committed inspection helper found its IDs/captured pixels, and error/reset/recovery plus normal close were rechecked. No direct-launch C# hot reload is claimed.

The durable [inspection helper](tools/inspect-linux.py) was executed against the native gallery using its own display/bus/Xauthority and generated a capture outside Git. Disposable drivers, JSON timing data, captures and logs are under `/tmp/tau12-native` on aquila. They are ephemeral evidence, not repository fixtures or test dependencies.

## Automated and packaging checks

Passed: locked solution restore; Debug solution build (zero warnings/errors); final Debug solution tests **215/215** (MarketData 25, adapter 99, Charting 42, Desktop/indicators 43, new UI/gallery 6). UI/gallery tests cover two-way bindings, availability, action dispatch, draft validation/Apply/Cancel, deterministic startup/reset and invalid startup options. Existing desktop tests retain import/error/cancellation, active-chart and indicator ownership checks.

Release desktop dependency-graph build passed with zero warnings/errors. Its files and `.deps.json` contain no HotAvalonia, runtime XAML loader, Fody, gallery or synthetic-data runtime dependency. The Release desktop also launched and was exercised natively without a tooling license/inspector. Central versions and affected lock files were updated deliberately; ordinary restore passes `--locked-mode`.

The local guides' relative links and root discovery pointers were checked (64 local links across the affected entry points/guides). The Linux startup and incremental restart commands ran from the documented repository root; the helper passed syntax and native execution checks. Windows PowerShell command spelling was inspected but not executed here.

## Unverified and limited coverage

| Requirement/environment | Result |
| --- | --- |
| Linux X11 isolated launch, input, shared reload, invalid recovery, shutdown | Passed |
| Windows native launch/input, shared reload, invalid recovery, shutdown | **Not run: no Windows session available** |
| Windows PowerShell commands / UI Automation route | Documented; **not run** |
| Physical DPI/scaling transitions, mixed monitors | **Not run: virtual display only** |
| Linux full desktop/Wayland/portal dialogs; GPU/presented-frame performance | Not run |
| Optional .NET C# hot reload via dotnet watch | Documented separately; not measured or verified |
| Licensed official reload / DevTools MCP | Not used; no entitlement assumed |

Cross-platform native acceptance remains unverified. No pixel-identical Windows/Linux rendering or broader performance guarantee is claimed.

The pale surface, blue accent, Inter text, spacing and small corners are provisional source-backed choices. The library guide owns [component/resource conventions](../../src/TauTick.UI/README.md#resources-fonts-and-component-authoring); the gallery has no runtime visual editor or automatic write-back. No final application-wide redesign is established by this task.


## TAU-11 shared content strip — 2026-11-09

The toolbar scene now uses production `ContentTabBar`/`ContentTabHeader` with
synthetic chart and empty tabs. On Ubuntu 24.04 x64, X11/Skia software rendering,
private Xvfb 2400×1800, Openbox, separate D-Bus/profile and scale 1: native selection,
+, drag reorder and keyboard activation passed with inspected captures. An AXAML
minimum-height edit (38 → 44 → 38) reloaded in the same PID; + and Space activation
still worked after reload. Normal gallery close returned process code 0. Source
was restored, and captures/drivers remain outside Git at
`/tmp/tautick-tau11-gallery/`. Windows, physical DPI/monitors and Wayland were not
verified. The [workspace validation](../../docs/workspace-validation.md) records
cross-window application checks separately.
