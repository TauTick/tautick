using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TauTick.Charting;

namespace TauTick.Desktop.Workspace;

internal sealed class ChartView : UserControl
{
    private readonly ChartDocument _document;
    private readonly TextBox _year = new() { Name = "ContractYear", Width = 76, MaxLength = 4, PlaceholderText = "2025" };
    private readonly Button _open = new() { Name = "OpenCsv", Content = "Open CSV…" };
    private readonly Button _fit = new() { Name = "FitData", Content = "Fit" };
    private readonly TextBlock _summary = new() { Name = "DatasetSummary", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Name = "LoadStatus", FontSize = 11 };
    private readonly TextBlock _error = new() { Name = "LoadError", Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private bool _refreshing;
    private bool _attached;

    internal ChartView(ChartDocument document, Func<Control?>? commands = null)
    {
        _document = document;
        Chart = new BarChart { Name = "BarChart", Focusable = true };
        document.SurfaceCreated(Chart);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(new TextBlock { Text = "Year", VerticalAlignment = VerticalAlignment.Center, IsVisible = document.IsProduction, Margin = new(0, 0, 5, 0) });
        _year.IsVisible = document.IsProduction;
        actions.Children.Add(_year);
        actions.Children.Add(_open);
        actions.Children.Add(_fit);
        if (commands?.Invoke() is { } menu) actions.Children.Add(menu);
        var header = new StackPanel { Spacing = 3, Margin = new(6), Children = { actions, _summary, _status, _error, _notice } };
        var panel = new DockPanel();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(Chart);
        Content = panel;
        _open.Click += async (_, _) => { document.Activate?.Invoke(); await document.Session.OpenAsync(); };
        _fit.Click += (_, _) => { document.Activate?.Invoke(); document.FitToData(); };
        _year.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_refreshing) document.ContractYearText = _year.Text ?? "";
        };
        AddHandler(PointerPressedEvent, (_, _) => document.Activate?.Invoke(), RoutingStrategies.Tunnel, true);
        AddHandler(GotFocusEvent, (_, _) => document.Activate?.Invoke(), RoutingStrategies.Bubble, true);
        KeyDown += async (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key == Key.O && _open.IsEnabled)
            {
                e.Handled = true;
                document.Activate?.Invoke();
                await document.Session.OpenAsync();
            }
            else if (e.Key == Key.D0)
            {
                e.Handled = true;
                document.FitToData();
            }
        };
        Refresh();
    }

    internal BarChart Chart { get; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_attached) return;
        _attached = true;
        _document.Changed += Refresh;
        _document.FitRequested += Chart.FitToData;
        Chart.VisibleTimeRangeChanged += RangeChanged;
        _document.WindowOwner = GetWindow;
        _document.SurfaceCreated(Chart);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _document.Changed -= Refresh;
        _document.FitRequested -= Chart.FitToData;
        Chart.VisibleTimeRangeChanged -= RangeChanged;
        if (_document.WindowOwner == GetWindow) _document.WindowOwner = null;
        Chart.ClearDataset();
        base.OnDetachedFromVisualTree(e);
    }

    private Window? GetWindow() => TopLevel.GetTopLevel(this) as Window;
    private void RangeChanged() { if (!_refreshing) _document.SetVisibleTimeRange(Chart.VisibleTimeRange); }

    private void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var session = _document.Session;
            if (!ReferenceEquals(Chart.Dataset, session.Dataset))
            {
                if (session.Dataset is { } data) Chart.SetDataset(data);
                else Chart.ClearDataset();
            }
            if (_document.VisibleTimeRange is { } range && !Equals(Chart.VisibleTimeRange, range)) Chart.TryRestoreTimeRange(range);
            _year.Text = _document.ContractYearText;
            _year.IsEnabled = !session.IsBusy && !session.IsClosed && !_document.IsRestoring;
            _open.IsEnabled = !session.IsBusy && !session.IsClosed && !_document.IsRestoring && (!_document.IsProduction || _document.ContractYear is not null);
            _fit.IsEnabled = session.Dataset is not null && !session.IsClosed;
            _summary.Text = session.Dataset is { } accepted ? DatasetSummary.Format(accepted) : "No dataset loaded.";
            ToolTip.SetTip(_summary, _summary.Text);
            _status.Text = _document.IsRestoring && !session.IsBusy ? "Waiting to verify saved input…" : session.Status;
            _status.IsVisible = session.IsBusy || session.Error is not null || session.Dataset is null;
            _error.Text = session.Error;
            _error.IsVisible = session.Error is not null;
            _notice.Text = _document.Notice;
            _notice.IsVisible = _document.Notice is not null;
        }
        finally { _refreshing = false; }
    }
}
