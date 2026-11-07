using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TauTick.Charting.Tests;

public sealed class ChartInteractionTests
{
    [AvaloniaFact]
    public void SetDatasetInputPanZoomResetReplaceAndClearUseTheRealControl()
    {
        var chart = new BarChart();
        var data = ChartFixtures.History(100);
        chart.SetDataset(data);
        using var host = new Host(chart);
        var fit = chart.Viewport!;
        var plot = chart.PreparedLayout!.Plot;
        var pointer = new Point(plot.X + plot.Width * 0.3, plot.Center.Y);
        var before = fit.StartTicks + (decimal)0.3 * (fit.EndTicks - fit.StartTicks);
        host.Window.MouseWheel(pointer, new(0, 5));
        var zoomed = chart.Viewport!;
        var after = zoomed.StartTicks + (decimal)0.3 * (zoomed.EndTicks - zoomed.StartTicks);
        Assert.InRange(Math.Abs(after - before), 0, 1);
        Assert.True(zoomed.EndTicks - zoomed.StartTicks < fit.EndTicks - fit.StartTicks);
        Assert.Equal(fit.Low, zoomed.Low);
        Assert.Equal(fit.High, zoomed.High);
        host.Window.MouseDown(pointer, MouseButton.Left);
        Assert.True(chart.IsDragging);
        host.Window.MouseMove(pointer + new Vector(-100, 0), RawInputModifiers.LeftMouseButton);
        Assert.True(chart.Viewport!.StartTicks > zoomed.StartTicks);
        host.Window.MouseUp(pointer + new Vector(-100, 0), MouseButton.Left);
        Assert.False(chart.IsDragging);
        // A native double click is represented by the routed click-count contract.
        Press(chart, pointer, clickCount: 2);
        Assert.Same(fit, chart.Viewport);
        Assert.Same(data, chart.Dataset);
        Assert.NotNull(chart.InspectedBar);
        chart.SetDataset(ChartFixtures.Cases());
        Assert.Null(chart.InspectedBar);
        Assert.Null(chart.InspectionPosition);
        Assert.Equal(ChartFixtures.Start.Ticks, chart.Viewport!.StartTicks);
        host.Window.MouseMove(pointer);
        Assert.NotNull(chart.InspectedBar);
        chart.ClearDataset();
        Assert.Null(chart.Viewport);
        Assert.Null(chart.InspectedBar);
        host.Window.MouseWheel(pointer, new(0, 100));
        host.Window.MouseDown(pointer, MouseButton.Left);
        host.Window.MouseMove(pointer, RawInputModifiers.LeftMouseButton);
        host.Window.MouseUp(pointer, MouseButton.Left);
        Assert.False(chart.IsDragging);
        Assert.Null(chart.PreparedLayout);
    }

    [AvaloniaFact]
    public void InspectionReusesGeometryAndHonorsGapBoundariesExitAndExactReadout()
    {
        var chart = new BarChart();
        var data = ChartFixtures.Cases();
        chart.SetDataset(data);
        using var host = new Host(chart);
        var prepared = chart.PreparedLayout!;
        Point At(int minute) => new(prepared.Coordinates.X(ChartFixtures.Start.AddMinutes(minute).Ticks), prepared.Plot.Center.Y);
        using var target = new RenderTargetBitmap(new(900, 500), new(96, 96));
        for (var i = 0; i < 50; i++)
        {
            host.Window.MouseMove(At(0) + new Vector(i, 0));
            target.Render(chart);
            Assert.Same(prepared, chart.PreparedLayout);
            Assert.Same(prepared.Candles, chart.PreparedLayout!.Candles);
            Assert.Same(prepared.Times, chart.PreparedLayout.Times);
        }
        Assert.Same(data.Bars[0], chart.InspectedBar);
        Assert.Equal("2025-09-22 09:30:00.0000000 UTC\nO 100   H 105\nL 98   C 103\nV 12", chart.InspectionReadout);
        host.Window.MouseMove(At(1));
        Assert.Same(data.Bars[1], chart.InspectedBar);
        host.Window.MouseMove(At(3));
        Assert.Null(chart.InspectedBar);
        Assert.NotNull(chart.InspectionPosition); // Crosshair remains in an empty interval.
        host.Window.MouseMove(At(4));
        Assert.Same(data.Bars[3], chart.InspectedBar);
        Assert.EndsWith("V 0", chart.InspectionReadout);
        host.Window.MouseMove(new(prepared.Plot.Right + 10, prepared.Plot.Center.Y));
        Assert.Null(chart.InspectionPosition);
        Assert.Null(chart.InspectionReadout);
        Assert.Same(prepared, chart.PreparedLayout);
    }

