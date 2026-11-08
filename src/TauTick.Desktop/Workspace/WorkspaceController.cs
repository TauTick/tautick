using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Settings;
using TauTick.MarketData;

namespace TauTick.Desktop.Workspace;

internal sealed class WorkspaceController : IChartHost
{
    private readonly List<WorkspaceTab> _tabs = [];
    private readonly Dictionary<Guid, Document> _workspaceViews = [];
    private IChartDocument? _active;
    private bool _selecting;
    private bool _restoringLayout;
    internal bool IsClosed { get; private set; }
    internal WorkspaceDockFactory ShellFactory { get; } = new("tautick-workspaces", true);
    internal IRootDock Shell { get; private set; }
    internal DockControl Surface { get; }
    internal WorkspaceTab? SelectedWorkspace { get; private set; }
    internal IReadOnlyList<WorkspaceTab> Workspaces => _tabs;
    internal ChartDocument InitialChart { get; }
    internal string? Notice { get; private set; }
    internal event Action? Changed;
    public event Action? ActiveChartChanged;
    public IReadOnlyList<IChartDocument> Charts => _tabs.SelectMany(t => t.Charts.Values).Cast<IChartDocument>().ToArray();
    public IChartDocument? ActiveChart => _active;

    internal WorkspaceController(IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null)
    {
        DockSettings.CloseFloatingWindowsOnMainWindowClose = false;
        Shell = ShellFactory.NewRoot();
        Surface = ShellFactory.Surface(Shell);
        ShellFactory.ClosedView = RemoveWorkspace;
        ShellFactory.ActivatedView = id =>
        {
            if (_restoringLayout || IsClosed) return;
            if (_tabs.FirstOrDefault(t => t.Id == id) is not { } tab) return;
            SelectedWorkspace = tab;
            SetActive(tab.ActiveChartId is { } chartId ? tab.Charts.GetValueOrDefault(chartId) : tab.Charts.Values.FirstOrDefault());
        };
        var initial = AddWorkspace("Workspace 1", false);
        InitialChart = AddChart(initial, reader, picker);
    }

    internal WorkspaceTab AddWorkspace(string? name = null, bool addChart = true, Guid? id = null)
    {
        VerifyOpen();
        var tab = new WorkspaceTab(this, id ?? Guid.NewGuid(), name ?? $"Workspace {_tabs.Count + 1}");
        _tabs.Add(tab);
        var view = ShellFactory.View(tab.Id, tab.Name, tab.CreateView);
        _workspaceViews.Add(tab.Id, view);
        ShellFactory.AddDockable(ShellFactory.FirstGroup(Shell), view);
        SelectedWorkspace = tab;
        if (addChart) AddChart(tab);
        ShellFactory.Select(view);
        Changed?.Invoke();
        return tab;
    }

    internal ChartDocument AddChart(WorkspaceTab? workspace = null, IHistoricalBarReader? reader = null,
        ICsvFilePicker? picker = null, Guid? id = null)
    {
        VerifyOpen();
        workspace ??= SelectedWorkspace ?? AddWorkspace(addChart: false);
        var tab = workspace;
        var document = new ChartDocument(id ?? Guid.NewGuid(), reader, picker);
        var label = $"Chart {tab.Charts.Count + 1}";
        var view = tab.Factory.View(document.Id, label, () => new ChartView(document, () => ChartMenu(tab, document)));
        document.Activate = () => ActivateChart(document.Id);
        document.Changed += () => view.Title = document.Dataset is { } data ? $"{label} · {data.Instrument.Symbol}" : label;
        tab.Charts.Add(document.Id, document);
        tab.Views.Add(document.Id, view);
        var target = tab.ActiveChartId is { } active && tab.Views.TryGetValue(active, out var activeView)
            && activeView.Owner is IDocumentDock current ? current : tab.Factory.FirstGroup(tab.Root);
        tab.Factory.AddDockable(target, view);
        ActivateChart(document.Id);
        Changed?.Invoke();
        return document;
    }

