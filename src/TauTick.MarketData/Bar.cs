namespace TauTick.MarketData;

/// <summary>An immutable OHLCV interval [StartUtc, EndUtc), with exact decimal prices.</summary>
public sealed record Bar
{
    public Bar(DateTimeOffset startUtc, TimeSpan interval, decimal open, decimal high,
        decimal low, decimal close, long volume)
    {
        if (startUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Bar starts must use a UTC offset of zero.", nameof(startUtc));
        if (interval <= TimeSpan.Zero || interval.Ticks > DateTimeOffset.MaxValue.Ticks - startUtc.Ticks)
            throw new ArgumentOutOfRangeException(nameof(interval), "The interval must be positive with a representable end.");
        if (low > high || open < low || open > high || close < low || close > high)
            throw new ArgumentException("Low <= open/close <= high is required.");
        if (volume < 0)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be negative.");

        StartUtc = startUtc;
        Interval = interval;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc => StartUtc + Interval;
    public TimeSpan Interval { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public long Volume { get; }
}
