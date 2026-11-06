# Databento adapter boundary

This buildable library intentionally has no reader implementation yet. The CSV
task owns this directory and will implement `IHistoricalBarReader` from
`TauTick.MarketData`. See [the contract guide](../../docs/development.md) for input
ownership, timestamp semantics, validation and failures. No Avalonia or provider
SDK dependency is needed for the CSV task.
