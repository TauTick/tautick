using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TauTick.Desktop.Tests;

/// <summary>Invented CSV values through production composition, parser, session and chart.</summary>
public sealed class ProductionCsvTests
{
    private const string Csv = """
        ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
        2025-09-22T09:30:00Z,33,1,42,100,105,98,103,123,ESZ5
        2025-09-22T09:31:00Z,33,1,42,103,104,97,99,456,ESZ5
        2025-09-22T09:33:00Z,33,1,42,100,102,98,100,0,ESZ5
        """;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [AvaloniaFact]
    public async Task ProductionLoadPreservesValuesGapsAndRenderedPositionsThenFitsAndResizes()
    {
        var window = new MainWindow(picker: new Picker(Csv));
        window.Show();
        try
        {
            Assert.False(Find<Button>(window, "OpenCsv").IsEnabled);
            foreach (var invalid in new[] { "25", "0000", "abcd", "2.25" })
            {
                Find<TextBox>(window, "ContractYear").Text = invalid;
                Assert.False(Find<Button>(window, "OpenCsv").IsEnabled);
            }
            Find<TextBox>(window, "ContractYear").Text = "2025";
            Assert.True(Find<Button>(window, "OpenCsv").IsEnabled);
            await window.Session.OpenAsync();
            window.UpdateLayout();
            var data = Assert.IsType<TauTick.MarketData.BarDataset>(window.Chart.Dataset);
            Assert.Same(data, window.Session.Dataset);
            Assert.Equal("Databento", data.Source.Provider);
            Assert.Equal("ESZ5", data.Instrument.Symbol);
            Assert.Equal(new DateOnly(2025, 12, 1), data.Instrument.ContractMonth);
            Assert.Equal(3, data.Bars.Count);
            Assert.Equal(123, data.Bars[0].Volume);
            Assert.Equal((100m, 105m, 98m, 103m), (data.Bars[0].Open, data.Bars[0].High, data.Bars[0].Low, data.Bars[0].Close));
            Assert.True(data.Coverage.HasGaps);
            Assert.Equal(DateTimeOffset.Parse("2025-09-22T09:34:00Z"), data.Coverage.EndUtc);
            Assert.Equal(DatasetSummary.Format(data), Find<TextBlock>(window, "DatasetSummary").Text);
            var layout = window.Chart.PreparedLayout!;
            Assert.Equal(3, layout.Candles.Length);
            // Four minute slots, with one missing slot before the third candle.
            Assert.Equal(layout.Plot.Left + layout.Plot.Width / 8, layout.Candles[0].X, 6);
            Assert.Equal(layout.Plot.Left + layout.Plot.Width * 7 / 8, layout.Candles[2].X, 6);
            Assert.Equal(layout.Plot.Top + 0.08 * layout.Plot.Height, layout.Candles[0].HighY, 6);
            Assert.Equal(layout.Plot.Top + 0.92 * layout.Plot.Height, layout.Candles[1].LowY, 6);
            Assert.Equal(new[] { 1, -1, 0 }, layout.Candles.Select(c => c.Direction));
            window.Chart.SetVisibleTimeRange(data.Bars[1].StartUtc, data.Bars[1].EndUtc);
            Assert.Single(window.Chart.PreparedLayout!.Candles);
            Find<Button>(window, "FitData").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(3, window.Chart.PreparedLayout!.Candles.Length);
            // Headless control layout check; actual native window resizing is checked separately.
            window.Chart.Measure(new Avalonia.Size(600, 300));
            window.Chart.Arrange(new Avalonia.Rect(0, 0, 600, 300));
            Assert.NotEqual(layout.Plot.Width, window.Chart.PreparedLayout!.Plot.Width);
            Assert.Same(data, window.Chart.Dataset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task InvalidSchemaRetainsChartMetadataAndViewportAndNextFileReplacesThem()
    {
        var picker = new Picker(Csv);
        var window = new MainWindow(picker: picker);
        window.Show();
        try
        {
            Find<TextBox>(window, "ContractYear").Text = "2025";
            await window.Session.OpenAsync();
            var accepted = window.Chart.Dataset!;
            var metadata = Find<TextBlock>(window, "DatasetSummary").Text;
            window.Chart.SetVisibleTimeRange(accepted.Bars[0].StartUtc, accepted.Bars[0].EndUtc);
            var viewport = window.Chart.Viewport;
            picker.Text = Csv.Replace(",33,", ",32,");
            await window.Session.OpenAsync();
            Assert.Same(accepted, window.Chart.Dataset);
            Assert.Same(viewport, window.Chart.Viewport);
            Assert.Equal(metadata, Find<TextBlock>(window, "DatasetSummary").Text);
            Assert.Contains("rtype", window.Session.Error);
            Assert.Contains("row 2", window.Session.Error);
            Assert.True(Find<Button>(window, "OpenCsv").IsEnabled);
            // A changed year is explicitly applied to the next read, never to retained metadata.
            Find<TextBox>(window, "ContractYear").Text = "2026";
            Assert.Equal(metadata, Find<TextBlock>(window, "DatasetSummary").Text);
            picker.Text = Csv.Replace("ESZ5", "NQH6").Replace("2025-09-22", "2026-01-02");
            await window.Session.OpenAsync();
            Assert.Null(window.Session.Error);
            Assert.Equal("NQH6", window.Chart.Dataset!.Instrument.Symbol);
            Assert.Equal(new DateOnly(2026, 3, 1), window.Chart.Dataset.Instrument.ContractMonth);
            Assert.Equal(window.Chart.Dataset.Coverage.StartUtc.Ticks, window.Chart.Viewport!.StartTicks);
            Assert.Equal(DatasetSummary.Format(window.Chart.Dataset), Find<TextBlock>(window, "DatasetSummary").Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingDuringRealParserReadCancelsPendingIoAndReleasesOwnedResources()
    {
        var input = new PendingStream();
        var released = false;
        var picker = new Picker(Csv)
        {
            Open = () => Task.FromResult<Stream>(input), Release = () => released = true
        };
        var window = new MainWindow(picker: picker);
        window.Show();
        Find<TextBox>(window, "ContractYear").Text = "2025";
        var attempt = window.Session.OpenAsync();
        try
        {
            await input.Entered.Task.WaitAsync(Timeout);
            Assert.True(window.Session.IsBusy);
            Assert.False(Find<Button>(window, "OpenCsv").IsEnabled);
            Assert.False(Find<TextBox>(window, "ContractYear").IsEnabled);
            var uiRan = false;
            await Dispatcher.UIThread.InvokeAsync(() => uiRan = true);
            Assert.True(uiRan);
            await window.Session.OpenAsync();
            Assert.Equal(1, picker.Calls);
        }
        finally { window.Close(); }
        await attempt.WaitAsync(Timeout);
        Assert.True(input.Canceled);
        Assert.True(input.Disposed);
        Assert.True(released);
        Assert.Null(window.Chart.Dataset);
        Assert.Null(window.Session.Dataset);
        Assert.True(window.Session.IsClosed);
    }

    private static T Find<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private sealed class Picker(string csv) : ICsvFilePicker
    {
        public string Text { get; set; } = csv;
        public Func<Task<Stream>>? Open { get; init; }
        public Action? Release { get; init; }
        public int Calls { get; private set; }
        public Task<CsvFile?> PickAsync()
        {
            Calls++;
            return Task.FromResult<CsvFile?>(new("invented.csv",
                Open ?? (() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Text)))), Release));
        }
    }

    private sealed class PendingStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Canceled { get; private set; }
        public bool Disposed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            try { await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { Canceled = true; throw; }
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
