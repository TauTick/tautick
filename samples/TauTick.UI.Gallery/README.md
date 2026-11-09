# Native UI gallery

The gallery is a standalone Avalonia desktop executable for developing the [shared UI library](../../src/TauTick.UI/README.md). It uses deterministic samples without downloaded market data, credentials, a saved workspace or a live service. Read [local agent guidance](AGENTS.md), the [library guidance](../../src/TauTick.UI/AGENTS.md) and the [native Linux procedure](../../docs/development.md#native-visual-checks-on-linux).

## Setup and launch

Use the repository's stable .NET 10 SDK policy (`global.json`, minimum 10.0.114), Avalonia/Dock 12.1.5 and committed package locks. Linux needs the X11/font libraries in the developer guide. Native agent inspection additionally needs its Xvfb, D-Bus, Openbox, XTEST, AT-SPI, Python GI and Pillow helpers. Windows needs a graphical desktop and the .NET SDK; UI Automation can inspect ordinary controls. No commercial IDE, license key or connected inspector is required.

From the repository root, one-time locked restore and initial build, in either Linux Bash or Windows PowerShell:

```sh
dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --no-restore
```

Daily startup after that build, Linux Bash:

```bash
dotnet samples/TauTick.UI.Gallery/bin/Debug/net10.0/TauTick.UI.Gallery.dll --scene toolbar
```

Windows PowerShell, from the same repository root:

```powershell
dotnet .\samples\TauTick.UI.Gallery\bin\Debug\net10.0\TauTick.UI.Gallery.dll --scene toolbar
```

Use `--scene controls|toolbar|settings`, optionally `--compact`. Unknown/missing option values exit with usage and code 2. Closing the native window stops this direct process and its in-process watcher. The default is `controls`; scene changes and Reset create a fresh deterministic sample. Ctrl+1/2/3 selects the three scenes, Ctrl+R resets. The header also offers scene selection, Reset, Compact (560 DIP) and Wide (1040 DIP). Resize normally for other sizes; vertical scrolling is available. There are no runtime design overrides or saved gallery settings.

An ordinary interactive launch uses the user's desktop and identifies its PID/display/scale in the gallery header. An isolated agent launch uses a private virtual display/bus/profile as described below; it does not appear on the user's physical desktop. The gallery never reads/writes the production workspace store.

## Warm editing and recovery

The default mechanism is **HotAvalonia 3.1.5 (MIT)** with **Avalonia.Markup.Xaml.Loader 12.1.5**. It auto-enables in Debug and watches the real source paths encoded in the build. It is installed in both startup and shared-control projects, private to each. Its runtime compiler applies AXAML; this avoids a solution build on each save, not all compilation. The [project documentation](https://github.com/Kira-NT/HotAvalonia/tree/v3.1.5) describes its hooks. [Our compatibility evidence](VALIDATION.md) establishes the tested scope for TauTick's pinned stack.

1. Select/reset a known scene and choose the desired width.
2. Edit the actual shared `src/TauTick.UI/*.axaml` or gallery scene AXAML. For a first visible experiment, change `Content="Fit"` to `Content="Fit range"` in `ChartToolbar.axaml`, then save. Restore it after inspecting.
3. Observe terminal `Reloading`/`Recompiling` output and the `GalleryStatus` surface. These report tool activity, not proof that an intended edit rendered. Wait for the expected label, property/bounds or pixels to change; fail a bounded check (for example 15 seconds) rather than silently accepting a stale preview.
4. Inspect native rendering and relevant structured targets, then exercise hover/focus/input/commands. Refine and save again. Do not invoke the solution restore/build, normal application launchers or full tests after each visual edit.
5. Convert feedback into the library's authored resources/templates. A runtime inspector property experiment becomes durable only after an equivalent source edit and recheck.

Supported and observed in the same process: shared text/layout, control additions/removals, button template and color/spacing resources. Resource recompilation reloads the AXAML Application and dependent scenes. Keep `GalleryApp.axaml` and the explicit shared `Theme` class: a C#-only Application root left resource updates stale in the initial compatibility probe. The C# gallery shell keeps scene selection; selected scenes remain available. A recreated view can lose focus, popup state, tab selection, transient input and chart viewport. Reset is the deterministic recovery; model-bound settings survive many local reloads but are not a persistence promise. `InitializeComponentState` reconnects layout-dependent toolbar/chart behavior to recreated children.

For an intentional error check, add an invalid property such as `Broken="true"` to a TextBlock in shared `SettingsPanel.axaml`. Terminal output includes the source URI, property error and line/position; the status surface shows a reload error. The tool may keep the previous rendering or leave a partial view. Correct the source, save, and verify a new visible edit in the same PID; Reset can then restore sample state. Missing reload activity or a control that never returns is a failed check, not success. Startup errors appear on stderr and terminate the process.

This direct launch does **not** apply C# edits. Supported method-body changes can use the optional .NET driver below; standard .NET hot-reload limitations apply and that path is not part of the measured AXAML loop:

```sh
dotnet watch --project samples/TauTick.UI.Gallery run --no-restore -- --scene settings
```

See [dotnet watch](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch). Keep its terminal attached and stop the driver with Ctrl+C when done; it owns any child process. Adding types/fields, changing dependencies/project files, unsupported code edits, new AXAML files unknown to the original build, moved source paths or reload-tool startup changes should use the explicit restart path. After correcting malformed source, close the gallery, build only the gallery dependency graph, and reopen the chosen scene:

```sh
dotnet build samples/TauTick.UI.Gallery --no-restore
dotnet samples/TauTick.UI.Gallery/bin/Debug/net10.0/TauTick.UI.Gallery.dll --scene settings
```

Dependency changes first require deliberate central pin/lock updates and a locked restore check. Never call restart-on-every-save a warm reload. [Official Avalonia hot reload](https://docs.avaloniaui.net/tools/hot-reload/) and [DevTools MCP](https://docs.avaloniaui.net/tools/developer-tools/mcp) are optional licensed alternatives, not installed defaults or assumed connected tools. No license was used here.

## Native inspection and session isolation

Follow the [existing Linux procedure](../../docs/development.md#native-visual-checks-on-linux) from the repository root: create a unique temporary evidence directory and XDG profile, then start a private Xvfb, D-Bus session and Openbox. Inside that shell, replace the viewer command with the direct gallery startup above. Redirect its stdout/stderr to that directory, retain `$!` as the gallery PID, and wait for `GALLERY_READY` plus usable scene controls. Keep its DISPLAY, XAUTHORITY and D-Bus environment when invoking helpers. Concurrent agents must use separate displays/buses/profiles; never target by window title alone.

The small read-only helper waits up to 15 seconds for PID-scoped targets and optionally captures native pixels. In that same shell (Python with GI/Pillow available):

```sh
python3 samples/TauTick.UI.Gallery/tools/inspect-linux.py --pid "$TAUTICK_APP_PID" \
  --id SceneSelector --id GalleryStatus --id FitData --capture "$TAUTICK_UI/gallery.png"
```

Replace `FitData` with `DisplayName` for settings or `StateAction` for controls. Captures must be outside Git. Open the PNG with the agent's image viewer; a capture saved but never inspected is not a visual check. The helper's JSON supplies current screen bounds for XTEST mouse/keyboard input using `xdotool`, following the developer guide. A matching label alone does not prove pixels or real interaction.

Important IDs: `SceneSelector`, `ResetScene`, `CompactPreview`, `WidePreview`, `GalleryStatus`, `CurrentScene`; toolbar `OpenCsv`, `FitData`, `OpenIndicators`, `BarChart`, `ContentTabs`; settings `DisplayName`, `LineWidth`, `PriceDisplay`, `SettingsError`, `ApplySettings`, `CancelSettings`; controls `StateAction`, `IconAction`, `StateToggle`, `StateCheckbox`, `StateText`, `StateInvalid`, `StateNumber`, `StateSelector`, `StateMenu`; result `SceneResult`. Scope repeated template peers to the intended scene/window/process. Popup peers can be separate native windows belonging to that PID.

The toolbar scene uses the production `ContentTabBar`: select/close a tab, add an
empty tab with +, reorder with a real drag and cancel a drag with Escape. Reset
reconstructs its deterministic synthetic tabs. Cross-window transfers are exercised
in the desktop application.

Exercise real Tab/Shift+Tab/Space/Return, hover, held press, typing, selector/menu navigation, settings Apply/Cancel and chart wheel/Fit. The red input is a labeled demonstration state; Settings supplies actual draft validation. Test compact/wide sizes and the long selector/tab labels. Xvfb checks do not prove physical DPI, GPU performance, Windows or Wayland behavior.

On Windows, inspect the gallery PID through UI Automation (Accessibility Insights or PowerShell `UIAutomationClient`) and query `AutomationId` before supplying native input. Repeat launch, selection/reset, shared style edit, malformed-edit recovery and close. These Windows operations remain unverified in this implementation environment; do not infer them from a Linux build.

Close with the window close button or Alt+F4, require process exit code 0, then exit the isolated shell so its traps/wrappers stop the owned WM, bus and Xvfb. Confirm that the owned PID/windows/helpers are gone. Direct HotAvalonia has no separate watcher server here. Do not kill another user's/agent's processes or reuse their profile. Generated logs/captures and disposable drivers stay outside Git.

## Adding scenes and reusing components

Add an AXAML UserControl under `Scenes`, using real shared controls and a typed `GalleryState` or a separate gallery-owned sample model. Add its stable key to `GalleryOptions.SceneNames` and the `GalleryWindow.ResetScene` factory; keep startup and reset deterministic, then add a focused setup/state test. Restart after adding a file/class. Use existing `SyntheticBars.Create` and `BarChart.SetDataset` for chart context; fixtures are explicitly invented, immutable and prepared before rendering.

Extend the library through presentation APIs, demonstrate the exact implementation here, then integrate at a narrow desktop seam. The production chart toolbar already uses `ChartToolbar` and `Theme`, with the same base theme order and Inter font. Do not copy its appearance into a gallery lookalike. Design values remain provisional; record accepted source-backed conventions in the [library guide](../../src/TauTick.UI/README.md), and measurements/limitations in [VALIDATION.md](VALIDATION.md).
