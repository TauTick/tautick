using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TauTick.Charting;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;

namespace TauTick.Desktop;

public sealed class MainWindow : Window
{
    private readonly WorkspaceStore? _store;
    private readonly TextBlock _status = new() { Name = "WorkspaceStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 11 };
    private bool _allowClose;
    private bool _saving;
    private bool _loadingLayout;
    private bool _closeAfterLoad;
    private readonly Button _discardAndClose = new() { Content = "Close without saving", IsVisible = false };
    private WindowGeometry? _normalBounds;

    // Explicit readers/pickers remain development seams. Persistence is enabled by the app composition.
    public MainWindow(IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null, string? notice = null, WorkspaceStore? store = null)
    {
        Title = "TauTick — Workspace";
        Width = 1200; Height = 800; MinWidth = 560; MinHeight = 380;
        _store = store;
        Controller = new(reader, picker);
        var add = new Button { Name = "AddWorkspace", Content = "+ Workspace" };
        var save = new Button { Name = "SaveWorkspace", Content = "Save layout", IsVisible = store is not null };
        add.Click += (_, _) => Controller.AddWorkspace();
        save.Click += async (_, _) => await SaveAsync();
        _discardAndClose.Click += (_, _) => { _allowClose = true; Close(); };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(8, 4),
            Children = { new TextBlock { Text = "TauTick", FontSize = 18, VerticalAlignment = VerticalAlignment.Center }, add, save, _discardAndClose } };
        var header = new StackPanel { Children = { actions, _status } };
        _status.Text = notice;
        _status.IsVisible = notice is not null;
        var root = new DockPanel();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(Controller.Surface);
        Content = root;
        Controller.Changed += () => { if (Controller.Notice is { } text) SetStatus(text); };
        Opened += async (_, _) =>
        {
            TrackBounds();
            if (_store is null) return;
            _loadingLayout = true;
            var loaded = await _store.LoadAsync();
            _loadingLayout = false;
            if (Controller.IsClosed) return;
            if (loaded.Error is { } error) SetStatus(error);
            if (loaded.State is { } state)
            {
                state.MainWindow?.Apply(this);
                var restoring = Controller.RestoreAsync(state);
                if (_closeAfterLoad) Close();
                await restoring;
            }
            else if (_closeAfterLoad) Close();
        };
        PositionChanged += (_, _) => TrackBounds();
        SizeChanged += (_, _) => TrackBounds();
        Closing += async (_, e) =>
        {
            if (_allowClose || _store is null) return;
            e.Cancel = true;
            if (_loadingLayout) { _closeAfterLoad = true; return; }
            if (_saving) return;
            if (await SaveAsync()) { _allowClose = true; Close(); }
        };
        Closed += (_, _) => Controller.Close();
    }

    internal WorkspaceController Controller { get; }
    public IChartHost ChartHost => Controller;
    // Compatibility with the original single-view development hosts; new features use ChartHost.
    public BarChart Chart => Controller.InitialChart.LatestChart;
    public CsvViewerSession Session => Controller.InitialChart.Session;

    public async Task<bool> SaveAsync()
    {
        if (_store is null || _saving || _loadingLayout || Controller.IsClosed) return false;
        _saving = true;
        try
        {
            var geometry = (_normalBounds ?? new(Position.X, Position.Y, Width, Height, false)) with { Maximized = WindowState == WindowState.Maximized };
            await _store.SaveAsync(Controller.Capture(geometry));
            if (!Controller.IsClosed) SetStatus("Layout saved.");
            _discardAndClose.IsVisible = false;
            return true;
        }
        catch (Exception)
        {
            SetStatus("Layout could not be saved. Check the settings folder and retry Save layout, or close without saving.");
            _discardAndClose.IsVisible = true;
            return false;
        }
        finally { _saving = false; }
    }

    private void TrackBounds()
    {
        if (WindowState == WindowState.Normal && Bounds.Width > 0 && Bounds.Height > 0)
            _normalBounds = new(Position.X, Position.Y, Bounds.Width, Bounds.Height, false);
    }

    private void SetStatus(string message) { _status.Text = message; _status.IsVisible = true; }
}
