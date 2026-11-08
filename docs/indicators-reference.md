# Indicator reference evidence

Reference check: 2026-11-08, before TAU-9 implementation. No ATAS X binary was
run here. The [official changelog](https://feedback.atas.net/en/changelog) identifies
Beta 8.0.15 (2026-09-04) and describes duplicate indicator instances. This is a
published reference version, not a claim that we inspected the latest installed build.
The [X product page](https://atas.net/atas-x/) describes a beta with differences
from classic ATAS. Exact current X manager layout/apply semantics remain unverified.

The [general manager guide](https://help.atas.net/en/support/solutions/articles/72000602613-indicator-settings-window)
provides supporting context for search, available/added indicators, settings and
Apply. It is **not verified X behavior**. The
[first-indicator guide](https://docs.atas.net/en/chart-indicators/first-indicator.html)
describes legend settings/visibility/removal controls, without establishing an X
build-specific interaction trace.

The [drawing API](https://docs.atas.net/en/common-api/drawing.html) motivates
custom drawing, coordinates and rendering layers. The
[X extension guide](https://docs.atas.net/en/publishing/atas-x.html) explicitly
supports Avalonia editors. Neither is a binary-compatibility target.
[Avalonia custom rendering](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering)
is the rendering reference; compilation/tests use pinned Avalonia 12.1.5.

TauTick decisions: a modeless chart-owned manager; searchable catalog, instances
and selected settings; explicit Apply for settings, immediate add/remove/order/
visibility. Closing or switching selection discards unapplied fields. Persistent
legend buttons are used instead of hover-only controls. No favorites/templates,
DLL installation or trading features. These are deliberate local decisions pending
an interactive comparison with current ATAS X. Full reference fidelity is not claimed.
