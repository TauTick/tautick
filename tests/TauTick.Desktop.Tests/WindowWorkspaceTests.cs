using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Dock.Model.Core;
using TauTick.Charting;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;
using TauTick.SyntheticData;
using TauTick.UI;
using Avalonia.VisualTree;

namespace TauTick.Desktop.Tests;

public sealed partial class WorkspaceTests
{
    [AvaloniaFact]
    public async Task NamedSaveAndLoadFromReceivingWindowReplaceOtherHostsAndKeepNamedSnapshotOnClose()
    {
        using var temp = new TempInput(Csv);
        var store = new WorkspaceStore(Path.Combine(temp.Directory, "active.json"));
        var window = new MainWindow(store: store); window.Show();
        var owner = window.Controller;
        try
        {
            await WorkspaceTestUi.Ready(window);
            for (var i = 0; owner.PersistenceBusy && i < 100; i++) await Task.Delay(10);
            window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "LayoutName").Text = "First";
            Assert.True(await window.SaveAsync());
            var detached = owner.DetachTab(owner.SelectedTab!);
            var receiver = detached.Host!;
            owner.AddTab(detached, "Empty");
            receiver.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "LayoutName").Text = "Second";
            Assert.True(await receiver.SaveAsync());
            var firstGeneration = owner.Charts.Single();
            Assert.True(await receiver.LoadAsync("First"));
            Assert.True(firstGeneration.IsClosed); Assert.False(window.IsVisible);
            Assert.Single(owner.Windows); Assert.Single(owner.Tabs); Assert.Same(receiver, owner.Windows[0].Host);
            Assert.True(await receiver.LoadAsync("Second"));
            Assert.Equal(2, owner.Windows.Count); Assert.Equal(3, owner.Tabs.Count());
            var nameCount = store.Names().Count;
            Assert.True(await receiver.SaveAsync(saveNamed: false));
            Assert.Equal(nameCount, store.Names().Count);
            Assert.Equal(2, (await store.Named("Second").LoadAsync()).State!.Windows.Count);
        }
        finally { owner.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClickingRestoredInactiveTabSelectsAndPresentsItsChart()
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; var populated = owner.SelectedTab!;
            owner.AddTab(window.Model, "Empty");
            await owner.RestoreAsync(owner.Capture());
            window.UpdateLayout();
            var button = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == populated.Id.ToString());
            var point = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal(populated.Id, window.Model.SelectedTabId);
            await WorkspaceTestUi.Ready(window);
            Assert.NotNull(owner.ActiveChart);
        }
        finally { window.Controller.Close(); window.Close(); }
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
    public void EmptyTabCreationAndCrossWindowInsertionPreserveSiblingOrder()
    {
        var owner = new WorkspaceController();
        try
        {
            var first = owner.Windows[0]; var a = first.Tabs[0]; owner.Rename(a, "A");
            var b = owner.AddTab(first, "B"); var c = owner.AddTab(first, "C");
            var second = owner.DetachTab(c); var d = owner.AddTab(second, "D");
            Assert.Empty(d.Charts); Assert.Equal(d.Id, second.SelectedTabId); Assert.Null(owner.ActiveChart);
            owner.MoveTab(b, second, 1);
            Assert.Equal(["A"], first.Tabs.Select(t => t.Name));
            Assert.Equal(["C", "B", "D"], second.Tabs.Select(t => t.Name));
            owner.MoveTab(d, second, 0);
            Assert.Equal(["D", "C", "B"], second.Tabs.Select(t => t.Name));
            owner.MoveTab(b, first, first.Tabs.Count);
            Assert.Equal(["A", "B"], first.Tabs.Select(t => t.Name));
            owner.MoveTab(b, second, second.Tabs.Count);
            owner.MoveTab(c, first, 0);
            Assert.Equal(["C", "A"], first.Tabs.Select(t => t.Name));
            Assert.Equal(["D", "B"], second.Tabs.Select(t => t.Name));
            Assert.Equal(4, owner.Tabs.Select(t => t.Id).Distinct().Count());
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task MovingPopulatedSplitTabPreservesIdentityDatasetViewportSettingsAndReceivingWindow()
    {
        var window = new MainWindow(picker: new TextPicker(Csv)); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; var tab = owner.SelectedTab!; var first = owner.InitialChart;
            first.ContractYearText = "2025"; await first.Session.OpenAsync();
            var data = first.Dataset!;
            var range = new ChartTimeRange(data.Bars[0].StartUtc.Ticks, data.Bars[0].EndUtc.Ticks);
            first.SetVisibleTimeRange(range);
            var settings = new Settings(); first.RegisterSettings(settings);
            var closed = 0; first.Closed += () => closed++;
            var second = owner.AddChart(tab); owner.SplitChart(tab, second.Id, false);
            ((IDockable)tab.Views[first.Id].Owner!).Proportion = .35;
            ((IDockable)tab.Views[second.Id].Owner!).Proportion = .65;
            owner.ActivateChart(first.Id);
            var layout = JsonSerializer.Serialize(tab.Factory.Capture(tab.Root).Root);
            var detached = owner.DetachTab(tab);
            await WorkspaceTestUi.Ready(detached.Host!);
            Assert.Same(data, first.Dataset); Assert.Equal(range, first.VisibleTimeRange);
            Assert.Equal("2025", first.ContractYearText); Assert.Equal(0, closed); Assert.False(settings.Disposed);
            Assert.Same(detached.Host, first.WindowOwner!());
            Assert.Same(first, owner.Charts.Single(c => c.Id == first.Id));
            Assert.Equal(layout, JsonSerializer.Serialize(tab.Factory.Capture(tab.Root).Root));
            Assert.Same(first, owner.ActiveChart);
            owner.MoveTab(tab, window.Model, 0);
            await WorkspaceTestUi.Ready(window);
            Assert.Same(window, first.WindowOwner!());
            Assert.Same(data, first.LatestChart.Dataset); Assert.Equal(range, first.LatestChart.VisibleTimeRange);
            Assert.Equal(0, closed); Assert.False(settings.Disposed);
            owner.CloseChart(tab, first.Id);
            Assert.Equal(1, closed); Assert.True(settings.Disposed); Assert.False(second.IsClosed);
        }
        finally { window.Controller.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task TransferDoesNotCancelPendingLoadsAndActualWindowCloseAffectsOnlyItsContents()
    {
        var firstReader = new PendingReader(); var secondReader = new PendingReader();
        var window = new MainWindow(firstReader, new TextPicker("synthetic ignored bytes")); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; var tab = owner.SelectedTab!; var first = owner.InitialChart;
            var otherTab = owner.AddTab(window.Model); var second = owner.AddChart(otherTab, secondReader, new TextPicker("synthetic ignored bytes"));
            var a = first.Session.OpenAsync(); var b = second.Session.OpenAsync();
            var tokenA = await firstReader.Entered.Task; var tokenB = await secondReader.Entered.Task;
            var detached = owner.DetachTab(tab);
            Assert.False(tokenA.IsCancellationRequested); Assert.False(tokenB.IsCancellationRequested);
            detached.Host!.Close();
            Assert.True(tokenA.IsCancellationRequested); Assert.False(tokenB.IsCancellationRequested);
            var data = SyntheticBars.Create();
            firstReader.Result.SetResult(new BarReadResult.Success(data)); secondReader.Result.SetResult(new BarReadResult.Success(data));
            await Task.WhenAll(a, b);
            Assert.True(first.IsClosed); Assert.Null(first.Dataset); Assert.Same(data, second.Dataset);
            Assert.Single(owner.Windows); Assert.False(owner.IsClosed);
        }
        finally { window.Controller.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task LastTabTransferAndCloseLeaveUsableEmptyHostsAndClosingOriginalWindowKeepsOtherWindowAlive()
    {
        var window = new MainWindow(); window.Show();
        var owner = window.Controller;
        try
        {
            await WorkspaceTestUi.Ready(window);
            var chart = owner.InitialChart; var tab = owner.SelectedTab!;
            var second = owner.DetachTab(tab);
            Assert.Empty(Assert.Single(window.Model.Tabs).Charts); Assert.False(chart.IsClosed);
            Assert.Contains(second.Host!.GetVisualDescendants().OfType<Button>(), b => b.Name == "AddContentTab");
            window.Close();
            Assert.False(owner.IsClosed); Assert.False(chart.IsClosed); Assert.Single(owner.Windows);
            owner.CloseTab(tab);
            Assert.True(chart.IsClosed); Assert.Empty(Assert.Single(second.Tabs).Charts);
            second.Host!.Close();
            Assert.True(owner.IsClosed); Assert.Empty(owner.Charts); Assert.Empty(owner.Windows);
        }
        finally { owner.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task WholeWorkspaceRoundTripRestoresWindowsEmptyTabsSelectionInputsSplitsAndFeatureSettings()
    {
        using var temp = new TempInput(Csv);
        var window = new MainWindow(picker: new PathPicker(temp.File)); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; var first = owner.InitialChart; var tab = owner.SelectedTab!;
            owner.Rename(tab, "Research"); owner.Name = "Named research";
            first.ContractYearText = "2025"; await first.Session.OpenAsync(); first.RegisterSettings(new Settings());
            var range = new ChartTimeRange(first.Dataset!.Bars[1].StartUtc.Ticks, first.Dataset.Bars[1].EndUtc.Ticks);
            first.SetVisibleTimeRange(range);
            var second = owner.AddChart(tab); owner.SplitChart(tab, second.Id, true);
            ((IDockable)tab.Views[first.Id].Owner!).Proportion = .35;
            ((IDockable)tab.Views[second.Id].Owner!).Proportion = .65;
            var detached = owner.DetachTab(tab);
            var empty = owner.AddTab(detached, "Empty");
            owner.SelectTab(detached, tab); owner.ActivateChart(first.Id);
            var saved = owner.Capture(new(200, 300, 1000, 700, true));
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await store.SaveAsync(saved); var loaded = await store.LoadAsync(); Assert.Null(loaded.Error);
            await owner.RestoreAsync(loaded.State!);
            Assert.Equal(2, owner.Windows.Count); Assert.Equal(2, owner.Windows[1].Tabs.Count);
            Assert.Equal(empty.Id, owner.Windows[1].Tabs[1].Id); Assert.Empty(owner.Windows[1].Tabs[1].Charts);
            var restored = (ChartDocument)owner.Charts.Single(c => c.Id == first.Id);
            Assert.Equal(range, restored.VisibleTimeRange); Assert.Equal("2025", restored.ContractYearText);
            Assert.Equal(saved.Windows[1].Tabs[0].Charts[0].Input, restored.Session.InputReference);
            var settings = new Settings(); restored.RegisterSettings(settings);
            Assert.Equal("violet", settings.Restored!.Value.GetProperty("color").GetString());
            var roundTrip = owner.Capture(saved.Windows[0].Geometry);
            Assert.Equal(saved.Name, roundTrip.Name); Assert.Equal(saved.ActiveChart, roundTrip.ActiveChart);
            Assert.Equal(saved.ActiveWindow, roundTrip.ActiveWindow);
            Assert.Equal(saved.Windows.Select(w => w.SelectedTab), roundTrip.Windows.Select(w => w.SelectedTab));
            Assert.Equal(JsonSerializer.Serialize(saved.Windows[1].Tabs[0].Layout.Root), JsonSerializer.Serialize(roundTrip.Windows[1].Tabs[0].Layout.Root));
            Assert.Equal(first.Id, owner.ActiveChart!.Id);
        }
        finally { window.Controller.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task NamedLayoutsReplaceEntireArrangementAndClosePriorDocumentsAndHosts()
    {
        using var temp = new TempInput(Csv);
        var owner = new WorkspaceController();
        try
        {
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "active.json"));
            owner.Name = "First / research";
            var first = owner.Capture(); await store.Named(owner.Name).SaveAsync(first);
            var chart = owner.InitialChart;
            var detached = owner.DetachTab(owner.SelectedTab!); owner.AddTab(detached, "Empty"); owner.Name = "Second";
            var second = owner.Capture(); await store.Named(owner.Name).SaveAsync(second);
            Assert.Equal(["First / research", "Second"], store.Names());
            await owner.RestoreAsync((await store.Named(first.Name).LoadAsync()).State!);
            Assert.True(chart.IsClosed); Assert.Single(owner.Windows); Assert.Single(owner.Tabs);
            var replaced = owner.Charts.Single();
            await owner.RestoreAsync((await store.Named(second.Name).LoadAsync()).State!);
            Assert.True(replaced.IsClosed); Assert.Equal(2, owner.Windows.Count); Assert.Equal(3, owner.Tabs.Count());
            Assert.Equal(second.Windows.SelectMany(w => w.Tabs).Select(t => t.Id), owner.Tabs.Select(t => t.Id));
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task LegacyLayoutMigratesFloatingChartsAndPreservesOriginalUntilSave()
    {
        using var temp = new TempInput(Csv); var owner = new WorkspaceController();
        try
        {
            var tab = owner.SelectedTab!; var chart = owner.InitialChart;
            var root = tab.Factory.Capture(tab.Root).Root;
            var emptyRoot = new LayoutNode("root", null, null, false, -1, []);
            var legacyTab = new ContentTabState(tab.Id, "Legacy", new(emptyRoot, [new(root, new(500, 400, 900, 600, false))], chart.Id), [chart.Capture()]);
            var shell = new LayoutNode("root", null, null, false, 0, [new("tabs", null, null, false, 0, [new("view", tab.Id, null, false, -1, [])])]);
            var legacy = new LegacyWorkspaceState(1, new(shell, [], tab.Id), [legacyTab], new(100, 100, 1200, 800, false), chart.Id);
            var json = JsonSerializer.Serialize(legacy); var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await File.WriteAllTextAsync(store.Path, json);
            var loaded = await store.LoadAsync(); Assert.NotNull(loaded.State); Assert.Contains("migrated", loaded.Error);
            Assert.Equal(json, await File.ReadAllTextAsync(store.Path));
            await owner.RestoreAsync(loaded.State!);
            Assert.Equal(2, owner.Windows.Count); Assert.Single(owner.Charts);
            Assert.Empty(owner.Windows[0].Tabs[0].Charts); Assert.Equal(chart.Id, owner.ActiveChart!.Id);
            await store.SaveAsync(owner.Capture()); Assert.Equal(json, await File.ReadAllTextAsync(store.Path + ".backup"));
        }
        finally { owner.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidLegacyTabNameWithFloatingChartReturnsRecoverableErrorAndPreservesFile(string? name)
    {
        using var temp = new TempInput(Csv); var owner = new WorkspaceController();
        try
        {
            var tab = owner.SelectedTab!; var chart = owner.InitialChart;
            var root = tab.Factory.Capture(tab.Root).Root;
            var emptyRoot = new LayoutNode("root", null, null, false, -1, []);
            var legacyTab = new ContentTabState(tab.Id, name!,
                new(emptyRoot, [new(root, new(500, 400, 900, 600, false))], chart.Id), [chart.Capture()]);
            var shell = new LayoutNode("root", null, null, false, 0,
                [new("tabs", null, null, false, 0, [new("view", tab.Id, null, false, -1, [])])]);
            var legacy = new LegacyWorkspaceState(1, new(shell, [], tab.Id), [legacyTab], null, chart.Id);
            var json = JsonSerializer.Serialize(legacy);
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            await File.WriteAllTextAsync(store.Path, json);
            var loaded = await store.LoadAsync();
            Assert.Null(loaded.State); Assert.NotNull(loaded.Error);
            Assert.Equal(json, await File.ReadAllTextAsync(store.Path));
            Assert.False(chart.IsClosed);
            await store.SaveAsync(owner.Capture());
            Assert.Equal(json, await File.ReadAllTextAsync(store.Path + ".backup"));
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task InvalidContainmentOwnershipSelectionAndCorruptJsonRetainPriorFile()
    {
        using var temp = new TempInput(Csv); var owner = new WorkspaceController();
        try
        {
            var state = owner.Capture(); var window = state.Windows[0]; var tab = window.Tabs[0];
            var root = tab.Layout.Root; var group = root.Children[0]; var view = group.Children[0];
            var store = new WorkspaceStore(Path.Combine(temp.Directory, "layout.json"));
            foreach (var invalid in new[] { root with { Children = [view] }, root with { Children = [group with { Children = [view, view] }] }, root with { ActiveIndex = 5 } })
            {
                var broken = state with { Windows = [window with { Tabs = [tab with { Layout = tab.Layout with { Root = invalid } }] }] };
                await File.WriteAllTextAsync(store.Path, JsonSerializer.Serialize(broken));
                Assert.Null((await store.LoadAsync()).State);
            }
            foreach (var broken in new[] { state with { Windows = [window, window] }, state with { Windows = [window with { SelectedTab = Guid.NewGuid() }] } })
                Assert.Throws<InvalidDataException>(() => WorkspaceStore.Validate(broken));
            await File.WriteAllTextAsync(store.Path, "{ broken"); Assert.Null((await store.LoadAsync()).State);
            await store.SaveAsync(state); Assert.Equal("{ broken", await File.ReadAllTextAsync(store.Path + ".backup"));
            state.Windows[0].Tabs[0].Charts[0] = tab.Charts[0] with { ViewType = "future-chart" };
            await owner.RestoreAsync(state); Assert.Empty(owner.Charts); Assert.Contains("unsupported", owner.Notice);
            Assert.NotNull(owner.AddChart());
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task CloseDuringRestoreCannotPublishIntoClosedDocuments()
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; var saved = owner.Capture();
            var restoring = owner.RestoreAsync(saved); var chart = owner.Charts.Single();
            owner.Close(); await restoring;
            Assert.True(chart.IsClosed); Assert.Null(owner.ActiveChart); Assert.Empty(owner.Windows);
        }
        finally { window.Controller.Close(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task TabStripCoversHeaderGapsAndFreeSpaceAndCancellationLeavesOwnershipUnchanged()
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller; owner.AddTab(window.Model, "Empty");
            window.UpdateLayout();
            var bar = window.GetVisualDescendants().OfType<ContentTabBar>().Single();
            Assert.Equal(0, bar.InsertionAt(bar.PointToScreen(new(1, 10))));
            Assert.Equal(2, bar.InsertionAt(bar.PointToScreen(new(bar.Bounds.Width - 2, 10))));
            var saved = owner.Capture();
            var header = bar.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetAutomationId(b) == window.Model.Tabs[0].Id.ToString());
            var start = header.TranslatePoint(new(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(170, 0), RawInputModifiers.LeftMouseButton);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.MouseUp(start + new Vector(170, 0), MouseButton.Left);
            Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(owner.Capture()));
        }
        finally { window.Controller.Close(); window.Close(); }
    }
}
