using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using TauTick.Adapters.Databento;
using TauTick.Charting;
using TauTick.Charting.Studies;
using TauTick.Desktop.Studies;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;
using TauTick.Studies;
using TauTick.TestStudies;

namespace TauTick.Desktop.Tests;

public sealed class IndicatorTests
{
    internal const string Csv = """
        ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol
        2025-09-22T09:30:00Z,33,1,42,0,0,0,0,0,ESZ5
        2025-09-22T09:31:00Z,33,1,42,-3,0,-6,-3,2,ESZ5
        2025-09-22T09:33:00Z,33,1,42,3,9,0,9,4,ESZ5
        2025-09-22T09:34:00Z,33,1,42,10,10,10,10,0,ESZ5
        """;
    private static async Task<BarDataset> Parse(string csv = Csv)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return Assert.IsType<BarReadResult.Success>(await new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic.csv", default)).Dataset;
    }
    [Fact]
    public async Task IndependentExamplesCoverAnchorsConventionsZerosGapsAndNegativePrices()
    {
        var data = await Parse();
        Assert.Equal(new decimal?[] { null, -3, 3, 3 }, VwapDefinition.Calculate(data, new(), TestContext.Current.CancellationToken).Values);
        Assert.Equal(new decimal?[] { null, -3, 5, 5 }, VwapDefinition.Calculate(data, new(VwapPrice.Close), TestContext.Current.CancellationToken).Values);
        Assert.Equal(new decimal?[] { null, null, 6, 6 }, VwapDefinition.Calculate(data, new(AnchorUtcTicks: data.Bars[2].StartUtc.Ticks), TestContext.Current.CancellationToken).Values);
        Assert.All(VwapDefinition.Calculate(data, new(AnchorUtcTicks: data.Bars[3].StartUtc.Ticks), TestContext.Current.CancellationToken).Values, Assert.Null);
        Assert.Throws<ArgumentException>(() => VwapDefinition.Calculate(data, new(AnchorUtcTicks: data.Bars[1].EndUtc.Ticks), TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => VwapDefinition.Calculate(data, new(), cancellation.Token));
        var extreme = new BarDataset(data.Instrument, data.Source, [new(data.Bars[0].StartUtc, TimeSpan.FromMinutes(1), decimal.MaxValue, decimal.MaxValue, decimal.MaxValue, decimal.MaxValue, 2)]);
        Assert.Throws<OverflowException>(() => VwapDefinition.Calculate(extreme, new(VwapPrice.Close), TestContext.Current.CancellationToken));
    }
    [AvaloniaFact]
    public async Task ManagerTwoInstancesApplyAppearanceHideOrderRemoveAndReopen()
    {
        var window = new MainWindow(picker: new Picker()); window.Show();
        await WorkspaceTestUi.Ready(window);
        try
        {
            var document = (ChartDocument)window.ChartHost.ActiveChart!;
            document.ContractYearText = "2025";
            await document.Session.OpenAsync();
            var manager = new IndicatorManager(document); manager.Show(window);
            Find<Button>(manager, "AddIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<Button>(manager, "AddIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await document.Studies.Pending;
            Assert.Equal(2, document.Studies.Instances.Count);
            var first = document.Studies.Instances[0]; var second = document.Studies.Instances[1];
            manager.Select(second);
            Find<ComboBox>(manager, "VwapPrice").SelectedItem = VwapPrice.Close;
            Find<TextBox>(manager, "VwapColor").Text = "#FF0000";
            Find<Button>(manager, "ApplyIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await document.Studies.Pending;
            Assert.Equal(5, ((VwapResult)second.Result!).Values[2]);
            Assert.Equal(3, ((VwapResult)first.Result!).Values[2]);
            var result = second.Result;
            Find<TextBox>(manager, "VwapColor").Text = "#00FF00";
            Find<Button>(manager, "ApplyIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(result, second.Result);
            Find<Button>(manager, "MoveIndicatorUp").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(second, document.Studies.Instances[0]);
            Find<Button>(manager, "ToggleIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Single(document.Studies.Visuals);
            Find<Button>(manager, "RemoveIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            manager.Close();
            var reopened = new IndicatorManager(document); reopened.Show(window);
            Assert.Single(Find<ListBox>(reopened, "AddedIndicators").Items);
            reopened.Close();
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task ReplacementFailureRetentionPersistenceAndAnchorBinding()
    {
        var picker = new Picker(); var document = new ChartDocument(Guid.NewGuid(), picker: picker) { ContractYearText = "2025" };
        var studies = document.Studies;
        await document.Session.OpenAsync();
        var instance = studies.Add(VwapDefinition.Id); await studies.Pending;
        var settings = JsonSerializer.SerializeToElement(new VwapSettings(AnchorUtcTicks: document.Dataset!.Bars[1].StartUtc.Ticks));
        studies.Apply(instance, settings, new(), true); await studies.Pending;
        var saved = studies.Capture(); var result = instance.Result;
        picker.Text = "broken"; await document.Session.OpenAsync();
        Assert.Same(result, instance.Result);
        picker.Text = Csv.Replace(",9,4,", ",6,4,"); await document.Session.OpenAsync(); await studies.Pending;
        Assert.Equal(StudyStatus.Error, instance.Status); Assert.Null(instance.Result);
        Assert.Contains("different", instance.Message);
        picker.Text = Csv; await document.Session.OpenAsync(); await studies.Pending;
        Assert.Equal(StudyStatus.Ready, instance.Status);
        studies.Restore(saved); await studies.Pending;
        Assert.Equal(instance.Saved.Descriptor.Id, studies.Instances[0].Saved.Descriptor.Id);
        Assert.Equal(3, ((VwapResult)studies.Instances[0].Result!).Values[2]);
        document.Close(); await studies.Pending;
        Assert.All(studies.Instances, i => Assert.Equal(StudyStatus.Disposed, i.Status));
    }
    [AvaloniaFact]
    public async Task CustomStudyUsesReservedRegionGeometryTextAndInputThroughSameHost()
    {
        var document = new ChartDocument(Guid.NewGuid(), picker: new Picker()) { ContractYearText = "2025" };
        await document.Session.OpenAsync();
        var definition = new SyntheticLevels(); SyntheticRenderer? renderer = null;
        using var studies = document.Studies;
        studies.Register(new(definition, (_, result, _) => renderer = new((Levels)result)));
        var instance = studies.Add(definition.TypeId); await studies.Pending;
        var chart = new BarChart(); chart.SetDataset(document.Dataset!); chart.SetStudies(studies.Visuals);
        chart.Measure(new(900, 500)); chart.Arrange(new(0, 0, 900, 500));
        using var bitmap = new RenderTargetBitmap(new(900, 500), new(96, 96)); bitmap.Render(chart);
        Assert.NotNull(renderer!.Last); Assert.Equal(80, renderer.Last.ReservedRegion.Height);
        Assert.True(renderer.Last.ReservedRegion.Top > renderer.Last.Plot.Bottom);
        Assert.Equal("Synthetic level 7", renderer.HitTest(renderer.Last.ReservedRegion.Center, renderer.Last));
        Assert.True(renderer.PointerPressed(renderer.Last.ReservedRegion.Center, renderer.Last));
        var result = instance.Result;
        chart.FitToData(); Assert.Same(result, instance.Result);
        chart.ClearDataset(); Assert.Empty(chart.Studies);
        studies.Remove(instance); Assert.True(renderer.Disposed);
        document.Close();
    }
    [AvaloniaFact]
    public async Task DockingAndWorkspaceRoundTripKeepStudiesAndVerifiedInput()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tau9-synthetic-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, Csv, TestContext.Current.CancellationToken);
        var picker = new LocalPicker(path);
        var window = new MainWindow(picker: picker); window.Show();
        await WorkspaceTestUi.Ready(window);
        try
        {
            var controller = window.Controller; var tab = controller.Workspaces[0]; var first = controller.InitialChart;
            first.ContractYearText = "2025"; await first.Session.OpenAsync();
            var study = first.Studies.Add(VwapDefinition.Id); await first.Studies.Pending;
            first.Studies.Apply(study, JsonSerializer.SerializeToElement(new VwapSettings(AnchorUtcTicks: first.Dataset!.Bars[1].StartUtc.Ticks)), new("#FF0000"), true);
            await first.Studies.Pending;
            var result = study.Result;
            var second = controller.AddChart(tab, picker: picker);
            second.ContractYearText = "2025"; await second.Session.OpenAsync();
            Assert.Empty(second.Studies.Instances);
            controller.SplitChart(tab, second.Id, false); controller.DetachChart(tab, first.Id);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            controller.RedockChart(tab, first.Id); controller.DetachWorkspace(tab);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            controller.RedockWorkspace(tab); controller.ActivateChart(first.Id);
            Assert.Same(result, study.Result);
            var saved = controller.Capture(null);
            await controller.RestoreAsync(saved);
            var restored = (ChartDocument)controller.Charts.Single(c => c.Id == first.Id);
            await restored.Studies.Pending;
            var instance = Assert.Single(restored.Studies.Instances);
            Assert.Equal(study.Saved.Descriptor.Id, instance.Saved.Descriptor.Id);
            Assert.Equal(study.Saved.Appearance, instance.Saved.Appearance);
            Assert.Equal(3, ((VwapResult)instance.Result!).Values[2]);
        }
        finally { window.Close(); File.Delete(path); }
    }
    [AvaloniaFact]
    public async Task UnknownSettingsAndUnsupportedInputsAreRetainedAndNeverCalculated()
    {
        var document = new ChartDocument(Guid.NewGuid(), picker: new Picker()) { ContractYearText = "2025" };
        await document.Session.OpenAsync();
        using var studies = new ChartStudies(document, [new(new NeedsTrades(), (_, _, _) => throw new Exception("must not draw"))]);
        var trades = studies.Add("test.trades");
        Assert.Equal(StudyStatus.Unsupported, trades.Status);
        var unknown = new SavedStudy(new(Guid.NewGuid(), "future.study", 17, JsonSerializer.SerializeToElement(new { future = true })), new());
        studies.Restore(new(1, JsonSerializer.SerializeToElement(new[] { unknown })));
        Assert.Equal(StudyStatus.Unsupported, studies.Instances[0].Status);
        Assert.Equal(unknown.Descriptor.Settings.GetRawText(), studies.Capture().Value[0].GetProperty("Descriptor").GetProperty("Settings").GetRawText());
        document.Close();
    }
    [AvaloniaFact]
    public async Task SupersededAndClosedWorkCannotPublishEvenIfCalculationIgnoresCancellation()
    {
        var document = new ChartDocument(Guid.NewGuid(), picker: new Picker()) { ContractYearText = "2025" };
        await document.Session.OpenAsync();
        using var gate = new ManualResetEventSlim();
        var definition = new SlowStudy(gate);
        using var studies = new ChartStudies(document, [new(definition, (_, _, _) => new SyntheticRenderer(new(7, "synthetic")))]);
        var instance = studies.Add(definition.TypeId);
        await definition.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        studies.Remove(instance); gate.Set(); await studies.Pending;
        Assert.Null(instance.Result); Assert.Empty(studies.Visuals);
        var closed = studies.Add(definition.TypeId); studies.Dispose(); await studies.Pending;
        Assert.Null(closed.Result); Assert.Equal(StudyStatus.Disposed, closed.Status);
        document.Close();
    }
    private sealed class LocalPicker(string path) : ICsvFilePicker
    {
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new(Path.GetFileName(path), () => Task.FromResult<Stream>(File.OpenRead(path)), localPath: path));
    }
    private sealed class NeedsTrades : IStudyDefinition
    {
        public string TypeId => "test.trades";
        public string Name => "Trade requirement probe";
        public StudyInput RequiredInput => StudyInput.Trades;
        public JsonElement DefaultSettings => JsonSerializer.SerializeToElement(new { });
        public void Validate(int version, JsonElement settings) { }
        public object Calculate(BarDataset dataset, JsonElement settings, CancellationToken token) => throw new Exception("must not calculate");
    }
    private sealed class SlowStudy(ManualResetEventSlim gate) : IStudyDefinition
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string TypeId => "test.slow";
        public string Name => "Synthetic delayed result";
        public StudyInput RequiredInput => StudyInput.Bars;
        public JsonElement DefaultSettings => JsonSerializer.SerializeToElement(new { });
        public void Validate(int version, JsonElement settings) { }
        public object Calculate(BarDataset dataset, JsonElement settings, CancellationToken token)
        { Started.TrySetResult(); gate.Wait(TimeSpan.FromSeconds(5)); return new object(); }
    }
    [AvaloniaFact]
    public async Task ManagerAppearanceEditCannotRebindAnchorAfterDatasetReplacement()
    {
        var picker = new Picker();
        var document = new ChartDocument(Guid.NewGuid(), picker: picker) { ContractYearText = "2025" };
        var manager = new IndicatorManager(document);
        manager.Show();
        try
        {
            await document.Session.OpenAsync();
            var studies = document.Studies;
            var instance = studies.Add(VwapDefinition.Id);
            await studies.Pending;
            studies.Apply(instance, JsonSerializer.SerializeToElement(new VwapSettings(VwapPrice.Close,
                document.Dataset!.Bars[2].StartUtc.Ticks)), new(), true);
            await studies.Pending;
            var originalBinding = instance.Saved.AnchorInput;
            manager.Select(instance);
            picker.Text = Csv.Replace(",9,4,", ",6,4,");
            await document.Session.OpenAsync();
            await studies.Pending;
            Assert.Equal(StudyStatus.Error, instance.Status);
            Find<TextBox>(manager, "VwapColor").Text = "#FF0000";
            Find<Button>(manager, "ApplyIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("#FF0000", instance.Saved.Appearance.Color);
            Assert.Equal(originalBinding, instance.Saved.AnchorInput);
            Assert.Equal(StudyStatus.Error, instance.Status);
            Assert.Null(instance.Result);
            Assert.Empty(studies.Visuals);
            Find<CheckBox>(manager, "RebindVwapAnchor").IsChecked = true;
            Find<Button>(manager, "ApplyIndicator").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await studies.Pending;
            Assert.NotEqual(originalBinding, instance.Saved.AnchorInput);
            Assert.Equal(StudyStatus.Ready, instance.Status);
            Assert.Equal(6, Assert.IsType<VwapResult>(instance.Result).Values[2]);
            Assert.False(Find<CheckBox>(manager, "RebindVwapAnchor").IsChecked);
        }
        finally { manager.Close(); document.Close(); }
    }

    [AvaloniaFact]
    public async Task AppearanceAfterIndentedRestoreReusesResultsDespitePropertyOrder()
    {
        var document = new ChartDocument(Guid.NewGuid(), picker: new Picker()) { ContractYearText = "2025" };
        try
        {
            await document.Session.OpenAsync();
            var studies = document.Studies;
            studies.Add(VwapDefinition.Id);
            await studies.Pending;
            var json = JsonSerializer.Serialize(studies.Capture(), new JsonSerializerOptions { WriteIndented = true });
            studies.Restore(JsonSerializer.Deserialize<SavedChartSettings>(json)!);
            await studies.Pending;
            var instance = Assert.Single(studies.Instances);
            var result = Assert.IsType<VwapResult>(instance.Result);
            var pending = studies.Pending;
            // Same calculation values, with different whitespace and object-property order.
            using var reordered = JsonDocument.Parse("{\"AnchorUtcTicks\":null,\"Price\":0}");
            studies.Apply(instance, reordered.RootElement, new("#FF0000", 3));
            Assert.Same(result, instance.Result);
            Assert.Same(pending, studies.Pending);
            Assert.Equal(StudyStatus.Ready, instance.Status);
            Assert.Single(studies.Visuals);
            studies.Apply(instance, JsonSerializer.SerializeToElement(new VwapSettings(VwapPrice.Close)), new("#FF0000", 3));
            await studies.Pending;
            Assert.NotSame(result, instance.Result);
            Assert.Equal(5, Assert.IsType<VwapResult>(instance.Result).Values[2]);
        }
        finally { document.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("future.study")]
    [InlineData(VwapDefinition.Id)]
    public async Task MissingCalculationSettingsRetainRawPayloadWithoutBlockingWorkspaceSave(string typeId)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tau9-settings-{Guid.NewGuid():N}");
        var owner = new WorkspaceController();
        try
        {
            var document = owner.InitialChart;
            using var raw = JsonDocument.Parse($$$"""
                [{"Descriptor":{"Id":"b60bbf88-1477-4b19-bc85-f87aaf7ae985","TypeId":"{{{typeId}}}","Version":1},"Appearance":{}}]
                """);
            document.PrepareRestore(document.Capture() with
            {
                Settings = new() { ["indicators"] = new(1, raw.RootElement) }
            });
            Assert.Empty(document.Studies.Instances);
            Assert.Contains("could not be applied", document.Notice);
            var store = new WorkspaceStore(Path.Combine(directory, "workspace.json"));
            await store.SaveAsync(owner.Capture(null));
            var (restored, error) = await store.LoadAsync();
            Assert.Null(error);
            var settings = Assert.Single(Assert.Single(restored!.Workspaces).Charts).Settings["indicators"];
            Assert.True(JsonElement.DeepEquals(raw.RootElement, settings.Value));
            // Rejection also leaves an existing collection intact rather than partially replacing it.
            var existing = document.Studies.Add(VwapDefinition.Id);
            Assert.Throws<ArgumentException>(() => document.Studies.Restore(new(1, raw.RootElement)));
            Assert.Same(existing, Assert.Single(document.Studies.Instances));
        }
        finally
        {
            owner.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static T Find<T>(Control parent, string name) where T : Control => parent.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private sealed class Picker : ICsvFilePicker
    {
        public string Text = Csv;
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new("synthetic.csv", () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Text)))));
    }
}
