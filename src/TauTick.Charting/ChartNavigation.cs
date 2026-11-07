using System.Globalization;
using TauTick.MarketData;

namespace TauTick.Charting;

/// <summary>Bounded time navigation; prices and the immutable bar intervals are unchanged.</summary>
internal static class ChartNavigation
{
    public static ChartViewport Zoom(ChartViewport viewport, BarDataset dataset, double fraction, double factor)
    {
        if (!double.IsFinite(fraction) || !double.IsFinite(factor) || factor <= 0) return viewport;
        var current = viewport.EndTicks - viewport.StartTicks;
        var minimum = dataset.Interval.Ticks;
        var maximum = dataset.Coverage.EndUtc.Ticks - dataset.Coverage.StartUtc.Ticks;
        var span = factor >= (double)maximum / current ? maximum
            : factor <= (double)minimum / current ? minimum
            : (long)Math.Clamp(decimal.Round(current * (decimal)factor), minimum, maximum);
        // Do relative arithmetic in decimal so a single tick near year 9999 is not lost.
        var start = viewport.StartTicks + (current - span) * (decimal)Math.Clamp(fraction, 0, 1);
        return Range(viewport, dataset, start, span);
    }

    public static ChartViewport Pan(ChartViewport viewport, BarDataset dataset, double fraction)
    {
        if (!double.IsFinite(fraction)) return viewport;
        var span = viewport.EndTicks - viewport.StartTicks;
        var coverage = dataset.Coverage.EndUtc.Ticks - dataset.Coverage.StartUtc.Ticks;
        var bound = (double)coverage / span;
        var start = viewport.StartTicks + span * (decimal)Math.Clamp(fraction, -bound, bound);
        return Range(viewport, dataset, start, Math.Min(span, coverage));
    }

    private static ChartViewport Range(ChartViewport viewport, BarDataset dataset, decimal start, long span)
    {
        var first = (long)Math.Clamp(decimal.Round(start), dataset.Coverage.StartUtc.Ticks,
            dataset.Coverage.EndUtc.Ticks - span);
        return first == viewport.StartTicks && first + span == viewport.EndTicks ? viewport
            : new(first, first + span, viewport.Low, viewport.High);
    }
}

internal static class BarInspection
{
    /// <summary>
    /// Finds the actual half-open interval under X in O(log n). Comparing in drawing coordinates
    /// makes an exact rendered boundary belong to the next bar, without rounding a time into a gap.
    /// </summary>
    public static int Find(IReadOnlyList<Bar> bars, ChartCoordinates coordinates, double x)
    {
        if (!double.IsFinite(x) || x < coordinates.Left || x >= coordinates.Left + coordinates.Width) return -1;
        var left = 0;
        var right = bars.Count;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            if (coordinates.X(bars[middle].StartUtc.Ticks) <= x) left = middle + 1;
            else right = middle;
        }
        var index = left - 1;
        return index >= 0 && x < coordinates.X(bars[index].EndUtc.Ticks) ? index : -1;
    }

    public static string Format(Bar bar) => string.Create(CultureInfo.InvariantCulture,
        $"{bar.StartUtc:yyyy-MM-dd HH:mm:ss.fffffff} UTC\nO {bar.Open}   H {bar.High}\nL {bar.Low}   C {bar.Close}\nV {bar.Volume}");
}
