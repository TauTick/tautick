using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TauTick.MarketData;

namespace TauTick.Charting;

/// <summary>Static, fit-to-data candles and UTC axes for an immutable bar snapshot.</summary>
public sealed class BarChart : Control
{
    private BarDataset? _dataset;
    private ChartViewport? _viewport;
    private ChartLayout? _layout;
    private TopLevel? _topLevel;
    private Size _layoutSize;
    private double _layoutScaling;
    private bool _layoutValid;
    private readonly FormattedText _empty = ChartPalette.Text("No bar data");
    private readonly FormattedText _small = ChartPalette.Text("Enlarge chart to show axes");
    private readonly FormattedText _utc = ChartPalette.Text("UTC");

    public BarChart() => ClipToBounds = true;

    internal ChartViewport? Viewport => _viewport;
    internal ChartLayout? PreparedLayout => PrepareLayout();

    /// <summary>The current immutable snapshot. Access this control only on the UI thread.</summary>
    public BarDataset? Dataset
    {
        get
        {
            VerifyAccess();
            return _dataset;
        }
    }

    /// <summary>Replaces the complete snapshot; old snapshots remain valid for their other owners.</summary>
    public void SetDataset(BarDataset dataset)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(dataset);
        var viewport = ChartViewport.Fit(dataset);
        _dataset = dataset;
        _viewport = viewport;
        InvalidateLayout();
    }

    /// <summary>Releases the chart's reference. Safe to repeat; no dataset owns disposable resources.</summary>
    public void ClearDataset()
    {
        VerifyAccess();
        _dataset = null;
        _viewport = null;
        InvalidateLayout();
    }

    // Test/measurement harness access only. There are no user-facing navigation interactions yet.
    internal void SetVisibleTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        VerifyAccess();
        if (_viewport is null) throw new InvalidOperationException("Supply a dataset first.");
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
            throw new ArgumentException("Viewport boundaries must be UTC.");
        _viewport = new(start.Ticks, end.Ticks, _viewport.Low, _viewport.High);
        InvalidateLayout();
    }

    private void InvalidateLayout()
    {
        _layout = null;
        _layoutValid = false;
        InvalidateVisual();
    }

    private ChartLayout? PrepareLayout()
    {
        var size = Bounds.Size;
        var scaling = _topLevel?.RenderScaling ?? 1;
        if (!_layoutValid || size != _layoutSize || scaling != _layoutScaling)
        {
            _layout = _dataset is not null && _viewport is not null
                ? ChartLayout.Create(_dataset, _viewport, size, scaling) : null;
            _layoutSize = size;
            _layoutScaling = scaling;
            _layoutValid = true;
        }
        return _layout;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize != _layoutSize) InvalidateLayout();
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null) _topLevel.ScalingChanged += ScalingChanged;
        InvalidateLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null) _topLevel.ScalingChanged -= ScalingChanged;
        _topLevel = null;
        InvalidateLayout();
        base.OnDetachedFromVisualTree(e);
    }

    private void ScalingChanged(object? sender, EventArgs e) => InvalidateLayout();

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        using var boundsClip = context.PushClip(new Rect(Bounds.Size));
        context.DrawRectangle(ChartPalette.Background, null, new Rect(Bounds.Size));
        var layout = PrepareLayout();
        if (layout is null)
        {
            context.DrawText(_dataset is null ? _empty : _small, new Point(14, 12));
            return;
        }
        using (context.PushClip(layout.Plot))
        {
            foreach (var label in layout.Prices)
                context.DrawLine(ChartPalette.Grid, new(layout.Plot.Left, label.Coordinate), new(layout.Plot.Right, label.Coordinate));
            foreach (var label in layout.Times)
                context.DrawLine(ChartPalette.Grid, new(label.Coordinate, layout.Plot.Top), new(label.Coordinate, layout.Plot.Bottom));
            foreach (var candle in layout.Candles)
            {
                var brush = candle.Direction > 0 ? ChartPalette.Rising : candle.Direction < 0 ? ChartPalette.Falling : ChartPalette.Doji;
                var pen = candle.Direction > 0 ? layout.RisingPen : candle.Direction < 0 ? layout.FallingPen : layout.DojiPen;
                context.DrawLine(pen, new(candle.X, candle.HighY), new(candle.X, candle.LowY));
                context.DrawRectangle(brush, null, candle.Body);
            }
        }
        foreach (var label in layout.Prices) context.DrawText(label.Text, label.Origin);
        foreach (var label in layout.Times) context.DrawText(label.Text, label.Origin);
        context.DrawText(_utc, new Point(layout.Plot.Right + 8, layout.Plot.Bottom + 10));
    }
}
