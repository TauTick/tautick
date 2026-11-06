using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TauTick.Adapters.Databento;
using TauTick.Charting;
using TauTick.MarketData;

namespace TauTick.Desktop;

public sealed class MainWindow : Window
{
    private readonly Button _open = new() { Name = "OpenCsv", Content = "Open CSV…" };
    private readonly Button _fit = new() { Name = "FitData", Content = "Fit to data" };
    private readonly TextBox _year = new() { Name = "ContractYear", PlaceholderText = "e.g. 2025", Width = 130, MaxLength = 4 };
    private readonly TextBlock _summary = new() { Name = "DatasetSummary", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Name = "LoadStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _error = new() { Name = "LoadError", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed };
    private readonly ProgressBar _loading = new() { IsIndeterminate = true, Height = 3 };
    private readonly ScrollViewer _errorPanel;
    private readonly bool _production;

    // An explicit reader is a development/test seam; normal startup always uses the real adapter.
    public MainWindow(IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null, string? notice = null)
    {
        Title = "TauTick — CSV viewer";
        Width = 1000;
        Height = 680;
        MinWidth = 560;
        MinHeight = 440;
        Chart = new BarChart();
        _production = reader is null;
        Session = new CsvViewerSession(
            reader is null ? () => new DatabentoMinuteCsvReader(ContractYear!.Value) : () => reader,
            picker ?? new NativeCsvFilePicker(this));
        _errorPanel = new ScrollViewer { MaxHeight = 100, Content = _error };
        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8, IsVisible = _production,
                    Margin = new Thickness(0, 0, 12, 6),
                    Children = { new TextBlock { Text = "Full contract year", VerticalAlignment = VerticalAlignment.Center }, _year }
                },
                _open, _fit
            }
        };
        _open.Margin = new Thickness(0, 0, 8, 6);
        _fit.Margin = new Thickness(0, 0, 0, 6);
        var header = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "TauTick", FontSize = 24 },
                new TextBlock { Text = notice, IsVisible = notice is not null, TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = "Databento minute CSV · Enter the known contract year (2025 for the ESZ5 reference sample).",
                    IsVisible = _production, TextWrapping = TextWrapping.Wrap
                },
                actions, _summary, _status, _loading,
                _errorPanel
            }
        };
        var panel = new DockPanel { Margin = new Thickness(20), LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(new Border
        {
            Margin = new Thickness(0, 16, 0, 0), BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray, Child = Chart
        });
        Content = panel;
        _open.Click += async (_, _) => await Session.OpenAsync();
        _fit.Click += (_, _) => Chart.FitToData();
        _year.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty) Refresh();
        };
        Session.Changed += Refresh;
        Closed += (_, _) =>
        {
            Session.Changed -= Refresh;
            Session.Close();
            Chart.ClearDataset();
        };
        Refresh();
    }

    public BarChart Chart { get; }
    public CsvViewerSession Session { get; }

    private int? ContractYear => _year.Text is { Length: 4 } text
        && text.All(c => c is >= '0' and <= '9')
        && int.TryParse(text, out var year) && year > 0 ? year : null;

    private void Refresh()
    {
        // One UI turn publishes the accepted snapshot and its metadata, with no await in between.
        if (!ReferenceEquals(Chart.Dataset, Session.Dataset))
        {
            if (Session.Dataset is { } data) Chart.SetDataset(data);
            else Chart.ClearDataset();
        }
        _summary.Text = Session.Dataset is { } accepted
            ? DatasetSummary.Format(accepted)
            : "No dataset loaded. Open a local CSV to begin.";
        _open.IsEnabled = !Session.IsBusy && !Session.IsClosed && (!_production || ContractYear is not null);
        _year.IsEnabled = !Session.IsBusy && !Session.IsClosed;
        _fit.IsEnabled = Session.Dataset is not null && !Session.IsClosed;
        _loading.IsVisible = Session.IsBusy;
        _status.Text = Session.Status;
        _error.Text = Session.Error;
        _errorPanel.IsVisible = Session.Error is not null;
    }
}
