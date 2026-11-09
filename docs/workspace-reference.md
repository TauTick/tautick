# Workspace reference and docking choice

The current interaction contract is the user's explicit **workspace → windows →
tabs** clarification in TAU-11, retrieved 2026-11-09. It supersedes the TAU-8
multiple-workspace-tab interpretation. ATAS X runtime access is not a prerequisite.
The following external evidence was inspected **2026-11-08** for the earlier task.

## ATAS X evidence and limits

The [ATAS X Beta product page](https://atas.net/atas-x/) explicitly describes tabs
containing charts and widgets and a compact analytical interface. Its
[primary promotional image](https://atas.net/wp-content/uploads/2026/05/atas-x.webp)
was inspected: it shows an upper tab strip and chart-first content, but cannot
establish drag, close or redocking semantics. The page supplies no build number.

The [official changelog](https://feedback.atas.net/changelog), entry **Beta 8.0.15,
2026-09-04**, distinguishes ATAS X from Classic and reports remembering the last
workspace. This is a dated published reference, **not a claim that an installed
ATAS X build was exercised**. The linked Windows bootstrapper returned
Last-Modified 2026-08-07; that timestamp does not identify the current installed
application. No authenticated Windows/macOS ATAS X runtime was available here.

The [general workspaces page](https://atas.net/modules/workspaces/) describes
independent layouts and adjustable proportions but does not identify its build
or establish every ATAS X gesture. Classic docking documentation and Classic-only
changelog entries are not evidence for X. Exact current X tear-off, target visuals,
rename and close gestures remain unverified.

TauTick follows TAU-11 for the window/tab model. The shared content strip accepts
insertions across normal windows through native pointer capture and screen-coordinate
hit testing, with a visible insertion marker and commit on release. Dock remains
inside content tabs for chart grouping and splits. Other local choices are explicit
Detach tab, per-chart CSV/year controls and versioned layouts with verified inputs. No
ATAS account, synchronized price axes, trading controls or market connections are
introduced. The acceptance walkthrough documents TauTick's gestures directly.

## Dock evaluation

Selected **Dock 12.1.5**, MIT licensed, from NuGet's stable feed. Its package metadata
provides net10.0 assets and depends on **Avalonia 12.1.5**, matching the repository's
**net10.0** target and pinned UI packages. The package repository commit is
[`9e85df60156fc2ba454ffaee7038ec8e37d8fe78`](https://github.com/wieslawsoltes/Dock/tree/9e85df60156fc2ba454ffaee7038ec8e37d8fe78).

The [upstream guide](https://wieslawsoltes.github.io/Dock/articles/README.html) and
pinned source expose tabbed documents, proportional horizontal/vertical containers,
drag targets, floating native hosts and close-versus-move callbacks. TAU-11 retains rebuildable
chart documents and Dock tab/split containers inside each content tab. Workspace
windows and top-level tabs are owned by the desktop controller; inner Dock roots
cannot create native windows. Dock types remain inside the desktop implementation;
arbitrary runtime types are never deserialized.

Package/source compatibility is preliminary evidence. Application tests and native
checks must establish the actual integration; Windows and physical multi-monitor
acceptance cannot be inferred from package targets or Xvfb. See the workspace guide
for the resulting checks and outstanding limits.
