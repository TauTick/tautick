using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TauTick.Charting;
using TauTick.UI;

namespace TauTick.Desktop.Workspace;

internal sealed class ChartView : UserControl
{
    private readonly ChartDocument _document;
    private readonly ChartToolbar _toolbar = new();
    private readonly TextBlock _summary = new() { Name = "DatasetSummary", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Name = "LoadStatus", FontSize = 11 };
    private readonly TextBlock _error = new() { Name = "LoadError", Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly StackPanel _legend = new() { Spacing = 2 };
    private bool _refreshing;
    private bool _attached;

    internal ChartView(ChartDocument document, Func<Control?>? commands = null)
    {
        _document = document;
        Chart = new BarChart { Name = "BarChart", Focusable = true };
        document.SurfaceCreated(Chart);
        _toolbar.ShowContractYear = document.IsProduction;
        _toolbar.IndicatorsRequested += (_, _) => document.OpenIndicators();
        var indicatorMenu = new MenuItem { Header = "Indicators… (Ctrl+I)" };
        indicatorMenu.Click += (_, _) => document.OpenIndicators();
        Chart.ContextMenu = new ContextMenu { ItemsSource = new[] { indicatorMenu } };
        if (commands?.Invoke() is { } menu) _toolbar.AdditionalContent = menu;
        var header = new StackPanel { Spacing = 3, Margin = new(6), Children = { _toolbar, _summary, _status, _error, _notice, _legend } };
        var panel = new DockPanel();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(Chart);
        Content = panel;
        _toolbar.OpenRequested += async (_, _) => { document.Activate?.Invoke(); await document.Session.OpenAsync(); };
        _toolbar.FitRequested += (_, _) => { document.Activate?.Invoke(); document.FitToData(); };
        _toolbar.PropertyChanged += (_, e) =>
        {
            if (e.Property == ChartToolbar.ContractYearTextProperty && !_refreshing) document.ContractYearText = _toolbar.ContractYearText ?? "";
        };
        AddHandler(PointerPressedEvent, (_, _) => document.Activate?.Invoke(), RoutingStrategies.Tunnel, true);
        AddHandler(GotFocusEvent, (_, _) => document.Activate?.Invoke(), RoutingStrategies.Bubble, true);
        KeyDown += async (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key == Key.O && _toolbar.CanOpen)
            {
                e.Handled = true;
                document.Activate?.Invoke();
                await document.Session.OpenAsync();
            }
            else if (e.Key == Key.I) { e.Handled = true; document.OpenIndicators(); }
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
        _document.Studies.Changed += RefreshStudies;
        _document.FitRequested += Chart.FitToData;
        Chart.VisibleTimeRangeChanged += RangeChanged;
        if (!_document.IsWorkspaceHosted) _document.WindowOwner = GetWindow;
        _document.SurfaceCreated(Chart);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _document.Changed -= Refresh;
        _document.Studies.Changed -= RefreshStudies;
        _document.FitRequested -= Chart.FitToData;
        Chart.VisibleTimeRangeChanged -= RangeChanged;
        if (_document.WindowOwner == GetWindow) _document.WindowOwner = null;
        Chart.ClearDataset();
        base.OnDetachedFromVisualTree(e);
    }

    private void RefreshStudies()
    {
        Chart.SetStudies(_document.Studies.Visuals);
        _legend.Children.Clear();
        foreach (var instance in _document.Studies.Instances)
        {
            var row = new WrapPanel();
            var description = instance.ToString();
            if (instance.Saved.Descriptor.TypeId == TauTick.Studies.VwapDefinition.Id)
            {
                try
                {
                    var settings = System.Text.Json.JsonSerializer.Deserialize<TauTick.Studies.VwapSettings>(instance.Saved.Descriptor.Settings);
                    description += $" · {settings?.Price} · anchor " + (settings?.AnchorUtcTicks is { } ticks
                        ? new DateTimeOffset(ticks, TimeSpan.Zero).ToString("u") : "dataset start") + " · bar estimate";
                }
                catch { description += " · unsupported settings"; }
            }
            var label = new TextBlock { Text = description, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(label, instance.Message);
            row.Children.Add(label);
            foreach (var (title, action) in new (string, Action)[] {
                ("Settings", () => _document.OpenIndicators(instance)),
                (instance.Saved.Descriptor.Visible ? "Hide" : "Show", () => _document.Studies.SetVisible(instance, !instance.Saved.Descriptor.Visible)),
                ("↑", () => _document.Studies.Move(instance, -1)), ("↓", () => _document.Studies.Move(instance, 1)),
                ("Remove", () => _document.Studies.Remove(instance)) })
            {
                var button = new Button { Content = title, Padding = new(5, 2), Margin = new(2, 0) };
                button.Click += (_, _) => action(); row.Children.Add(button);
            }
            _legend.Children.Add(row);
        }
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
            _toolbar.ContractYearText = _document.ContractYearText;
            _toolbar.CanEditYear = !session.IsBusy && !session.IsClosed && !_document.IsRestoring;
            _toolbar.CanOpen = !session.IsBusy && !session.IsClosed && !_document.IsRestoring && (!_document.IsProduction || _document.ContractYear is not null);
            _toolbar.CanFit = session.Dataset is not null && !session.IsClosed;
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
        RefreshStudies();
    }
}
