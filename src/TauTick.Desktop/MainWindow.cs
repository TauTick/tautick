using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly WindowHeader _header = new();
    private ContentTabBar _tabs => _header.Tabs;
    private readonly Button _retrySave = new() { Name = "RetryWorkspaceSave", Content = "Retry", IsVisible = false };
    private readonly Button _discardAndClose = new() { Content = "Close without saving", IsVisible = false };
    private bool _allowClose;
    private bool _layoutClose;
    private bool _saving;
    private bool _loadingLayout;
    private bool _resumeCloseAfterPersistence;
    private CloseIntent _closeIntent;
    private enum CloseIntent { None, Window, Application }
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
        WindowHeader.ConfigureWindow(this);
        Controller = controller; Model = model ?? controller.Windows[0]; Model.Host = this;
        Store = store; _startup = startup;
        _header.AddChartRequested += () => Controller.AddChart(Model.SelectedTab);
        _header.ExitRequested += async () => await ExitAsync();
        _retrySave.Click += async (_, _) => await RetrySaveAsync();
        _discardAndClose.Click += (_, _) => CompleteClose();
        AutomationProperties.SetAutomationId(_status, "WorkspaceStatus");
        AutomationProperties.SetAutomationId(_retrySave, "RetryWorkspaceSave");
        AutomationProperties.SetAutomationId(_discardAndClose, "CloseWithoutSaving");
        var recovery = new WrapPanel { Children = { _retrySave, _discardAndClose } };
        var header = new StackPanel { Children = { _header, _status, recovery } };
        _status.IsVisible = false;
        var root = new DockPanel(); DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        root.Children.Add(header); root.Children.Add(_content); Content = root;
        _tabs.AddRequested += () => Controller.AddTab(Model);
        _tabs.SelectionRequested += id => Controller.SelectTab(Model, Model.Tabs.Single(t => t.Id.ToString() == id));
        _tabs.CloseRequested += id => Controller.CloseTab(Model.Tabs.Single(t => t.Id.ToString() == id));
        _tabs.DetachRequested += id => Controller.DetachTab(Model.Tabs.Single(t => t.Id.ToString() == id));
        _tabs.RenameRequested += (id, name) => Controller.Rename(Model.Tabs.Single(t => t.Id.ToString() == id), name);
        _tabs.DragMoved += drag => Drag(drag, false);
        _tabs.DragCompleted += drag => Drag(drag, true);
        _tabs.DragCancelled += ClearInsertion;
        Controller.Changed += Refresh;
        Controller.PersistenceCompleted += PersistenceCompleted;
        Activated += (_, _) => Controller.ActivateWindow(Model);
        Opened += async (_, _) =>
        {
            TrackBounds();
            if (!_startup || Store is null) return;
            await LoadAsync(null);
        };
        PositionChanged += (_, _) => TrackBounds(); SizeChanged += (_, _) => TrackBounds();
        Closing += async (_, e) =>
        {
            _tabs.CancelDrag();
            if (_allowClose || Store is null) return;
            if (Controller.Windows.Count > 1 && !Controller.PersistenceBusy) return;
            e.Cancel = true;
            if (_closeIntent != CloseIntent.Application) _closeIntent = CloseIntent.Window;
            if (Controller.PersistenceBusy) { _resumeCloseAfterPersistence = true; return; }
            await SaveAndCloseAsync();
        };
        Closed += (_, _) =>
        {
            Controller.Changed -= Refresh;
            Controller.PersistenceCompleted -= PersistenceCompleted;
            if (!_layoutClose) Controller.CloseWindow(Model);
        };
        KeyDown += async (_, e) =>
        {
            if (e.Handled || e.KeyModifiers != KeyModifiers.Control || Model.SelectedTab is not { } tab
                || tab.ActiveChartId is not { } id || !tab.Charts.TryGetValue(id, out var chart)) return;
            if (e.Key == Key.O)
            {
                e.Handled = true;
                if (!chart.IsRestoring && !chart.Session.IsBusy && (!chart.IsProduction || chart.ContractYear is not null))
                { Controller.ActivateChart(id); await chart.Session.OpenAsync(); }
            }
            else if (e.Key == Key.D0) { e.Handled = true; Controller.ActivateChart(id); chart.FitToData(); }
            else if (e.Key == Key.I) { e.Handled = true; chart.OpenIndicators(); }
        };
        Refresh();
    }

    internal void ReleaseContent() => _content.Content = null;
    internal void CloseForLayout() { _layoutClose = true; _allowClose = true; Close(); }
    private void Refresh()
    {
        if (Controller.IsClosed) return;
        _tabs.SetTabs(Model.Tabs.Select(t => new ContentTabItem(t.Id.ToString(), t.Name)), Model.SelectedTabId?.ToString());
        var selected = Model.SelectedTab;
        if (selected is not null && !ReferenceEquals(_content.Content, selected.Surface)) _content.Content = selected.Surface;
        if (Controller.Notice is { } notice) SetStatus(notice);
        Title = "TauTick — Workspace";
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

    public async Task<bool> SaveAsync(bool saveNamed = false)
    {
        if (Store is null || _saving || _loadingLayout || Controller.IsClosed || !Controller.TryBeginPersistence()) return false;
        _saving = true;
        try
        {
            var state = Controller.Capture();
            if (saveNamed) await Store.Named(state.Name).SaveAsync(state);
            await Store.SaveAsync(state);
            Controller.Name = state.Name;
            _retrySave.IsVisible = _discardAndClose.IsVisible = false;
            return true;
        }
        catch (Exception)
        {
            SetStatus("The arrangement could not be saved. Check the settings folder and chart settings, then Retry or Close without saving.");
            _retrySave.IsVisible = _discardAndClose.IsVisible = true; return false;
        }
        finally { _saving = false; Controller.EndPersistence(); }
    }

    internal async Task<bool> LoadAsync(string? name)
    {
        if (Store is null || _saving || _loadingLayout || Controller.IsClosed || !Controller.TryBeginPersistence()) return false;
        _loadingLayout = true;
        try
        {
            // Let native initial sizing settle before restoring the visible host.
            // Hold persistence ownership while queued so an early exit cannot
            // overwrite the saved arrangement with the initial empty window.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Loaded);
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
    internal async Task ExitAsync()
    {
        _closeIntent = CloseIntent.Application;
        if (Controller.PersistenceBusy) { _resumeCloseAfterPersistence = true; return; }
        await SaveAndCloseAsync();
    }
    private async Task SaveAndCloseAsync()
    {
        if (Controller.IsClosed) return;
        if (Store is null || (_closeIntent == CloseIntent.Window && Controller.Windows.Count > 1) || await SaveAsync()) CompleteClose();
    }
    private async Task RetrySaveAsync()
    {
        if (_closeIntent == CloseIntent.None) await SaveAsync();
        else await SaveAndCloseAsync();
    }
    private void CompleteClose()
    {
        if (_closeIntent == CloseIntent.Application) Controller.Close();
        else { _allowClose = true; Close(); }
    }
    private void PersistenceCompleted()
    {
        if (!_resumeCloseAfterPersistence || Controller.IsClosed) return;
        _resumeCloseAfterPersistence = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(async () => await SaveAndCloseAsync());
    }
    internal WindowGeometry CaptureGeometry() => (_normalBounds ?? new(Position.X, Position.Y, Width, Height, false)) with { Maximized = WindowState == WindowState.Maximized };
    internal void SetRestoredNormalGeometry(WindowGeometry geometry)
    {
        _normalBounds = geometry with { Maximized = false };
        _header.SetRestoreBounds(new(geometry.Width, geometry.Height), new((int)geometry.X, (int)geometry.Y));
    }
    private void TrackBounds()
    {
        if (WindowState == WindowState.Normal && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0)
            _normalBounds = new(Position.X, Position.Y, Width, Height, false);
    }
    private void SetStatus(string message) { _status.Text = message; _status.IsVisible = true; }
}
