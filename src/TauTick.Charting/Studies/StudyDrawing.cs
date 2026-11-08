using System.Globalization;
using Avalonia;
using Avalonia.Media;
using TauTick.MarketData;
using TauTick.Studies;

namespace TauTick.Charting.Studies;

/// <summary>UI-thread drawing contract. Custom operations submitted to DrawingContext.Custom must
/// own immutable render-thread data/resources and release them from ICustomDrawOperation.Dispose.</summary>
public interface IStudyRenderer : IDisposable
{
    double ReservedHeight => 0;
    void Draw(DrawingContext drawing, StudyDrawingContext context);
    string? Inspect(int barIndex) => null;
    string? HitTest(Point point, StudyDrawingContext context) => null;
    bool PointerPressed(Point point, StudyDrawingContext context) => false;
}

/// <summary>Allocated regions are in device-independent pixels. Order is bottom to top.</summary>
public sealed record StudyDrawingContext(Rect Plot, Rect PriceAxis, Rect TimeAxis, Rect Margin,
    Rect ReservedRegion, double DpiScale, int Order, long StartUtcTicks, long EndUtcTicks,
    decimal Low, decimal High, Typeface Font, IBrush Foreground, IBrush Background,
    Func<long, double> TimeX, Func<decimal, double> PriceY, int FirstBar, int EndBar);

public sealed record StudyVisual(Guid InstanceId, BarDataset Dataset, IStudyRenderer Renderer);

public sealed class VwapRenderer(BarDataset dataset, VwapResult result, string color, double width) : IStudyRenderer
{
    private readonly IPen _pen = new Pen(Brush.Parse(color), width).ToImmutable();
    public void Draw(DrawingContext drawing, StudyDrawingContext context)
    {
        using var clip = drawing.PushClip(context.Plot);
        Point? previous = null;
        long previousEnd = -1;
        for (var i = Math.Max(0, context.FirstBar - 1); i < Math.Min(dataset.Bars.Count, context.EndBar + 1); i++)
        {
            var bar = dataset.Bars[i];
            if (result.Values[i] is not { } value) { previous = null; continue; }
            var point = new Point(context.TimeX(bar.StartUtc.Ticks + bar.Interval.Ticks / 2), context.PriceY(value));
            if (previous is { } start && previousEnd == bar.StartUtc.Ticks) drawing.DrawLine(_pen, start, point);
            else drawing.DrawEllipse(_pen.Brush, null, point, _pen.Thickness / 2, _pen.Thickness / 2);
            previous = point;
            previousEnd = bar.EndUtc.Ticks;
        }
    }
    public string? Inspect(int barIndex) => barIndex < 0 || barIndex >= result.Values.Count ? null
        : $"VWAP ({result.Price}, bar estimate) {result.Values[barIndex]?.ToString("G29", CultureInfo.InvariantCulture) ?? "unavailable"}";
    public void Dispose() { }
}
