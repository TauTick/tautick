using TauTick.MarketData;

namespace TauTick.Charting;

/// <summary>Presentation coordinates only; no Avalonia types or market aggregation.</summary>
internal sealed class ChartViewport
{
    public ChartViewport(long startTicks, long endTicks, decimal low, decimal high)
    {
        if (startTicks < 0 || endTicks > DateTimeOffset.MaxValue.Ticks || endTicks <= startTicks)
            throw new ArgumentOutOfRangeException(nameof(endTicks));
        if (high < low)
            throw new ArgumentOutOfRangeException(nameof(high));
        StartTicks = startTicks;
        EndTicks = endTicks;
        Low = low;
        High = high;
    }

    public long StartTicks { get; }
    public long EndTicks { get; }
    public decimal Low { get; }
    public decimal High { get; }
    public double PriceSpan => Difference(High, Low);

    public static ChartViewport Fit(BarDataset dataset)
    {
        var low = dataset.Bars[0].Low;
        var high = dataset.Bars[0].High;
        foreach (var bar in dataset.Bars)
        {
            low = Math.Min(low, bar.Low);
            high = Math.Max(high, bar.High);
        }
        return new(dataset.Coverage.StartUtc.Ticks, dataset.Coverage.EndUtc.Ticks, low, high);
    }

    public double TimeFraction(long ticks) => (double)(ticks - StartTicks) / (EndTicks - StartTicks);

    // Subtract in decimal BEFORE converting: adjacent prices near a large offset must stay distinct.
    // Opposite decimal extremes can overflow subtraction, but their double difference is finite.
    private static double Difference(decimal a, decimal b)
    {
        try { return (double)(a - b); }
        catch (OverflowException) { return (double)a - (double)b; }
    }

    public double PriceFraction(decimal price) => Low == High ? 0.5 : Difference(price, Low) / Difference(High, Low);
}

internal static class PriceTicks
{
    public static IReadOnlyList<decimal> Create(ChartViewport viewport, int intervals)
    {
        if (viewport.Low == viewport.High) return [viewport.Low];
        var raw = viewport.PriceSpan / intervals;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var nice = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        if (nice >= (double)decimal.MaxValue) return [viewport.Low, 0, viewport.High];
        var step = Math.Max(0.0000000000000000000000000001m, (decimal)nice);
        var ticks = new List<decimal>();
        try
        {
            // Modulo avoids overflowing low / step for a tiny range at a large price offset.
            var remainder = viewport.Low % step;
            var value = viewport.Low - remainder;
            if (remainder > 0) value += step;
            while (value <= viewport.High)
            {
                ticks.Add(value);
                var next = value + step;
                if (next <= value) break; // Decimal may round a sub-unit step at its largest magnitudes.
                value = next;
            }
        }
        catch (OverflowException) { /* No further representable tick. */ }
        return ticks.Count > 0 ? ticks : [viewport.Low, viewport.High];
    }
}

internal readonly record struct VisibleBarRange(int Start, int End)
{
    public int Count => End - Start;

    /// <summary>Intersects [start, end). Binary searches retain partly visible intervals and gaps.</summary>
    public static VisibleBarRange Find(IReadOnlyList<Bar> bars, long start, long end)
    {
        if (end <= start) throw new ArgumentOutOfRangeException(nameof(end));
        var left = 0;
        var right = bars.Count;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            if (bars[middle].EndUtc.Ticks <= start) left = middle + 1;
            else right = middle;
        }
        var first = left;
        right = bars.Count;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            if (bars[middle].StartUtc.Ticks < end) left = middle + 1;
            else right = middle;
        }
        return new(first, left);
    }
}

internal readonly record struct ChartCoordinates(ChartViewport Viewport, double Left, double Top, double Width, double Height)
{
    public double X(long ticks) => Left + Viewport.TimeFraction(ticks) * Width;
    public double CenterX(Bar bar) => Left + ((bar.StartUtc.Ticks - Viewport.StartTicks) + bar.Interval.Ticks / 2.0)
        / (Viewport.EndTicks - Viewport.StartTicks) * Width;
    // Eight percent headroom at each end. A flat dataset sits in the center without invented prices.
    public double Y(decimal price) => Top + (0.92 - 0.84 * Viewport.PriceFraction(price)) * Height;
    public double IntervalWidth(TimeSpan interval) => (double)interval.Ticks / (Viewport.EndTicks - Viewport.StartTicks) * Width;
}