    public bool ActivateChart(Guid id)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsClosed || _restoringLayout) return false;
        var tab = _tabs.FirstOrDefault(t => t.Charts.ContainsKey(id));
        if (tab is null || tab.Charts[id].IsClosed) return false;
        if (_selecting) return true;
        _selecting = true;
        try
        {
            SelectedWorkspace = tab;
            tab.ActiveChartId = id;
            var view = tab.Views[id];
            if (ReferenceEquals(tab.Factory.FindRoot(view), tab.Root) && _workspaceViews.TryGetValue(tab.Id, out var workspaceView))
                ShellFactory.Select(workspaceView);
            tab.Factory.Select(view);
            SetActive(tab.Charts[id]);
            return true;
        }
        finally { _selecting = false; }
    }

    internal void ActivateFromDock(WorkspaceTab tab, Guid id)
    {
        if (_restoringLayout || IsClosed) return;
        if (!tab.Charts.TryGetValue(id, out var document) || document.IsClosed) return;
        SelectedWorkspace = tab;
        tab.ActiveChartId = id;
        SetActive(document);
    }

    private void SetActive(IChartDocument? chart)
    {
        if (ReferenceEquals(_active, chart)) return;
        _active = chart;
        ActiveChartChanged?.Invoke();
    }

    internal void Rename(WorkspaceTab tab, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 200) return;
        tab.Name = trimmed;
        _workspaceViews[tab.Id].Title = trimmed;
        Changed?.Invoke();
    }

    internal void MoveWorkspace(WorkspaceTab tab, int delta)
    {
        var view = _workspaceViews[tab.Id];
        if (view.Owner is not IDock { VisibleDockables: { } items } parent) return;
        var index = items.IndexOf(view);
        var target = Math.Clamp(index + delta, 0, items.Count - 1);
        if (index == target) return;
        items.RemoveAt(index);
        items.Insert(target, view);
        parent.ActiveDockable = view;
    }

    internal void DetachWorkspace(WorkspaceTab tab) => ShellFactory.FloatDockable(_workspaceViews[tab.Id]);
    internal void RedockWorkspace(WorkspaceTab tab) => ShellFactory.Redock(Shell, _workspaceViews[tab.Id]);
    internal void CloseWorkspace(WorkspaceTab tab) => ShellFactory.CloseDockable(_workspaceViews[tab.Id]);
    internal void CloseChart(WorkspaceTab tab, Guid id) { if (tab.Views.TryGetValue(id, out var view)) tab.Factory.CloseDockable(view); }
    internal void DetachChart(WorkspaceTab tab, Guid id) => tab.Factory.FloatDockable(tab.Views[id]);
    internal void RedockChart(WorkspaceTab tab, Guid id) => tab.Factory.Redock(tab.Root, tab.Views[id]);
    internal void SplitChart(WorkspaceTab tab, Guid id, bool below)
    {
        if (below) tab.Factory.NewVerticalDocumentDock(tab.Views[id]);
        else tab.Factory.NewHorizontalDocumentDock(tab.Views[id]);
        ActivateChart(id);
    }

    internal void RemoveChart(WorkspaceTab tab, Guid id)
    {
        if (!tab.Charts.Remove(id, out var chart)) return;
        tab.Views.Remove(id);
        chart.Close();
        if (tab.ActiveChartId == id) tab.ActiveChartId = tab.Charts.Keys.Cast<Guid?>().FirstOrDefault();
        if (ReferenceEquals(_active, chart)) SetActive(tab.Charts.Values.FirstOrDefault());
        Changed?.Invoke();
    }

    private void RemoveWorkspace(Guid id)
    {
        if (_tabs.FirstOrDefault(t => t.Id == id) is not { } tab) return;
        _tabs.Remove(tab);
        _workspaceViews.Remove(id);
        tab.Close();
        if (SelectedWorkspace == tab) SelectedWorkspace = _tabs.FirstOrDefault();
        if (_active is { IsClosed: true }) SetActive(SelectedWorkspace?.Charts.Values.FirstOrDefault());
        Changed?.Invoke();
    }

    internal WorkspaceState Capture(WindowGeometry? geometry) => new(1, ShellFactory.Capture(Shell),
        // The workspace owns active-chart targeting across its main and floating roots.
        // The main Dock root's focus alone can still point at a different embedded chart.
        _tabs.Select(t => new WorkspaceTabState(t.Id, t.Name,
            t.Factory.Capture(t.Root) with { FocusedView = t.ActiveChartId },
            t.Charts.Values.Select(c => c.Capture()).ToList())).ToList(),
        geometry, _active?.Id);

    internal async Task RestoreAsync(WorkspaceState state)
    {
        VerifyOpen();
        WorkspaceStore.Validate(state);
        var restore = new List<(ChartDocument Document, ChartState State)>();
        _restoringLayout = true;
        try
        {
            foreach (var tab in _tabs.ToArray()) tab.Close();
            ShellFactory.CloseFloating(Shell);
            _tabs.Clear();
            _workspaceViews.Clear();
            SetActive(null);
            var skipped = 0;
            foreach (var saved in state.Workspaces)
            {
                var tab = new WorkspaceTab(this, saved.Id, saved.Name);
                _tabs.Add(tab);
                foreach (var chart in saved.Charts)
                {
                    if (chart.ViewType != "candles") { skipped++; continue; }
                    var document = new ChartDocument(chart.Id);
                    var label = $"Chart {tab.Charts.Count + 1}";
                    var view = tab.Factory.View(chart.Id, label, () => new ChartView(document, () => ChartMenu(tab, document)));
                    document.Activate = () => ActivateChart(document.Id);
                    document.Changed += () => view.Title = document.Dataset is { } data ? $"{label} · {data.Instrument.Symbol}" : label;
                    tab.Charts.Add(chart.Id, document);
                    tab.Views.Add(chart.Id, view);
                    document.PrepareRestore(chart);
                    restore.Add((document, chart));
                }
                tab.Root = tab.Factory.Restore(saved.Layout, tab.Views);
                tab.ActiveChartId = saved.Layout.FocusedView is { } focused && tab.Charts.ContainsKey(focused)
                    ? focused : tab.Charts.Keys.Cast<Guid?>().FirstOrDefault();
                _workspaceViews.Add(tab.Id, ShellFactory.View(tab.Id, tab.Name, tab.CreateView));
            }
            Shell = ShellFactory.Restore(state.Shell, _workspaceViews);
            Surface.Layout = Shell;
            SelectedWorkspace = _tabs.FirstOrDefault(t => t.Id == state.Shell.FocusedView) ?? _tabs.FirstOrDefault();
            if (_tabs.Count == 0) AddWorkspace();
            if (_tabs.FirstOrDefault(t => state.ActiveChart is { } id && t.Charts.ContainsKey(id)) is { } activeTab)
            {
                SelectedWorkspace = activeTab;
                activeTab.ActiveChartId = state.ActiveChart;
            }
            SetActive(SelectedWorkspace?.ActiveChartId is { } active
                ? SelectedWorkspace.Charts.GetValueOrDefault(active) : SelectedWorkspace?.Charts.Values.FirstOrDefault());
            if (skipped > 0) Notice = $"Skipped {skipped} unsupported saved view(s). Supported charts remain available; the previous layout is backed up on save.";
            Changed?.Invoke();

            // Dock's nested content and window callbacks run on dispatcher turns.
            // Ignore their incidental activation until the saved selection is presented.
            // This is a queue boundary, not a timing delay or a wait for CSV imports.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        }
        finally { _restoringLayout = false; }
        if (IsClosed) return;
        if (_active is ChartDocument activeChart)
        {
            ActivateChart(activeChart.Id);
            activeChart.WindowOwner?.Invoke()?.Activate();
        }
        foreach (var (document, saved) in restore)
        {
            if (IsClosed) break;
            if (!document.IsClosed) await document.RestoreAsync(saved);
        }
    }

    internal void Close()
    {
        if (IsClosed) return;
        IsClosed = true;
        foreach (var tab in _tabs.ToArray()) tab.Close();
        ShellFactory.CloseFloating(Shell);
        SetActive(null);
        ShellFactory.ClosedView = null;
        ShellFactory.ActivatedView = null;
        Surface.Layout = null;
        _tabs.Clear();
        _workspaceViews.Clear();
    }

    private Menu ChartMenu(WorkspaceTab tab, ChartDocument document)
    {
        var menu = new MenuItem { Header = "Chart ▾" };
        menu.Items.Add(Item("Split right", () => SplitChart(tab, document.Id, false)));
        menu.Items.Add(Item("Split below", () => SplitChart(tab, document.Id, true)));
        menu.Items.Add(Item("Detach chart", () => DetachChart(tab, document.Id)));
        menu.Items.Add(Item("Dock back", () => RedockChart(tab, document.Id)));
        menu.Items.Add(Item("Close chart", () => CloseChart(tab, document.Id)));
        return new Menu { Items = { menu } };
    }

    internal static MenuItem Item(string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        return item;
    }

    private void VerifyOpen() { Dispatcher.UIThread.VerifyAccess(); ObjectDisposedException.ThrowIf(IsClosed, this); }
}

