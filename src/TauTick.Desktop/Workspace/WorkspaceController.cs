using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using TauTick.MarketData;

namespace TauTick.Desktop.Workspace;

/// <summary>One live workspace owns windows; windows own tabs; tabs own chart lifetimes.</summary>
internal sealed class WorkspaceController : IChartHost
{
    private readonly List<WorkspaceWindow> _windows = [];
    private IChartDocument? _active;
    private bool _selecting;
    private bool _restoringLayout;
    private int _generation;
    private long _activationOrder;
    internal bool IsClosed { get; private set; }
    internal IReadOnlyList<WorkspaceWindow> Windows => _windows;
    internal IEnumerable<ContentTab> Tabs => _windows.SelectMany(w => w.Tabs);
    internal WorkspaceWindow ActiveWindow { get; private set; }
    internal ContentTab? SelectedTab => ActiveWindow.SelectedTab;
    internal ChartDocument InitialChart { get; }
    internal string Name { get; set; } = "Workspace";
    internal string? Notice { get; private set; }
    internal bool PersistenceBusy { get; private set; }
    internal event Action? PersistenceCompleted;
    internal event Action? Changed;
    public event Action? ActiveChartChanged;
    public IReadOnlyList<IChartDocument> Charts => Tabs.SelectMany(t => t.Charts.Values).Cast<IChartDocument>().ToArray();
    public IChartDocument? ActiveChart => _active;

    internal bool TryBeginPersistence()
    {
        VerifyOpen();
        if (PersistenceBusy) return false;
        PersistenceBusy = true;
        return true;
    }
    internal void EndPersistence() { PersistenceBusy = false; PersistenceCompleted?.Invoke(); }

    internal WorkspaceController(IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null)
    {
        ActiveWindow = new(Guid.NewGuid());
        _windows.Add(ActiveWindow);
        var initial = AddTab(ActiveWindow, "Tab 1");
        InitialChart = AddChart(initial, reader, picker);
    }

    internal ContentTab AddTab(WorkspaceWindow window, string? name = null)
    {
        VerifyWindow(window);
        var tab = new ContentTab(this, Guid.NewGuid(), name ?? $"Tab {window.Tabs.Count + 1}");
        window.Tabs.Add(tab);
        SelectTab(window, tab);
        return tab;
    }

    internal WorkspaceWindow NewWindow(WindowGeometry? geometry = null, bool show = true)
    {
        VerifyOpen();
        var model = new WorkspaceWindow(Guid.NewGuid()) { Geometry = geometry ?? new(150, 150, 1000, 700, false) };
        _windows.Add(model);
        if (_windows.FirstOrDefault(w => w.Host is not null)?.Host is { } source)
        {
            var host = new MainWindow(this, model, source.Store) { ShowActivated = show };
            model.Geometry.Apply(host);
            if (show) host.Show();
        }
        return model;
    }

    internal void SelectTab(WorkspaceWindow window, ContentTab tab)
    {
        VerifyWindow(window);
        if (!window.Tabs.Contains(tab)) throw new InvalidOperationException("Tab belongs to another window.");
        window.SelectedTabId = tab.Id;
        ActiveWindow = window;
        SetActive(tab.ActiveChartId is { } id ? tab.Charts.GetValueOrDefault(id) : null);
        Changed?.Invoke();
    }

    internal void ActivateWindow(WorkspaceWindow window)
    {
        if (IsClosed || _restoringLayout || !_windows.Contains(window)) return;
        ActiveWindow = window;
        window.ActivationOrder = ++_activationOrder;
        SetActive(window.SelectedTab?.ActiveChartId is { } id ? window.SelectedTab.Charts.GetValueOrDefault(id) : null);
    }

    internal void MoveTab(ContentTab tab, WorkspaceWindow destination, int insertionIndex)
    {
        VerifyWindow(destination);
        var source = Owner(tab);
        var oldIndex = source.Tabs.IndexOf(tab);
        var index = Math.Clamp(insertionIndex, 0, destination.Tabs.Count);
        if (source == destination && index > oldIndex) index--;
        if (source == destination && index == oldIndex) { SelectTab(destination, tab); return; }
        // Release the old visual parent before any destination can attach the same content.
        source.Host?.ReleaseContent();
        source.Tabs.RemoveAt(oldIndex);
        destination.Tabs.Insert(index, tab);
        if (source != destination)
        {
            tab.HostChanged();
            if (source.SelectedTabId == tab.Id) source.SelectedTabId = source.Tabs.ElementAtOrDefault(Math.Min(oldIndex, source.Tabs.Count - 1))?.Id;
            // Keeping an empty tab makes even the original/last source window a normal host.
            if (source.Tabs.Count == 0) AddTab(source);
        }
        SelectTab(destination, tab);
        destination.Host?.Activate();
    }

