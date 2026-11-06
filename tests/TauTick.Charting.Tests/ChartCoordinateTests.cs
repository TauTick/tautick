using System.Collections;
using TauTick.MarketData;

namespace TauTick.Charting.Tests;

public sealed class ChartCoordinateTests
{
    [Fact]
    public void CoordinatesRespectOhlcAndIntervalCentersIncludingTheGap()
    {
        var data = ChartFixtures.Cases();
        var viewport = ChartViewport.Fit(data);
        var coordinates = new ChartCoordinates(viewport, 10, 20, 500, 200);
        Assert.Equal(97, viewport.Low);
        Assert.Equal(105, viewport.High);
        Assert.Equal(10, coordinates.X(data.Coverage.StartUtc.Ticks));
        Assert.Equal(510, coordinates.X(data.Coverage.EndUtc.Ticks));
        Assert.Equal(new double[] { 60, 160, 260, 460 }, data.Bars.Select(coordinates.CenterX));
        Assert.Equal(36, coordinates.Y(105), 10);
        Assert.Equal(204, coordinates.Y(97), 10);
        Assert.Equal(120, coordinates.Y(101), 10);
    }

    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(1, 2, 1, 2)]
    [InlineData(3, 4, 3, 3)]
    [InlineData(3, 5, 3, 4)]
    [InlineData(-3, 0, 0, 0)]
    [InlineData(5, 7, 4, 4)]
    public void VisibleRangeUsesHalfOpenIntersection(int start, int end, int first, int last)
    {
        var data = ChartFixtures.Cases();
        Assert.Equal(new VisibleBarRange(first, last), VisibleBarRange.Find(data.Bars,
            ChartFixtures.Start.AddMinutes(start).Ticks, ChartFixtures.Start.AddMinutes(end).Ticks));
    }

    [Fact]
    public void PartialIntervalsStayVisibleAndLargeHistoriesUseLogarithmicLookup()
    {
        var bars = new CountingBars(ChartFixtures.History(100_000).Bars);
        var start = ChartFixtures.Start.AddMinutes(50_000).AddSeconds(30);
        var end = start.AddMinutes(1_000);
        var range = VisibleBarRange.Find(bars, start.Ticks, end.Ticks);
        Assert.Equal(new VisibleBarRange(50_000, 51_001), range);
        Assert.InRange(bars.Reads, 1, 36);
    }

    [Fact]
    public void SubMicrosecondIntervalsUseRelativeTicksEvenAtTheEndOfTheCalendar()
    {
        var start = DateTimeOffset.MaxValue.AddTicks(-10);
        var data = ChartFixtures.Dataset(new(start, TimeSpan.FromTicks(1), 0, 1, -1, 0, 0),
            new(start.AddTicks(2), TimeSpan.FromTicks(1), 0, 1, -1, 0, 0));
        var coordinates = new ChartCoordinates(ChartViewport.Fit(data), 0, 0, 300, 100);
        Assert.Equal(50, coordinates.CenterX(data.Bars[0]));
        Assert.Equal(250, coordinates.CenterX(data.Bars[1]));
        Assert.Equal(300, coordinates.X(start.AddTicks(3).Ticks));
    }

    [Fact]
    public void FlatNegativeTinyAndExtremePricesRemainFiniteAndDistinct()
    {
        var flat = new ChartViewport(0, 1, -10, -10);
        Assert.Equal(0.5, flat.PriceFraction(-10));
        foreach (var (low, high) in new[] { (0m, 0.0000000000000000000000000001m),
                     (decimal.MaxValue - 2, decimal.MaxValue), (decimal.MinValue, decimal.MaxValue) })
        {
            var viewport = new ChartViewport(0, 1, low, high);
            Assert.Equal(0, viewport.PriceFraction(low));
            Assert.Equal(1, viewport.PriceFraction(high));
            var ticks = PriceTicks.Create(viewport, 6);
            Assert.InRange(ticks.Count, 1, 10);
            Assert.All(ticks, tick => Assert.InRange(tick, low, high));
        }
        var adjacent = new ChartViewport(0, 1, decimal.MaxValue - 2, decimal.MaxValue);
        Assert.Equal(0.5, adjacent.PriceFraction(decimal.MaxValue - 1));
    }

    private sealed class CountingBars(IReadOnlyList<Bar> bars) : IReadOnlyList<Bar>
    {
        public int Reads { get; private set; }
        public int Count => bars.Count;
        public Bar this[int index] { get { Reads++; return bars[index]; } }
        public IEnumerator<Bar> GetEnumerator() => throw new InvalidOperationException("Lookup must not scan history.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
