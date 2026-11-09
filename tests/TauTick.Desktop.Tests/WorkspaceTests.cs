using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using TauTick.Charting;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Desktop.Tests;

public sealed partial class WorkspaceTests
{
    // Invented prices/volumes and a missing minute; never downloaded market data.
    private const string Csv = """
        ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
        2025-09-22T09:30:00Z,33,1,42,100,105,98,103,123,ESZ5
        2025-09-22T09:31:00Z,33,1,42,103,104,97,99,456,ESZ5
        2025-09-22T09:33:00Z,33,1,42,100,102,98,100,0,ESZ5
        """;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrChangedCsvDoesNotSilentlyLoadAndExplicitReplacementUsesNewYear(bool missing)
    {
        using var temp = new TempInput(Csv);
        var document = new ChartDocument(Guid.NewGuid(), picker: new PathPicker(temp.File));
        document.ContractYearText = "2025";
        await document.Session.OpenAsync();
        var saved = document.Capture();
        document.Close();
        if (missing) File.Delete(temp.File); else await File.WriteAllTextAsync(temp.File, Csv.Replace("100,105", "100,106"));
        var restored = new ChartDocument(saved.Id, picker: new PathPicker(temp.File));
        try
        {
            await restored.RestoreAsync(saved);
            Assert.Null(restored.Dataset);
            Assert.NotNull(restored.Session.Error);
            Assert.Equal(saved.Input, restored.Capture().Input);
            Assert.Contains(missing ? "no longer exists" : "has changed", restored.Session.Error);
            await File.WriteAllTextAsync(temp.File, Csv.Replace("ESZ5", "ESH6"));
            restored.ContractYearText = "2026";
            await restored.Session.OpenAsync();
            Assert.Null(restored.Session.Error);
            Assert.Equal(new DateOnly(2026, 3, 1), restored.Dataset!.Instrument.ContractMonth);
        }
        finally { restored.Close(); }
    }

    [Fact]
    public void MissingMonitorAndScalingKeepWholeWindowReachable()
    {
        var screens = new[] { new ScreenArea(-1920, 0, 1920, 1080, 1), new ScreenArea(0, 0, 2560, 1440, 2) };
        var lost = new WindowGeometry(8000, -4000, 5000, 3000, true).Reachable(screens);
        Assert.Equal(-1920, lost.X);
        Assert.Equal(0, lost.Y);
        Assert.Equal(1920, lost.Width);
        Assert.Equal(1080, lost.Height);
        Assert.True(lost.Maximized);
        var scaled = new WindowGeometry(2500, 1300, 1000, 600, false).Reachable(screens);
        Assert.InRange(scaled.X + scaled.Width * 2, 0, 2560);
        Assert.InRange(scaled.Y + scaled.Height * 2, 0, 1440);
    }

    [AvaloniaFact]
    public async Task UnappliedFeatureSettingsAndNotYetLoadedInputsRemainSaveable()
    {
        using var temp = new TempInput(Csv);
        var source = new ChartDocument(Guid.NewGuid(), picker: new PathPicker(temp.File));
        source.ContractYearText = "2025";
        await source.Session.OpenAsync();
        source.RegisterSettings(new Settings());
        var saved = source.Capture();
        source.Close();
        var restored = new ChartDocument(saved.Id);
        try
        {
            restored.PrepareRestore(saved);
            Assert.Null(restored.Dataset);
            Assert.Equal(saved.Input, restored.Capture().Input);
            Assert.Equal(saved.InputContractYear, restored.Capture().InputContractYear);
            restored.RegisterSettings(new RejectingSettings());
            Assert.Contains("could not be applied", restored.Notice);
            Assert.Equal("violet", restored.Capture().Settings["synthetic.test-feature"].Value.GetProperty("color").GetString());
        }
        finally { restored.Close(); }
    }

    private sealed class RejectingSettings : IChartSettingsParticipant
    {
        public string Key => "synthetic.test-feature";
        public SavedChartSettings Capture() => new(1, JsonSerializer.SerializeToElement(new { color = "default" }));
        public void Restore(SavedChartSettings settings) => throw new NotSupportedException();
        public void Dispose() { }
    }

    private static IEnumerable<LayoutNode> Flatten(LayoutNode node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
    private sealed class TextPicker(string text) : ICsvFilePicker
    {
        public string Text { get; set; } = text;
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new("synthetic.csv", () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Text)))));
    }
    private sealed class PathPicker(string path) : ICsvFilePicker
    {
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new(Path.GetFileName(path),
            () => Task.FromResult<Stream>(File.OpenRead(path)), localPath: path));
    }
    private sealed class PendingReader : IHistoricalBarReader
    {
        public TaskCompletionSource<CancellationToken> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<BarReadResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<BarReadResult> ReadAsync(Stream input, string name, CancellationToken token) { Entered.TrySetResult(token); return Result.Task; }
    }
    private sealed class Settings : IChartSettingsParticipant
    {
        public string Key => "synthetic.test-feature";
        public bool Disposed { get; private set; }
        public SavedChartSettings? Restored { get; private set; }
        public SavedChartSettings Capture() => new(1, JsonSerializer.SerializeToElement(new { color = "violet" }));
        public void Restore(SavedChartSettings settings) => Restored = settings;
        public void Dispose() => Disposed = true;
    }
    private sealed class TempInput : IDisposable
    {
        public TempInput(string csv)
        {
            Directory = Path.Combine(Path.GetTempPath(), "tautick-synthetic-" + Guid.NewGuid());
            System.IO.Directory.CreateDirectory(Directory);
            File = Path.Combine(Directory, "synthetic.csv");
            System.IO.File.WriteAllText(File, csv);
        }
        public string Directory { get; }
        public string File { get; }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