    internal WorkspaceWindow DetachTab(ContentTab tab, PixelPoint? position = null)
    {
        var source = Owner(tab);
        var bounds = source.Host?.CaptureGeometry() ?? source.Geometry;
        var destination = NewWindow(bounds with { X = position?.X ?? bounds.X + 40, Y = position?.Y ?? bounds.Y + 40, Maximized = false }, false);
        MoveTab(tab, destination, 0);
        destination.Host?.Show();
        destination.Host?.Activate();
        return destination;
    }

    internal void CloseTab(ContentTab tab)
    {
        var window = Owner(tab);
        window.Host?.ReleaseContent();
        var index = window.Tabs.IndexOf(tab);
        window.Tabs.Remove(tab);
        tab.Close();
        if (window.SelectedTabId == tab.Id) window.SelectedTabId = window.Tabs.ElementAtOrDefault(Math.Min(index, window.Tabs.Count - 1))?.Id;
        if (window.Tabs.Count == 0) AddTab(window);
        else SelectTab(window, window.SelectedTab ?? window.Tabs[0]);
    }

    internal ChartDocument AddChart(ContentTab? tab = null, IHistoricalBarReader? reader = null,
        ICsvFilePicker? picker = null, Guid? id = null)
    {
        VerifyOpen();
        tab ??= SelectedTab ?? AddTab(ActiveWindow);
        var document = new ChartDocument(id ?? Guid.NewGuid(), reader, picker);
        AddDocument(tab, document);
        var target = tab.ActiveChartId is { } active && tab.Views.TryGetValue(active, out var activeView)
            && activeView.Owner is IDocumentDock current ? current : tab.Factory.FirstGroup(tab.Root);
        tab.Factory.AddDockable(target, tab.Views[document.Id]);
        ActivateChart(document.Id);
        Changed?.Invoke();
        return document;
    }

    private void AddDocument(ContentTab tab, ChartDocument document)
    {
        var label = $"Chart {tab.Charts.Count + 1}";
        var view = tab.Factory.View(document.Id, label, () => new ChartView(document, () => ChartMenu(tab, document)));
        document.IsWorkspaceHosted = true;
        document.WindowOwner = () => _windows.FirstOrDefault(w => w.Tabs.Contains(tab))?.Host;
        document.Activate = () => ActivateChart(document.Id);
        document.Changed += () => view.Title = document.Dataset is { } data ? $"{label} · {data.Instrument.Symbol}" : label;
        tab.Charts.Add(document.Id, document);
        tab.Views.Add(document.Id, view);
    }

