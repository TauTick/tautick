using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using TauTick.Charting;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;
using TauTick.UI;

namespace TauTick.Desktop;

public sealed class MainWindow : Window
{
    private readonly TextBlock _status = new() { Name = "WorkspaceStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 11 };
    private readonly ContentControl _content = new();
    private readonly ContentTabBar _tabs = new() { Name = "ContentTabs" };
    private readonly TextBox _layoutName = new() { Name = "LayoutName", Width = 150, MaxLength = 100 };
    private readonly ComboBox _savedLayouts = new() { Name = "SavedLayouts", Width = 160, PlaceholderText = "Saved layouts" };
    private readonly TextBox _tabName = new() { Name = "ContentTabName", Width = 150, MaxLength = 200 };
    private readonly Button _discardAndClose = new() { Content = "Close without saving", IsVisible = false };
    private bool _allowClose;
    private bool _layoutClose;
    private bool _saving;
    private bool _loadingLayout;
    private bool _closeAfterLoad;
    private WindowGeometry? _normalBounds;
    private readonly bool _startup;
    internal WorkspaceStore? Store { get; }
    internal WorkspaceWindow Model { get; }
    internal WorkspaceController Controller { get; }
    public IChartHost ChartHost => Controller;
    public BarChart Chart => Controller.InitialChart.LatestChart;
    public CsvViewerSession Session => Controller.InitialChart.Session;

    public MainWindow(IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null, string? notice = null, WorkspaceStore? store = null)
        : this(new WorkspaceController(reader, picker), null, store, true)
    { if (notice is not null) SetStatus(notice); }

    internal MainWindow(WorkspaceController controller, WorkspaceWindow model, WorkspaceStore? store)
        : this(controller, model, store, false) { }

    private MainWindow(WorkspaceController controller, WorkspaceWindow? model, WorkspaceStore? store, bool startup)
    {
        Title = "TauTick — Workspace";
        Width = 1200; Height = 800; MinWidth = 560; MinHeight = 380;
        Controller = controller; Model = model ?? controller.Windows[0]; Model.Host = this;
        Store = store; _startup = startup;
        _layoutName.Text = controller.Name;
        var save = Button("Save layout", "SaveWorkspace", async () => await SaveAsync());
        var load = Button("Load layout", "LoadWorkspace", async () =>
        {
            if (_savedLayouts.SelectedItem is string name) await LoadAsync(name);
        });
        var exit = Button("Exit application", "ExitApplication", async () =>
        {
            if (Store is null || await SaveAsync(saveNamed: false)) Controller.Close();
        });
        save.IsVisible = load.IsVisible = _savedLayouts.IsVisible = _layoutName.IsVisible = store is not null;
        _discardAndClose.Click += (_, _) => { _allowClose = true; Close(); };
        var actions = new WrapPanel { Margin = new(8, 4), Children = {
            new TextBlock { Text = "TauTick", FontSize = 18, Margin = new(0,0,10,0), VerticalAlignment = VerticalAlignment.Center },
            _layoutName, save, _savedLayouts, load, exit, _discardAndClose } };
        _tabName.LostFocus += (_, _) => RenameTab();
        _tabName.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) RenameTab(); };
        var addChart = Button("+ Chart", "AddChart", () => Controller.AddChart(Model.SelectedTab));
        var detach = Button("Detach tab", "DetachTab", () => { if (Model.SelectedTab is { } tab) Controller.DetachTab(tab); });
        var tabActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(6, 3), Children = { _tabName, addChart, detach } };
        var header = new StackPanel { Children = { actions, _status, _tabs, tabActions } };
        _status.IsVisible = false;
        var root = new DockPanel(); DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        root.Children.Add(header); root.Children.Add(_content); Content = root;
        foreach (var control in new Control[] { _tabs, _layoutName, _savedLayouts, _tabName }) AutomationProperties.SetAutomationId(control, control.Name!);
        _tabs.AddRequested += () => Controller.AddTab(Model);
        _tabs.SelectionRequested += id => Controller.SelectTab(Model, Model.Tabs.Single(t => t.Id.ToString() == id));
        _tabs.CloseRequested += id => Controller.CloseTab(Model.Tabs.Single(t => t.Id.ToString() == id));
        _tabs.DragMoved += drag => Drag(drag, false);
        _tabs.DragCompleted += drag => Drag(drag, true);
        _tabs.DragCancelled += ClearInsertion;
        Controller.Changed += Refresh;
        Controller.PersistenceCompleted += PersistenceCompleted;
        Activated += (_, _) => Controller.ActivateWindow(Model);
        Opened += async (_, _) =>
        {
            TrackBounds(); RefreshNames();
            if (!_startup || Store is null) return;
            await LoadAsync(null);
        };
        PositionChanged += (_, _) => TrackBounds(); SizeChanged += (_, _) => TrackBounds();
        Closing += async (_, e) =>
        {
            _tabs.CancelDrag();
            if (_allowClose || Store is null || Controller.Windows.Count > 1) return;
            e.Cancel = true;
            if (Controller.PersistenceBusy) { _closeAfterLoad = true; return; }
            if (_saving) return;
            if (await SaveAsync(saveNamed: false)) { _allowClose = true; Close(); }
        };
        Closed += (_, _) =>
        {
            Controller.Changed -= Refresh;
            Controller.PersistenceCompleted -= PersistenceCompleted;
            if (!_layoutClose) Controller.CloseWindow(Model);
        };
        Refresh();
    }

    private static Button Button(string text, string id, Action action)
    {
        var button = new Button { Name = id, Content = text, Classes = { "tau" }, Margin = new(3, 0) };
        AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => action(); return button;
    }
    private void RenameTab() { if (Model.SelectedTab is { } tab) Controller.Rename(tab, _tabName.Text ?? tab.Name); }
    internal void ReleaseContent() => _content.Content = null;
    internal void CloseForLayout() { _layoutClose = true; _allowClose = true; Close(); }
    private void Refresh()
    {
        if (Controller.IsClosed) return;
        _tabs.SetTabs(Model.Tabs.Select(t => new ContentTabItem(t.Id.ToString(), t.Name)), Model.SelectedTabId?.ToString());
        var selected = Model.SelectedTab;
        _tabName.Text = selected?.Name;
        if (selected is not null && !ReferenceEquals(_content.Content, selected.Surface)) _content.Content = selected.Surface;
        if (Controller.Notice is { } notice) SetStatus(notice);
        Title = $"TauTick — {Controller.Name}";
        if (!_layoutName.IsFocused) _layoutName.Text = Controller.Name;
    }
    private void Drag(ContentTabDrag drag, bool complete)
    {
        var tab = Controller.Tabs.FirstOrDefault(t => t.Id.ToString() == drag.Id);
        if (tab is null) { ClearInsertion(); return; }
        // Only the foremost workspace window under the pointer can receive a drop.
        // A covered strip behind another window's ordinary content is not a target.
        var hit = Controller.Windows.OrderByDescending(w => w.ActivationOrder).FirstOrDefault(w => Contains(w, drag.Position));
        var insertion = hit?.Host?._tabs.InsertionAt(drag.Position);
        foreach (var window in Controller.Windows)
        {
            window.Host?._tabs.ShowInsertion(window == hit ? insertion : null);
        }
        if (!complete) return;
        ClearInsertion();
        if (hit is not null && insertion is { } index) Controller.MoveTab(tab, hit, index);
        else if (hit is null)
            Controller.DetachTab(tab, drag.Position);
        // A release over ordinary window content is a cancelled transfer.
    }
    private static bool Contains(WorkspaceWindow window, PixelPoint point) => window.Host is { IsVisible: true } host
        && host.WindowState != WindowState.Minimized
        && new PixelRect(host.Position, PixelSize.FromSize(host.Bounds.Size, host.RenderScaling)).Contains(point);
    private void ClearInsertion() { foreach (var window in Controller.Windows) window.Host?._tabs.ShowInsertion(null); }

    public async Task<bool> SaveAsync(bool saveNamed = true)
    {
        if (Store is null || _saving || _loadingLayout || Controller.IsClosed || !Controller.TryBeginPersistence()) return false;
        _saving = true;
        try
        {
            var state = Controller.Capture() with { Name = (_layoutName.Text ?? "").Trim() };
            if (saveNamed) await Store.Named(state.Name).SaveAsync(state);
            await Store.SaveAsync(state);
            Controller.Name = state.Name;
            if (!Controller.IsClosed) SetStatus("Layout saved.");
            _discardAndClose.IsVisible = false;
            foreach (var window in Controller.Windows) { window.Host?.RefreshNames(); window.Host?.Refresh(); }
            return true;
        }
        catch (Exception)
        {
            SetStatus("Layout could not be saved. Check its name and settings folder, then retry Save layout, or close without saving.");
            _discardAndClose.IsVisible = true; return false;
        }
        finally { _saving = false; Controller.EndPersistence(); }
    }

    internal async Task<bool> LoadAsync(string? name)
    {
        if (Store is null || _saving || _loadingLayout || Controller.IsClosed || !Controller.TryBeginPersistence()) return false;
        _loadingLayout = true;
        try
        {
            var loaded = await (name is null ? Store : Store.Named(name)).LoadAsync();
            if (Controller.IsClosed) return false;
            if (loaded.State is { } state) await Controller.RestoreAsync(state, Model);
            if (loaded.Error is { } error) SetStatus(error);
            else if (name is not null && loaded.State is null) SetStatus("That saved layout is unavailable. The current arrangement was kept.");
            return loaded.State is not null;
        }
        finally
        {
            _loadingLayout = false;
            Controller.EndPersistence();
        }
    }
    private void RefreshNames()
    {
        if (Store is null) return;
        try { _savedLayouts.ItemsSource = Store.Names(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { SetStatus("Saved layout names could not be read."); }
    }
    private void PersistenceCompleted()
    {
        if (!_closeAfterLoad || Controller.IsClosed) return;
        _closeAfterLoad = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(Close);
    }
    internal WindowGeometry CaptureGeometry() => (_normalBounds ?? new(Position.X, Position.Y, Width, Height, false)) with { Maximized = WindowState == WindowState.Maximized };
    private void TrackBounds()
    {
        if (WindowState == WindowState.Normal && Bounds.Width > 0 && Bounds.Height > 0)
            _normalBounds = new(Position.X, Position.Y, Bounds.Width, Bounds.Height, false);
    }
    private void SetStatus(string message) { _status.Text = message; _status.IsVisible = true; }
}
