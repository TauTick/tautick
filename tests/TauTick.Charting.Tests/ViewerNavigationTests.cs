using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TauTick.Desktop;

namespace TauTick.Charting.Tests;

public sealed class ViewerNavigationTests
{
    [AvaloniaFact]
    public async Task ProductionCsvViewerNavigatesInspectsFitsAndReplacesThroughItsExistingHandoff()
    {
        const string csv = """
            ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
            2025-09-22T09:30:00.0000000Z,33,1,42,100.125,105.375,98.25,103.5,123,ESZ5
            2025-09-22T09:31:00.0000000Z,33,1,42,103,104,97,99,456,ESZ5
            2025-09-22T09:33:00.0000000Z,33,1,42,100,102,98,100,0,ESZ5
            """;
        var picker = new Picker(csv);
        var window = new MainWindow(picker: picker);
        window.Show();
        try
        {
            window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "ContractYear").Text = "2025";
            await window.Session.OpenAsync();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Assert.Null(window.Session.Error);
            var chart = window.Chart;
            var data = chart.Dataset!;
            Assert.Same(data, window.Session.Dataset);
            var fit = chart.Viewport!;
            var layout = chart.PreparedLayout!;
            var local = new Point(layout.Coordinates.CenterX(data.Bars[0]), layout.Plot.Center.Y);
            var pointer = chart.TranslatePoint(local, window)!.Value;
            window.MouseMove(pointer);
            Assert.Same(data.Bars[0], chart.InspectedBar);
            Assert.Contains("09:30:00.0000000 UTC", chart.InspectionReadout);
            Assert.Contains("O 100.125   H 105.375", chart.InspectionReadout);
            window.MouseWheel(pointer, new(0, 3));
            Assert.True(chart.Viewport!.EndTicks - chart.Viewport.StartTicks < fit.EndTicks - fit.StartTicks);
            var zoomed = chart.Viewport;
            window.MouseDown(pointer, MouseButton.Left);
            window.MouseMove(pointer + new Vector(-50, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(pointer + new Vector(-50, 0), MouseButton.Left);
            Assert.True(chart.Viewport.StartTicks > zoomed.StartTicks);
            window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "FitData")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(fit, chart.Viewport);
            picker.Text = csv.Replace("09:30:00.0000000", "09:29:00.0000000");
            await window.Session.OpenAsync();
            Assert.NotSame(data, chart.Dataset);
            Assert.Null(chart.InspectionReadout);
            Assert.False(chart.IsDragging);
            Assert.Equal(chart.Dataset!.Coverage.StartUtc.Ticks, chart.Viewport!.StartTicks);
        }
        finally { window.Close(); }
        Assert.Null(window.Chart.Dataset);
        Assert.Null(window.Chart.InspectionPosition);
    }

    private sealed class Picker(string csv) : ICsvFilePicker
    {
        public string Text { get; set; } = csv;
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new("invented-navigation.csv",
            () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Text)))));
    }
}
