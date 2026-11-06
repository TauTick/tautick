using System.Globalization;
using Avalonia;
using Avalonia.Media;
using TauTick.MarketData;

namespace TauTick.Charting;

internal readonly record struct AxisLabel(FormattedText Text, Point Origin, double Coordinate, string Value);
internal readonly record struct CandleGeometry(double X, double HighY, double LowY, Rect Body, int Direction);

/// <summary>Prepared geometry and shaped labels, rebuilt only for data, viewport, size or DPI changes.</summary>
internal sealed class ChartLayout
{
    private static readonly long[] TimeSteps =
    [
        1, 2, 5, 10, 20, 50, 100, 200, 500, 1_000, 2_000, 5_000,
        10_000, 20_000, 50_000, 100_000, 200_000, 500_000, 1_000_000, 2_000_000, 5_000_000,
        TimeSpan.TicksPerSecond, 2 * TimeSpan.TicksPerSecond, 5 * TimeSpan.TicksPerSecond,
        10 * TimeSpan.TicksPerSecond, 15 * TimeSpan.TicksPerSecond, 30 * TimeSpan.TicksPerSecond,
        TimeSpan.TicksPerMinute, 2 * TimeSpan.TicksPerMinute, 5 * TimeSpan.TicksPerMinute,
        10 * TimeSpan.TicksPerMinute, 15 * TimeSpan.TicksPerMinute, 30 * TimeSpan.TicksPerMinute,
        TimeSpan.TicksPerHour, 2 * TimeSpan.TicksPerHour, 4 * TimeSpan.TicksPerHour,
        8 * TimeSpan.TicksPerHour, 12 * TimeSpan.TicksPerHour, TimeSpan.TicksPerDay,
        2 * TimeSpan.TicksPerDay, 7 * TimeSpan.TicksPerDay, 30 * TimeSpan.TicksPerDay,
        90 * TimeSpan.TicksPerDay, 365 * TimeSpan.TicksPerDay
    ];

    private ChartLayout(Rect plot, ChartCoordinates coordinates, VisibleBarRange visible,
        CandleGeometry[] candles, AxisLabel[] prices, AxisLabel[] times, double pixel)
    {
        Plot = plot;
        Coordinates = coordinates;
        Visible = visible;
        Candles = candles;
        Prices = prices;
        Times = times;
        Pixel = pixel;
        RisingPen = new Pen(ChartPalette.Rising, pixel).ToImmutable();
        FallingPen = new Pen(ChartPalette.Falling, pixel).ToImmutable();
        DojiPen = new Pen(ChartPalette.Doji, pixel).ToImmutable();
    }

    public Rect Plot { get; }
    public ChartCoordinates Coordinates { get; }
    public VisibleBarRange Visible { get; }
    public CandleGeometry[] Candles { get; }
    public AxisLabel[] Prices { get; }
    public AxisLabel[] Times { get; }
    public double Pixel { get; }
    public IPen RisingPen { get; }
    public IPen FallingPen { get; }
    public IPen DojiPen { get; }

