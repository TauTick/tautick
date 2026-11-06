using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace TauTick.Charting.Tests;

public sealed class ChartRenderingTests
{
    [AvaloniaTheory]
    [InlineData(610000000L, 150000000L, "09:30:15")]
    [InlineData(11000000L, 1000000L, "09:30:00.1000000")]
    [InlineData(610000000L, 150000001L, "09:30:15.0000001")]
    public void UnalignedFallbackTimeLabelsPreserveTheActualTick(long intervalTicks, long startOffsetTicks, string expectedTime)
    {
        var start = ChartFixtures.Start.AddTicks(startOffsetTicks);
        var data = ChartFixtures.Dataset(new TauTick.MarketData.Bar(start, TimeSpan.FromTicks(intervalTicks), 100, 101, 99, 100, 0));
        var layout = Assert.IsType<ChartLayout>(ChartLayout.Create(data, ChartViewport.Fit(data), new(240, 300), 1));
        var label = Assert.Single(layout.Times);
        Assert.Equal(layout.Coordinates.X(start.Ticks), label.Coordinate);
        Assert.Equal($"2025-09-22\n{expectedTime}", label.Value);
        Assert.True(label.Origin.X + label.Text.Width <= layout.Plot.Right);
    }

    [AvaloniaFact]
    public void DisplayScaleNotificationsRebuildDevicePixelGeometryWithoutChangingIntervals()
    {
        var chart = new BarChart();
        var data = ChartFixtures.Cases();
        chart.SetDataset(data);
        var window = new Window { Width = 900, Height = 500, Content = chart };
        try
        {
            window.Show();
            var original = Assert.IsType<ChartLayout>(chart.PreparedLayout);
            foreach (var scale in new[] { 2.0, 1.25, 1.0 })
            {
                window.SetRenderScaling(scale);
                var scaled = Assert.IsType<ChartLayout>(chart.PreparedLayout);
                Assert.NotSame(original, scaled);
                Assert.Equal(1 / scale, scaled.Pixel);
                Assert.Equal(1 / scale, scaled.Candles[2].Body.Height, 10);
                Assert.Same(data, chart.Dataset);
                Assert.Equal(original.Coordinates.Viewport.StartTicks, chart.Viewport!.StartTicks);
                Assert.Equal(original.Coordinates.Viewport.EndTicks, chart.Viewport.EndTicks);
                Assert.All(scaled.Times, label => Assert.InRange(label.Origin.X, scaled.Plot.Left, scaled.Plot.Right));
                original = scaled;
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LayoutHasExactBodiesWicksAxesAndScaleAwareDoji()
    {
        var data = ChartFixtures.Cases();
        var viewport = ChartViewport.Fit(data);
        var layout = Assert.IsType<ChartLayout>(ChartLayout.Create(data, viewport, new(900, 500), 2));
        Assert.Equal(new[] { 1, -1, 0, 0 }, layout.Candles.Select(c => c.Direction));
        for (var i = 0; i < data.Bars.Count; i++)
        {
            var candle = layout.Candles[i];
            var bar = data.Bars[i];
            Assert.Equal(layout.Coordinates.Y(bar.High), candle.HighY);
            Assert.Equal(layout.Coordinates.Y(bar.Low), candle.LowY);
            if (bar.Open != bar.Close)
            {
                Assert.Equal(layout.Coordinates.Y(Math.Max(bar.Open, bar.Close)), candle.Body.Top, 10);
                Assert.Equal(layout.Coordinates.Y(Math.Min(bar.Open, bar.Close)), candle.Body.Bottom, 10);
            }
            else Assert.Equal(0.5, candle.Body.Height);
        }
        Assert.NotEmpty(layout.Times);
        Assert.All(layout.Prices, tick => Assert.True(tick.Value.Length <= 5));
        Assert.All(layout.Times, tick => Assert.Contains("2025-09-22", tick.Value));
        Assert.All(layout.Prices, tick => Assert.InRange(tick.Coordinate, layout.Plot.Top, layout.Plot.Bottom));
        var single = ChartFixtures.Dataset(data.Bars[3]);
        var flat = Assert.IsType<ChartLayout>(ChartLayout.Create(single, ChartViewport.Fit(single), new(900, 500), 1));
        Assert.Single(flat.Prices);
        Assert.Equal("100", flat.Prices[0].Value);
        Assert.InRange(Math.Abs(flat.Plot.Center.Y - flat.Candles[0].Body.Center.Y), 0, 1);
    }

    [AvaloniaFact]
    public void ReplacementClearingResizeAndBoundedViewportInvalidatePreparedContent()
    {
        var chart = new BarChart();
        Arrange(chart, 900, 500);
        Assert.Null(chart.PreparedLayout);
        var first = ChartFixtures.Cases();
        chart.SetDataset(first);
        var initial = Assert.IsType<ChartLayout>(chart.PreparedLayout);
        Assert.Same(initial, chart.PreparedLayout);
        chart.SetVisibleTimeRange(ChartFixtures.Start.AddMinutes(1), ChartFixtures.Start.AddMinutes(3));
        var bounded = Assert.IsType<ChartLayout>(chart.PreparedLayout);
        Assert.Equal(2, bounded.Candles.Length);
        Arrange(chart, 600, 320);
        var resized = Assert.IsType<ChartLayout>(chart.PreparedLayout);
        Assert.True(resized.Plot.Width < bounded.Plot.Width);
        Assert.True(resized.Plot.Height < bounded.Plot.Height);
        chart.SetDataset(ChartFixtures.Dataset(first.Bars[3]));
        Assert.Single(Assert.IsType<ChartLayout>(chart.PreparedLayout).Candles);
        Assert.Equal(first.Bars[3].StartUtc.Ticks, chart.Viewport!.StartTicks);
        chart.ClearDataset();
        Assert.Null(chart.PreparedLayout);
        Assert.Null(chart.Viewport);
        chart.SetDataset(first);
        Arrange(chart, 20, 20);
        Assert.Null(chart.PreparedLayout);
        Arrange(chart, 900, 500);
        Assert.Equal(4, Assert.IsType<ChartLayout>(chart.PreparedLayout).Candles.Length);
    }

    [AvaloniaFact]
    public void SoftwareRasterContainsBodiesWicksDojiGapAndNoStaleCandlesAfterClear()
    {
        var chart = new BarChart();
        chart.SetDataset(ChartFixtures.Cases());
        var window = new Window { Width = 900, Height = 500, Content = chart };
        try
        {
            window.Show();
            Arrange(chart, 900, 500);
            var layout = Assert.IsType<ChartLayout>(chart.PreparedLayout);
            using var pixels = Raster(chart);
            AssertPixelNear(pixels, layout.Candles[0].Body.Center, SKColor.Parse("45D6B0"));
            AssertPixelNear(pixels, layout.Candles[1].Body.Center, SKColor.Parse("FF7285"));
            AssertPixelNear(pixels, layout.Candles[2].Body.Center, SKColor.Parse("F4CF78"));
            var wick = layout.Candles[0];
            AssertPixelNear(pixels, new(wick.X, (wick.HighY + wick.Body.Top) / 2), SKColor.Parse("45D6B0"));
            var gapX = layout.Coordinates.X(ChartFixtures.Start.AddMinutes(3).AddSeconds(30).Ticks);
            for (var y = (int)layout.Plot.Top; y < layout.Plot.Bottom; y++)
                Assert.False(IsCandle(pixels.GetPixel((int)gapX, y)));
            chart.ClearDataset();
            using var cleared = Raster(chart);
            Assert.Equal(SKColor.Parse("101820"), cleared.GetPixel((int)wick.Body.Center.X, (int)wick.Body.Center.Y));
            chart.SetDataset(ChartFixtures.Dataset(new TauTick.MarketData.Bar(ChartFixtures.Start.AddDays(1), ChartFixtures.Minute, -10, -10, -10, -10, 0)));
            using var replaced = Raster(chart);
            AssertPixelNear(replaced, Assert.IsType<ChartLayout>(chart.PreparedLayout).Candles[0].Body.Center, SKColor.Parse("F4CF78"));
        }
        finally { window.Close(); }
    }

    private static bool IsCandle(SKColor color) => color.Green > 100 && color.Red < 100
        || color.Red > 200 && color.Blue < 170;

    private static void AssertPixelNear(SKBitmap bitmap, Point point, SKColor color)
    {
        // A one-device-pixel stroke can straddle pixels; allow the adjacent pixel and antialiasing.
        var found = false;
        for (var x = (int)point.X - 1; x <= (int)point.X + 1; x++)
        for (var y = (int)point.Y - 1; y <= (int)point.Y + 1; y++)
        {
            var actual = bitmap.GetPixel(x, y);
            found |= Math.Abs(actual.Red - color.Red) < 70 && Math.Abs(actual.Green - color.Green) < 70
                && Math.Abs(actual.Blue - color.Blue) < 70;
        }
        Assert.True(found, $"Expected {color} near {point}.");
    }

    private static SKBitmap Raster(BarChart chart)
    {
        using var target = new RenderTargetBitmap(new(900, 500), new(96, 96));
        target.Render(chart);
        using var stream = new MemoryStream();
        target.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private static void Arrange(BarChart chart, double width, double height)
    {
        chart.Measure(new(width, height));
        chart.Arrange(new(0, 0, width, height));
    }
}
