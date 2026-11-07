using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using TauTick.Charting;
using TauTick.Charting.Tests;
using TauTick.SyntheticData;

namespace TauTick.Charting.Native;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<FixtureApp>()
        .UsePlatformDetect().WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
}

internal sealed class FixtureApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new FixtureWindow(desktop.Args ?? []);
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class FixtureWindow : Window
{
    private readonly BarChart _chart = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly string[] _args;
    private bool _measuring;

    public FixtureWindow(string[] args)
    {
        _args = args;
        var unaligned = args.Contains("--unaligned");
        Title = "TauTick native chart validation — SYNTHETIC";
        Width = unaligned ? 240 : args.Contains("--small") ? 530 : 1060;
        Height = unaligned ? 300 : args.Contains("--small") ? 380 : 680;
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        AddButton(controls, "Cases", () => Load("cases"));
        AddButton(controls, "Foundation", () => Load("foundation"));
        AddButton(controls, "Single / flat", () => Load("single"));
        AddButton(controls, "960 bars", () => Load("960"));
        AddButton(controls, "100k / 1k visible", () => Load("large"));
        AddButton(controls, "Clear", () => { _chart.ClearDataset(); _status.Text = "Cleared dataset"; });
        AddButton(controls, "Fit", _chart.FitToData);
        AddButton(controls, "Small", () => { Width = 530; Height = 380; });
        AddButton(controls, "Large", () => { Width = 1060; Height = 680; });
        AddButton(controls, "Measure", () => _ = MeasureAsync());
        var header = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock { Text = "Synthetic fixtures · drag to pan · wheel to zoom · double-click to fit", FontSize = 16 },
                controls, _status
            }
        };
        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        if (!unaligned) panel.Children.Add(_chart);
        Content = unaligned ? _chart : panel;
        if (args.Contains("--trace-input")) NativeInputTrace.Attach(_chart, this);
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.F1: Load("cases"); break;
                case Key.F2: Load("foundation"); break;
                case Key.F3: Load("single"); break;
                case Key.F4: Load("960"); break;
                case Key.F5: Load("large"); break;
                case Key.F6: _chart.ClearDataset(); break;
                case Key.F7: Width = 530; Height = 380; break;
                case Key.F8: Width = 1060; Height = 680; break;
                case Key.F9: _ = MeasureAsync(); break;
                case Key.F10: _chart.FitToData(); break;
            }
        };
        Opened += (_, _) =>
        {
            Load(unaligned ? "unaligned" : "cases");
            Console.WriteLine($"Native host: {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; .NET {Environment.Version}; scale={RenderScaling}; pid={Environment.ProcessId}");
            if (_args.Contains("--measure")) Dispatcher.UIThread.Post(async () => await MeasureAsync());
        };
        ScalingChanged += (_, _) => Console.WriteLine($"Native display scale changed: {RenderScaling}");
        Closed += (_, _) => _chart.ClearDataset();
    }

    private static void AddButton(Panel panel, string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 6, 4) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void Load(string name)
    {
        var data = name switch
        {
            "unaligned" => ChartFixtures.Unaligned(),
            "foundation" => SyntheticBars.Create(),
            "single" => ChartFixtures.Dataset(ChartFixtures.Cases().Bars[3]),
            "960" => ChartFixtures.History(960),
            "large" => ChartFixtures.History(100_000),
            _ => ChartFixtures.Cases()
        };
        _chart.SetDataset(data);
        if (name == "large") _chart.SetVisibleTimeRange(data.Bars[50_000].StartUtc, data.Bars[51_000].StartUtc);
        _status.Text = $"{name} · {data.Bars.Count:N0} invented bars · UTC · scale {RenderScaling:G3}";
        if (name == "cases") _status.Text += " · 09:30 rise, 09:31 fall, 09:32 doji, 09:33 gap, 09:34 flat";
    }

    private async Task MeasureAsync()
    {
        if (_measuring) return;
        _measuring = true;
        try
        {
            Width = 1060;
            Height = 680;
            foreach (var name in new[] { "960", "large" })
            {
                Load(name);
                await Task.Delay(250); // Allow the native layout/render turn to complete before measuring.
                var layout = _chart.PreparedLayout!;
                Console.WriteLine($"Workload={name}; loaded={_chart.Dataset!.Bars.Count}; visible={layout.Visible.Count}; chart={_chart.Bounds.Size}; scale={RenderScaling}; native platform={PlatformImpl?.GetType().FullName}");
                // A real Skia software redraw, including Avalonia recording and rasterization.
                // This is not native GPU/compositor/presentation timing.
                using var target = new RenderTargetBitmap(
                    new PixelSize((int)_chart.Bounds.Width, (int)_chart.Bounds.Height), new Vector(96, 96));
                Measure("software RenderTargetBitmap", () => target.Render(_chart));
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                var sample = 0;
                Measure("routed inspection + software redraw", () =>
                {
                    var point = new Point(layout.Plot.Left + layout.Plot.Width * (0.25 + sample++ % 50 / 100.0), layout.Plot.Center.Y);
                    _chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, _chart, pointer, _chart, point,
                        0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
                    target.Render(_chart);
                    if (!ReferenceEquals(layout, _chart.PreparedLayout)) throw new InvalidOperationException("Inspection rebuilt geometry.");
                });
            }
            Load("cases");
            Console.WriteLine("Measurements complete; not presented-frame rate.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            _status.Text = exception.Message;
            Environment.ExitCode = 1;
        }
        finally { _measuring = false; }
        if (_args.Contains("--exit-after-measure")) Close();
    }

    private static void Measure(string name, Action action)
    {
        for (var i = 0; i < 20; i++) action();
        const int iterations = 100;
        var elapsed = new double[iterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            elapsed[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / iterations;
        Array.Sort(elapsed);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{name}: n={iterations}; median={elapsed[49]:F3} ms; p95={elapsed[94]:F3} ms; managed current-thread bytes/redraw={bytes}"));
    }
}
