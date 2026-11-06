using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Charting.Tests;

/// <summary>Invented prices and times, never a market recording.</summary>
internal static class ChartFixtures
{
    public static readonly DateTimeOffset Start = new(2025, 9, 22, 9, 30, 0, TimeSpan.Zero);
    public static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    public static BarDataset Dataset(params Bar[] bars)
    {
        var template = SyntheticBars.Create();
        return new(template.Instrument,
            new DataSourceIdentity("Synthetic", "Invented chart fixtures", "ohlcv", "chart-validation"), bars);
    }

    public static BarDataset Cases() => Dataset(
        new(Start, Minute, 100, 105, 98, 103, 12),
        new(Start.AddMinutes(1), Minute, 103, 104, 97, 99, 14),
        new(Start.AddMinutes(2), Minute, 100, 102, 98, 100, 9),
        new(Start.AddMinutes(4), Minute, 100, 100, 100, 100, 0));

    public static BarDataset Unaligned() => Dataset(new Bar(Start.AddTicks(150000001),
        TimeSpan.FromSeconds(61), 100, 101, 99, 100, 0));

    public static BarDataset History(int count)
    {
        var bars = new Bar[count];
        for (var i = 0; i < count; i++)
        {
            var open = 100m + Math.Round((decimal)Math.Sin(i * 0.07) * 8m, 2);
            var close = open + (i % 7 - 3) * 0.25m;
            bars[i] = new(Start.AddMinutes(i), Minute, open, Math.Max(open, close) + 0.75m,
                Math.Min(open, close) - 0.5m, close, i % 100);
        }
        return Dataset(bars);
    }
}
