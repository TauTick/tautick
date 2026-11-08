using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using TauTick.Charting.Studies;
using TauTick.MarketData;
using TauTick.Studies;

namespace TauTick.TestStudies;

// Deliberately synthetic custom price-level model, not a market profile or production catalog item.
internal sealed record Levels(decimal Price, string Caption);
internal sealed class SyntheticLevels : IStudyDefinition
{
    public string TypeId => "test.synthetic-levels";
    public string Name => "Synthetic levels";
    public StudyInput RequiredInput => StudyInput.Bars;
    public JsonElement DefaultSettings => JsonSerializer.SerializeToElement(new { });
    public void Validate(int version, JsonElement settings) { }
    public object Calculate(BarDataset data, JsonElement settings, CancellationToken token) => new Levels(7, "SYNTHETIC — not market-derived");
}
internal sealed class SyntheticRenderer(Levels levels) : IStudyRenderer
{
    public double ReservedHeight => 80;
    public StudyDrawingContext? Last;
    public bool Disposed;
    public event Action? Pressed;
    public event Action? Released;
    public void Draw(DrawingContext drawing, StudyDrawingContext context)
    {
        Last = context;
        drawing.DrawRectangle(Brushes.Teal, null, context.ReservedRegion);
        drawing.DrawText(new(levels.Caption, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, context.Font, 14, context.Foreground), context.ReservedRegion.TopLeft);
        drawing.DrawEllipse(Brushes.Orange, null, new(context.Plot.Center.X, context.PriceY(levels.Price)), 8, 8);
    }
    public string? HitTest(Point point, StudyDrawingContext context) => context.ReservedRegion.Contains(point) ? $"Synthetic level {levels.Price}" : null;
    public bool PointerPressed(Point point, StudyDrawingContext context)
    {
        if (HitTest(point, context) is null) return false;
        Pressed?.Invoke();
        return true;
    }
    public void Dispose() { Disposed = true; Released?.Invoke(); }
}