    public static ChartLayout? Create(BarDataset dataset, ChartViewport viewport, Size size, double scaling)
    {
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width < 120 || size.Height < 100)
            return null;
        var count = Math.Clamp((int)(size.Height / 72), 2, 8);
        var values = PriceTicks.Create(viewport, count)
            .Select(price => (Price: price, Text: ChartPalette.Text(price.ToString("G29", CultureInfo.InvariantCulture)))).ToArray();
        var gutter = Math.Max(64, values.Max(value => value.Text.Width) + 18);
        var plot = new Rect(14, 12, Math.Max(0, size.Width - gutter - 14), size.Height - 66);
        if (plot.Width < 70) return null;
        var coordinates = new ChartCoordinates(viewport, plot.X, plot.Y, plot.Width, plot.Height);
        var prices = values.Select(value => new AxisLabel(value.Text,
            new Point(plot.Right + 8, coordinates.Y(value.Price) - value.Text.Height / 2), coordinates.Y(value.Price),
            value.Price.ToString("G29", CultureInfo.InvariantCulture))).ToArray();
        var times = CreateTimeLabels(viewport, coordinates, plot);
        if (times.Length == 0) return null; // Show the small-control message instead of an unreadable time axis.
        var visible = VisibleBarRange.Find(dataset.Bars, viewport.StartTicks, viewport.EndTicks);
        var pixel = 1 / (double.IsFinite(scaling) && scaling > 0 ? scaling : 1);
        var width = Math.Max(pixel, Math.Min(coordinates.IntervalWidth(dataset.Interval) * 0.7, 24));
        var candles = new CandleGeometry[visible.Count];
        for (var i = visible.Start; i < visible.End; i++)
        {
            var bar = dataset.Bars[i];
            var x = coordinates.CenterX(bar);
            var openY = coordinates.Y(bar.Open);
            var closeY = coordinates.Y(bar.Close);
            var height = Math.Max(pixel, Math.Abs(openY - closeY));
            var top = (openY + closeY - height) / 2;
            // A minimum-height body must cover a complete device pixel, including a flat doji.
            if (height == pixel) top = Math.Floor(top / pixel) * pixel;
            var body = new Rect(x - width / 2, top, width, height);
            candles[i - visible.Start] = new(x, coordinates.Y(bar.High), coordinates.Y(bar.Low), body, bar.Close.CompareTo(bar.Open));
        }
        return new(plot, coordinates, visible, candles, prices, times, pixel);
    }

    private static AxisLabel[] CreateTimeLabels(ChartViewport viewport, ChartCoordinates coordinates, Rect plot)
    {
        var span = viewport.EndTicks - viewport.StartTicks;
        var target = span / Math.Max(1, (int)(plot.Width / 155));
        var step = TimeSteps.FirstOrDefault(value => value >= target);
        if (step == 0) step = target; // Multi-year histories, still bounded by label count.
        var first = viewport.StartTicks + (step - viewport.StartTicks % step) % step;
        if (first > viewport.EndTicks) first = viewport.StartTicks;
        var labels = new List<AxisLabel>();
        var lastRight = double.NegativeInfinity;
        for (var ticks = first; ticks <= viewport.EndTicks; ticks += step)
        {
            // Fallback starts and long-history steps need not align to a minute or second.
            // The label must preserve the instant at its coordinate, regardless of tick spacing.
            var format = step < TimeSpan.TicksPerSecond || ticks % TimeSpan.TicksPerSecond != 0 ? "yyyy-MM-dd\nHH:mm:ss.fffffff"
                : step < TimeSpan.TicksPerMinute || ticks % TimeSpan.TicksPerMinute != 0 ? "yyyy-MM-dd\nHH:mm:ss" : "yyyy-MM-dd\nHH:mm";
            var value = new DateTimeOffset(ticks, TimeSpan.Zero).ToString(format, CultureInfo.InvariantCulture);
            var text = ChartPalette.Text(value);
            var x = coordinates.X(ticks);
            var left = Math.Clamp(x - text.Width / 2, plot.Left, Math.Max(plot.Left, plot.Right - text.Width));
            if (text.Width <= plot.Width && left >= lastRight + 12)
            {
                labels.Add(new(text, new Point(left, plot.Bottom + 10), x, value));
                lastRight = left + text.Width;
            }
        }
        return labels.ToArray();
    }
}

internal static class ChartPalette
{
    public static readonly IBrush Background = Brush.Parse("#101820").ToImmutable();
    public static readonly IBrush Rising = Brush.Parse("#45D6B0").ToImmutable();
    public static readonly IBrush Falling = Brush.Parse("#FF7285").ToImmutable();
    public static readonly IBrush Doji = Brush.Parse("#F4CF78").ToImmutable();
    public static readonly IBrush Foreground = Brush.Parse("#C7D3DF").ToImmutable();
    public static readonly IPen Grid = new Pen(Brush.Parse("#293641"), 1).ToImmutable();
    public static FormattedText Text(string value) => new(value, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Typeface.Default, 12, Foreground);
}