    public bool ActivateChart(Guid id)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsClosed || _restoringLayout) return false;
        var tab = Tabs.FirstOrDefault(t => t.Charts.ContainsKey(id));
        if (tab is null || tab.Charts[id].IsClosed) return false;
        if (_selecting) return true;
        _selecting = true;
        try
        {
            tab.ActiveChartId = id;
            SelectTab(Owner(tab), tab);
            tab.Factory.Select(tab.Views[id]);
            return true;
        }
        finally { _selecting = false; }
    }

    internal void ActivateFromDock(ContentTab tab, Guid id)
    {
        if (_restoringLayout || IsClosed || _selecting || !tab.Charts.TryGetValue(id, out var document) || document.IsClosed) return;
        tab.ActiveChartId = id;
        // Background presentation of a hidden tab must not steal the window's selection.
        var window = Owner(tab);
        if (window.SelectedTabId == tab.Id && ActiveWindow == window) SetActive(document);
    }

    private void SetActive(IChartDocument? chart)
    {
        if (ReferenceEquals(_active, chart)) return;
        _active = chart;
        ActiveChartChanged?.Invoke();
    }

    internal void Rename(ContentTab tab, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 200) return;
        tab.Name = trimmed;
        Changed?.Invoke();
    }

    internal void CloseChart(ContentTab tab, Guid id) { if (tab.Views.TryGetValue(id, out var view)) tab.Factory.CloseDockable(view); }
    internal void SplitChart(ContentTab tab, Guid id, bool below)
    {
        if (below) tab.Factory.NewVerticalDocumentDock(tab.Views[id]);
        else tab.Factory.NewHorizontalDocumentDock(tab.Views[id]);
        ActivateChart(id);
    }

    internal void RemoveChart(ContentTab tab, Guid id)
    {
        if (!tab.Charts.Remove(id, out var chart)) return;
        tab.Views.Remove(id);
        chart.Close();
        if (tab.ActiveChartId == id) tab.ActiveChartId = tab.Charts.Keys.Cast<Guid?>().FirstOrDefault();
        if (ReferenceEquals(_active, chart)) SetActive(tab.ActiveChartId is { } active ? tab.Charts[active] : null);
        Changed?.Invoke();
    }

    internal WorkspaceState Capture(WindowGeometry? geometry = null) => new(2, Name,
        _windows.Select((w, i) => new WorkspaceWindowState(w.Id,
            i == 0 && geometry is not null ? geometry : w.Host?.CaptureGeometry() ?? w.Geometry,
            w.Tabs.Select(t => new ContentTabState(t.Id, t.Name, t.Factory.Capture(t.Root) with { FocusedView = t.ActiveChartId },
                t.Charts.Values.Select(c => c.Capture()).ToList())).ToList(), w.SelectedTabId ?? w.Tabs[0].Id)).ToList(),
        ActiveWindow.Id, _active?.Id);

    internal async Task RestoreAsync(WorkspaceState state, WorkspaceWindow? receivingWindow = null)
    {
        VerifyOpen();
        WorkspaceStore.Validate(state);
        var generation = ++_generation;
        var restore = new List<(ChartDocument Document, ChartState State)>();
        _restoringLayout = true;
        try
        {
            var retained = receivingWindow ?? _windows[0];
            foreach (var window in _windows.ToArray())
            {
                window.Host?.ReleaseContent();
                foreach (var tab in window.Tabs) tab.Close();
                window.Tabs.Clear();
                if (window != retained) window.Host?.CloseForLayout();
            }
            _windows.Clear();
            SetActive(null);
            Name = state.Name;
            Notice = null;
            var skipped = 0;
            for (var index = 0; index < state.Windows.Count; index++)
            {
                var savedWindow = state.Windows[index];
                var window = index == 0 ? retained : new WorkspaceWindow(savedWindow.Id);
                window.Id = savedWindow.Id;
                window.Geometry = savedWindow.Geometry;
                _windows.Add(window);
                foreach (var saved in savedWindow.Tabs)
                {
                    var tab = new ContentTab(this, saved.Id, saved.Name);
                    window.Tabs.Add(tab);
                    foreach (var chart in saved.Charts)
                    {
                        if (chart.ViewType != "candles") { skipped++; continue; }
                        var document = new ChartDocument(chart.Id);
                        AddDocument(tab, document);
                        document.PrepareRestore(chart);
                        restore.Add((document, chart));
                    }
                    tab.Root = tab.Factory.Restore(saved.Layout, tab.Views);
                    tab.ActiveChartId = saved.Layout.FocusedView is { } focused && tab.Charts.ContainsKey(focused)
                        ? focused : tab.Charts.Keys.Cast<Guid?>().FirstOrDefault();
                }
                window.SelectedTabId = savedWindow.SelectedTab;
                if (index != 0 && retained.Host is { } source)
                    _ = new MainWindow(this, window, source.Store) { ShowActivated = false };
                window.Geometry.ApplyIfPresent(window.Host);
                if (index != 0) window.Host?.Show();
            }
            ActiveWindow = _windows.FirstOrDefault(w => w.Id == state.ActiveWindow) ?? _windows[0];
            if (state.ActiveChart is { } chartId && ActiveWindow.SelectedTab is { } activeTab && activeTab.Charts.TryGetValue(chartId, out var active))
                SetActive(active);
            else SetActive(ActiveWindow.SelectedTab?.ActiveChartId is { } id ? ActiveWindow.SelectedTab.Charts.GetValueOrDefault(id) : null);
            if (skipped > 0) Notice = $"Skipped {skipped} unsupported saved view(s). Supported charts remain available; the previous layout is backed up on save.";
            Changed?.Invoke();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        }
        finally { if (generation == _generation) _restoringLayout = false; }
        if (IsClosed || generation != _generation) return;
        ActiveWindow.Host?.Activate();
        // Parallel verification avoids serializing unrelated saved inputs. Each document owns cancellation.
        await Task.WhenAll(restore.Where(x => !x.Document.IsClosed).Select(x => x.Document.RestoreAsync(x.State)));
    }

    internal WorkspaceWindow Owner(ContentTab tab) => _windows.Single(w => w.Tabs.Contains(tab));

    internal void CloseWindow(WorkspaceWindow window)
    {
        if (!_windows.Remove(window)) return;
        window.Host?.ReleaseContent();
        foreach (var tab in window.Tabs) tab.Close();
        window.Tabs.Clear();
        window.Host = null;
        if (_windows.Count == 0) { IsClosed = true; _generation++; SetActive(null); }
        else { ActiveWindow = _windows.Contains(ActiveWindow) ? ActiveWindow : _windows[0]; ActivateWindow(ActiveWindow); Changed?.Invoke(); }
    }

    internal void Close()
    {
        if (IsClosed) return;
        IsClosed = true;
        _generation++;
        foreach (var window in _windows.ToArray())
        {
            window.Host?.ReleaseContent();
            foreach (var tab in window.Tabs) tab.Close();
            window.Tabs.Clear();
            window.Host?.CloseForLayout();
        }
        _windows.Clear();
        SetActive(null);
    }

    private Menu ChartMenu(ContentTab tab, ChartDocument document)
    {
        var menu = new MenuItem { Header = "Chart ▾" };
        menu.Items.Add(Item("Split right", () => SplitChart(tab, document.Id, false)));
        menu.Items.Add(Item("Split below", () => SplitChart(tab, document.Id, true)));
        menu.Items.Add(Item("Close chart", () => CloseChart(tab, document.Id)));
        return new Menu { Items = { menu } };
    }

    internal static MenuItem Item(string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        return item;
    }

    private void VerifyWindow(WorkspaceWindow window) { VerifyOpen(); if (!_windows.Contains(window)) throw new InvalidOperationException("Unknown workspace window."); }
    private void VerifyOpen() { Dispatcher.UIThread.VerifyAccess(); ObjectDisposedException.ThrowIf(IsClosed, this); }
}

