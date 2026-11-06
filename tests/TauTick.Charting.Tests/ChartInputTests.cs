using Avalonia.Headless.XUnit;
using TauTick.Desktop;
using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Charting.Tests;

public sealed class ChartInputTests
{
    [AvaloniaFact]
    public async Task ReaderToChartPreservesSnapshotAcrossReplacementAndClearing()
    {
        var first = SyntheticBars.Create("first-synthetic-source");
        IHistoricalBarReader reader = new InMemoryBarReader(new BarReadResult.Success(first));
        using var input = new MemoryStream();
        var result = Assert.IsType<BarReadResult.Success>(await reader.ReadAsync(input, first.Source.SourceName));
        var chart = new BarChart();
        chart.SetDataset(result.Dataset);
        var retained = Assert.IsType<BarDataset>(chart.Dataset);
        Assert.Same(first, retained);
        Assert.Equal(first.Bars, retained.Bars);
        Assert.Equal(first.Instrument, retained.Instrument);
        Assert.Equal(first.Source, retained.Source);
        Assert.Equal(first.Coverage, retained.Coverage);

        var next = new BarDataset(first.Instrument,
            new DataSourceIdentity("Synthetic", "Invented replacement", "ohlcv-1m", "second-synthetic-source"),
            first.Bars.Take(2));
        chart.SetDataset(next);
        Assert.Same(next, chart.Dataset);
        Assert.False(Assert.IsType<BarDataset>(chart.Dataset).Coverage.HasGaps);
        chart.ClearDataset();
        chart.ClearDataset();
        Assert.Null(chart.Dataset);
        Assert.Equal(3, retained.Bars.Count);
        Assert.True(retained.Coverage.HasGaps);
    }

    [AvaloniaFact]
    public void NullReplacementDoesNotDiscardTheCurrentSnapshot()
    {
        var chart = new BarChart();
        var data = SyntheticBars.Create();
        chart.SetDataset(data);
        Assert.Throws<ArgumentNullException>(() => chart.SetDataset(null!));
        Assert.Same(data, chart.Dataset);
    }

    [AvaloniaFact]
    public async Task ControlRejectsWorkerThreadMutation()
    {
        var chart = new BarChart();
        var data = SyntheticBars.Create();
        chart.SetDataset(data);
        await Task.Run(() =>
        {
            Assert.Throws<InvalidOperationException>(() => chart.SetDataset(data));
            Assert.Throws<InvalidOperationException>(() => chart.ClearDataset());
        });
        Assert.Same(data, chart.Dataset);
    }

    [AvaloniaFact]
    public void DemoWindowLoadsTheSyntheticFixtureAndReleasesItOnClose()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            var data = Assert.IsType<BarDataset>(window.Chart.Dataset);
            Assert.Equal("SYNTH-Z25", data.Instrument.Symbol);
            Assert.Equal("Synthetic", data.Source.Provider);
            Assert.Equal(SyntheticBars.Create().Bars, data.Bars);
            Assert.Equal(SyntheticBars.Create().Coverage, data.Coverage);
        }
        finally
        {
            window.Close();
        }
        Assert.Null(window.Chart.Dataset);
    }
}
