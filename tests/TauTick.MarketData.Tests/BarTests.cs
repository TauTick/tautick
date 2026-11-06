using TauTick.MarketData;

namespace TauTick.MarketData.Tests;

public sealed class BarTests
{
    private static readonly DateTimeOffset Start = new(2025, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PreservesExactPricesSubsecondTimestampAndHalfOpenInterval()
    {
        var start = Start.AddTicks(1_234_567);
        var bar = new Bar(start, TimeSpan.FromMinutes(1), 100.000000001m, 101.123456789m,
            99.000000001m, 100.999999999m, long.MaxValue);

        Assert.Equal(start, bar.StartUtc);
        Assert.Equal(start.AddMinutes(1), bar.EndUtc);
        Assert.Equal(100.000000001m, bar.Open);
        Assert.Equal(101.123456789m, bar.High);
        Assert.Equal(99.000000001m, bar.Low);
        Assert.Equal(100.999999999m, bar.Close);
        Assert.Equal(long.MaxValue, bar.Volume);
    }

    [Theory]
    [InlineData(1, 0, 0, 0)] // Open above high.
    [InlineData(-1, 0, 0, 0)] // Open below low.
    [InlineData(0, 0, 0, 1)] // Close above high.
    [InlineData(0, 0, 0, -1)] // Close below low.
    [InlineData(0, -1, 1, 0)] // Inverted bounds.
    public void RejectsInvalidOhlc(decimal open, decimal high, decimal low, decimal close) =>
        Assert.Throws<ArgumentException>(() => new Bar(Start, TimeSpan.FromMinutes(1), open, high, low, close, 0));

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    public void AcceptsFlatPricesAndKnownZeroVolume(decimal price)
    {
        var bar = new Bar(Start, TimeSpan.FromMinutes(1), price, price, price, price, 0);
        Assert.Equal(bar.Low, bar.High);
        Assert.Equal(0, bar.Volume);
    }

    [Fact]
    public void RejectsNegativeVolume() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new Bar(Start, TimeSpan.FromMinutes(1), 1, 1, 1, 1, -1));

    [Fact]
    public void RejectsNonUtcTimestampWithoutSilentlyConverting() => Assert.Throws<ArgumentException>(() =>
        new Bar(Start.ToOffset(TimeSpan.FromHours(1)), TimeSpan.FromMinutes(1), 1, 1, 1, 1, 1));

    [Fact]
    public void RejectsNonpositiveDurationAndEndOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bar(Start, TimeSpan.Zero, 1, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bar(Start, TimeSpan.FromTicks(-1), 1, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bar(DateTimeOffset.MaxValue, TimeSpan.FromTicks(1), 1, 1, 1, 1, 1));
    }
}
