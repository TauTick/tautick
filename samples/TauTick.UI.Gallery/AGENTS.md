# Native gallery work

Read [the runbook](README.md), [shared UI guidance](../../src/TauTick.UI/AGENTS.md) and [the native Linux procedure](../../docs/development.md#native-visual-checks-on-linux). Root guidance still applies.

- Scenes use the actual shared implementations with deterministic, clearly synthetic fixtures and a reset path. Do not depend on credentials, downloaded data, personal settings or unfinished product features.
- Verify startup scene selection, reset, reload readiness/errors and recovery in the running native process. Use structured targets scoped to the owned window/process and real keyboard/pointer input; open native captures for layout checks.
- Use a private display, bus and temporary profile for isolated validation. Keep captures/logs/disposable drivers outside Git and clean up only owned processes.
- Keep scene state and development diagnostics in the gallery. Shared components and the production application must not depend on it.
