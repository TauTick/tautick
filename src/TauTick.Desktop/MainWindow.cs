using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TauTick.Charting;
using TauTick.Desktop.Development;
using TauTick.MarketData;

namespace TauTick.Desktop;

public sealed class MainWindow : Window
{
    private readonly Button _open = new() { Name = "OpenCsv", Content = "Open CSV…" };
    private readonly TextBlock _summary = new() { Name = "DatasetSummary", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Name = "LoadStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _error = new() { Name = "LoadError", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed };
    private readonly ProgressBar _loading = new() { IsIndeterminate = true, Height = 3 };
    private readonly ScrollViewer _errorPanel;

    // TAU-5 replaces this development reader at the composition entry point.
    public MainWindow() : this(new SyntheticPreviewReader(), notice: SyntheticPreviewReader.Notice) { }

    public MainWindow(IHistoricalBarReader reader, ICsvFilePicker? picker = null, string? notice = null)
    {
        Title = "TauTick — CSV viewer";
        Width = 1000;
        Height = 680;
        MinWidth = 560;
        MinHeight = 440;
        Chart = new BarChart();
        Session = new CsvViewerSession(reader, picker ?? new NativeCsvFilePicker(this));
        _errorPanel = new ScrollViewer { MaxHeight = 100, Content = _error };
        var header = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "TauTick", FontSize = 24 },
                new TextBlock { Text = notice, IsVisible = notice is not null, TextWrapping = TextWrapping.Wrap },
                _open, _summary, _status, _loading,
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
        _open.IsEnabled = !Session.IsBusy && !Session.IsClosed;
        _loading.IsVisible = Session.IsBusy;
        _status.Text = Session.Status;
        _error.Text = Session.Error;
        _errorPanel.IsVisible = Session.Error is not null;
    }
}
