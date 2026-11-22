# Shared native UI

TauTick.UI owns reusable Avalonia presentation controls, AXAML compositions, templates and design resources. It has no reference to market data, adapters, calculations, persistence, application services or gallery fixtures. Read [local agent guidance](AGENTS.md) and the [gallery runbook](../../samples/TauTick.UI.Gallery/README.md) before UI work.

## Presentation API and ownership

`ChartToolbar` is the exact implementation used by `Desktop/Workspace/ChartView` and the gallery's toolbar scene. Its properties are `ContractYearText` (two way), `ShowContractYear`, `CanEditYear`, `CanOpen`, `CanFit` and `AdditionalContent`. Set `OpenCommand`, `FitCommand`, `IndicatorsCommand`, or subscribe to `OpenRequested`, `FitRequested`, `IndicatorsRequested`. An owner normally uses one action mechanism; setting both invokes both. Events are wired to freshly created buttons by `InitializeComponentState`, including after markup reload. Do not cache child controls across reloads.

The desktop owner retains loading, error/cancellation handling, active chart targeting, data and indicator lifetime. The library neither opens provider files nor reconstructs a chart. `AdditionalContent` is a presentation slot for existing owner controls. Stable IDs (`ContractYear`, `OpenCsv`, `FitData`, `OpenIndicators`) remain scoped to each toolbar/window.

`SettingsPanel` exposes `DisplayNameText`, nullable `StrokeWidth`, `PriceDisplayIndex`, `ErrorText`, `CanApply` and Apply/Cancel commands. The caller owns drafts, validation and persistence; the gallery's `GalleryState` supplies deterministic sample behavior. The selector demonstrates two display choices; this initial panel is not the production indicator editor. `PresentationCommand` is a synchronous action adapter. An owner starting async work must retain its own cancellation/error lifetime, as the desktop already does.

`WindowHeader` supplies the first row shared by Desktop and the native gallery:
Menu, `ContentTabBar`, a reserved drag area and caption buttons. `ConfigureWindow`
uses Avalonia 12.1.5 `WindowDecorations.BorderOnly`, extended client area and
`WindowDecorationProperties.ElementRole`. `WindowDragRegion` uses native title-bar
hit testing on Windows. On Linux it distinguishes a double-click from a drag before
calling native `BeginMoveDrag`: Avalonia 12.1.5 X11 consumes TitleBar presses before
managed click recognition. Interactive children use their own roles. Caption buttons retain native
hit testing, accessible names, tooltips and keyboard invocation. Application menu
items raise AddChart/Exit requests. The host retains a normal native Window and its
close policy. The tab bar instance survives header markup reload. `SetRestoreBounds`
accepts the host's restored normal geometry; subsequent normal moves/resizes update
the retained bounds after native state and layout events settle. Saved geometry
remains pending through maximized startup until the window returns to normal,
including restore through native keyboard/system-menu actions. Geometry is applied
after the state transition and decoration insets settle; this preserves normal
bounds on Windows and avoids retained maximized dimensions or cumulative inset
loss on Linux at scale 2.

`ContentTabBar` and its AXAML `ContentTabHeader` present ordered content tabs,
selection, +, context actions and insertion feedback. `SetTabs` accepts presentation-only
`ContentTabItem` values and retains controls by tab ID, including during rename commits
that occur inside a focus/press event. Add/Selection/Close/Detach/Rename and drag events are requests; the owner
commits transfers. `InsertionAt` uses screen coordinates, including headers and
unused strip space; the pinned + appends. Overflow scrolls inside the strip while
Menu, + and caption buttons stay available. Tab context menus support Shift+F10,
deferring to Avalonia when the platform registers that gesture and supplying a fallback otherwise;
Rename uses an inline editor with Enter/blur to commit and Escape to cancel.
Escape/capture loss cancels a tab drag. Pointer capture starts after a
movement threshold. No document, window ownership or persistence lives here. The
gallery toolbar scene exercises this exact strip with synthetic chart/empty tabs.

Decoration API references: [Avalonia 12 migration](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes),
[12.1.5 attached-role source](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Controls/Chrome/WindowDecorationProperties.cs),
and [12.1.5 Window source](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Controls/Window.cs).
The removed `ExtendClientAreaChromeHints` and obsolete `SystemDecorations` are not used.
On Windows, the pinned `BorderOnly` implementation drops `WS_SYSMENU`.
`ConfigureWindow` retains that flag through the public
[12.1.5 window-style callback](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Controls/Platform/Win32Properties.cs)
so Alt+Space keeps its native menu without adding another title bar. Avalonia ignores
the callback on other platforms.

## Resources, fonts and component authoring

Load framework `FluentTheme`, `DockFluentTheme`, then `new TauTick.UI.Theme()`. Both gallery and desktop use that order and `.WithInterFont()` in the native AppBuilder. Consumers supply `Avalonia.Fonts.Inter`; the `TauFont` resource addresses the same bundled font. No external asset download is needed.

`Theme.axaml` centralizes initial colors (`TauAccent`, `TauInk`, `TauSurface`, `TauBorder`, hover/pressed/error brushes), body/heading sizes, gap, panel/control padding, minimum button height, border width and corner radius. `TauButton` owns the button template and real hover, pressed, disabled and keyboard-focus feedback. `Button.tau` selects it; `UserControl.tau` applies shared typography. Toggle selection and error examples use shared values while native input, editing, focus navigation, selectors, popups and accessibility retain Avalonia's foundations. Unclassified existing product controls retain their framework themes.

Use AXAML for iterated structure. Prefer dynamic resources for authored design values and styled properties for values a host binds. Preserve input/focus behavior and automation IDs when changing templates; do not replace an input control with a drawing that only looks editable. Add a component to a gallery scene before expanding production adoption.

The explicit `Theme : Styles` class gives the resource source a stable reload identity. Gallery `GalleryApp.axaml` is also essential: HotAvalonia needs an AXAML Application root to reapply global resources. A C#-only app root can leave resources stale even when the tool reports recompilation. Production loads the same resources normally and does not require live reload.

## Design decisions and checks

The initial pale surface, blue accent, Inter typography and small corner treatment are provisional. User feedback becomes durable by editing these shared values/templates, checking the affected gallery scenes at compact/wide sizes, exercising real input, and recording accepted decisions here when they change. There is no runtime source write-back or separate copied gallery design.

From the repository root, after the [initial gallery setup](../../samples/TauTick.UI.Gallery/README.md#setup-and-launch):

```sh
dotnet test tests/TauTick.UI.Tests --no-build --no-restore
dotnet test tests/TauTick.Desktop.Tests --no-build --no-restore
```

Build affected projects first after C# changes. Tests cover meaningful bindings, availability, commands, settings drafts and deterministic scene setup/reset; desktop tests cover the production import/chart/indicator seam. AXAML/template/resource changes also require the native edit → reload → inspect → exercise → refine loop. Source inspection and headless tests cannot establish native behavior. See the [validation record](../../samples/TauTick.UI.Gallery/VALIDATION.md) for actual checks and platform limits.

HotAvalonia and the matching runtime XAML loader are pinned, private development dependencies. HotAvalonia's build targets disable weaving and remove their runtime references for Release (`HotAvaloniaIncludeXamlLoader=false`). The production Release output must contain neither reload binaries nor gallery/fixture dependencies. Do not add developer inspectors, endpoints or license requirements to production composition.