internal sealed class WorkspaceTab
{
    private readonly WorkspaceController _owner;
    internal WorkspaceTab(WorkspaceController owner, Guid id, string name)
    {
        _owner = owner; Id = id; Name = name;
        Factory = new("charts-" + id, false);
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

    internal Control CreateView()
    {
        var name = new TextBox { Name = "WorkspaceName", Text = Name, Width = 160, MaxLength = 200 };
        name.LostFocus += (_, _) => _owner.Rename(this, name.Text ?? Name);
        name.KeyDown += (_, e) => { if (e.Key == Key.Enter) _owner.Rename(this, name.Text ?? Name); };
        var commands = new MenuItem { Header = "Workspace ▾" };
        commands.Items.Add(WorkspaceController.Item("Move tab left", () => _owner.MoveWorkspace(this, -1)));
        commands.Items.Add(WorkspaceController.Item("Move tab right", () => _owner.MoveWorkspace(this, 1)));
        commands.Items.Add(WorkspaceController.Item("Detach workspace", () => _owner.DetachWorkspace(this)));
        commands.Items.Add(WorkspaceController.Item("Dock workspace back", () => _owner.RedockWorkspace(this)));
        commands.Items.Add(WorkspaceController.Item("Close workspace and its charts", () => _owner.CloseWorkspace(this)));
        var add = new Button { Name = "AddChart", Content = "+ Chart" };
        add.Click += (_, _) => _owner.AddChart(this);
        var header = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Margin = new(6, 3),
            Children = { name, add, new Menu { Items = { commands } } } };
        var panel = new DockPanel();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(Factory.Surface(Root));
        return panel;
    }

    internal void Close()
    {
        foreach (var chart in Charts.Values.ToArray()) chart.Close();
        Factory.ClosedView = null;
        Factory.ActivatedView = null;
        Factory.CloseFloating(Root);
        Charts.Clear();
        Views.Clear();
    }
}
