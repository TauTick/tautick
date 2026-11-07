using System.Collections;
using TauTick.MarketData;

namespace TauTick.Charting.Tests;

public sealed class ChartNavigationTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.23)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ZoomPreservesPointerTimeAndFittedPriceScale(double fraction)
    {
        var data = ChartFixtures.History(100);
        var fit = ChartViewport.Fit(data);
        var zoomed = ChartNavigation.Zoom(fit, data, fraction, 0.2);
        var anchorBefore = fit.StartTicks + (decimal)fraction * (fit.EndTicks - fit.StartTicks);
        var anchorAfter = zoomed.StartTicks + (decimal)fraction * (zoomed.EndTicks - zoomed.StartTicks);
        Assert.InRange(Math.Abs(anchorAfter - anchorBefore), 0, 1);
        Assert.Equal(TimeSpan.FromMinutes(20).Ticks, zoomed.EndTicks - zoomed.StartTicks);
        Assert.Equal(fit.Low, zoomed.Low);
        Assert.Equal(fit.High, zoomed.High);
        var restored = ChartNavigation.Zoom(zoomed, data, fraction, 5);
        Assert.Equal(fit.StartTicks, restored.StartTicks);
        Assert.Equal(fit.EndTicks, restored.EndTicks);
    }

    [Fact]
    public void NavigationClampsToOneIntervalAndCoverageIncludingMissingIntervals()
    {
        var data = ChartFixtures.Cases();
        var fit = ChartViewport.Fit(data);
        var minimum = ChartNavigation.Zoom(fit, data, 0.75, double.Epsilon);
        Assert.Equal(data.Interval.Ticks, minimum.EndTicks - minimum.StartTicks);
        // This viewport can contain only the missing interval; navigation never collapses the gap.
        Assert.Equal(ChartFixtures.Start.AddMinutes(3).Ticks, minimum.StartTicks);
        Assert.Equal(0, VisibleBarRange.Find(data.Bars, minimum.StartTicks, minimum.EndTicks).Count);
        var first = ChartNavigation.Pan(minimum, data, -double.MaxValue);
        var last = ChartNavigation.Pan(minimum, data, double.MaxValue);
        Assert.Equal(fit.StartTicks, first.StartTicks);
        Assert.Equal(fit.EndTicks, last.EndTicks);
        var all = ChartNavigation.Zoom(last, data, 0.1, double.MaxValue);
        Assert.Equal(fit.StartTicks, all.StartTicks);
        Assert.Equal(fit.EndTicks, all.EndTicks);
        Assert.Same(fit, ChartNavigation.Pan(fit, data, 5));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(3155378975999999899L)]
    public void ExtremeTimestampsRetainSingleTicksAndNeverOverflow(long start)
    {
        var data = ChartFixtures.Dataset(
            new(new(start, TimeSpan.Zero), TimeSpan.FromTicks(1), -10, -10, -10, -10, 0),
            new(new(start + 99, TimeSpan.Zero), TimeSpan.FromTicks(1), -10, -10, -10, -10, 0));
        var fit = ChartViewport.Fit(data);
        var one = ChartNavigation.Zoom(fit, data, 0.5, 0.01);
        Assert.Equal(1, one.EndTicks - one.StartTicks);
        Assert.InRange(one.StartTicks, start + 49, start + 50);
        Assert.Equal(start, ChartNavigation.Pan(one, data, -1000).StartTicks);
        Assert.Equal(start + 100, ChartNavigation.Pan(one, data, 1000).EndTicks);
        var fullCalendar = ChartFixtures.Dataset(
            new(DateTimeOffset.MinValue, TimeSpan.FromTicks(1), 0, 0, 0, 0, 0),
            new(DateTimeOffset.MaxValue.AddTicks(-1), TimeSpan.FromTicks(1), 0, 0, 0, 0, 0));
        var longFit = ChartViewport.Fit(fullCalendar);
        var zoom = ChartNavigation.Zoom(longFit, fullCalendar, 0.999, 1e-20);
        var end = ChartNavigation.Pan(zoom, fullCalendar, double.MaxValue);
        Assert.Equal(DateTimeOffset.MaxValue.Ticks, end.EndTicks);
        Assert.Equal(1, end.EndTicks - end.StartTicks);
    }

    [Fact]
    public void SingleFlatBarAndInvalidDeltasAreStable()
    {
        var data = ChartFixtures.Dataset(ChartFixtures.Cases().Bars[3]);
        var fit = ChartViewport.Fit(data);
        Assert.Same(fit, ChartNavigation.Zoom(fit, data, 0.5, 0.01));
        Assert.Same(fit, ChartNavigation.Zoom(fit, data, 0.5, 100));
        foreach (var value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        {
            Assert.Same(fit, ChartNavigation.Zoom(fit, data, 0.5, value));
            Assert.Same(fit, ChartNavigation.Pan(fit, data, value));
        }
    }

    [Fact]
    public void InspectionUsesHalfOpenBoundariesAndGapsIncludingCalendarExtremes()
    {
        foreach (var start in new[] { ChartFixtures.Start, DateTimeOffset.MinValue, DateTimeOffset.MaxValue.AddTicks(-50) })
        {
            var bars = ChartFixtures.Dataset(
                new(start, TimeSpan.FromTicks(10), 1, 1, 1, 1, 0),
                new(start.AddTicks(10), TimeSpan.FromTicks(10), 2, 2, 2, 2, 1),
                new(start.AddTicks(40), TimeSpan.FromTicks(10), 3, 3, 3, 3, 2)).Bars;
            var viewport = new ChartViewport(start.Ticks, start.Ticks + 50, 1, 3);
            var coordinates = new ChartCoordinates(viewport, 14, 12, 731.3, 300);
            int At(long offset) => BarInspection.Find(bars, coordinates, coordinates.X(start.Ticks + offset));
            Assert.Equal(0, At(0));
            Assert.Equal(0, At(9));
            Assert.Equal(1, At(10));
            Assert.Equal(-1, At(20));
            Assert.Equal(-1, At(39));
            Assert.Equal(2, At(40));
            Assert.Equal(-1, At(50));
            Assert.Equal(-1, BarInspection.Find(bars, coordinates, coordinates.Left - 1));
            Assert.Equal(-1, BarInspection.Find([], coordinates, coordinates.Left));
        }
    }

    [Fact]
    public void InspectionDoesNotScanLargeHistoryAndFormatsExactSourceValues()
    {
        var data = ChartFixtures.History(100_000);
        var bars = new CountingBars(data.Bars);
        var coordinates = new ChartCoordinates(ChartViewport.Fit(data), 0, 0, 1000, 400);
        Assert.Equal(50_000, BarInspection.Find(bars, coordinates, coordinates.CenterX(data.Bars[50_000])));
        Assert.InRange(bars.Reads, 1, 19);
        var exact = new Bar(ChartFixtures.Start.AddTicks(1), TimeSpan.FromTicks(1),
            1234567890.1234567890123456789m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m, long.MaxValue);
        var text = BarInspection.Format(exact);
        Assert.Contains("2025-09-22 09:30:00.0000001 UTC", text);
        Assert.Contains("O 1234567890.1234567890123456789", text);
        Assert.Contains("H 79228162514264337593543950335", text);
        Assert.Contains("L -79228162514264337593543950335", text);
        Assert.Contains("C 0.0000000000000000000000000001", text);
        Assert.Contains("V 9223372036854775807", text);
    }

    private sealed class CountingBars(IReadOnlyList<Bar> bars) : IReadOnlyList<Bar>
    {
        public int Reads { get; private set; }
        public int Count => bars.Count;
        public Bar this[int index] { get { Reads++; return bars[index]; } }
        public IEnumerator<Bar> GetEnumerator() => throw new InvalidOperationException("No full-history scans.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