internal sealed class WorkspaceWindow(Guid id)
{
    internal Guid Id { get; set; } = id;
    internal List<ContentTab> Tabs { get; } = [];
    internal Guid? SelectedTabId { get; set; }
    internal ContentTab? SelectedTab => Tabs.FirstOrDefault(t => t.Id == SelectedTabId);
    internal MainWindow? Host { get; set; }
    internal long ActivationOrder { get; set; }
    internal WindowGeometry Geometry { get; set; } = new(100, 100, 1200, 800, false);
}

internal sealed class ContentTab
{
    private Control? _surface;
    internal ContentTab(WorkspaceController owner, Guid id, string name)
    {
        Id = id; Name = name;
        Factory = new("charts-" + id);
        Factory.ClosedView = viewId => owner.RemoveChart(this, viewId);
        Factory.ActivatedView = viewId => owner.ActivateFromDock(this, viewId);
        Root = Factory.NewRoot();
    }
    internal Guid Id { get; }
    internal string Name { get; set; }
    internal WorkspaceDockFactory Factory { get; }
    internal IRootDock Root { get; set; }
    internal Dictionary<Guid, ChartDocument> Charts { get; } = [];
    internal Dictionary<Guid, Document> Views { get; } = [];
    internal Guid? ActiveChartId { get; set; }
    internal Control Surface => _surface ??= Factory.Surface(Root);
    internal void HostChanged()
    {
        foreach (var chart in Charts.Values) chart.HostChanged();
        if (_surface is Dock.Avalonia.Controls.DockControl dock) dock.Layout = null;
        _surface = null;
    }
    internal void Close()
    {
        foreach (var chart in Charts.Values.ToArray()) chart.Close();
        Factory.ClosedView = null;
        Factory.ActivatedView = null;
        Factory.CloseFloating(Root);
        Charts.Clear(); Views.Clear();
        if (_surface is Dock.Avalonia.Controls.DockControl dock) dock.Layout = null;
        _surface = null;
    }
}