    [AvaloniaTheory]
    [InlineData("release")]
    [InlineData("loss")]
    [InlineData("clear")]
    [InlineData("replace")]
    [InlineData("detach")]
    [InlineData("resize")]
    [InlineData("scale")]
    public void CaptureIsReleasedAndStateCannotLeakAcrossLifecycleChanges(string action)
    {
        var chart = new BarChart();
        chart.SetDataset(ChartFixtures.History(100));
        using var host = new Host(chart);
        var point = chart.PreparedLayout!.Plot.Center;
        host.Window.MouseWheel(point, new(0, 5));
        var pointer = Press(chart, point);
        Assert.Same(chart, pointer.Captured);
        Assert.True(chart.IsDragging);
        switch (action)
        {
            case "release":
                chart.RaiseEvent(new PointerReleasedEventArgs(chart, pointer, chart, point, 0,
                    new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
                break;
            case "loss": pointer.Capture(null); break;
            case "clear": chart.ClearDataset(); break;
            case "replace": chart.SetDataset(ChartFixtures.Cases()); break;
            case "detach": host.Window.Content = null; break;
            case "resize": host.Window.Width = 600; host.Refresh(); break;
            case "scale": host.Window.SetRenderScaling(2); break;
        }
        Assert.False(chart.IsDragging);
        Assert.Null(pointer.Captured);
        if (action != "release") Assert.Null(chart.InspectedBar);
    }

    [AvaloniaFact]
    public void DragContinuesBeyondPlotAndReleaseOutsideHidesInspection()
    {
        var chart = new BarChart();
        var data = ChartFixtures.History(100);
        chart.SetDataset(data);
        using var host = new Host(chart);
        var point = chart.PreparedLayout!.Plot.Center;
        host.Window.MouseWheel(point, new(0, 5));
        host.Window.MouseDown(point, MouseButton.Left);
        host.Window.MouseMove(new(-2000, point.Y), RawInputModifiers.LeftMouseButton);
        Assert.True(chart.IsDragging);
        Assert.Equal(data.Coverage.EndUtc.Ticks, chart.Viewport!.EndTicks);
        Assert.Null(chart.InspectionPosition);
        host.Window.MouseUp(new(-2000, point.Y), MouseButton.Left);
        Assert.False(chart.IsDragging);
        Assert.Null(chart.InspectedBar);
    }

    [AvaloniaFact]
    public void ResizedAndScaledInputUsesLogicalCoordinatesAndOutsideGesturesDoNothing()
    {
        var chart = new BarChart();
        var data = ChartFixtures.Cases();
        chart.SetDataset(data);
        using var host = new Host(chart);
        foreach (var scale in new[] { 1.0, 1.25, 2.0 })
        {
            host.Window.SetRenderScaling(scale);
            host.Window.Width = 650 + scale * 50;
            host.Refresh();
            var layout = chart.PreparedLayout!;
            var point = new Point(layout.Coordinates.CenterX(data.Bars[1]), layout.Plot.Center.Y);
            host.Window.MouseMove(point);
            Assert.Same(data.Bars[1], chart.InspectedBar);
            var viewport = chart.Viewport;
            var outside = new Point(layout.Plot.Right + 5, point.Y);
            host.Window.MouseWheel(outside, new(0, 5));
            host.Window.MouseDown(outside, MouseButton.Left);
            host.Window.MouseMove(outside + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(outside, MouseButton.Left);
            Assert.Same(viewport, chart.Viewport);
            Assert.False(chart.IsDragging);
        }
    }

    private static Pointer Press(BarChart chart, Point point, int clickCount = 1)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        chart.RaiseEvent(new PointerPressedEventArgs(chart, pointer, chart, point, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, clickCount));
        return pointer;
    }

    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(BarChart chart)
        {
            Window = new Window { Width = 900, Height = 500, Content = chart };
            Window.Show();
            Refresh();
        }
        public void Refresh()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            using var frame = Window.CaptureRenderedFrame();
        }
        public void Dispose() => Window.Close();
    }
}
