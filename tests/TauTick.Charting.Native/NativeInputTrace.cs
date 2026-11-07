using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TauTick.Charting.Native;

// Opt-in observation of native event routing; this never synthesizes control events.
internal static class NativeInputTrace
{
    public static void Attach(BarChart chart, Window window)
    {
        void After(string reason) => Dispatcher.UIThread.Post(() => Write(chart, reason));
        chart.AddHandler(InputElement.PointerMovedEvent, (_, _) => After("move"), RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerPressedEvent, (_, _) => After("press"), RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerReleasedEvent, (_, _) => After("release"), RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => After("wheel"), RoutingStrategies.Bubble, true);
        chart.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => After("capture-lost"), RoutingStrategies.Bubble, true);
        window.KeyUp += (_, _) => After("key");
        window.SizeChanged += (_, _) => After("resize");
        window.Opened += (_, _) => After("opened");
        window.ScalingChanged += (_, _) => After("scale");
    }

    public static void Write(BarChart chart, string reason)
    {
        var layout = chart.PreparedLayout;
        var origin = chart.PointToScreen(default);
        Console.WriteLine("INPUT " + JsonSerializer.Serialize(new
        {
            reason, origin = new[] { origin.X, origin.Y }, scale = TopLevel.GetTopLevel(chart)?.RenderScaling,
            size = new[] { chart.Bounds.Width, chart.Bounds.Height },
            plot = layout is null ? null : new[] { layout.Plot.X, layout.Plot.Y, layout.Plot.Width, layout.Plot.Height },
            start = chart.Viewport?.StartTicks, end = chart.Viewport?.EndTicks,
            loaded = chart.Dataset?.Bars.Count, visible = layout?.Visible.Count,
            layout = layout is null ? 0 : RuntimeHelpers.GetHashCode(layout),
            dragging = chart.IsDragging, inspection = chart.InspectionReadout,
            crosshair = chart.InspectionPosition is { } p ? new[] { p.X, p.Y } : null
        }));
    }
}
