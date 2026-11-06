using TauTick.MarketData;

namespace TauTick.SyntheticData;

/// <summary>Invented data for wiring and contract tests; never a market recording.</summary>
public static class SyntheticBars
{
    public static BarDataset Create(string sourceName = "synthetic-in-memory")
    {
        var start = new DateTimeOffset(2025, 9, 22, 0, 0, 0, TimeSpan.Zero);
        var minute = TimeSpan.FromMinutes(1);
        return new BarDataset(
            new InstrumentIdentity("SYNTH-Z25", "SYNTHETIC", InstrumentKind.DatedContract,
                new DateOnly(2025, 12, 1)),
            new DataSourceIdentity("Synthetic", "Invented bars", "ohlcv-1m", sourceName, "synthetic-1"),
            [
                new Bar(start, minute, 100.25m, 101.50m, 100.00m, 101.00m, 12),
                new Bar(start.AddMinutes(1), minute, 101.00m, 101.25m, 100.75m, 101.00m, 7), // Doji.
                new Bar(start.AddMinutes(3), minute, 100.50m, 100.50m, 100.50m, 100.50m, 0) // Gap, flat, known zero.
            ]);
    }
}
