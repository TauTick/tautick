# Shared UI authoring

Read [the library guide](README.md) and [the gallery runbook](../../samples/TauTick.UI.Gallery/README.md) before UI work. Root guidance still applies.

- Keep public APIs about presentation values and commands. Do not reference market adapters, calculations, persistence, application services or gallery fixtures.
- Author reusable structure in AXAML and design values/templates in shared resources. Preserve Avalonia input, focus and accessibility behavior; give important controls stable automation IDs.
- Verify affected components in the native gallery with real input and inspected captures. Run focused binding/command tests and the affected desktop integration checks; record platform limits accurately.
- Keep reload packages and diagnostics out of production Release output. Runtime experiments become durable only through saved source edits.
