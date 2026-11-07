using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TauTick.MarketData;

namespace TauTick.Charting;

/// <summary>Candles, bounded time navigation and exact inspection of an immutable bar snapshot.</summary>
public sealed class BarChart : Control
{
    private BarDataset? _dataset;
    private ChartViewport? _viewport;
    private ChartViewport? _fittedViewport;
    private ChartLayout? _layout;
    private TopLevel? _topLevel;
    private Size _layoutSize;
    private double _layoutScaling;
    private bool _layoutValid;
    private IPointer? _dragPointer;
    private ChartViewport? _dragViewport;
    private Point _dragOrigin;
    private double _dragWidth;
    private Point? _inspectionPosition;
    private int _inspectedIndex = -1;
    private FormattedText? _inspectionText;
    private readonly FormattedText _empty = ChartPalette.Text("No bar data");
    private readonly FormattedText _small = ChartPalette.Text("Enlarge chart to show axes");
    private readonly FormattedText _smallInspection = ChartPalette.Text("Enlarge chart to inspect values");
    private readonly FormattedText _utc = ChartPalette.Text("UTC");

    public BarChart() => ClipToBounds = true;

    internal ChartViewport? Viewport => _viewport;
    internal ChartLayout? PreparedLayout => PrepareLayout();
    internal bool IsDragging => _dragPointer is not null;
    internal Point? InspectionPosition => _inspectionPosition;
    internal Bar? InspectedBar => _inspectedIndex >= 0 ? _dataset?.Bars[_inspectedIndex] : null;
    internal string? InspectionReadout => InspectedBar is { } bar ? BarInspection.Format(bar) : null;

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
        ResetInteraction();
        _dataset = dataset;
        _viewport = _fittedViewport = viewport;
        InvalidateLayout();
    }

    /// <summary>Releases the chart's reference. Safe to repeat; no dataset owns disposable resources.</summary>
    public void ClearDataset()
    {
        VerifyAccess();
        ResetInteraction();
        _dataset = null;
        _viewport = _fittedViewport = null;
        InvalidateLayout();
    }

    /// <summary>Fits the complete accepted dataset without changing its bars or timeframe.</summary>
    public void FitToData()
    {
        VerifyAccess();
        if (_dataset is null) return;
        ResetInteraction();
        _viewport = _fittedViewport;
        InvalidateLayout();
    }

    // Test/measurement harness access only; normal navigation goes through native pointer input.
    internal void SetVisibleTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        VerifyAccess();
        if (_viewport is null) throw new InvalidOperationException("Supply a dataset first.");
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
            throw new ArgumentException("Viewport boundaries must be UTC.");
        ResetInteraction();
        _viewport = new(start.Ticks, end.Ticks, _viewport.Low, _viewport.High);
        InvalidateLayout();
    }

    private void InvalidateLayout()
    {
        _layout = null;
        _layoutValid = false;
        _inspectionText = null;
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
        if (finalSize != _layoutSize)
        {
            ResetInteraction();
            InvalidateLayout();
        }
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
        ResetInteraction();
        if (_topLevel is not null) _topLevel.ScalingChanged -= ScalingChanged;
        _topLevel = null;
        InvalidateLayout();
        base.OnDetachedFromVisualTree(e);
    }

    private void ScalingChanged(object? sender, EventArgs e)
    {
        ResetInteraction();
        InvalidateLayout();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type != PointerType.Mouse || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;
        var position = e.GetPosition(this);
        var layout = PrepareLayout();
        if (layout is null || !InsidePlot(layout, position)) return;
        if (e.ClickCount == 2)
        {
            FitToData();
            UpdateInspection(position);
        }
        else
        {
            _dragOrigin = position;
            _dragWidth = layout.Plot.Width;
            _dragViewport = _viewport;
            _dragPointer = e.Pointer;
            e.Pointer.Capture(this);
            UpdateInspection(position);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse) return;
        var position = e.GetPosition(this);
        if (e.Pointer == _dragPointer && _dragViewport is not null && _dataset is not null)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                ChangeViewport(ChartNavigation.Pan(_dragViewport, _dataset, (_dragOrigin.X - position.X) / _dragWidth));
            else EndDrag();
        }
        UpdateInspection(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer != _dragPointer || e.InitialPressMouseButton != MouseButton.Left) return;
        EndDrag();
        UpdateInspection(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer == _dragPointer) ResetInteraction();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HideInspection();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var position = e.GetPosition(this);
        var layout = PrepareLayout();
        if (layout is null || _dataset is null || _viewport is null || !InsidePlot(layout, position)
            || !double.IsFinite(e.Delta.Y) || e.Delta.Y == 0) return;
        EndDrag();
        var fraction = (position.X - layout.Plot.Left) / layout.Plot.Width;
        ChangeViewport(ChartNavigation.Zoom(_viewport, _dataset, fraction, Math.Pow(1.2, -Math.Clamp(e.Delta.Y, -100, 100))));
        UpdateInspection(position);
        e.Handled = true;
    }

    private void ChangeViewport(ChartViewport viewport)
    {
        if (_viewport is not null && viewport.StartTicks == _viewport.StartTicks && viewport.EndTicks == _viewport.EndTicks) return;
        _viewport = viewport;
        InvalidateLayout();
    }

    private void EndDrag()
    {
        var pointer = _dragPointer;
        _dragPointer = null;
        _dragViewport = null;
        if (pointer?.Captured == this) pointer.Capture(null);
    }

    private void ResetInteraction()
    {
        EndDrag();
        HideInspection();
    }

    private void HideInspection()
    {
        _inspectionPosition = null;
        _inspectedIndex = -1;
        _inspectionText = null;
        InvalidateVisual();
    }

    private static bool InsidePlot(ChartLayout layout, Point position) =>
        position.X >= layout.Plot.Left && position.X < layout.Plot.Right
        && position.Y >= layout.Plot.Top && position.Y < layout.Plot.Bottom;

    private void UpdateInspection(Point position)
    {
        var layout = PrepareLayout();
        if (layout is null || _dataset is null || !InsidePlot(layout, position))
        {
            HideInspection();
            return;
        }
        _inspectionPosition = position;
        var index = BarInspection.Find(_dataset.Bars, layout.Coordinates, position.X);
        if (index != _inspectedIndex) _inspectionText = null;
        _inspectedIndex = index;
        // Pointer-only invalidation reuses the prepared candles, axes and pens.
        InvalidateVisual();
    }

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
        DrawInspection(context, layout);
    }

    private void DrawInspection(DrawingContext context, ChartLayout layout)
    {
        if (_inspectionPosition is not { } position) return;
        using (context.PushClip(layout.Plot))
        {
            context.DrawLine(ChartPalette.Crosshair, new(position.X, layout.Plot.Top), new(position.X, layout.Plot.Bottom));
            context.DrawLine(ChartPalette.Crosshair, new(layout.Plot.Left, position.Y), new(layout.Plot.Right, position.Y));
        }
        if (InspectedBar is not { } bar) return;
        if (_inspectionText is null)
        {
            _inspectionText = ChartPalette.Text(BarInspection.Format(bar));
            _inspectionText.MaxTextWidth = Math.Max(1, layout.Plot.Width - 20);
            // On a short/narrow chart, use the control's available space before giving up detail.
            if (_inspectionText.Height + 16 > layout.Plot.Height)
                _inspectionText.MaxTextWidth = Math.Max(1, Bounds.Width - 20);
        }
        // Never silently clip or round an exact value when the control cannot fit the readout.
        var text = _inspectionText.Height + 16 <= Bounds.Height ? _inspectionText : _smallInspection;
        var width = text.Width + 12;
        var height = text.Height + 8;
        var x = position.X < layout.Plot.Center.X ? layout.Plot.Right - width - 4 : layout.Plot.Left + 4;
        var origin = new Point(Math.Clamp(x, 4, Math.Max(4, Bounds.Width - width - 4)),
            Math.Min(layout.Plot.Top + 4, Bounds.Height - height - 4));
        context.DrawRectangle(ChartPalette.Background, ChartPalette.Grid, new Rect(origin, new Size(width, height)));
        context.DrawText(text, origin + new Vector(6, 4));
    }
}
