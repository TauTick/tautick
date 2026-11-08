# Workspace reference and docking choice

Evidence inspected **2026-11-08**, before workspace implementation.

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

TauTick follows the verified tab/workspace direction and the explicit TAU-8
requirements. Deliberate local choices are visible Dock targets for chart grouping
and splits, explicit detach/redock actions alongside dragging, per-chart CSV/year
controls, and a versioned local layout file with verified input references. No
ATAS account, synchronized price axes, trading controls or market connections are
introduced. The acceptance walkthrough documents TauTick's gestures directly.

## Dock evaluation

Selected **Dock 12.1.5**, MIT licensed, from NuGet's stable feed. Its package metadata
provides net10.0 assets and depends on **Avalonia 12.1.5**, matching the repository's
**net10.0** target and pinned UI packages. The package repository commit is
[`9e85df60156fc2ba454ffaee7038ec8e37d8fe78`](https://github.com/wieslawsoltes/Dock/tree/9e85df60156fc2ba454ffaee7038ec8e37d8fe78).

The [upstream guide](https://wieslawsoltes.github.io/Dock/articles/README.html) and
pinned source expose tabbed documents, proportional horizontal/vertical containers,
drag targets, floating native hosts and close-versus-move callbacks. The code-only
sample uses rebuildable document content, suitable for hosting a nested workspace
layout inside a detachable workspace document. TauTick uses this composition and
keeps Dock types inside its desktop workspace implementation. It does not build a
new docking engine or deserialize arbitrary library/runtime types.

Package/source compatibility is preliminary evidence. Application tests and native
checks must establish the actual integration; Windows and physical multi-monitor
acceptance cannot be inferred from package targets or Xvfb. See the workspace guide
for the resulting checks and outstanding limits.
