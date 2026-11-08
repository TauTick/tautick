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

public sealed class WorkspaceTests
{
    // Invented prices/volumes and a missing minute; never downloaded market data.
    private const string Csv = """
        ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
        2025-09-22T09:30:00Z,33,1,42,100,105,98,103,123,ESZ5
        2025-09-22T09:31:00Z,33,1,42,103,104,97,99,456,ESZ5
        2025-09-22T09:33:00Z,33,1,42,100,102,98,100,0,ESZ5
        """;

    [AvaloniaFact]
    public async Task SplitTabFloatAndWholeWorkspaceMovesPreserveDocumentsDatasetsRangesAndSettings()
    {
        var window = new MainWindow(picker: new TextPicker(Csv));
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller;
            var tab = owner.Workspaces[0];
            var first = owner.InitialChart;
            first.ContractYearText = "2025";
            await first.Session.OpenAsync();
            var firstData = first.Dataset;
            var range = new ChartTimeRange(firstData!.Bars[0].StartUtc.Ticks, firstData.Bars[0].EndUtc.Ticks);
            Assert.True(first.LatestChart.TryRestoreTimeRange(range));
            var settings = new Settings();
            first.RegisterSettings(settings);
            var closed = 0;
            first.Closed += () => closed++;
            var second = owner.AddChart(tab, picker: new TextPicker(Csv));
            second.ContractYearText = "2025";
            await second.Session.OpenAsync();
            var secondData = second.Dataset;
            owner.SplitChart(tab, second.Id, false);
            owner.DetachChart(tab, first.Id);
            await Task.Delay(80);
            Assert.Single(tab.Root.Windows!);
            Assert.Same(firstData, first.Dataset);
            Assert.Equal(range, first.VisibleTimeRange);
            owner.RedockChart(tab, first.Id);
            Assert.Empty(tab.Root.Windows!);
            var firstGroup = Assert.IsAssignableFrom<IDock>(tab.Views[first.Id].Owner);
            var secondGroup = Assert.IsAssignableFrom<IDock>(tab.Views[second.Id].Owner);
            tab.Factory.MoveDockable(secondGroup, firstGroup, tab.Views[second.Id], tab.Views[first.Id]);
            Assert.Same(tab.Views[first.Id].Owner, tab.Views[second.Id].Owner);
            owner.DetachWorkspace(tab);
            await Task.Delay(80);
            Assert.Single(owner.Shell.Windows!);
            owner.RedockWorkspace(tab);
            Assert.Empty(owner.Shell.Windows!);
            owner.ActivateChart(first.Id);
            await WorkspaceTestUi.Ready(window);
            Assert.Same(firstData, first.Dataset);
            Assert.Same(secondData, second.Dataset);
            Assert.Equal(range, first.VisibleTimeRange);
            Assert.Equal(range, first.LatestChart.VisibleTimeRange);
            Assert.Same(firstData, first.LatestChart.Dataset);
            Assert.Equal(0, closed);
            Assert.False(settings.Disposed);
            owner.CloseChart(tab, first.Id);
            Assert.Equal(1, closed);
            Assert.True(settings.Disposed);
            Assert.Null(first.Dataset);
            Assert.Same(secondData, second.Dataset);
            Assert.Single(owner.Charts);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ActiveTargetFitAndFailedReplacementAreLocalToTheSelectedChart()
    {
        var picker = new TextPicker(Csv);
        var window = new MainWindow(picker: picker);
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var c = window.Controller;
            var first = c.InitialChart;
            first.ContractYearText = "2025";
            await first.Session.OpenAsync();
            var second = c.AddChart(picker: new TextPicker(Csv));
            second.ContractYearText = "2025";
            await second.Session.OpenAsync();
            var shortRange = new ChartTimeRange(first.Dataset!.Bars[0].StartUtc.Ticks, first.Dataset.Bars[0].EndUtc.Ticks);
            first.SetVisibleTimeRange(shortRange);
            second.SetVisibleTimeRange(shortRange);
            Assert.True(c.ActivateChart(second.Id));
            c.ActiveChart!.FitToData();
            Assert.Equal(shortRange, first.VisibleTimeRange);
            Assert.NotEqual(shortRange, second.VisibleTimeRange);
            var firstData = first.Dataset;
            var secondData = second.Dataset;
            picker.Text = "not a CSV";
            await first.Session.OpenAsync();
            Assert.NotNull(first.Session.Error);
            Assert.Null(second.Session.Error);
            Assert.Same(firstData, first.Dataset);
            Assert.Same(secondData, second.Dataset);
            Assert.Equal(shortRange, first.VisibleTimeRange);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ActualCloseCancelsOnlyThatChartAndRejectsItsLateResultAfterMoves()
    {
        var firstReader = new PendingReader();
        var secondReader = new PendingReader();
        var window = new MainWindow(firstReader, new TextPicker("synthetic ignored input"));
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var c = window.Controller;
            var tab = c.Workspaces[0];
            var first = c.InitialChart;
            var second = c.AddChart(tab, secondReader, new TextPicker("synthetic ignored input"));
            var a = first.Session.OpenAsync();
            var b = second.Session.OpenAsync();
            var tokenA = await firstReader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var tokenB = await secondReader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.DetachChart(tab, first.Id);
            c.RedockChart(tab, first.Id);
            Assert.False(tokenA.IsCancellationRequested);
            c.CloseChart(tab, first.Id);
            Assert.True(tokenA.IsCancellationRequested);
            Assert.False(tokenB.IsCancellationRequested);
            var data = SyntheticBars.Create();
            firstReader.Result.SetResult(new BarReadResult.Success(data));
            secondReader.Result.SetResult(new BarReadResult.Success(data));
            await Task.WhenAll(a, b);
            Assert.Null(first.Dataset);
            Assert.True(first.IsClosed);
            Assert.Same(data, second.Dataset);
            Assert.False(second.IsClosed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task NativeHostCloseClosesItsContentsAndApplicationCloseLeavesNoHosts()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller;
            var tab = owner.Workspaces[0];
            var first = owner.InitialChart;
            var second = owner.AddChart(tab);
            owner.DetachChart(tab, first.Id);
            var floating = Assert.Single(tab.Root.Windows!);
            Assert.IsAssignableFrom<Window>(floating.Host).Close();
            Assert.True(first.IsClosed);
            Assert.False(second.IsClosed);
            Assert.Empty(tab.Root.Windows!);
            owner.DetachWorkspace(tab);
            var workspaceHost = Assert.Single(owner.Shell.Windows!);
            Assert.IsAssignableFrom<Window>(workspaceHost.Host).Close();
            Assert.True(second.IsClosed);
            Assert.Empty(owner.Workspaces);
            Assert.Empty(owner.Shell.Windows!);
            var another = owner.AddWorkspace();
            owner.DetachChart(another, another.Charts.Keys.Single());
            owner.DetachWorkspace(another);
            window.Close();
            Assert.Empty(another.Factory.HostWindows);
            Assert.Empty(owner.ShellFactory.HostWindows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task SavedLayoutRoundTripsOrderSplitsFloatingWorkspacesInputViewportAndFeatureSettings()
    {
        using var temp = new TempInput(Csv);
        var window = new MainWindow(picker: new PathPicker(temp.File));
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var c = window.Controller;
            var tab = c.Workspaces[0];
            c.Rename(tab, "Research");
            var first = c.InitialChart;
            first.ContractYearText = "2025";
            await first.Session.OpenAsync();
            Assert.NotNull(first.Session.InputReference);
            var range = new ChartTimeRange(first.Dataset!.Bars[1].StartUtc.Ticks, first.Dataset.Bars[1].EndUtc.Ticks);
            first.SetVisibleTimeRange(range);
            first.RegisterSettings(new Settings());
            var second = c.AddChart(tab, picker: new PathPicker(temp.File));
            second.ContractYearText = "2025";
            await second.Session.OpenAsync();
            c.SplitChart(tab, second.Id, true);
            ((IDockable)tab.Views[first.Id].Owner!).Proportion = .35;
            ((IDockable)tab.Views[second.Id].Owner!).Proportion = .65;
            var another = c.AddWorkspace("Other");
            c.MoveWorkspace(another, -1);
            c.DetachWorkspace(tab);
            c.ActivateChart(first.Id);
            var saved = c.Capture(new(200, 300, 1000, 700, true));
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await store.SaveAsync(saved);
            var loaded = await store.LoadAsync();
            Assert.Null(loaded.Error);
            Assert.NotNull(loaded.State);
            await c.RestoreAsync(loaded.State!);
            var restored = Assert.IsType<ChartDocument>(c.Charts.Single(x => x.Id == first.Id));
            Assert.Equal(range, restored.VisibleTimeRange);
            Assert.Equal(first.Session.InputReference?.Sha256 ?? saved.Workspaces[0].Charts[0].Input!.Sha256, restored.Session.InputReference!.Sha256);
            Assert.Equal(3, restored.Dataset!.Bars.Count);
            Assert.True(restored.Dataset.Coverage.HasGaps);
            Assert.Equal(0, restored.Dataset.Bars[2].Volume);
            var participant = new Settings();
            restored.RegisterSettings(participant);
            Assert.Equal("violet", participant.Restored!.Value.GetProperty("color").GetString());
            var roundTrip = c.Capture(saved.MainWindow);
            Assert.Equal(JsonSerializer.Serialize(saved.Shell.Root), JsonSerializer.Serialize(roundTrip.Shell.Root));
            Assert.Single(roundTrip.Shell.Windows);
            var restoredTab = roundTrip.Workspaces.Single(x => x.Id == tab.Id);
            Assert.Equal("Research", restoredTab.Name);
            Assert.Contains(Flatten(restoredTab.Layout.Root), x => x.Kind == "split" && x.Vertical);
            Assert.Contains(Flatten(restoredTab.Layout.Root), x => x.Proportion == .35);
            Assert.Equal(first.Id, c.ActiveChart!.Id);
            var liveTab = c.Workspaces.Single(x => x.Id == tab.Id);
            c.RedockWorkspace(liveTab);
            Assert.Empty(c.Shell.Windows!);
            Assert.Empty(c.ShellFactory.HostWindows);
            c.DetachChart(liveTab, first.Id);
            var floatedChartState = c.Capture(null);
            await c.RestoreAsync(floatedChartState);
            liveTab = c.Workspaces.Single(x => x.Id == tab.Id);
            c.RedockChart(liveTab, first.Id);
            Assert.Empty(liveTab.Root.Windows!);
            Assert.Empty(liveTab.Factory.HostWindows);
            Assert.Equal(range, liveTab.Charts[first.Id].VisibleTimeRange);
        }
        finally { window.Close(); }
    }

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

    [AvaloniaFact]
    public async Task InactiveWorkspaceRestoresItsLastActiveFloatingChartAndCommandTarget()
    {
        using var temp = new TempInput(Csv);
        var window = new MainWindow(picker: new PathPicker(temp.File));
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller;
            var workspace = owner.Workspaces[0];
            var first = owner.InitialChart;
            var floating = owner.AddChart(workspace, picker: new PathPicker(temp.File));
            foreach (var chart in new[] { first, floating })
            {
                chart.ContractYearText = "2025";
                await chart.Session.OpenAsync();
            }
            var range = new ChartTimeRange(first.Dataset!.Bars[0].StartUtc.Ticks, first.Dataset.Bars[0].EndUtc.Ticks);
            first.SetVisibleTimeRange(range);
            floating.SetVisibleTimeRange(range);
            owner.ActivateChart(first.Id);
            owner.DetachChart(workspace, floating.Id);
            owner.ActivateChart(floating.Id);
            var other = owner.AddWorkspace();
            var otherChartId = other.Charts.Keys.Single();
            Assert.Equal(floating.Id, workspace.ActiveChartId);
            Assert.Equal(otherChartId, owner.ActiveChart!.Id);
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await store.SaveAsync(owner.Capture(null));
            var loaded = await store.LoadAsync();
            Assert.Null(loaded.Error);
            Assert.Equal(floating.Id, loaded.State!.Workspaces.Single(t => t.Id == workspace.Id).Layout.FocusedView);
            var targetsDuringRestore = new List<Guid?>();
            void TrackTarget() => targetsDuringRestore.Add(owner.ActiveChart?.Id);
            owner.ActiveChartChanged += TrackTarget;
            var restoring = owner.RestoreAsync(loaded.State);
            var restored = owner.Workspaces.Single(t => t.Id == workspace.Id);
            var restoredWindow = Assert.Single(restored.Root.Windows!);
            // Native window activation can arrive after the layout models were rebuilt.
            // Deliver it on a dispatcher turn while restoration is still in progress.
            var activationDelivered = false;
            Dispatcher.UIThread.Post(() =>
            {
                restored.Factory.OnWindowActivated(restoredWindow);
                activationDelivered = true;
            }, DispatcherPriority.Send);
            await restoring;
            Assert.Equal(otherChartId, owner.ActiveChart!.Id);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            owner.ActiveChartChanged -= TrackTarget;
            Assert.True(activationDelivered);
            Assert.DoesNotContain(floating.Id, targetsDuringRestore);
            Assert.Equal(otherChartId, owner.ActiveChart!.Id);
            Assert.Equal(floating.Id, restored.ActiveChartId);
            var workspaceTab = WorkspaceDockFactory.Walk(owner.Shell).OfType<IDocument>().Single(t => t.Id == workspace.Id.ToString());
            owner.ShellFactory.Select(workspaceTab);
            Assert.Equal(floating.Id, owner.ActiveChart!.Id);
            owner.ActiveChart.FitToData();
            Assert.Equal(range, restored.Charts[first.Id].VisibleTimeRange);
            Assert.NotEqual(range, restored.Charts[floating.Id].VisibleTimeRange);
            Assert.Equal(2, restored.Views.Values.Select(v => v.Title).Distinct().Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CloseDuringLayoutPresentationKeepsSavedTargetAndCannotReactivateClosedCharts()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller;
            var workspace = owner.Workspaces[0];
            var chart = owner.InitialChart;
            owner.DetachChart(workspace, chart.Id);
            var saved = owner.Capture(null);
            var restoring = owner.RestoreAsync(saved);
            Assert.False(restoring.IsCompleted);
            Assert.Equal(saved.ActiveChart, owner.Capture(null).ActiveChart);
            var restored = owner.Workspaces.Single();
            var document = restored.Charts[chart.Id];
            window.Close();
            await restoring;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Assert.True(document.IsClosed);
            Assert.Null(owner.ActiveChart);
            Assert.Empty(owner.Charts);
            Assert.Empty(restored.Factory.HostWindows);
            Assert.Empty(owner.ShellFactory.HostWindows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UnsupportedViewIsRecoverableAndCorruptLayoutKeepsPreviousFile()
    {
        var window = new MainWindow();
        window.Show();
        using var temp = new TempInput(Csv);
        try
        {
            var c = window.Controller;
            var state = c.Capture(null);
            state.Workspaces[0].Charts[0] = state.Workspaces[0].Charts[0] with { ViewType = "future-chart" };
            await c.RestoreAsync(state);
            Assert.Empty(c.Charts);
            Assert.Contains("unsupported", c.Notice);
            Assert.NotNull(c.AddChart());
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await File.WriteAllTextAsync(store.Path, "{ broken");
            var loaded = await store.LoadAsync();
            Assert.Null(loaded.State);
            Assert.NotNull(loaded.Error);
            Assert.Equal("{ broken", await File.ReadAllTextAsync(store.Path));
            await store.SaveAsync(c.Capture(null));
            Assert.Equal("{ broken", await File.ReadAllTextAsync(store.Path + ".backup"));
        }
        finally { window.Close(); }
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
    public async Task InvalidContainmentAndDuplicateOwnershipFallBackWithoutConstructingDockObjects()
    {
        var window = new MainWindow();
        using var temp = new TempInput(Csv);
        try
        {
            var state = window.Controller.Capture(null);
            var root = state.Workspaces[0].Layout.Root;
            var group = root.Children[0];
            var view = group.Children[0];
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            foreach (var invalid in new[]
            {
                root with { Children = [view] },
                root with { Children = [group with { Children = [view, view] }] },
                root with { ActiveIndex = 5 },
                root with { Children = [group with { Children = [root] }] }
            })
            {
                state.Workspaces[0] = state.Workspaces[0] with { Layout = state.Workspaces[0].Layout with { Root = invalid } };
                await File.WriteAllTextAsync(store.Path, JsonSerializer.Serialize(state));
                var loaded = await store.LoadAsync();
                Assert.Null(loaded.State);
                Assert.NotNull(loaded.Error);
            }
        }
        finally { window.Controller.Close(); }
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
