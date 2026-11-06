using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TauTick.Charting;
using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Desktop;

/// <summary>Foundation demo only. The later desktop task owns file selection and loading UI.</summary>
public sealed class MainWindow : Window
{
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };

    public MainWindow()
    {
        Title = "TauTick — synthetic foundation demo";
        Width = 900;
        Height = 560;
        MinWidth = 480;
        MinHeight = 320;

        Chart = new BarChart();
        var header = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "TauTick · Synthetic example", FontSize = 24 },
                _summary,
                new TextBlock
                {
                    Text = "Chart input is connected. Candle drawing and CSV import are future tasks.",
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        var panel = new DockPanel { Margin = new Thickness(24), LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(new Border
        {
            Margin = new Thickness(0, 24, 0, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Child = Chart
        });
        Content = panel;
        Opened += LoadSyntheticExample;
        Closed += (_, _) => Chart.ClearDataset();
    }

    public BarChart Chart { get; }

    private async void LoadSyntheticExample(object? sender, EventArgs args)
    {
        const string sourceName = "synthetic-in-memory";
        IHistoricalBarReader reader = new InMemoryBarReader(new BarReadResult.Success(SyntheticBars.Create(sourceName)));
        using var input = new MemoryStream(); // The fake ignores bytes; the caller still owns the stream.
        var result = await reader.ReadAsync(input, sourceName);
        if (result is not BarReadResult.Success success)
            throw new InvalidOperationException("The synthetic demo must be configured with a successful result.");

        Chart.SetDataset(success.Dataset);
        var data = success.Dataset;
        _summary.Text = FormattableString.Invariant(
            $"{data.Instrument.Symbol} · {data.Bars.Count} invented one-minute bars · [{data.Coverage.StartUtc:yyyy-MM-dd HH:mm}, {data.Coverage.EndUtc:HH:mm}) UTC · includes a gap · tick size unknown");
    }
}
